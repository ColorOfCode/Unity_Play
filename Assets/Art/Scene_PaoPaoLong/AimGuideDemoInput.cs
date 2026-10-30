using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Controls the launcher, its two orbiting bubbles and the aimed shot.
[ExecuteAlways]
public class AimGuideDemoInput : MonoBehaviour
{
    [SerializeField] private Camera gameCamera;
    [SerializeField] private AimGuide2D guide;
    [SerializeField] private BoxCollider2D leftWall;
    [SerializeField] private BoxCollider2D rightWall;
    [SerializeField] private float wallThickness = 0.2f;
    [SerializeField] private BubbleBoard2D board;
    [SerializeField] private SpriteRenderer launcherRenderer;
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private float ballRadius = 0.25f;
    [SerializeField, Min(0.1f)] private float shotSpeed = 8f;
    [SerializeField] private float tapMaxSeconds = 0.18f;
    [SerializeField] private float tapMaxPixels = 18f;
    [SerializeField] private float launchTapRadius = 0.55f;
    [SerializeField] private int startingType = 2;
    [Tooltip("右侧预备位在开始时显示的道具类型；0 对应 AimDot0。")]
    [SerializeField] private int startingPreviewType = 0;

    [Header("发射指引光效")]
    [Tooltip("按 AimDot0 到 AimDot4 的顺序绑定对应的 AimDot*_FaShi* Prefab。")]
    [SerializeField] private GameObject[] launchEffectPrefabs = new GameObject[5];

    [Header("发射道具拖尾")]
    [Tooltip("按 AimDot0 到 AimDot4 的顺序绑定对应的 AimDot*_Particles_Fashe* Prefab。")]
    [SerializeField] private GameObject[] shotTrailPrefabs = new GameObject[5];
    [Tooltip("发射道具碰撞后，拖尾粒子渐变消失的时间（秒）。")]
    [SerializeField, Min(0.01f)] private float shotTrailFadeDuration = 0.3f;
    [Header("自动清屏")]
    [Tooltip("清屏期间两次发射之间的时间间隔（秒）。")]
    [SerializeField, Min(0f)] private float autoShotDelay = 0.2f;

    [Header("发射道具圆环")]
    [Tooltip("发射点到圆心的距离，也是圆环半径（世界单位）。")]
    [SerializeField, Min(0.1f)] private float orbitRadius = 0.6f;
    [Tooltip("圆心相对原位置的上下偏移（世界单位）；正值向上，负值向下。圆环和两个道具一起移动。")]
    [SerializeField] private float orbitCenterYOffset = 0f;
    [Tooltip("右侧预备道具相对上方发射道具的比例。")]
    [SerializeField, Range(0.01f, 1f)] private float previewScale = 0.7f;
    [Tooltip("上方道具逆时针移到左侧时的角速度（度/秒）。")]
    [SerializeField, Min(1f)] private float outgoingOrbitSpeed = 360f;
    [Tooltip("右侧预备道具逆时针移到上方时的角速度（度/秒）。")]
    [SerializeField, Min(1f)] private float incomingOrbitSpeed = 360f;
    [Tooltip("两个道具完成移动后，下一个预备道具开始出现的等待时间（秒）。")]
    [SerializeField, Min(0f)] private float previewAppearDelay = 0.05f;
    [Tooltip("下一个预备道具从 0 缩放到预备比例的时间（秒）。")]
    [SerializeField, Min(0.01f)] private float previewAppearDuration = 0.2f;
    [Tooltip("点击圆心触发切换的判定半径（世界单位）。")]
    [SerializeField, Min(0.01f)] private float centerTapRadius = 0.3f;
    [Tooltip("替代白色圆环的底图 Sprite。")]
    [SerializeField] private Sprite orbitImageSprite;
    [Tooltip("圆环底图自身的缩放；不改变发射和预备道具的圆周半径。")]
    [SerializeField, Min(0.01f)] private float orbitImageScale = 0.6f;
    [Tooltip("圆环底图逆时针自转速度（度/秒）。")]
    [SerializeField, Min(0f)] private float orbitImageRotationSpeed = 45f;
    // Keep the old scene values so previously tuned settings are not discarded.
#pragma warning disable 0414
    [SerializeField, HideInInspector] private Color orbitColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField, HideInInspector] private float orbitLineWidth = 0.025f;
