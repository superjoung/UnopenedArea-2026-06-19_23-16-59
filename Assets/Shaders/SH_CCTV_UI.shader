Shader "Unrecorded Area/CCTV UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TintColor ("CCTV Tint", Color) = (0.48, 0.68, 0.58, 1)
        _Desaturation ("Desaturation", Range(0, 1)) = 0.2
        _Brightness ("Brightness", Range(0.25, 2)) = 1
        _ScanlineStrength ("Scanline Strength", Range(0, 0.4)) = 0.08
        _ScanlineCount ("Scanline Count", Range(50, 1200)) = 420
        _NoiseStrength ("Noise Strength", Range(0, 0.3)) = 0.04
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 1.5
        _FlickerStrength ("Flicker Strength", Range(0, 0.5)) = 0.04
        _GlitchStrength ("Glitch Strength", Range(0, 0.08)) = 0.01
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.02)) = 0.001
        _EffectIntensity ("Effect Intensity", Range(0, 2)) = 1
        _NoiseTime ("Noise Time", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _TintColor;
            float _Desaturation;
            float _Brightness;
            float _ScanlineStrength;
            float _ScanlineCount;
            float _NoiseStrength;
            float _NoiseSpeed;
            float _FlickerStrength;
            float _GlitchStrength;
            float _ChromaticAberration;
            float _EffectIntensity;
            float _NoiseTime;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 SampleUi(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float intensity = max(_EffectIntensity, 0.0);
                float band = step(0.92, frac(i.texcoord.y * 6.0 + _NoiseTime * 0.7));
                float glitchNoise = hash21(float2(floor(i.texcoord.y * 24.0), floor(_NoiseTime * 12.0)));
                float glitchOffset = (glitchNoise - 0.5) * _GlitchStrength * band * intensity;

                float2 uv = i.texcoord + float2(glitchOffset, 0.0);
                float2 chromaOffset = float2(_ChromaticAberration * intensity, 0.0);

                fixed4 center = SampleUi(uv);
                float red = SampleUi(uv + chromaOffset).r;
                float blue = SampleUi(uv - chromaOffset).b;
                fixed4 color = fixed4(red, center.g, blue, center.a) * i.color;

                float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, luminance.xxx, _Desaturation * intensity);
                color.rgb *= lerp(float3(1.0, 1.0, 1.0), _TintColor.rgb, saturate(intensity));
                color.rgb *= _Brightness;

                float scanline = sin((i.texcoord.y + _NoiseTime * 0.015) * _ScanlineCount) * 0.5 + 0.5;
                color.rgb *= 1.0 - scanline * _ScanlineStrength * intensity;

                float noise = hash21(floor(i.texcoord * float2(480.0, 270.0)) + _NoiseTime * _NoiseSpeed);
                color.rgb += (noise - 0.5) * _NoiseStrength * intensity;

                float flicker = sin(_NoiseTime * 19.0) * 0.5 + 0.5;
                color.a *= 1.0 - flicker * _FlickerStrength * intensity;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return saturate(color);
            }
            ENDCG
        }
    }
}

