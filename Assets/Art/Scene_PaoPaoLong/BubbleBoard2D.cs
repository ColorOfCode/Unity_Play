using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hexagonal bubble board used by the VFX scene.
public class BubbleBoard2D : MonoBehaviour
{
    [SerializeField] private GameObject[] bubblePrefabs = new GameObject[5];
    [SerializeField] private Collider2D ceiling;
    [SerializeField] private Camera gameCamera;
    [Header("连消提示")]
    [SerializeField] private GameObject coolCelebrationPrefab;
    [SerializeField] private GameObject amazingCelebrationPrefab;
    [Tooltip("一次消除至少多少个道具时播放 Cool；包含发射道具、同色消除道具和失去支撑后掉落的道具。")]
    [SerializeField, Min(1)] private int coolCelebrationMinCount = 9;
    [Tooltip("一次消除至少多少个道具时播放 Amazing；包含发射道具、同色消除道具和失去支撑后掉落的道具。达到两个门槛时优先播放 Amazing。")]
    [SerializeField, Min(1)] private int amazingCelebrationMinCount = 10;
    [Header("道具消失数字")]
    [SerializeField] private GameObject matchNumberPrefab;
    [SerializeField] private GameObject dropNumberPrefab;
    [Header("道具消失粒子")]
    [Tooltip("按 AimDot0 到 AimDot4 的顺序绑定对应的粒子 Prefab。")]
    [SerializeField] private GameObject[] disappearanceParticlePrefabs = new GameObject[5];
    [Tooltip("每个消失粒子特效的存活时间（秒），结束后自动清除。")]
    [SerializeField, Min(0.01f)] private float disappearanceParticleLifetime = 1f;
    [SerializeField] private float bubbleRadius = 0.25f;
    [SerializeField] private float visualScale = 0.9f;
    [SerializeField] private float horizontalSpacing = 0.501761f;
    [SerializeField] private float verticalSpacing = 0.431338f;
    [SerializeField] private float topRowY = 4.058099f;
    [SerializeField] private float evenRowLeftX = -2.306338f;
    [SerializeField] private float oddRowLeftX = -2.561620f;
    [SerializeField] private int maxRows = 14;
    [Header("顶部补排")]
    [Tooltip("棋盘剩余的非空横排不超过此值时，在本次消除/掉落结束后补排。")]
    [SerializeField, Min(0)] private int refillAtRowCount = 4;
    [Tooltip("每次从顶部补充的横排数量。")]
    [SerializeField, Min(1)] private int refillRows = 3;
    [Tooltip("补排时每组同色道具至少相连的数量；零散格子会尽量并入相邻组。")]
    [SerializeField, Min(1)] private int refillMinConnectedCount = 4;
    [Tooltip("AimDot5 的外观 Prefab；特殊道具消除后的下一次补排会生成一个。")]
    [SerializeField] private GameObject specialVisualPrefab;
    [Header("撞击回弹")]
    [Tooltip("回弹半径（格）。设置为 2 时覆盖碰撞点周围 5×5 格。")]
    [SerializeField, Min(0)] private int recoilRadiusCells = 2;
    [Tooltip("最近道具的最大回弹位移，单位为世界单位；向外围自动衰减。")]
    [SerializeField, Min(0f)] private float recoilStrength = 0.16f;
    [SerializeField, Min(0.01f)] private float recoilDuration = 0.35f;
    [SerializeField] private float popDuration = 0.16f;

    [Header("同色消除")]
    [Tooltip("从被撞到的道具开始，向外逐颗消失的时间间隔（秒）；0 表示同时消失。")]
    [SerializeField, Min(0f)] private float matchPopInterval = 0.2f;

    [Header("悬空道具掉落")]
    [Tooltip("整组悬空道具上弹启动的最大时间差（秒）。0 表示同一帧一起上弹；值越大，错落感越强。")]
    [SerializeField, Min(0f)] private float dropLaunchTimingSpread = 0.12f;
    // Preserve the scene's tuned timing weights while the visible control
    // limits the total delay independently of the number of falling bubbles.
    [SerializeField, HideInInspector] private float dropStaggerInterval = 0.07f;
    [SerializeField, HideInInspector] private float dropDelayJitter = 0.08f;
    [Tooltip("上弹力度范围（初速度，世界单位/秒）。每个道具随机取值；值越大，弹起越快。")]
    [SerializeField, Min(0.1f)] private float dropLaunchSpeedMin = 2.5f;
    [SerializeField, Min(0.1f)] private float dropLaunchSpeedMax = 4.5f;
    [Tooltip("上弹距离范围（世界单位）。每个道具随机取值，到达这个高度后再开始下落。")]
    [SerializeField, Min(0.01f)] private float dropRiseDistanceMin = 0.35f;
    [SerializeField, Min(0.01f)] private float dropRiseDistanceMax = 0.95f;
    [SerializeField, Range(0f, 85f)] private float dropFanHalfAngle = 45f;
    [SerializeField, Min(0.1f)] private float dropGravityMin = 8f;
    [SerializeField, Min(0.1f)] private float dropGravityMax = 11f;
    [Tooltip("距摄像机画面下边缘的像素距离。到达后道具停住并淡出。")]
    [SerializeField, Min(0f)] private float fadeStartPixels = 300f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
    [Header("掉落拖尾")]
    [Tooltip("按 AimDot0 到 AimDot4 的顺序绑定；仅失去支撑并开始上弹的道具播放。")]
    [SerializeField] private GameObject[] dropTrailParticlePrefabs = new GameObject[5];