#pragma warning restore 0414

    private float lastAspect = -1f;
    private float lastSize = -1f;
    private Vector3 lastCameraPosition;
    private bool holding;
    private enum PressTarget { None, Launcher, Center }
    private PressTarget pressTarget;
    private int activeFinger = -1;
    private float pressedAt;
    private Vector2 pressedScreen;
    private Vector2 aimDirection = Vector2.up;
    private int currentType;
    private int nextType;
    private GameObject currentVisual;
    private GameObject previewVisual;
    private GameObject outgoingVisual;
    private GameObject orbitObject;
    private SpriteRenderer orbitImageRenderer;
    private GameObject launchEffect;
    private int launchEffectType = -1;
    private GameObject editorCurrentPrefab;
    private GameObject editorNextPrefab;
    private bool rotating;
    private GameObject shot;
    private GameObject shotTrail;
    private readonly List<GameObject> fadingShotTrails = new List<GameObject>();
    private static readonly int ParticleColorId = Shader.PropertyToID("_Color");
    private Vector2 shotDirection;
    private int shotType;
    private Coroutine autoClearRoutine;
    private int autoShotsInFlight;
    private readonly HashSet<Collider2D> autoReserved = new HashSet<Collider2D>();
    private readonly List<GameObject> autoProjectiles = new List<GameObject>();

    private void OnEnable()
    {
        if (Application.isPlaying)
            AimNumberMinAnimation.AutoClearRequested += BeginAutoClear;
    }

    private void BeginAutoClear()
    {
        if (autoClearRoutine != null || board == null) return;
        holding = false;
        activeFinger = -1;
        pressTarget = PressTarget.None;
        if (guide != null) guide.Hide();
        board.BeginAutoClear();
        autoClearRoutine = StartCoroutine(AutoClearBoard());
    }

    private IEnumerator AutoClearBoard()
    {
        while (shot != null || rotating) yield return null;
        while (board.HasBubbles || autoShotsInFlight > 0)
        {
            if (!board.HasBubbles) { yield return null; continue; }

            int targetType;
            Vector2 direction;
            Collider2D targetCollider;
            int mask = wallMask.value | targetMask.value;
            if (!board.TryGetAutoAim(LaunchPosition(), ballRadius, mask,
                                     autoReserved,
                                     out targetType, out direction,
                                     out targetCollider))
            {
                // All remaining targets may already have shots in flight.
                yield return null;
                continue;
            }

            if (currentType != targetType)
            {
                DestroyVisual(currentVisual);
                currentType = targetType;
                currentVisual = CreateVisual(currentType, LaunchPosition(), 1f);
                ApplyLauncherSprite();
            }
            aimDirection = direction;
            if (guide != null) guide.Hide();
            GameObject projectile = CreateVisual(targetType, LaunchPosition(), 1f);
            if (projectile == null) { yield return null; continue; }
            projectile.name = "Auto_Flying_AimDot" + targetType;
            GameObject trail = StartShotTrail(targetType, projectile.transform);
            autoReserved.Add(targetCollider);
            autoProjectiles.Add(projectile);
            autoShotsInFlight++;
            StartCoroutine(FlyAutoShot(projectile, trail, targetCollider, targetType));
            float interval = Mathf.Max(0f, autoShotDelay);
            if (interval > 0f) yield return new WaitForSeconds(interval);
            else yield return null;
        }

        yield return board.FinishAutoClearAndRefill();
        AimNumberMinAnimation.NotifyAutoClearCompleted();
        autoClearRoutine = null;
    }

    private IEnumerator FlyAutoShot(GameObject projectile, GameObject trail,
                                    Collider2D targetCollider, int type)
    {
        while (projectile != null && targetCollider != null)
        {
            Vector2 target = targetCollider.bounds.center;
            Vector2 origin = projectile.transform.position;
            Vector2 direction = (target - origin).normalized;
            float stopRadius = ballRadius +
                Mathf.Max(targetCollider.bounds.extents.x, 0.01f);
            float step = Mathf.Max(0.1f, shotSpeed) * Time.fixedDeltaTime;
            Vector2 next = Vector2.MoveTowards(origin, target, step);
            if (Vector2.Distance(next, target) <= stopRadius)
            {
                Vector2 impact = target - direction * stopRadius;
                projectile.transform.position = impact;
                if (trail != null) trail.transform.position = impact;
                board.ResolveCollision(targetCollider, type, impact);
                break;
            }
            projectile.transform.position = next;
            if (trail != null) trail.transform.position = next;
            yield return new WaitForFixedUpdate();
        }

        autoReserved.Remove(targetCollider);
        autoShotsInFlight--;
        autoProjectiles.Remove(projectile);
        if (trail != null)
        {
            fadingShotTrails.Add(trail);
            StartCoroutine(FadeShotTrail(trail));
        }
        if (projectile != null) Destroy(projectile);
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        ClearVisuals();
        if (board != null) board.Initialize();
        currentType = Mathf.Clamp(startingType, 0,
            Mathf.Max(0, board == null ? 0 : board.TypeCount - 1));
        nextType = Mathf.Clamp(startingPreviewType, 0,
            Mathf.Max(0, board == null ? 0 : board.TypeCount - 1));
        ApplyLauncherSprite();
        if (board != null && board.TypeCount > 0)
        {
            currentVisual = CreateVisual(currentType, LaunchPosition(), 1f);
            previewVisual = CreateVisual(nextType, RightPosition(), PreviewRatio);
            if (currentVisual != null && previewVisual != null)
            {
                if (launcherRenderer != null) launcherRenderer.forceRenderingOff = true;
                CreateOrbitImage();
            }
            else
            {
                if (currentVisual != null) Destroy(currentVisual);
                if (previewVisual != null) Destroy(previewVisual);
                currentVisual = null;
                previewVisual = null;
            }
        }
        if (guide != null) guide.Hide();
        UpdateWalls();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            RefreshEditorPreview();
            return;
        }

        if (gameCamera == null || guide == null || board == null ||
            leftWall == null || rightWall == null || !gameCamera.orthographic)
            return;

        UpdateWalls();
        RefreshOrbitLayout();

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && activeFinger < 0)
                    BeginPress(touch.position, touch.fingerId);
                else if (holding && touch.fingerId == activeFinger)
                {
                    if (touch.phase == TouchPhase.Ended ||
                        touch.phase == TouchPhase.Canceled)
                        EndPress(touch.position, touch.phase == TouchPhase.Canceled);
                    else if (pressTarget == PressTarget.Launcher)
                        UpdateAim(touch.position);
                }
            }
        }
        else if (activeFinger < 0)
        {
            if (Input.GetMouseButtonDown(0)) BeginPress(Input.mousePosition, -1);
            else if (holding && Input.GetMouseButtonUp(0)) EndPress(Input.mousePosition, false);
            else if (holding && pressTarget == PressTarget.Launcher &&
                     Input.GetMouseButton(0)) UpdateAim(Input.mousePosition);
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        bool previewVisible = holding && pressTarget == PressTarget.Launcher &&
                              !rotating && currentVisual != null &&
                              guide != null && guide.isActiveAndEnabled &&
                              guide.IsShowing;
        if (!previewVisible)
        {
            if (launchEffect != null) launchEffect.SetActive(false);
            return;
        }

        EnsureLaunchEffectPrefab();
        if (launchEffect == null) return;

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) *
                      Mathf.Rad2Deg - 90f;
        launchEffect.transform.SetPositionAndRotation(LaunchPosition(),
            Quaternion.Euler(0f, 0f, angle));
        if (!launchEffect.activeSelf)
        {
            KeepLaunchEffectBehindBubble();
            launchEffect.SetActive(true);
            foreach (ParticleSystem particle in launchEffect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
        }
    }

    private void BeginPress(Vector2 screen, int fingerId)
    {
        if (holding || rotating || autoClearRoutine != null) return;
        Vector2 world = ScreenToWorld(screen);

        float centerRadius = Mathf.Min(Mathf.Max(0.01f, centerTapRadius),
                                       OrbitRadius * 0.8f);
        if (previewVisual != null &&
            Vector2.Distance(world, OrbitCenter()) <= centerRadius)
            pressTarget = PressTarget.Center;
        else if (shot == null && currentVisual != null &&
                 Vector2.Distance(world, LaunchPosition()) <= launchTapRadius)
            pressTarget = PressTarget.Launcher;
        else
            return;

        holding = true;
        activeFinger = fingerId;
        pressedAt = Time.time;
        pressedScreen = screen;
        if (pressTarget == PressTarget.Launcher) UpdateAim(screen);
    }

    private void UpdateAim(Vector2 screen)
    {
        Vector2 direction = ScreenToWorld(screen) - (Vector2)LaunchPosition();
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        direction.y = Mathf.Max(0.18f, direction.y);
        aimDirection = direction.normalized;
        guide.Show(LaunchPosition(), (Vector2)LaunchPosition() + aimDirection);
    }

    private void EndPress(Vector2 screen, bool cancelled)
    {
        if (!holding) return;
        PressTarget releasedTarget = pressTarget;
        holding = false;
        activeFinger = -1;
        pressTarget = PressTarget.None;
        guide.Hide();
        if (cancelled) return;

        float movedPixels = Vector2.Distance(screen, pressedScreen);
        if (releasedTarget == PressTarget.Center)
        {
            if (movedPixels <= tapMaxPixels)
                StartCoroutine(RotateQueue(currentVisual));
            return;
        }

        float heldSeconds = Time.time - pressedAt;
        if (heldSeconds <= tapMaxSeconds && movedPixels <= tapMaxPixels)
            return;

        UpdateAim(screen);
        guide.Hide();
        Fire();
    }

    private void Fire()
    {
        if (currentVisual == null || previewVisual == null || shot != null || rotating)
            return;

        shotType = currentType;
        shotDirection = aimDirection;
        shot = currentVisual;
        currentVisual = null;
        shot.name = "Flying_AimDot" + shotType;
        shotTrail = StartShotTrail(shotType, shot.transform);
        StartCoroutine(RotateQueue(null));
    }

    private void FixedUpdate()
    {
        if (shot == null) return;

        float remaining = Mathf.Max(0.1f, shotSpeed) * Time.fixedDeltaTime;
        int mask = wallMask.value | targetMask.value;
        for (int bounce = 0; bounce < 8 && remaining > 0.0001f; bounce++)
        {
            Vector2 start = shot.transform.position;
            RaycastHit2D hit = Physics2D.CircleCast(
                start + shotDirection * 0.002f, ballRadius,
                shotDirection, remaining, mask);

            if (!hit || hit.fraction <= 0f)
            {
                shot.transform.position = start + shotDirection * remaining;
                SyncShotTrail();
                break;
            }

            Vector2 center = hit.centroid;
            shot.transform.position = center;
            SyncShotTrail();
            remaining -= Vector2.Distance(start, center);

            bool wall = (wallMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (wall)
            {
                shotDirection = Vector2.Reflect(shotDirection, hit.normal).normalized;
                shot.transform.position = center + shotDirection * 0.003f;
                SyncShotTrail();
                continue;
            }

            if (!board.ResolveCollision(hit.collider, shotType, center))
                Debug.LogWarning("BubbleBoard2D: no available cell at impact.", board);
            FinishShotTrail();
            Destroy(shot);
            shot = null;
            break;
        }
    }

    private GameObject StartShotTrail(int type, Transform shotTransform)
    {
        if (shotTrailPrefabs == null || type < 0 ||
            type >= shotTrailPrefabs.Length) return null;
        GameObject prefab = shotTrailPrefabs[type];
        if (prefab == null) return null;

        // Keep runtime particles outside the shot's Prefab instance so they
        // never become part of that instance's editor overrides.
        GameObject effect = Instantiate(prefab, shotTransform.position,
            prefab.transform.rotation);
        effect.name = prefab.name;
        effect.transform.localScale = Vector3.Scale(
            prefab.transform.localScale, shotTransform.lossyScale);
        ParticleSystem particles = effect.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            Destroy(effect);
            return null;
        }

        SpriteRenderer shotRenderer = shotTransform.GetComponentInChildren<SpriteRenderer>(true);
        ParticleSystemRenderer effectRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (shotRenderer != null && effectRenderer != null)
        {
            effectRenderer.sortingLayerID = shotRenderer.sortingLayerID;
            effectRenderer.sortingOrder = shotRenderer.sortingOrder - 1;
        }
        return effect;
    }

    private void SyncShotTrail()
    {
        if (shotTrail != null && shot != null)
            shotTrail.transform.position = shot.transform.position;
    }

    private void FinishShotTrail()
    {
        GameObject effect = shotTrail;
        shotTrail = null;
        if (effect == null) return;

        // The independent emitter stays at the impact point while it fades.
        fadingShotTrails.Add(effect);
        StartCoroutine(FadeShotTrail(effect));
    }

    private IEnumerator FadeShotTrail(GameObject effect)
    {
        ParticleSystem particles = effect.GetComponent<ParticleSystem>();
        if (particles != null)
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        ParticleSystemRenderer effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
        MaterialPropertyBlock properties = null;
        Color originalColor = Color.white;
        if (effectRenderer != null && effectRenderer.sharedMaterial != null &&
            effectRenderer.sharedMaterial.HasProperty(ParticleColorId))
        {
            properties = new MaterialPropertyBlock();
            effectRenderer.GetPropertyBlock(properties);
            originalColor = effectRenderer.sharedMaterial.GetColor(ParticleColorId);
        }

        float duration = Mathf.Max(0.01f, shotTrailFadeDuration);
        for (float elapsed = 0f; elapsed < duration && effect != null;
             elapsed += Time.deltaTime)
        {
            if (properties != null && effectRenderer != null)
            {
                Color color = originalColor;
                // This particle shader multiplies _Color twice.
                color.a *= Mathf.Sqrt(1f - elapsed / duration);
                properties.SetColor(ParticleColorId, color);
                effectRenderer.SetPropertyBlock(properties);
            }
            yield return null;
        }

        fadingShotTrails.Remove(effect);
        if (effect != null) Destroy(effect);
    }

    private float OrbitRadius => Mathf.Max(0.1f, orbitRadius);
    private float PreviewRatio => Mathf.Clamp(previewScale, 0.01f, 1f);

    private Vector2 OrbitCenter()
    {
        return (Vector2)transform.position +
               Vector2.up * (orbitCenterYOffset - OrbitRadius);
    }

    private Vector3 LaunchPosition() => OrbitPosition(90f);

    private Vector3 OrbitPosition(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        Vector2 center = OrbitCenter();
        return new Vector3(center.x + OrbitRadius * Mathf.Cos(radians),
                           center.y + OrbitRadius * Mathf.Sin(radians),
                           transform.position.z);
    }

    private Vector3 RightPosition() => OrbitPosition(0f);

    private int RandomOtherType(int type)
    {
        int count = board == null ? 0 : board.TypeCount;
        if (count <= 1) return type;
        return (type + Random.Range(1, count)) % count;
    }

    private GameObject CreateVisual(int type, Vector3 position, float scale)
    {
        GameObject prefab = board == null ? null : board.PrefabForType(type);
        if (prefab == null) return null;

        GameObject visual = Instantiate(prefab, position, Quaternion.identity);
        visual.name = "Launcher_AimDot" + type;
        if (!Application.isPlaying) visual.hideFlags = HideFlags.HideAndDontSave;
        SetVisualScale(visual, scale);
        return visual;
    }

    private void SetVisualScale(GameObject visual, float scale)
    {
        if (visual == null) return;
        Vector3 baseScale = transform.lossyScale;
        visual.transform.localScale = new Vector3(
            baseScale.x * Mathf.Max(0f, scale),
            baseScale.y * Mathf.Max(0f, scale), baseScale.z);
    }

    private void RefreshEditorPreview()
    {
        if (board == null || board.TypeCount == 0)
        {
            ClearVisuals();
            return;
        }

        int displayType = Mathf.Clamp(startingType, 0, board.TypeCount - 1);
        int displayNextType = Mathf.Clamp(startingPreviewType, 0, board.TypeCount - 1);
        GameObject currentPrefab = board.PrefabForType(displayType);
        GameObject nextPrefab = board.PrefabForType(displayNextType);
        if (currentPrefab == null || nextPrefab == null)
        {
            ClearVisuals();
            return;
        }

        if (currentVisual == null || editorCurrentPrefab != currentPrefab)
        {
            DestroyVisual(currentVisual);
            currentVisual = CreateVisual(displayType, LaunchPosition(), 1f);
            editorCurrentPrefab = currentPrefab;
        }
        if (previewVisual == null || editorNextPrefab != nextPrefab)
        {
            DestroyVisual(previewVisual);
            previewVisual = CreateVisual(displayNextType, RightPosition(), PreviewRatio);
            editorNextPrefab = nextPrefab;
        }
        currentType = displayType;
        nextType = displayNextType;

        if (orbitImageRenderer == null) CreateOrbitImage();
        RefreshOrbitLayout();
        if (launcherRenderer != null)
            launcherRenderer.forceRenderingOff = currentVisual != null;
    }

    private void CreateOrbitImage()
    {
        orbitObject = new GameObject("LauncherOrbitImage");
        if (!Application.isPlaying) orbitObject.hideFlags = HideFlags.HideAndDontSave;
        orbitImageRenderer = orbitObject.AddComponent<SpriteRenderer>();
        orbitImageRenderer.sprite = orbitImageSprite;
        if (launcherRenderer != null)
        {
            orbitImageRenderer.sortingLayerID = launcherRenderer.sortingLayerID;
            orbitImageRenderer.sortingOrder = launcherRenderer.sortingOrder + 1;
        }
        RefreshOrbitLayout();
    }

    private void RefreshOrbitLayout()
    {
        if (orbitImageRenderer == null) return;

        orbitObject.transform.position = OrbitCenter();
        float scale = Mathf.Max(0.01f, orbitImageScale);
        orbitObject.transform.localScale = new Vector3(scale, scale, 1f);
        orbitImageRenderer.sprite = orbitImageSprite;
        if (Application.isPlaying)
            orbitObject.transform.Rotate(0f, 0f,
                Mathf.Max(0f, orbitImageRotationSpeed) * Time.deltaTime);
        if (rotating) return;

        if (currentVisual != null)
        {
            currentVisual.transform.position = LaunchPosition();
            SetVisualScale(currentVisual, 1f);
        }
        if (previewVisual != null)
        {
            previewVisual.transform.position = RightPosition();
            SetVisualScale(previewVisual, PreviewRatio);
        }
    }

    private IEnumerator RotateQueue(GameObject outgoing)
    {
        if (rotating || previewVisual == null) yield break;
        rotating = true;
        outgoingVisual = outgoing;
        GameObject incoming = previewVisual;
        previewVisual = null;
        currentVisual = incoming;
        currentType = nextType;
        nextType = RandomOtherType(currentType);
        ApplyLauncherSprite();

        float outgoingDuration = outgoing == null ? 0f :
            90f / Mathf.Max(1f, outgoingOrbitSpeed);
        float incomingDuration = 90f / Mathf.Max(1f, incomingOrbitSpeed);
        float duration = Mathf.Max(outgoingDuration, incomingDuration);
        SpriteRenderer outgoingRenderer = outgoing == null ? null :
            outgoing.GetComponent<SpriteRenderer>();
        float outgoingAlpha = outgoingRenderer == null ? 1f : outgoingRenderer.color.a;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float outgoingT = outgoingDuration <= 0f ? 1f :
                Mathf.Clamp01(elapsed / outgoingDuration);
            float incomingT = Mathf.Clamp01(elapsed / incomingDuration);

            if (outgoing != null)
            {
                outgoing.transform.position = OrbitPosition(90f + 90f * outgoingT);
                if (outgoingRenderer != null)
                {
                    Color color = outgoingRenderer.color;
                    color.a = outgoingAlpha * (1f - outgoingT);
                    outgoingRenderer.color = color;
                }
            }
            incoming.transform.position = OrbitPosition(90f * incomingT);
            SetVisualScale(incoming, Mathf.Lerp(PreviewRatio, 1f, incomingT));
            yield return null;
        }

        if (outgoing != null)
        {
            outgoing.transform.position = OrbitPosition(180f);
            outgoing.SetActive(false);
            Destroy(outgoing);
        }
        outgoingVisual = null;
        incoming.transform.position = LaunchPosition();
        SetVisualScale(incoming, 1f);

        float delay = Mathf.Max(0f, previewAppearDelay);
        if (delay > 0f) yield return new WaitForSeconds(delay);

        previewVisual = CreateVisual(nextType, RightPosition(), 0f);
        if (previewVisual != null)
        {
            float appearDuration = Mathf.Max(0.01f, previewAppearDuration);
            float appearElapsed = 0f;
            while (appearElapsed < appearDuration)
            {
                appearElapsed = Mathf.Min(appearDuration,
                                          appearElapsed + Time.deltaTime);
                SetVisualScale(previewVisual,
                    PreviewRatio * (appearElapsed / appearDuration));
                yield return null;
            }
            SetVisualScale(previewVisual, PreviewRatio);
        }
        rotating = false;
    }

    private void ApplyLauncherSprite()
    {
        if (board == null) return;

        if (launcherRenderer != null)
            launcherRenderer.sprite = board.SpriteForType(currentType);
        if (guide != null)
            guide.SetDotPrefab(board.PrefabForType(currentType));
        EnsureLaunchEffectPrefab();
    }

    private void EnsureLaunchEffectPrefab()
    {
        if (launchEffect != null && launchEffectType == currentType) return;
        DestroyVisual(launchEffect);
        launchEffect = null;
        launchEffectType = -1;
        if (launchEffectPrefabs == null || currentType < 0 ||
            currentType >= launchEffectPrefabs.Length) return;

        GameObject prefab = launchEffectPrefabs[currentType];
        if (prefab == null) return;
        launchEffect = Instantiate(prefab, LaunchPosition(), Quaternion.identity);
        launchEffect.name = "Launcher_FaShi_AimDot" + currentType;
        launchEffectType = currentType;
        launchEffect.SetActive(false);
    }

    private void KeepLaunchEffectBehindBubble()
    {
        if (launchEffect == null || currentVisual == null) return;
        SpriteRenderer bubbleRenderer = currentVisual.GetComponentInChildren<SpriteRenderer>(true);
        if (bubbleRenderer == null) return;

        foreach (Renderer effectRenderer in launchEffect.GetComponentsInChildren<Renderer>(true))
        {
            effectRenderer.sortingLayerID = bubbleRenderer.sortingLayerID;
            if (effectRenderer.sortingOrder >= bubbleRenderer.sortingOrder)
                effectRenderer.sortingOrder = bubbleRenderer.sortingOrder - 1;
        }
    }

    private Vector2 ScreenToWorld(Vector2 screen)
    {
        Vector3 point = new Vector3(screen.x, screen.y,
            Mathf.Abs(transform.position.z - gameCamera.transform.position.z));
        return gameCamera.ScreenToWorldPoint(point);
    }

    private void UpdateWalls()
    {
        if (gameCamera == null || leftWall == null || rightWall == null) return;
        float aspect = gameCamera.aspect;
        float halfHeight = gameCamera.orthographicSize;
        Vector3 cameraPosition = gameCamera.transform.position;

        if (Mathf.Approximately(aspect, lastAspect) &&
            Mathf.Approximately(halfHeight, lastSize) &&
            cameraPosition == lastCameraPosition)
            return;

        float thickness = Mathf.Max(0.01f, wallThickness);
        float halfWidth = halfHeight * aspect;
        float wallX = halfWidth + thickness * 0.5f;
        Vector2 wallSize = new Vector2(thickness, halfHeight * 2f + 1f);

        leftWall.size = wallSize;
        rightWall.size = wallSize;
        leftWall.transform.position = new Vector3(
            cameraPosition.x - wallX, cameraPosition.y, 0f);
        rightWall.transform.position = new Vector3(
            cameraPosition.x + wallX, cameraPosition.y, 0f);

        Physics2D.SyncTransforms();
        lastAspect = aspect;
        lastSize = halfHeight;
        lastCameraPosition = cameraPosition;
    }

    private void DestroyVisual(Object value)
    {
        if (value == null) return;
        GameObject gameObjectToHide = value as GameObject;
        if (gameObjectToHide != null) gameObjectToHide.SetActive(false);
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    private void ClearVisuals()
    {
        foreach (GameObject projectile in autoProjectiles)
            DestroyVisual(projectile);
        autoProjectiles.Clear();
        autoReserved.Clear();
        autoShotsInFlight = 0;
        foreach (GameObject effect in fadingShotTrails)
            DestroyVisual(effect);
        fadingShotTrails.Clear();
        DestroyVisual(shotTrail);
        DestroyVisual(shot);
        DestroyVisual(currentVisual);
        DestroyVisual(previewVisual);
        DestroyVisual(outgoingVisual);
        DestroyVisual(orbitObject);
        DestroyVisual(launchEffect);
        shot = null;
        shotTrail = null;
        currentVisual = null;
        previewVisual = null;
        outgoingVisual = null;
        orbitObject = null;
        orbitImageRenderer = null;
        launchEffect = null;
        launchEffectType = -1;
        editorCurrentPrefab = null;
        editorNextPrefab = null;
        if (launcherRenderer != null) launcherRenderer.forceRenderingOff = false;
    }

    private void OnDisable()
    {
        AimNumberMinAnimation.AutoClearRequested -= BeginAutoClear;
        StopAllCoroutines();
        if (autoClearRoutine != null && board != null && Application.isPlaying)
            board.CancelAutoClear();
        autoClearRoutine = null;
        if (guide != null) guide.Hide();
        ClearVisuals();
        rotating = false;
        holding = false;
        pressTarget = PressTarget.None;
        activeFinger = -1;
    }
}
