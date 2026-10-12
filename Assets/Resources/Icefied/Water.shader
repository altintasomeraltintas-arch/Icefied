// Stilize okyanus: buz kenarında köpük, kesim izinde parlayan çatlak, hareketli ışık desenleri.
Shader "Icefied/Water"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.03,0.42,0.70,1)
        _ShallowColor ("Shallow", Color) = (0.20,0.82,0.92,1)
        _FoamColor ("Foam", Color) = (1,1,1,1)
        _TrailColor ("Trail", Color) = (1.4,0.55,0.25,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half4 _TrailColor;
            CBUFFER_END

            TEXTURE2D(_IceMask);
            SAMPLER(sampler_IceMask);
            float4 _BoardRect;
            float4 _MaskTexel;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            float2 MaskAt(float2 uv)
            {
                float2 t = _MaskTexel.xy * 1.6;
                float2 m = SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv).rg * 2;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv + float2(t.x, 0)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv - float2(t.x, 0)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv + float2(0, t.y)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv - float2(0, t.y)).rg;
                return m / 6;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float t = _Time.y;

                float2 wob = float2(sin(p.y * 3.1 + t * 1.7), cos(p.x * 2.9 + t * 1.3)) * 0.035;
                float2 uv = (p + wob - _BoardRect.xy) / _BoardRect.zw;
                float2 m = MaskAt(uv);
                float ice = m.x;
                float trail = SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, (p - _BoardRect.xy) / _BoardRect.zw).g;

                float2 center = _BoardRect.xy + _BoardRect.zw * 0.5;
                float far = saturate(length(p - center) / 14);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(0.35 + far * 0.9 - ice * 1.5));

                float c = sin(p.x * 3.7 + t * 1.1 + sin(p.y * 2.6 + t)) * sin(p.y * 4.1 - t * 0.9 + sin(p.x * 2.3 - t * 0.7));
                col += pow(saturate(c), 6) * 0.13;

                float ripple = sin(p.x * 14 + t * 2.4) * sin(p.y * 12 - t * 2.0) * 0.035;
                float foam = smoothstep(0.05, 0.2, ice + ripple);
                col = lerp(col, _FoamColor.rgb, foam * 0.92);

                float pulse = 0.75 + 0.25 * sin(t * 9);
                col = lerp(col, _TrailColor.rgb, saturate(max(trail, m.y * 1.4) * 1.8) * pulse);

                col *= lerp(1.0, 0.82, far);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
