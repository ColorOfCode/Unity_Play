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

        [Header("平滑")]
        [Tooltip("平滑时间（秒），0 = 硬跟随（不做任何缓动）")]
        [Min(0f)] public float smoothTime = 0f;
        [Tooltip("最大跟随速度（米/秒）。<=0 表示不限速（内部会传 Infinity；传 0 会把相机锁死不动）")]
        [Min(0f)] public float maxSpeed = 0f;

        [Header("朝向")]
        [Tooltip("是否让相机始终看向目标（关闭则只平移、保持原朝向）")]
        public bool lookAtTarget = false;

        [Header("调试")]
        [Tooltip("运行时在 Game 视图左上角显示跟随 HUD（验证相机是否真的在动）")]
        public bool showDebugHud = true;

        Vector3 _velocity;

        void Start()
        {
            Transform t = ResolveFollowTarget();
            if (autoOffsetOnStart && t != null)
                offset = transform.position - t.position;
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

            // 固定世界偏移：相机与目标的相对位置 = 你在编辑器里摆好的机位
            Vector3 desired = t.position + offset;
            if (smoothTime > 0f)
            {
                // 注意：SmoothDamp 的 maxSpeed 传 0 会把位移钳制为 0（相机完全不动），
                // 因此 0 表示不限速时必须传 Infinity
                float speedLimit = maxSpeed > 0f ? maxSpeed : Mathf.Infinity;
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime, speedLimit);
            }
            else
            {
                transform.position = desired;
            }

            if (lookAtTarget)
            {
                Vector3 lookDir = t.position - transform.position;
                if (lookDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }
        }

        /// <summary>运行时 HUD：显示跟随目标与相机实时世界坐标</summary>
        void OnGUI()
        {
            if (!showDebugHud || !Application.isPlaying) return;
            Transform t = ResolveFollowTarget();
            string info = t != null
                ? $"跟随目标: {t.name}\n目标位置: {t.position:F2}\n相机位置: {transform.position:F2}\n偏移: {offset:F2}"
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
