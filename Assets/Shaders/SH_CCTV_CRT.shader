Shader "Unrecorded Area/CCTV CRT"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.3)) = 0.08
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.03)) = 0.004
        _TintColor ("Tint Color", Color) = (0.48, 0.68, 0.58, 1)
        _Desaturation ("Desaturation", Range(0, 1)) = 0.25
        _Brightness ("Brightness", Range(0.25, 2)) = 0.9
        _Contrast ("Contrast", Range(0.25, 2)) = 1.1
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.35
        _VignetteSoftness ("Vignette Softness", Range(0.01, 1)) = 0.55
        _ScanlineStrength ("Scanline Strength", Range(0, 0.3)) = 0.08
        _ScanlineCount ("Scanline Count", Range(50, 1200)) = 420
        _NoiseStrength ("Noise Strength", Range(0, 0.3)) = 0.035
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 1.5
        _NoiseTime ("Noise Time", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _DistortionStrength;
            float _ChromaticAberration;
            float4 _TintColor;
            float _Desaturation;
            float _Brightness;
            float _Contrast;
            float _VignetteStrength;
            float _VignetteSoftness;
            float _ScanlineStrength;
            float _ScanlineCount;
            float _NoiseStrength;
            float _NoiseSpeed;
            float _NoiseTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 DistortUV(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radius = dot(centered, centered);
                centered *= 1.0 + radius * _DistortionStrength;
                return centered * 0.5 + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = DistortUV(i.uv);

                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return fixed4(0.0, 0.0, 0.0, 1.0);

                float2 centerDir = uv - 0.5;
                float2 aberrationOffset = centerDir * _ChromaticAberration;

                float red = tex2D(_MainTex, uv + aberrationOffset).r;
                float green = tex2D(_MainTex, uv).g;
                float blue = tex2D(_MainTex, uv - aberrationOffset).b;
                float alpha = tex2D(_MainTex, uv).a;

                float3 color = float3(red, green, blue);

                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                color = lerp(color, luminance.xxx, _Desaturation);
                color *= _TintColor.rgb;
                color = (color - 0.5) * _Contrast + 0.5;
                color *= _Brightness;

                float scanline = sin((uv.y + _NoiseTime * 0.015) * _ScanlineCount) * 0.5 + 0.5;
                color *= 1.0 - (scanline * _ScanlineStrength);

                float noise = hash21(floor(uv * float2(640.0, 360.0)) + _NoiseTime * _NoiseSpeed);
                color += (noise - 0.5) * _NoiseStrength;

                float edgeDistance = distance(i.uv, float2(0.5, 0.5));
                float vignette = smoothstep(_VignetteSoftness, 0.95, edgeDistance);
                color *= 1.0 - vignette * _VignetteStrength;

                return fixed4(saturate(color), alpha) * i.color;
            }
            ENDCG
        }
    }
}
