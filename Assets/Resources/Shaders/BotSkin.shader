// The Gen bots (YoBot.fbx, SpiderBot.fbx): their painted 1K skin under the knoll's night lighting.
// The skin's alpha is a glow mask (the Yo Bot's HUD and core, the Spider Bot's light strips, glass and consoles).
// No emission from the imported FBX material: that is what made the bots read as white shapes.
Shader "Knoll/BotSkin"
{
    Properties
    {
        _MainTex ("Skin (A = glow)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Lift ("Night Lift", Range(0, 2)) = 1.35
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.3
        _RimColor ("Moon Rim", Color) = (0.42, 0.55, 0.95, 1)
        _RimStrength ("Moon Rim Strength", Range(0, 2)) = 0.45
        _Glow ("Glow", Range(0, 4)) = 1.6
        _Flash ("Hit Flash", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        fixed4 _Color;
        float _Lift;
        float _AmbientBoost;
        float4 _RimColor;
        float _RimStrength;
        float _Glow;
        float4 _Flash;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 skin = tex2D(_MainTex, IN.uv_MainTex);
            // Gunmetal and navy are dark: lift them a little so the shapes read on the night field.
            float3 albedo = saturate(skin.rgb * _Lift) * _Color.rgb;
            o.Albedo = albedo;

            float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float3 normal = normalize(IN.worldNormal);
            float rim = pow(1.0 - saturate(dot(viewDir, normal)), 4.0);
            float3 rimLight = _RimColor.rgb * rim * _RimStrength;

            o.Emission = albedo * skin.a * _Glow + albedo * _AmbientBoost * 0.35 + rimLight + _Flash.rgb * _Flash.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
