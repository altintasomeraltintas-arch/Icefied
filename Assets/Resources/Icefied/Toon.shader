// Karakterler ve objeler için yumuşak, parlak "mobil premium" toon ışıklandırma.
Shader "Icefied/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Color", Color) = (0.58,0.64,0.88,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _Gloss ("Gloss", Range(0,1)) = 0.35
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
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
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _RimColor;
                half _RimStrength;
                half _Gloss;
                half _Flash;
                half4 _FlashColor;
            CBUFFER_END

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

            half4 frag(Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                half ndl = dot(n, light.direction);
                half lit = smoothstep(-0.25, 0.6, ndl);
                half3 col = _BaseColor.rgb * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
                col += _BaseColor.rgb * 0.10 * saturate(n.y);

                float3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), 40) * _Gloss;
                col += spec;

                half rim = pow(1 - saturate(dot(n, v)), 3) * _RimStrength;
                col += rim * _RimColor.rgb;

                col = lerp(col, _FlashColor.rgb, _Flash);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
