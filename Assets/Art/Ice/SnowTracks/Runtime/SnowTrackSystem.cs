using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Snowfield
{
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class SnowTrackSystem : MonoBehaviour
    {
        [Serializable] public class Cascade
        {
            [Min(2)] public float worldSize = 16;
            [Range(128,2048)] public int resolution = 1024;
            [NonSerialized] public RenderTexture texture;
            [NonSerialized] public Vector2 center;
        }
        [Header("角色与绘制")]
        public Transform followTarget;
        public Shader stampShader;
        public Shader smoothingShader;
        [Header("圆润边缘（米）")]
        [Range(0,.12f)] public float edgeSmoothing=.045f;
        public SnowDepthSurface snowSurface;
        [Range(5,60)] public float updateRate = 20;
        [Header("雪痕时间（秒，使用游戏时间，暂停时冻结）")]
        [Min(0)] public float holdSeconds = 60;
        [Min(0.1f)] public float fadeSeconds = 20;
        [Min(0.05f)] public float settlingSeconds = 0.5f;
        [Range(128,65536)] public int maxFootprints = 32768;
        [Header("形变（米）")]
        [Range(0,0.6f)] public float depression = 0.30f;
        [Range(0,0.2f)] public float raisedEdge = 0.075f;
        [Range(0,0.15f)] public float freshSnowBoost = 0.045f;
        [Range(0,2)] public float normalStrength = 1;
        [Range(0,0.6f)] public float darkening = 0.16f;
        [Range(0,1)] public float contactShadow = 0.35f;
        [Range(1,64)] public float tessellation = 64;
        [Header("级联：覆盖边长 / 纹理像素")]
        public Cascade near = new Cascade { worldSize=16, resolution=1024 };
        public Cascade middle = new Cascade { worldSize=48, resolution=1024 };
        public Cascade far = new Cascade { worldSize=96, resolution=1024 };
        [Header("调试")]
        public bool showCascades;
        public bool showHud;

        public struct Footprint
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector2 size;
            public double created;
            public float pressure;
            public bool left;
            public float furrow;
        }
        readonly List<Footprint> history = new List<Footprint>(2048);
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        readonly Vector4[] data = new Vector4[1023];
        Material stampMaterial;
        Material smoothingMaterial;
        MaterialPropertyBlock properties;
        Mesh quad;
        CommandBuffer commands;
        double nextUpdate;
        bool dirty;
        static readonly int StampData = Shader.PropertyToID("_SnowStampData");
        static readonly int BlurScratch=Shader.PropertyToID("_SnowBlurScratch");
        public int FootprintCount => history.Count;
        public RenderTexture GetCascadeTexture(int i) => GetCascade(i).texture;
        public void PrepareForValidation(){if(commands==null)OnEnable();}
        public void FinishValidation(){OnDisable();}
        Cascade GetCascade(int i) => i==0 ? near : i==1 ? middle : far;
        public static float Visibility(double age, float hold, float fade)
        {
            float t = Mathf.Clamp01((float)(age-hold)/Mathf.Max(0.1f,fade));
            return 1-t*t*(3-2*t);
        }
        void OnEnable()
        {
            if(!snowSurface)snowSurface=FindObjectOfType<SnowDepthSurface>();
            if(snowSurface)snowSurface.Bind();
            if (stampShader==null) stampShader=Shader.Find("Hidden/Snowfield/Stamp");
            if(stampShader==null || !stampShader.isSupported || !SystemInfo.supportsInstancing)
            { Debug.LogError("Snow tracks require the stamp shader and GPU instancing.",this); enabled=false;return; }
            stampMaterial=new Material(stampShader){enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};
            if(!smoothingShader)smoothingShader=Shader.Find("Hidden/Snowfield/Soften");
            if(smoothingShader)smoothingMaterial=new Material(smoothingShader){hideFlags=HideFlags.HideAndDontSave};
            properties=new MaterialPropertyBlock();
            commands=new CommandBuffer {name="Snow footprints: three cascades"};
            quad=new Mesh {name="Snow footprint quad",hideFlags=HideFlags.HideAndDontSave};
            quad.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            quad.triangles=new[]{0,2,1,0,3,2};quad.RecalculateBounds();
            for(int i=0;i<3;i++)
            {
                var c=GetCascade(i);
                c.resolution=Mathf.Clamp(Mathf.ClosestPowerOfTwo(c.resolution),128,2048);
                c.worldSize=Mathf.Max(2,c.worldSize);
                c.texture=new RenderTexture(c.resolution,c.resolution,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {name="Snow cascade "+i,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                c.texture.Create();
            }
            dirty=true; Rebuild(Time.timeAsDouble);
        }
        public void AddFootprint(Vector3 position, Vector3 forward, Vector2 size, bool left, float pressure=1, double created=-1,float thickness=-1,float furrow=0)
        {
            if(thickness<0)thickness=snowSurface?snowSurface.ThicknessAt(position):.36f;
            if(thickness<.008f)return;
            if(history.Count>=maxFootprints) history.RemoveAt(0);
            forward.y=0;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            history.Add(new Footprint {position=position,rotation=Quaternion.LookRotation(forward.normalized),size=size,
                left=left,furrow=furrow,pressure=Mathf.Clamp01(pressure)*Mathf.Clamp01(thickness*.92f/Mathf.Max(.001f,depression)),created=created<0?Time.timeAsDouble:created});
            dirty=true;
        }
        [ContextMenu("清空雪痕")]
        public void ClearFootprints(){history.Clear();dirty=true;if(isActiveAndEnabled)Rebuild(Time.timeAsDouble);}
        void LateUpdate()
        {
            double now=Time.timeAsDouble;
            if(now<nextUpdate)return;
            nextUpdate=now+1.0/Mathf.Max(5,updateRate);Rebuild(now);
        }
        public void Rebuild(double now)
        {
            if(commands==null)return;
            history.RemoveAll(p=>now-p.created>=holdSeconds+fadeSeconds);
            Vector3 target=followTarget!=null?followTarget.position:transform.position;
            commands.Clear();
            for(int layer=0;layer<3;layer++)
            {
                var c=GetCascade(layer);float pixel=c.worldSize/c.resolution;
                c.center=new Vector2(Mathf.Floor(target.x/pixel)*pixel,Mathf.Floor(target.z/pixel)*pixel);
                var map=new Vector4(c.center.x,c.center.y,1/c.worldSize,pixel);
                commands.SetRenderTarget(c.texture);
                commands.SetViewport(new Rect(0,0,c.resolution,c.resolution));
                commands.ClearRenderTarget(false,true,Color.clear);
                commands.SetGlobalVector("_SnowDrawMap",map);
                int count=0;
                for(int j=0;j<history.Count;j++)
                {
                    var p=history[j];
                    if(Mathf.Abs(p.position.x-c.center.x)>c.worldSize*.5f+p.size.y || Mathf.Abs(p.position.z-c.center.y)>c.worldSize*.5f+p.size.y)continue;
                    float fade=Visibility(now-p.created,holdSeconds,fadeSeconds)*p.pressure;
                    float boost=Mathf.Exp(-(float)(now-p.created)/Mathf.Max(.05f,settlingSeconds));
                    matrices[count]=Matrix4x4.TRS(p.position,p.rotation,new Vector3(p.size.x*2.05f,1,p.size.y*1.70f));
                    data[count]=new Vector4(fade,boost,p.left?-1:1,p.furrow);count++;
                    if(count==1023){DrawBatch(count);count=0;}
                }
                if(count>0)DrawBatch(count);
                if(smoothingMaterial && edgeSmoothing>0)
                {
                    commands.GetTemporaryRT(BlurScratch,c.resolution,c.resolution,0,FilterMode.Bilinear,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
                    float step=Mathf.Max(edgeSmoothing/1.4f,pixel*.5f)/c.worldSize;
                    commands.SetGlobalVector("_SnowBlurStep",new Vector4(step,0,0,0));
                    commands.Blit(c.texture,BlurScratch,smoothingMaterial,0);
                    commands.SetGlobalVector("_SnowBlurStep",new Vector4(0,step,0,0));
                    commands.Blit(BlurScratch,c.texture,smoothingMaterial,0);
                    commands.ReleaseTemporaryRT(BlurScratch);
                }
                commands.SetGlobalTexture("_SnowTrail"+layer,c.texture);
                commands.SetGlobalVector("_SnowMap"+layer,map);
            }
            Graphics.ExecuteCommandBuffer(commands);
            Shader.SetGlobalVector("_SnowShape",new Vector4(depression,raisedEdge,freshSnowBoost,normalStrength));
            Shader.SetGlobalVector("_SnowLook",new Vector4(darkening,contactShadow,tessellation,1));
            Shader.SetGlobalVector("_SnowFocus",new Vector4(target.x,target.y,target.z,near.worldSize*.5f));
            dirty=false;
        }
        void DrawBatch(int count)
        {
            properties.Clear();properties.SetVectorArray(StampData,data);
            commands.DrawMeshInstanced(quad,0,stampMaterial,0,matrices,count,properties);
        }
        void OnDisable()
        {
            Shader.SetGlobalVector("_SnowLook",Vector4.zero);
            if(commands!=null){commands.Release();commands=null;}
            for(int i=0;i<3;i++){var c=GetCascade(i);if(c.texture!=null){c.texture.Release();DestroyResource(c.texture);c.texture=null;}}
            DestroyResource(stampMaterial);DestroyResource(quad);history.Clear();
            DestroyResource(smoothingMaterial);
        }
        static void DestroyResource(UnityEngine.Object obj){if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnGUI()
        {
            if(!showHud)return;
            GUI.Box(new Rect(12,12,340,82),"Snow tracks | "+history.Count+" footprints\nHold "+holdSeconds+"s + fade "+fadeSeconds+"s\nNear / middle / far: "+near.worldSize+" / "+middle.worldSize+" / "+far.worldSize+" m");
        }
        void OnDrawGizmosSelected()
        {
            if(!showCascades)return;Vector3 p=followTarget?followTarget.position:transform.position;
            for(int i=0;i<3;i++){Gizmos.color=i==0?Color.green:i==1?Color.yellow:Color.cyan;Gizmos.DrawWireCube(p,new Vector3(GetCascade(i).worldSize,.05f,GetCascade(i).worldSize));}
        }
    }
}
