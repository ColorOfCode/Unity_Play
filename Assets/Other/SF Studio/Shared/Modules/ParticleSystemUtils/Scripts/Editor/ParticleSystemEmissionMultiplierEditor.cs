using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SF_Studio.Shared.Modules.ParticleSystemUtils.Scripts.Editor {
    /// <summary>
    /// Helper script for changing emissions of custom vertex streams.
    /// Requires Custom Vertex Streams and a Vector1 custom data enabled in the particle system
    /// 
    /// Useful for the Unity Asset Optimized Projectiles VFX since it uses custom particle shaders and particle systems that have full emission control
    /// </summary>
    public class ParticleSystemEmissionMultiplierEditor : EditorWindow {
        private readonly List<GameObject> _prefabs = new();
        private float _emissionMultiplier = 1.0f;
        private Vector2 _scrollPosition;
        private SerializedProperty _prefabsProperty;

        [MenuItem("Tools/SF Studio/Particle System Utils/Emission Multiplier Editor")]
        public static void ShowWindow() {
            GetWindow<ParticleSystemEmissionMultiplierEditor>("Emission Multiplier");
        }

        private void OnGUI() {
            GUILayout.Label("Particle System Emission Multiplier", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Emission multiplier field
            _emissionMultiplier = EditorGUILayout.FloatField("Emission Multiplier", _emissionMultiplier);
            GUILayout.Space(10);

            // Drag and drop area
            GUILayout.Label("Drag and Drop Prefabs Here:", EditorStyles.boldLabel);
            var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop Prefabs Here");

            HandleDragAndDrop(dropArea, _prefabs);

            GUILayout.Space(10);

            // Display prefabs list
            GUILayout.Label($"Prefabs ({_prefabs.Count}):", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            for (var i = _prefabs.Count - 1; i >= 0; i--) {
                EditorGUILayout.BeginHorizontal();
                _prefabs[i] = (GameObject)EditorGUILayout.ObjectField(_prefabs[i], typeof(GameObject), false);

                if (GUILayout.Button("Remove", GUILayout.Width(70))) {
                    _prefabs.RemoveAt(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All")) {
                _prefabs.Clear();
            }

            if (GUILayout.Button("Apply Multiplier")) {
                ApplyMultiplier();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyMultiplier() {
            if (_prefabs.Count == 0) {
                EditorUtility.DisplayDialog("No Prefabs", "Please add at least one prefab to the list.", "OK");
                return;
            }

            var modifiedCount = 0;
            var totalParticleSystems = 0;

            foreach (var prefab in _prefabs) {
                if (prefab == null) {
                    continue;
                }

                // Get all particle systems in the prefab hierarchy
                var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                totalParticleSystems += particleSystems.Length;

                foreach (var ps in particleSystems) {
                    // Get the renderer component
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();

                    // Skip particle systems with disabled renderer
                    if (renderer != null && !renderer.enabled) {
                        continue;
                    }

                    // Get the custom data module
                    var customData = ps.customData;

                    // Check if custom data is enabled
                    if (customData.enabled) {
                        // Get the mode for Custom1 stream
                        var custom1Mode = customData.GetMode(ParticleSystemCustomData.Custom1);

                        // Check if it's set to Vector mode
                        if (custom1Mode == ParticleSystemCustomDataMode.Vector) {
                            // Get the X component (index 0)
                            var xCurve = customData.GetVector(ParticleSystemCustomData.Custom1, 0);
                            // Multiply the curve values
                            var modifiedCurve = MultiplyMinMaxCurve(xCurve, _emissionMultiplier);
                            // Set the modified curve back
                            customData.SetVector(ParticleSystemCustomData.Custom1, 0, modifiedCurve);
                            modifiedCount++;
                        }
                    }
                }

                // Mark the prefab as dirty to save changes
                EditorUtility.SetDirty(prefab);
            }

            // Save all modified prefabs
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete",
                $"Modified {modifiedCount} particle systems across {_prefabs.Count} prefab(s).\n" +
                $"Total particle systems found: {totalParticleSystems}",
                "OK"
            );
        }

        private ParticleSystem.MinMaxCurve MultiplyMinMaxCurve(ParticleSystem.MinMaxCurve curve, float multiplier) {
            var result = new ParticleSystem.MinMaxCurve {
                mode = curve.mode,
                curveMultiplier = curve.curveMultiplier * multiplier
            };

            switch (curve.mode) {
                case ParticleSystemCurveMode.Constant:
                    result.constant = curve.constant * multiplier;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    result.constantMin = curve.constantMin * multiplier;
                    result.constantMax = curve.constantMax * multiplier;
                    break;
                case ParticleSystemCurveMode.Curve:
                    result.curve = curve.curve;
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    result.curveMin = curve.curveMin;
                    result.curveMax = curve.curveMax;
                    break;
            }

            return result;
        }
        
        private static void HandleDragAndDrop( Rect dropArea,List<GameObject> objects)
        {
            var evt = Event.current;
        
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition)) {
                        return;
                    }
        
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
        
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            var go = draggedObject as GameObject;
                            if (go != null && !objects.Contains(go))
                            {
                                objects.Add(go);
                            }
                        }
                    }
                    evt.Use();
                    break;
            }
        }
    }
}