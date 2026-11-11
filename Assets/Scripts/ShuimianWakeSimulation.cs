using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shuimian.Gameplay
{
    /// <summary>World-anchored ship and character wakes, projected into each camera's existing water cascades.</summary>
    public sealed class ShuimianWakeSimulation : IDisposable
    {
        const int MaximumSamples = 256;
        readonly ComputeShader shader;
        readonly int generateKernel, displacementKernel, foamKernel;
        readonly ComputeBuffer samplesBuffer;
        readonly List<WakeSample> samples = new List<WakeSample>(MaximumSamples * 2);
        readonly WakeSample[] upload = new WakeSample[MaximumSamples];
        RenderTexture compositeFoam;
        bool disposed;
        float previousTime = float.NaN;
        int previousHash;
        public RenderTexture Field { get; private set; }
        public Vector2 Center { get; private set; }
        public float AreaSize { get; private set; }
        public int SampleCount { get; private set; }
        public int FieldRevision { get; private set; }

        public ShuimianWakeSimulation(ComputeShader source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            shader = Object.Instantiate(source); shader.hideFlags = HideFlags.HideAndDontSave;
            generateKernel = shader.FindKernel("GenerateWake");
            displacementKernel = shader.FindKernel("AddDisplacement");
            foamKernel = shader.FindKernel("CompositeFoam");
            samplesBuffer = new ComputeBuffer(MaximumSamples, 112);
        }
        public void Update(ShuimianWaterMesh owner, float time, int resolution = 512, float areaSize = 192f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWakeSimulation));
            if (owner == null || float.IsNaN(time) || float.IsInfinity(time)) return;
            samples.Clear();
            IShuimianWakeSource primary = null;
            var sources = ShuimianWakeSources.Active;
            bool rewind = !float.IsNaN(previousTime) && time < previousTime - .00001f;
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null || (source is Object unityObject && unityObject == null) || !source.CanSimulate) continue;
                if (source.Water == null) source.Water = owner;
                if (source.Water != owner) continue;
                if (rewind) source.ClearHistory();
                source.SampleMotion(time); source.AppendSamples(samples, time);
                if (primary == null || (primary.Speed < primary.MinSpeed && source.Speed >= source.MinSpeed)) primary = source;
            }
            if (samples.Count > MaximumSamples)
            {
                samples.Sort((a, b) => a.positionTime.z.CompareTo(b.positionTime.z));
                samples.RemoveRange(0, samples.Count - MaximumSamples);
            }
            SampleCount = samples.Count;
            if (primary == null && Field == null) { previousTime = time; return; }
            resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 128, 1024));
            areaSize = Mathf.Clamp(areaSize, 32f, 1024f);
            float texel = areaSize / resolution;
            Vector2 center = primary != null ? primary.Position - primary.Direction * (areaSize * .2f) : Center;
            center.x = Mathf.Round(center.x / texel) * texel;
            center.y = Mathf.Round(center.y / texel) * texel;
            int hash = SampleCount;
            unchecked
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    var sample = samples[i];
                    hash = hash * 31 + sample.positionTime.GetHashCode(); hash = hash * 31 + sample.directionShape.GetHashCode();
                    hash = hash * 31 + sample.trailEndpoints.GetHashCode(); hash = hash * 31 + sample.waveParams.GetHashCode();
                    hash = hash * 31 + sample.foamParams.GetHashCode();
                    hash = hash * 31 + sample.foamLifeParams.GetHashCode();
                    hash = hash * 31 + sample.foamSizeParams.GetHashCode();
                }
            }
            bool layoutChanged = Field == null || Field.width != resolution || AreaSize != areaSize || Center != center;
            if (!layoutChanged && time == previousTime && previousHash == hash) return;
            if (Field == null || Field.width != resolution)
            {
                ReleaseTexture(Field);
                Field = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "Shuimian Ship Wake Field", hideFlags = HideFlags.HideAndDontSave,
                    enableRandomWrite = true, useMipMap = true, autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
                };
                Field.Create();
            }
            Center = center; AreaSize = areaSize;
            for (int i = 0; i < samples.Count; i++) upload[i] = samples[i];
            if (samples.Count > 0) samplesBuffer.SetData(upload, 0, 0, samples.Count);
            shader.SetInt("_WakeSampleCount", samples.Count);
            shader.SetInt("_WakeResolution", resolution);
            shader.SetFloat("_WakeTime", time);
            shader.SetVector("_WakeCenter", new Vector4(Center.x, Center.y, 0f, 0f));
            shader.SetFloat("_WakeAreaSize", AreaSize);
            shader.SetBuffer(generateKernel, "_WakeSamples", samplesBuffer);
            shader.SetTexture(generateKernel, "_WakeOutput", Field);
            shader.Dispatch(generateKernel, (resolution + 7) / 8, (resolution + 7) / 8, 1);
            Field.GenerateMips();
            previousTime = time; previousHash = hash; FieldRevision++;
        }
        void SetProjection(RenderTexture displacement, Vector2 center, float nearDiameter, float farDiameter, int cascades)
        {
            shader.SetInt("_SurfaceResolution", displacement.width);
            shader.SetInt("_SurfaceCascades", Mathf.Min(cascades, displacement.volumeDepth));
            shader.SetVector("_SurfaceCenter", new Vector4(center.x, center.y, 0f, 0f));
            shader.SetVector("_SurfaceDiameters", new Vector4(nearDiameter, farDiameter, 0f, 0f));
            shader.SetVector("_WakeCenter", new Vector4(Center.x, Center.y, 0f, 0f));
            shader.SetFloat("_WakeAreaSize", AreaSize);
            shader.SetInt("_WakeResolution", Field.width);
        }
        /// <summary>Call once after each fresh FFT combine, before height queries/foam. W variance is retained.</summary>
        public void ApplyDisplacement(RenderTexture displacement, Vector2 surfaceCenter, float nearDiameter, float farDiameter, int cascades)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWakeSimulation));
            if (SampleCount == 0 || Field == null || displacement == null) return;
            SetProjection(displacement, surfaceCenter, nearDiameter, farDiameter, cascades);
            shader.SetTexture(displacementKernel, "_WakeField", Field);
            shader.SetTexture(displacementKernel, "_WakeDisplacement", displacement);
            shader.Dispatch(displacementKernel, (displacement.width + 7) / 8, (displacement.height + 7) / 8, Mathf.Min(cascades, displacement.volumeDepth));
        }
        /// <summary>Separate output prevents boat foam from feeding back into native foam history.</summary>
        public RenderTexture CompositeFoam(Texture nativeFoam, RenderTexture displacement, Vector2 center,
            float nearDiameter, float farDiameter, int cascades)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWakeSimulation));
            if (SampleCount == 0 || Field == null || displacement == null) return nativeFoam as RenderTexture;
            cascades = Mathf.Min(cascades, displacement.volumeDepth);
            if (compositeFoam == null || compositeFoam.width != displacement.width || compositeFoam.volumeDepth != cascades)
            {
                ReleaseTexture(compositeFoam);
                compositeFoam = new RenderTexture(displacement.width, displacement.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "Shuimian Boat and Ocean Foam", hideFlags = HideFlags.HideAndDontSave,
                    dimension = TextureDimension.Tex2DArray, volumeDepth = cascades,
                    enableRandomWrite = true, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                compositeFoam.Create();
            }
            SetProjection(displacement, center, nearDiameter, farDiameter, cascades);
            shader.SetInt("_HasNativeFoam", nativeFoam != null ? 1 : 0);
            shader.SetTexture(foamKernel, "_WakeField", Field);
            shader.SetTexture(foamKernel, "_SurfaceDisplacement", displacement);
            shader.SetTexture(foamKernel, "_NativeFoam", nativeFoam != null ? nativeFoam : displacement);
            shader.SetTexture(foamKernel, "_CompositeFoam", compositeFoam);
            shader.Dispatch(foamKernel, (displacement.width + 7) / 8, (displacement.height + 7) / 8, cascades);
            return compositeFoam;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; samplesBuffer.Release();
            ReleaseTexture(Field); Field = null; ReleaseTexture(compositeFoam); compositeFoam = null;
            Destroy(shader);
        }
        static void ReleaseTexture(RenderTexture texture) { if (texture != null) { texture.Release(); Destroy(texture); } }
        static void Destroy(Object value) { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
