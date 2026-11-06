using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Small asynchronous GPU queries against the actual FFT surface. Readback
    /// data is copied during completion, then retained until the caller consumes it.
    /// </summary>
    public sealed class ShuimianWaveHeightSampler : IDisposable
    {
        readonly ComputeShader shader;
        readonly int kernel;
        readonly Action<AsyncGPUReadbackRequest> completed;
        ComputeBuffer points, results;
        Vector4[] positions, values;
        AsyncGPUReadbackRequest readback;
        bool pending, ready, disposed;
        int count;

        public ShuimianWaveHeightSampler(ComputeShader source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            shader = Object.Instantiate(source);
            shader.hideFlags = HideFlags.HideAndDontSave;
            kernel = shader.FindKernel("SampleHeight");
            completed = Receive;
        }
        public bool Request(Vector3[] worldPositions, RenderTexture displacement,
            Vector2 center, Vector2 diameters, float seaLevel, int cascadeCount = 2)
        {
            // A ready result must survive irregular editor update intervals and
            // cannot be replaced before TryGetHeights has consumed its cache.
            if (disposed || pending || ready || displacement == null || worldPositions == null || worldPositions.Length == 0)
                return false;
            count = worldPositions.Length;
            if (points == null || points.count != count)
            {
                points?.Release(); results?.Release();
                points = new ComputeBuffer(count, 16);
                results = new ComputeBuffer(count, 16);
                positions = new Vector4[count]; values = new Vector4[count];
            }
            for (int i = 0; i < count; i++) positions[i] = worldPositions[i];
            points.SetData(positions);
            shader.SetInt("_SampleCount", count);
            shader.SetInt("_CascadeCount", Mathf.Clamp(cascadeCount, 2, displacement.volumeDepth));
            shader.SetFloat("_SeaLevel", seaLevel);
            shader.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
            shader.SetVector("_Diameters", new Vector4(diameters.x, diameters.y, 0f, 0f));
            shader.SetTexture(kernel, "_HeightDisplacement", displacement);
            shader.SetBuffer(kernel, "_SamplePositions", points);
            shader.SetBuffer(kernel, "_SampleResults", results);
            shader.Dispatch(kernel, (count + 7) / 8, 1, 1);
            pending = true;
            try
            {
                if (SystemInfo.supportsAsyncGPUReadback)
                    readback = AsyncGPUReadback.Request(results, completed);
                else
                {
                    results.GetData(values);
                    pending = false;
                    ready = true;
                }
            }
            catch
            {
                pending = false;
                ready = false;
                throw;
            }
            return true;
        }
        void Receive(AsyncGPUReadbackRequest request)
        {
            pending = false;
            // Completion can be delivered while or after Dispose waits for the GPU.
            if (disposed || request.hasError) return;
            var data = request.GetData<Vector4>();
            if (data.Length < count) return;
            for (int i = 0; i < count; i++) values[i] = data[i];
            ready = true;
        }
        public bool TryGetHeights(float[] heights)
        {
            if (disposed || !ready || heights == null || heights.Length < count) return false;
            ready = false;
            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(values[i].x) || float.IsInfinity(values[i].x)) return false;
                heights[i] = values[i].x;
            }
            return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ready = false;
            // Normal sampling never waits. At teardown, complete this sampler's
            // outstanding query before releasing the buffers it references.
            if (pending && SystemInfo.supportsAsyncGPUReadback && !readback.done) readback.WaitForCompletion();
            pending = false;
            points?.Release(); results?.Release();
            points = null; results = null;
            if (Application.isPlaying) Object.Destroy(shader); else Object.DestroyImmediate(shader);
        }
    }
}
