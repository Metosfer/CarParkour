Shader "CarParkour/URP/ToonUnlitCar"
{
    Properties
    {
        [HDR]_Color("Albedo", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}

        // Toon controls (light impact kept subtle to preserve original colors)
        _Threshold1 ("Shade Threshold 1", Range(0.0,1.0)) = 0.35
        _Threshold2 ("Shade Threshold 2", Range(0.0,1.0)) = 0.75
        _Smoothness ("Band Smoothness", Range(0.0,0.2)) = 0.05
        _DarkFactor ("Shadow Dark Factor", Range(0.5,1.0)) = 0.85

        [Header(Stencil)]
        _Stencil ("Stencil ID [0;255]", Float) = 0
        _ReadMask ("ReadMask [0;255]", Int) = 255
        _WriteMask ("WriteMask [0;255]", Int) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Int) = 0

        [Header(Rendering)]
        _Offset("Offset", float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Int) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Int) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 4
        [Enum(None,0,Alpha,1,Red,8,Green,4,Blue,2,RGB,14,RGBA,15)] _ColorMask("Color Mask", Int) = 15
    }

    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Stencil
        {
            Ref [_Stencil]
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }

        Pass
        {
            Name "UniversalForward"
            Tags{ "LightMode" = "UniversalForward" }
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
                float _Threshold1;
                float _Threshold2;
                float _Smoothness;
                float _DarkFactor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            half3 ApplyToon(half3 baseCol, float3 positionWS, float3 normalWS)
            {
                // Main light
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(normalize(normalWS), normalize(mainLight.direction)));
                // Shadowing reduce
                NdotL *= mainLight.shadowAttenuation;

                // Two-band toon with slight smoothing to preserve original color
                float t1 = _Threshold1;
                float t2 = _Threshold2;
                float s = _Smoothness;
                float band1 = smoothstep(t1 - s, t1 + s, NdotL);
                float band2 = smoothstep(t2 - s, t2 + s, NdotL);
                // 0..1 where 0=shadow, 1=lit (two steps)
                float ramp = 0.0;
                ramp = lerp(_DarkFactor, 0.93, band1);   // shadow -> mid
                ramp = lerp(ramp, 1.0, band2);           // mid -> lit

                // Keep color close to texture, modulate gently by light color intensity
                half lightInt = saturate(Luminance(mainLight.color));
                half lightBoost = lerp(0.95, 1.05, lightInt);
                return baseCol * (ramp * lightBoost);
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                half3 toon = ApplyToon(albedo.rgb, i.positionWS, i.normalWS);
                return half4(toon, albedo.a);
            }

            ENDHLSL
        }

        // Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags{ "LightMode" = "ShadowCaster" }
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
