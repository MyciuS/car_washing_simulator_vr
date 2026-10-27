// See-through layer over a floor that shows where it is wet or soapy (used by WetGround).
// _WetMap: red = how wet (darker, with sharp glossy reflections), green = soap foam (white, a bit shiny).
// Dry spots draw nothing. The reflections are kept at full strength, so wet concrete shines like real water.
Shader "Custom/Wet Ground"
{
    Properties
    {
        [NoScaleOffset] _WetMap("Wet Map (set by WetGround)", 2D) = "black" {}
        _WetDarkness("How dark water makes the floor", Range(0, 1)) = 0.55
        _WetSmoothness("How glossy water is", Range(0, 1)) = 0.85
        _Reflections("How strongly water reflects the sky", Range(0, 1)) = 0.35
        _FoamColor("Foam Color", Color) = (0.95, 0.96, 0.98, 1)
        _FoamOpacity("Foam Opacity", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // Premultiplied alpha: the floor shows through, but reflections are added at full strength.
            Blend One OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define _ALPHAPREMULTIPLY_ON 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WetMap);
            SAMPLER(sampler_WetMap);

            CBUFFER_START(UnityPerMaterial)
                half _WetDarkness;
                half _WetSmoothness;
                half _Reflections;
                half4 _FoamColor;
                half _FoamOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half2 wetAndFoam = SAMPLE_TEXTURE2D(_WetMap, sampler_WetMap, input.uv).rg;
                half wet = wetAndFoam.r;
                half foam = saturate(wetAndFoam.g * 2.0h);
                // Uneven, bubbly foam: every 3 cm cell of floor gets its own random amount.
                float2 cell = floor(input.positionWS.xz * 33.0);
                half bubbles = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                foam *= lerp(0.55h, 1.0h, bubbles);
                // Fade the whole effect (reflections too) in with the amount of water, so the edges of
                // a wet patch are soft instead of a bright ring, and dry floor draws nothing.
                half shown = saturate(wet + foam);
                clip(shown - 0.002h);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _FoamColor.rgb * foam;            // black water darkens, white foam lightens
                surface.smoothness = lerp(_WetSmoothness, 0.35h, foam);
                surface.occlusion = 1;
                surface.alpha = saturate(wet * _WetDarkness + foam * _FoamOpacity);
                surface.normalTS = half3(0, 0, 1);

                // Lit result = floor darkening/foam (scaled by alpha) + reflections (full strength).
                half4 lit = UniversalFragmentPBR(inputData, surface);
                // The same light with a black surface is only the reflections. Scale them by the
                // amount of water too, so thin water reflects as little as it darkens, and by
                // _Reflections: under a roof the floor mostly mirrors the dim ceiling, not the bright sky.
                SurfaceData reflectionOnly = surface;
                reflectionOnly.albedo = half3(0, 0, 0);
                half3 reflections = UniversalFragmentPBR(inputData, reflectionOnly).rgb;
                half3 color = (lit.rgb - reflections) + reflections * shown * _Reflections;
                // Fog too only as strong as the layer itself (the floor under it already has its own fog).
                color = MixFogColor(color, unity_FogColor.rgb * surface.alpha, inputData.fogCoord);
                return half4(color, surface.alpha);
            }
            ENDHLSL
        }
    }
}
