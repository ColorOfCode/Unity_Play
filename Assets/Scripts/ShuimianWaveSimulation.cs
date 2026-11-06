// FFT spectrum and transform stages are adapted from Crest Ocean System.
// Copyright 2021 Wave Harmonic Ltd. Inspired by speps/GX-EncinoWaves.
// This controller owns independent compute shader instances and GPU resources.

using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Shuimian.Gameplay
{
    /// <summary>
    /// Water scene's FFT wave spectrum with no dependency on its C# components.
    /// Output RGBA is world-space displacement (X,Y,Z) and zero variance, in
    /// camera-centered texture cascades. Time is supplied by the caller for preview,
    /// runtime animation and deterministic rendering tests.
    /// </summary>
    public sealed class ShuimianWaveSimulation : IDisposable
    {
        const int FftResolution = 32;
        const int RawCascadeCount = 16;
        const int FftHorizontalKernel = 4;
        const int FftVerticalKernel = 5;

        // Copied values from Water / WaveSpectrumIsland_Windy, not a reference to
        // that ScriptableObject. Independent settings can be added here later.
        static readonly float[] SpectrumPowerLog =
        {
            -7.39794f, -7.39794f, -6.121847f, -5.519817f, -4.9178762f,
            -4.178795f, -3.4600666f, -2.7367418f, -2.330296f, -1.6429203f,
            -.77596164f, -.26321554f, .76956f, 1.4295601f
        };

        readonly ComputeShader spectrumShader, fftShader, combineShader;
        readonly int spectrumInitializeKernel, spectrumUpdateKernel, combineKernel, foamKernel;
        RenderTexture spectrumInitial, spectrumHeight, spectrumX, spectrumZ;
        RenderTexture fftTempHeight, fftTempX, fftTempZ, rawDisplacement;
        Texture2D butterfly, spectrumControls;
        bool initialized, spectrumInitialized, disposed;
        int outputResolution, outputCascadeCount;
        float lastWindSpeed, lastTurbulence, lastHeading, lastGravity;
        float lastTime = float.NaN, lastChop = float.NaN;
        RenderTexture foamScratch;
        Vector2 previousFoamCenter, previousFoamDiameters;
        float previousFoamTime = float.NaN;
        bool foamHistoryValid;

        public RenderTexture Displacement { get; private set; }
        /// <summary>Foam coverage in red, using the displacement texture's cascade layout.</summary>
        public RenderTexture Foam { get; private set; }

        public ShuimianWaveSimulation(ComputeShader spectrum, ComputeShader fft, ComputeShader combine)
        {
            if (spectrum == null || fft == null || combine == null)
                throw new ArgumentNullException("Assign all three independent Shuimian FFT compute shaders.");
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports2DArrayTextures)
                throw new NotSupportedException("Shuimian FFT waves need compute shaders and texture arrays.");

            spectrumShader = Object.Instantiate(spectrum);
            fftShader = Object.Instantiate(fft);
            combineShader = Object.Instantiate(combine);
            spectrumShader.hideFlags = fftShader.hideFlags = combineShader.hideFlags = HideFlags.HideAndDontSave;
            spectrumInitializeKernel = spectrumShader.FindKernel("SpectrumInitalize");
            spectrumUpdateKernel = spectrumShader.FindKernel("SpectrumUpdate");
            combineKernel = combineShader.FindKernel("Combine");
            foamKernel = combineShader.FindKernel("UpdateFoam");
        }

        public void Update(Vector2 center, float nearDiameter, float farDiameter, int outputResolution,
            float time, float windSpeedKmh, float turbulence, float chop, float amplitude, float headingDeg, int cascadeCount = 2,
            Texture seaFloorDepth = null, float seaLevel = 0f, float attenuation = 0f, float maximumAttenuationDepth = 1000f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWaveSimulation));
            if (!initialized) Initialize();
            EnsureOutput(Mathf.Clamp(outputResolution, 16, 1024), Mathf.Clamp(cascadeCount, 2, 10));

            float windSpeed = Mathf.Max(.001f, windSpeedKmh / 3.6f);
            turbulence = Mathf.Clamp01(turbulence);
            float gravity = Mathf.Max(.001f, Physics.gravity.magnitude);
            bool conditionsChanged = !spectrumInitialized || windSpeed != lastWindSpeed ||
                turbulence != lastTurbulence || headingDeg != lastHeading || gravity != lastGravity;

            if (conditionsChanged)
            {
                InitializeSpectrum(windSpeed, turbulence, headingDeg, gravity);
                lastWindSpeed = windSpeed; lastTurbulence = turbulence;
                lastHeading = headingDeg; lastGravity = gravity;
                spectrumInitialized = true;
            }

            chop = Mathf.Max(0f, chop);
            if (conditionsChanged || lastTime != time || lastChop != chop)
            {
                UpdateSpectrum(time, chop);
                TransformSpectrum();
                lastTime = time; lastChop = chop;
            }

            nearDiameter = Mathf.Max(1f, nearDiameter);
            farDiameter = Mathf.Max(nearDiameter, farDiameter);
            combineShader.SetInt("_OutputResolution", this.outputResolution);
            combineShader.SetInt("_CascadeCount", outputCascadeCount);
            combineShader.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
            combineShader.SetVector("_Diameters", new Vector4(nearDiameter, farDiameter, 0f, 0f));
            combineShader.SetFloat("_Amplitude", Mathf.Max(0f, amplitude));
            // Depth uses the same centered cascade layout as the output field:
            // R = sea-floor world height, G = sea-level offset. Attenuate each
            // wavelength band before summation, matching Water's ShapeFFT input.
            bool hasDepth = seaFloorDepth != null && seaFloorDepth.dimension == TextureDimension.Tex2DArray
                && (seaFloorDepth is Texture2DArray array ? array.depth >= outputCascadeCount
                    : seaFloorDepth is RenderTexture renderTexture && renderTexture.volumeDepth >= outputCascadeCount);
            combineShader.SetInt("_WaveHasDepth", hasDepth ? 1 : 0);
            combineShader.SetFloat("_WaveSeaLevel", seaLevel);
            combineShader.SetFloat("_ShallowAttenuation", hasDepth ? Mathf.Clamp01(attenuation) : 0f);
            combineShader.SetFloat("_MaximumAttenuationDepth", Mathf.Clamp(maximumAttenuationDepth, 1f, 1000f));
            // A separate read-only array is a safe fallback, including when the
            // shader skips depth. Never bind the output as both SRV and UAV.
            combineShader.SetTexture(combineKernel, "_WaveSeaFloorDepth", hasDepth ? seaFloorDepth : rawDisplacement);
            combineShader.SetTexture(combineKernel, "_RawDisplacement", rawDisplacement);
            combineShader.SetTexture(combineKernel, "_Displacement", Displacement);
            int groups = (this.outputResolution + 7) / 8;
            combineShader.Dispatch(combineKernel, groups, groups, outputCascadeCount);
        }

        /// <summary>
        /// Updates persistent whitecaps and, when supplied, shallow-water foam.
        /// Call after Update with the same center, diameters, cascade count and time.
        /// Sea-floor data uses red = terrain world height, green = sea-level offset.
        /// Defaults are copied values from Water's foam settings; no Crest components
        /// or simulation assets are referenced. The existing ocean material supplies
        /// the small-scale foam texture and its lighting appearance.
        /// </summary>
        public void UpdateFoam(Vector2 center, float nearDiameter, float farDiameter,
            int cascadeCount, float time, float waveFoamStrength = 3.27f,
            float waveFoamCoverage = .76f, float foamFadeRate = .8f,
            Texture seaFloorDepth = null, float seaLevel = 0f,
            float shorelineFoamMaxDepth = 1f, float shorelineFoamStrength = 2f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShuimianWaveSimulation));
            if (Displacement == null)
                throw new InvalidOperationException("Call the wave Update before UpdateFoam.");
            if (Mathf.Clamp(cascadeCount, 2, 10) != outputCascadeCount)
                throw new ArgumentException("Foam and wave cascade counts must match.", nameof(cascadeCount));
            EnsureFoamOutput();

            nearDiameter = Mathf.Max(1f, nearDiameter);
            farDiameter = Mathf.Max(nearDiameter, farDiameter);
            float elapsed = foamHistoryValid ? time - previousFoamTime : 0f;
            // Timeline rewinds and long edit-mode pauses need a fresh field instead
            // of carrying foam and wave velocity from an unrelated simulation time.
            bool prewarm = !foamHistoryValid || elapsed < 0f || elapsed > 1f;
            float deltaTime = prewarm ? 0f : Mathf.Min(elapsed, .25f);
            bool hasDepth = seaFloorDepth != null && seaFloorDepth.dimension == TextureDimension.Tex2DArray;

            combineShader.SetInt("_OutputResolution", outputResolution);
            combineShader.SetInt("_CascadeCount", outputCascadeCount);
            combineShader.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
            combineShader.SetVector("_Diameters", new Vector4(nearDiameter, farDiameter, 0f, 0f));
            combineShader.SetVector("_FoamPreviousCenter", new Vector4(previousFoamCenter.x, previousFoamCenter.y, 0f, 0f));
            combineShader.SetVector("_FoamPreviousDiameters", new Vector4(previousFoamDiameters.x, previousFoamDiameters.y, 0f, 0f));
            combineShader.SetInt("_FoamHistoryValid", prewarm ? 0 : 1);
            combineShader.SetInt("_FoamPrewarm", prewarm ? 1 : 0);
            combineShader.SetInt("_FoamHasDepth", hasDepth ? 1 : 0);
            combineShader.SetFloat("_FoamDeltaTime", deltaTime);
            combineShader.SetFloat("_FoamStrength", Mathf.Max(0f, waveFoamStrength));
            combineShader.SetFloat("_FoamCoverage", Mathf.Clamp01(waveFoamCoverage));
            combineShader.SetFloat("_FoamFadeRate", Mathf.Max(.001f, foamFadeRate));
            combineShader.SetFloat("_FoamSeaLevel", seaLevel);
            combineShader.SetFloat("_FoamShoreMaxDepth", Mathf.Max(.01f, shorelineFoamMaxDepth));
            combineShader.SetFloat("_FoamShoreStrength", Mathf.Max(0f, shorelineFoamStrength));
            combineShader.SetTexture(foamKernel, "_FoamDisplacement", Displacement);
            combineShader.SetTexture(foamKernel, "_FoamHistory", Foam);
            // Bind a compatible array even when shoreline generation is disabled.
            combineShader.SetTexture(foamKernel, "_FoamSeaFloorDepth", hasDepth ? seaFloorDepth : Displacement);
            combineShader.SetTexture(foamKernel, "_FoamOutput", foamScratch);
            int groups = (outputResolution + 7) / 8;
            combineShader.Dispatch(foamKernel, groups, groups, outputCascadeCount);

            RenderTexture previous = Foam;
            Foam = foamScratch;
            foamScratch = previous;
            previousFoamCenter = center;
            previousFoamDiameters = new Vector2(nearDiameter, farDiameter);
            previousFoamTime = time;
            foamHistoryValid = true;
        }

        void EnsureFoamOutput()
        {
            if (Foam != null && foamScratch != null) return;
            ReleaseFoamOutput();
            // The spare channels store the previous horizontal displacement for
            // wave-driven foam advection; only red is exposed to the water shader.
            Foam = CreateArray("Shuimian Foam", outputResolution, outputCascadeCount, RenderTextureFormat.ARGBHalf);
            foamScratch = CreateArray("Shuimian Foam History", outputResolution, outputCascadeCount, RenderTextureFormat.ARGBHalf);
            foamHistoryValid = false;
        }

        void ReleaseFoamOutput()
        {
            ReleaseTexture(Foam); ReleaseTexture(foamScratch);
            Foam = null; foamScratch = null;
            foamHistoryValid = false;
            previousFoamTime = float.NaN;
        }

        void Initialize()
        {
            spectrumInitial = CreateArray("Shuimian FFT Initial Spectrum", FftResolution, RawCascadeCount, RenderTextureFormat.ARGBFloat);
            spectrumHeight = CreateArray("Shuimian FFT Height Spectrum", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            spectrumX = CreateArray("Shuimian FFT X Spectrum", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            spectrumZ = CreateArray("Shuimian FFT Z Spectrum", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            fftTempHeight = CreateArray("Shuimian FFT Temp Height", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            fftTempX = CreateArray("Shuimian FFT Temp X", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            fftTempZ = CreateArray("Shuimian FFT Temp Z", FftResolution, RawCascadeCount, RenderTextureFormat.RGFloat);
            rawDisplacement = CreateArray("Shuimian FFT Raw Displacement", FftResolution, RawCascadeCount, RenderTextureFormat.ARGBHalf);
            rawDisplacement.wrapMode = TextureWrapMode.Repeat;

            int passes = Mathf.RoundToInt(Mathf.Log(FftResolution, 2f));
            butterfly = new Texture2D(FftResolution, passes, TextureFormat.RGBAFloat, false, true)
            { name = "Shuimian FFT Butterfly", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var colors = new Color[FftResolution * passes];
            int offset = 1, iterations = FftResolution >> 1;
            for (int row = 0; row < passes; row++)
            {
                int start = 0, end = 2 * offset;
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    float k = 0f;
                    for (int index = start; index < end; index += 2)
                    {
                        float phase = 2f * Mathf.PI * k * iterations / FftResolution;
                        float cos = Mathf.Cos(phase), sin = Mathf.Sin(phase);
                        colors[row * FftResolution + index / 2] = new Color(cos, -sin, 0f, 1f);
                        colors[row * FftResolution + index / 2 + offset] = new Color(-cos, sin, 0f, 1f);
                        k++;
                    }
                    start += 4 * offset; end = start + 2 * offset;
                }
                iterations >>= 1; offset <<= 1;
            }
            butterfly.SetPixels(colors); butterfly.Apply(false, true);

            spectrumControls = new Texture2D(SpectrumPowerLog.Length, 1, TextureFormat.RFloat, false, true)
            {
                name = "Shuimian Windy Spectrum", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var spectrumPixels = new Color[SpectrumPowerLog.Length];
            for (int i = 0; i < spectrumPixels.Length; i++)
                spectrumPixels[i] = Color.white * Mathf.Pow(10f, SpectrumPowerLog[i]);
            spectrumControls.SetPixels(spectrumPixels); spectrumControls.Apply(false, true);
            initialized = true;
        }

        void EnsureOutput(int resolution, int cascadeCount)
        {
            if (Displacement != null && outputResolution == resolution && outputCascadeCount == cascadeCount) return;
            ReleaseTexture(Displacement);
            ReleaseFoamOutput();
            outputResolution = resolution;
            outputCascadeCount = cascadeCount;
            Displacement = CreateArray("Shuimian Wave Displacement", resolution, cascadeCount, RenderTextureFormat.ARGBHalf);
        }

        static RenderTexture CreateArray(string name, int resolution, int slices, RenderTextureFormat format)
        {
            var texture = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave,
                dimension = TextureDimension.Tex2DArray, volumeDepth = slices,
                enableRandomWrite = true, antiAliasing = 1,
                useMipMap = false, autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            if (!texture.Create())
            {
                DestroyObject(texture);
                throw new InvalidOperationException("Cannot create " + name);
            }
            return texture;
        }

        void InitializeSpectrum(float windSpeed, float turbulence, float heading, float gravity)
        {
            spectrumShader.SetInt("_Size", FftResolution);
            spectrumShader.SetFloat("_WindSpeed", windSpeed);
            spectrumShader.SetFloat("_Turbulence", turbulence);
            spectrumShader.SetFloat("_Gravity", gravity);
            spectrumShader.SetFloat("_Period", -1f);
            float radians = heading * Mathf.Deg2Rad;
            spectrumShader.SetVector("_WindDir", new Vector4(Mathf.Cos(radians), Mathf.Sin(radians), 0f, 0f));
            spectrumShader.SetTexture(spectrumInitializeKernel, "_SpectrumControls", spectrumControls);
            spectrumShader.SetTexture(spectrumInitializeKernel, "_ResultInit", spectrumInitial);
            spectrumShader.Dispatch(spectrumInitializeKernel, FftResolution / 8, FftResolution / 8, RawCascadeCount);
        }

        void UpdateSpectrum(float time, float chop)
        {
            spectrumShader.SetInt("_Size", FftResolution);
            spectrumShader.SetFloat("_Time", time);
            spectrumShader.SetFloat("_Chop", chop);
            spectrumShader.SetFloat("_Period", -1f);
            spectrumShader.SetTexture(spectrumUpdateKernel, "_Init0", spectrumInitial);
            spectrumShader.SetTexture(spectrumUpdateKernel, "_ResultHeight", spectrumHeight);
            spectrumShader.SetTexture(spectrumUpdateKernel, "_ResultDisplaceX", spectrumX);
            spectrumShader.SetTexture(spectrumUpdateKernel, "_ResultDisplaceZ", spectrumZ);
            spectrumShader.Dispatch(spectrumUpdateKernel, FftResolution / 8, FftResolution / 8, RawCascadeCount);
        }

        void TransformSpectrum()
        {
            fftShader.SetTexture(FftHorizontalKernel, "_InputH", spectrumHeight);
            fftShader.SetTexture(FftHorizontalKernel, "_InputX", spectrumX);
            fftShader.SetTexture(FftHorizontalKernel, "_InputZ", spectrumZ);
            fftShader.SetTexture(FftHorizontalKernel, "_InputButterfly", butterfly);
            fftShader.SetTexture(FftHorizontalKernel, "_Output1", fftTempHeight);
            fftShader.SetTexture(FftHorizontalKernel, "_Output2", fftTempX);
            fftShader.SetTexture(FftHorizontalKernel, "_Output3", fftTempZ);
            fftShader.Dispatch(FftHorizontalKernel, 1, FftResolution, RawCascadeCount);

            fftShader.SetTexture(FftVerticalKernel, "_InputH", fftTempHeight);
            fftShader.SetTexture(FftVerticalKernel, "_InputX", fftTempX);
            fftShader.SetTexture(FftVerticalKernel, "_InputZ", fftTempZ);
            fftShader.SetTexture(FftVerticalKernel, "_InputButterfly", butterfly);
            fftShader.SetTexture(FftVerticalKernel, "_Output", rawDisplacement);
            fftShader.Dispatch(FftVerticalKernel, FftResolution, 1, RawCascadeCount);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseTexture(spectrumInitial); ReleaseTexture(spectrumHeight);
            ReleaseTexture(spectrumX); ReleaseTexture(spectrumZ);
            ReleaseTexture(fftTempHeight); ReleaseTexture(fftTempX); ReleaseTexture(fftTempZ);
            ReleaseTexture(rawDisplacement); ReleaseTexture(Displacement);
            ReleaseFoamOutput();
            DestroyObject(butterfly); DestroyObject(spectrumControls);
            DestroyObject(spectrumShader); DestroyObject(fftShader); DestroyObject(combineShader);
            Displacement = null;
        }

        static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            DestroyObject(texture);
        }
        static void DestroyObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
