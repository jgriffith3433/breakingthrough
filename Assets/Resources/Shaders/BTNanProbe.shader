// Playtest only (PlaytestHud.cs): turns a raw HDR frame into a flag image that can be read back as plain 8-bit colour,
// so a browser run never has to read a half-float target. Red is 1 where a pixel is NaN or Inf, green is 1 where it is
// finite but brighter than 64 (BTPost.shader holds bloom taps at 64). NaN fails every comparison, so "not below the largest
// finite number" is true for NaN and Inf alike; this is written like BTPost's SafeChannel and does not rely on isnan().
Shader "Hidden/BT/NanProbe"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZTest Always ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            half4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                bool finite = abs(c.r) < 3.0e38 && abs(c.g) < 3.0e38 && abs(c.b) < 3.0e38;
                bool hot = finite && (c.r > 64.0 || c.g > 64.0 || c.b > 64.0);
                return half4(finite ? 0.0 : 1.0, hot ? 1.0 : 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
}
