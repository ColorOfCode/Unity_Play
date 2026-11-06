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
        [Tooltip("在原有吃水位置上额外增加的高度（米）。")]
        public float heightOffset;
        [Tooltip("取船中心及四周的平均海面高度，减少短波抖动；0 表示只采样中心。")]
        [Min(0f)] public float sampleRadius = 1.5f;
        [Tooltip("数值越大越贴近波面；0 表示直接跟随。")]
        [Min(0f)] public float response = 10f;
        [Tooltip("在 Scene 和 Game 视图中预览漂浮；保存和进入运行模式时恢复原始摆放高度。")]
        public bool previewInEditor = true;

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
            if (sampler != null && sampler.TryGetHeights(sampleHeights))
            {
                float sum = 0f;
                for (int i = 0; i < sampleHeights.Length; i++) sum += sampleHeights[i];
                sampledSeaHeight = sum / sampleHeights.Length;
                hasSample = true;
            }
            if (!hasSample) return;
            Vector3 position = target.position;
            float desired = sampledSeaHeight + (preserveInitialHeight ? initialHeight : 0f) + heightOffset;
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
            if (!water.enableWaves || water.WaveDisplacement == null)
            {
                sampler?.Dispose(); sampler = null;
                sampledSeaHeight = water.transform.position.y;
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
            int count = sampleRadius > 0f ? 5 : 1;
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
            Vector3 center = target.position;
            samplePoints[0] = center;
            if (count == 5)
            {
                samplePoints[1] = center + Vector3.right * sampleRadius;
                samplePoints[2] = center - Vector3.right * sampleRadius;
                samplePoints[3] = center + Vector3.forward * sampleRadius;
                samplePoints[4] = center - Vector3.forward * sampleRadius;
            }
            sampler.Request(samplePoints, water.WaveDisplacement, water.WaveFieldCenter,
                water.WaveFieldDiameters, water.transform.position.y, water.WaveFieldCascadeCount);
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
            StopSampling();
        }
        void StopSampling()
        {
            if (subscribedWater != null) subscribedWater.SurfacePrepared -= SampleSurface;
            subscribedWater = null;
            sampler?.Dispose(); sampler = null;
            initialized = false; hasSample = false;
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
