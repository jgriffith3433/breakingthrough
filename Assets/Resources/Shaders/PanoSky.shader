// A painted equirectangular sky (a Hawkes Gen panorama) used as a chapter's skybox. ChapterSky stays the fallback when
// a map has no panorama in Resources/Skies. _Spin turns it around the vertical axis (degrees); _Tint grades it (the hub's dawn).
Shader "Knoll/PanoSky"
{
    Properties
    {
        _MainTex ("Panorama (2:1)", 2D) = "grey" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Spin ("Spin", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            float _Spin;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float lon = atan2(d.x, d.z) / 6.2831853 + 0.5 + _Spin / 360.0;
                float lat = asin(clamp(d.y, -1.0, 1.0)) / 3.1415927 + 0.5;
                // Below the horizon the horizon row is held, so the painting never shows its own floor.
                lat = max(lat, 0.5);
                fixed4 c = tex2Dlod(_MainTex, float4(frac(lon), lat, 0, 0));
                return fixed4(c.rgb * _Tint.rgb, 1.0);
            }
            ENDCG
        }
    }
}
