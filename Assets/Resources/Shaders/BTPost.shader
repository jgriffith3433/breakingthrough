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
    sampler2D _LutA;     // colour-grading LUTs, a 256x16 strip of 16 slices (FilmLight); _Lut.x = blend A to B, _Lut.y = amount
    sampler2D _LutB;
    float4 _Lut;
    float4 _Tone;        // shoulder start, bloom brightness clamp, dither amplitude, 0
    float _Stakes;       // 0..1 Slice's lost health: the screen's edges drain toward grey

    half3 LutSample(sampler2D lut, half3 c)
    {
        c = saturate(c);
        half b = c.b * 15.0;
        half b0 = floor(b);
        half f = b - b0;
        half b1 = min(b0 + 1.0, 15.0);
        half2 rg = c.rg * (15.0 / 16.0) + (0.5 / 16.0);
        half u0 = (b0 + rg.x) / 16.0;
        half u1 = (b1 + rg.x) / 16.0;
        return lerp(tex2D(lut, half2(u0, rg.y)).rgb, tex2D(lut, half2(u1, rg.y)).rgb, f);
    }

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

    // One NaN or Inf pixel in the HDR frame (a bad normal, a zero-scale mesh) would spread through every bloom level
    // as a black square, because NaN survives the box filter and the additive upsample. Every tap goes through this:
    // a NaN, an Inf or a negative becomes 0 and anything brighter than 64 is held there. The test is written so a
    // NaN fails it (NaN >= 0 is false) instead of relying on min/max, whose NaN result differs between GPUs.
    half SafeChannel(half x)
    {
        return x >= 0.0 ? min(x, 64.0) : 0.0;
    }

    half3 SafeHDR(half3 c)
    {
        return half3(SafeChannel(c.r), SafeChannel(c.g), SafeChannel(c.b));
    }

    half3 Box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = SafeHDR(tex2D(_MainTex, uv + o.xy).rgb) + SafeHDR(tex2D(_MainTex, uv + o.zy).rgb) +
                  SafeHDR(tex2D(_MainTex, uv + o.xw).rgb) + SafeHDR(tex2D(_MainTex, uv + o.zw).rgb);
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        // Hold one pixel's contribution (the HDR sun disc, an additive glow) so a crowd of glows cannot pile up into a wash.
        c = min(c, _Tone.yyy);
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
                half3 c = SafeHDR(tex2D(_SourceTex, i.uv).rgb);
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
                // filmic shoulder: identity below _Tone.x, then an exponential roll-off that approaches 1 and never clips,
                // so highlights keep detail instead of going pure white
                half k = _Tone.x;
                half3 over = max(c - k, 0);
                c = min(c, k) + (1 - k) * (1 - exp(-over / (1 - k)));

                if (_Lut.y > 0.001)
                {
                    half3 graded = lerp(LutSample(_LutA, c), LutSample(_LutB, c), _Lut.x);
                    c = lerp(c, graded, _Lut.y);
                }
                float2 d = i.uv - 0.5;
                half v = 1 - dot(d, d) * _Grade.w;
                c *= saturate(v);
                // stakes: the edges lose colour and darken, the middle stays readable
                half edge = saturate(dot(d, d) * 3.2);
                half drain = _Stakes * edge;
                half g = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(c, g.xxx * (1 - 0.35 * drain), saturate(drain * 1.1));
                // triangular dither (1/255 steps): breaks the banding of the vignette and fog gradients in 8-bit
                float2 sp = i.uv * _ScreenParams.xy;
                float n1 = frac(52.9829189 * frac(dot(sp, float2(0.06711056, 0.00583715))));
                float n2 = frac(52.9829189 * frac(dot(sp + 17.0, float2(0.06711056, 0.00583715))));
                c += (n1 + n2 - 1.0) * _Tone.z;
                return half4(SafeHDR(c), 1);
            }
            ENDCG
        }
    }
}
