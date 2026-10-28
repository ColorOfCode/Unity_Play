using System.Collections.Generic;
using UnityEngine;

public class AimGuide2D : MonoBehaviour
{
    [SerializeField] private GameObject dotPrefab;
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private LayerMask targetMask;

    [SerializeField] private float ballRadius = 0.25f;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField] private int maxBounces = 2;
    [SerializeField] private float dotSpacing = 0.35f;

    [Header("射线图标循环缩放")]
    [Tooltip("射线图标的最小倍率；相邻图标以相反方向缩放。")]
    [SerializeField, Min(0f)] private float dotScaleMin = 1f;
    [Tooltip("射线图标的最大倍率。")]
    [SerializeField, Min(0f)] private float dotScaleMax = 1.5f;
    [Tooltip("每秒完成的缩放循环次数。")]
    [SerializeField, Min(0f)] private float dotScaleFrequency = 2f;

    private readonly List<Vector2> points = new List<Vector2>();
    private readonly List<GameObject> dots = new List<GameObject>();
    private readonly List<Vector3> dotBaseScales = new List<Vector3>();
    private bool showing;
    private float animationStartedAt;
    private int visibleDotCount;

    public bool IsShowing => showing && visibleDotCount > 0;

    public void SetDotPrefab(GameObject prefab)
    {
        if (dotPrefab == prefab) return;

        Hide();
        foreach (GameObject dot in dots)
            if (dot != null) Destroy(dot);
        dots.Clear();
        dotBaseScales.Clear();
        dotPrefab = prefab;
    }

    private void LateUpdate()
    {
        if (showing) AnimateDots();
    }

    // origin：发射点；aimPoint：鼠标或触摸对应的世界坐标
    public void Show(Vector2 origin, Vector2 aimPoint)
    {
        if (dotPrefab == null)
        {
            Hide();
            return;
        }

        Vector2 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.0001f)
        {
            Hide();
            return;
        }

        direction.Normalize();
        points.Clear();
        points.Add(origin);

        Vector2 current = origin;
        float remaining = Mathf.Max(0f, maxDistance);
        int collisionMask = wallMask.value | targetMask.value;
        Collider2D previousWall = null;

        for (int bounce = 0; bounce <= maxBounces && remaining > 0.001f; bounce++)
        {
            // 从碰撞点稍微向前移，避免下一次检测再次命中同一面墙。
            float offset = Mathf.Min(0.002f, remaining * 0.5f);
            Vector2 castOrigin = current + direction * offset;
            float castDistance = remaining - offset;
            RaycastHit2D hit = Physics2D.CircleCast(
                castOrigin,
                ballRadius,
                direction,
                castDistance,
                collisionMask
            );

            // After a reflection the new cast can start microscopically inside
            // that wall. Ignore its zero-distance hit, then look for the next
            // wall or target along the reflected direction.
            if (hit && hit.fraction <= 0f && hit.collider == previousWall)
                hit = CastPastPreviousWall(castOrigin, direction,
                                           castDistance, collisionMask,
                                           previousWall);

            if (!hit)
            {
                points.Add(current + direction * remaining);
                break;
            }

            // 起点已处于碰撞体内，无法得到可靠的下一段路径。
            if (hit.fraction <= 0f)
                break;

            Vector2 end = hit.centroid; // 球心位置，不是墙上的接触点
            points.Add(end);
            remaining -= Vector2.Distance(current, end);

            bool hitWall = (wallMask.value &
                            (1 << hit.collider.gameObject.layer)) != 0;

            if (!hitWall || bounce == maxBounces)
                break; // 碰到目标，或已达到允许的反弹次数

            direction = Vector2.Reflect(direction, hit.normal).normalized;
            current = end;
            previousWall = hit.collider;
        }

        if (!showing)
        {
            showing = true;
            animationStartedAt = Time.time;
        }
        PlaceDots();
        AnimateDots();
    }

    private RaycastHit2D CastPastPreviousWall(Vector2 origin,
                                             Vector2 direction,
                                             float distance, int mask,
                                             Collider2D previousWall)
    {
        RaycastHit2D nearest = default(RaycastHit2D);
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D candidate in Physics2D.CircleCastAll(
                     origin, ballRadius, direction, distance, mask))
        {
            if (candidate.collider == previousWall && candidate.fraction <= 0f)
                continue;
            if (candidate.distance >= nearestDistance) continue;
            nearest = candidate;
            nearestDistance = candidate.distance;
        }
        return nearest;
    }

    private void PlaceDots()
    {
        float spacing = Mathf.Max(0.01f, dotSpacing);
        float nextDotDistance = spacing;
        float distanceBeforeSegment = 0f;
        int used = 0;

        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1];
            Vector2 b = points[i];
            float segmentLength = Vector2.Distance(a, b);

            if (segmentLength < 0.0001f)
                continue;

            while (nextDotDistance <= distanceBeforeSegment + segmentLength)
            {
                float t = (nextDotDistance - distanceBeforeSegment) / segmentLength;
                Vector2 position = Vector2.Lerp(a, b, t);

                if (used == dots.Count)
                {
                    GameObject newDot = Instantiate(dotPrefab, transform);
                    dots.Add(newDot);
                    dotBaseScales.Add(newDot.transform.localScale);
                }

                GameObject dot = dots[used++];
                dot.transform.position =
                    new Vector3(position.x, position.y, transform.position.z);
                dot.SetActive(true);

                nextDotDistance += spacing;
            }

            distanceBeforeSegment += segmentLength;
        }

        visibleDotCount = used;
        for (int i = used; i < dots.Count; i++)
            dots[i].SetActive(false);
    }

    private void AnimateDots()
    {
        float min = Mathf.Min(dotScaleMin, dotScaleMax);
        float max = Mathf.Max(dotScaleMin, dotScaleMax);
        float cycles = Mathf.Max(0f, dotScaleFrequency) *
                       (Time.time - animationStartedAt);
        float phase = 0.5f - 0.5f * Mathf.Cos(cycles * Mathf.PI * 2f);

        for (int i = 0; i < visibleDotCount; i++)
        {
            float factor = i % 2 == 0
                ? Mathf.Lerp(min, max, phase)
                : Mathf.Lerp(max, min, phase);
            dots[i].transform.localScale = dotBaseScales[i] * factor;
        }
    }

    public void Hide()
    {
        showing = false;
        visibleDotCount = 0;
        foreach (GameObject dot in dots)
            if (dot != null) dot.SetActive(false);
    }
}
