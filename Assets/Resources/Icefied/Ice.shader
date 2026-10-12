// Buz tabakası: üst yüzeyde kar dokusu + kenar parlaması, yanlarda turkuaz derinlik gradyanı.
Shader "Icefied/Ice"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.94,0.98,1,1)
        _TopColor2 ("Top Variation", Color) = (0.80,0.91,1,1)
        _EdgeColor ("Edge", Color) = (1,1,1,1)
        _SideTop ("Side Top", Color) = (0.60,0.90,1,1)
        _SideBottom ("Side Bottom", Color) = (0.10,0.50,0.78,1)
        _ShadeColor ("Shade", Color) = (0.62,0.74,0.96,1)
        _Flash ("Flash", Range(0,1)) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _TopColor2;
                half4 _EdgeColor;
                half4 _SideTop;
                half4 _SideBottom;
                half4 _ShadeColor;
                half _Flash;
            CBUFFER_END

            TEXTURE2D(_IceMask);
            SAMPLER(sampler_IceMask);
            float4 _BoardRect;   // xy = board min (world xz), zw = board size
            float4 _MaskTexel;   // xy = 1 / mask size
            float _IceTopY;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 MaskAt(float2 uv)
            {
                float2 t = _MaskTexel.xy * 1.5;
                float2 m = SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv).rg * 2;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv + float2(t.x, 0)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv - float2(t.x, 0)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv + float2(0, t.y)).rg;
                m += SAMPLE_TEXTURE2D(_IceMask, sampler_IceMask, uv - float2(0, t.y)).rg;
                return m / 6;
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 col;

                if (n.y > 0.5)
                {
                    float2 uv = (i.positionWS.xz - _BoardRect.xy) / _BoardRect.zw;
                    float2 mm = MaskAt(uv);
                    float m = mm.x;

                    float2 p = i.positionWS.xz;
                    float n = ValueNoise(p * 1.3) * 0.6 + ValueNoise(p * 3.1 + 7.3) * 0.4;
                    col = lerp(_TopColor.rgb, _TopColor2.rgb, smoothstep(0.35, 0.85, n) * 0.7);

                    // diagonal parlama şeritleri
                    float streak = sin((p.x + p.y * 0.6) * 1.7 + ValueNoise(p * 0.8) * 2.0);
                    col += smoothstep(0.82, 1.0, streak) * 0.07;

                    float edge = 1 - smoothstep(0.5, 0.95, m);
                    col = lerp(col, _EdgeColor.rgb, edge * 0.9);

                    // iz etrafında sıcak parıltı (tehlike bölgesi)
                    float pulse = 0.7 + 0.3 * sin(_Time.y * 9);
                    col = lerp(col, half3(1.0, 0.55, 0.3), saturate(mm.y * 2.2) * 0.75 * pulse);

                    float s = Hash(floor(i.positionWS.xz * 14));
                    float twinkle = sin(_Time.y * 3 + s * 40) * 0.5 + 0.5;
                    col += step(0.986, s) * twinkle * 0.6;
                }
                else
                {
                    float t = saturate((i.positionWS.y + 0.2) / (_IceTopY + 0.2));
                    col = lerp(_SideBottom.rgb, _SideTop.rgb, t);
                    float stripe = sin((i.positionWS.x + i.positionWS.z) * 18) * 0.5 + 0.5;
                    col += stripe * 0.05;
                    col = lerp(col, _EdgeColor.rgb, smoothstep(_IceTopY - 0.035, _IceTopY, i.positionWS.y) * 0.85);
                }

                half ndl = dot(n, light.direction);
                half lit = smoothstep(-0.3, 0.6, ndl);
                col *= lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                col += pow(1 - saturate(dot(n, v)), 4) * 0.2;
                col = lerp(col, half3(1, 1, 1), _Flash);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
