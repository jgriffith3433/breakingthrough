Shader "Knoll/VertexColorLit"
{
    Properties
    {
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.18
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        float _AmbientBoost;

        struct Input
        {
            float4 vertColor;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float3 albedo = IN.vertColor.rgb;
            o.Albedo = albedo;
            float windowGlow = step(0.85, albedo.r) * step(0.45, albedo.g) * step(albedo.b, 0.4);
            o.Emission = albedo * windowGlow * 2.4 + albedo * _AmbientBoost * 0.35;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
