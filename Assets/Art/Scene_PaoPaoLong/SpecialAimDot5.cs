using System.Collections;
using Spine.Unity;
using UnityEngine;

// AimDot5 is a scene-only special item. It never joins the launcher queue or
// the board's color matching and drop rules.
public class SpecialAimDot5 : MonoBehaviour
{
    public static event System.Action ActivationStarted;
    public static event System.Action ReachedDestination;

    [SerializeField] private BubbleBoard2D board;
    [SerializeField] private Camera gameCamera;
    [SerializeField] private RectTransform destinationIcon;
    [SerializeField] private Vector2Int boardCell = new Vector2Int(5, 2);
    [SerializeField] private GameObject activationEffectPrefab;

    [Header("激活轨迹")]
    [Tooltip("开始下沉时的重力加速度，单位为世界单位/秒²。")]
    [SerializeField, Min(0.1f)] private float fallGravity = 8f;
    [Tooltip("重力下沉的持续时间（秒）。")]
    [SerializeField, Min(0.01f)] private float fallDuration = 0.28f;
    [Tooltip("从下沉处丝滑上浮到目标的持续时间（秒）。")]
    [SerializeField, Min(0.01f)] private float riseDuration = 1.3f;
    [Tooltip("抵达目标后回弹缩小至消失的持续时间（秒）。")]
    [SerializeField, Min(0.01f)] private float disappearDuration = 0.3f;

    private const string IdleAnimation = "Panda_Small_Spine1";
    private const string ActivationAnimation = "Panda_Small_Spine2";
    private const string MovingAnimation = "Panda_Small_Spine3";
    private SkeletonAnimation skeletonAnimation;
    private bool activated;

    public bool IsActivating => activated;

    public struct SpawnSettings
    {
        public Camera gameCamera;
        public RectTransform destinationIcon;
        public GameObject activationEffectPrefab;
        public float fallGravity;
        public float fallDuration;
        public float riseDuration;
        public float disappearDuration;
    }

    public SpawnSettings CaptureSpawnSettings()
    {
        return new SpawnSettings
        {
            gameCamera = gameCamera,
            destinationIcon = destinationIcon,
            activationEffectPrefab = activationEffectPrefab,
            fallGravity = fallGravity,
            fallDuration = fallDuration,
            riseDuration = riseDuration,
            disappearDuration = disappearDuration
        };
    }

    public void Configure(BubbleBoard2D owner, Vector2Int cell,
                          SpawnSettings settings)
    {
        board = owner;
        boardCell = cell;
        gameCamera = settings.gameCamera;
        destinationIcon = settings.destinationIcon;
        activationEffectPrefab = settings.activationEffectPrefab;
        fallGravity = settings.fallGravity;
        fallDuration = settings.fallDuration;
        riseDuration = settings.riseDuration;
        disappearDuration = settings.disappearDuration;
    }

    public void ShiftDownRows(int rows, float rowSpacing)
    {
        if (activated || rows <= 0) return;
        if (board != null) board.ReleaseSpecialCell(boardCell);
        boardCell.y += rows;
        transform.position += Vector3.down * (rows * rowSpacing);
        if (board != null) board.ReserveSpecialCell(boardCell);
    }

    private void Awake()
    {
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (board == null) board = GetComponentInParent<BubbleBoard2D>();
        if (board == null) return;
        board.RegisterSpecial(this);
        board.ReserveSpecialCell(boardCell);
        board.BoardChanged += TryActivate;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        if (!activated) PlayAnimation(IdleAnimation, true);
        TryActivate();
    }

    private void OnDisable()
    {
        if (board == null) return;
        board.BoardChanged -= TryActivate;
        board.UnregisterSpecial(this);
        board.ReleaseSpecialCell(boardCell);
    }

