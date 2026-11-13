using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using ObscuredByClouds.Gameplay;

namespace Snowfield.EditorTools
{
    public static class SnowSceneConnector
    {
        const string Root="Assets/Art/Ice/SnowTracks";
        public static string LastReport {get;private set;}="尚未检查。";
        public static Vector3 NearestSnow {get;private set;}
        public static bool HasNearestSnow {get;private set;}
        public static string Folder(Terrain terrain)
        {
            string sceneGuid=AssetDatabase.AssetPathToGUID(terrain.gameObject.scene.path);
            if(string.IsNullOrEmpty(sceneGuid))throw new InvalidOperationException("请先保存当前场景，再一键接入。");
            string objectId=GlobalObjectId.GetGlobalObjectIdSlow(terrain).targetObjectId.ToString();
            return Root+"/Generated/"+sceneGuid+"/Terrain_"+objectId;
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        public static bool SnowName(TerrainLayer l)
        {
            if(!l)return false;string n=l.name.ToLowerInvariant();
            return (n.Contains("snow")||n.Contains("雪"))&&!n.Contains("nosnow")&&!n.Contains("无雪");
        }
        static Shader Adapter(Material m)
        {
            string n=m&&m.shader?m.shader.name:"";
            if(n=="ObscuredByClouds/Terrain Lit Indirect" || n=="Snowfield/OBC Terrain Lit Indirect")return Shader.Find("Snowfield/OBC Terrain Lit Indirect");
            if(n=="Universal Render Pipeline/Terrain/Lit" || n=="Snowfield/Terrain Lit" || string.IsNullOrEmpty(n))return Shader.Find("Snowfield/Terrain Lit");
            return null;
        }
        [MenuItem("Tools/Snowfield/一键接入当前场景",false,0)]
        public static void ConnectMenu(){ConnectCurrentScene();SnowSceneConnectorWindow.Open();}
        public static bool ConnectCurrentScene()
        {
            var b=new StringBuilder();HasNearestSnow=false;
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先退出 Play 模式。");
                var scene=SceneManager.GetActiveScene();
                if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("请先保存场景。");
                if(!(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("当前适配器要求 URP；没有自动转换渲染管线。");
                var terrains=Terrain.activeTerrains.Where(t=>t.gameObject.scene==scene).ToArray();
                if(terrains.Length!=1)throw new InvalidOperationException("当前一键接入支持单 Terrain 场景。当前数量="+terrains.Length+"；纯网格地面或多 Terrain 需要额外适配。");
                var terrain=terrains[0];Shader shader=Adapter(terrain.materialTemplate);
                if(!shader || !shader.isSupported)throw new InvalidOperationException("地形材质没有可用的雪痕适配器："+(terrain.materialTemplate?terrain.materialTemplate.shader.name:"NULL")+"。已保留现有材质，需先适配自定义 Shader。");
                var managers=UnityEngine.Object.FindObjectsOfType<SnowTrackSystem>().Where(x=>x.gameObject.scene==scene).ToArray();
                if(managers.Length>1)throw new InvalidOperationException("发现多个雪痕管理器，请保留一个再接入。");
                var emitters=UnityEngine.Object.FindObjectsOfType<SnowFootEmitter>().Where(x=>x.gameObject.scene==scene).ToArray();
                var player=UnityEngine.Object.FindObjectsOfType<ClickToMoveCharacter>().FirstOrDefault(x=>x.gameObject.scene==scene);
                if(emitters.Length==0 && !player)throw new InvalidOperationException("没有角色 SnowFootEmitter；请先挂在角色移动根节点上。");
                var manager=managers.FirstOrDefault();
                string folder=Folder(terrain);EnsureFolder(folder);
                string backup=folder+"/BeforeSnowConnection.unity";
                if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
                if(!manager)manager=new GameObject("Snow Tracks — 雪痕管理").AddComponent<SnowTrackSystem>();
                if(emitters.Length==0)emitters=new[]{player.gameObject.AddComponent<SnowFootEmitter>()};
                var surface=terrain.GetComponent<SnowDepthSurface>();if(!surface)surface=terrain.gameObject.AddComponent<SnowDepthSurface>();
                surface.terrain=terrain;
                if(!surface.useExplicitLayers)
                {
                    surface.snowLayers=terrain.terrainData.terrainLayers.Where(SnowName).Select(l=>new SnowLayerDepth{layer=l,depth=surface.meshSnowDepth}).ToArray();
                    surface.useExplicitLayers=true;surface.useClearingGradient=surface.useClearingGradient && surface.clearingCenter.sqrMagnitude>.0001f;
                }
                Material current=terrain.materialTemplate;
                if(current && !current.shader.name.StartsWith("Snowfield/"))surface.sourceMaterial=current;
                string materialPath=folder+"/Terrain_Snow.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material){material=current?new Material(current):new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
                material.shader=shader;EditorUtility.SetDirty(material);terrain.materialTemplate=material;
                manager.snowSurface=surface;manager.stampShader=Shader.Find("Hidden/Snowfield/Stamp");manager.smoothingShader=Shader.Find("Hidden/Snowfield/Soften");
                if(!manager.followTarget || manager.followTarget.gameObject.scene!=scene)manager.followTarget=emitters[0].transform;
                foreach(var e in emitters)
                {
                    e.system=manager;e.snowSurface=surface;e.groundMask=e.groundMask.value|(1<<terrain.gameObject.layer);
                    var animator=e.GetComponentInChildren<Animator>();
                    foreach(var t in e.GetComponentsInChildren<Transform>(true))
                    {
                        if(!e.leftFoot && t.name=="foot_l")e.leftFoot=t;if(!e.rightFoot && t.name=="foot_r")e.rightFoot=t;
                        if(!e.leftToe && t.name=="ball_l")e.leftToe=t;if(!e.rightToe && t.name=="ball_r")e.rightToe=t;
                    }
                    if(animator && animator.isHuman){if(!e.leftFoot)e.leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);if(!e.rightFoot)e.rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);if(!e.leftToe)e.leftToe=animator.GetBoneTransform(HumanBodyBones.LeftToes);if(!e.rightToe)e.rightToe=animator.GetBoneTransform(HumanBodyBones.RightToes);}
                    if(!e.leftFoot || !e.rightFoot)b.AppendLine("待处理："+e.name+" 左右脚骨未找到，请在组件中指定。");
                    EditorUtility.SetDirty(e);PrefabUtility.RecordPrefabInstancePropertyModifications(e);
                }
                Bake(surface);
                EditorUtility.SetDirty(surface);EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(manager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                b.AppendLine("已接入："+scene.name);b.AppendLine("材质："+shader.name);b.AppendLine("雪厚图："+AssetDatabase.GetAssetPath(surface.thicknessMap));
                foreach(var entry in surface.snowLayers)if(entry.layer)b.AppendLine("雪层："+entry.layer.name+" / "+entry.depth.ToString("F2")+" 米");
                if(surface.snowLayers.Length==0)b.AppendLine("当前没有识别到雪图层。请在 Terrain 的 SnowDepthSurface 中指定雪图层，或添加并绘制雪图层后再点一键接入。工具不会把草地自动变成雪地。");
                float depth=surface.ThicknessAt(manager.followTarget.position);b.AppendLine("角色当前雪厚："+(depth*100).ToString("F1")+" 厘米");
                if(depth<.008f)b.AppendLine("当前位置无雪，不产生雪痕。请走到已绘制的雪层区域。");
                HasNearestSnow=FindNearestSnow(surface,manager.followTarget.position,out var nearest);NearestSnow=nearest;
                if(HasNearestSnow)b.AppendLine("最近雪区："+nearest.ToString("F2")+"；距角色约 "+Vector2.Distance(new Vector2(nearest.x,nearest.z),new Vector2(manager.followTarget.position.x,manager.followTarget.position.z)).ToString("F1")+" 米。");
                b.AppendLine("场景备份："+backup);LastReport=b.ToString();SessionState.SetString("SnowConnectionReport",LastReport);
                Debug.Log(LastReport);Selection.activeGameObject=manager.gameObject;return true;
            }
            catch(Exception e){LastReport=b+"接入未完成："+e.Message;SessionState.SetString("SnowConnectionReport",LastReport);Debug.LogWarning(LastReport);return false;}
        }
        public static void Bake(SnowDepthSurface s)
        {
            if(!s || !s.terrain)return;
            var d=s.terrain.terrainData;int w=d.alphamapWidth,h=d.alphamapHeight;var layers=d.terrainLayers;
            var depths=new float[layers.Length];
            for(int i=0;i<layers.Length;i++)
            {
                if(s.useExplicitLayers){foreach(var entry in s.snowLayers??new SnowLayerDepth[0])if(entry!=null && entry.layer==layers[i])depths[i]=Mathf.Clamp(entry.depth,0,s.maximumDepth);}
                else if(SnowName(layers[i]))depths[i]=s.meshSnowDepth;
            }
            var colors=new Color32[w*h];Vector3 origin=s.terrain.transform.position,size=d.size;
            for(int row=0;row<h;row+=32)
            {
                int rows=Mathf.Min(32,h-row);var a=d.GetAlphamaps(0,row,w,rows);
                for(int z=0;z<rows;z++)for(int x=0;x<w;x++)
                {
                    float depth=0;for(int l=0;l<layers.Length;l++)depth+=a[z,x,l]*depths[l];
                    if(s.useClearingGradient){float px=(origin.x+(x+.5f)/w*size.x-s.clearingCenter.x)/Mathf.Max(.1f,s.clearingRadii.x),pz=(origin.z+(row+z+.5f)/h*size.z-s.clearingCenter.z)/Mathf.Max(.1f,s.clearingRadii.y);float r=Mathf.Sqrt(px*px+pz*pz);depth*=Mathf.Lerp(.025f/Mathf.Max(.025f,s.meshSnowDepth),1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,2.2f,r)));}
                    byte value=(byte)Mathf.RoundToInt(Mathf.Clamp01(depth/s.maximumDepth)*255);colors[(row+z)*w+x]=new Color32(value,value,value,255);
                }
            }
            string folder=Folder(s.terrain);EnsureFolder(folder);string path=folder+"/SnowThickness.asset";
            var map=AssetDatabase.LoadAssetAtPath<Texture2D>(path);bool changed=false;
            if(!map){map=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name="SnowThickness",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};AssetDatabase.CreateAsset(map,path);changed=true;}
            if(map.width!=w || map.height!=h){map.Reinitialize(w,h);changed=true;}
            if(!changed){var old=map.GetPixels32();for(int i=0;i<old.Length;i++)if(old[i].r!=colors[i].r){changed=true;break;}}
            if(changed){map.SetPixels32(colors);map.Apply(false,false);EditorUtility.SetDirty(map);AssetDatabase.SaveAssets();}
            if(s.thicknessMap!=map){s.thicknessMap=map;EditorUtility.SetDirty(s);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);}
            s.Bind();
        }
        public static bool FindNearestSnow(SnowDepthSurface s,Vector3 point,out Vector3 nearest)
        {
            nearest=point;if(!s || !s.thicknessMap)return false;
            var map=s.thicknessMap;var pixels=map.GetPixels32();Vector3 o=s.terrain.transform.position,size=s.terrain.terrainData.size;float best=float.MaxValue;
            for(int z=0;z<map.height;z+=2)for(int x=0;x<map.width;x+=2)
            {
                if(pixels[z*map.width+x].r/255f*s.maximumDepth<.025f)continue;
                Vector3 p=new Vector3(o.x+(x+.5f)/map.width*size.x,0,o.z+(z+.5f)/map.height*size.z);
                float distance=(p.x-point.x)*(p.x-point.x)+(p.z-point.z)*(p.z-point.z);if(distance<best){best=distance;nearest=p;}
            }
            if(best==float.MaxValue)return false;nearest.y=s.terrain.SampleHeight(nearest)+o.y;return true;
        }
    }
    public class SnowSceneConnectorWindow:EditorWindow
    {
        Vector2 scroll;
        [MenuItem("Tools/Snowfield/雪痕接入与检查",false,1)]
        public static void Open(){GetWindow<SnowSceneConnectorWindow>("雪痕一键接入");}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("保存场景后点击接入。自动备份、连接组件、独立雪厚图及材质。草地不产生雪痕。支持 URP Terrain Lit、当前森林间接光材质及已有雪痕材质；其他自定义 Shader 会明确提示。",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("一键检查并接入当前场景",GUILayout.Height(34)))SnowSceneConnector.ConnectCurrentScene();
            scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.TextArea(SessionState.GetString("SnowConnectionReport",SnowSceneConnector.LastReport),GUILayout.ExpandHeight(true));EditorGUILayout.EndScrollView();
            if(SnowSceneConnector.HasNearestSnow && GUILayout.Button("在 Scene 视图定位最近的雪区")){SceneView.lastActiveSceneView?.LookAt(SnowSceneConnector.NearestSnow);}
        }
    }
}
