Shader "ResortEmpire/TerrainSeamless"
{
    Properties
    {
        [PerRendererData] _MainTex("Tile",2D)="white"{}
        _DiamondMask("Shared diamond coverage",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _GrassSurface("Continuous grass detail",2D)="gray"{}
        _DetailStrength("Grass detail strength",Range(0,1))=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DiamondMask); SAMPLER(sampler_DiamondMask);
            TEXTURE2D(_GrassSurface); SAMPLER(sampler_GrassSurface);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _DetailStrength;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; output.color=input.color*_Color;
                output.world=TransformObjectToWorld(input.positionOS).xy;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Clip to the exact diamond. Shared edges meet without overlapping AA coverage.
                float edge=1-abs(input.uv.x*2-1)-abs(input.uv.y*2-1);
                clip(edge+.00001);
                half4 tile=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                half coverage=SAMPLE_TEXTURE2D(_DiamondMask,sampler_DiamondMask,input.uv).a;
                // Remove only the common rasterized diamond coverage, retaining transition alpha.
                tile.a=saturate(tile.a/max(coverage,.001));
                float2 ground=float2(input.world.x+2*input.world.y,-input.world.x+2*input.world.y);
                half3 grass=SAMPLE_TEXTURE2D(_GrassSurface,sampler_GrassSurface,ground/5.5).rgb;
                half detail=dot(grass,half3(.299,.587,.114));
                tile.rgb*=1+(detail-.45)*1.2*_DetailStrength;
                return tile*input.color;
            }
            ENDHLSL
        }
    }
}
