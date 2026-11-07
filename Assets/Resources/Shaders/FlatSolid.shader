// Opaque flat colour with a little sun and ambient light, for solid things built in code (level blocks, props held by bots).
// Sprites/Default is transparent with ZWrite off, so solids drawn with it sort by distance and paint over each other.
Shader "Knoll/FlatSolid"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite On
        ZTest LEqual
        Cull Back
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed3 light : COLOR0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float ndl = saturate(dot(n, _WorldSpaceLightPos0.xyz));
                o.light = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * ndl;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = fixed4(_Color.rgb * saturate(i.light + 0.25), 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
