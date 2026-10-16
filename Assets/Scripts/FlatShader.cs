using UnityEngine;

// Unlit/Color is stripped from WebGL builds, so Shader.Find returns null and new Material(null) throws.
// BT/Flat ships from Resources; Sprites/Default is always included.
public static class FlatShader
{
    static Shader cached;

    public static Shader Get()
    {
        if (cached == null)
        {
            cached = Shader.Find("BT/Flat");
            if (cached == null)
                cached = Shader.Find("Unlit/Color");
            if (cached == null)
                cached = Shader.Find("Sprites/Default");
        }
        return cached;
    }
}
