#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ObscuredByClouds.Gameplay;

namespace ObscuredByClouds.EditorTools
{
    public static class TerrainGrassInteractionSetup
    {
        [MenuItem("Tools/ObscuredByClouds/Setup Terrain Grass Interaction")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run scene setup outside Play mode.");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the target scene first.");
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var existing = transforms.Select(t => t.GetComponent<TerrainGrassInteraction>()).FirstOrDefault(c => c);
            var actor = existing && existing.character ? existing.character : transforms.FirstOrDefault(t => t.name == "SK_Mannequin");
            if (!actor) throw new InvalidOperationException("Assign a character to Terrain Grass Interaction, or add SK_Mannequin to the scene.");
            int prepared = 0;
            foreach (var terrain in transforms.Select(t => t.GetComponent<Terrain>()).Where(t => t))
                prepared += TerrainGrassTexturePadding.PrepareTerrain(terrain);
            if (!existing)
            {
                var manager = new GameObject("Terrain Grass Interaction");
                SceneManager.MoveGameObjectToScene(manager, scene);
                Undo.RegisterCreatedObjectUndo(manager, "Add character grass interaction");
                existing = Undo.AddComponent<TerrainGrassInteraction>(manager);
            }
            Undo.RecordObject(existing, "Bind grass interaction character");
            existing.character = actor;
            EditorUtility.SetDirty(existing);
            EditorSceneManager.MarkSceneDirty(scene);
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Save the scene to retain the interaction component.");
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Grass interaction follows " + actor.name + "; prepared " + prepared + " detail prototypes. All compatible plants share this character field.", existing);
        }
    }
}
#endif
