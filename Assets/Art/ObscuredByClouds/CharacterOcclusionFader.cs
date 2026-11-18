using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ObscuredByClouds.Gameplay
{
    [DefaultExecutionOrder(200), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("ObscuredByClouds/角色遮挡透明")]
    public sealed partial class CharacterOcclusionFader : MonoBehaviour
    {
        [Header("角色与遮挡物")]
        [Tooltip("留空优先取 CameraRotateQE 的角色，再查找同场景的点击移动角色。")]
        public Transform target;
        public LayerMask occluderLayers = ~0;
        [Tooltip("自动发现带有 Occlusion Fade Target 且允许渐变的模型；场景摆放和 Terrain 树均需在 Prefab 上配置。")]
        public bool autoDiscover = true;
        [Tooltip("额外指定模型，也可在模型上添加 Occlusion Fade Target 组件排除。")]
        public OcclusionFadeTarget[] extraTargets = new OcclusionFadeTarget[0];
        [Min(0f)] public float minimumObjectHeight = .6f;
        [Header("遮挡比例与透明度")]
        [InspectorName("触发遮挡比例"), Range(.05f,1f)] public float triggerCoverage = .8f;
        [InspectorName("恢复遮挡比例"), Range(0f,1f)] public float restoreCoverage = .6f;
        [InspectorName("遮挡物不透明度"), Range(.05f,1f)] public float fadedOpacity = .2f;
        [InspectorName("遮挡确认时间"), Range(0f,.5f)] public float activationDelay = .1f;
        [InspectorName("恢复等待时间"), Range(0f,1f)] public float restoreDelay = .2f;
        [InspectorName("渐变时间"), Range(0f,1f)] public float fadeDuration = .2f;
        [Header("采样与性能")]
        [InspectorName("检查间隔"), Range(.03f,.3f)] public float checkInterval = .075f;
        [InspectorName("模型更新间隔"), Min(.2f)] public float discoveryInterval = 1f;
        [InspectorName("横向采样数"), Range(3,9)] public int sampleColumns = 5;
        [InspectorName("纵向采样数"), Range(3,11)] public int sampleRows = 7;
        [Tooltip("角色可见轮廓的高度和宽度，单位米；留 0 时从点击移动控制器的碰撞尺寸自动取。")]
        public float characterHeight;
        public float characterWidth;
        public Vector3 characterFeetOffset;
        public float CurrentCoverage { get; private set; }
        public bool IsOccluded { get; private set; }
        public int FadedObjectCount { get; private set; }
        public int TrackedObjectCount => groups.Count + TerrainTreeCount;

        sealed class Part
        {
            public Renderer renderer;
            public Material[] original, faded;
            public Color[] baseColors;
        }
        sealed class LodFadeState
        {
            public LODGroup lod;
            public LODFadeMode mode;
            public bool animated;
        }
        sealed class Group
        {
            public Transform root;
            public Renderer[] renderers;
            public readonly List<Part> parts = new List<Part>();
            public readonly List<Material> clones = new List<Material>();
            public Bounds bounds;
            public float opacity = 1;
            public float clearSince = -1;
            public bool wanted, bound;
            public int hits;
            public TreeRecord terrainTree;
            public bool layeredFoliage, foliageChecked, lodOverridden;
            public readonly List<LodFadeState> lodStates = new List<LodFadeState>();
            public OcclusionFadeTarget configuration;
            public int configurationSignature;
        }
        sealed class Geometry { public MeshCollider collider; public bool failed; }
        readonly Dictionary<Transform,Group> groups = new Dictionary<Transform,Group>();
        readonly Dictionary<Mesh,Geometry> geometries = new Dictionary<Mesh,Geometry>();
        readonly List<Transform> removed = new List<Transform>();
        readonly HashSet<Transform> discovered = new HashSet<Transform>();
        readonly List<Group> candidates = new List<Group>();
        readonly List<Ray> sampleRays = new List<Ray>();
        readonly List<float> sampleDistances = new List<float>();
        Camera view;
        Scene queryScene;
        CapsuleCollider bodyQuery;
        int queryGeneration;
        float nextCheck, nextDiscovery, blockedSince = -1, clearSince = -1;
        Transform previousTarget;
        static readonly int Opacity = Shader.PropertyToID("_OcclusionOpacity");

        public static bool Supports(Material material)
        {
            if (!material || !material.shader || material.renderQueue > 2500) return false;
            return SupportsFadeShader(material.shader);
        }
        public static bool SupportsFadeShader(Shader shader)
        {
            if(!shader)return false;
            bool states=shader.FindPropertyIndex("_SrcBlend")>=0&&shader.FindPropertyIndex("_DstBlend")>=0&&shader.FindPropertyIndex("_ZWrite")>=0;
            bool custom=shader.FindPropertyIndex("_OcclusionSrcBlend")>=0&&shader.FindPropertyIndex("_OcclusionDstBlend")>=0&&shader.FindPropertyIndex("_OcclusionZWrite")>=0;
            return (shader.FindPropertyIndex("_OcclusionOpacity")>=0&&(states||custom))||
                (states&&shader.FindPropertyIndex("_Surface")>=0&&shader.FindPropertyIndex("_BaseColor")>=0);
        }
        void OnEnable(){view=GetComponent<Camera>();nextDiscovery=nextCheck=0;}
        void OnValidate(){restoreCoverage=Mathf.Min(restoreCoverage,triggerCoverage);characterHeight=Mathf.Max(0,characterHeight);characterWidth=Mathf.Max(0,characterWidth);}
        void LateUpdate()
        {
            ResolveTarget();
            if (!target || !view || !view.enabled) { ResetVisibility();return; }
            var characterSwitch = target.GetComponent<CharacterOcclusionSwitch>();
            if (characterSwitch && !characterSwitch.CanFade) { ResetVisibility();return; }
            if (previousTarget!=target) { ResetVisibility();previousTarget=target;nextDiscovery=0; }
            float now=Time.unscaledTime;
            if(now>=nextDiscovery){RefreshObjects();nextDiscovery=now+Mathf.Max(.2f,discoveryInterval);}
            if(now>=nextCheck){Evaluate(now);nextCheck=now+Mathf.Max(.03f,checkInterval);}
            FadeStep(Time.unscaledDeltaTime);
        }
        void ResolveTarget()
        {
            if(target)return;
            var cameraControl=GetComponent<global::CameraRotateQE>();
            if(cameraControl&&cameraControl.followTarget){target=cameraControl.followTarget;return;}
            foreach(var character in FindObjectsOfType<ClickToMoveCharacter>())
                if(character.gameObject.scene==gameObject.scene){target=character.transform;return;}
        }
        Transform RootFor(Renderer renderer)
        {
            var permission=OcclusionFadeTarget.Owner(renderer);
            return permission&&permission.enabled&&permission.allowFade?permission.transform:null;
        }
        bool Allowed(Renderer renderer,bool includeInactive=false)
        {
            if(!(renderer is MeshRenderer)||(!includeInactive&&!renderer.gameObject.activeInHierarchy))return false;
            if(terrainVisualRenderers.Contains(renderer))return false;
            if((occluderLayers.value&(1<<renderer.gameObject.layer))==0)return false;
            if(view&&(view.cullingMask&(1<<renderer.gameObject.layer))==0)return false;
            if(renderer.forceRenderingOff)return false;
            if(target&&(renderer.transform==target||renderer.transform.IsChildOf(target)))return false;
            var permission=OcclusionFadeTarget.Owner(renderer);
            if(!permission||!permission.enabled||!permission.allowFade||!permission.Includes(renderer))return false;
            var materials=renderer.sharedMaterials;
            if(materials.Length==0)return false;
            foreach(var material in materials)if(!permission.CanFade(material)&&!IsOurClone(material))return false;
            return true;
        }
        bool IsOurClone(Material material)
        {
            // All runtime clones carry this marker; avoid scanning every group/material.
            return material&&material.GetTag("CharacterOcclusionInstance",false,"")==GetInstanceID().ToString();
        }
        void AddRoot(Transform root,Renderer[] overrideRenderers=null)
        {
            if(!root||discovered.Contains(root))return;
            var configuration=root.GetComponent<OcclusionFadeTarget>();
            if(!configuration||!configuration.enabled||!configuration.allowFade)return;
            var all=overrideRenderers!=null&&overrideRenderers.Length>0?overrideRenderers:root.GetComponentsInChildren<Renderer>(true);
            var accepted=new List<Renderer>();foreach(var renderer in all)if(renderer&&OcclusionFadeTarget.Owner(renderer)==configuration&&Allowed(renderer,true))accepted.Add(renderer);
            if(accepted.Count==0)return;
            var group=new Group{root=root,renderers=accepted.ToArray(),configuration=configuration,configurationSignature=configuration.ConfigurationSignature()};UpdateBounds(group);
            if(group.bounds.size.y<minimumObjectHeight)return;
            discovered.Add(root);
            if(groups.TryGetValue(root,out var existing))
            {
                bool same=existing.renderers.Length==group.renderers.Length&&existing.configurationSignature==group.configurationSignature;
                for(int i=0;same&&i<group.renderers.Length;i++)same=existing.renderers[i]==group.renderers[i];
                if(same)return;
                group.opacity=existing.opacity;group.wanted=existing.wanted;Release(existing);
                groups[root]=group;
            }
            else groups.Add(root,group);
        }
        [ContextMenu("重新扫描遮挡模型")]
        public void RefreshObjects()
        {
            if(!Application.isPlaying)return;
            RefreshTerrainTrees();
            discovered.Clear();
            if(autoDiscover)foreach(var permission in FindObjectsOfType<OcclusionFadeTarget>())
                if(permission.gameObject.scene==gameObject.scene&&permission.enabled&&permission.allowFade)AddRoot(permission.transform,permission.renderers);
            foreach(var item in extraTargets)if(item&&item.enabled&&item.allowFade)AddRoot(item.transform,item.renderers);
            removed.Clear();foreach(var pair in groups)if(!pair.Key||!discovered.Contains(pair.Key))removed.Add(pair.Key);
            foreach(var root in removed){Release(groups[root]);groups.Remove(root);}
        }
        void EnsureQueries()
        {
            if(queryScene.IsValid()&&queryScene.isLoaded&&bodyQuery)return;
            queryScene=SceneManager.CreateScene("Occlusion queries "+GetInstanceID()+"_"+(++queryGeneration),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var body=new GameObject("Character visibility proxy"){hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(body,queryScene);bodyQuery=body.AddComponent<CapsuleCollider>();
        }
        Geometry GetGeometry(Mesh mesh)
        {
            if(geometries.TryGetValue(mesh,out var geometry))return geometry;
            geometry=new Geometry();geometries.Add(mesh,geometry);
            try
            {
                EnsureQueries();var go=new GameObject("Occlusion mesh query"){hideFlags=HideFlags.HideAndDontSave};
                SceneManager.MoveGameObjectToScene(go,queryScene);
                geometry.collider=go.AddComponent<MeshCollider>();geometry.collider.sharedMesh=mesh;
                Physics.SyncTransforms();
            }
            catch(Exception){geometry.failed=true;}
            return geometry;
        }
        bool Intersects(Renderer renderer,Ray ray,float distance)
        {
            if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy)return false;
            if(!renderer.bounds.IntersectRay(ray,out float boundDistance)||boundDistance>=distance)return false;
            var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)return false;
            Geometry geometry=GetGeometry(filter.sharedMesh);
            if(geometry.failed||!geometry.collider)return false;
            Matrix4x4 inverse=renderer.transform.worldToLocalMatrix;
            Vector3 localDirection=inverse.MultiplyVector(ray.direction);float scale=localDirection.magnitude;
            if(scale<.000001f)return false;
            var localRay=new Ray(inverse.MultiplyPoint3x4(ray.origin),localDirection/scale);
            return geometry.collider.Raycast(localRay,out _,distance*scale);
        }
        void BuildSamples()
        {
            EnsureQueries();sampleRays.Clear();sampleDistances.Clear();
            var movement=target.GetComponent<ClickToMoveCharacter>();
            float height=characterHeight>0?characterHeight:movement?movement.bodyHeight:1.8f;
            float width=characterWidth>0?characterWidth:movement?Mathf.Min(movement.bodyRadius*2,.7f):.65f;
            height=Mathf.Max(.2f,height);width=Mathf.Clamp(width,.1f,height);
            bodyQuery.transform.position=target.position+characterFeetOffset;bodyQuery.height=height;bodyQuery.radius=width*.5f;bodyQuery.center=Vector3.up*height*.5f;
            Physics.SyncTransforms();Bounds bounds=bodyQuery.bounds;
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<8;i++)
            {
                Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 viewport=view.WorldToViewportPoint(corner);if(viewport.z<=view.nearClipPlane)return;
                min=Vector2.Min(min,viewport);max=Vector2.Max(max,viewport);
            }
            int cols=Mathf.Clamp(sampleColumns,3,9),rows=Mathf.Clamp(sampleRows,3,11);
            for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
            {
                Vector2 uv=new Vector2(Mathf.Lerp(min.x,max.x,(x+.5f)/cols),Mathf.Lerp(min.y,max.y,(y+.5f)/rows));
                if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)continue;
                Ray ray=view.ViewportPointToRay(uv);
                if(bodyQuery.Raycast(ray,out var hit,view.farClipPlane))
                {sampleRays.Add(ray);sampleDistances.Add(Mathf.Max(0,hit.distance-.02f));}
            }
        }
        void UpdateBounds(Group group)
        {
            bool first=true;foreach(var renderer in group.renderers)if(renderer&&renderer.gameObject.activeInHierarchy)
            {if(first){group.bounds=renderer.bounds;first=false;}else group.bounds.Encapsulate(renderer.bounds);}
        }
        void Evaluate(float now)
        {
            BuildSamples();candidates.Clear();
            CollectTerrainTreeCandidates();
            foreach(var group in groups.Values)
            {
                group.hits=0;if(!group.root||!group.root.gameObject.activeInHierarchy||!group.configuration||!group.configuration.enabled||!group.configuration.allowFade)continue;
                UpdateBounds(group);
                // The broad phase is cheap; mesh queries are cooked lazily only for models on a sample ray.
                bool nearby=false;for(int i=0;i<sampleRays.Count;i++)
                    if(group.bounds.IntersectRay(sampleRays[i],out float d)&&d<sampleDistances[i]){nearby=true;break;}
                if(nearby)candidates.Add(group);
            }
            int blocked=0;
            for(int i=0;i<sampleRays.Count;i++)
            {
                bool any=false;foreach(var group in candidates)
                {
                    bool hit=false;foreach(var renderer in group.renderers)
                        if(Intersects(renderer,sampleRays[i],sampleDistances[i])){hit=true;break;}
                    if(hit){group.hits++;any=true;}
                }
                foreach(var tree in treeCandidates)
                    if(IntersectsTree(tree,sampleRays[i],sampleDistances[i]))
                    {tree.group.hits++;any=true;}
                if(any)blocked++;
            }
            CurrentCoverage=sampleRays.Count>0?(float)blocked/sampleRays.Count:0;
            if(!IsOccluded)
            {
                clearSince=-1;
                if(CurrentCoverage>=triggerCoverage)
                {if(blockedSince<0)blockedSince=now;if(now-blockedSince>=activationDelay)IsOccluded=true;}
                else blockedSince=-1;
            }
            else
            {
                blockedSince=-1;
                if(CurrentCoverage<=Mathf.Min(restoreCoverage,triggerCoverage))
                {if(clearSince<0)clearSince=now;if(now-clearSince>=restoreDelay)IsOccluded=false;}
                else clearSince=-1;
            }
            foreach(var group in AllFadeGroups())
            {
                if(IsOccluded&&group.hits>0){group.wanted=true;group.clearSince=-1;}
                else if(IsOccluded&&group.wanted)
                {if(group.clearSince<0)group.clearSince=now;group.wanted=now-group.clearSince<restoreDelay;}
                else {group.wanted=false;group.clearSince=-1;}
            }
        }
        void Bind(Group group)
        {
            if(group.bound)return;group.bound=true;
            var copies=new Dictionary<Material,Material>();
            foreach(var renderer in group.renderers)
            {
                if(!renderer)continue;var original=renderer.sharedMaterials;var faded=new Material[original.Length];var colors=new Color[original.Length];
                for(int i=0;i<original.Length;i++)
                {
                    var source=original[i];if(!source)continue;
                    if(!copies.TryGetValue(source,out var copy))
                    {
                        copy=group.configuration.CreateTransitionMaterial(source);
                        copy.name=source.name+" (Character occlusion)";copy.hideFlags=HideFlags.DontSave;
                        copy.SetOverrideTag("CharacterOcclusionInstance",GetInstanceID().ToString());
                        copy.SetOverrideTag("RenderType","Transparent");copy.renderQueue=3000;
                        if(copy.HasProperty("_OcclusionSrcBlend"))
                        {copy.SetFloat("_OcclusionSrcBlend",(float)BlendMode.SrcAlpha);copy.SetFloat("_OcclusionDstBlend",(float)BlendMode.OneMinusSrcAlpha);copy.SetFloat("_OcclusionZWrite",0);}
                        else
                        {if(copy.HasProperty("_Surface"))copy.SetFloat("_Surface",1);copy.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);copy.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);copy.SetFloat("_ZWrite",0);if(!copy.HasProperty(Opacity))copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");}
                        copy.SetShaderPassEnabled("DepthOnly",false);copy.SetShaderPassEnabled("DepthNormals",false);copy.SetShaderPassEnabled("DepthNormalsOnly",false);copy.SetShaderPassEnabled("GBuffer",false);
                        copies.Add(source,copy);group.clones.Add(copy);
                    }
                    faded[i]=copy;
                    colors[i]=copy.HasProperty("_BaseColor")?copy.GetColor("_BaseColor"):Color.white;
                }
                group.parts.Add(new Part{renderer=renderer,original=original,faded=faded,baseColors=colors});
            }
        }
        void SynchronizeFoliageLods(Group group)
        {
            if(!group.foliageChecked)
            {
                group.foliageChecked=true;
                foreach(var renderer in group.renderers)if(renderer)
                    foreach(var material in renderer.sharedMaterials)
                        if(material&&material.shader&&material.shader.name=="Snowfield/Glacier/VegetationLeaves")
                            group.layeredFoliage=true;
            }
            if(!group.layeredFoliage||group.lodOverridden||!group.root)return;
            group.lodStates.Clear();
            foreach(var lod in group.root.GetComponentsInChildren<LODGroup>(true))
            {
                group.lodStates.Add(new LodFadeState{lod=lod,mode=lod.fadeMode,animated=lod.animateCrossFading});
                // Two alpha-blended LODs double the foliage layers and make snow/leaf
                // coverage pop during a separate occlusion fade. Keep one LOD here.
                lod.fadeMode=LODFadeMode.None;
                lod.animateCrossFading=false;
            }
            group.lodOverridden=true;
        }
        void RestoreFoliageLods(Group group)
        {
            if(!group.lodOverridden)return;
            foreach(var state in group.lodStates)if(state.lod)
            {state.lod.fadeMode=state.mode;state.lod.animateCrossFading=state.animated;}
            group.lodStates.Clear();group.lodOverridden=false;
        }
        void FadeStep(float dt)
        {
            FadedObjectCount=0;
            FadedTerrainTreeCount=0;
            foreach(var group in AllFadeGroups())
            {
                if(!group.configuration||!group.configuration.enabled||!group.configuration.allowFade)
                {group.wanted=false;group.opacity=1;Restore(group);if(group.terrainTree!=null)RestoreTreeVisual(group);continue;}
                float goal=group.wanted?fadedOpacity:1;
                group.opacity=fadeDuration<=0?goal:Mathf.MoveTowards(group.opacity,goal,dt*Mathf.Max(.01f,1-fadedOpacity)/fadeDuration);
                if(group.opacity<.9999f)
                {
                    if(group.terrainTree!=null&&!EnsureTreeVisual(group))continue;
                    if(group.terrainTree!=null)FadedTerrainTreeCount++;
                    SynchronizeFoliageLods(group);
                    Bind(group);FadedObjectCount++;
                    // Update every slot to the same value before publishing renderer arrays.
                    foreach(var part in group.parts)if(part.renderer)
                    {
                        for(int i=0;i<part.faded.Length;i++)if(part.faded[i])
                        {
                            if(part.faded[i].HasProperty(Opacity))part.faded[i].SetFloat(Opacity,group.opacity);
                            else {Color color=part.baseColors[i];color.a*=group.opacity;part.faded[i].SetColor("_BaseColor",color);}
                        }
                    }
                    foreach(var part in group.parts)if(part.renderer)
                    {
                        var current=part.renderer.sharedMaterials;
                        bool same=current.Length==part.faded.Length;
                        for(int i=0;same&&i<current.Length;i++)same=current[i]==part.faded[i];
                        if(!same)part.renderer.sharedMaterials=part.faded;
                    }
                }
                else {Restore(group);if(group.terrainTree!=null)RestoreTreeVisual(group);}
            }
        }
        void Restore(Group group)
        {
            RestoreFoliageLods(group);
            foreach(var part in group.parts)if(part.renderer)
            {
                var current=part.renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<current.Length&&i<part.faded.Length;i++)if(current[i]==part.faded[i]){current[i]=part.original[i];changed=true;}
                if(changed)part.renderer.sharedMaterials=current;
            }
        }
        void Release(Group group)
        {Restore(group);foreach(var clone in group.clones)if(clone)Destroy(clone);group.parts.Clear();group.clones.Clear();}
        void ResetVisibility()
        {foreach(var group in AllFadeGroups()){group.wanted=false;group.opacity=1;group.clearSince=-1;Restore(group);if(group.terrainTree!=null)RestoreTreeVisual(group);}CurrentCoverage=0;IsOccluded=false;blockedSince=clearSince=-1;FadedObjectCount=0;}
        void OnDisable()
        {
            ResetVisibility();foreach(var group in groups.Values)Release(group);groups.Clear();geometries.Clear();
            ReleaseTerrainTrees();
            Scene previousScene=queryScene;queryScene=default;
            if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.UnloadSceneAsync(previousScene);
            bodyQuery=null;
        }
    }
}
