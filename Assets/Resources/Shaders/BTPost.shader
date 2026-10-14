// Bloom, color grade and vignette for the built-in pipeline. Driven by NightGrade.cs.
Shader "Hidden/BT/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _SourceTex;
    float4 _Filter;      // threshold, threshold - knee, 2 * knee, 0.25 / knee
    float _Intensity;
    float4 _Grade;       // exposure, contrast, saturation, vignette
    float4 _Shadows;     // tint for darks
    float4 _Highlights;  // tint for lights
    float4 _Lift;        // added to darks

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                  tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half soft = brightness - _Filter.y;
        soft = clamp(soft, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half contribution = max(soft, brightness - _Filter.x);
        contribution /= max(brightness, 0.00001);
        return c * contribution;
    }
    ENDCG

    SubShader
    {
        Cull Off ZTest Always ZWrite Off

        Pass // 0 prefilter + first downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
            ENDCG
        }

        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG
        }

        Pass // 2 upsample, added onto the larger level
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG
        }

        Pass // 3 combine and grade
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_SourceTex, i.uv).rgb;
                half3 bloom = Box(i.uv, 0.5) * _Intensity;
                c += bloom;

                c *= _Grade.x;
                half luma = dot(c, half3(0.299, 0.587, 0.114));
                // split tone: cool shadows, warm lights
                half t = saturate(luma * 1.4);
                c *= lerp(_Shadows.rgb, _Highlights.rgb, t);
                c += _Lift.rgb * (1 - t);
                // saturation and contrast around mid grey
                luma = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Grade.z);
                c = (c - 0.5) * _Grade.y + 0.5;
                // gentle shoulder so bright bloom rolls off instead of clipping hard
                c = c / (1 + max(c - 0.85, 0) * 0.6);

                float2 d = i.uv - 0.5;
                half v = 1 - dot(d, d) * _Grade.w;
                c *= saturate(v);
                return half4(max(c, 0), 1);
            }
            ENDCG
        }
    }
}
