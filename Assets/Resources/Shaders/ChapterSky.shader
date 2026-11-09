// A chapter's own sky: a soft gradient (zenith, horizon, below) with one broad disc (moon or low sun) and its halo.
// Drawn as the skybox of a chapter's map scene (OwnMap.cs). No stars, no sparks: the effects rules hold for skies too.
Shader "Knoll/ChapterSky"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.05, 0.07, 0.14, 1)
        _Horizon ("Horizon", Color) = (0.15, 0.2, 0.32, 1)
        _Ground ("Below", Color) = (0.03, 0.04, 0.07, 1)
        _Disc ("Disc", Color) = (0.8, 0.85, 1, 1)
        _DiscDir ("Disc Direction", Vector) = (0.3, 0.5, 0.8, 0)
        _DiscSize ("Disc Size", Range(0.001, 0.2)) = 0.02
        _Haze ("Haze Height", Range(0.1, 1)) = 0.45
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

            fixed4 _Top;
            fixed4 _Horizon;
            fixed4 _Ground;
            fixed4 _Disc;
            float4 _DiscDir;
            float _DiscSize;
            float _Haze;

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
                float h = d.y;
                float3 c = h >= 0.0
                    ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(h / max(_Haze, 0.001)), 0.6))
                    : lerp(_Horizon.rgb, _Ground.rgb, saturate(-h * 4.0));
                float s = saturate(dot(d, normalize(_DiscDir.xyz)));
                float disc = smoothstep(1.0 - _DiscSize, 1.0 - _DiscSize * 0.55, s);
                float halo = pow(s, 28.0) * 0.28;
                c += _Disc.rgb * (disc + halo);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
