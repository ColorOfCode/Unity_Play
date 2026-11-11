using System.Collections.Generic;
using UnityEngine;
using Shuimian.Gameplay;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ObscuredByClouds.Gameplay
{
    /// <summary>Character wading effects. Reads actual movement; never moves or floats the character.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("ObscuredByClouds/角色涉水波浪与尾迹")]
    public sealed class CharacterWaterWake : MonoBehaviour, IShuimianWakeSource
    {
        [Header("水面与角色")]
        public ShuimianWaterMesh water;
        [Tooltip("留空使用当前物体。目标位置应为角色脚底，尺寸使用世界单位米。")]
        public Transform feetTarget;
        public float feetHeightOffset;
        [Min(.1f)] public float bodyRadius = .5f;
        [Min(.2f)] public float bodyHeight = 1.8f;
        [Tooltip("脚底略高于波峰时保留接触，避免水线处闪断。")]
        [Range(0f, .5f)] public float contactTolerance = .12f;
        [Tooltip("异步读取实际海浪高度；尚未取得结果时使用平均海平面。")]
        public bool sampleWaveHeight = true;
        public bool emit = true;
        [Tooltip("默认仅在播放模式生成；启用后可拖动角色预览。")]
        public bool previewInEditor;
        [Header("移动与波浪")]
        [Min(.01f)] public float minSpeed = .1f;
        [Min(.2f)] public float fullSpeed = 3f;
        public bool enableWaves = true;
        [Range(0f, .5f)] public float waveStrength = .14f;
        [Min(.1f)] public float waveSpeed = 1.5f;
        [Min(.3f)] public float wavelength = .85f;
        [Min(.2f)] public float packetWidth = .4f;
        [Header("脚部泡沫与尾迹")]
        public bool enableFoam = true;
        [Range(0f, 3f)] public float foamStrength = 1.8f;
        [Min(.1f)] public float foamDisappearTime = 2f;
        [Range(.1f, 4f)] public float wakeFoamSize = .85f;
        [Range(0f, .5f)] public float foamSpread = .12f;
        [Range(1f, 12f)] public float lifetime = 4f;
        [Range(.05f, .25f)] public float sampleInterval = .08f;

        const int Capacity = 256;
        readonly WakeSample[] history = new WakeSample[Capacity];
        readonly Vector3[] queryPosition = new Vector3[1];
        readonly float[] queryHeight = new float[1];
        int first, count;
        bool initialized, previousWet, hasSurface;
        float previousTime, emittedTime, surfaceY, surfaceTime;
        Vector2 previousPosition, emittedPosition, direction = Vector2.up, surfacePosition;
        ShuimianWaterMesh subscribedWater;
        ShuimianWaveHeightSampler sampler;
        ComputeShader samplerShader;
        Vector2 pendingPosition;
        public ShuimianWaterMesh Water { get => water; set { water = value; BindWater(); } }
        public float MinSpeed => minSpeed;
        public Vector2 Position { get { var p = Feet; return new Vector2(p.x, p.z); } }
        public Vector2 Direction => direction;
        public float Speed { get; private set; }
        public bool IsInWater { get; private set; }
        public int PendingSampleCount => count;
        Vector3 Feet => (feetTarget ? feetTarget.position : transform.position) + Vector3.up * feetHeightOffset;
        public bool CanSimulate
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                if (Application.IsPlaying(gameObject)) return true;
#if UNITY_EDITOR
                return previewInEditor && gameObject.scene.IsValid() && gameObject.scene.isLoaded
                    && !EditorUtility.IsPersistent(this) && PrefabStageUtility.GetPrefabStage(gameObject) == null;
#else
                return false;
#endif
            }
        }
        void OnEnable() { BindWater(); ClearHistory(); ShuimianWakeSources.Register(this); }
        void OnDisable() { ShuimianWakeSources.Unregister(this); UnbindWater(); ClearHistory(); }
        void Update() { BindWater(); }
        void BindWater()
        {
            if (subscribedWater == water) return;
            UnbindWater(); subscribedWater = water;
            if (subscribedWater) subscribedWater.SurfacePrepared += QuerySurface;
            ClearHistory();
        }
        void UnbindWater()
        {
            if (subscribedWater) subscribedWater.SurfacePrepared -= QuerySurface;
            subscribedWater = null; sampler?.Dispose(); sampler = null; samplerShader = null; hasSurface = false;
        }
        void QuerySurface(Camera camera)
        {
            if (!CanSimulate || !sampleWaveHeight || !water || !water.combineShader || !water.WaveDisplacement) return;
            if (sampler == null || samplerShader != water.combineShader)
            { sampler?.Dispose(); samplerShader = water.combineShader; sampler = new ShuimianWaveHeightSampler(samplerShader); }
            if (sampler.TryGetHeights(queryHeight))
            { surfaceY = queryHeight[0]; surfacePosition = pendingPosition; surfaceTime = Time.realtimeSinceStartup; hasSurface = true; }
            // Distant dry characters need no GPU query. Retain support for all body heights.
            float feetY = Feet.y, sea = water.transform.position.y;
            if (feetY > sea + 3f || feetY + bodyHeight < sea - 3f) return;
            queryPosition[0] = Feet;
            if (sampler.Request(queryPosition, water.WaveDisplacement, water.WaveFieldCenter,
                water.WaveFieldDiameters, sea, water.WaveFieldCascadeCount)) pendingPosition = Position;
        }
        bool TouchingWater()
        {
            if (!water || !water.isActiveAndEnabled) return false;
            float height = water.transform.position.y;
            if (sampleWaveHeight && hasSurface && Time.realtimeSinceStartup - surfaceTime < .5f
                && (Position - surfacePosition).sqrMagnitude < 4f) height = surfaceY;
            float tolerance = Mathf.Max(0f, contactTolerance) + (previousWet ? .06f : 0f);
            return Feet.y <= height + tolerance && Feet.y + Mathf.Max(.2f, bodyHeight) > height + .05f;
        }
        public void ClearHistory()
        { first = count = 0; initialized = previousWet = false; Speed = 0; IsInWater = false; hasSurface = false; }
        void Anchor(Vector2 position, float time, bool wet)
        { previousPosition = emittedPosition = position; previousTime = emittedTime = time; previousWet = wet; initialized = true; }
        void Prune(float time)
        { while (count > 0 && time - history[first].positionTime.z >= history[first].foamParams.y) { first = (first + 1) % Capacity; count--; } }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void SampleMotion(float time)
        {
            if (!CanSimulate || !Finite(time)) return;
            BindWater();
            var position = Position;
            if (!Finite(position.x) || !Finite(position.y) || !Finite(Feet.y)) { ClearHistory(); return; }
            if (initialized && time < previousTime - .00001f) ClearHistory();
            Prune(time); IsInWater = TouchingWater();
            if (!initialized) { Anchor(position, time, IsInWater); return; }
            float elapsed = time - previousTime;
            if (elapsed <= .000001f) return;
            Vector2 travel = position - previousPosition;
            float distance = travel.magnitude;
            Speed = distance / elapsed;
            if (elapsed > 1f || distance > Mathf.Max(3f, Mathf.Max(.2f, fullSpeed) * elapsed * 4f))
            { Speed = 0; Anchor(position, time, IsInWater); return; }
            if (distance > .00001f) direction = travel / distance;
            if (!emit || !IsInWater || !previousWet || Speed < Mathf.Max(.01f, minSpeed) || (!enableWaves && !enableFoam))
            { if (!IsInWater) Speed = 0; Anchor(position, time, IsInWater); return; }
            float interval = Mathf.Clamp(sampleInterval, .05f, .25f);
            int steps = Mathf.Min(20, Mathf.FloorToInt((time - emittedTime + .00001f) / interval));
            Vector2 origin = emittedPosition;
            float start = emittedTime, duration = Mathf.Max(interval, time - start);
            for (int i = 1; i <= steps; i++)
            {
                float sampleTime = start + i * interval;
                Vector2 end = Vector2.Lerp(origin, position, (sampleTime - start) / duration);
                Vector2 delta = end - emittedPosition;
                float speed = delta.magnitude / interval;
                if (speed >= Mathf.Max(.01f, minSpeed)) AddSample(emittedPosition, end, delta.normalized, sampleTime, interval, speed);
                emittedPosition = end; emittedTime = sampleTime;
            }
            previousPosition = position; previousTime = time; previousWet = IsInWater;
        }
        void AddSample(Vector2 start, Vector2 end, Vector2 heading, float time, float duration, float speed)
        {
            float width = Mathf.Max(.2f, bodyRadius * 2f), length = Mathf.Max(.5f, bodyRadius * 1.2f);
            Vector2 a = start - heading * (length * .38f), b = end - heading * (length * .38f);
            var sample = new WakeSample
            {
                positionTime = new Vector4(end.x, end.y, time, Mathf.Clamp01((speed - minSpeed) / Mathf.Max(.1f, fullSpeed - minSpeed))),
                directionShape = new Vector4(heading.x, heading.y, width, length),
                trailEndpoints = new Vector4(a.x, a.y, b.x, b.y),
                waveParams = new Vector4(enableWaves ? Mathf.Max(0, waveStrength) : 0, Mathf.Max(.1f, waveSpeed), Mathf.Max(.3f, wavelength), Mathf.Max(.2f, packetWidth)),
                foamParams = new Vector4(enableFoam ? Mathf.Max(0, foamStrength) : 0, Mathf.Clamp(lifetime, 1f, 12f), duration, Mathf.Clamp(foamSpread, 0f, .5f)),
                foamLifeParams = new Vector4(Mathf.Max(.1f, foamDisappearTime), speed, 0, 0)
            };
            int index = (first + count) % Capacity;
            if (count == Capacity) { index = first; first = (first + 1) % Capacity; } else count++;
            history[index] = sample;
        }
        public void AppendSamples(List<WakeSample> destination, float time)
        {
            if (destination == null || !CanSimulate) return;
            Prune(time);
            for (int i = 0; i < count; i++)
            {
                var sample = history[(first + i) % Capacity];
                if (time < sample.positionTime.z || time - sample.positionTime.z >= sample.foamParams.y) continue;
                if (!enableWaves) sample.waveParams.x = 0;
                if (!enableFoam) sample.foamParams.x = 0;
                sample.foamLifeParams = new Vector4(Mathf.Max(.1f, foamDisappearTime), sample.foamLifeParams.y, Position.x, Position.y);
                sample.foamSizeParams = new Vector4(Mathf.Clamp(wakeFoamSize, .1f, 4f), 0, 0, 0);
                destination.Add(sample);
            }
        }
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.1f, .75f, 1f, .8f);
            Gizmos.DrawWireSphere(Feet, bodyRadius);
            Gizmos.DrawLine(Feet, Feet + Vector3.up * bodyHeight);
        }
#endif
    }
}
