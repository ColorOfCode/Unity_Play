using System.Collections.Generic;
using UnityEngine;

namespace ObscuredByClouds.Gameplay
{
    public sealed partial class CharacterOcclusionFader
    {
        [Header("Terrain 绘制的树")]
        [Tooltip("检测 Terrain TreeInstance；只接管需要淡出的单棵树。首次触发时使用运行时 TerrainData 副本，源地形资产不修改。")]
        public bool fadeTerrainTrees = true;
        public int TerrainTreeCount { get; private set; }
        public int FadedTerrainTreeCount { get; private set; }
        const float TreeCellSize = 32f;
        sealed class TreeMesh
        {
            public Mesh mesh;
            public Matrix4x4 relative;
            public Bounds bounds;
            public bool doubleSided;
        }
        sealed class TreeTemplate
        {
            public GameObject prefab;
            public TreeMesh[] meshes;
            public OcclusionFadeTarget configuration;
        }
        sealed class TreeTerrain
        {
            public Terrain terrain;
            public TerrainData source, runtime;
            public TreeTemplate[] templates;
            public readonly Dictionary<Vector2Int,List<TreeRecord>> cells=new Dictionary<Vector2Int,List<TreeRecord>>();
            public int count, sourceCount;
            public Vector3 position;
            public int configurationSignature;
        }
        sealed class TreeRecord
        {
            public TreeTerrain owner;
            public TreeTemplate template;
            public TreeInstance instance;
            public int index;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public Group group;
            public GameObject visual;
            public bool hidden;
        }
        readonly Dictionary<Terrain,TreeTerrain> treeTerrains=new Dictionary<Terrain,TreeTerrain>();
        readonly List<Terrain> removedTerrains=new List<Terrain>();
        readonly HashSet<Terrain> liveTerrains=new HashSet<Terrain>();
        readonly List<Group> nativeGroups=new List<Group>();
        readonly List<TreeRecord> treeCandidates=new List<TreeRecord>();
        readonly HashSet<TreeRecord> seenTrees=new HashSet<TreeRecord>();
        readonly HashSet<Renderer> terrainVisualRenderers=new HashSet<Renderer>();
        IEnumerable<Group> AllFadeGroups()
        {foreach(var group in groups.Values)yield return group;foreach(var group in nativeGroups)yield return group;}

