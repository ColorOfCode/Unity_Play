using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Shuimian.Gameplay
{
    [ExecuteAlways, DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Shuimian/船体随浪漂浮")]
    public class ShuimianShipFloat : MonoBehaviour
    {
        [Header("水面与船")]
        public ShuimianWaterMesh water;
        public ShipController controller;
        [Tooltip("留空时使用 ShipController 的实际移动目标。")]
        public Transform floatingTarget;
        [Header("上下漂浮")]
        [Tooltip("保留场景中船根对象相对平均海面的初始高度。")]
        public bool preserveInitialHeight = true;
        [Tooltip("根据当前启用模型自动调整浮力采样范围和相对水线，响应子模型缩放、位置和替换。")]
        public bool autoFitModel = true;
        [Tooltip("模型整体高度中，从底部起算的吃水比例；自动使该水线对齐波面，并叠加初始高度/额外高度偏移。")]
        [Range(0f, .9f)] public float draftFraction = .13f;
        [Tooltip("在原有吃水位置上额外增加的高度（米）。")]
        public float heightOffset;
        [Tooltip("自动模式按模型中心/长宽布置采样；关闭自动模式时使用此半径；0表示只采样中心。")]
        [Min(0f)] public float sampleRadius = 1.5f;
        [Tooltip("数值越大越贴近波面；0 表示直接跟随。")]
        [Min(0f)] public float response = 10f;
        [Tooltip("在 Scene 和 Game 视图中预览漂浮；保存和进入运行模式时恢复原始摆放高度。")]
        public bool previewInEditor = true;

        [Header("随浪俯仰与侧倾")]
        public bool followWaveRotation = true;
        [Tooltip("按船头与船尾的实际浪高差抬头/低头，1为波面坡度。")]
        [Range(0f, 2f)] public float pitchStrength = 1.15f;
        [Range(0f, 2f)] public float rollStrength = .35f;
        [Range(0f, 35f)] public float maxPitchAngle = 15f;
        [Range(0f, 25f)] public float maxRollAngle = 7f;
        [Tooltip("俯仰和侧倾的平滑响应；0表示直接跟随波面。")]
        [Min(0f)] public float rotationResponse = 4f;

        ShuimianWaterMesh subscribedWater;
        ShuimianWaveHeightSampler sampler;
        ComputeShader samplerSource;
        ShuimianShipTerrainCollision terrainCollision;
        Transform target;
        Transform initialTarget;
        ShuimianWaterMesh initialWater;
        Vector3[] samplePoints;
        float[] sampleHeights;
        float initialHeight, sampledSeaHeight;
        bool initialized, hasSample;
        Quaternion appliedWaveTilt = Quaternion.identity;
        Vector3 sampledForward = Vector3.forward, sampledRight = Vector3.right;
        float sampledHalfLength, sampledHalfWidth, pitchSlope, rollSlope;
        public Transform Target => target != null ? target : floatingTarget != null ? floatingTarget
            : controller != null ? controller.Target : transform;
        public Quaternion UnrockedRotation => Quaternion.Inverse(appliedWaveTilt) * Target.rotation;
        /// <summary>Change steering without replacing the currently applied wave attitude.</summary>
        public void SetHeadingRotation(Quaternion heading)
        {
            Target.rotation = appliedWaveTilt * heading;
        }
        public bool TryGetWaterSurfaceHeight(out float height)
        { height = sampledSeaHeight; return initialized && hasSample; }
        public bool TryGetModelFootprint(out ShuimianShipGeometry.Footprint footprint)
            => ShuimianShipGeometry.TryFootprint(Target,
                controller != null ? controller.ForwardFlat : UnrockedRotation * Vector3.forward, UnrockedRotation, out footprint);
#if UNITY_EDITOR
        double lastEditorTime, nextEditorUpdate;
        float authoredHeight, lastPreviewHeight;
        bool previewPoseApplied, savingScene;
#endif

        void OnEnable()
        {
#if UNITY_EDITOR
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorTick;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorSceneManager.sceneSaving += BeforeSceneSave;
            EditorSceneManager.sceneSaved += AfterSceneSave;
#endif
            Initialize();
        }
        void Start() => Initialize();
        bool CanRun
        {
            get
            {
                if (Application.IsPlaying(gameObject)) return true;
#if UNITY_EDITOR
                return previewInEditor && !savingScene && !EditorApplication.isPlayingOrWillChangePlaymode
                    && gameObject.scene.IsValid() && gameObject.scene.isLoaded
                    && !EditorUtility.IsPersistent(this) && PrefabStageUtility.GetPrefabStage(gameObject) == null;
#else
                return false;
#endif
            }
        }
        void Initialize()
        {
            if (initialized || !CanRun) return;
            if (water == null) water = FindObjectOfType<ShuimianWaterMesh>();
            if (controller == null) controller = GetComponent<ShipController>();
            target = floatingTarget != null ? floatingTarget : controller != null ? controller.Target : transform;
            if (water == null || target == null) return;
            if (initialTarget != target || initialWater != water)
            {
                initialHeight = target.position.y - water.transform.position.y;
                initialTarget = target; initialWater = water;
#if UNITY_EDITOR
                authoredHeight = target.position.y;
#endif
            }
            subscribedWater = water;
            subscribedWater.SurfacePrepared += SampleSurface;
            initialized = true;
        }
        void LateUpdate()
        {
            if (Application.IsPlaying(gameObject)) ApplyHeight(Time.deltaTime);
        }
        void ApplyHeight(float deltaTime)
        {
            if (!CanRun) return;
            if (!initialized) Initialize();
            if (!initialized || target == null) return;
            if (autoFitModel && !TryGetModelFootprint(out _)) return;
            if (sampler != null && sampler.TryGetHeights(sampleHeights))
            {
                float sum = 0f;
                for (int i = 0; i < sampleHeights.Length; i++) sum += sampleHeights[i];
                sampledSeaHeight = sum / sampleHeights.Length;
                pitchSlope = sampleHeights.Length == 5 && sampledHalfLength > .001f
                    ? (sampleHeights[3] - sampleHeights[4]) / (2f * sampledHalfLength) : 0f;
                rollSlope = sampleHeights.Length == 5 && sampledHalfWidth > .001f
                    ? (sampleHeights[1] - sampleHeights[2]) / (2f * sampledHalfWidth) : 0f;
                hasSample = true;
            }
            if (!hasSample) return;
            ApplyWaveRotation(deltaTime);
            Vector3 position = target.position;
            float desired = sampledSeaHeight + (preserveInitialHeight ? initialHeight : 0f) + heightOffset;
            if (autoFitModel && TryGetModelFootprint(out var hull))
            {
                float offset = hull.Waterline(draftFraction) - target.position.y;
                desired -= offset;
            }
            float blend = response <= 0f ? 1f : 1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime));
            position.y = Mathf.Lerp(position.y, desired, blend);
            if (terrainCollision == null) terrainCollision = GetComponent<ShuimianShipTerrainCollision>();
            if (terrainCollision != null && terrainCollision.isActiveAndEnabled)
                position.y = terrainCollision.ClampFloatHeight(target, position.y);
            target.position = position;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                previewPoseApplied = true;
                lastPreviewHeight = position.y;
            }
#endif
        }
        void ApplyWaveRotation(float deltaTime)
        {
            Quaternion level = UnrockedRotation;
            float pitch = followWaveRotation ? Mathf.Clamp(Mathf.Atan(pitchSlope) * Mathf.Rad2Deg * pitchStrength, -maxPitchAngle, maxPitchAngle) : 0f;
            float roll = followWaveRotation ? Mathf.Clamp(Mathf.Atan(rollSlope) * Mathf.Rad2Deg * rollStrength, -maxRollAngle, maxRollAngle) : 0f;
            var desired = Quaternion.AngleAxis(-pitch, sampledRight) * Quaternion.AngleAxis(roll, sampledForward);
            float blend = rotationResponse <= 0f ? 1f : 1f - Mathf.Exp(-rotationResponse * Mathf.Max(0f, deltaTime));
            appliedWaveTilt = Quaternion.Slerp(appliedWaveTilt, desired, blend);
            target.rotation = appliedWaveTilt * level;
        }
        void RestoreWaveRotation()
        {
            if (target != null) target.rotation = UnrockedRotation;
            appliedWaveTilt = Quaternion.identity;
        }
        void SampleSurface(Camera camera)
        {
            if (!isActiveAndEnabled || !CanRun) return;
            if (!initialized) Initialize();
            if (!initialized || target == null || water == null) return;
            if (Application.IsPlaying(gameObject))
            {
                if (camera.cameraType != CameraType.Game) return;
                var main = Camera.main;
                if (main != null && camera != main) return;
            }
            else if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if ((!water.enableWaves && !water.enableShipInteraction) || water.WaveDisplacement == null)
            {
                sampler?.Dispose(); sampler = null;
                sampledSeaHeight = water.transform.position.y;
                pitchSlope = rollSlope = 0f;
                hasSample = true;
                return;
            }
            if (sampler == null || samplerSource != water.combineShader)
            {
                sampler?.Dispose();
                samplerSource = water.combineShader;
                if (samplerSource == null) return;
                sampler = new ShuimianWaveHeightSampler(samplerSource);
            }
            int count = autoFitModel || sampleRadius > 0f ? 5 : 1;
            // Consume pending results before changing the sample layout.
            if (samplePoints != null && samplePoints.Length != count)
            {
                sampler.Dispose();
                sampler = new ShuimianWaveHeightSampler(samplerSource);
            }
            if (samplePoints == null || samplePoints.Length != count)
            {
                samplePoints = new Vector3[count]; sampleHeights = new float[count];
            }
            var modelHull = default(ShuimianShipGeometry.Footprint);
            bool fit = autoFitModel && TryGetModelFootprint(out modelHull);
            Vector3 center = fit ? modelHull.center : target.position;
            samplePoints[0] = center;
            Vector3 forward = fit ? modelHull.forward : controller != null ? controller.ForwardFlat : UnrockedRotation * Vector3.forward;
            forward.y = 0f; forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = fit ? modelHull.width * .35f : sampleRadius;
            float halfLength = fit ? modelHull.length * .4f : sampleRadius;
            if (count == 5)
            {
                Vector3 across = right * halfWidth;
                Vector3 along = forward * halfLength;
                samplePoints[1] = center + across;
                samplePoints[2] = center - across;
                samplePoints[3] = center + along;
                samplePoints[4] = center - along;
            }
            if (sampler.Request(samplePoints, water.WaveDisplacement, water.WaveFieldCenter,
                water.WaveFieldDiameters, water.transform.position.y, water.WaveFieldCascadeCount))
            {
                // Keep the axes/spans associated with this asynchronous result.
                sampledForward = forward; sampledRight = right;
                sampledHalfLength = halfLength; sampledHalfWidth = halfWidth;
            }
        }
        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorSceneManager.sceneSaving -= BeforeSceneSave;
            EditorSceneManager.sceneSaved -= AfterSceneSave;
            if (!Application.IsPlaying(gameObject)) RestorePreviewPose();
