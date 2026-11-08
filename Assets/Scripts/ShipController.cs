using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// WASD 平移控制器（不转向）：
    /// - W 向世界 +Z 前进，S 向 -Z 后退，A 向 -X 左移，D 向 +X 右移
    /// - 只改 X/Z 位置，Y 轴保持不变，物体朝向完全不变
    /// - 使用旧版 Input Manager（Project Settings > Player > Active Input Handling = Input Manager (Old)）
    /// </summary>
    /// <summary>WASD 的移动参考坐标系</summary>
    public enum MoveSpace
    {
        World,       // 世界轴：W = +Z
        ShipForward, // 沿船头
        Camera       // 沿相机看的方向（推荐）
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/WASD 平移控制器")]
    public class ShipController : MonoBehaviour
    {
        [Header("移动目标")]
        [Tooltip("实际被移动的物体（留空则移动本物体自己）")]
        public Transform moveTarget;

        [Header("移动参数")]
        [Tooltip("最大移动速度（米/秒）")]
        [Min(0f)] public float moveSpeed = 8f;
        [Tooltip("加速度（米/秒²），0 表示不做加速直接达到最大速度")]
        [Min(0f)] public float acceleration = 40f;
        [Tooltip("是否允许斜向移动归一化（防止斜着走更快）")]
        public bool normalizeDiagonal = true;
        [Tooltip("是否允许用方向键 ↑↓←→ 代替 WASD")]
        public bool alsoArrowKeys = true;
        [Tooltip("允许 A/D 横移和 S 后退（含对应方向键）；关闭时只保留 W/上方向键前进。")]
        public bool enableADS = false;
        [Header("移动坐标空间")]
        [Tooltip("Camera（默认）= 沿相机看的方向，W 永远朝画面里走；ShipForward = 沿船头；World = 世界轴")]
        public MoveSpace moveSpace = MoveSpace.Camera;
        [Tooltip("判断船头朝向的物体（留空自动取第一个带 MeshRenderer 的子物体，即模型）")]
        public Transform forwardSource;
        [Tooltip("船头方向修正（度）：模型朝向反了就填 180")]
        public float forwardOffsetDeg = 0f;

        [Header("调试")]
        [Tooltip("在 Scene 视图绘制移动方向（仅运行时）")]
        public bool drawDebugGizmo = false;

        Vector3 _velocity; // 当前速度（仅 X/Z 有用）
        ShuimianShipTerrainCollision _terrainCollision;

        /// <summary>当前实际移动的 Transform</summary>
        public Transform Target => moveTarget != null ? moveTarget : transform;

        /// <summary>船头方向（水平分量，已应用 forwardOffsetDeg 修正；模型朝向决定，非根物体朝向）</summary>
        public Vector3 ForwardFlat
        {
            get
            {
                Transform src = forwardSource;
                if (src == null)
                {
                    var mr = GetComponentInChildren<MeshRenderer>();
                    src = mr != null ? mr.transform : Target;
                }
                Vector3 f = src.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
                f.Normalize();
                if (forwardSource == null && ShuimianShipGeometry.TryFootprint(Target, f, out var hull)
                    && hull.width > hull.length * 1.1f)
                    f = Vector3.Cross(Vector3.up, f).normalized;
                if (Mathf.Abs(forwardOffsetDeg) > 0.001f)
                    f = Quaternion.AngleAxis(forwardOffsetDeg, Vector3.up) * f;
                return f;
            }
        }

        void Update()
        {
            Vector2 input = ReadInput();
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (normalizeDiagonal && dir.sqrMagnitude > 1f) dir.Normalize();

            // 按所选坐标系换算前/右方向
            if (moveSpace == MoveSpace.ShipForward)
            {
                Vector3 fwd = ForwardFlat;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized; // 船头右侧
                dir = fwd * dir.z + right * dir.x;
                if (dir.sqrMagnitude > 1f) dir.Normalize();
            }
            else if (moveSpace == MoveSpace.Camera)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 f = cam.transform.forward; f.y = 0f;
                    Vector3 r = cam.transform.right; r.y = 0f;
                    if (f.sqrMagnitude > 0.0001f) f.Normalize(); else f = Vector3.forward;
                    if (r.sqrMagnitude > 0.0001f) r.Normalize(); else r = Vector3.right;
                    dir = f * dir.z + r * dir.x;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                }
            }

            Vector3 targetVelocity = dir * moveSpeed;
            if (acceleration > 0f)
                _velocity = Vector3.MoveTowards(_velocity, targetVelocity, acceleration * Time.deltaTime);
            else
                _velocity = targetVelocity;

            if (_velocity.sqrMagnitude <= 0.000001f) return;

            Vector3 requested = _velocity * Time.deltaTime;
            Vector3 actual = Move(requested);
            if (Time.deltaTime > 0f && (actual - requested).sqrMagnitude > .000001f)
                _velocity = actual / Time.deltaTime;
        }

        /// <summary>Move horizontally through the same terrain constraints used by WASD.</summary>
        public Vector3 Move(Vector3 displacement)
        {
            displacement.y = 0f;
            if (_terrainCollision == null) _terrainCollision = GetComponent<ShuimianShipTerrainCollision>();
            Transform t = Target;
            if (_terrainCollision != null && _terrainCollision.isActiveAndEnabled)
                displacement = _terrainCollision.ConstrainMove(t, displacement);
            t.position += displacement;
            return displacement;
        }

        Vector2 ReadInput()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;

            if (alsoArrowKeys)
            {
                if (Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            }
            return FilterMovementInput(new Vector2(x, y), enableADS);
        }
        public static Vector2 FilterMovementInput(Vector2 input, bool allowADS)
            => new Vector2(allowADS ? Mathf.Clamp(input.x, -1f, 1f) : 0f,
                Mathf.Clamp(input.y, allowADS ? -1f : 0f, 1f));

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawDebugGizmo || !Application.isPlaying) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(Target.position + Vector3.up * 0.6f, _velocity);
        }
#endif
    }
}
