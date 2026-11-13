Shader "Hidden/Snowfield/Stamp"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SnowDrawMap;
            UNITY_INSTANCING_BUFFER_START(SnowStamp)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SnowStampData)
            UNITY_INSTANCING_BUFFER_END(SnowStamp)
            struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 stamp:TEXCOORD1;float height:TEXCOORD2;float2 worldXZ:TEXCOORD3;};
            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v); V o;
                float3 world=TransformObjectToWorld(v.positionOS);
                o.positionCS=float4((world.xz-_SnowDrawMap.xy)*_SnowDrawMap.z*2,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.positionCS.y=-o.positionCS.y;
                #endif
                o.uv=v.uv*2-1;o.stamp=UNITY_ACCESS_INSTANCED_PROP(SnowStamp,_SnowStampData);o.height=world.y;o.worldXZ=world.xz;return o;
            }
            float4 Frag(V i):SV_TARGET
            {
                float2 p=i.uv; p.x*=i.stamp.z;
                float width=lerp(.49,.76,saturate(p.y*.65+.5));
                p.x+=(1-i.stamp.w)*.055*sin(p.y*3.5);
                float shape=1-length(float2(p.x/width,(p.y+.02)/.83));
                shape=lerp(shape,1-length(float2(p.x/.78,p.y/.94)),i.stamp.w);
                float2 w=i.worldXZ;
                float noise=sin(w.x*5.3+w.y*3.7)*sin(w.y*4.1-w.x*2.9);
                float edge=shape+noise*lerp(.025,.045,i.stamp.w);
                clip(edge+.24);
                float radial=saturate(1-edge);
                float bowl=saturate(1-radial*radial);
                float depression=bowl*bowl;
                float tread=1-(1-i.stamp.w)*.025*smoothstep(.5,.85,sin(p.y*42))*smoothstep(.1,.3,shape);
                float rim=exp(-pow((edge-.015)/.24,2))*(.90+.10*noise);
                return float4(depression*tread*i.stamp.x,rim*i.stamp.x,(i.height+256)/512,rim*i.stamp.x*i.stamp.y);
            }
            ENDHLSL
        }
    }
}
