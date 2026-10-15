// Cartoon shading for every lit piece of the game: a crisp light/shade split with soft cool-violet shadows,
// a glossy candy highlight, a thin bright rim, and a bold ink outline (an inverted hull pushed out along
// smoothed normals stored in TEXCOORD3, so hard-edged meshes still get an unbroken outline).
Shader "OutOfOffice/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Texture", 2D) = "white" {}
        _Smoothness ("Gloss", Range(0, 1)) = 0.5
        _Glow ("Self glow", Float) = 0
        _Outline ("Outline width", Float) = 0.022
        _ShadeColor ("Shade tint", Color) = (0.8, 0.8, 0.87, 1)
        _PatchColor ("Face patch color", Color) = (1, 1, 1, 1)
        _Patch ("Face patch strength", Float) = 0
        _FaceDirWS ("Face direction (world)", Vector) = (0, 0, -1, 0)
        _BendWS ("Unused bend", Vector) = (0, 0, 0, 0)
        _ShadeSoftness ("Shade edge softness", Float) = 0.08
        _ShadowWeight ("Cast shadow weight", Float) = 1
        _InkMix ("Outline tinted by the surface", Float) = 0
        _ViewLight ("Key light bent toward the viewer", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor, _BaseMap_ST, _ShadeColor, _PatchColor, _FaceDirWS, _BendWS;
        float _Smoothness, _Glow, _Outline, _Patch, _ShadeSoftness, _ShadowWeight, _InkMix, _ViewLight;
    CBUFFER_END
    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    struct Attributes
    {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
        float3 smoothNormal : TEXCOORD3;
    };

    float3 BaseColor(float2 uv)
    {
        return _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Toon"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 base = BaseColor(i.uv);
                // A lighter muzzle where the face sits (the mascot sets the direction every frame).
                float patch = _Patch * smoothstep(0.55, 0.8, dot(N, normalize(_FaceDirWS.xyz)));
                base = lerp(base, _PatchColor.rgb, patch);

                // Two tones with a soft edge; cast shadows join the shade side.
                float3 L = normalize(light.direction + V * _ViewLight);
                float ndl = dot(N, L);
                float shadow = lerp(1.0, lerp(0.35, 1.0, light.shadowAttenuation), _ShadowWeight);
                float lit = smoothstep(-_ShadeSoftness * 0.5, 0.04 + _ShadeSoftness, ndl) * shadow;
                float3 shade = base * _ShadeColor.rgb;
                float3 tone = lerp(shade, base, lit);
                float3 ambient = SampleSH(N);
                float3 c = tone * (light.color * 0.78 + ambient * 0.42) + base * _Glow;

                // Candy highlight: a small, crisp blob that grows with gloss.
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), exp2(4.0 + 6.0 * _Smoothness));
                c += smoothstep(0.45, 0.55, spec) * _Smoothness * 0.45 * lit * light.color;
                // Thin bright rim on the lit side, for a sticker-like pop.
                float rim = smoothstep(0.7, 0.78, 1.0 - saturate(dot(N, V))) * lit;
                c += rim * 0.18 * (base + 0.3);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 n = dot(v.smoothNormal, v.smoothNormal) > 0.01 ? v.smoothNormal : float3(0, 0, 0);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(n);
                p += (dot(n, n) > 0.01 ? normalize(nw) : 0) * _Outline;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                if (_Outline <= 0.0001) discard;
                float3 base = BaseColor(i.uv);
                // Deep ink, tinted by the surface color (linear values: these read near-black on screen).
                float3 ink = float3(0.022, 0.012, 0.04);
                return half4(lerp(lerp(base * 0.07, ink, 0.55), base * 0.3, _InkMix), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dir = normalize(_LightPosition - p);
                #else
                    float3 dir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, dir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex NormalVert
            #pragma fragment NormalFrag
            struct VaryingsN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            VaryingsN NormalVert(Attributes v)
            {
                VaryingsN o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 NormalFrag(VaryingsN i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
