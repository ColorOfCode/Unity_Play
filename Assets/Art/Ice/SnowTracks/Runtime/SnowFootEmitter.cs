using UnityEngine;
namespace Snowfield
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class SnowFootEmitter : MonoBehaviour
    {
        public SnowTrackSystem system;
        public SnowDepthSurface snowSurface;
        public Transform leftFoot,rightFoot,leftToe,rightToe;
        [Header("鞋底与连续扫雪（米）")]
        public Vector2 footprintSize=new Vector2(.23f,.38f);
        [Min(.015f)] public float minimumSpacing=.065f;
        [Min(.01f)] public float minimumInterval=.035f;
        [Range(.05f,.5f)] public float deepSnowThreshold=.12f;
        [Range(.1f,1)] public float ploughStrength=.56f;
        [Range(0,1)] public float pressureVariation=.70f;
        [Header("接触检测")]
        public LayerMask groundMask=~0;
        [Range(.01f,.2f)] public float contactRadius=.055f;
        [Range(.02f,.5f)] public float soleTolerance=.10f;
        public bool emit=true;
        readonly RaycastHit[] hits=new RaycastHit[48];
        readonly Vector3[] previousFoot=new Vector3[2];
        readonly bool[] footValid=new bool[2];
        readonly double[] lastTime={-100,-100};
        readonly float[] previousLegOffset={-.13f,.13f};
        Vector3 previousRoot,lastTravel=Vector3.forward;
        bool rootValid;
        float travelDistance;
        public int ContactStamps {get;private set;}
        public int PloughStamps {get;private set;}
        void OnEnable(){previousRoot=transform.position;rootValid=false;footValid[0]=footValid[1]=false;}
        void Awake()
        {
            if(!system)system=FindObjectOfType<SnowTrackSystem>();
            if(!snowSurface)snowSurface=FindObjectOfType<SnowDepthSurface>();
            foreach(var bone in GetComponentsInChildren<Transform>(true))
            {
                if(!leftFoot && bone.name=="foot_l")leftFoot=bone;
                if(!rightFoot && bone.name=="foot_r")rightFoot=bone;
                if(!leftToe && bone.name=="ball_l")leftToe=bone;
                if(!rightToe && bone.name=="ball_r")rightToe=bone;
            }
        }
        void LateUpdate()
        {
            Vector3 start=previousRoot,now=transform.position;previousRoot=now;
            Vector3 movement=now-start;movement.y=0;
            if(!emit || !system || movement.sqrMagnitude<.0000005f){rootValid=false;return;}
            if(movement.magnitude>1.5f){rootValid=false;footValid[0]=footValid[1]=false;return;}
            lastTravel=movement.normalized;
            travelDistance+=movement.magnitude;
            SweepFoot(leftFoot,leftToe,0);SweepFoot(rightFoot,rightToe,1);
            SweepLegs(rootValid?start:now,now,lastTravel);rootValid=true;
        }
        bool Ground(Vector3 p,out RaycastHit best)
        {
            int count=Physics.SphereCastNonAlloc(p+Vector3.up,contactRadius,Vector3.down,hits,2,groundMask,QueryTriggerInteraction.Ignore);
            best=default;float nearest=float.MaxValue;
            for(int i=0;i<count;i++)
            {
                var h=hits[i];if(h.collider.transform.IsChildOf(transform)||h.normal.y<.55f)continue;
                if(h.distance<nearest){nearest=h.distance;best=h;}
            }
            return best.collider;
        }
        public float Thickness(RaycastHit h)
        {
            if(h.collider.GetComponent<Terrain>())return snowSurface?snowSurface.ThicknessAt(h.point):0;
            return IsSnow(h)?(snowSurface?snowSurface.meshSnowDepth:.36f):0;
        }
        void SweepFoot(Transform foot,Transform toe,int index)
        {
            if(!foot || Time.timeAsDouble-lastTime[index]<minimumInterval)return;
            Vector3 probe=toe?Vector3.Lerp(foot.position,toe.position,.55f):foot.position;
            if(!Ground(probe,out var hit)){footValid[index]=false;return;}
            float depth=Thickness(hit),soleHeight=probe.y-hit.point.y-.075f;
            if(depth<.008f || soleHeight>depth+soleTolerance || soleHeight<-.4f){footValid[index]=false;return;}
            Vector3 forward=toe?toe.position-foot.position:lastTravel;forward.y=0;
            Vector3 from=footValid[index]?previousFoot[index]:hit.point;
            float distance=Vector2.Distance(new Vector2(from.x,from.z),new Vector2(hit.point.x,hit.point.z));
            if(distance>.8f)from=hit.point;
            int samples=Mathf.Clamp(Mathf.CeilToInt(distance/Mathf.Max(.015f,minimumSpacing)),1,24);
            float penetration=Mathf.Clamp01((depth-Mathf.Max(0,soleHeight))/Mathf.Max(.01f,depth));
            float pressure=Mathf.Lerp(.22f,1,penetration);
            pressure*=Mathf.Lerp(1,Mathf.Lerp(.72f,1.08f,Mathf.PerlinNoise(hit.point.x*1.7f,hit.point.z*1.7f)),pressureVariation);
            for(int j=1;j<=samples;j++)
            {
                Vector3 p=Vector3.Lerp(from,hit.point,j/(float)samples);
                float d=hit.collider.GetComponent<Terrain>()&&snowSurface?snowSurface.ThicknessAt(p):depth;
                if(d<.008f)continue;
                system.AddFootprint(p,forward,footprintSize,index==0,pressure,-1,d,0);ContactStamps++;
            }
            previousFoot[index]=hit.point;footValid[index]=true;lastTime[index]=Time.timeAsDouble;
        }
        void SweepLegs(Vector3 from,Vector3 to,Vector3 forward)
        {
            float length=Vector3.Distance(from,to);if(length<.0005f)return;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            int samples=Mathf.Clamp(Mathf.CeilToInt(length/minimumSpacing),1,24);
            for(int side=-1;side<=1;side+=2)
            {
                int index=side<0?0:1;Transform foot=side<0?leftFoot:rightFoot;
                float reach=foot?Vector3.Dot(foot.position-to,forward):0;
                float lateral=foot?Mathf.Clamp(Vector3.Dot(foot.position-to,right),-.32f,.32f):side*.13f;
                float offset=Mathf.Lerp(side*.13f,lateral,.65f)+side*.035f*Mathf.Sin(reach*9);
                offset+=.04f*(Mathf.PerlinNoise(travelDistance*.9f,index*7.3f)-.5f)*pressureVariation;
                float previous=rootValid?previousLegOffset[index]:offset;
                for(int j=1;j<=samples;j++)
                {
                Vector3 p=Vector3.Lerp(from,to,j/(float)samples)+right*Mathf.Lerp(previous,offset,j/(float)samples);
                if(!Ground(p,out var hit) || Mathf.Abs(p.y-hit.point.y)>.4f)continue;
                float depth=Thickness(hit);if(depth<=deepSnowThreshold)continue;
                float deep=Mathf.SmoothStep(0,1,Mathf.InverseLerp(deepSnowThreshold,.32f,depth));
                float atDistance=travelDistance-length*(1-j/(float)samples);
                float organic=Mathf.PerlinNoise(atDistance*1.65f,index*11.7f+3.1f);
                float lift=foot?foot.position.y-hit.point.y-.075f:0;
                float contact=Mathf.Clamp01((depth-lift)/Mathf.Max(.02f,depth));
                float variation=Mathf.Lerp(1,(.3f+.7f*contact)*Mathf.Lerp(.65f,1.25f,organic),pressureVariation);
                float gaitWidth=(1+.20f*Mathf.Sin(reach*8+side))*Mathf.Lerp(.8f,1.18f,organic);
                system.AddFootprint(hit.point,forward,new Vector2(Mathf.Lerp(.20f,.34f,deep)*gaitWidth,.26f),side<0,Mathf.Lerp(.15f,ploughStrength,deep)*variation,-1,depth,1);PloughStamps++;
                }
                previousLegOffset[index]=offset;
            }
        }
        public static bool IsSnow(RaycastHit hit)
        {
            var terrain=hit.collider.GetComponent<Terrain>();
            if(terrain)
            {
                var surface=terrain.GetComponent<SnowDepthSurface>();if(surface)return surface.ThicknessAt(hit.point)>.008f;
                var d=terrain.terrainData;Vector3 uv=hit.point-terrain.transform.position;
                int x=Mathf.Clamp(Mathf.FloorToInt(uv.x/d.size.x*d.alphamapWidth),0,d.alphamapWidth-1);
                int z=Mathf.Clamp(Mathf.FloorToInt(uv.z/d.size.z*d.alphamapHeight),0,d.alphamapHeight-1);
                var alpha=d.GetAlphamaps(x,z,1,1);float snow=0;
                for(int i=0;i<d.terrainLayers.Length;i++)if(d.terrainLayers[i] && d.terrainLayers[i].name.ToLowerInvariant().Contains("snow"))snow+=alpha[0,0,i];
                return snow>.45f;
            }
            for(var t=hit.collider.transform;t!=null;t=t.parent)
            {
                string n=t.name.ToLowerInvariant();if(n.Contains("snowground")||n.Contains("snowmound")||n.Contains("bouldersnow"))return true;
            }
            return false;
        }
    }
}
