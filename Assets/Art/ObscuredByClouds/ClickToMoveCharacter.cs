using UnityEngine;

namespace ObscuredByClouds.Gameplay
{
    /// <summary>
    /// 俯视角「点哪跑哪」角色控制器：
    /// 1) 点一次：角色记下落点，一路跑到那个位置，跑动朝向始终盯着目标点（速度在 Inspector 里开放）
    /// 2) 按住拖动：目标点跟着光标实时更新，松开后继续跑到最后的光标位置
    /// 3) 到达之后 / 没有目标时：原地播放待机动作
    /// 4) 每次点击落点调用一次特效 FX_Tap
    ///
    /// 前提：Anim_DSS3_Idle / Anim_DSS3_Run_F 都是原地动作，位移由本脚本驱动 transform，
    ///       所以 Animator 的 Apply Root Motion 必须为关闭（本工程已是关闭状态）。
    ///
    /// 动画走 Animator 参数 + 过渡（IsRun: Bool → Idle / Run_F 两条 0.15s 过渡），
    /// 这样过渡由 Animator 自己插值，脚本不再逐帧干预状态，动作不会抖。
    /// 若 Controller 里没这个参数（比如换了别的控制器），自动退回按状态名 CrossFade。
    ///
    /// 朝向不再靠「猜模型正前方是哪个轴」，而是启动时用骨骼位置量出来：
    /// 用 foot_l → ball_l 的向量当模型正前方（脚踝→脚掌前掌，天然指向前方），
    /// 用 pelvis → spine_01 当模型的“上”。这样不管 FBX 是 Z-up 还是 Y-up、
    /// 场景根节点被转成什么角度，朝向都是算出来的，不需要手工填偏移。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ObscuredByClouds/点击移动角色 (Click To Move)")]
    public class ClickToMoveCharacter : MonoBehaviour
    {
        [Header("引用（留空会自动查找）")]
        [Tooltip("角色 Animator，留空则自动从自身 / 子物体上取")]
        public Animator animator;
        [Tooltip("发射鼠标射线的相机，留空用 Camera.main")]
        public Camera clickCamera;
        [Tooltip("点击落点生成的特效预制体（FX_Tap）")]
        public GameObject fxPrefab;

        [Header("动画状态名（必须与 Controller 里的状态同名）")]
        [Tooltip("待机状态名")]
        public string idleState = "Anim_DSS3_Idle";
        [Tooltip("向前跑状态名")]
        public string runState = "Anim_DSS3_Run_F";
        [Tooltip("驱动 Idle / Run 切换的 Animator Bool 参数名。" +
                 "Controller 里没有这个参数时会自动退回按状态名 CrossFade")]
        public string runBoolParam = "IsRun";
        [Tooltip("Idle / Run 之间的过渡时长（秒）。" +
                 "菜单 Tools → ObscuredByClouds → 修复：Idle→Run 过渡 会把这个值写进 Animator 的两条过渡里；" +
                 "只有在退回 CrossFade 时才在运行时直接生效")]
        [Min(0.02f)] public float animBlendTime = 0.15f;

        [Header("脚步节奏")]
        [Tooltip("自动把动画播放速度贴合实际移动速度，减少脚滑")]
        public bool matchRunAnimToSpeed = true;
        [Tooltip("跑动动画在播放速度=1 时对应的地面速度（米/秒）。" +
                 "调法：看角色跑起来脚是滑还是倒腾太快，滑就把这个值调大，太快就调小")]
        [Min(0.1f)] public float runAnimRefSpeed = 4.2f;

        [Header("移动")]
        [Tooltip("跑动速度（米/秒），运行时可随时改这个值")]
        [Min(0f)] public float moveSpeed = 5f;
        [Tooltip("离目标点多近算到达（米）")]
        [Min(0.01f)] public float stopDistance = 0.08f;
        [Tooltip("转身角速度（度/秒）；填 0 或负数 = 瞬间转向")]
        public float turnSpeedDeg = 720f;
        [Tooltip("起跑 / 刹车的速度过渡时间（秒），0 = 立刻满速。" +
                 "和 0.15s 的动作过渡配合，起步不会像被弹出去")]
        [Min(0f)] public float accelTime = 0.12f;
        [Tooltip("按住左键时目标点实时跟随光标（松开后仍会跑到最后的位置）。" +
                 "取消勾选：只有按下那一刻的目标点生效")]
        public bool holdToMove = true;

