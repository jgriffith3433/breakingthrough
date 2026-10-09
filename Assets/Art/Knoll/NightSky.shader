Shader "Knoll/NightSky"
{
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

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 direction = normalize(i.dir);
                float height = saturate(direction.y);
                float3 horizon = float3(0.09, 0.10, 0.18);
                float3 zenith = float3(0.012, 0.018, 0.05);
                float3 color = lerp(horizon, zenith, pow(height, 0.55));

                float3 cell = floor(direction * 220.0);
                float noise = Hash(cell);
                float star = step(0.993, noise) * saturate(direction.y * 3.0);
                color += star * (0.7 + noise);

                float3 moonDirection = normalize(float3(0.45, 0.62, 0.55));
                float moon = smoothstep(0.9976, 0.9992, dot(direction, moonDirection));
                color += moon * float3(0.95, 0.96, 0.82);
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
