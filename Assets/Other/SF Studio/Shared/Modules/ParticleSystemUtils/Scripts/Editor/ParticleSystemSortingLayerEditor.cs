using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SF_Studio.Shared.Modules.ParticleSystemUtils.Scripts.Editor {
    /// <summary>
    /// This script is used for bulk changing of sorting layer Ids
    /// Simply go to Tools/ SF Studio/ Particle System/ Sorting Layer Editor
    /// Drag and drop all of your relevant prefab particle systems
    /// Select a mesh which you want to filter for
    /// Select a sorting layer that you want to assign particles that match the selected mesh.
    ///
    /// The script will automatically change the sorting layer, ensuring that all particle systems and child particle systems that are using the selected mesh are in the same layer.
    /// This is useful to reduce batch draw calls for particle systems that are using a specific mesh
    /// </summary>
    public class ParticleSystemSortingLayerEditor : EditorWindow {
        private readonly List<GameObject> _prefabs = new();
        private Mesh _targetMesh;
        private int _sortingLayerID;
        private Vector2 _scrollPos;
        private SerializedProperty _prefabsProperty;

        [MenuItem("Tools/SF Studio/Particle System Utils/Sorting Layer Editor")]
        public static void ShowWindow() {
            GetWindow<ParticleSystemSortingLayerEditor>("PS Sorting Layer Editor");
        }

        private void OnGUI() {
            GUILayout.Label("Particle System Sorting Layer Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefabs (Drag & Drop)", EditorStyles.boldLabel);

            var dropArea = GUILayoutUtility.GetRect(0f, 100f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag prefabs here or add manually below");

            HandleDragAndDrop(dropArea);

            EditorGUILayout.Space();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));

            for (var i = 0; i < _prefabs.Count; i++) {
                EditorGUILayout.BeginHorizontal();
                _prefabs[i] = (GameObject)EditorGUILayout.ObjectField(_prefabs[i], typeof(GameObject), false);
                if (GUILayout.Button("X", GUILayout.Width(25))) {
                    _prefabs.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Prefab Slot")) {
                _prefabs.Add(null);
            }

            if (GUILayout.Button("Clear All") && _prefabs.Count > 0) {
                if (EditorUtility.DisplayDialog("Clear All", "Remove all prefabs from the list?", "Yes", "No")) {
                    _prefabs.Clear();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Target mesh
            _targetMesh = (Mesh)EditorGUILayout.ObjectField("Target Mesh", _targetMesh, typeof(Mesh), false);

            EditorGUILayout.Space();

            // Sorting Layer ID
            var sortingLayerNames = GetSortingLayerNames();
            var currentIndex = GetSortingLayerIndex(_sortingLayerID);
            var newIndex = EditorGUILayout.Popup("Sorting Layer", currentIndex, sortingLayerNames);

            if (newIndex >= 0 && newIndex < sortingLayerNames.Length) {
                _sortingLayerID = SortingLayer.NameToID(sortingLayerNames[newIndex]);
            }

            EditorGUILayout.Space();

            // Apply button
            GUI.enabled = _targetMesh != null && _prefabs.Count > 0;
            if (GUILayout.Button("Update Sorting Layers", GUILayout.Height(30))) {
                UpdateSortingLayers();
            }

            GUI.enabled = true;
        }

        private void UpdateSortingLayers() {
            var updatedCount = 0;

            foreach (var prefab in _prefabs) {
                if (prefab == null) {
                    continue;
                }

                // Get all particle systems in the prefab (including nested ones)
                var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);

                foreach (var ps in particleSystems) {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();

                    if (renderer != null && renderer.mesh == _targetMesh) {
                        Undo.RecordObject(renderer, "Update Particle System Sorting Layer");
                        renderer.sortingLayerID = _sortingLayerID;
                        EditorUtility.SetDirty(renderer);
                        updatedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Updated {updatedCount} particle system renderer(s) with sorting layer ID {_sortingLayerID}");
        }

        private string[] GetSortingLayerNames() {
            var internalEditorUtilityType = typeof(UnityEditorInternal.InternalEditorUtility);
            var sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (sortingLayersProperty != null) {
                return (string[])sortingLayersProperty.GetValue(null, Array.Empty<object>());
            }

            return new string[] { };
        }

        private int GetSortingLayerIndex(int layerID) {
            var layerNames = GetSortingLayerNames();
            var layerName = SortingLayer.IDToName(layerID);

            for (var i = 0; i < layerNames.Length; i++) {
                if (layerNames[i] == layerName) {
                    return i;
                }
            }

            return 0; // Default layer
        }

        private void HandleDragAndDrop(Rect dropArea) {
            var evt = Event.current;

            switch (evt.type) {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition)) {
                        return;
                    }

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform) {
                        DragAndDrop.AcceptDrag();

                        foreach (var draggedObject in DragAndDrop.objectReferences) {
                            var go = draggedObject as GameObject;
                            if (go != null && !_prefabs.Contains(go)) {
                                _prefabs.Add(go);
                            }
                        }
                    }

                    evt.Use();
                    break;
            }
        }
    }
}