        [Header("朝向修正")]
        [Tooltip("朝向微调（度）。正前方是启动时用脚掌/脊柱骨骼量出来的，" +
                 "正常情况不用动；如果实测整体偏了一点（比如偏 5°、偏 15°），在这里补")]
        public float facingTrimDeg = 0f;
        [Tooltip("朝向校准用的骨骼名（一般不用改）。顺序：脚、脚掌前段、盆骨、脊柱")]
        public string boneFootL = "foot_l";
        public string boneBallL = "ball_l";
        public string bonePelvis = "pelvis";
        public string boneSpine = "spine_01";

        [Header("地面")]
        [Tooltip("勾选：每帧向下打射线贴合地面高度（本场景是平地，可不勾）")]
        public bool stickToGround = false;
        [Tooltip("贴地射线检测的层")]
        public LayerMask groundMask = ~0;
        [Tooltip("光标没打到任何碰撞体时，按水平地面高度做投影（俯视角基本不会失败）")]
        public bool useGroundPlaneFallback = true;

        [Header("点击特效")]
        [Tooltip("特效尺寸倍率")]
        [Min(0.01f)] public float fxScale = 1f;
        [Tooltip("相对落点的偏移，默认略微抬高一点，避免和地面 z-fighting")]
        public Vector3 fxOffset = new Vector3(0f, 0.02f, 0f);
        [Tooltip("特效自动销毁时间（秒）；0 = 按粒子系统时长自动算")]
        [Min(0f)] public float fxLifetime = 0f;

        [Header("鼠标拾取")]
        [Tooltip("参与鼠标拾取的层（角色自身会被自动忽略）")]
        public LayerMask clickMask = ~0;
        [Tooltip("射线最大长度")]
        [Min(1f)] public float rayLength = 500f;

        [Header("调试")]
        [Tooltip("运行时左上角显示速度 / 状态")]
        public bool showDebugHud = false;

        // ---- 运行时状态 ----
        Transform _tr;
        Quaternion _baseRotation;   // 出场时的世界朝向，转身时只在它基础上叠加绕 Y 的偏航
        float _baseY;
        float _baseYawDeg;          // 出场时模型实际的正前方（用骨骼量出来的，单位度）
        float _currentYawDeg;       // 给 HUD 显示用的当前朝向
        Vector3 _modelForward = Vector3.forward;
        Vector3 _modelUp = Vector3.up;
        bool _hasTarget;
        Vector3 _target;
        int _currentStateHash;
        int _idleHash;
        int _runHash;
        bool _useAnimatorParam;     // Controller 里有没有 runBoolParam 这个 Bool
        bool _runAnimState;         // 当前驱动值（true = 应该在跑）
        float _speedMul;            // 起跑/刹车的速度系数 0~1
        float _lastAnimSpeed = 1f;
        readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        /// <summary>跑动速度（米/秒），可直接在别的脚本里读写</summary>
        public float Speed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        /// <summary>当前是否在跑</summary>
        public bool IsRunning => _hasTarget;

        /// <summary>当前目标点</summary>
        public Vector3 Target => _target;

        void Awake()
        {
            _tr = transform;
            _baseRotation = _tr.rotation;
            _baseY = _tr.position.y;
            _idleHash = Animator.StringToHash(idleState);
            _runHash = Animator.StringToHash(runState);

            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (clickCamera == null) clickCamera = Camera.main;

            CalibrateFacing();
        }

