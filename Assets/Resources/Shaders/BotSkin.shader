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
        _RampLow ("Ramp Shade", Range(0, 1)) = 0.62
        _RampEdge ("Ramp Edge", Range(0, 0.5)) = 0.04
        _InkColor ("Ink Outline", Color) = (0.03, 0.04, 0.08, 1)
        _OutlinePx ("Outline Pixels", Range(0, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Painted
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
        // The act's film rim (FilmLight sets it for every character: rgb colour, a gain). Zero when nothing sets it.
        float4 _FilmRim;
        float _RampLow;
        float _RampEdge;

        // Two-step painted ramp: a lit tone and a softened shade tone, joined by a narrow smoothstep.
        half4 LightingPainted(SurfaceOutput s, half3 lightDir, half atten)
        {
            half nl = dot(s.Normal, lightDir) * 0.5h + 0.5h;
            half step2 = smoothstep(0.5h - _RampEdge, 0.5h + _RampEdge, nl);
            half ramp = lerp(_RampLow, 1.0h, step2);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * ramp * atten;
            c.a = s.Alpha;
            return c;
        }

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

            // A zero vector (a mesh normal of 0,0,0, a pixel on the camera) must not become NaN: it would reach the HDR frame
            // and the bloom would smear it into a black square. Written so NaN fails the test as well.
            float3 toCam = _WorldSpaceCameraPos - IN.worldPos;
            float toCamSq = dot(toCam, toCam);
            float3 viewDir = toCamSq > 1e-8 ? toCam * rsqrt(toCamSq) : float3(0, 0, 1);
            float nSq = dot(IN.worldNormal, IN.worldNormal);
            float3 normal = nSq > 1e-8 ? IN.worldNormal * rsqrt(nSq) : float3(0, 1, 0);
            float rim = pow(1.0 - saturate(dot(viewDir, normal)), 4.0);
            float filmRim = pow(1.0 - saturate(dot(viewDir, normal)), 2.2);
            // The moon is the scene's directional light: the rim hugs the side facing it.
            float3 moonDir = normalize(_WorldSpaceLightPos0.xyz + float3(0, 1e-4, 0));
            float moonSide = saturate(dot(normal, moonDir) * 0.5 + 0.65);
            float3 rimLight = _RimColor.rgb * rim * moonSide * 1.4 * _RimStrength + _FilmRim.rgb * filmRim * _FilmRim.a;

            // Capped so a bright texel can never burn to a flat white shape or bleed past the silhouette.
            float3 lit = albedo * skin.a * _Glow + albedo * _AmbientBoost * 0.35 + rimLight;
            o.Emission = min(lit, float3(0.85, 0.9, 0.95)) + _Flash.rgb * _Flash.a;
        }
        ENDCG

        // Thin ink outline: inverted hull, pushed out in clip space so it stays a fixed pixel width (phone included).
        Pass
        {
            Name "INK"
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _InkColor;
            float _OutlinePx;
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 dir = mul((float2x2)UNITY_MATRIX_P, n.xy);
                float dl = dot(dir, dir);
                dir = dl > 1e-8 ? dir * rsqrt(dl) : float2(0, 0);
                o.pos.xy += dir * (_OutlinePx * 2.0) / _ScreenParams.xy * o.pos.w;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return _InkColor; }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
