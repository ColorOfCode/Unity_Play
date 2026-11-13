using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ObscuredByClouds.Gameplay;
namespace Snowfield.EditorTools
{
    [InitializeOnLoad]
    public static class SnowDepthTerrainTools
    {
        const string Root=SnowTrackSetup.Root+"/Surfaces";
        static SnowDepthSurface pending;
        static double refreshAt;
        static bool baking;
        static SnowDepthTerrainTools(){TerrainCallbacks.textureChanged+=Changed;EditorApplication.update+=Update;}
        static void Changed(Terrain terrain,string name,RectInt region,bool synced)
        {
            if(baking || EditorApplication.isPlaying || !terrain || name!="alphamap")return;
            var surface=terrain.GetComponent<SnowDepthSurface>();if(!surface)return;
            pending=surface;refreshAt=EditorApplication.timeSinceStartup+2;
        }
        static void Update()
        {
            if(pending && EditorApplication.timeSinceStartup>refreshAt && !EditorApplication.isUpdating && !EditorApplication.isPlaying)
            {var s=pending;pending=null;Bake(s);}
        }
        [MenuItem("Tools/Snowfield/绘制角色周围草地与厚雪过渡")]
        public static void Configure()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying || scene.name!="Snowfield_Player")throw new Exception("Open Snowfield_Player in edit mode.");
            var player=UnityEngine.Object.FindObjectOfType<ClickToMoveCharacter>();
            var terrain=Terrain.activeTerrain;if(!terrain || !player)throw new Exception("Missing terrain or player.");
            // Preserve the user's unsaved scene edits before making the new changes.
            EditorSceneManager.SaveScene(scene);
            var backup=@"C:\Users\ZY\Documents\Codex\2026-11-12\new-chat\outputs\SnowTracks_厚雪改造备份\Snowfield_Player_修改前含当前编辑.unity";
            if(!File.Exists(backup))File.Copy(scene.path,backup);
            if(!AssetDatabase.IsValidFolder(Root))AssetDatabase.CreateFolder(SnowTrackSetup.Root,"Surfaces");
            string terrainPath=Root+"/Snowfield_GrassTerrain.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            if(!data){AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(terrain.terrainData),terrainPath);data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);}
            terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
            string grassPath=Root+"/Grass_NoSnow.terrainlayer";
            var grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>(grassPath);
            if(!grass){AssetDatabase.CopyAsset("Assets/TriForge Assets/Top Down - Common Files/Terrain Layers/LAYER_grass_1.terrainlayer",grassPath);grass=AssetDatabase.LoadAssetAtPath<TerrainLayer>(grassPath);}
            grass.tileSize=new Vector2(3.5f,3.5f);grass.smoothness=.12f;EditorUtility.SetDirty(grass);
            var layers=data.terrainLayers;int grassIndex=Array.IndexOf(layers,grass);
            if(grassIndex<0){grassIndex=layers.Length;Array.Resize(ref layers,layers.Length+1);layers[grassIndex]=grass;data.terrainLayers=layers;}
            var surface=terrain.GetComponent<SnowDepthSurface>();if(!surface)surface=terrain.gameObject.AddComponent<SnowDepthSurface>();
            surface.terrain=terrain;surface.maximumDepth=.45f;surface.meshSnowDepth=.36f;
            surface.clearingCenter=player.transform.position+Vector3.forward*1.5f;surface.clearingRadii=new Vector2(4,5);
            Vector3 origin=terrain.transform.position,size=data.size;int w=data.alphamapWidth,h=data.alphamapHeight;
            int x0=Mathf.Clamp(Mathf.FloorToInt((surface.clearingCenter.x-origin.x-8)/size.x*w),0,w-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((surface.clearingCenter.z-origin.z-9)/size.z*h),0,h-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((surface.clearingCenter.x-origin.x+8)/size.x*w),x0+1,w);
            int z1=Mathf.Clamp(Mathf.CeilToInt((surface.clearingCenter.z-origin.z+9)/size.z*h),z0+1,h);
            var a=data.GetAlphamaps(x0,z0,x1-x0,z1-z0);baking=true;
            try{
                for(int z=0;z<z1-z0;z++)for(int x=0;x<x1-x0;x++)
                {
                    Vector3 p=new Vector3(origin.x+(x+x0+.5f)/w*size.x,0,origin.z+(z+z0+.5f)/h*size.z);
                    float r=Radius(surface,p);float cover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1.15f,r));
                    float sum=0;for(int l=0;l<layers.Length;l++)if(l!=grassIndex)sum+=a[z,x,l];
                    if(sum<.001f)continue;
                    for(int l=0;l<layers.Length;l++)if(l!=grassIndex)a[z,x,l]=a[z,x,l]/sum*cover;
                    a[z,x,grassIndex]=1-cover;
                }
                data.SetAlphamaps(x0,z0,a);
            }finally{baking=false;}
            Bake(surface);
            var manager=UnityEngine.Object.FindObjectOfType<SnowTrackSystem>();
            manager.snowSurface=surface;manager.depression=.26f;manager.raisedEdge=.045f;manager.freshSnowBoost=.025f;
            manager.settlingSeconds=.8f;manager.darkening=.12f;manager.maxFootprints=32768;manager.tessellation=64;manager.edgeSmoothing=.045f;manager.normalStrength=.65f;manager.contactShadow=.20f;manager.smoothingShader=Shader.Find("Hidden/Snowfield/Soften");
            var emitter=player.GetComponent<SnowFootEmitter>();emitter.snowSurface=surface;emitter.minimumSpacing=.065f;emitter.minimumInterval=.035f;
            emitter.soleTolerance=.10f;emitter.footprintSize=new Vector2(.23f,.38f);emitter.deepSnowThreshold=.12f;emitter.ploughStrength=.50f;emitter.pressureVariation=.75f;
            EditorUtility.SetDirty(manager);EditorUtility.SetDirty(emitter);EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(surface);EditorUtility.SetDirty(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=manager.gameObject;
            Debug.Log("THICK_SNOW_SETUP_COMPLETE: cloned TerrainData; grass clearing + thin/deep snow. Alphamap "+w+"x"+h);
        }
        static float Radius(SnowDepthSurface s,Vector3 p)
        {
            float x=(p.x-s.clearingCenter.x)/s.clearingRadii.x,z=(p.z-s.clearingCenter.z)/s.clearingRadii.y;
            return Mathf.Sqrt(x*x+z*z)+.045f*Mathf.Sin(p.x*.8f)*Mathf.Sin(p.z*.65f);
        }
        [MenuItem("Tools/Snowfield/根据地形图层重建雪厚图")]
        static void Rebuild(){foreach(var surface in UnityEngine.Object.FindObjectsOfType<SnowDepthSurface>())Bake(surface);}
        public static void Bake(SnowDepthSurface surface)
        {
            if(baking)return;
            baking=true;
            try{SnowSceneConnector.Bake(surface);}finally{baking=false;}
        }
    }
}