        /// <summary>
        /// 用骨骼位置量出模型真正的“正前方”，不再依赖 FBX 的坐标轴约定。
        /// 脚踝 → 前脚掌 天然指向前方；盆骨 → 脊柱 天然指向上方。
        /// </summary>
        void CalibrateFacing()
        {
            Transform footL = FindBone(boneFootL);
            Transform ballL = FindBone(boneBallL);
            Transform pelvis = FindBone(bonePelvis);
            Transform spine = FindBone(boneSpine);

            // ---- 上方向：盆骨 → 脊柱 ----
            Vector3 up = Vector3.up;
            if (pelvis != null && spine != null)
            {
                Vector3 candidate = spine.position - pelvis.position;
                if (candidate.sqrMagnitude > 1e-6f) up = candidate.normalized;
            }
            _modelUp = up;

            // ---- 正前方：脚踝 → 前脚掌，去掉竖直分量后水平归一化 ----
            Vector3 forward = Vector3.zero;
            if (footL != null && ballL != null)
            {
                forward = ballL.position - footL.position;
                forward -= Vector3.Dot(forward, up) * up;
            }

            if (forward.sqrMagnitude < 1e-6f)
            {
                // 骨骼没找到（或名字对不上）：退回用根节点的 forward 水平投影
                forward = _tr.forward;
                forward -= Vector3.Dot(forward, up) * up;
            }
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;

            _modelForward = forward.normalized;
            _baseYawDeg = Mathf.Atan2(_modelForward.x, _modelForward.z) * Mathf.Rad2Deg;
            _currentYawDeg = _baseYawDeg;

            string src = (footL != null && ballL != null) ? "骨骼" : "根节点 forward 兜底";
            Debug.Log($"[ClickToMoveCharacter] 朝向校准（{src}）: 出场正前方 yaw = {_baseYawDeg:F1}°," +
                      $" forward = {_modelForward:F3}, 模型上方向 = {_modelUp:F3}" +
                      $"（与世界上方向夹角 {Vector3.Angle(_modelUp, Vector3.up):F1}°）", this);
        }

