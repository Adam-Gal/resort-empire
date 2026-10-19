Shader "ResortEmpire/ConstructionGrid"
{
    Properties
    {
        _LineColor("Grid line", Color) = (1, .94, .76, .72)
        _ShadowColor("Contrast border", Color) = (.16, .25, .12, .42)
        _Width("Line width", Range(.01,.1)) = .035
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
            CBUFFER_START(UnityPerMaterial)
            half4 _LineColor, _ShadowColor;
            float _Width;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float edge=1-abs(input.uv.x*2-1)-abs(input.uv.y*2-1);
                clip(edge);
                float aa=max(fwidth(edge)*.5,.0001);
                float width=max(_Width,aa*1.5);
                half gridLine=1-smoothstep(width-aa,width+aa,edge);
                half border=1-smoothstep(width*2-aa,width*2+aa,edge);
                half4 color=lerp(_ShadowColor,_LineColor,gridLine);
                color.a*=border;
                return color*input.color;
            }
            ENDHLSL
        }
    }
}
