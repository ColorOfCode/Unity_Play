using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ObscuredByClouds.Gameplay
{
    /// <summary>Original procedural sky features, driven by one directional light without cloud-plugin or timeline effects.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("ObscuredByClouds/Time Of Day 昼夜变化")]
    public sealed class TimeOfDay : MonoBehaviour
    {
        [Header("天空与唯一平行光")]
        public Material SkyboxMaterial;
        [Tooltip("白天照太阳，夜间切换为月光；始终复用这一盏 Directional Light。")]
        public Transform SunLight;
        public Texture2D MoonTexture;
        public float SunMoonAngleAdjust = -5;
        [Header("时间控制")]
        public bool UseTimeline = true;
        [Range(0, 24)] public float Timeline = 12;
        public float TimelineOffset = -12;
        public bool ProgressTime = true;
        [Tooltip("每秒经过的游戏小时数；0.02 对应现实 20 分钟一个昼夜。")]
        public float TimelineSpeed = .02f;
        public float Latitude = 130.5f;
        public float Longitude = 87;
        [Header("天空颜色（原 TimeOfDay 曲线）")]
        [GradientUsage(true)] public Gradient HorizonColor = new Gradient();
        [GradientUsage(true)] public Gradient ZenithColor = new Gradient();
        public AnimationCurve HorizonFalloff = AnimationCurve.Linear(-1, 1, 1, 1);
        public float HorizonTilt = .2f;
        public float HorizonOffset = -.2f;
        [Header("太阳与月亮")]
        [GradientUsage(true)] public Gradient SunColor = new Gradient();
        public AnimationCurve SunIntensity = AnimationCurve.Linear(-1, .5f, 1, .5f);
        public float SunRadius = .02f, SunBloom = 1.27f, SunScattering = .3f;
        [GradientUsage(true)] public Gradient MoonColor = new Gradient();
        public AnimationCurve MoonIntensity = AnimationCurve.Linear(-1, .5f, 1, .5f);
        [Tooltip("月亮尺寸倍率，默认 2；无上限，增大数值会持续放大月亮。")]
        public float MoonSize = 2f;
        public float MoonBloom = 5, MoonScattering = .5f;
        [Header("夜空")]
        [GradientUsage(true)] public Gradient StarsColor = new Gradient();
        [Min(0)] public float StarsIntensity = 100f;
        public Vector2 StarTilling = new Vector2(2, 2);
        [Header("T_Background 静态云层")]
        [GradientUsage(true)] public Gradient CloudsBackgroundColor = new Gradient();
        public AnimationCurve CloudsBackgroundBrightness = AnimationCurve.Linear(-1, .5f, 1, .5f);
        [Header("主光与环境光")]
        public bool ControlLighting = true;
        public Gradient SunLightColor = new Gradient();
        public AnimationCurve SunLightIntensity = AnimationCurve.Linear(0, 1, 1, 1);
        public Gradient MoonLightColor = new Gradient();
        public AnimationCurve MoonLightIntensity = AnimationCurve.Linear(0, 1, 1, .5f);
        [GradientUsage(true)] public Gradient SkyLightColor = new Gradient();
        [Min(0)] public float DayLightMultiplier = 1;
        [Min(0)] public float MoonLightMultiplier = .15f;
        public Color DayAmbientSky, DayAmbientEquator, DayAmbientGround;
        [Range(0, 1)] public float NightAmbientBrightness = .15f;
        public bool ControlFog = true;
        public Color DayFogColor;
        [Header("整体环境光 / 间接光")]
        public bool ControlIndirectLighting = true;
        [Tooltip("最终间接光 = 材质 Indirect Light Strength × 此倍率 × 时间曲线。主光、火光与自发光保留。")]
        [Min(0)] public float GlobalIndirectLightStrength = 1f;
        [Tooltip("横轴直接使用 Timeline 的 0～24 小时，纵轴为间接光倍率。")]
        public AnimationCurve IndirectLightByTimeline = new AnimationCurve(
            new Keyframe(0, .025f), new Keyframe(5, .04f), new Keyframe(6, .12f),
            new Keyframe(8, .65f), new Keyframe(12, 1f), new Keyframe(16, .7f),
            new Keyframe(18, .16f), new Keyframe(20, .05f), new Keyframe(24, .025f));
        public float CurrentIndirectLightStrength { get; private set; } = 1f;
        [Header("环境反射更新")]
        public bool UpdateEnvironment = true;
        [Min(.5f)] public float EnvironmentUpdateInterval = 2;
        public float SunElevation { get; private set; }
        public bool IsNight => SunElevation < 0;
        public float EffectiveHour => Mathf.Repeat(Timeline + TimelineOffset, 24);
        float lastEnvironmentTime = -100, lastEnvironmentHour = -100;
        Material previousSky;
        Quaternion previousRotation;
        Color previousLightColor, previousAmbientSky, previousAmbientEquator, previousAmbientGround, previousFog;
        float previousLightIntensity, previousReflection;
        AmbientMode previousAmbientMode;
        DefaultReflectionMode previousReflectionMode;
        Light previousSun;
        bool captured;
        Quaternion manualSunRotation, lastAppliedRotation;
        bool manualInitialized;

        void OnEnable()
        {
            if (!SunLight || !SkyboxMaterial) return;
            var light = SunLight.GetComponent<Light>();
            if (!light || light.type != LightType.Directional) return;
            previousSky = RenderSettings.skybox; previousSun = RenderSettings.sun;
            previousRotation = SunLight.rotation; previousLightColor = light.color; previousLightIntensity = light.intensity;
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientSky = RenderSettings.ambientSkyColor; previousAmbientEquator = RenderSettings.ambientEquatorColor; previousAmbientGround = RenderSettings.ambientGroundColor;
            previousFog = RenderSettings.fogColor; previousReflection = RenderSettings.reflectionIntensity; previousReflectionMode = RenderSettings.defaultReflectionMode;
            captured = true; ApplyTimeOfDay();
        }
        void OnValidate()
        {
            Timeline = Mathf.Repeat(Timeline, 24);
#if UNITY_EDITOR
            EditorApplication.delayCall -= RefreshEditorLighting;
            EditorApplication.delayCall += RefreshEditorLighting;
#endif
        }
#if UNITY_EDITOR
        void RefreshEditorLighting()
        {
            if (!this || !isActiveAndEnabled) return;
            ApplyTimeOfDay(); SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
        void Update()
        {
            if (Application.IsPlaying(gameObject) && ProgressTime) Timeline = Mathf.Repeat(Timeline + TimelineSpeed * Time.deltaTime, 24);
            ApplyTimeOfDay();
        }
        static Vector4 Rgb(Color c) => new Vector4(c.r, c.g, c.b, c.a);
        static Color Ratio(Color value, Color reference) => new Color(value.r / Mathf.Max(.02f, reference.r), value.g / Mathf.Max(.02f, reference.g), value.b / Mathf.Max(.02f, reference.b));
        public void ApplyTimeOfDay()
        {
            if (!isActiveAndEnabled || !SunLight || !SkyboxMaterial) return;
#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(this) || PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
#endif
            var light = SunLight.GetComponent<Light>();
            if (!light || light.type != LightType.Directional) return;
            if (!UseTimeline && (!manualInitialized || Quaternion.Angle(SunLight.rotation, lastAppliedRotation) > .01f))
            { manualSunRotation = SunLight.rotation; manualInitialized = true; }
            Quaternion sun = UseTimeline ? Quaternion.Euler(0, Longitude, -Latitude) * Quaternion.Euler(EffectiveHour * 15 - 90, 180, 0) : manualSunRotation;
            Quaternion moon = sun * Quaternion.Euler(SunMoonAngleAdjust, -180, 0);
            Vector3 sunDirection = -(sun * Vector3.forward), moonDirection = -(moon * Vector3.forward);
            SunElevation = sunDirection.y;
            float elevation01 = Mathf.Clamp01(SunElevation * .5f + .5f), moonElevation = moonDirection.y;
            float maximumElevation = Mathf.Max(.1f, Mathf.Abs(Mathf.Cos(Latitude * Mathf.Deg2Rad)));
            float dayReference = .5f + maximumElevation * .5f;
            float daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.15f, .25f, SunElevation));
            RenderSettings.skybox = SkyboxMaterial;
            CurrentIndirectLightStrength = ControlIndirectLighting
                ? Mathf.Max(0, GlobalIndirectLightStrength) * Mathf.Max(0, IndirectLightByTimeline != null ? IndirectLightByTimeline.Evaluate(Mathf.Repeat(Timeline, 24)) : 1f) : 1f;
            Shader.SetGlobalVector("_OBCIndirectLightControl", new Vector4(ControlIndirectLighting ? 1f : 0f, CurrentIndirectLightStrength, 0, 0));
            Shader.SetGlobalFloat("_ObcTodEnabled", 1);
            Color horizon = HorizonColor.Evaluate(elevation01), zenith = ZenithColor.Evaluate(elevation01);
            Color sunColor = SunColor.Evaluate(elevation01), moonColor = MoonColor.Evaluate(elevation01);
            Color stars = StarsColor.Evaluate(elevation01), clouds = CloudsBackgroundColor.Evaluate(elevation01);
            float sunIntensity = Mathf.Max(0, SunIntensity.Evaluate(SunElevation)), moonIntensity = Mathf.Max(0, MoonIntensity.Evaluate(moonElevation));
            float falloff = Mathf.Max(.001f, HorizonFalloff.Evaluate(SunElevation));
            float cloudBrightness = Mathf.Max(0, CloudsBackgroundBrightness.Evaluate(SunElevation));
            Shader.SetGlobalVector("_ObcTodHorizonColor", Rgb(horizon));
            Shader.SetGlobalVector("_ObcTodZenithColor", Rgb(zenith));
            Shader.SetGlobalVector("_ObcTodSkyParams", new Vector4(falloff, HorizonTilt, HorizonOffset, 0));
            Shader.SetGlobalVector("_ObcTodSunDirection", sunDirection);
            Shader.SetGlobalVector("_ObcTodMoonDirection", moonDirection);
            Shader.SetGlobalVector("_ObcTodSunColor", Rgb(sunColor));
            Shader.SetGlobalVector("_ObcTodMoonColor", Rgb(moonColor));
            Shader.SetGlobalVector("_ObcTodDiscParams", new Vector4(Mathf.Max(0, SunRadius), Mathf.Max(.0001f, MoonSize), SunBloom, MoonBloom));
            Shader.SetGlobalVector("_ObcTodScatterParams", new Vector4(SunScattering, MoonScattering, sunIntensity, moonIntensity));
            Shader.SetGlobalVector("_ObcTodStars", Rgb(stars * Mathf.Max(0, StarsIntensity)));
            Shader.SetGlobalVector("_ObcTodCloudColor", Rgb(clouds));
            Shader.SetGlobalVector("_ObcTodBackgroundParams", new Vector4(StarTilling.x, StarTilling.y, cloudBrightness, 0));
            Shader.SetGlobalTexture("_ObcTodMoonTex", MoonTexture ? MoonTexture : Texture2D.whiteTexture);
            var background = SkyboxMaterial.GetTexture("_BackGroundTex");
            Shader.SetGlobalTexture("_ObcTodBackgroundTex", background ? background : Texture2D.blackTexture);
            // Keep the original ProcedualSky material controls live as well as
            // passing the same values to the ocean's reflection/horizon shader.
            SkyboxMaterial.SetVector("_SunDirection", sunDirection); SkyboxMaterial.SetVector("_MoonDirection", moonDirection);
            SkyboxMaterial.SetColor("_HorizonColor", horizon); SkyboxMaterial.SetColor("_ZenithColor", zenith);
            SkyboxMaterial.SetFloat("_HorizonFalloff", falloff); SkyboxMaterial.SetFloat("_HorizonTilt", HorizonTilt); SkyboxMaterial.SetFloat("_HorizonOffset", HorizonOffset);
            SkyboxMaterial.SetColor("_SunColor", sunColor); SkyboxMaterial.SetFloat("_SunIntensity", sunIntensity);
            SkyboxMaterial.SetFloat("_SunRadius", Mathf.Max(0, SunRadius)); SkyboxMaterial.SetFloat("_SunBloom", SunBloom); SkyboxMaterial.SetFloat("_SunScattering", SunScattering);
            SkyboxMaterial.SetColor("_MoonColor", moonColor); SkyboxMaterial.SetFloat("_MoonIntensity", moonIntensity);
            SkyboxMaterial.SetFloat("_MoonSize", Mathf.Max(.0001f, MoonSize)); SkyboxMaterial.SetFloat("_MoonBloom", MoonBloom); SkyboxMaterial.SetFloat("_MoonScattering", MoonScattering);
            if (MoonTexture) SkyboxMaterial.SetTexture("_MoonTex", MoonTexture);
            SkyboxMaterial.SetColor("_StarsColor", stars); SkyboxMaterial.SetFloat("_StarsIntensity", Mathf.Max(0, StarsIntensity));
            SkyboxMaterial.SetVector("_StarTilling", new Vector4(StarTilling.x, StarTilling.y, 0, 0));
            SkyboxMaterial.SetColor("_CloudsBackgroundColor", clouds); SkyboxMaterial.SetFloat("_CloudsBackgroundBrightness", cloudBrightness);
            if (ControlLighting)
            {
                // Fade to zero around the horizon before changing the single light's direction.
                bool sunIsKey = SunElevation >= 0;
                Vector3 direction = sunIsKey ? sunDirection : moonDirection;
                light.transform.rotation = Quaternion.LookRotation(-direction);
                lastAppliedRotation = light.transform.rotation;
                float visibility = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .12f, Mathf.Abs(SunElevation)))
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .03f, direction.y));
                light.color = (sunIsKey ? SunLightColor.Evaluate(SunElevation) : MoonLightColor.Evaluate(moonElevation)).linear;
                light.intensity = visibility * Mathf.Max(0, sunIsKey ? SunLightIntensity.Evaluate(SunElevation) * DayLightMultiplier : MoonLightIntensity.Evaluate(moonElevation) * MoonLightMultiplier);
                RenderSettings.sun = light;
                Color ambientTint = Ratio(SkyLightColor.Evaluate(elevation01), SkyLightColor.Evaluate(dayReference));
                float ambientStrength = Mathf.Lerp(NightAmbientBrightness, 1, daylight);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = DayAmbientSky * ambientTint * ambientStrength;
                RenderSettings.ambientEquatorColor = DayAmbientEquator * ambientTint * ambientStrength;
                RenderSettings.ambientGroundColor = DayAmbientGround * ambientTint * ambientStrength;
                if (ControlFog) RenderSettings.fogColor = DayFogColor * ambientTint * Mathf.Lerp(.035f, 1, daylight);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity = Mathf.Lerp(.12f, 1, daylight);
            }
            if (UpdateEnvironment && Time.realtimeSinceStartup - lastEnvironmentTime >= Mathf.Max(.5f, EnvironmentUpdateInterval)
                && Mathf.Abs(Mathf.DeltaAngle(EffectiveHour * 15, lastEnvironmentHour * 15)) > .75f)
            {
                DynamicGI.UpdateEnvironment(); lastEnvironmentTime = Time.realtimeSinceStartup; lastEnvironmentHour = EffectiveHour;
            }
        }
        void OnDisable()
        {
            Shader.SetGlobalVector("_OBCIndirectLightControl", Vector4.zero);
            Shader.SetGlobalFloat("_ObcTodEnabled", 0);
            if (!captured) return;
            if (RenderSettings.skybox == SkyboxMaterial) RenderSettings.skybox = previousSky;
            if (SunLight)
            {
                var light = SunLight.GetComponent<Light>(); SunLight.rotation = previousRotation;
                if (light) { light.color = previousLightColor; light.intensity = previousLightIntensity; }
            }
            RenderSettings.sun = previousSun; RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousAmbientSky; RenderSettings.ambientEquatorColor = previousAmbientEquator; RenderSettings.ambientGroundColor = previousAmbientGround;
            RenderSettings.fogColor = previousFog; RenderSettings.reflectionIntensity = previousReflection; RenderSettings.defaultReflectionMode = previousReflectionMode;
            captured = false;
        }
    }
}
