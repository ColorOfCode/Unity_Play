using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ObscuredByClouds.Gameplay;

namespace Snowfield.EditorTools
{
    public static class SnowTrackSetup
    {
        public const string Root="Assets/Art/Ice/SnowTracks";
        [MenuItem("Tools/Snowfield/配置当前雪地场景")]
        public static void Setup()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="Snowfield_Player" || EditorApplication.isPlaying){Debug.LogError("Open Snowfield_Player in edit mode before snow setup.");return;}
            var player=Object.FindObjectOfType<ClickToMoveCharacter>();
            if(!player){Debug.LogError("No click-to-move character found.");return;}
            var manager=Object.FindObjectOfType<SnowTrackSystem>();
            if(!manager)manager=new GameObject("Snow Tracks — 雪痕管理").AddComponent<SnowTrackSystem>();
            manager.followTarget=player.transform;
            manager.stampShader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/Shaders/SnowStamp.shader");
            var emitter=player.GetComponent<SnowFootEmitter>();if(!emitter)emitter=player.gameObject.AddComponent<SnowFootEmitter>();
            emitter.system=manager;
            foreach(var t in player.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="foot_l")emitter.leftFoot=t;if(t.name=="foot_r")emitter.rightFoot=t;
                if(t.name=="ball_l")emitter.leftToe=t;if(t.name=="ball_r")emitter.rightToe=t;
            }
            if(!AssetDatabase.IsValidFolder(Root+"/Materials"))AssetDatabase.CreateFolder(Root,"Materials");
            var cache=new Dictionary<Material,Material>();int renderers=0;
            foreach(var renderer in Object.FindObjectsOfType<Renderer>())
            {
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var m=materials[i];if(!m || !m.shader)continue;
                    string path=AssetDatabase.GetAssetPath(m);
                    if(!path.Contains("PureNature_Glacier/Models/SnowGround/") && !path.Contains("PureNature_Glacier/Models/BoulderSnow/"))continue;
                    if(!cache.TryGetValue(m,out var copy))
                    {
                        string assetPath=Root+"/Materials/"+m.name+"_Tracks.mat";
                        copy=AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        if(!copy){copy=new Material(m);AssetDatabase.CreateAsset(copy,assetPath);}
                        copy.shader=Shader.Find("Snowfield/Layered Snow");
                        copy.SetFloat("_TessValue",64);copy.SetFloat("_TessMin",1);copy.SetFloat("_TessMax",14);
                        EditorUtility.SetDirty(copy);cache[m]=copy;
                    }
                    materials[i]=copy;changed=true;
                }
                if(changed){renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);renderers++;}
            }
            foreach(var terrain in Terrain.activeTerrains)
            {
                string path=Root+"/Materials/Terrain_Tracks.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(terrain.materialTemplate);AssetDatabase.CreateAsset(material,path);}
                material.shader=Shader.Find("Snowfield/Terrain Lit");EditorUtility.SetDirty(material);
                terrain.materialTemplate=material;terrain.basemapDistance=Mathf.Max(terrain.basemapDistance,150);
                EditorUtility.SetDirty(terrain);PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            }
            EditorUtility.SetDirty(manager);EditorUtility.SetDirty(emitter);PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("SNOW_SETUP_COMPLETE: "+renderers+" renderers, "+cache.Count+" mesh materials. Feet: "+emitter.leftFoot+" / "+emitter.rightFoot);
        }
    }
}

