using UnityEngine;

/// <summary>
/// 挂在 Main Camera 上：
///
/// 【开关开启（followMannequin = true）—— 跟随/环绕模式】
/// 1. 相机位置跟随 SK_Mannequin 移动，WASD 按键无效
/// 2. 按住鼠标右键：左右 = 绕 SK_Mannequin 水平旋转（Y），上下 = 俯仰（X）
///    —— 全部以 SK_Mannequin 为中心点环绕，相机始终看向它
/// 3. Q / E 同样以 SK_Mannequin 为中心水平环绕
/// 4. 俯仰以开启那一刻的 X 值为中心，上下各最多 pitchMaxAngle（默认 20°）
///
/// 【开关关闭（followMannequin = false）—— 自由模式】
/// 1. WASD 按键移动相机有效（W前 S后 A左 D右，水平移动不升降）
/// 2. 鼠标方向操作（右键旋转）失效
/// 3. Q / E 以「画面中心点」为中心环绕旋转（中心点由屏幕中心射线检测得到）
/// </summary>
public class CameraRotateQE : MonoBehaviour
{
    [Header("按键旋转速度（度/秒）")]
    [Tooltip("按住 Q/E 时每秒旋转的度数")]
    public float rotateSpeed = 90f;

    [Header("鼠标灵敏度（度/像素）")]
    [Tooltip("按住右键时，鼠标每移动 1 像素对应的旋转度数（仅开关开启时生效）")]
    public float mouseSensitivity = 0.2f;

    [Tooltip("按住右键时，鼠标每移动 1 像素对应的俯仰度数（仅开关开启时生效）")]
    public float pitchSensitivity = 0.2f;

    [Header("WASD 移动速度（仅开关关闭时生效）")]
    [Tooltip("按住 WASD 时每秒移动的世界单位数")]
    public float moveSpeed = 12f;

    [Tooltip("按住 Shift 时 WASD 的加速倍数（1 = 不加速）")]
    public float shiftBoost = 4f;

    [Header("环绕中心点（自由模式）")]
    [Tooltip("Q/E 环绕时画面中心射线检测不到物体时，使用的中心点距离（米）")]
    public float orbitFallbackDistance = 10f;

    [Header("跟随开关")]
    [Tooltip("开启：跟随 SK_Mannequin，环绕中心 = SK_Mannequin；关闭：WASD 自由移动，Q/E 环绕画面中心点")]
    public bool followMannequin = true;

    [Tooltip("跟随目标（拖 SK_Mannequin 进来；留空则自动按名字查找 SK_Mannequin）")]
    public Transform followTarget;

    [Tooltip("跟随平滑时间（秒）：0 = 硬性贴合，值越大越柔和")]
    [Range(0f, 1f)]
    public float followSmoothTime = 0.1f;

    [Header("俯仰限制（X 轴，仅跟随模式）")]
    [Tooltip("以开启跟随那一刻的 X 值为中心，上/下最多可再偏移的角度（度）")]
    public float pitchMaxAngle = 20f;

    // 跟随时保持的相对偏移（开开关那一刻的相机-目标相对位置）
    private Vector3 _followOffset;
    private bool _offsetReady;
    private Vector3 _followVelocity;

    // 俯仰基准：开启跟随那一刻的 X 欧拉角（上下各 ±pitchMaxAngle 的中心点）
    private float _basePitchDeg;
    private bool _basePitchReady;

    private void Awake()
    {
        // 目标留空时自动查找场景里的 SK_Mannequin
        if (followTarget == null)
        {
            GameObject found = GameObject.Find("SK_Mannequin");
            if (found != null)
                followTarget = found.transform;
            else
                Debug.LogWarning("[CameraRotateQE] followTarget 未设置，且场景里没有名为 SK_Mannequin 的物体，跟随不会生效");
        }
    }

    private void Update()
    {
        // ================= 旋转输入：Q / E（始终可用）=================
        float yawDelta = 0f;      // 水平旋转量（度）
        float pitchDelta = 0f;    // 俯仰变化量（度）

        if (Input.GetKey(KeyCode.Q))
            yawDelta += rotateSpeed * Time.deltaTime;   // Q：正向旋转
        if (Input.GetKey(KeyCode.E))
            yawDelta -= rotateSpeed * Time.deltaTime;   // E：负向旋转

        // ============ 鼠标方向：仅开关开启（跟随模式）时可用 ============
        if (Input.GetMouseButton(1) && followMannequin)
        {
            // 右移 = 负向旋转，左移 = 正向旋转（与 E/Q 对应）
            yawDelta -= Input.GetAxis("Mouse X") * mouseSensitivity;

            // 上移 = 俯仰减小、下移 = 俯仰增大（反向手感）
            pitchDelta -= Input.GetAxis("Mouse Y") * pitchSensitivity;
        }

        // ============ 环绕旋转：绕中心点转，而不是原地转 ============
        if (yawDelta != 0f || pitchDelta != 0f)
            ApplyOrbitRotation(yawDelta, pitchDelta);

        // ============ WASD 移动：仅开关关闭（自由模式）时有效 ============
        if (!followMannequin)
        {
            // 前后方向取 forward 的水平投影：按 W 不会因俯视角向下钻、高度保持不变
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            flatForward.Normalize();

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += flatForward;     // 前（水平）
            if (Input.GetKey(KeyCode.S)) move -= flatForward;     // 后（水平）
            if (Input.GetKey(KeyCode.D)) move += transform.right; // 右
            if (Input.GetKey(KeyCode.A)) move -= transform.right; // 左

            if (move != Vector3.zero)
            {
                float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftBoost : 1f);
                transform.position += move.normalized * speed * Time.deltaTime;
            }
        }

