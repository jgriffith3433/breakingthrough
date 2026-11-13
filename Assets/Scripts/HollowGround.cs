using UnityEngine;
using UnityEngine.SceneManagement;

// The Moonlit Hollow's ground in painted PBR (Docs/Materials.md): moonlit lawn grass, the worn dirt lanes the flock walks in on and a
// cobble apron round Slice's circle, blended by a splat mask on the arena's ground mesh. BuildArena bakes the ground with a plain
// vertex-colour material, and its colours already hold the rim shade and rock; this swaps the material at load (the mesh, the
// collider and the vertex colours stay) so the old look is the fallback when the Gen files are missing. The mask repeats
// BuildArena's lane maths (same seed, same wobble) so the paths lie exactly where the scene's lanes do.
public static class HollowGround {
    const float Reach = 165f;      // the mask covers the field and a little more, 330 m across
    const int Size = 384;
    const float FieldEnd = 150f;

    public static bool Applied { get; private set; }
    public static float Seconds { get; private set; }
    public static string Report { get; private set; } = "not applied";
    public static Material Material { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        SceneManager.sceneLoaded += (s, mode) => Apply();
        Apply();
    }

    public static bool Apply() {
        GameObject arena = GameObject.Find("Arena");
        if (arena == null) {
            return false;
        }
        Transform ground = arena.transform.Find("Ground");
        var rend = ground != null ? ground.GetComponent<MeshRenderer>() : null;
        if (rend == null) {
            return false;
        }
        if (rend.sharedMaterial != null && rend.sharedMaterial.shader != null && rend.sharedMaterial.shader.name == "Knoll/PbrSplat") {
            return true;
        }
        if (!PbrKit.Have(PbrKit.HollowDir, PbrKit.HollowSets)) {
            Report = "Pbr/hollow textures missing: kept the vertex-colour ground";
            return false;
        }
        float t0 = Time.realtimeSinceStartup;
        GameObject piano = GameObject.Find("PianoRecital");
        Vector3 c = piano != null ? piano.transform.position : arena.transform.position;
        var rng = new System.Random(20261014);   // BuildArena's seed: the lanes wobble the same way
        float seedX = (float)rng.NextDouble() * 1000f;
        float seedZ = (float)rng.NextDouble() * 1000f;
        float rMax = Reach * 1.5f;
        const int Steps = 4;
        var wobble = new float[Steps][];
        for (int i = 0; i < Steps; i++) {
            wobble[i] = new float[(int)(rMax / 0.5f) + 2];
            for (int k = 0; k < wobble[i].Length; k++) {
                wobble[i][k] = (Mathf.PerlinNoise(k * 0.5f * 0.04f + i * 10f, seedX) - 0.5f) * 8f;
            }
        }
        Texture2D mask = PbrKit.Mask("Hollow Splat Mask", Size, (px, py) => {
            float x = (px + 0.5f) / Size * Reach * 2f - Reach, z = (py + 0.5f) / Size * Reach * 2f - Reach;
            float r = Mathf.Sqrt(x * x + z * z);
            float angDeg = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            float lane = 999f;
            int ki = Mathf.Min((int)(r / 0.5f), wobble[0].Length - 1);
            for (int i = 0; i < Steps; i++) {
                float laneDeg = (i * Mathf.PI * 0.5f + Mathf.PI * 0.25f) * Mathf.Rad2Deg;
                float diff = Mathf.DeltaAngle(angDeg, laneDeg) * Mathf.Deg2Rad;
                lane = Mathf.Min(lane, Mathf.Abs(Mathf.Sin(diff) * r + wobble[i][ki]) + (Mathf.Abs(diff) > Mathf.PI * 0.5f ? 999f : 0f));
            }
            float width = Mathf.Lerp(1.4f, 3.2f, Mathf.Clamp01(r / FieldEnd)) + 0.8f;   // a little wider than the old colour: the path reads from the camera
            float path = Mathf.Clamp01((width - lane) / 1.4f) * Mathf.Clamp01((r - 4f) / 4f) * Mathf.Clamp01((FieldEnd + 10f - r) / 20f);
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 5.4f) / 1.8f);   // the worn ring round the plaza is dirt too
            path = Mathf.Max(path, ring * 0.9f);
            // Cobble: an apron out to 8 m round the circle, and the lanes turn to cobble for their last 18 m.
            float apron = Mathf.Clamp01((9f - r) / 2f) * Mathf.Clamp01((r - 2.5f) / 2f);
            float inLane = path * Mathf.Clamp01((24f - r) / 12f);
            float cobble = Mathf.Max(apron, inLane);
            return new Color32((byte)(path * 255f), (byte)(cobble * 255f), 0, 255);
        });
        Material m = PbrKit.Splat(PbrKit.HollowDir, PbrKit.HollowSets, mask, new Vector4(c.x - Reach, c.z - Reach, 1f / (Reach * 2f), 1f / (Reach * 2f)), 3f, 0.6f, 1f, new Color(1f, 1f, 1f));
        if (m == null) {
            Report = "PbrSplat shader or textures missing: kept the vertex-colour ground";
            return false;
        }
        // The mask is in world space around the piano, and the shader reads the world position, so no UVs are needed.
        rend.sharedMaterial = m;
        Material = m;
        Applied = true;
        Seconds = Time.realtimeSinceStartup - t0;
        Report = PbrKit.Last + ", mask " + Size + "x" + Size + " in " + (Seconds * 1000f).ToString("0") + " ms, " + (PbrKit.Bytes() / 1048576f).ToString("0.00") + " MB of textures";
        return true;
    }
}
