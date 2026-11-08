using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Shuimian.Gameplay.Editor
{
    /// <summary>
    /// Adds the independent ship interaction to the already-open authored scene.
    /// Never opens/reloads a scene: saving preserves the editor's current objects,
    /// including existing user edits. The previous disk scene is backed up first.
    /// </summary>
    public static class ShuimianShipWakeSetup
    {
        const string ScenePath = "Assets/Art/Water_JiaoHu/Shuimian.unity";
        const string ComputePath = "Assets/Art/Water_JiaoHu/ShuimianShipWake.compute";
        const string MenuPath = "Tools/Shuimian/配置当前场景船与水面交互";

        [MenuItem(MenuPath)]
        public static void ConfigureCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var scene = SceneManager.GetActiveScene();
            if (!IsTargetScene(scene))
            {
                Debug.LogWarning("船与水面交互配置仅作用于当前已打开的 Shuimian 场景；请先打开该场景，再使用此菜单。");
                return;
            }
            var water = FindWater(scene);
            var controller = water != null ? FindController(scene, water) : null;
            if (water == null || controller == null)
            {
                Debug.LogWarning("Shuimian 场景中需要一个独立水面和 ShipController，未更改场景。");
                return;
            }
            Configure(scene, water, controller);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating && IsTargetScene(SceneManager.GetActiveScene());
        }

        static bool IsTargetScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase);
        }

        static ShuimianWaterMesh FindWater(Scene scene)
        {
            ShuimianWaterMesh first = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<ShuimianWaterMesh>(true))
            {
                if (candidate.gameObject.scene != scene || EditorUtility.IsPersistent(candidate)) continue;
                if (candidate.name == "Shuimian Water") return candidate;
                if (first == null) first = candidate;
            }
            return first;
        }

        static ShipController FindController(Scene scene, ShuimianWaterMesh water)
        {
            ShipController first = null, floatingCandidate = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<ShipController>(true))
            {
                if (candidate.gameObject.scene != scene || EditorUtility.IsPersistent(candidate)) continue;
                var floating = candidate.GetComponent<ShuimianShipFloat>();
                if (floating != null && floating.water == water) return candidate;
                if (floating != null && floatingCandidate == null) floatingCandidate = candidate;
                if (first == null || candidate.name == "Ship") first = candidate;
            }
            return floatingCandidate != null ? floatingCandidate : first;
        }

        static void Configure(Scene scene, ShuimianWaterMesh water, ShipController controller)
        {
            var wake = controller.GetComponent<ShuimianShipWake>();
            bool newEmitter = wake == null;
            bool firstWaterSetup = newEmitter && water.shipWakeShader == null;
            var compute = water.shipWakeShader != null ? water.shipWakeShader : AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            if (compute == null)
            {
                Debug.LogWarning("Shuimian 船行交互尚未配置：缺少 " + ComputePath + "。资源导入完成后可再次使用配置菜单。", water);
                return;
            }
            bool waterChanged = water.shipWakeShader == null || firstWaterSetup;
            bool emitterChanged = newEmitter || wake.water == null || wake.controller == null || wake.motionTarget == null;
            if (!waterChanged && !emitterChanged)
            {
                LogComplete(water, controller, false);
                return;
            }

            // Perform the disk backup before adding components or touching any
            // authored property. No scene reload or replacement is involved.
            string backupPath;
            try { backupPath = BackupDiskScene(scene); }
            catch (Exception exception)
            {
                Debug.LogError("Shuimian 船行交互配置中止：无法先备份原场景。" + exception.Message, water);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("配置船与水面交互");
            try
            {
                if (waterChanged)
                {
                    Undo.RecordObject(water, "配置水面交互资源");
                    if (water.shipWakeShader == null) water.shipWakeShader = compute;
                    if (firstWaterSetup)
                    {
                        water.enableShipInteraction = true;
                        water.shipWakeResolution = 512;
                        water.shipWakeAreaSize = 192f;
                    }
                    RecordAuthoredChange(water);
                }
                if (newEmitter) wake = Undo.AddComponent<ShuimianShipWake>(controller.gameObject);
                if (emitterChanged)
                {
                    Undo.RecordObject(wake, "配置船体尾迹");
                    if (newEmitter || wake.water == null) wake.water = water;
                    if (newEmitter || wake.controller == null) wake.controller = controller;
                    if (newEmitter || wake.motionTarget == null) wake.motionTarget = controller.Target;
                    if (newEmitter)
                    {
                        wake.hullWidth = 4f;
                        wake.hullLength = 13.5f;
                    }
                    RecordAuthoredChange(wake);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity 未能保存当前内存中的场景。 ");
                Undo.CollapseUndoOperations(undoGroup);
                LogComplete(water, controller, true);
                Debug.Log("Shuimian Ship Wake previous disk-scene backup: " + backupPath, water);
            }
            catch (Exception exception)
            {
                // Undo only this configuration group, preserving pre-existing
                // editor edits. Never replace the scene with the disk backup.
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("Shuimian 船行交互配置未完成，已撤销本次配置。" + exception.Message, water);
            }
        }

        static void RecordAuthoredChange(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }

        static string BackupDiskScene(Scene scene)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string source = Path.GetFullPath(Path.Combine(projectRoot, scene.path));
            string directory = Path.GetFullPath(Path.Combine(projectRoot, "Library", "ShuimianShipWakeBackup"));
            if (!source.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)
                || !directory.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("备份路径不在当前 Unity 项目中。");
            if (!File.Exists(source)) throw new FileNotFoundException("未找到磁盘上的 Shuimian 场景。", source);
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, "Shuimian.before-setup.unity");
            if (File.Exists(destination))
                destination = Path.Combine(directory, "Shuimian.before-setup." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                    + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".unity");
            File.Copy(source, destination, false);
            return destination;
        }

        static void LogComplete(ShuimianWaterMesh water, ShipController controller, bool saved)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in controller.Target.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Debug.Log("Shuimian Ship Wake setup complete. Water=" + water.name + "; Ship=" + controller.name
                + "; Saved=" + saved + "; RendererBounds=" + (found ? bounds.center.ToString("F3") + " size " + bounds.size.ToString("F3") : "unavailable"), water);
        }
    }
}
