#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObscuredByClouds.EditorTools
{
    /// <summary>
    /// Releases model inspectors before reload and removes editor instances whose
    /// importer targets no longer exist. Clearing Selection alone leaves orphaned
    /// and locked inspectors alive across domain reloads in Unity 2021.3.
    /// </summary>
    [InitializeOnLoad]
    internal static class ModelImporterPanelErrorGuard
    {
        const string LogTag = "[ErrorGuard]";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type ModelEditorType = typeof(Editor).Assembly.GetType("UnityEditor.ModelImporterEditor");
        static readonly Type InspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        static readonly PropertyInfo TrackerProperty = InspectorType?.GetProperty("tracker", InstanceFlags);
        static readonly PropertyInfo LockedProperty = InspectorType?.GetProperty("isLocked", InstanceFlags);
        static readonly MethodInfo SetLockedObjects = typeof(ActiveEditorTracker).GetMethod("SetObjectsLockedByThisTracker", InstanceFlags);
        static bool preparing;

        static ModelImporterPanelErrorGuard()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            AssemblyReloadEvents.beforeAssemblyReload += PrepareForReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RemoveInvalidEditors;
        }

        static void OnCompilationStarted(object context) { PrepareForReload(); }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                PrepareForReload();
        }

        static bool IsModelAsset(Object obj)
        {
            if (obj == null) return false;
            if (obj is ModelImporter) return true;
            string path = AssetDatabase.GetAssetPath(obj);
            return !string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter;
        }

        static bool IsModelEditor(Editor editor)
        {
            return editor != null && ModelEditorType != null && ModelEditorType.IsInstanceOfType(editor);
        }

        static bool HasInvalidTargets(Editor editor)
        {
            Object[] targets = editor.targets;
            if (targets.Length == 0) return true;
            foreach (Object target in targets)
                if (!(target is ModelImporter) || target == null) return true;
            return false;
        }

        static void PrepareForReload()
        {
            if (preparing || ModelEditorType == null) return;
            preparing = true;
            try
            {
                // Capture affected trackers before changing Selection; locked
                // inspectors keep their own target list independently of Selection.
                var affected = new Dictionary<ActiveEditorTracker, Object>();
                if (InspectorType != null && TrackerProperty != null)
                {
                    foreach (Object window in Resources.FindObjectsOfTypeAll(InspectorType))
                    {
                        var tracker = TrackerProperty.GetValue(window, null) as ActiveEditorTracker;
                        if (tracker == null) continue;
                        foreach (Editor editor in tracker.activeEditors)
                        {
                            if (!IsModelEditor(editor)) continue;
                            affected[tracker] = window;
                            break;
                        }
                    }
                }

                var keepSelection = new List<Object>();
                bool selectionChanged = false;
                foreach (Object obj in Selection.objects)
                {
                    if (IsModelAsset(obj)) selectionChanged = true;
                    else keepSelection.Add(obj);
                }
                if (selectionChanged) Selection.objects = keepSelection.ToArray();

                foreach (var pair in affected)
                {
                    if (LockedProperty != null && (bool)LockedProperty.GetValue(pair.Value, null))
                    {
                        SetLockedObjects?.Invoke(pair.Key, new object[] { new List<Object>() });
                        LockedProperty.SetValue(pair.Value, false, null);
                    }
                    // Repaint only schedules UI work; ForceRebuild releases the
                    // editors synchronously while importer targets are still valid.
                    pair.Key.ForceRebuild();
                }
                if (selectionChanged) ActiveEditorTracker.sharedTracker.ForceRebuild();
                RemoveInvalidEditors();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogTag + " 无法释放模型 Inspector：" + ex);
            }
            finally { preparing = false; }
        }

        static void RemoveInvalidEditors()
        {
            if (ModelEditorType == null) return;
            int removed = 0;
            // Orphaned editors are absent from activeEditors, so inspect all loaded
            // editor instances. Never destroy the importer or its model asset.
            foreach (Object obj in Resources.FindObjectsOfTypeAll(ModelEditorType))
            {
                var editor = obj as Editor;
                if (!IsModelEditor(editor) || !HasInvalidTargets(editor)) continue;
                Object.DestroyImmediate(editor);
                removed++;
            }
            if (removed > 0)
                Debug.Log(LogTag + " 已清理 " + removed + " 个目标失效的模型 Inspector。");
        }

        [MenuItem("Tools/ObscuredByClouds/修复模型 Inspector 报错", false, 19)]
        public static void RepairModelInspectors() { PrepareForReload(); }

        [MenuItem("Tools/ObscuredByClouds/清空 Console", false, 20)]
        public static void ClearConsole()
        {
            Type logEntries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo clear = logEntries?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            clear?.Invoke(null, null);
        }
    }
}
#endif
