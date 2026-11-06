using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Per-camera underwater override against the actual animated FFT surface.
    /// Close to the surface, leave classification to the shader's front/back face.
    /// Queries never wait for the GPU during rendering.
    /// </summary>
    public sealed class ShuimianWaterViewState : IDisposable
    {
        const float OverrideDistance = 2f;
        const float MaximumHorizontalDrift = .25f;
        const float MaximumSimulationAge = .25f;
        const float MaximumRealtimeAge = .5f;

        sealed class View
        {
            public Camera camera;
            public ComputeShader source;
            public ShuimianWaveHeightSampler sampler;
            public readonly Vector3[] point = new Vector3[1];
            public readonly float[] height = new float[1];
            public Sample requested, cached;
            public float waterHeight;
            public bool hasHeight;
        }

        struct Sample
        {
            public Vector2 position;
            public float simulationTime, realtime, seaLevel;
            public int textureId, resolution, cascadeCount;
        }

        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly List<View> retiredViews = new List<View>();
        bool disposed;

        /// <returns>
        /// +1 well below the animated surface, -1 well above it, or zero to use
        /// per-pixel face classification near waves or while a fresh query is pending.
        /// </returns>
        public float Update(Camera camera, ComputeShader combine, RenderTexture displacement,
            Vector2 center, Vector2 diameters, float seaLevel, int cascadeCount, float time)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWaterViewState));
            if (camera == null) return 0f;
            int id = camera.GetInstanceID();
            if (displacement == null)
            {
                if (views.TryGetValue(id, out var oldView)) oldView.hasHeight = false;
                return Override(camera.transform.position.y - seaLevel);
            }
            // The existing sampler supports a synchronous fallback. The view-state
            // path deliberately uses face classification on such devices instead.
            if (combine == null || !SystemInfo.supportsAsyncGPUReadback) return 0f;

            if (!views.TryGetValue(id, out var view) || view.source != combine)
            {
                // Keep old pending queries alive until owner teardown instead of
                // waiting for their completion inside a rendering callback.
                if (view != null) retiredViews.Add(view);
                view = new View
                {
                    camera = camera, source = combine,
                    sampler = new ShuimianWaveHeightSampler(combine)
                };
                views[id] = view;
            }

            Vector3 position = camera.transform.position;
            var current = new Sample
            {
                position = new Vector2(position.x, position.z),
                simulationTime = time, realtime = Time.realtimeSinceStartup, seaLevel = seaLevel,
                textureId = displacement.GetInstanceID(), resolution = displacement.width,
                cascadeCount = cascadeCount
            };
            if (view.sampler.TryGetHeights(view.height))
            {
                view.hasHeight = IsCurrent(view.requested, current);
                if (view.hasHeight)
                {
                    view.waterHeight = view.height[0];
                    view.cached = view.requested;
                }
            }
            if (view.hasHeight && !IsCurrent(view.cached, current)) view.hasHeight = false;

            view.point[0] = position;
            if (view.sampler.Request(view.point, displacement, center, diameters, seaLevel, cascadeCount))
                view.requested = current;

            return view.hasHeight ? Override(position.y - view.waterHeight) : 0f;
        }

        static bool IsCurrent(Sample sampled, Sample current)
        {
            float elapsed = current.simulationTime - sampled.simulationTime;
            float realtimeElapsed = current.realtime - sampled.realtime;
            return sampled.textureId == current.textureId
                && sampled.resolution == current.resolution
                && sampled.cascadeCount == current.cascadeCount
                && Mathf.Abs(sampled.seaLevel - current.seaLevel) < .001f
                && (sampled.position - current.position).sqrMagnitude <= MaximumHorizontalDrift * MaximumHorizontalDrift
                && elapsed >= 0f && elapsed <= MaximumSimulationAge
                && realtimeElapsed >= 0f && realtimeElapsed <= MaximumRealtimeAge;
        }

        static float Override(float heightAboveWater)
        {
            return heightAboveWater < -OverrideDistance ? 1f
                : heightAboveWater > OverrideDistance ? -1f : 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var view in views.Values) view.sampler.Dispose();
            foreach (var view in retiredViews) view.sampler.Dispose();
            views.Clear();
            retiredViews.Clear();
        }
    }
}
