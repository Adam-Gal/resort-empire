Shader "ResortEmpire/DetailedOcean"
{
    Properties
    {
        [PerRendererData] _MainTex("Tile",2D)="white"{}
        _DiamondMask("Diamond",2D)="white"{}
        _WaveSurface("Repeating painterly wave detail",2D)="gray"{}
        _DeepColor("Deep water",Color)=(0.015,0.44,0.57,1)
        _ShallowColor("Shallow water",Color)=(0.23,0.78,0.72,1)
        _Depth("Depth (0 shallow, 1 open sea)",Range(0,1))=1
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
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DiamondMask); SAMPLER(sampler_DiamondMask);
            TEXTURE2D(_WaveSurface); SAMPLER(sampler_WaveSurface);
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor, _ShallowColor;
            half _Depth;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;float3 p=TransformObjectToWorld(input.positionOS);
                output.positionCS=TransformWorldToHClip(p);output.world=p.xy;output.uv=input.uv;return output;
            }
            half3 OceanRipples(float2 world)
            {
                // Periodic C1 crossfade: each raw-image boundary has zero weight.
                // No clamped out-of-bounds sampling and no mirrored baked lighting.
                float2 uv=world/float2(14,9)+float2(.173,.317);
                float2 dx=ddx(uv),dy=ddy(uv);
                float2 a=frac(uv),b=frac(uv+.5);
                float2 w=sin(a*3.14159265);w*=w;
                half3 aa=SAMPLE_TEXTURE2D_GRAD(_WaveSurface,sampler_WaveSurface,a,dx,dy).rgb;
                half3 ba=SAMPLE_TEXTURE2D_GRAD(_WaveSurface,sampler_WaveSurface,float2(b.x,a.y),dx,dy).rgb;
                half3 ab=SAMPLE_TEXTURE2D_GRAD(_WaveSurface,sampler_WaveSurface,float2(a.x,b.y),dx,dy).rgb;
                half3 bb=SAMPLE_TEXTURE2D_GRAD(_WaveSurface,sampler_WaveSurface,b,dx,dy).rgb;
                return lerp(lerp(bb,ab,w.x),lerp(ba,aa,w.x),w.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                clip(1-abs(input.uv.x*2-1)-abs(input.uv.y*2-1)+.00001);
                float2 p=input.world;
                // Uniform open-sea depth; no island-centred gradient.
                float depth=_Depth;
                // Individual water tiles provide the base. A seamless repeating
                // material adds organic waves across cells, rather than cell outlines.
                half3 tile=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).rgb;
                half3 ripples=OceanRipples(p);
                half bright=saturate((max(ripples.r,max(ripples.g,ripples.b))-.65)/.35);
                ripples=lerp(ripples,lerp(ripples,tile,.60),bright*.45);
                half3 sea=lerp(tile,ripples,.87)*lerp(1.03,.88,depth);
                half3 water=lerp(sea,_ShallowColor.rgb,.10*(1-depth));
                // Water cells are full diamonds. Exact clipping above owns coverage;
                // mipmapped sprite alpha must not open gaps at distant zoom levels.
                return half4(water,1);
            }
            ENDHLSL
        }
    }
}
