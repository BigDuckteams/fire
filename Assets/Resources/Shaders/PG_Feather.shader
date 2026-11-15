// PokaGoryu - shader of the "Feathers" health bar: flat shaded feathers lit by two fake lights
// (a directional one and a warm point light, both set from code through _FB_* globals) plus emission.
// The bar is rendered by its own camera into a small RenderTexture, so nothing here touches the game world.
Shader "Pokagoryu/Feather"
{
    Properties
    {
        _Color ("Albedo", Color) = (1,1,1,1)
        _Emit ("Emission", Color) = (0,0,0,1)
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
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _Emit;
            float4 _FB_DirToLight;   // xyz = unit vector towards the directional light
            float4 _FB_DirColor;     // rgb = color * intensity
            float4 _FB_PointPos;     // xyz = world position, w = range
            float4 _FB_PointColor;   // rgb = color * intensity
            float4 _FB_Ambient;      // rgb

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wnrm : TEXCOORD0;
                float3 wpos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wnrm);
                float ndl = saturate(dot(n, _FB_DirToLight.xyz));
                float3 toP = _FB_PointPos.xyz - i.wpos;
                float d = length(toP);
                float att = saturate(1.0 - d / max(_FB_PointPos.w, 0.001));
                att = att * att;
                float ndp = saturate(dot(n, toP / max(d, 0.0001)));
                float3 lit = _FB_Ambient.rgb + _FB_DirColor.rgb * ndl + _FB_PointColor.rgb * (att * ndp);
                float3 col = _Color.rgb * lit + _Emit.rgb;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
