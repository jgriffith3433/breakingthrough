// Three tiling PBR layers (albedo with roughness in alpha, tangent-space normal) blended by a splat mask read in world space:
// the Hollow's grass, worn dirt path and cobble, and the hub office's boards, linoleum and carpet. The mask is one small RGBA
// texture over a world rectangle (R = layer 1, G = layer 2, nothing = layer 0). Tiles are sized in metres per layer so the
// texel density is the same on every layer. Tiling is hidden three ways: a macro brightness/hue layer at about 75 m, a warp
// of the sample position, and (desktop only, _Lite 0) a second sample of each layer at another scale and angle. The vertex
// colour of the ground mesh still tints the result (it carries the rim shade and the rock), hue-preserving.
// A detail normal at 0.6 m adds close-up grain. The mesh has no tangents of its own, so the vertex stage builds them from
// the normal for a ground that faces up.
Shader "Knoll/PbrSplat"
{
    Properties
    {
        _A0 ("Layer 0 albedo (a = roughness)", 2D) = "grey" {}
        _A1 ("Layer 1 albedo (a = roughness)", 2D) = "grey" {}
        _A2 ("Layer 2 albedo (a = roughness)", 2D) = "grey" {}
        _N0 ("Layer 0 normal", 2D) = "bump" {}
        _N1 ("Layer 1 normal", 2D) = "bump" {}
        _N2 ("Layer 2 normal", 2D) = "bump" {}
        _Mask ("Splat mask (RGBA)", 2D) = "black" {}
        _Noise ("Macro and detail noise", 2D) = "grey" {}
        _MaskRect ("Mask rect (x0, z0, 1/width, 1/depth)", Vector) = (0,0,0.01,0.01)
        _Tile ("Metres per tile (layers 0..2)", Vector) = (3,3,1.5,1)
        _Bump ("Normal strength", Range(0,2)) = 1
        _Macro ("Macro variation", Range(0,1)) = 0.5
        _Lite ("Phone tier (1 skips the second samples)", Range(0,1)) = 0
        _Calm ("Calm: lower contrast, no swirl (0 off, 1 full)", Range(0,1)) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _VCol ("Use the vertex colour as the ground shade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows finalcolor:HdrGuard
        #pragma target 3.0

        sampler2D _A0, _A1, _A2, _N0, _N1, _N2, _Mask, _Noise;
        float4 _MaskRect, _Tile;
        half _Bump, _Macro, _Lite, _VCol, _Calm;
        fixed4 _Tint;

        struct Input
        {
            float3 worldPos;
            float4 color : COLOR;
        };

        void vert (inout appdata_full v)
        {
            float3 n = normalize(v.normal);
            v.tangent = float4(normalize(cross(n, float3(0, 0, 1)) + float3(0.0001, 0, 0)), -1);
        }

        float2 Rot(float2 p, float a)
        {
            float s = sin(a), c = cos(a);
            return float2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        // One layer: albedo and roughness at its tile size; the second sample (another scale and angle) hides the repeat.
        fixed4 LayerA(sampler2D t, float2 p, float tile, float mixAmt)
        {
            fixed4 a = tex2D(t, p / tile);
            if (_Calm > 0.5)
            {
                // Calm ground: pull each texel toward the layer's own average so stones and soil stop speckling.
                fixed4 avg = tex2Dlod(t, float4(p / tile, 0, 9));
                a.rgb = lerp(a.rgb, avg.rgb, 0.6 * _Calm);
            }
            else if (_Lite < 0.5)
            {
                fixed4 b = tex2D(t, Rot(p, 0.61) / (tile * 3.1) + 0.37);
                a = lerp(a, b, mixAmt);
            }
            return a;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float4 nzM = tex2D(_Noise, p * 0.0133);          // macro, about 75 m
            float4 nzS = tex2D(_Noise, p * 0.043 + 0.31);    // about 23 m
            p += (nzM.rg - 0.5) * 2.4 * _Macro * (1.0 - 0.85 * _Calm);               // warp: the tile grid never lines up with the eye

            float4 m = tex2D(_Mask, (IN.worldPos.xz - _MaskRect.xy) * _MaskRect.zw);
            float w1 = saturate((m.r + (nzS.a - 0.5) * 0.45 - 0.5) * 5.0);
            float w2 = saturate((m.g + (nzS.b - 0.5) * 0.3 - 0.5) * 6.0);
            float w0 = (1.0 - w1) * (1.0 - w2);
            w1 = w1 * (1.0 - w2);

            float mixAmt = 0.35 + 0.3 * nzM.b;
            fixed4 a0 = LayerA(_A0, p, _Tile.x, mixAmt);
            fixed4 a1 = LayerA(_A1, p, _Tile.y, mixAmt);
            fixed4 a2 = LayerA(_A2, p, _Tile.z, mixAmt);
            fixed4 a = a0 * w0 + a1 * w1 + a2 * w2;

            float3 n0 = tex2D(_N0, p / _Tile.x).xyz * 2 - 1;
            float3 n1 = tex2D(_N1, p / _Tile.y).xyz * 2 - 1;
            float3 n2 = tex2D(_N2, p / _Tile.z).xyz * 2 - 1;
            float3 n = n0 * w0 + n1 * w1 + n2 * w2;
            if (_Lite < 0.5 && _Calm < 0.5)
            {
                float2 dn = tex2D(_Noise, p * 1.7).rg * 2 - 1;
                n.xy += dn * 0.25;
            }
            n.xy *= _Bump * (1.0 - 0.7 * _Calm);
            float nn = dot(n, n);
            o.Normal = nn > 0.0001 ? n * rsqrt(nn) : float3(0, 0, 1);

            // Macro colour: brighter and darker patches, a little warmer and cooler, across the whole field.
            float macro = lerp(1.0 - 0.28 * _Macro * (1.0 - 0.6 * _Calm), 1.0 + 0.18 * _Macro * (1.0 - 0.6 * _Calm), nzM.b);
            float3 col = a.rgb * macro * _Tint.rgb;
            col *= lerp(float3(1.0, 1.0, 1.0), float3(1.06, 1.0, 0.92), (nzM.a - 0.5) * _Macro);

            // The vertex colour is the old ground's shade (rim, rock, worn ring): keep its brightness and a little of its hue.
            float vl = dot(IN.color.rgb, float3(0.299, 0.587, 0.114));
            float3 vh = IN.color.rgb / max(vl, 0.02);
            col *= lerp(float3(1, 1, 1), lerp(float3(1, 1, 1), vh, 0.35) * clamp(lerp(1.0, vl / 0.2, 0.7), 0.35, 1.25), _VCol);

            // Cheap cavity occlusion from the albedo: dark crevices in the painted stones and soil read as depth.
            float lum = dot(col, float3(0.299, 0.587, 0.114));
            o.Occlusion = saturate(lerp(0.55 + lum * 1.6, 0.9, 0.7 * _Calm));
            o.Albedo = col;
            o.Metallic = 0;
            o.Smoothness = saturate(1.0 - a.a);
            o.Alpha = 1;
        }

        void HdrGuard (Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            half3 c = color.rgb;
            color.rgb = half3(c.r >= 0.0 ? min(c.r, 64.0) : 0.0, c.g >= 0.0 ? min(c.g, 64.0) : 0.0, c.b >= 0.0 ? min(c.b, 64.0) : 0.0);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