        Transform FindBone(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return null;
            if (_tr.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase)) return _tr;

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase)) return t;

            return null;
        }

        void Start()
        {
            if (animator == null)
            {
                Debug.LogError("[ClickToMoveCharacter] 没找到 Animator，动画不会播放。", this);
            }
            else if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[ClickToMoveCharacter] Animator 上没有 Controller。", this);
            }
            // 注意：这套动作是 Generic（逐骨骼 Transform 曲线），Avatar 为空才是正常的，
            // 千万不要给 Animator 塞 Humanoid Avatar，会把播放搞坏。

            ResolveAnimParam();

            // 进场状态确定：先落到 Idle，再开始用参数驱动
            _currentStateHash = 0;
            _runAnimState = true;                 // 让下面这次 SetRunState(false) 一定执行
            if (animator != null && _useAnimatorParam && animator.isActiveAndEnabled)
                animator.Play(_idleHash, 0, 0f);
            SetRunState(false, true);
            ApplyAnimPlaybackSpeed(0f);

            if (_useAnimatorParam)
                Debug.Log($"[ClickToMoveCharacter] 动画由参数 {runBoolParam} 驱动，" +
                          $"过渡交给 Animator 的 Idle/Run 过渡线（{animBlendTime:0.00}s）。", this);
            else
                Debug.LogWarning($"[ClickToMoveCharacter] Controller 里没有 Bool 参数「{runBoolParam}」，" +
                                 "已退回按状态名 CrossFade。想要过渡效果请执行菜单 " +
                                 "Tools → ObscuredByClouds → 修复：Idle→Run 过渡。", this);
        }

        void ResolveAnimParam()
        {
            _useAnimatorParam = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (string.IsNullOrEmpty(runBoolParam)) return;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool &&
                    p.name == runBoolParam)
                {
                    _useAnimatorParam = true;
                    return;
                }
            }
        }

        void Update()
        {
            Camera cam = clickCamera != null ? clickCamera : Camera.main;

            // ---- 1. 点击：落点特效 + 记录目标点（点一次就会一直跑到那里） ----
            if (Input.GetMouseButtonDown(0) && cam != null)
            {
                if (TryGetGroundPoint(cam, out Vector3 point))
                {
                    SpawnFx(point);
                    SetTarget(point);
                }
            }

            // ---- 2. 按住拖动：目标点实时跟随光标；一松开就停在最后的位置，然后继续跑到那儿 ----
            if (holdToMove && Input.GetMouseButton(0) && cam != null)
            {
                if (TryGetGroundPoint(cam, out Vector3 point)) SetTarget(point);
            }

            // ---- 3. 位移 + 动画 ----
            float dt = Time.deltaTime;

            // 速度系数：起步/停下各用 accelTime 做一段缓变，动作过渡和位移起步同步
            float targetMul = _hasTarget ? 1f : 0f;
            float rate = accelTime > 0.001f ? dt / accelTime : 1f;
            _speedMul = Mathf.MoveTowards(_speedMul, targetMul, rate);

            if (_hasTarget) MoveStep(dt);
            else PlayIdle();

            ApplyAnimPlaybackSpeed(dt);
        }

        // ================= 移动 =================

        /// <summary>设置目标点（世界坐标）</summary>
        public void SetTarget(Vector3 worldPoint)
        {
            _hasTarget = true;
            _target = worldPoint;
        }

        /// <summary>清除目标点，立刻回到待机</summary>
        public void ClearTarget()
        {
            _hasTarget = false;
            PlayIdle();
        }

        /// <summary>立即停下（外部调用）</summary>
        public void StopNow() => ClearTarget();

        void MoveStep(float dt)
        {
            Vector3 toTarget = _target - _tr.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;

            if (dist <= stopDistance)
            {
                // 到达了：切待机并清掉目标（清掉之后就算角色被别的东西推开也不会自己再跑）
                _hasTarget = false;
                _currentYawDeg = _baseYawDeg;   // 只是给 HUD 用
                PlayIdle();
                return;
            }

            Vector3 dir = toTarget / dist;

            // ---- 转身：以「启动时量出来的模型正前方」为零点，绕世界 Y 只转偏航，
            //      所以模型自己带什么 FBX 轴向修正都不会被破坏 ----
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float deltaYaw = Mathf.DeltaAngle(_baseYawDeg, yaw) + facingTrimDeg;
            Quaternion face = Quaternion.AngleAxis(deltaYaw, Vector3.up) * _baseRotation;
            _tr.rotation = turnSpeedDeg > 0f
                ? Quaternion.RotateTowards(_tr.rotation, face, turnSpeedDeg * dt)
                : face;

            // HUD 用的当前朝向（把旋转量换算回 yaw）
            Vector3 curForward = Quaternion.AngleAxis(deltaYaw, Vector3.up) * _modelForward;
            _currentYawDeg = Mathf.Atan2(curForward.x, curForward.z) * Mathf.Rad2Deg;

            // ---- 位移：最后一步不越过目标点 ----
            float step = moveSpeed * Mathf.Max(_speedMul, 0.02f) * dt;
            Vector3 next = step >= dist - stopDistance
                ? _target
                : _tr.position + dir * step;
            next.y = ResolveGroundY(next);
            _tr.position = next;

            PlayRun();
        }

        float ResolveGroundY(Vector3 worldPos)
        {
            if (!stickToGround) return _baseY;

            Vector3 origin = new Vector3(worldPos.x, _baseY + 2f, worldPos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, groundMask,
                    QueryTriggerInteraction.Ignore) && !IsSelf(hit.collider.transform))
                return hit.point.y;

            return _baseY;
        }

        // ================= 动画 =================

        void PlayIdle() => SetRunState(false);
        void PlayRun() => SetRunState(true);

        /// <summary>
        /// 统一切换待机 / 跑动：
        /// 优先用 Animator 的 Bool 参数（过渡由 Animator 的过渡线插值，脚本不插手中途）；
        /// Controller 里没有这个参数时退回按状态名 CrossFade。
        /// </summary>
        void SetRunState(bool run, bool force = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                _runAnimState = run;
                return;
            }
            if (!force && _runAnimState == run) return;
            _runAnimState = run;

            if (_useAnimatorParam)
            {
                if (animator.isActiveAndEnabled) animator.SetBool(runBoolParam, run);
                return;
            }

            int hash = run ? _runHash : _idleHash;
            if (!force && _currentStateHash == hash) return;
            _currentStateHash = hash;
            CrossFade(hash);
        }

        /// <summary>把动画播放速度贴合实际移动速度，减少脚滑（dt = 0 时直接到位）</summary>
        void ApplyAnimPlaybackSpeed(float dt)
        {
            if (animator == null) return;

            float target = 1f;
            if (matchRunAnimToSpeed && _runAnimState && runAnimRefSpeed > 0.01f)
                target = Mathf.Clamp(moveSpeed / runAnimRefSpeed, 0.35f, 2.5f);

            _lastAnimSpeed = dt > 0f
                ? Mathf.MoveTowards(_lastAnimSpeed, target, dt * 4f)
                : target;
            animator.speed = _lastAnimSpeed;
        }

        void CrossFade(int stateHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!animator.isActiveAndEnabled) return;

            if (animBlendTime > 0f) animator.CrossFadeInFixedTime(stateHash, animBlendTime, 0, 0f);
            else animator.Play(stateHash, 0, 0f);
        }

        // ================= 鼠标 → 地面点 =================

        bool TryGetGroundPoint(Camera cam, out Vector3 point)
        {
            point = default;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            int count = Physics.RaycastNonAlloc(ray, _hitBuffer, rayLength, clickMask,
                QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.collider == null) continue;
                // 角色自身（含子物体碰撞体）不参与拾取，否则光标压在角色身上时点不到地面
                if (IsSelf(hit.collider.transform)) continue;
                if (hit.distance >= nearest) continue;

                nearest = hit.distance;
                point = hit.point;
                found = true;
            }

            if (found)
            {
                point.y = ResolveGroundY(point);
                return true;
            }

            // 兜底：直接和角色脚下的水平面求交（俯视角一定命中）
            if (useGroundPlaneFallback)
            {
                Plane ground = new Plane(Vector3.up, new Vector3(0f, _baseY, 0f));
                if (ground.Raycast(ray, out float enter))
                {
                    point = ray.GetPoint(enter);
                    point.y = _baseY;
                    return true;
                }
            }

            return false;
        }

        bool IsSelf(Transform t)
        {
            return t == _tr || t.IsChildOf(_tr);
        }

        // ================= 点击特效 =================

        void SpawnFx(Vector3 groundPoint)
        {
            if (fxPrefab == null) return;

            Vector3 pos = groundPoint + fxOffset;
            GameObject go = Instantiate(fxPrefab, pos, fxPrefab.transform.rotation);
            go.name = fxPrefab.name + "_Click";
            if (!Mathf.Approximately(fxScale, 1f))
                go.transform.localScale = fxPrefab.transform.localScale * fxScale;

            float life = fxLifetime;
            if (life <= 0f)
            {
                life = 0.2f;
                foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    // FX_Tap 里有一个 looping 的子发射器，不关掉只能靠定时销毁，会闪一下被切
                    if (main.loop) main.loop = false;
                    ps.Play(true);
                    life = Mathf.Max(life, main.duration + main.startLifetime.constantMax);
                }
            }

            Destroy(go, life);
        }

        // ================= 调试 =================

        void OnGUI()
        {
            if (!showDebugHud || !Application.isPlaying) return;

            string info = $"速度: {moveSpeed:F2} m/s   {(turnSpeedDeg > 0f ? $"{turnSpeedDeg:F0}°/s" : "瞬转")}\n" +
                          $"状态: {(_hasTarget ? "跑 (" + runState + ")" : "待机 (" + idleState + ")")}\n" +
                          $"朝向: 当前 {_currentYawDeg:F0}° / 出场基准 {_baseYawDeg:F0}°\n" +
                          $"位置: {_tr.position:F2}" +
                          (_hasTarget ? $" → 目标: {_target:F2}" : "");

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUI.color = new Color(1f, 0.92f, 0.2f);
            GUI.Label(new Rect(10f, 10f, 620f, 90f), info, style);
        }
    }
}
