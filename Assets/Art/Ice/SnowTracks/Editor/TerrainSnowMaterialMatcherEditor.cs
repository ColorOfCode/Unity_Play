using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Snowfield.EditorTools
{
    [CustomEditor(typeof(TerrainSnowMaterialMatcher)), CanEditMultipleObjects]
    public sealed class TerrainSnowMaterialMatcherEditor : Editor
    {
        public override bool RequiresConstantRepaint() => true;
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("仅处理支持的雪模型 Shader，并且脚下雪层覆盖达到阈值的模型。普通石头和草地上的模型保留原显示参数。编辑模式自动刷新。", MessageType.Info);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties())
                foreach (TerrainSnowMaterialMatcher matcher in targets) matcher.Invalidate();
            var component = (TerrainSnowMaterialMatcher)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("匹配状态", component.Status);
            EditorGUILayout.LabelField("符合条件的材质槽", component.CompatibleMaterialCount.ToString());
            EditorGUILayout.LabelField("脚下雪层覆盖", component.SnowCoverage.ToString("P1"));
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("当前地形", component.CurrentTerrain, typeof(Terrain), true);
                EditorGUILayout.ObjectField("当前雪层", component.MatchedLayer, typeof(TerrainLayer), false);
                if (component.MatchedLayer)
                {
                    var layer = component.MatchedLayer;
                    EditorGUILayout.ObjectField("生效颜色贴图", layer.diffuseTexture, typeof(Texture2D), false);
                    EditorGUILayout.ObjectField("生效法线贴图", layer.normalMapTexture, typeof(Texture2D), false);
                    EditorGUILayout.ObjectField("生效遮罩贴图", layer.maskMapTexture, typeof(Texture2D), false);
                    EditorGUILayout.Vector2Field("地形铺设尺寸（米）", layer.tileSize);
                }
            }
            EditorGUILayout.HelpBox("自动匹配作用于该模型的显示参数；共享材质中的 Layer Maps 保留原设置。停用或移除组件即可恢复。", MessageType.None);
            if (GUILayout.Button("立即刷新匹配")) foreach (TerrainSnowMaterialMatcher matcher in targets) matcher.RefreshNow();
            if (component.MatchedLayer && GUILayout.Button("定位当前 TerrainLayer")) EditorGUIUtility.PingObject(component.MatchedLayer);
        }
        [MenuItem("Tools/Snowfield/给所选模型添加地形雪材质匹配", false, 30)]
        static void AddToSelected()
        {
            int added = 0;
            foreach (var root in Selection.gameObjects)
            {
                if (!root.scene.IsValid() || !root.scene.isLoaded) continue;
                bool eligible = root.GetComponentsInChildren<Renderer>(true)
                    .Any(r => r.sharedMaterials.Any(TerrainSnowMaterialMatcher.Supports));
                if (!eligible) continue;
                var component = root.GetComponent<TerrainSnowMaterialMatcher>();
                if (!component) { component = Undo.AddComponent<TerrainSnowMaterialMatcher>(root); ++added; }
                component.RefreshNow();
            }
            Debug.Log("地形雪材质匹配：新增 " + added + " 个组件；不支持的 Shader 已跳过。");
        }
    }
}