        static Bounds TransformBounds(Bounds local,Matrix4x4 matrix)
        {
            Vector3 x=matrix.MultiplyVector(new Vector3(local.extents.x,0,0));
            Vector3 y=matrix.MultiplyVector(new Vector3(0,local.extents.y,0));
            Vector3 z=matrix.MultiplyVector(new Vector3(0,0,local.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center),2*new Vector3(
                Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z)));
        }
        TreeTemplate MakeTemplate(GameObject prefab)
        {
            if(!prefab)return null;
            var permission=prefab.GetComponent<OcclusionFadeTarget>();
            if(!permission||!permission.enabled||!permission.allowFade)return null;
            var all=prefab.GetComponentsInChildren<MeshRenderer>(true);
            var meshes=new List<TreeMesh>();var lod=prefab.GetComponent<LODGroup>();
            var highDetail=new HashSet<Renderer>();
            if(lod){var levels=lod.GetLODs();if(levels.Length>0)foreach(var r in levels[0].renderers)if(r)highDetail.Add(r);}
            foreach(var renderer in all)
            {
                if(!renderer.enabled)continue;
                if(!permission.Includes(renderer))continue;
                var materials=renderer.sharedMaterials;if(materials.Length==0)continue;
                bool supported=true,doubleSided=false;
                foreach(var material in materials)
                {
                    if(!permission.CanFade(material)){supported=false;break;}
                    string shader=material.shader.name;
                    doubleSided|=shader.StartsWith("TriForge/URP/Tree")||shader.Contains("VegetationLeaves")||shader.Contains("Impostor")||
                        (material.HasProperty("_Cull")&&material.GetFloat("_Cull")==0);
                }
                if(!supported)return null;
                if(highDetail.Count>0&&!highDetail.Contains(renderer))continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                meshes.Add(new TreeMesh{mesh=filter.sharedMesh,bounds=filter.sharedMesh.bounds,
                    relative=prefab.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix,doubleSided=doubleSided});
            }
            return meshes.Count>0?new TreeTemplate{prefab=prefab,meshes=meshes.ToArray(),configuration=permission}:null;
        }
        int TreePrototypeConfiguration(TerrainData data)
        {
            unchecked
            {
                int signature=17;
                foreach(var prototype in data.treePrototypes)
                {
                    var prefab=prototype.prefab;var permission=prefab?prefab.GetComponent<OcclusionFadeTarget>():null;
                    signature=signature*397+(prefab?prefab.GetInstanceID():0);
                    signature=signature*397+(permission?permission.ConfigurationSignature():0);
                }
                return signature;
            }
        }
        TreeTerrain CacheTerrain(Terrain terrain)
        {
            var source=terrain.terrainData;var prototypes=source.treePrototypes;
            var state=new TreeTerrain{terrain=terrain,source=source,sourceCount=source.treeInstanceCount,position=terrain.transform.position,templates=new TreeTemplate[prototypes.Length],configurationSignature=TreePrototypeConfiguration(source)};
            for(int i=0;i<prototypes.Length;i++)state.templates[i]=MakeTemplate(prototypes[i].prefab);
            var instances=source.treeInstances;
            for(int i=0;i<instances.Length;i++)
            {
                var tree=instances[i];if(tree.widthScale<=0||tree.heightScale<=0||tree.prototypeIndex<0||tree.prototypeIndex>=state.templates.Length)continue;
                var template=state.templates[tree.prototypeIndex];if(template==null)continue;
                Vector3 position=terrain.transform.position+Vector3.Scale(tree.position,source.size);
                var prefab=template.prefab.transform;
                var matrix=Matrix4x4.TRS(position,Quaternion.AngleAxis(tree.rotation*Mathf.Rad2Deg,Vector3.up)*prefab.localRotation,
                    Vector3.Scale(prefab.localScale,new Vector3(tree.widthScale,tree.heightScale,tree.widthScale)));
                bool first=true;Bounds bounds=default;
                foreach(var part in template.meshes){var b=TransformBounds(part.bounds,matrix*part.relative);if(first){bounds=b;first=false;}else bounds.Encapsulate(b);}
                if(first||bounds.size.y<minimumObjectHeight)continue;
                var record=new TreeRecord{owner=state,template=template,instance=tree,index=i,matrix=matrix,bounds=bounds};
                int x0=Mathf.FloorToInt(bounds.min.x/TreeCellSize),x1=Mathf.FloorToInt(bounds.max.x/TreeCellSize);
                int z0=Mathf.FloorToInt(bounds.min.z/TreeCellSize),z1=Mathf.FloorToInt(bounds.max.z/TreeCellSize);
                for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                {var key=new Vector2Int(x,z);if(!state.cells.TryGetValue(key,out var list)){list=new List<TreeRecord>();state.cells.Add(key,list);}list.Add(record);}
                state.count++;
            }
            return state;
        }
        void RefreshTerrainTrees()
        {
            liveTerrains.Clear();
            if(fadeTerrainTrees)foreach(var terrain in Terrain.activeTerrains)
            {
                if(!terrain||!terrain.terrainData||terrain.gameObject.scene!=gameObject.scene)continue;
                liveTerrains.Add(terrain);
                if(treeTerrains.TryGetValue(terrain,out var state))
                {
                    var data=terrain.terrainData;
                    if((data==state.source||data==state.runtime)&&state.sourceCount==state.source.treeInstanceCount&&state.position==terrain.transform.position&&state.configurationSignature==TreePrototypeConfiguration(state.source))continue;
                    ReleaseTreeTerrain(state);treeTerrains.Remove(terrain);
                }
                treeTerrains.Add(terrain,CacheTerrain(terrain));
            }
            removedTerrains.Clear();foreach(var pair in treeTerrains)if(!liveTerrains.Contains(pair.Key))removedTerrains.Add(pair.Key);
            foreach(var terrain in removedTerrains){ReleaseTreeTerrain(treeTerrains[terrain]);treeTerrains.Remove(terrain);}
            TerrainTreeCount=0;foreach(var state in treeTerrains.Values)TerrainTreeCount+=state.count;
        }
        void CollectTerrainTreeCandidates()
        {
            treeCandidates.Clear();seenTrees.Clear();foreach(var group in nativeGroups)group.hits=0;
            if(!fadeTerrainTrees||sampleRays.Count==0)return;
            Vector3 min=view.transform.position,max=min;
            for(int i=0;i<sampleRays.Count;i++){Vector3 p=sampleRays[i].GetPoint(sampleDistances[i]);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            int x0=Mathf.FloorToInt(min.x/TreeCellSize),x1=Mathf.FloorToInt(max.x/TreeCellSize);
            int z0=Mathf.FloorToInt(min.z/TreeCellSize),z1=Mathf.FloorToInt(max.z/TreeCellSize);
            foreach(var state in treeTerrains.Values)
            {
                if(!state.terrain||!state.terrain.isActiveAndEnabled||!state.terrain.drawTreesAndFoliage)continue;
                for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                {
                    if(!state.cells.TryGetValue(new Vector2Int(x,z),out var records))continue;
                    foreach(var tree in records)
                    {
                        if(!seenTrees.Add(tree))continue;
                        if(!tree.template.configuration||!tree.template.configuration.enabled||!tree.template.configuration.allowFade)continue;
                        int layer=tree.template.prefab.layer;
                        if((occluderLayers.value&(1<<layer))==0||(view.cullingMask&(1<<layer))==0)continue;
                        if(Vector3.Distance(view.transform.position,tree.bounds.center)>state.terrain.treeDistance+tree.bounds.extents.magnitude)continue;
                        bool nearby=false;for(int i=0;i<sampleRays.Count;i++)
                            if(tree.bounds.IntersectRay(sampleRays[i],out float d)&&d<sampleDistances[i]){nearby=true;break;}
                        if(!nearby)continue;
                        if(tree.group==null){tree.group=new Group{terrainTree=tree,renderers=new Renderer[0],bounds=tree.bounds,configuration=tree.template.configuration};nativeGroups.Add(tree.group);}
                        treeCandidates.Add(tree);
                    }
                }
            }
        }
        bool IntersectsTree(TreeRecord tree,Ray ray,float distance)
        {
            foreach(var part in tree.template.meshes)
            {
                Matrix4x4 matrix=tree.matrix*part.relative;
                if(!TransformBounds(part.bounds,matrix).IntersectRay(ray,out float d)||d>=distance)continue;
                var geometry=GetGeometry(part.mesh);if(geometry.failed||!geometry.collider)continue;
                Matrix4x4 inverse=matrix.inverse;Vector3 direction=inverse.MultiplyVector(ray.direction);float scale=direction.magnitude;
                if(scale<.000001f)continue;
                var local=new Ray(inverse.MultiplyPoint3x4(ray.origin),direction/scale);float length=distance*scale;
                if(geometry.collider.Raycast(local,out _,length))return true;
                if(part.doubleSided&&geometry.collider.Raycast(new Ray(local.GetPoint(length),-local.direction),out _,length))return true;
            }
            return false;
        }
        bool EnsureTreeVisual(Group group)
        {
            var tree=group.terrainTree;if(tree.visual)return true;
            var state=tree.owner;if(!state.terrain||!state.source)return false;
            if(!state.runtime)
            {
                state.runtime=Instantiate(state.source);state.runtime.name=state.source.name+" (runtime occlusion trees)";state.runtime.hideFlags=HideFlags.DontSave;
                state.terrain.terrainData=state.runtime;
            }
            if(state.terrain.terrainData!=state.runtime)return false;
            // An inactive parent prevents prefab behaviours/colliders from briefly joining gameplay.
            var holder=new GameObject("Terrain tree occlusion staging");holder.SetActive(false);holder.hideFlags=HideFlags.DontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder,gameObject.scene);
            var visual=Instantiate(tree.template.prefab,holder.transform);visual.name=tree.template.prefab.name+" (Terrain occlusion "+tree.index+")";
            foreach(var collider in visual.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                if(!(behaviour is Snowfield.TerrainSnowMaterialMatcher))behaviour.enabled=false;
            var prefab=tree.template.prefab.transform;var instance=tree.instance;
            visual.transform.SetPositionAndRotation(state.terrain.transform.position+Vector3.Scale(instance.position,state.source.size),
                Quaternion.AngleAxis(instance.rotation*Mathf.Rad2Deg,Vector3.up)*prefab.localRotation);
            visual.transform.localScale=Vector3.Scale(prefab.localScale,new Vector3(instance.widthScale,instance.heightScale,instance.widthScale));
            visual.hideFlags=HideFlags.DontSave;
            var renderers=visual.GetComponentsInChildren<MeshRenderer>(true);var block=new MaterialPropertyBlock();
            foreach(var renderer in renderers)
            {
                terrainVisualRenderers.Add(renderer);renderer.GetPropertyBlock(block);block.SetColor("_TreeInstanceColor",instance.color);block.SetColor("_TreeInstanceLightmapColor",instance.lightmapColor);renderer.SetPropertyBlock(block);
            }
            var hidden=instance;hidden.widthScale=hidden.heightScale=0;state.runtime.SetTreeInstance(tree.index,hidden);
            var localConfiguration=visual.GetComponent<OcclusionFadeTarget>();var selected=new List<Renderer>();
            foreach(var renderer in renderers)if(localConfiguration&&localConfiguration.Includes(renderer))selected.Add(renderer);
            tree.hidden=true;tree.visual=visual;group.root=visual.transform;group.renderers=selected.ToArray();
            visual.SetActive(true);visual.transform.SetParent(null,true);
            // Resolve snow tint/normal overrides before its first visible frame,
            // instead of letting the next Update change only the snow slots.
            foreach(var matcher in visual.GetComponentsInChildren<Snowfield.TerrainSnowMaterialMatcher>(true))
                if(matcher.isActiveAndEnabled)matcher.RefreshNow();
            Destroy(holder);return true;
        }
        void RestoreTreeVisual(Group group)
        {
            var tree=group.terrainTree;
            if(tree.hidden&&tree.owner.runtime&&tree.index<tree.owner.runtime.treeInstanceCount)
                tree.owner.runtime.SetTreeInstance(tree.index,tree.instance);
            tree.hidden=false;
            if(tree.visual)
            {
                foreach(var renderer in tree.visual.GetComponentsInChildren<Renderer>(true))terrainVisualRenderers.Remove(renderer);
                Release(group);tree.visual.SetActive(false);Destroy(tree.visual);tree.visual=null;
            }
            group.root=null;group.renderers=new Renderer[0];group.bound=false;
        }
        void ReleaseTreeTerrain(TreeTerrain state)
        {
            for(int i=nativeGroups.Count-1;i>=0;i--)
            {
                var group=nativeGroups[i];if(group.terrainTree.owner!=state)continue;
                Restore(group);RestoreTreeVisual(group);group.terrainTree.group=null;nativeGroups.RemoveAt(i);
            }
            if(state.runtime)
            {if(state.terrain&&state.terrain.terrainData==state.runtime)state.terrain.terrainData=state.source;Destroy(state.runtime);state.runtime=null;}
        }
        void ReleaseTerrainTrees()
        {
            foreach(var state in treeTerrains.Values)ReleaseTreeTerrain(state);
            treeTerrains.Clear();nativeGroups.Clear();treeCandidates.Clear();seenTrees.Clear();terrainVisualRenderers.Clear();TerrainTreeCount=FadedTerrainTreeCount=0;
        }
    }
}
