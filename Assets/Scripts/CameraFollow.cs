using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// 相机跟随：
    /// - 保持与目标的固定世界偏移（默认取运行/初始化时的初始偏移）
    /// - LateUpdate 中跟随并做平滑插值，避免抖动
    /// - 自动识别 ShipController：即使 target 拖的是 Ship 根（不动的父物体），
    ///   也会自动改成跟随「控制器真正移动的那个物体」，避免相机跟了个不动的物体
    /// - 可选让相机始终朝向目标
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/相机跟随")]
    public class CameraFollow : MonoBehaviour
    {
        [Header("跟随目标")]
        [Tooltip("跟随的目标物体（拖 Ship 或 Cube 都可以）")]
        public Transform target;
        [Tooltip("留空会自动在 target 的父级链上查找 ShipController，并跟随它真正移动的物体")]
        public ShipController controller;

        [Header("偏移")]
        [Tooltip("相机相对目标的世界坐标偏移（X 左右、Y 高度、Z 前后）")]
        public Vector3 offset = new Vector3(0f, 1f, -10f);
        [Tooltip("未手动设置偏移时，用场景初始位置自动计算偏移")]
        public bool autoOffsetOnStart = true;

        [Header("回弹（弹性跟随）")]
        [Tooltip("0 = 完全不回弹（相机硬性贴合目标，跟随最稳）；值越大，追上目标时的回弹/过冲越明显")]
        [Range(0f, 0.95f)] public float followBounce = 0f;
        [Tooltip("回弹追击刚度：越大追得越快、回弹频率越高幅度越小；越小则软绵绵地荡回来")]
        [Min(1f)] public float followStiffness = 60f;

        [Header("跟随船旋转")]
        [Tooltip("船转向时，相机绕着船同步旋转（始终保持在船的相对方位，例如正后方）。" +
                 "只取水平的偏航增量，不受模型自身倾斜影响，起始机位保持你在编辑器里摆的样子")]
        public bool followTargetYaw = true;
        [Tooltip("相机朝向也跟着船的偏航一起转（转向后镜头始终对着船）。" +
                 "如果相机上挂了 MouseLookController，朝向由它处理，这里请关闭")]
        public bool rotateCameraWithTarget = false;

        [Header("朝向")]
        [Tooltip("是否让相机始终看向目标（关闭则只平移、保持原朝向）")]
        public bool lookAtTarget = false;

        [Header("调试")]
        [Tooltip("运行时在 Game 视图左上角显示跟随 HUD（验证相机是否真的在动）")]
        public bool showDebugHud = true;

        Vector3 _velocity;

        float _baseYawDeg;        // 初始时刻目标的水平朝向
        float _cameraBaseYaw;     // 初始时刻相机自身的左右朝向
        Transform _yawBaseTarget; // 基准对应的物体（换目标时重新记录）
        bool _yawBaseReady;

        /// <summary>目标相对初始朝向的水平偏航增量（度），可被其它脚本读取统一朝向</summary>
        public float YawDeltaDeg { get; private set; }

        void Start()
        {
            Transform t = ResolveFollowTarget();
            if (autoOffsetOnStart && t != null)
                offset = transform.position - t.position;
            EnsureYawBase(t);
        }

        /// <summary>解析真正要跟随的 Transform（优先用控制器真正的移动目标）</summary>
        public Transform ResolveFollowTarget()
        {
            if (controller == null && target != null)
                controller = target.GetComponentInParent<ShipController>();
            if (controller != null && controller.enabled)
                return controller.Target;
            return target;
        }

        void LateUpdate()
        {
            Transform t = ResolveFollowTarget();
            if (t == null) return;

            // ---- 环绕：船转向时，偏移绕 Y 轴同步旋转 ----
            Vector3 worldOffset = offset;
            if (followTargetYaw)
            {
                EnsureYawBase(t);
                YawDeltaDeg = Mathf.DeltaAngle(_baseYawDeg, PlanarYawDeg(FlatForward(t)));
                worldOffset = Quaternion.AngleAxis(YawDeltaDeg, Vector3.up) * offset;
            }
            else
            {
                YawDeltaDeg = 0f;
            }

            Vector3 desired = t.position + worldOffset;

            if (followBounce > 0f)
            {
                // 弹簧-阻尼：followBounce 越大 → 阻尼比越小 → 越过目标位再荡回来（回弹）
                float zeta = Mathf.Clamp(1f - followBounce, 0.05f, 1f);
                float omega = Mathf.Sqrt(followStiffness);
                float dt = Mathf.Min(Time.deltaTime, 1f / 20f);

                // 分子步积分，保证大风大 elastic 系数下也稳定
                int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 8);
                float h = dt / steps;
                for (int i = 0; i < steps; i++)
                {
                    Vector3 accel = (desired - transform.position) * followStiffness
                                    - _velocity * (2f * zeta * omega);
                    _velocity += accel * h;
                    transform.position += _velocity * h;
                }
            }
            else
            {
                // 不回弹：硬性贴合，任何旧的平滑参数都不生效
                _velocity = Vector3.zero;
                transform.position = desired;
            }

            if (lookAtTarget)
            {
                Vector3 lookDir = t.position - transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }
            else if (rotateCameraWithTarget && followTargetYaw)
            {
                // 相机自身没有 MouseLookController 时，用它保证镜头对着转过来的船
                Vector3 e = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(e.x, _cameraBaseYaw + YawDeltaDeg, 0f);
            }
        }

        /// <summary>记录基准朝向（只在目标变更或首帧时取，避免把已转过的结果当成基准）</summary>
        void EnsureYawBase(Transform t)
        {
            if (t == null) return;
            if (_yawBaseReady && _yawBaseTarget == t) return;
            _baseYawDeg = PlanarYawDeg(FlatForward(t));
            _cameraBaseYaw = transform.eulerAngles.y;
            _yawBaseTarget = t;
            _yawBaseReady = true;
        }

        /// <summary>目标的水平朝向：优先用控制器算出的船头（含 forwardOffsetDeg 修正）</summary>
        Vector3 FlatForward(Transform t)
        {
            if (controller != null)
                return controller.ForwardFlat;
            Vector3 f = t.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            return f.normalized;
        }

        /// <summary>水平方向 → 绕 Y 轴的角度（度）</summary>
        public static float PlanarYawDeg(Vector3 flatForward)
        {
            return Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;
        }

        /// <summary>运行时 HUD：显示跟随目标与相机实时世界坐标</summary>
        void OnGUI()
        {
            if (!showDebugHud || !Application.isPlaying) return;
            Transform t = ResolveFollowTarget();
            string info = t != null
                ? $"跟随目标: {t.name}\n目标位置: {t.position:F2}\n相机位置: {transform.position:F2}\n" +
                  $"回弹: {followBounce:F2}（0=不回弹）  偏移: {offset:F2}"
                : "跟随目标: null（未设置！）";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            GUI.color = new Color(1f, 0.92f, 0.2f);
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            GUI.Label(new Rect(10, 10, 620, 110), info, style);
        }

        /// <summary>右键菜单：打印当前接线情况，便于排查</summary>
        [ContextMenu("打印当前跟随目标")]
        void LogTargetInfo()
        {
            var t = ResolveFollowTarget();
            Debug.Log($"[CameraFollow] target={(target ? target.name : "null")}, " +
                      $"controller={(controller ? controller.name : "null")}, " +
                      $"实际跟随={(t ? t.name : "null")}, offset={offset}", this);
        }
    }
}