#endif
            RestoreWaveRotation();
            StopSampling();
        }
        void StopSampling()
        {
            if (subscribedWater != null) subscribedWater.SurfacePrepared -= SampleSurface;
            subscribedWater = null;
            sampler?.Dispose(); sampler = null;
            initialized = false; hasSample = false;
            pitchSlope = rollSlope = 0f;
        }
#if UNITY_EDITOR
        void EditorTick()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!CanRun)
            {
                RestorePreviewPose();
                StopSampling();
                initialTarget = null; initialWater = null;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            if (now < nextEditorUpdate) return;
            float deltaTime = Mathf.Clamp((float)(now - lastEditorTime), 0f, .1f);
            lastEditorTime = now; nextEditorUpdate = now + 1.0 / 30.0;
            // Preserve an intentional Y adjustment made with the editor move tool.
            if (previewPoseApplied && target != null && Mathf.Abs(target.position.y - lastPreviewHeight) > .0001f)
            {
                float edit = target.position.y - lastPreviewHeight;
                authoredHeight += edit; initialHeight += edit;
            }
            ApplyHeight(deltaTime);
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        void RestorePreviewPose()
        {
            if (!previewPoseApplied || target == null) return;
            var position = target.position;
            position.y = authoredHeight;
            target.position = position;
            RestoreWaveRotation();
            previewPoseApplied = false;
        }
        void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RestorePreviewPose();
                StopSampling();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                initialTarget = null; initialWater = null;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }
        }
        void BeforeSceneSave(Scene scene, string path)
        {
            if (Application.isPlaying || gameObject.scene != scene) return;
            savingScene = true;
            RestorePreviewPose();
        }
        void AfterSceneSave(Scene scene)
        {
            if (gameObject.scene == scene) savingScene = false;
        }
#endif
    }
}
