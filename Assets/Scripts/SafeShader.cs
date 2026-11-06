using UnityEngine;

// Shader.Find returns null in a player build when nothing in the build references the shader
// (Unlit/Color and Standard are not in the always-included list), and new Material(null) throws
// "ArgumentNullException: Value cannot be null. Parameter name: shader". Always resolve through here.
public static class SafeShader
{
    static readonly string[] Fallbacks = { "Sprites/Default", "Unlit/Color", "UI/Default" };

    public static Shader Find(string name)
    {
        Shader shader = Shader.Find(name);
        if (shader != null) return shader;
        for (int i = 0; i < Fallbacks.Length; i++) {
            shader = Shader.Find(Fallbacks[i]);
            if (shader != null) return shader;
        }
        return null;
    }

    // Opaque flat colour that writes depth. Sprites/Default is transparent with ZWrite off, so solid meshes drawn
    // with it sort by distance and paint over each other. Falls back to Sprites/Default only if the shader is missing.
    public static Shader Solid()
    {
        Shader shader = Shader.Find("Knoll/FlatSolid");
        if (shader == null) shader = Resources.Load<Shader>("Shaders/FlatSolid");
        return shader != null ? shader : Find("Sprites/Default");
    }

    // Solid when the colour is opaque, the sprite shader when it has alpha.
    public static Shader For(Color color)
    {
        return color.a >= 0.99f ? Solid() : Find("Sprites/Default");
    }
}