        // ================= 跟随：开关刚打开时记录基准 ================
        if (followMannequin && !_offsetReady && followTarget != null)
        {
            _followOffset = transform.position - followTarget.position;
            _offsetReady = true;

            // 俯仰基准 = 此刻的 X 角（下一次开关重新打开时也会重新取）
            _basePitchDeg = NormalizeAngle(transform.eulerAngles.x);
            _basePitchReady = true;
        }
        if (!followMannequin)
        {
            _offsetReady = false;
            _basePitchReady = false;
        }
    }

    private void LateUpdate()
    {
        // 跟随放在 LateUpdate：目标（角色/动画）先动完，相机再贴上，避免抖动
        if (!followMannequin || followTarget == null)
            return;

        Vector3 desired = followTarget.position + _followOffset;

        if (followSmoothTime <= 0f)
        {
            transform.position = desired;                        // 硬性贴合
        }
        else
        {
            transform.position = Vector3.SmoothDamp(              // 平滑跟随
                transform.position, desired, ref _followVelocity, followSmoothTime);
        }
    }

    /// <summary>
    /// 环绕旋转：相机绕「中心点」旋转，并始终看向中心点。
    /// 中心点：跟随模式 = SK_Mannequin 位置；自由模式 = 画面中心点（射线检测）。
    /// </summary>
    private void ApplyOrbitRotation(float yawDelta, float pitchDelta)
    {
        // ---- 1. 确定环绕中心点 ----
        Vector3 pivot;
        bool isFollow = followMannequin && followTarget != null;

        if (isFollow)
        {
            pivot = followTarget.position;               // 中心 = SK_Mannequin
        }
        else
        {
            pivot = GetScreenCenterPivot();              // 中心 = 画面中心点
        }

        // ---- 2. 取当前 偏移向量 ----
        Vector3 offset = transform.position - pivot;
        float distance = offset.magnitude;
        if (distance < 0.001f) return;                   // 距离太近，无法环绕

        // ---- 3. 水平旋转：绕世界 Y 轴旋转偏移向量 ----
        if (yawDelta != 0f)
            offset = Quaternion.AngleAxis(yawDelta, Vector3.up) * offset;

        // ---- 4. 俯仰：改变偏移向量的仰角（仅跟随模式有俯仰输入）----
        if (pitchDelta != 0f)
        {
            float elev = Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f)) * Mathf.Rad2Deg;

            // 俯仰限制：以开启跟随那一刻的 X 值为中心 ± pitchMaxAngle
            float minElev = -89.5f, maxElev = 89.5f;
            if (_basePitchReady)
            {
                minElev = Mathf.Max(_basePitchDeg - pitchMaxAngle, -89.5f);
                maxElev = Mathf.Min(_basePitchDeg + pitchMaxAngle, 89.5f);
            }

            float newElev = Mathf.Clamp(elev + pitchDelta, minElev, maxElev);
            float clampedDelta = newElev - elev;
            if (Mathf.Abs(clampedDelta) > 0.0001f)
            {
                // 保持水平方向与距离不变，只改仰角
                Vector3 horiz = new Vector3(offset.x, 0f, offset.z);
                float horizLen = horiz.magnitude;
                if (horizLen < 0.0001f)
                {
                    // 几乎垂直俯视：用相机水平朝向的反方向作为水平基准
                    Vector3 flatFwd = transform.forward; flatFwd.y = 0f;
                    horiz = -flatFwd.normalized * 0.001f;
                    horizLen = horiz.magnitude;
                }

                float newElevRad = newElev * Mathf.Deg2Rad;
                float newHorizLen = distance * Mathf.Cos(newElevRad);
                Vector3 newHoriz = horiz * (newHorizLen / horizLen);

                offset = new Vector3(newHoriz.x, distance * Mathf.Sin(newElevRad), newHoriz.z);
            }
        }

        // ---- 5. 重新摆放相机：位置 = 中心 + 新偏移，朝向 = 看向中心点 ----
        transform.position = pivot + offset;
        transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

        // ---- 6. 跟随模式：同步更新跟随偏移（LateUpdate 会继续沿新方向跟随）----
        if (isFollow)
            _followOffset = offset;
    }

    /// <summary>
    /// 自由模式下的环绕中心点：从相机沿视线（画面中心）发射射线，命中物体取命中点；
    /// 什么都没打中时，取视线前方 orbitFallbackDistance 米处的点。
    /// </summary>
    private Vector3 GetScreenCenterPivot()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.point;

        return transform.position + transform.forward * orbitFallbackDistance;
    }

    /// <summary>把欧拉角归一化到 [-180, 180]（处理 360 环绕）</summary>
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }
}
