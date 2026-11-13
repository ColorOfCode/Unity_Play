Shader "Hidden/Snowfield/Soften"
{
    Properties { _MainTex("Input",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _SnowBlurStep;
            float4 Frag(v2f_img i):SV_TARGET
            {
                float4 sum=0;float height=0,heightWeight=0;
                [unroll]for(int j=-3;j<=3;j++)
                {
                    int k=abs(j);float weight=k==0?.28:k==1?.22:k==2?.105:.035;
                    float4 s=tex2D(_MainTex,i.uv+_SnowBlurStep.xy*j);
                    sum+=s*weight;
                    float covered=step(.001,s.b)*weight;
                    height+=s.b*covered;heightWeight+=covered;
                }
                // Height is metadata, not a mask: empty background must not
                // pull its value toward zero and create a new cliff at the edge.
                sum.b=heightWeight>0?height/heightWeight:0;return sum;
            }
            ENDCG
        }
    }
}