    private static readonly int ParticleColorId = Shader.PropertyToID("_Color");

    private class Bubble
    {
        public Vector2Int cell;
        public int type;
        public Transform root;
        public Transform visual;
        public SpriteRenderer renderer;
        public CircleCollider2D collider;
        public Vector3 visualHome;
        public Coroutine recoil;
    }

    private readonly Dictionary<Vector2Int, Bubble> cells = new Dictionary<Vector2Int, Bubble>();
    private readonly Dictionary<Collider2D, Bubble> byCollider = new Dictionary<Collider2D, Bubble>();
    private RectTransform numberEffectsRoot;
    private bool specialCellReserved;
    private Vector2Int reservedSpecialCell;
    private SpecialAimDot5 specialItem;
    private SpecialAimDot5.SpawnSettings specialSpawnSettings;
    private bool hasSpecialSpawnSettings;
    private bool specialSpawnPending;
    private bool refillPending;
    private int removalAnimations;
    private int rowParityOffset;
    private bool initialized;

    public int TypeCount => bubblePrefabs == null ? 0 : bubblePrefabs.Length;
    public float TopRowY => topRowY;
    public event System.Action BoardChanged;

    public void RegisterSpecial(SpecialAimDot5 item)
    {
        specialItem = item;
        specialSpawnSettings = item.CaptureSpawnSettings();
        hasSpecialSpawnSettings = true;
    }

    public void UnregisterSpecial(SpecialAimDot5 item)
    {
        if (specialItem == item) specialItem = null;
    }

    public void NotifySpecialCleared(SpecialAimDot5 item)
    {
        if (specialItem != item) return;
        specialItem = null;
        specialSpawnPending = true;
        TryScheduleRefill();
    }

    public void ReserveSpecialCell(Vector2Int cell)
    {
        reservedSpecialCell = cell;
        specialCellReserved = true;
    }

    public void ReleaseSpecialCell(Vector2Int cell)
    {
        if (specialCellReserved && reservedSpecialCell == cell)
            specialCellReserved = false;
    }

    public bool HasBubbleBelow(Vector2Int cell)
    {
        Initialize();
        foreach (Vector2Int neighbor in Neighbors(cell))
            if (neighbor.y == cell.y + 1 && cells.ContainsKey(neighbor))
                return true;
        return false;
    }

    private void Awake() => Initialize();

