using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Shuimian.Gameplay
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WakeSample
    {
        public Vector4 positionTime;
        public Vector4 directionShape;
        public Vector4 trailEndpoints;
        public Vector4 waveParams;
        public Vector4 foamParams;
        public Vector4 foamLifeParams;
        public Vector4 foamSizeParams;
    }

    /// <summary>World-space movement history, independent of cameras and vertical buoyancy.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/船行波浪与尾迹")]
    public sealed class ShuimianShipWake : MonoBehaviour
    {
        [Header("水面与运动目标")]
        public ShuimianWaterMesh water;
        public ShipController controller;
        [Tooltip("留空使用 ShipController 的移动目标；仅取实际 X/Z 位移，忽略上下漂浮。")]
        public Transform motionTarget;
        [Tooltip("停止产生新尾迹；已经产生的波浪与泡沫仍会自然扩散消退。")]
        public bool emit = true;
        public bool previewInEditor = true;
        [Tooltip("船体中心偏移（米），按目标旋转，忽略目标缩放。")]
        public Vector3 centerOffset;
        [Tooltip("相对平均海面的接触容差；被抬离水面时不再发出新尾迹。")]
        [Min(.1f)] public float waterContactRange = 4f;
        [Header("船体与速度（世界单位：米）")]
        [Tooltip("实时识别启用的 Mesh/Skinned Mesh 模型，自动跟随子模型缩放、偏移、替换和增减；下方尺寸作为手动回退。")]
        public bool autoFitModel = true;
        [InspectorName("Manual Hull Width")]
        [Tooltip("关闭 Auto Fit Model 时使用的手动宽度；自动模式使用当前模型实测尺寸。")]
        [Min(.2f)] public float hullWidth = 4f;
        [InspectorName("Manual Hull Length")]
        [Tooltip("关闭 Auto Fit Model 时使用的手动长度；自动模式使用当前模型实测尺寸。")]
        [Min(.5f)] public float hullLength = 13.5f;
        [Min(.01f)] public float minSpeed = .2f;
        [Min(.1f)] public float fullSpeed = 8f;
        [Tooltip("使用实际运动方向，支持横移、倒退及转弯；关闭后使用船头方向。")]
        public bool alignWithMotion = true;
        [Header("向外传播的船行波")]
        public bool enableWaves = true;
        [Range(0f, 1f)] public float waveStrength = .3f;
        [Min(.1f)] public float waveSpeed = 3.2f;
        [Min(.3f)] public float wavelength = 2.6f;
        [Min(.2f)] public float packetWidth = 1.2f;
        [Header("船尾与两侧泡沫")]
        public bool enableFoam = true;
        [Range(0f, 3f)] public float foamStrength = 1f;
        [Tooltip("拖尾泡沫完全消失的秒数；透明度与密度共同按年龄和尾迹距离从1衰减到0。")]
        [Min(.1f)] public float foamDisappearTime = 1f;
        [InspectorName("Hull Contact Foam")]
        [Tooltip("船身水线泡沫涟漪：行驶和停船时都持续存在。")]
        public bool stationaryContactFoam = true;
        [Tooltip("船尾拖迹和前方两侧波峰泡沫的覆盖宽度/外展范围倍率；1保留当前范围，不改变泡沫消失时长。")]
        [Range(.1f, 4f)] public float wakeFoamSize = 1f;
        [Range(0f, 3f)] public float contactFoamStrength = .7f;
        [Min(.1f)] public float contactFoamWidth = .45f;
        [Tooltip("泡沫带随年龄增加的宽度（米/秒）。")]
        [Range(0f, .5f)] public float foamSpread = .12f;
        [Header("历史")]
        [Range(1f, 12f)] public float lifetime = 10f;
        [Range(.05f, .25f)] public float sampleInterval = .1f;

        // Covers the full exposed 12 s lifetime even at the 0.05 s sample interval.
        const int HistoryCapacity = 256;
        static readonly List<ShuimianShipWake> active = new List<ShuimianShipWake>();
        readonly WakeSample[] history = new WakeSample[HistoryCapacity];
        int first, count;
        bool initialized;
        float previousTime, emittedTime;
        Vector2 previousPosition, emittedPosition, direction = Vector2.up;
        ShuimianShipGeometry.Footprint fittedHull;
        bool hasFittedHull, hasGeometryBaseline;
        Vector3 previousGeometryCenter, previousGeometrySize;
        public float EffectiveHullWidth => hasFittedHull && autoFitModel ? fittedHull.width : hullWidth;
        public float EffectiveHullLength => hasFittedHull && autoFitModel ? fittedHull.length : hullLength;
        float HullScale => Mathf.Clamp(Mathf.Sqrt(EffectiveHullWidth / 4f * EffectiveHullLength / 13.5f), .1f, 20f);
        public void RefreshHull() => hasFittedHull = autoFitModel && ShuimianShipGeometry.TryFootprint(Target,
            controller != null ? controller.ForwardFlat : Target.forward, out fittedHull);
        public static IReadOnlyList<ShuimianShipWake> ActiveSources => active;
        public int PendingSampleCount => count;
        public Vector2 Position
        {
            get { Vector3 p = CenterPosition; return new Vector2(p.x, p.z); }
        }
        public Vector2 Direction => direction;
        public float Speed { get; private set; }
        public Transform Target => motionTarget != null ? motionTarget : controller != null ? controller.Target : transform;
        Vector3 CenterPosition
        {
            get
            {
                Vector3 center = Target.position;
                if (hasFittedHull && autoFitModel)
                {
                    var floating = GetComponent<ShuimianShipFloat>();
                    center = fittedHull.center;
                    center.y = fittedHull.Waterline(floating != null ? floating.draftFraction : .13f);
                }
                return center + Target.rotation * centerOffset;
            }
        }
        public bool CanSimulate
        {
            get
            {
                if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
                if (Application.IsPlaying(gameObject)) return true;
#if UNITY_EDITOR
                return previewInEditor && gameObject.scene.IsValid() && gameObject.scene.isLoaded
                    && !EditorUtility.IsPersistent(this) && PrefabStageUtility.GetPrefabStage(gameObject) == null;
#else
                return false;
#endif
            }
        }
        void OnEnable()
        {
            if (controller == null) controller = GetComponent<ShipController>();
            if (water == null) water = FindObjectOfType<ShuimianWaterMesh>();
            if (!active.Contains(this)) active.Add(this);
            ClearHistory();
        }
        void OnDisable() { active.Remove(this); ClearHistory(); }
        public void ClearHistory()
        {
            first = count = 0; initialized = false; Speed = 0f; hasGeometryBaseline = false;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void Anchor(Vector2 position, float time)
        {
            previousPosition = emittedPosition = position;
            previousTime = emittedTime = time; initialized = true;
        }
        Vector2 ForwardDirection()
        {
            Vector3 f = controller != null ? controller.ForwardFlat : Target.forward;
            Vector2 flat = new Vector2(f.x, f.z);
            return flat.sqrMagnitude > .0001f ? flat.normalized : Vector2.up;
        }
        void Prune(float time)
        {
            while (count > 0)
            {
                var sample = history[first];
                if (time - sample.positionTime.z < sample.foamParams.y) break;
                first = (first + 1) % HistoryCapacity; count--;
            }
        }
        /// <summary>Called once for a shared simulation time; repeated camera calls do not emit twice.</summary>
        public void SampleMotion(float time)
        {
            if (!CanSimulate || !Finite(time)) return;
            RefreshHull();
            Vector3 center3 = CenterPosition;
            Vector2 position = new Vector2(center3.x, center3.z);
            if (!Finite(position.x) || !Finite(position.y) || !Finite(center3.y)) { ClearHistory(); return; }
            if (initialized && time < previousTime) ClearHistory();
            Prune(time);
            if (autoFitModel && !hasFittedHull) { Speed = 0f; Anchor(position, time); return; }
            if (hasFittedHull)
            {
                Vector3 relative = Quaternion.Inverse(Target.rotation) * (center3 - Target.position);
                Vector3 size = new Vector3(fittedHull.width, fittedHull.height, fittedHull.length);
                bool changed = hasGeometryBaseline && ((relative - previousGeometryCenter).sqrMagnitude > .00001f
                    || (size - previousGeometrySize).sqrMagnitude > .00001f);
                previousGeometryCenter = relative; previousGeometrySize = size; hasGeometryBaseline = true;
                if (initialized && changed) { Speed = 0; Anchor(position, time); return; }
            }
            if (!initialized)
            {
                direction = ForwardDirection(); Anchor(position, time); return;
            }
            float elapsed = time - previousTime;
            if (elapsed <= .000001f) return;
            Vector2 travel = position - previousPosition;
            float distance = travel.magnitude;
            Speed = distance / elapsed;
            // Editor teleports and resumed timelines must not create a stripe
            // connecting two unrelated locations. Already emitted waves persist.
            if (elapsed > 1f || distance > Mathf.Max(12f, Mathf.Max(.1f, fullSpeed) * elapsed * 4f))
            {
                Speed = 0f; Anchor(position, time); return;
            }
            if (distance > .00001f) direction = alignWithMotion ? travel / distance : ForwardDirection();
            bool touching = water == null || Mathf.Abs(center3.y - water.transform.position.y) <= Mathf.Max(.1f, waterContactRange);
            if (!emit || !touching || Speed < Mathf.Max(.01f, minSpeed) || (!enableWaves && !enableFoam))
            {
                if (!touching) Speed = 0f;
                Anchor(position, time); return;
            }
            float interval = Mathf.Clamp(sampleInterval, .05f, .25f);
            int steps = Mathf.Min(20, Mathf.FloorToInt((time - emittedTime + .00001f) / interval));
            if (steps > 0)
            {
                Vector2 start = emittedPosition;
                float startTime = emittedTime;
                float duration = Mathf.Max(interval, time - startTime);
                for (int i = 1; i <= steps; i++)
                {
                    float sampleTime = startTime + interval * i;
                    Vector2 end = Vector2.Lerp(start, position, (sampleTime - startTime) / duration);
                    Vector2 segmentStart = i == 1 ? emittedPosition : Vector2.Lerp(start, position, interval * (i - 1) / duration);
                    Vector2 delta = end - segmentStart;
                    float speed = delta.magnitude / interval;
                    if (speed >= Mathf.Max(.01f, minSpeed))
                    {
                        Vector2 heading = alignWithMotion && delta.sqrMagnitude > .000001f ? delta.normalized : direction;
                        AddSample(segmentStart, end, heading, sampleTime, interval, speed);
                    }
                    emittedPosition = end; emittedTime = sampleTime;
                }
            }
            previousPosition = position; previousTime = time;
        }
        void AddSample(Vector2 start, Vector2 end, Vector2 heading, float time, float duration, float speed)
        {
            float width = Mathf.Max(.2f, EffectiveHullWidth), length = Mathf.Max(.5f, EffectiveHullLength);
            if (autoFitModel && ShuimianShipGeometry.TryFootprint(Target, new Vector3(heading.x, 0, heading.y), out var movingHull))
            { width = movingHull.width; length = movingHull.length; }
            Vector2 sternOffset = heading * (length * .38f);
            Vector2 a = start - sternOffset, b = end - sternOffset;
            var sample = new WakeSample
            {
                positionTime = new Vector4(end.x, end.y, time, Mathf.Clamp01((speed - minSpeed) / Mathf.Max(.1f, fullSpeed - minSpeed))),
                directionShape = new Vector4(heading.x, heading.y, width, length),
                trailEndpoints = new Vector4(a.x, a.y, b.x, b.y),
                waveParams = new Vector4(Mathf.Clamp(waveStrength, 0f, 1f) * HullScale, Mathf.Max(.1f, waveSpeed), Mathf.Max(.3f, wavelength * HullScale), Mathf.Max(.2f, packetWidth * HullScale)),
                foamParams = new Vector4(Mathf.Clamp(foamStrength, 0f, 3f), Mathf.Clamp(lifetime, 1f, 12f), duration, Mathf.Clamp(foamSpread, 0f, .5f) * HullScale),
                foamLifeParams = new Vector4(Mathf.Max(.1f, foamDisappearTime), speed, 0f, 0f)
            };
            int index = (first + count) % HistoryCapacity;
            if (count == HistoryCapacity) { index = first; first = (first + 1) % HistoryCapacity; }
            else count++;
            history[index] = sample;
        }
        /// <summary>Copies active history; it never consumes samples or advances time.</summary>
        public void AppendSamples(List<WakeSample> destination, float time)
        {
            if (destination == null || !CanSimulate) return;
            RefreshHull();
            Prune(time);
            for (int i = 0; i < count; i++)
            {
                WakeSample sample = history[(first + i) % HistoryCapacity];
                if (time < sample.positionTime.z || time - sample.positionTime.z >= sample.foamParams.y) continue;
                if (!enableWaves) sample.waveParams.x = 0f;
                if (!enableFoam) sample.foamParams.x = 0f;
                sample.foamLifeParams = new Vector4(Mathf.Max(.1f, foamDisappearTime), sample.foamLifeParams.y, Position.x, Position.y);
                sample.foamSizeParams = new Vector4(Mathf.Clamp(wakeFoamSize, .1f, 4f), 0f, 0f, 0f);
                destination.Add(sample);
            }
            if (emit && enableFoam && stationaryContactFoam && (!autoFitModel || hasFittedHull)
                && (water == null || Mathf.Abs(CenterPosition.y - water.transform.position.y) <= waterContactRange))
            {
                Vector2 f = ForwardDirection();
                destination.Add(new WakeSample
                {
                    positionTime = new Vector4(Position.x, Position.y, time, -1f),
                    directionShape = new Vector4(f.x, f.y, EffectiveHullWidth, EffectiveHullLength),
                    waveParams = new Vector4(enableWaves ? .015f * HullScale : 0f, 0f, 0f, 0f),
                    foamParams = new Vector4(contactFoamStrength, contactFoamWidth * HullScale, 0f, 0f)
                });
            }
        }
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            RefreshHull();
            Vector3 p = CenterPosition;
            Vector3 f = new Vector3(direction.x, 0f, direction.y);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Gizmos.color = new Color(.15f, .8f, 1f, .8f);
            Vector3 bow = p + f * EffectiveHullLength * .33f, stern = p - f * EffectiveHullLength * .38f;
            Gizmos.DrawLine(stern, bow);
            Gizmos.DrawLine(stern - r * EffectiveHullWidth * .5f, stern + r * EffectiveHullWidth * .5f);
            Gizmos.DrawRay(bow, f * 2f);
        }
#endif
    }
}
