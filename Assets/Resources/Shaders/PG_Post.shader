// PokaGoryu - camcorder / VHS look (in the spirit of "found footage" games): lens distortion, chromatic aberration,
// bloom on bright lights, scanlines, film grain, tracking glitch, vignette and a cold-shadow color grade.
// The scene is rendered into a low-res RenderTexture and shown through this shader on a full-screen quad.
Shader "Pokagoryu/Post"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _Distort ("Lens distortion", Float) = 0.12
        _Chroma ("Chromatic aberration", Float) = 1.0
        _Grain ("Film grain", Float) = 0.07
        _Scan ("Scanlines", Float) = 0.07
        _Vignette ("Vignette", Float) = 2.0
        _Bloom ("Bloom", Float) = 0.7
        _Glitch ("Tracking glitch", Float) = 0.0
        _Brightness ("Brightness", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Distort;
            float _Chroma;
            float _Grain;
            float _Scan;
            float _Vignette;
            float _Bloom;
            float _Glitch;
            float _Brightness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 sceneAt(float2 uv)
            {
                return tex2D(_MainTex, uv).rgb;
            }

            float3 brightPart(float2 uv)
            {
                float3 c = sceneAt(uv);
                float l = dot(c, float3(0.299, 0.587, 0.114));
                return c * saturate((l - 0.45) * 2.2);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                // tracking glitch: horizontal bands jump sideways
                float band = floor(uv.y * 28.0);
                float jump = step(0.72, hash21(float2(band * 1.7, floor(t * 7.0))));
                uv.x += _Glitch * jump * (hash21(float2(band, floor(t * 13.0))) - 0.5) * 0.09;
                uv.x += sin(uv.y * 95.0 + t * 2.0) * 0.0005;

                // lens distortion (barrel)
                float2 c = uv - 0.5;
                float2 d = uv + c * dot(c, c) * _Distort;
                if (d.x < 0.0 || d.x > 1.0 || d.y < 0.0 || d.y > 1.0)
                {
                    return fixed4(0.0, 0.0, 0.0, 1.0);
                }

                // chromatic aberration grows towards the edges
                float2 off = (d - 0.5) * 0.007 * _Chroma;
                float3 col;
                col.r = sceneAt(d + off).r;
                col.g = sceneAt(d).g;
                col.b = sceneAt(d - off).b;

                // bloom: bright pixels bleed into their neighbours
                float2 px = _MainTex_TexelSize.xy;
                float3 b = float3(0.0, 0.0, 0.0);
                b += brightPart(d + px * float2(2.0, 0.0));
                b += brightPart(d + px * float2(-2.0, 0.0));
                b += brightPart(d + px * float2(0.0, 2.0));
                b += brightPart(d + px * float2(0.0, -2.0));
                b += brightPart(d + px * float2(4.0, 4.0));
                b += brightPart(d + px * float2(-4.0, 4.0));
                b += brightPart(d + px * float2(4.0, -4.0));
                b += brightPart(d + px * float2(-4.0, -4.0));
                b += brightPart(d + px * float2(7.0, 0.0));
                b += brightPart(d + px * float2(-7.0, 0.0));
                b += brightPart(d + px * float2(0.0, 7.0));
                b += brightPart(d + px * float2(0.0, -7.0));
                col += b * (_Bloom / 12.0) * 1.8;

                // scanlines and grain
                float scan = 1.0 - _Scan * (0.5 + 0.5 * sin(d.y * _MainTex_TexelSize.w * 6.28318));
                col *= scan;
                float n = hash21(d * _MainTex_TexelSize.zw + floor(t * 24.0));
                col += (n - 0.5) * _Grain;

                // color grade: gloomy - desaturated, deeper shadows, cold sickly tint in the dark
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(float3(lum, lum, lum), col, 0.80);
                col *= _Brightness;
                col = pow(max(col, float3(0.0, 0.0, 0.0)), float3(0.93, 0.93, 0.93));
                col += float3(0.0, 0.008, 0.018) * (1.0 - saturate(lum * 3.0));

                // vignette
                float v = 1.0 - dot(c, c) * _Vignette;
                col *= saturate(v);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
