using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SF_Studio.Shared.Modules.ParticleSystemUtils.Scripts.Editor {
    /// <summary>
    /// This script is used for bulk changing of sorting order for particle systems
    /// Simply go to Tools/ SF Studio/ Particle System/ Sorting Order Editor
    /// Drag and drop all of your relevant prefab particle systems
    /// Select a material which you want to filter for
    /// Specify a sorting order that you want to assign particles that match the selected material.
    ///
    /// The script will automatically change the sorting order, ensuring that all particle systems and child particle systems that are using the selected material have the same sorting order.
    /// This is useful for managing render order of particle systems using specific materials
    /// </summary>
    public class ParticleSystemSortingOrderEditor : EditorWindow {
        private readonly List<GameObject> _prefabs = new();
        private Material _targetMaterial;
        private int _sortingOrder;
        private Vector2 _scrollPos;
        private bool _processMeshRenderMode;
        private Mesh _targetMesh;

        [MenuItem("Tools/SF Studio/Particle System Utils/Sorting Order Editor")]
        public static void ShowWindow() {
            GetWindow<ParticleSystemSortingOrderEditor>("PS Sorting Order Editor");
        }

        private void OnGUI() {
            GUILayout.Label("Particle System Sorting Order Editor", EditorStyles.boldLabel);
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
                    ClearAll();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Target material
            _targetMaterial = (Material)EditorGUILayout.ObjectField("Target Material", _targetMaterial, typeof(Material), false);

            EditorGUILayout.Space();

            // Render Mode options
            EditorGUILayout.LabelField("Render Mode Filter", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Process Only Mesh Renderer",GUILayout.Width(170));
            _processMeshRenderMode = EditorGUILayout.Toggle(_processMeshRenderMode);
            EditorGUILayout.EndHorizontal();
            
            if (_processMeshRenderMode) {
                EditorGUI.indentLevel++;
                _targetMesh = (Mesh)EditorGUILayout.ObjectField("Target Mesh (Optional)", _targetMesh, typeof(Mesh), false);
                EditorGUILayout.HelpBox("If no mesh is specified, all Mesh render mode particles will be processed.", MessageType.Info);
                EditorGUI.indentLevel--;
            } else {
                EditorGUILayout.HelpBox("Only non-Mesh render mode particles will be processed.", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Sorting Order
            _sortingOrder = EditorGUILayout.IntField("Sorting Order", _sortingOrder);

            EditorGUILayout.Space();

            // Apply button
            GUI.enabled = _targetMaterial != null && _prefabs.Count > 0;
            if (GUILayout.Button("Apply Sorting Order", GUILayout.Height(30))) {
                UpdateSortingOrder();
            }

            GUI.enabled = true;
        }

        private void UpdateSortingOrder() {
            var updatedCount = 0;

            foreach (var prefab in _prefabs) {
                if (prefab == null) {
                    continue;
                }

                // Get all particle systems in the prefab (including nested ones)
                var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);

                foreach (var ps in particleSystems) {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();

                    if (renderer != null && renderer.sharedMaterial == _targetMaterial) {
                        // Check render mode filter
                        if (!ShouldProcessParticleSystem(renderer)) {
                            continue;
                        }

                        Undo.RecordObject(renderer, "Update Particle System Sorting Order");
                        renderer.sortingOrder = _sortingOrder;
                        EditorUtility.SetDirty(renderer);
                        updatedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Updated {updatedCount} particle system renderer(s) with sorting order {_sortingOrder}");
        }

        private bool ShouldProcessParticleSystem(ParticleSystemRenderer renderer) {
            var isMeshRenderMode = renderer.renderMode == ParticleSystemRenderMode.Mesh;

            if (_processMeshRenderMode) {
                // Process mesh render mode particles
                if (!isMeshRenderMode) {
                    return false;
                }

                // If a target mesh is specified, check if it matches
                if (_targetMesh != null && renderer.mesh != _targetMesh) {
                    return false;
                }

                return true;
            } else {
                // Skip mesh render mode particles
                return !isMeshRenderMode;
            }
        }

        private void ClearAll() {
            _prefabs.Clear();
            _targetMaterial = null;
            _sortingOrder = 0;
            _processMeshRenderMode = false;
            _targetMesh = null;
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