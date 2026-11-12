#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Shuimian.Gameplay;

namespace ObscuredByClouds.EditorTools
{
    public static class IndirectLightingMaterialRefresh
    {
        static bool queued;
        public static void Refresh(Material material)
        {
            // The generated ocean material is recopied from Source Material.
            // Persist this control there when edited through a generated renderer.
            if (material && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)) && material.HasProperty("_IndirectLightStrength"))
                foreach (var water in UnityEngine.Object.FindObjectsOfType<ShuimianWaterMesh>())
                    if (water.sourceMaterial && water.SurfaceRenderer && water.SurfaceRenderer.sharedMaterials.Contains(material))
                    {
                        Undo.RecordObject(water.sourceMaterial, "Change water indirect lighting");
                        water.sourceMaterial.SetFloat("_IndirectLightStrength", material.GetFloat("_IndirectLightStrength"));
                        EditorUtility.SetDirty(water.sourceMaterial);
                    }
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () =>
            {
                queued = false;
                if (!Application.isPlaying)
                    foreach (var terrain in Terrain.activeTerrains)
                        if (terrain.terrainData) { terrain.terrainData.RefreshPrototypes(); terrain.Flush(); }
                SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
            };
        }
    }
    public class IndirectLightingShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck(); base.OnGUI(materialEditor, properties);
            if (EditorGUI.EndChangeCheck()) foreach (var target in materialEditor.targets) IndirectLightingMaterialRefresh.Refresh(target as Material);
        }
    }
    public abstract class IndirectNativeShaderGUI : ShaderGUI
    {
        protected abstract string NativeType { get; }
        ShaderGUI native;
        protected ShaderGUI Native => native ?? (native = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(NativeType)).Where(t => t != null).Select(t => Activator.CreateInstance(t, true) as ShaderGUI).FirstOrDefault());
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            if (Native != null)
            {
                Native.OnGUI(editor, properties);
                var indirect = properties.FirstOrDefault(p => p.name == "_IndirectLightStrength");
                if (indirect != null) editor.ShaderProperty(indirect, "Indirect Light Strength");
            }
            else base.OnGUI(editor, properties);
            if (EditorGUI.EndChangeCheck()) foreach (var target in editor.targets) IndirectLightingMaterialRefresh.Refresh(target as Material);
        }
        public override void ValidateMaterial(Material material) { if (Native != null) Native.ValidateMaterial(material); }
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        { if (Native != null) Native.AssignNewShaderToMaterial(material, oldShader, newShader); else base.AssignNewShaderToMaterial(material, oldShader, newShader); }
    }
    public sealed class IndirectLitShaderGUI : IndirectNativeShaderGUI
    {
        protected override string NativeType => "UnityEditor.Rendering.Universal.ShaderGUI.LitShader";
    }
    public sealed class IndirectTerrainShaderGUI : IndirectNativeShaderGUI, ITerrainLayerCustomUI
    {
        protected override string NativeType => "UnityEditor.Rendering.Universal.TerrainLitShaderGUI";
        bool ITerrainLayerCustomUI.OnTerrainLayerGUI(TerrainLayer layer, Terrain terrain)
        { return Native is ITerrainLayerCustomUI gui && gui.OnTerrainLayerGUI(layer, terrain); }
    }
}
#endif
