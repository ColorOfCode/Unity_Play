using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shuimian.VFX.EditorTools
{
    /// <summary>
    /// 编辑器菜单：向当前打开的场景实例化“自适应无限平面”
    /// 菜单路径：Tools / 自适应无限平面 / 在场景中创建
    /// </summary>
    public static class AdaptiveGridPlaneCreator
    {
        [MenuItem("Tools/自适应无限平面/在场景中创建")]
        public static void CreateInScene()
        {
            // 1. 先建物体并挂主脚本（RequireComponent 会自动带出 MeshFilter + MeshRenderer）
            var go = new GameObject("AdaptiveInfinitePlane");
            Undo.RegisterCreatedObjectUndo(go, "创建自适应无限平面");
            var comp = go.AddComponent<Shuimian.VFX.AdaptiveGridPlane>();

            // 2. 现在渲染器已存在，再赋材质（尝试 URP Lit，避免粉红）
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var urpLit = AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat");
                if (urpLit != null) mr.sharedMaterial = urpLit;
            }

            // 3. 首次构建网格、选中并标记场景脏
            comp.Rebuild();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[AdaptiveGridPlane] 已在场景中创建 AdaptiveInfinitePlane，调整 Inspector 参数即可实时改密度，保存场景以保留。", go);
        }
    }
}
