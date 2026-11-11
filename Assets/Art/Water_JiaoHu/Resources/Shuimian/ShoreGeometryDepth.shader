Shader "Hidden/Shuimian/ShoreGeometryDepth"
{
    SubShader
    {
        Pass
        {
            Name "Capture World Mesh Heights"
            Cull Off ZTest Always ZWrite Off Blend One One BlendOp Max
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _ShuimianDepthRect;
            float _ShuimianHeightBase;
            struct Input {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float height:TEXCOORD0;};
            Varyings Vertex(Input input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                float2 uv=(world.xz-_ShuimianDepthRect.xy)/_ShuimianDepthRect.zw;
                output.positionCS=float4(uv*2-1,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                    output.positionCS.y=-output.positionCS.y;
                #endif
                output.height=world.y-_ShuimianHeightBase;
                return output;
            }
            float4 Fragment(Varyings input):SV_Target{return float4(max(0,input.height),1,0,1);}
            ENDHLSL
        }
        Pass
        {
            Name "Merge Terrain And Mesh Shore Depth"
            Cull Off ZTest Always ZWrite Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_ARRAY(_ShuimianTerrainDepth);SAMPLER(sampler_ShuimianTerrainDepth);
            TEXTURE2D(_ShuimianMeshDepth);SAMPLER(sampler_ShuimianMeshDepth);
            float4 _ShuimianDepthRect,_ShuimianDepthCascade,_ShuimianDepthSettings;
            struct Varyings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vertex(uint id:SV_VertexID){Varyings o;o.positionCS=GetFullScreenTriangleVertexPosition(id);o.uv=GetFullScreenTriangleTexCoord(id);return o;}
            float2 MeshSample(float2 uv){return SAMPLE_TEXTURE2D_LOD(_ShuimianMeshDepth,sampler_ShuimianMeshDepth,uv,0).rg;}
            float4 Fragment(Varyings input):SV_Target
            {
                float4 depth=SAMPLE_TEXTURE2D_ARRAY_LOD(_ShuimianTerrainDepth,sampler_ShuimianTerrainDepth,input.uv,_ShuimianDepthCascade.w,0);
                float2 world=_ShuimianDepthCascade.xy+(input.uv-.5)*_ShuimianDepthCascade.z;
                float2 uv=(world-_ShuimianDepthRect.xy)/_ShuimianDepthRect.zw;
                if(any(uv<0)||any(uv>1))return depth;
                float2 meshData=MeshSample(uv);
                if(meshData.y>.001)
                {
                    float meshHeight=min(meshData.x/max(meshData.y,.0001)+_ShuimianDepthSettings.x,_ShuimianDepthSettings.y);
                    depth.r=max(depth.r,lerp(depth.r,meshHeight,saturate(meshData.y)));
                }
                // A one-texel exterior band gives steep/vertical rock faces a visible contact shoreline.
                float edge=0;
                [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
                {
                    float weight=(x==0?2:1)*(y==0?2:1)/16.0;
                    edge+=MeshSample(uv+float2(x,y)*_ShuimianDepthSettings.w).y*weight;
                }
                float edgeWeight=smoothstep(0,1,saturate(edge*2));
                if(edgeWeight>.001)depth.r=max(depth.r,_ShuimianDepthSettings.y-_ShuimianDepthSettings.z*(1-.35*edgeWeight));
                return depth;
            }
            ENDHLSL
        }
    }
}