    private void Start() => TryScheduleRefill();

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        foreach (Transform child in transform)
        {
            CircleCollider2D bubbleCollider = child.GetComponent<CircleCollider2D>();
            SpriteRenderer sprite = child.GetComponentInChildren<SpriteRenderer>();
            if (bubbleCollider == null || sprite == null) continue;

            int type = TypeForSprite(sprite.sprite);
            if (type < 0)
            {
                Debug.LogWarning("BubbleBoard2D: unknown sprite on " + child.name, child);
                continue;
            }

            Vector2Int cell = CellForWorld(child.position);
            if (cells.ContainsKey(cell))
            {
                Debug.LogWarning("BubbleBoard2D: duplicate grid cell " + cell, child);
                continue;
            }

            Bubble bubble = new Bubble
            {
                cell = cell,
                type = type,
                root = child,
                visual = sprite.transform,
                renderer = sprite,
                collider = bubbleCollider,
                visualHome = sprite.transform.localPosition
            };
            cells.Add(cell, bubble);
            byCollider.Add(bubbleCollider, bubble);
        }
    }

    public GameObject PrefabForType(int type)
    {
        if (bubblePrefabs == null || type < 0 || type >= bubblePrefabs.Length)
            return null;
        return bubblePrefabs[type];
    }

    public Sprite SpriteForType(int type)
    {
        GameObject prefab = PrefabForType(type);
        SpriteRenderer renderer = prefab == null ? null : prefab.GetComponent<SpriteRenderer>();
        return renderer == null ? null : renderer.sprite;
    }

    public bool ResolveCollision(Collider2D hit, int shotType, Vector2 impactCenter)
    {
        Initialize();

        if (hit == ceiling)
        {
            Vector2Int topSlot;
            if (!FindTopSlot(impactCenter.x, out topSlot)) return false;
            return ResolvePlacedBubble(AddBubble(topSlot, shotType),
                                       topSlot, impactCenter);
        }

        Bubble touched;
        if (!byCollider.TryGetValue(hit, out touched)) return false;

        Vector2Int impactCell = touched.cell;
        if (touched.type == shotType)
        {
            // Capture the physical connections at impact before the matching
            // bubbles are removed and the remaining pieces separate.
            HashSet<Vector2Int> connectedAtImpact = ConnectedForRecoil(impactCell);
            List<Bubble> matching = ConnectedSameColor(touched);
            // The flying bubble disappears too, although it never enters cells.
            ShowNumberEffect(matchNumberPrefab,
                new Vector3(impactCenter.x, impactCenter.y, transform.position.z));
            SpawnDisappearanceParticles(shotType,
                new Vector3(impactCenter.x, impactCenter.y, transform.position.z));
            PopMatching(matching, 1);
            RecoilAround(impactCell, impactCenter, connectedAtImpact);
        }
        else
        {
            Vector2Int slot;
            if (!FindNearbySlot(impactCell, impactCenter, out slot)) return false;
            return ResolvePlacedBubble(AddBubble(slot, shotType),
                                       impactCell, impactCenter);
        }

        return true;
    }

    private bool ResolvePlacedBubble(Bubble placed, Vector2Int recoilCell,
                                     Vector2 impactCenter)
    {
        if (placed == null) return false;

        // The first collider hit can be a different color, while the snapped
        // cell is adjacent to a matching group. Match from the final cell.
        HashSet<Vector2Int> connectedAtImpact = ConnectedForRecoil(recoilCell);
        List<Bubble> matching = ConnectedSameColor(placed);
        if (matching.Count > 1)
            PopMatching(matching);

        RecoilAround(recoilCell, impactCenter, connectedAtImpact);
        TryScheduleRefill();
        return true;
    }

    private int TypeForSprite(Sprite sprite)
    {
        for (int i = 0; i < TypeCount; i++)
            if (SpriteForType(i) == sprite) return i;
        return -1;
    }

    private Vector2Int CellForWorld(Vector2 world)
    {
        int row = Mathf.RoundToInt((topRowY - world.y) / verticalSpacing);
        float left = (row + rowParityOffset) % 2 == 0
            ? evenRowLeftX : oddRowLeftX;
        int column = Mathf.RoundToInt((world.x - left) / horizontalSpacing);
        return new Vector2Int(column, row);
    }

    private Vector2 WorldForCell(Vector2Int cell)
    {
        float left = (cell.y + rowParityOffset) % 2 == 0
            ? evenRowLeftX : oddRowLeftX;
        return new Vector2(left + cell.x * horizontalSpacing,
                           topRowY - cell.y * verticalSpacing);
    }

    private IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
    {
        int x = cell.x;
        int y = cell.y;
        yield return new Vector2Int(x - 1, y);
        yield return new Vector2Int(x + 1, y);

        int left = (y + rowParityOffset) % 2 == 0 ? x : x - 1;
        int right = left + 1;
        if (y > 0)
        {
            yield return new Vector2Int(left, y - 1);
            yield return new Vector2Int(right, y - 1);
        }
        yield return new Vector2Int(left, y + 1);
        yield return new Vector2Int(right, y + 1);
    }

    private bool CanOccupy(Vector2Int cell)
    {
        if (cell.y < 0 || cell.y > maxRows || cells.ContainsKey(cell) ||
            (specialCellReserved && cell == reservedSpecialCell)) return false;
        Camera cameraToUse = gameCamera != null ? gameCamera : Camera.main;
        if (cameraToUse == null) return true;
        float halfWidth = cameraToUse.orthographicSize * cameraToUse.aspect;
        float x = WorldForCell(cell).x - cameraToUse.transform.position.x;
        return Mathf.Abs(x) <= halfWidth - bubbleRadius - 0.02f;
    }

    private bool FindTopSlot(float impactX, out Vector2Int result)
    {
        result = default(Vector2Int);
        float best = float.PositiveInfinity;
        bool found = false;
        for (int column = -2; column <= 20; column++)
        {
            Vector2Int cell = new Vector2Int(column, 0);
            if (!CanOccupy(cell)) continue;
            float distance = Mathf.Abs(WorldForCell(cell).x - impactX);
            if (distance >= best) continue;
            best = distance;
            result = cell;
            found = true;
        }
        return found;
    }

    private bool FindNearbySlot(Vector2Int hitCell, Vector2 impact,
                                out Vector2Int result)
    {
        result = default(Vector2Int);
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { hitCell };
        List<Vector2Int> frontier = new List<Vector2Int> { hitCell };

        for (int depth = 1; depth <= 12; depth++)
        {
            List<Vector2Int> next = new List<Vector2Int>();
            float best = float.PositiveInfinity;
            bool found = false;

            foreach (Vector2Int cell in frontier)
            {
                foreach (Vector2Int neighbor in Neighbors(cell))
                {
                    if (!visited.Add(neighbor)) continue;
                    if (neighbor.y > maxRows) continue;
                    next.Add(neighbor);
                    if (!CanOccupy(neighbor)) continue;

                    float distance = (WorldForCell(neighbor) - impact).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    result = neighbor;
                    found = true;
                }
            }

            if (found) return true;
            frontier = next;
        }
        return false;
    }

    private Bubble AddBubble(Vector2Int cell, int type)
    {
        GameObject prefab = PrefabForType(type);
        if (prefab == null) return null;

        GameObject wrapper = new GameObject($"Bubble_R{cell.y:00}_C{cell.x:00}_AimDot{type}");
        wrapper.layer = LayerMask.NameToLayer("Target");
        wrapper.transform.SetParent(transform, false);
        wrapper.transform.position = WorldForCell(cell);

        CircleCollider2D bubbleCollider = wrapper.AddComponent<CircleCollider2D>();
        bubbleCollider.radius = bubbleRadius;
        GameObject visualObject = Instantiate(prefab, wrapper.transform);
        visualObject.name = prefab.name;
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localScale = new Vector3(visualScale, visualScale, 1f);
        SpriteRenderer sprite = visualObject.GetComponent<SpriteRenderer>();

        Bubble bubble = new Bubble
        {
            cell = cell,
            type = type,
            root = wrapper.transform,
            visual = visualObject.transform,
            renderer = sprite,
            collider = bubbleCollider,
            visualHome = Vector3.zero
        };
        cells.Add(cell, bubble);
        byCollider.Add(bubbleCollider, bubble);
        Physics2D.SyncTransforms();
        return bubble;
    }

    private int OccupiedRowCount()
    {
        HashSet<int> rows = new HashSet<int>();
        foreach (Vector2Int cell in cells.Keys) rows.Add(cell.y);
        return rows.Count;
    }

    private void TryScheduleRefill()
    {
        if (refillPending || OccupiedRowCount() > Mathf.Max(0, refillAtRowCount))
            return;
        refillPending = true;
        StartCoroutine(RefillWhenReady());
    }

    private IEnumerator RefillWhenReady()
    {
        // Let the current collision and any deferred Destroy calls finish first.
        yield return null;
        while (removalAnimations > 0 ||
               (specialItem != null && specialItem.IsActivating))
            yield return null;

        if (OccupiedRowCount() <= Mathf.Max(0, refillAtRowCount))
            AddTopRows(Mathf.Max(1, refillRows));
        refillPending = false;
    }

    private void AddTopRows(int rowCount)
    {
        // Adding an odd number of hex rows flips their left/right offset.
        // Preserve every existing bubble's X and its original neighbors.
        List<Bubble> existing = new List<Bubble>(cells.Values);
        cells.Clear();
        rowParityOffset = (rowParityOffset + rowCount) & 1;
        Vector3 drop = Vector3.down * (rowCount * verticalSpacing);
        foreach (Bubble bubble in existing)
        {
            bubble.cell.y += rowCount;
            bubble.root.position += drop;
            cells.Add(bubble.cell, bubble);
        }
        if (specialItem != null)
            specialItem.ShiftDownRows(rowCount, verticalSpacing);

        Vector2Int? specialCell = null;
        if (specialSpawnPending && specialItem == null &&
            specialVisualPrefab != null && hasSpecialSpawnSettings)
        {
            // Use the middle new row so a complete new row supports AimDot5.
            int specialRow = rowCount >= 3 ? 1 : 0;
            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int column = -2; column <= 20; column++)
            {
                Vector2Int cell = new Vector2Int(column, specialRow);
                if (!CanOccupy(cell)) continue;
                foreach (Vector2Int neighbor in Neighbors(cell))
                {
                    if (neighbor.y != specialRow + 1 || !CanOccupy(neighbor))
                        continue;
                    candidates.Add(cell);
                    break;
                }
            }
            if (candidates.Count > 0)
                specialCell = candidates[Random.Range(0, candidates.Count)];
        }

        // Plan connected same-color patches before creating any new bubbles.
        List<Vector2Int> newCells = new List<Vector2Int>();
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = -2; column <= 20; column++)
            {
                Vector2Int cell = new Vector2Int(column, row);
                if (specialCell.HasValue && cell == specialCell.Value) continue;
                if (CanOccupy(cell)) newCells.Add(cell);
            }
        }
        if (TypeCount > 0)
        {
            Dictionary<Vector2Int, int> plannedTypes = PlanRefillTypes(newCells);
            foreach (Vector2Int cell in newCells)
                AddBubble(cell, plannedTypes[cell]);
        }

        if (specialCell.HasValue)
            SpawnSpecial(specialCell.Value);
        Physics2D.SyncTransforms();
        BoardChanged?.Invoke();
    }

    private Dictionary<Vector2Int, int> PlanRefillTypes(List<Vector2Int> slots)
    {
        Dictionary<Vector2Int, int> planned = new Dictionary<Vector2Int, int>(slots.Count);
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(slots);
        int minimum = Mathf.Max(1, refillMinConnectedCount);

        while (remaining.Count > 0)
        {
            List<Vector2Int> seeds = new List<Vector2Int>(remaining);
            Vector2Int seed = seeds[Random.Range(0, seeds.Count)];
            List<Vector2Int> group = new List<Vector2Int> { seed };
            remaining.Remove(seed);

            List<Vector2Int> frontier = new List<Vector2Int>();
            HashSet<Vector2Int> inFrontier = new HashSet<Vector2Int>();
            AddRefillFrontier(seed, remaining, frontier, inFrontier);
            int targetSize = Mathf.Min(remaining.Count + 1,
                                       minimum + Random.Range(0, 3));
            while (group.Count < targetSize && frontier.Count > 0)
            {
                int index = Random.Range(0, frontier.Count);
                Vector2Int cell = frontier[index];
                frontier.RemoveAt(index);
                inFrontier.Remove(cell);
                if (!remaining.Remove(cell)) continue;
                group.Add(cell);
                AddRefillFrontier(cell, remaining, frontier, inFrontier);
            }

            int type = ChooseRefillType(group, planned, group.Count < minimum);
            foreach (Vector2Int cell in group) planned.Add(cell, type);
        }
        return planned;
    }

    private void AddRefillFrontier(Vector2Int cell, HashSet<Vector2Int> remaining,
                                   List<Vector2Int> frontier,
                                   HashSet<Vector2Int> inFrontier)
    {
        foreach (Vector2Int neighbor in Neighbors(cell))
            if (remaining.Contains(neighbor) && inFrontier.Add(neighbor))
                frontier.Add(neighbor);
    }

    private int ChooseRefillType(List<Vector2Int> group,
                                 Dictionary<Vector2Int, int> planned,
                                 bool mustJoinNeighbor)
    {
        List<int> touchingNewTypes = new List<int>();
        int[] touchingExisting = new int[TypeCount];
        foreach (Vector2Int cell in group)
        {
            foreach (Vector2Int neighbor in Neighbors(cell))
            {
                int plannedType;
                Bubble existing;
                if (planned.TryGetValue(neighbor, out plannedType))
                    touchingNewTypes.Add(plannedType);
                else if (cells.TryGetValue(neighbor, out existing) &&
                         existing.type >= 0 && existing.type < TypeCount)
                    touchingExisting[existing.type]++;
            }
        }

        // A short remainder inherits a touching patch's color, so its full
        // connected group still reaches the minimum size.
        if (mustJoinNeighbor && touchingNewTypes.Count > 0)
            return touchingNewTypes[Random.Range(0, touchingNewTypes.Count)];

        // At the seam, continue a color already present in the old board.
        int bestScore = 0;
        List<int> bestExistingTypes = new List<int>();
        for (int type = 0; type < TypeCount; type++)
        {
            if (touchingExisting[type] < bestScore) continue;
            if (touchingExisting[type] > bestScore)
            {
                bestScore = touchingExisting[type];
                bestExistingTypes.Clear();
            }
            if (bestScore > 0) bestExistingTypes.Add(type);
        }
        if (bestExistingTypes.Count > 0)
            return bestExistingTypes[Random.Range(0, bestExistingTypes.Count)];

        // Keep independent patches visually distinct whenever a color remains.
        List<int> choices = new List<int>();
        for (int type = 0; type < TypeCount; type++)
            if (!touchingNewTypes.Contains(type)) choices.Add(type);
        if (choices.Count == 0) return Random.Range(0, TypeCount);
        return choices[Random.Range(0, choices.Count)];
    }

    private void SpawnSpecial(Vector2Int cell)
    {
        GameObject wrapper = new GameObject("SpecialAimDot5");
        wrapper.SetActive(false);
        wrapper.transform.SetParent(transform, false);
        wrapper.transform.position = WorldForCell(cell);
        SpecialAimDot5 special = wrapper.AddComponent<SpecialAimDot5>();
        special.Configure(this, cell, specialSpawnSettings);
        GameObject visual = Instantiate(specialVisualPrefab, wrapper.transform);
        visual.name = specialVisualPrefab.name;
        visual.transform.localPosition = Vector3.zero;
        wrapper.SetActive(true);
        specialSpawnPending = false;
    }

    private List<Bubble> ConnectedSameColor(Bubble start)
    {
        List<Bubble> result = new List<Bubble>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { start.cell };
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(start.cell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            Bubble bubble;
            if (!cells.TryGetValue(current, out bubble) || bubble.type != start.type)
                continue;
            result.Add(bubble);
            foreach (Vector2Int neighbor in Neighbors(current))
                if (visited.Add(neighbor)) queue.Enqueue(neighbor);
        }
        return result;
    }

    private void PopMatching(List<Bubble> matching, int additionalPoppedCount = 0)
    {
        if (matching.Count == 0) return;

        // ConnectedSameColor visits the hit bubble first, then its neighbors
        // one hex step at a time. Give each bubble its own start time.
        float interval = Mathf.Max(0f, matchPopInterval);
        for (int i = 0; i < matching.Count; i++)
            RemoveBubble(matching[i], false, i * interval);

        // Start unsupported drops now, so they overlap the staggered pops.
        List<Bubble> falling = DropUnsupported();
        BoardChanged?.Invoke();
        int clearedCount = matching.Count + additionalPoppedCount + falling.Count;
        GameObject celebrationPrefab = clearedCount >= Mathf.Max(1, amazingCelebrationMinCount)
            ? amazingCelebrationPrefab
            : clearedCount >= Mathf.Max(1, coolCelebrationMinCount)
                ? coolCelebrationPrefab : null;
        if (celebrationPrefab != null)
            StartCoroutine(PlayCelebrationAfterClear(matching, falling,
                                                     celebrationPrefab));
        TryScheduleRefill();
    }

    private IEnumerator PlayCelebrationAfterClear(List<Bubble> matching,
                                                   List<Bubble> falling,
                                                   GameObject prefab)
    {
        // Wait for both the staggered pops and the falling bubbles to finish.
        while (true)
        {
            bool anyVisible = false;
            foreach (Bubble bubble in matching)
            {
                if (bubble.root == null) continue;
                anyVisible = true;
                break;
            }
            if (!anyVisible)
            {
                foreach (Bubble bubble in falling)
                {
                    if (bubble.root == null) continue;
                    anyVisible = true;
                    break;
                }
            }
            if (!anyVisible) break;
            yield return null;
        }

        Camera cameraToUse = gameCamera != null ? gameCamera : Camera.main;
        Vector3 center = transform.position;
        if (cameraToUse != null)
        {
            float depth = Mathf.Abs(transform.position.z -
                                    cameraToUse.transform.position.z);
            center = cameraToUse.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, depth));
            center.z = transform.position.z;
        }

        GameObject effect = Instantiate(prefab, center, Quaternion.identity);
        Animator animator = effect.GetComponent<Animator>();
        if (animator == null)
        {
            Destroy(effect);
            yield break;
        }

        // Both supplied clips loop. Their actual clip duration is a more
        // reliable one-cycle timer than the state's first-frame normalizedTime.
        yield return new WaitForSeconds(AnimatorCycleDuration(animator));
        if (effect != null) Destroy(effect);
    }

    private static float AnimatorCycleDuration(Animator animator)
    {
        float clipDuration = 1f;
        RuntimeAnimatorController controller = animator == null
            ? null : animator.runtimeAnimatorController;
        if (controller != null && controller.animationClips.Length > 0)
            clipDuration = Mathf.Max(0.01f, controller.animationClips[0].length);
        return clipDuration / Mathf.Max(0.01f, animator == null ? 1f : animator.speed);
    }

    private void ShowNumberEffect(GameObject prefab, Vector3 worldPosition)
    {
        if (prefab == null) return;
        Camera cameraToUse = gameCamera != null ? gameCamera : Camera.main;
        if (cameraToUse == null) return;
        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f) return;

        if (numberEffectsRoot == null)
        {
            GameObject canvasObject = new GameObject("BubbleNumberEffects",
                typeof(RectTransform), typeof(Canvas));
            numberEffectsRoot = canvasObject.GetComponent<RectTransform>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            // No GraphicRaycaster: the number effects cannot block aiming input.
            Canvas.ForceUpdateCanvases();
        }

        RectTransform anchor = new GameObject(prefab.name + "_Anchor",
            typeof(RectTransform)).GetComponent<RectTransform>();
        anchor.SetParent(numberEffectsRoot, false);
        anchor.anchorMin = anchor.anchorMax = new Vector2(0.5f, 0.5f);
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(numberEffectsRoot,
            screenPosition, null, out localPoint);
        anchor.anchoredPosition = localPoint;

        GameObject effect = Instantiate(prefab, anchor, false);
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null) effectRect.anchoredPosition = Vector2.zero;
        // The clip animates its own anchored position; the wrapper keeps the
        // bubble's screen position as its origin.
        Destroy(anchor.gameObject, AnimatorCycleDuration(effect.GetComponent<Animator>()));
    }

    private void SpawnDisappearanceParticles(int type, Vector3 worldPosition)
    {
        if (disappearanceParticlePrefabs == null || type < 0 ||
            type >= disappearanceParticlePrefabs.Length) return;
        GameObject prefab = disappearanceParticlePrefabs[type];
        if (prefab == null) return;

        GameObject effect = Instantiate(prefab, worldPosition, Quaternion.identity);
        Destroy(effect, Mathf.Max(0.01f, disappearanceParticleLifetime));
    }

    private List<Bubble> DropUnsupported()
    {
        HashSet<Vector2Int> supported = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        foreach (Bubble bubble in cells.Values)
            if (bubble.cell.y == 0 && supported.Add(bubble.cell))
                queue.Enqueue(bubble.cell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int neighbor in Neighbors(current))
                if (cells.ContainsKey(neighbor) && supported.Add(neighbor))
                    queue.Enqueue(neighbor);
        }

        List<Bubble> falling = new List<Bubble>();
        foreach (Bubble bubble in cells.Values)
            if (!supported.Contains(bubble.cell)) falling.Add(bubble);
        // Shuffle the group so neighbors do not always fall in grid order.
        for (int i = falling.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            Bubble temp = falling[i];
            falling[i] = falling[swap];
            falling[swap] = temp;
        }
        for (int i = 0; i < falling.Count; i++)
        {
            float interval = Mathf.Max(0f, dropStaggerInterval);
            float jitter = Mathf.Max(0f, dropDelayJitter);
            float maxRawDelay = (falling.Count - 1) * interval + jitter;
            float spread = Mathf.Max(0f, dropLaunchTimingSpread);
            float delay = spread <= 0f ? 0f : maxRawDelay > 0f
                ? spread * (i * interval + Random.Range(0f, jitter)) / maxRawDelay
                : Random.Range(0f, spread);
            RemoveBubble(falling[i], true, delay);
        }
        return falling;
    }

    private void RemoveBubble(Bubble bubble, bool drop, float delay = 0f)
    {
        cells.Remove(bubble.cell);
        byCollider.Remove(bubble.collider);
        bubble.collider.enabled = false;
        if (bubble.recoil != null) StopCoroutine(bubble.recoil);
        if (bubble.visual != null) bubble.visual.localPosition = bubble.visualHome;
        removalAnimations++;
        StartCoroutine(AnimateRemoval(bubble, drop, delay));
    }

    private IEnumerator AnimateRemoval(Bubble bubble, bool drop, float delay)
    {
        yield return drop ? DropThenDestroy(bubble, delay)
                          : PopThenDestroy(bubble, delay);
        removalAnimations--;
    }

    private HashSet<Vector2Int> ConnectedForRecoil(Vector2Int impactCell)
    {
        HashSet<Vector2Int> connected = new HashSet<Vector2Int>();
        if (!cells.ContainsKey(impactCell)) return connected;

        int radius = Mathf.Max(0, recoilRadiusCells);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        connected.Add(impactCell);
        queue.Enqueue(impactCell);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            foreach (Vector2Int neighbor in Neighbors(current))
            {
                // A nearby but separate group must not receive the impact.
                if (Mathf.Abs(neighbor.x - impactCell.x) > radius ||
                    Mathf.Abs(neighbor.y - impactCell.y) > radius ||
                    !cells.ContainsKey(neighbor) || !connected.Add(neighbor))
                    continue;
                queue.Enqueue(neighbor);
            }
        }
        return connected;
    }

    private void RecoilAround(Vector2Int impactCell, Vector2 impact,
                              HashSet<Vector2Int> connected)
    {
        int radius = Mathf.Max(0, recoilRadiusCells);
        float duration = Mathf.Max(0.01f, recoilDuration);
        foreach (Bubble bubble in cells.Values)
        {
            if (!connected.Contains(bubble.cell)) continue;
            int dx = Mathf.Abs(bubble.cell.x - impactCell.x);
            int dy = Mathf.Abs(bubble.cell.y - impactCell.y);
            if (dx > radius || dy > radius) continue;

            float strength = Mathf.Max(0f, recoilStrength) /
                             (1f + dx * dx + dy * dy);
            if (strength < 0.001f) continue;

            Vector2 away = (Vector2)bubble.root.position - impact;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            away.Normalize();
            if (bubble.recoil != null) StopCoroutine(bubble.recoil);
            bubble.visual.localPosition = bubble.visualHome;
            bubble.recoil = StartCoroutine(Recoil(bubble, away, strength, duration));
        }
    }

    private IEnumerator Recoil(Bubble bubble, Vector2 direction,
                               float strength, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && bubble.visual != null)
        {
            float t = elapsed / duration;
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Exp(-2f * t);
            bubble.visual.localPosition = bubble.visualHome +
                (Vector3)(direction * strength * wave);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (bubble.visual != null) bubble.visual.localPosition = bubble.visualHome;
        bubble.recoil = null;
    }

    private IEnumerator PopThenDestroy(Bubble bubble, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (bubble.root == null) yield break;
        Vector3 originalScale = bubble.root.localScale;
        float duration = Mathf.Max(0.01f, popDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            bubble.root.localScale = originalScale * (1f - elapsed / duration);
            yield return null;
        }
        ShowNumberEffect(matchNumberPrefab, bubble.root.position);
        SpawnDisappearanceParticles(bubble.type, bubble.root.position);
        Destroy(bubble.root.gameObject);
    }

    private IEnumerator DropThenDestroy(Bubble bubble, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (bubble.root == null) yield break;

        ParticleSystem trail = StartDropTrail(bubble);
        Color originalColor = bubble.renderer == null ? Color.white : bubble.renderer.color;
        Camera cameraToUse = gameCamera != null ? gameCamera : Camera.main;
        float angle = Random.Range(-Mathf.Clamp(dropFanHalfAngle, 0f, 85f),
                                    Mathf.Clamp(dropFanHalfAngle, 0f, 85f)) * Mathf.Deg2Rad;
        float speed = Random.Range(Mathf.Max(0.1f, Mathf.Min(dropLaunchSpeedMin, dropLaunchSpeedMax)),
                                   Mathf.Max(0.1f, Mathf.Max(dropLaunchSpeedMin, dropLaunchSpeedMax)));
        float gravity = Random.Range(Mathf.Max(0.1f, Mathf.Min(dropGravityMin, dropGravityMax)),
                                     Mathf.Max(0.1f, Mathf.Max(dropGravityMin, dropGravityMax)));
        Vector2 velocity = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;
        float horizontalSpeed = velocity.x;
        float riseDistance = Random.Range(
            Mathf.Max(0.01f, Mathf.Min(dropRiseDistanceMin, dropRiseDistanceMax)),
            Mathf.Max(0.01f, Mathf.Max(dropRiseDistanceMin, dropRiseDistanceMax)));
        Vector3 launchPosition = bubble.root.position;
        float fallbackFadeY = bubble.root.position.y - 5f;

        // The sampled height is reached exactly. Launch speed controls how
        // quickly the bubble rises, independently of its random rise distance.
        float riseDuration = 2f * riseDistance / Mathf.Max(0.01f, velocity.y);
        float riseElapsed = 0f;
        while (riseElapsed < riseDuration && bubble.root != null)
        {
            float step = Mathf.Min(Time.deltaTime, riseDuration - riseElapsed);
            riseElapsed += step;
            float t = riseElapsed / riseDuration;
            bubble.root.position = new Vector3(
                bubble.root.position.x + horizontalSpeed * step,
                launchPosition.y + riseDistance * (2f * t - t * t),
                launchPosition.z);
            KeepDropInsideScreenX(bubble, cameraToUse, ref horizontalSpeed);
            yield return null;
        }
        if (bubble.root == null) yield break;

        Vector3 apex = bubble.root.position;
        float fallElapsed = 0f;
        while (bubble.root != null)
        {
            float step = Time.deltaTime;
            fallElapsed += step;
            bubble.root.position = new Vector3(
                bubble.root.position.x + horizontalSpeed * step,
                apex.y - 0.5f * gravity * fallElapsed * fallElapsed,
                apex.z);
            KeepDropInsideScreenX(bubble, cameraToUse, ref horizontalSpeed);

            // Fade at a fixed pixel distance above the camera's bottom edge.
            if (cameraToUse != null)
            {
                Vector3 screenPosition = cameraToUse.WorldToScreenPoint(bubble.root.position);
                float fadeLine = cameraToUse.pixelRect.yMin + Mathf.Max(0f, fadeStartPixels);
                if (screenPosition.y <= fadeLine)
                {
                    screenPosition.y = fadeLine;
                    bubble.root.position = cameraToUse.ScreenToWorldPoint(screenPosition);
                    break;
                }
            }
            else if (bubble.root.position.y <= fallbackFadeY)
            {
                Vector3 position = bubble.root.position;
                position.y = fallbackFadeY;
                bubble.root.position = position;
                break;
            }
            yield return null;
        }
        if (bubble.root == null) yield break;

        // Stop at the fade line instead of continuing to fall during fading.
        ShowNumberEffect(dropNumberPrefab, bubble.root.position);
        SpawnDisappearanceParticles(bubble.type, bubble.root.position);
        ParticleSystemRenderer trailRenderer = trail != null
            ? trail.GetComponent<ParticleSystemRenderer>() : null;
        MaterialPropertyBlock trailProperties = null;
        Color trailColor = Color.white;
        if (trail != null)
        {
            trail.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (trailRenderer != null && trailRenderer.sharedMaterial != null &&
                trailRenderer.sharedMaterial.HasProperty(ParticleColorId))
            {
                trailProperties = new MaterialPropertyBlock();
                trailRenderer.GetPropertyBlock(trailProperties);
                trailColor = trailRenderer.sharedMaterial.GetColor(ParticleColorId);
            }
        }
        float fadeTime = Mathf.Max(0.01f, fadeDuration);
        for (float fadeElapsed = 0f; fadeElapsed < fadeTime; fadeElapsed += Time.deltaTime)
        {
            float visibility = 1f - fadeElapsed / fadeTime;
            if (bubble.renderer != null)
            {
                Color color = originalColor;
                color.a *= visibility;
                bubble.renderer.color = color;
            }
            if (trailProperties != null && trailRenderer != null)
            {
                Color color = trailColor;
                // The trail shader multiplies _Color twice; sqrt gives a
                // visually linear alpha fade alongside the falling bubble.
                color.a *= Mathf.Sqrt(visibility);
                trailProperties.SetColor(ParticleColorId, color);
                trailRenderer.SetPropertyBlock(trailProperties);
            }
            yield return null;
        }
        Destroy(bubble.root.gameObject);
    }

    private ParticleSystem StartDropTrail(Bubble bubble)
    {
        if (dropTrailParticlePrefabs == null || bubble.type < 0 ||
            bubble.type >= dropTrailParticlePrefabs.Length) return null;
        GameObject prefab = dropTrailParticlePrefabs[bubble.type];
        if (prefab == null) return null;

        GameObject effect = Instantiate(prefab, bubble.root, false);
        effect.name = prefab.name;
        ParticleSystem trail = effect.GetComponent<ParticleSystem>();
        if (trail != null && bubble.renderer != null)
        {
            ParticleSystemRenderer effectRenderer = trail.GetComponent<ParticleSystemRenderer>();
            if (effectRenderer != null)
            {
                effectRenderer.sortingLayerID = bubble.renderer.sortingLayerID;
                effectRenderer.sortingOrder = bubble.renderer.sortingOrder - 1;
            }
        }
        return trail;
    }

    private void KeepDropInsideScreenX(Bubble bubble, Camera cameraToUse, ref float horizontalSpeed)
    {
        if (cameraToUse == null || bubble.root == null) return;

        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(bubble.root.position);
        float worldRadius = bubble.renderer != null
            ? bubble.renderer.bounds.extents.x
            : Mathf.Max(0.01f, bubbleRadius * visualScale);
        float pixelRadius = Mathf.Abs(cameraToUse.WorldToScreenPoint(
            bubble.root.position + Vector3.right * worldRadius).x - screenPosition.x);
        Rect view = cameraToUse.pixelRect;
        float left = view.xMin + pixelRadius;
        float right = view.xMax - pixelRadius;
        if (right < left) left = right = view.center.x;

        if (screenPosition.x < left)
        {
            screenPosition.x = left;
            horizontalSpeed = Mathf.Abs(horizontalSpeed);
        }
        else if (screenPosition.x > right)
        {
            screenPosition.x = right;
            horizontalSpeed = -Mathf.Abs(horizontalSpeed);
        }
        else return;

        bubble.root.position = cameraToUse.ScreenToWorldPoint(screenPosition);
    }
}
