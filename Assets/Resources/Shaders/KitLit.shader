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
        #pragma surface surf Standard fullforwardshadows addshadow
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
            fixed3 n = tex2D(_BumpMap, IN.uv_MainTex).xyz * 2 - 1;
            n.xy *= _BumpScale;
            o.Normal = normalize(n);
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;
            o.Albedo = lerp(o.Albedo, _Flash.rgb, _Flash.a * 0.6);
            o.Emission = _EmissionColor.rgb + _Flash.rgb * _Flash.a * 0.7;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
