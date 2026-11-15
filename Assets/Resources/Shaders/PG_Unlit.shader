// PokaGoryu - self-lit shader: glow of the hero body, lamp bulbs, halos, light cones, 2D enemy sprites
// (billboards), the beam and the horizon. Vertex color is multiplied in (used by light cones).
// Blend mode is set from code (Gfx.Unlit): opaque / alpha / additive.
// Fog is custom (_PG_Fog); _FogAmount = 0 disables fading (beacons, horizon).
// _EdgeFade > 0 softens the silhouette of a translucent volume (light cone): alpha falls off where the
// surface is seen edge-on, so the cone looks like light in fog instead of a flat triangle.
Shader "Pokagoryu/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _FogAmount ("Fog Amount", Range(0,1)) = 1
        _EdgeFade ("Edge Fade", Range(0,1)) = 0
        _AlphaOne ("Alpha One", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _FogAmount;
            float _EdgeFade;
            float _AlphaOne;
            float _PG_Fog;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float dist : TEXCOORD1;
                float ndv : TEXCOORD2;
                fixed4 vcol : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dist = distance(wp, _WorldSpaceCameraPos);
                o.vcol = v.color;
                o.ndv = 1.0;
                // нормаль нужна только мягким объёмам (у спрайтов и меша без нормалей ветка не выполняется)
                if (_EdgeFade > 0.001)
                {
                    float3 wn = UnityObjectToWorldNormal(v.normal);
                    float3 vd = normalize(_WorldSpaceCameraPos - wp);
                    o.ndv = abs(dot(wn, vd));
                }
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color * i.vcol;
                if (_EdgeFade > 0.001)
                {
                    float e = saturate(i.ndv * 1.6);
                    c.a *= lerp(1.0, e * e, _EdgeFade);
                }
                // _AlphaOne = 1: картинка из RenderTexture (полоска здоровья) складывается с кадром по цвету, альфа не нужна
                c.a = lerp(c.a, 1.0, _AlphaOne);
                float f = exp(-i.dist * _PG_Fog * _FogAmount);
                c.rgb *= f;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
