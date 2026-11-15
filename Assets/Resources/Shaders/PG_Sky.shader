// PokaGoryu - gloomy night sky dome: vertical gradient, heavy drifting clouds that swallow most of the stars,
// a dim moon and a warm dawn glow over the exit of the city.
// Draw it on a big sphere that follows the camera (Cull Front: we look at the inside faces).
Shader "Pokagoryu/Sky"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Cull Front
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _PG_SkyZenith;
            float4 _PG_SkyHorizon;
            float4 _PG_SkyGlow;
            float4 _PG_GlowDir;     // xyz = direction of the dawn glow
            float4 _PG_MoonDir;     // xyz = direction to the moon

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    s += a * vnoise(p);
                    p = p * 2.03 + 11.7;
                    a *= 0.5;
                }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                float3 col = lerp(_PG_SkyHorizon.rgb, _PG_SkyZenith.rgb, pow(up, 0.45));
                if (d.y < 0.0)
                {
                    col = _PG_SkyHorizon.rgb * 0.6;
                }

                // heavy overcast: dark clouds, slowly drifting
                float2 cuv = d.xz / (abs(d.y) + 0.45) * 1.6 + float2(_Time.y * 0.006, _Time.y * 0.002);
                float cover = smoothstep(0.36, 0.70, fbm(cuv));
                float cloudMask = cover * saturate(d.y * 5.0 + 0.2);
                col = lerp(col, col * 0.30 + float3(0.010, 0.011, 0.017), cloudMask);

                // dawn glow over the exit of the city (shines through the haze)
                float g = saturate(dot(normalize(float3(d.x, 0.0, d.z)), _PG_GlowDir.xyz));
                float horizon = 1.0 - saturate(d.y * 3.2);
                col += _PG_SkyGlow.rgb * pow(g, 5.0) * horizon * horizon;

                // a few stars in the gaps between the clouds
                float3 q = floor(d * 140.0);
                float h = hash31(q);
                float star = step(0.9978, h) * saturate(d.y * 2.5) * (1.0 - cover);
                col += float3(0.9, 0.95, 1.0) * star * (0.25 + 0.55 * hash31(q + 7.0));

                // dim moon behind the clouds
                float m = dot(d, normalize(_PG_MoonDir.xyz));
                float disk = smoothstep(0.9993, 0.9997, m);
                float halo = pow(saturate(m), 220.0) * 0.25;
                col += float3(0.70, 0.76, 0.90) * (disk * 0.45 + halo) * (1.0 - 0.8 * cover);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
