using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shuimian.Gameplay.Editor
{
    [CustomEditor(typeof(ShuimianShipModel))]
    public class ShuimianShipModelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("固定船体不受外观影响。替换会按船长等比缩放、居中并对齐水线。船头反向时，将外观节点绕 Y 旋转 180 度。", MessageType.Info);
            if (GUILayout.Button("替换外观（保持浮力、尾迹与阻挡）"))
            {
                var body = (ShuimianShipModel)target;
                try { Replace(body); EditorSceneManager.MarkSceneDirty(body.gameObject.scene); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
        public static void Replace(ShuimianShipModel body)
        {
            if (body.visualPrefab == null || !EditorUtility.IsPersistent(body.visualPrefab))
                throw new InvalidOperationException("请先指定 Project 中的外观 Prefab。");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(body.visualPrefab, body.transform);
            Undo.RegisterCreatedObjectUndo(visual, "替换船外观");
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            if (visual.GetComponentsInChildren<Rigidbody>(true).Any(b => !b.isKinematic)
                || visual.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b != null && b.enabled))
            {
                UnityEngine.Object.DestroyImmediate(visual);
                throw new InvalidOperationException("外观包含动态刚体或启用的脚本。请使用只含渲染/动画的外观，控制脚本留在 Ship 根节点。");
            }
            if (!ShuimianShipGeometry.TryLocalBounds(visual.transform, out var local))
            {
                UnityEngine.Object.DestroyImmediate(visual);
                throw new InvalidOperationException("外观没有启用的 MeshRenderer/SkinnedMeshRenderer。");
            }
            // Align its longest horizontal axis with the established control heading.
            var control = body.GetComponent<ShipController>();
            Vector3 heading = control != null ? body.transform.InverseTransformDirection(control.ForwardFlat) : Vector3.forward;
            float angle = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            float sourceAngle = local.size.x > local.size.z ? 90f : 0f;
            visual.transform.localRotation = Quaternion.Euler(0, angle - sourceAngle, 0);
            float desiredLength = Mathf.Max(body.hullBounds.size.x, body.hullBounds.size.z);
            float sourceLength = Mathf.Max(local.size.x, local.size.z);
            visual.transform.localScale = Vector3.one * (desiredLength / Mathf.Max(.001f, sourceLength));
            // Measure in root-local coordinates, independently of root's rotation/scale.
            Bounds fitted = TransformBounds(local, Matrix4x4.TRS(Vector3.zero, visual.transform.localRotation, visual.transform.localScale));
            var floating = body.GetComponent<ShuimianShipFloat>();
            float draft = floating != null ? floating.draftFraction : .13f;
            float waterline = body.hullBounds.min.y + body.hullBounds.size.y * draft;
            float modelLine = fitted.min.y + fitted.size.y * draft;
            visual.transform.localPosition = new Vector3(body.hullBounds.center.x - fitted.center.x,
                waterline - modelLine, body.hullBounds.center.z - fitted.center.z);
            Undo.RecordObject(body, "替换船外观");
            if (body.visualRoot != null) Undo.DestroyObjectImmediate(body.visualRoot.gameObject);
            body.visualRoot = visual.transform;
            EditorUtility.SetDirty(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
        static Bounds TransformBounds(Bounds input, Matrix4x4 matrix)
        {
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(input.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                result.Encapsulate(matrix.MultiplyPoint3x4(input.center + Vector3.Scale(input.extents, sign)));
            }
            return result;
        }
    }

    [InitializeOnLoad]
    public static class ShuimianShipModelSetup
    {
        const string ShipPath = "Assets/Art/Water_JiaoHu/Ship.prefab";
        const string ScenePath = "Assets/Art/Water_JiaoHu/Shuimian.unity";
        const string Request = "Temp/ShuimianShipModel.install";
        const string Result = "Temp/ShuimianShipModel.result.txt";
        static ShuimianShipModelSetup() { EditorApplication.delayCall += InstallRequested; }
        static void InstallRequested()
        {
            if (!File.Exists(Request)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += InstallRequested; return; }
            File.Delete(Request);
            try { Install(); }
            catch (Exception e) { File.WriteAllText(Result, "FAILED\n" + e); Debug.LogException(e); }
        }
        [MenuItem("Tools/Shuimian/Replace Ship Visual With Ship_UE")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("请在编辑模式打开 Shuimian 场景。");
            string backup = "ShipModelBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(ShipPath, backup + "/Ship.prefab", true);
            File.Copy(ScenePath, backup + "/Shuimian.unity", true);
            // Use the scene's authored model size (it overrides the original prefab scale).
            var sceneController = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ShipController>(true))
                .FirstOrDefault(c => c.name == "Ship");
            if (sceneController == null) throw new InvalidOperationException("场景中找不到 Ship。");
            if (!ShuimianShipGeometry.TryLocalBounds(sceneController.Target, out var sceneBounds))
                throw new InvalidOperationException("无法测量场景船体。");
            var root = PrefabUtility.LoadPrefabContents(ShipPath);
            Bounds baseline;
            try
            {
                var controller = root.GetComponent<ShipController>();
                var body = root.GetComponent<ShuimianShipModel>();
                if (body == null)
                {
                    if (!ShuimianShipGeometry.TryLocalBounds(root.transform, out baseline)) throw new InvalidOperationException("无法测量原船体。");
                    Vector3 oldHeading = root.transform.InverseTransformDirection(controller.ForwardFlat);
                    body = root.AddComponent<ShuimianShipModel>();
                    body.hullBounds = sceneBounds;
                    body.visualRoot = root.transform.GetChild(0);
                    controller.forwardSource = root.transform;
                    controller.forwardOffsetDeg = Mathf.Atan2(oldHeading.x, oldHeading.z) * Mathf.Rad2Deg;
                }
                body.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Mesh/Ship/Ship_UE.prefab");
                baseline = body.hullBounds;
                ShuimianShipModelEditor.Replace(body);
                Verify(body);
                var floating = root.GetComponent<ShuimianShipFloat>();
                floating.preserveInitialHeight = false;
                floating.heightOffset = 0;
                PrefabUtility.SaveAsPrefabAsset(root, ShipPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var controller in sceneRoot.GetComponentsInChildren<ShipController>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(controller) == null
                    || AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(controller)) != ShipPath) continue;
                var floating = controller.GetComponent<ShuimianShipFloat>();
                bool enabled = floating.enabled;
                floating.enabled = false; // Restore editor preview before changing its baseline.
                Undo.RecordObject(floating, "清除旧模型高度补偿");
                floating.preserveInitialHeight = false; floating.heightOffset = 0;
                PrefabUtility.RecordPrefabInstancePropertyModifications(floating);
                floating.enabled = enabled;
                foreach (var follow in UnityEngine.Object.FindObjectsOfType<CameraFollow>())
                {
                    if (follow.controller != controller) continue;
                    Undo.RecordObject(follow, "相机绑定固定船根节点");
                    follow.target = controller.Target;
                    EditorUtility.SetDirty(follow);
                }
                Verify(controller.GetComponent<ShuimianShipModel>());
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("场景保存失败。");
            string report = "PASS\nShip_UE visual installed. Fixed hull bounds: " + baseline
                + "\nGeometry invariant under visual scale, offset, rotation and disable: PASS"
                + "\nLegacy height offset cleared; camera targets stable Ship root.\nBackup: " + backup;
            Selection.activeGameObject = sceneController.gameObject;
            File.WriteAllText(Result, report);
            Debug.Log(report);
        }
        static void Verify(ShuimianShipModel body)
        {
            if (body == null || body.visualRoot == null) throw new InvalidOperationException("缺少固定船体或外观。");
            Transform root = body.transform, visual = body.visualRoot;
            var p = visual.localPosition; var r = visual.localRotation; var s = visual.localScale; bool active = visual.gameObject.activeSelf;
            var control = body.GetComponent<ShipController>();
            var direction = control.ForwardFlat;
            ShuimianShipGeometry.TryLocalBounds(root, out var before);
            ShuimianShipGeometry.TryFootprint(root, direction, out var footprint);
            try
            {
                visual.localScale = s * 3.7f; visual.localPosition += new Vector3(17, -8, 31);
                visual.localRotation = Quaternion.Euler(29, 72, 11); visual.gameObject.SetActive(false);
                if (!ShuimianShipGeometry.TryLocalBounds(root, out var after)
                    || (before.center - after.center).sqrMagnitude > .000001f
                    || (before.size - after.size).sqrMagnitude > .000001f
                    || Vector3.Angle(direction, control.ForwardFlat) > .001f
                    || !ShuimianShipGeometry.TryFootprint(root, direction, out var afterFootprint)
                    || Mathf.Abs(footprint.Waterline(.13f) - afterFootprint.Waterline(.13f)) > .0001f
                    || Mathf.Abs(footprint.length - afterFootprint.length) > .0001f)
                    throw new InvalidOperationException("外观独立性验证失败。");
            }
            finally { visual.localPosition = p; visual.localRotation = r; visual.localScale = s; visual.gameObject.SetActive(active); }
        }
    }
}