    private Spine.TrackEntry PlayAnimation(string animationName, bool loop)
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        if (skeletonAnimation == null) return null;
        skeletonAnimation.Initialize(false);
        if (skeletonAnimation.AnimationState != null)
            return skeletonAnimation.AnimationState.SetAnimation(0, animationName, loop);
        return null;
    }

    private void PlayDestinationEffect()
    {
        if (activationEffectPrefab == null) return;

        // Keep the burst at the destination while the special item scales away.
        GameObject effect = Instantiate(activationEffectPrefab, transform.position,
            activationEffectPrefab.transform.rotation, board != null ? board.transform : null);
        ParticleSystem rootParticles = effect.GetComponent<ParticleSystem>();
        if (rootParticles == null)
        {
            Destroy(effect, 10f);
            return;
        }

        // This prefab loops by default. Play one cycle and remove the instance
        // when its particles finish, without changing the original prefab.
        ParticleSystem.MainModule main = rootParticles.main;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;
    }

    private void TryActivate()
    {
        if (activated || board == null || board.HasBubbleBelow(boardCell)) return;
        activated = true;
        StartCoroutine(Activate());
    }

    private IEnumerator Activate()
    {
        Camera cameraToUse = gameCamera != null ? gameCamera : Camera.main;
        if (cameraToUse == null || destinationIcon == null)
        {
            Debug.LogWarning("AimDot5: missing camera or destination icon.", this);
            activated = false;
            if (board != null) board.ReserveSpecialCell(boardCell);
            PlayAnimation(IdleAnimation, true);
            yield break;
        }

        // Stay in place for one full Spine2 play, regardless of its playback speed.
        Spine.TrackEntry activation = PlayAnimation(ActivationAnimation, false);
        ActivationStarted?.Invoke();
        if (activation != null)
            yield return new WaitForSpineAnimationComplete(activation);
        PlayAnimation(MovingAnimation, true);
        // Keep the board cell occupied while Spine2 plays in place.
        if (board != null) board.ReleaseSpecialCell(boardCell);

        Vector3 start = transform.position;
        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(
            cameraToUse, destinationIcon.position);
        float depth = Mathf.Abs(start.z - cameraToUse.transform.position.z);
        Vector3 target = cameraToUse.ScreenToWorldPoint(
            new Vector3(targetScreen.x, targetScreen.y, depth));
        target.z = start.z;

        float gravity = Mathf.Max(0.1f, fallGravity);
        float fallTime = Mathf.Max(0.01f, fallDuration);
        float riseTime = Mathf.Max(0.01f, riseDuration);
        float elapsed = 0f;
        while (elapsed < fallTime)
        {
            elapsed = Mathf.Min(fallTime, elapsed + Time.deltaTime);
            transform.position = start + Vector3.down *
                (0.5f * gravity * elapsed * elapsed);
            yield return null;
        }

        Vector3 bottom = transform.position;
        // Match the downward velocity at the transition, then smoothly turn
        // upward and come to rest at the icon using a cubic Hermite curve.
        Vector3 entryTangent = Vector3.down * (gravity * fallTime * riseTime);
        elapsed = 0f;
        while (elapsed < riseTime)
        {
            elapsed = Mathf.Min(riseTime, elapsed + Time.deltaTime);
            float t = elapsed / riseTime;
            float t2 = t * t;
            float t3 = t2 * t;
            transform.position = (2f * t3 - 3f * t2 + 1f) * bottom +
                                 (t3 - 2f * t2 + t) * entryTangent +
                                 (-2f * t3 + 3f * t2) * target;
            yield return null;
        }

        transform.position = target;
        ReachedDestination?.Invoke();
        PlayDestinationEffect();
        Vector3 originalScale = transform.localScale;
        float duration = Mathf.Max(0.01f, disappearDuration);
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = elapsed / duration;
            // Ease-in-back gives a small outward rebound before shrinking to zero.
            const float overshoot = 2.2f;
            float scale = 1f - t * t * ((overshoot + 1f) * t - overshoot);
            transform.localScale = originalScale * scale;
            yield return null;
        }

        if (board != null) board.NotifySpecialCleared(this);
        Destroy(gameObject);
    }
}
