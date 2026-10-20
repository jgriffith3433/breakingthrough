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

}
