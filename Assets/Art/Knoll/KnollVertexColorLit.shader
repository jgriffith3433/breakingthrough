Shader "Knoll/VertexColorLit"
{
    Properties
    {
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.18
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _RimColor ("Moon Rim", Color) = (0.42, 0.55, 0.95, 1)
        _RimStrength ("Moon Rim Strength", Range(0, 2)) = 0.22
        _Meadow ("Meadow Variation", Range(0, 1)) = 0.8
        _Flash ("Hit Flash", Color) = (0, 0, 0, 0)
        _GlowBand ("Window Glow Band", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0
        #pragma multi_compile_instancing

        float _AmbientBoost;
        float4 _RimColor;
        float _RimStrength;
        float _Meadow;
        float4 _Flash;
        float _GlowBand;

        struct Input
        {
            float4 vertColor;
            float3 worldPos;
            float3 worldNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
        }

        float Hash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float Noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = Hash(i);
            float b = Hash(i + float2(1, 0));
            float c = Hash(i + float2(0, 1));
            float d = Hash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float3 albedo = IN.vertColor.rgb;

            // Meadow: grass-green surfaces get soft patches of cooler and warmer green.
            float green = saturate((albedo.g - max(albedo.r, albedo.b)) * 6.0) * _Meadow;
            float n = Noise(IN.worldPos.xz * 0.045) * 0.65 + Noise(IN.worldPos.xz * 0.21) * 0.35;
            float3 patchTint = lerp(float3(0.74, 0.86, 0.92), float3(1.14, 1.1, 0.82), n);
            albedo = lerp(albedo, albedo * patchTint, green);
            o.Albedo = albedo;

            // Window glow band, kept from the original look (warm yellow-orange vertex colors).
            float windowGlow = step(0.85, albedo.r) * step(0.45, albedo.g) * step(albedo.b, 0.4) * _GlowBand;

            // Moon rim on upright surfaces: birds, trunks and walls read against the dark field.
            float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float3 normal = normalize(IN.worldNormal);
            float rim = pow(1.0 - saturate(dot(viewDir, normal)), 4.0);
            float upright = 1.0 - saturate(abs(normal.y) * 1.4 - 0.2);
            float3 rimLight = _RimColor.rgb * rim * upright * _RimStrength;

            o.Emission = albedo * windowGlow * 2.4 + albedo * _AmbientBoost * 0.35 + rimLight + _Flash.rgb * _Flash.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
