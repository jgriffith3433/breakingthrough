// The Model Kit's one shader (Docs/ModelKit.md section 3): albedo, a tangent-space normal map stored as plain RGB, and
// the vertex colour as baked ambient occlusion (dark at the foot, light at the top). GPU instancing is on.
Shader "Knoll/KitLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal (RGB)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.15
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Flash ("Hit flash (rgb, a = amount)", Color) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow finalcolor:HdrGuard
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _Flash;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * IN.color.rgb;
            float3 n = tex2D(_BumpMap, IN.uv_MainTex).xyz * 2 - 1;
            n.xy *= _BumpScale;
            // A flat (0,0,0) texel must not turn into a NaN normal: the HDR frame would carry it into the bloom.
            float nn = dot(n, n);
            o.Normal = nn > 0.0001 ? n * rsqrt(nn) : float3(0, 0, 1);
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;
            o.Albedo = lerp(o.Albedo, _Flash.rgb, _Flash.a * 0.6);
            o.Emission = _EmissionColor.rgb + _Flash.rgb * _Flash.a * 0.7;
            o.Alpha = 1;
        }

        // The last line of defence for the HDR frame: whatever lighting produced (a mesh normal of 0,0,0 is the one we have
        // met), a NaN or Inf never leaves this shader, because one such pixel is smeared by the bloom into a black square.
        // Written like BTPost's SafeChannel so NaN fails the test (NaN >= 0 is false) and a negative or huge value is held.
        void HdrGuard (Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            half3 c = color.rgb;
            color.rgb = half3(c.r >= 0.0 ? min(c.r, 64.0) : 0.0, c.g >= 0.0 ? min(c.g, 64.0) : 0.0, c.b >= 0.0 ? min(c.b, 64.0) : 0.0);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
