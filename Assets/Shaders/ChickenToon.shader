Shader "TripoGame/Toon/ChickenToon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Lit Color", Color) = (1, 0.92, 0.62, 1)
        _ShadeColor ("Shade Color", Color) = (0.72, 0.42, 0.22, 1)
        _LightThreshold ("Light Threshold", Range(0, 1)) = 0.46
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.06
        _RimColor ("Rim Color", Color) = (1, 0.82, 0.35, 1)
        _RimPower ("Rim Power", Range(0.25, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                half _LightThreshold;
                half _ShadowSoftness;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half litStep = smoothstep(
                    _LightThreshold - _ShadowSoftness,
                    _LightThreshold + _ShadowSoftness,
                    ndotl);
                litStep *= mainLight.shadowAttenuation;

                half3 toonColor = lerp(_ShadeColor.rgb, _BaseColor.rgb * mainLight.color, litStep);
                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                half3 ambient = SampleSH(normalWS) * 0.25h;

                half3 color = tex.rgb * (toonColor + ambient) + _RimColor.rgb * rim;
                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
