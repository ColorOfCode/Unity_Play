using UnityEngine;
namespace Snowfield
{
    [System.Serializable]
    public class SnowLayerDepth
    {
        public TerrainLayer layer;
        [Range(0,1)] public float depth=.36f;
    }
    // Metres above the collision surface. CPU contacts and GPU geometry share it.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SnowDepthSurface : MonoBehaviour
    {
        public Terrain terrain;
        public Texture2D thicknessMap;
        [Range(.05f,1)] public float maximumDepth=.45f;
        [Range(0,1)] public float meshSnowDepth=.36f;
        [Header("雪地图层及厚度（米）")]
        public bool useExplicitLayers;
        public SnowLayerDepth[] snowLayers=new SnowLayerDepth[0];
        public bool useClearingGradient=true;
        [HideInInspector] public Material sourceMaterial;
        public Vector3 clearingCenter;
        public Vector2 clearingRadii=new Vector2(4,5);
        public float ThicknessAt(Vector3 world)
        {
            if(!terrain || !thicknessMap)return 0;
            Vector3 p=world-terrain.transform.position,s=terrain.terrainData.size;
            if(p.x<0 || p.z<0 || p.x>s.x || p.z>s.z)return 0;
            return thicknessMap.GetPixelBilinear(p.x/s.x,p.z/s.z).r*maximumDepth;
        }
        public void Bind()
        {
            if(!terrain)terrain=GetComponent<Terrain>();
            if(!terrain || !thicknessMap)return;
            Vector3 p=terrain.transform.position,s=terrain.terrainData.size;
            Shader.SetGlobalTexture("_SnowDepthMap",thicknessMap);
            Shader.SetGlobalVector("_SnowDepthBounds",new Vector4(p.x,p.z,1/s.x,1/s.z));
            Shader.SetGlobalVector("_SnowDepthConfig",new Vector4(maximumDepth,meshSnowDepth,0,1));
        }
        void OnEnable(){Bind();}
        void OnValidate(){Bind();}
        void OnDisable(){Shader.SetGlobalVector("_SnowDepthConfig",Vector4.zero);}
    }
}
