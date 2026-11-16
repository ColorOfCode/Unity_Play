using System.IO;
using UnityEditor;
using UnityEngine;
using ObscuredByClouds.Gameplay;

public static class OcclusionFadeConfigurationMenu
{
    [MenuItem("Tools/ObscuredByClouds/遮挡渐变/配置选中的 Prefab 或场景模型")]
    static void ConfigureSelected()
    {
        foreach(var selected in Selection.gameObjects)
        {
            string path=AssetDatabase.GetAssetPath(selected);
            if(EditorUtility.IsPersistent(selected))
            {
                if(!path.EndsWith(".prefab")){Debug.LogWarning("FBX 原模型不能保存自定义组件，请使用‘从选中的 FBX 生成可配置 Prefab’。",selected);continue;}
                var contents=PrefabUtility.LoadPrefabContents(path);
                try{var c=contents.GetComponent<OcclusionFadeTarget>();if(!c)c=contents.AddComponent<OcclusionFadeTarget>();c.RefreshMaterialBindings();PrefabUtility.SaveAsPrefabAsset(contents,path);}
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            else
            {
                var c=selected.GetComponent<OcclusionFadeTarget>();if(!c)c=Undo.AddComponent<OcclusionFadeTarget>(selected);
                Undo.RecordObject(c,"Configure occlusion fade");c.RefreshMaterialBindings();EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
        }
    }
    [MenuItem("Tools/ObscuredByClouds/遮挡渐变/从选中的 FBX 生成可配置 Prefab")]
    static void CreateConfiguredPrefab()
    {
        var model=Selection.activeObject as GameObject;
        if(!model||!EditorUtility.IsPersistent(model)||PrefabUtility.GetPrefabAssetType(model)!=PrefabAssetType.Model)
        {Debug.LogWarning("请先在 Project 中选中一个 FBX/模型资产。");return;}
        string path=EditorUtility.SaveFilePanelInProject("保存遮挡渐变 Prefab",model.name+"_Occlusion","prefab","生成带配置组件的 Prefab Variant，FBX 源模型保持原样。");if(string.IsNullOrEmpty(path))return;
        var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var instance=PrefabUtility.InstantiatePrefab(model,preview) as GameObject;
        try
        {
            var c=instance.GetComponent<OcclusionFadeTarget>();if(!c)c=instance.AddComponent<OcclusionFadeTarget>();c.RefreshMaterialBindings();
            var prefab=PrefabUtility.SaveAsPrefabAsset(instance,path);Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);
        }
        finally{if(instance)Object.DestroyImmediate(instance);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
    }
}
