// PokaGoryu - world shader (buildings, ground, props, hero body, people).
//
// Lighting is computed from a global array of light sources (_PG_Lights) that LightField.PushToShaders()
// fills every frame. The game logic uses the very same set (LightField.Sample), so "what looks lit" and
// "what counts as light" always match. On top of that there is a cheap hemisphere "sky + moon" ambient,
// wet-asphalt specular highlights and emissive windows (texture alpha = emission mask).
//
// _WorldTile > 0 switches to triplanar world-space UV (windows stay the same size on every wall).
// No keywords and no built-in Unity fog: nothing gets stripped from builds.
Shader "Pokagoryu/World"
{
    Properties
    {
        _MainTex ("Albedo (rgb), emission mask (a)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WorldTile ("World tile in meters (0 = mesh UV)", Float) = 0
        _Emission ("Emission strength", Float) = 0
        _Gloss ("Gloss", Float) = 0
        _RoofFlat ("Flat roofs", Float) = 0
        _GlossAlpha ("Gloss from texture alpha (wet asphalt)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _WorldTile;
            float _Emission;
            float _Gloss;
            float _RoofFlat;
            float _GlossAlpha;

            float4 _PG_Lights[16];       // xyz = position, w = radius
            float4 _PG_LightColors[16];  // rgb = color * intensity
            float4 _PG_AmbSky;
            float4 _PG_AmbGround;
            float4 _PG_MoonDir;
            float4 _PG_MoonColor;
            float4 _PG_FogColor;
            float _PG_Fog;
            float _PG_Exposure;          // overall light brightness (visuals only, gameplay is not affected)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wnrm : TEXCOORD2;
                fixed4 vcol : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                o.vcol = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wnrm);
                float3 an = abs(n);
                float2 uv = i.uv;
                bool roof = false;
                if (_WorldTile > 0.0)
                {
                    if (an.y > 0.6)
                    {
                        uv = i.wpos.xz / _WorldTile;
                        roof = true;
                    }
                    else if (an.x > an.z)
                    {
                        uv = float2(i.wpos.z, i.wpos.y) / _WorldTile;
                    }
                    else
                    {
                        uv = float2(i.wpos.x, i.wpos.y) / _WorldTile;
                    }
                }

                float4 tex = tex2D(_MainTex, uv);
                float3 albedo = tex.rgb * _Color.rgb * i.vcol.rgb;
                float emissive = tex.a * _Emission;
                if (roof && _RoofFlat > 0.5)
                {
                    albedo = _Color.rgb * i.vcol.rgb * 0.30;
                    emissive = 0.0;
                }
                // walls: grime near the ground (rain splash, soot), fades out about 3 m up
                if (_WorldTile > 0.0 && !roof)
                {
                    albedo *= lerp(0.58, 1.0, saturate(i.wpos.y * 0.33));
                }

                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 sum = float3(0.0, 0.0, 0.0);
                float3 spec = float3(0.0, 0.0, 0.0);

                for (int k = 0; k < 16; k++)
                {
                    float3 toL = _PG_Lights[k].xyz - i.wpos;
                    float d = length(toL);
                    float r = max(_PG_Lights[k].w, 0.001);
                    float att = saturate(1.0 - d / r);
                    att = att * att;
                    float3 L = toL / max(d, 0.0001);
                    float ndl = saturate(dot(n, L) * 0.65 + 0.35);
                    float3 lc = _PG_LightColors[k].rgb;
                    sum += lc * (att * ndl);
                    float3 Hv = normalize(L + V);
                    float sp = pow(saturate(dot(n, Hv)), 36.0);
                    spec += lc * (att * sp);
                }

                float3 hemi = lerp(_PG_AmbGround.rgb, _PG_AmbSky.rgb, n.y * 0.5 + 0.5);
                float3 moon = _PG_MoonColor.rgb * saturate(dot(n, _PG_MoonDir.xyz));
                float3 lit = hemi + moon + sum * _PG_Exposure;
                // wet asphalt: gloss comes from the texture alpha (puddles shine, dry patches do not)
                float glossK = lerp(1.0, tex.a, step(0.5, _GlossAlpha));
                float3 col = albedo * lit + spec * (_Gloss * glossK * _PG_Exposure) + tex.rgb * emissive;

                float dist = distance(i.wpos, _WorldSpaceCameraPos);
                float fog = exp(-dist * _PG_Fog);
                col = lerp(_PG_FogColor.rgb, col, fog);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
