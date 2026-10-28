using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// 鼠标视角控制：
    /// - 鼠标 Y（上下移动）→ 相机俯仰：向上最多 pitchUpLimit 度，向下最多 pitchDownLimit 度
    /// - 鼠标 X（左右移动）→ 船的左右旋转（绕 Y 轴偏航），相机朝向同步
    /// - WASD 移动方向不变（由 ShipController 负责），鼠标只负责转向和俯仰
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/鼠标视角控制")]
    public class MouseLookController : MonoBehaviour
    {
        [Header("控制对象")]
        [Tooltip("左右旋转的物体（船）")]
        public Transform yawTarget;
        [Tooltip("上下俯仰的物体（相机）")]
        public Transform cameraTarget;

        [Header("灵敏度")]
        [Tooltip("左右（偏航）灵敏度：度/像素")]
        [Min(0f)] public float sensitivityX = 3f;
        [Tooltip("上下（俯仰）灵敏度：度/像素")]
        [Min(0f)] public float sensitivityY = 2f;
        [Tooltip("反转 Y 轴（鼠标上移=向下看）")]
        public bool invertY = false;

        [Header("俯仰限制（度）")]
        [Tooltip("向上最大角度")]
        [Range(0f, 89f)] public float pitchUpLimit = 60f;
        [Tooltip("向下最大角度")]
        [Range(0f, 89f)] public float pitchDownLimit = 30f;
        [Tooltip("相机朝向的额外偏航修正：模型朝向反了（FBX 常见 -Z 朝前）时填 180")]
        public float yawOffsetDeg = 0f;

        [Header("平滑")]
        [Tooltip("平滑系数（0~1，越大跟手越快；1 = 不平滑）")]
        [Range(0.01f, 1f)] public float smoothing = 1f;

        [Header("光标")]
        [Tooltip("运行时锁定并隐藏鼠标光标，按 Esc 解锁")]
        public bool lockCursor = true;

        float _yaw;      // 累计偏航（度）
        float _pitch;    // 当前俯仰（度，正=向上看）
        float _yawSmooth;
        float _pitchSmooth;
        Quaternion _baseRotation;  // 船的初始朝向（保留 FBX 自带的旋转，不覆盖）
        float _cameraBaseYaw;      // 相机的初始左右朝向（保持不变，不受鼠标影响）
        bool _hasBase;             // 初始朝向是否已记录
        bool _cursorLocked;

        void Awake()
        {
            AutoWire();
        }

        void Start()
        {
            // 进入运行时才记录基准朝向，保证拿到的是 Play 开始那一刻的机位
            AutoWire();
            if (cameraTarget != null)
                _cameraBaseYaw = cameraTarget.eulerAngles.y;
            if (yawTarget != null && !_hasBase)
            {
                _baseRotation = yawTarget.rotation;
                _hasBase = true;
            }
        }

        /// <summary>自动接线：相机取 Camera.main，船取场景里的 ShipController 移动目标</summary>
        void AutoWire()
        {
            if (cameraTarget == null)
            {
                var cam = Camera.main;
                if (cam != null) cameraTarget = cam.transform;
            }
            if (yawTarget == null)
            {
                var ctrl = FindObjectOfType<ShipController>();
                if (ctrl != null) yawTarget = ctrl.Target;
            }
            // 初始朝向只记录一次，避免把已经转过的结果当成基准
            if (yawTarget != null && !_hasBase)
            {
                _baseRotation = yawTarget.rotation;
                _hasBase = true;
            }
        }

        void Update()
        {
            // 引用缺失时低频重试（避免每帧 FindObjectOfType）
            if ((yawTarget == null || cameraTarget == null) && Time.frameCount % 30 == 0)
                AutoWire();

            HandleCursor();

            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            _yaw += mx * sensitivityX;
            _pitch += my * sensitivityY * (invertY ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch, -pitchDownLimit, pitchUpLimit);

            // 0~1 的平滑系数，1 表示立即跟手
            _yawSmooth = Mathf.Lerp(_yawSmooth, _yaw, smoothing);
            _pitchSmooth = Mathf.Lerp(_pitchSmooth, _pitch, smoothing);

            // 船左右旋转：在初始朝向上叠加世界 Y 轴旋转（不覆盖模型自带朝向）
            if (yawTarget != null)
                yawTarget.rotation = Quaternion.AngleAxis(_yawSmooth, Vector3.up) * _baseRotation;

            // 相机：只改俯仰（上下），左右朝向保持初始机位不变，不跟随鼠标也不跟随船
            if (cameraTarget != null)
                cameraTarget.rotation = Quaternion.Euler(_pitchSmooth, _cameraBaseYaw + yawOffsetDeg, 0f);
        }

        void HandleCursor()
        {
            if (!lockCursor) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _cursorLocked = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Input.GetMouseButtonDown(0) && !_cursorLocked)
            {
                _cursorLocked = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (!_cursorLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _cursorLocked = true;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>运行时 HUD：显示鼠标输入累计值与旋转目标，确认是否真的在接收鼠标</summary>
        void OnGUI()
        {
            if (!Application.isPlaying) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUI.color = new Color(0.5f, 1f, 0.5f);
            GUI.Label(new Rect(10, 120, 620, 70),
                $"鼠标偏航 yaw={_yaw:F1}°  俯仰 pitch={_pitch:F1}°\n" +
                $"旋转目标: {(yawTarget ? yawTarget.name : "null")}  相机: {(cameraTarget ? cameraTarget.name : "null")}",
                style);
        }

        /// <summary>右键菜单：归零当前视角</summary>
        [ContextMenu("重置视角")]
        void ResetLook()
        {
            _yaw = _pitch = _yawSmooth = _pitchSmooth = 0f;
            if (yawTarget != null)
            {
                _baseRotation = yawTarget.rotation;
                _hasBase = true;
            }
        }
    }
}
