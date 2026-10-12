// Partikül, gölge ve şeffaf efektler. _Shape: 0 yumuşak daire, 1 kare, 2 gölge lekesi, 3 düz renk.
Shader "Icefied/Fx"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Shape ("Shape", Float) = 0
        _UseVertexColor ("Use Vertex Color", Float) = 1
        _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Shape;
                half _UseVertexColor;
                half _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = _BaseColor * lerp(half4(1, 1, 1, 1), i.color, _UseVertexColor);
                float r = length(i.uv - 0.5) * 2;
                if (_Shape < 0.5)       c.a *= 1 - smoothstep(0.45, 1.0, r);
                else if (_Shape < 1.5)  c.a *= 1;
                else if (_Shape < 2.5)  c.a *= 1 - smoothstep(0.15, 1.0, r);
                return c;
            }
            ENDHLSL
        }
    }
}
