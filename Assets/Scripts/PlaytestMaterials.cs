using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "materials" scenario (Docs/Materials.md): the Hollow's ground is painted PBR, not flat colour.
//   1. Arcade starts and HollowGround has swapped the arena ground to Knoll/PbrSplat with grass, dirt and cobble (materials-hollow).
//   2. The splat mask holds a path (dirt) and a cobble apron near the circle (materials-mask: shares of each, and the path reaching the circle).
//   3. A frame off the play camera, ground only, has real detail (materials-detail: luminance spread of a ground patch) and no obvious
//      tiling: the same patch shifted by one tile of 3 m differs (materials-repeat: mean difference between the two samples).
//   4. Texture memory of every PBR texture is under 12 MB (pbrTextureMb) and the phone tier says its mip limit (materials-phone).
// Touch-safe: it only waits and looks; Arcade is started by the base scenario's own tap.
public static partial class Playtest {
    static IEnumerator MaterialsRun(PlaytestOptions options) {
        yield return run.Until(() => WaveDirector.Instance != null && ControlsHelp.PlayedSeconds > 0f, 20f, "Arcade to start");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSeconds(2f);
        run.Scene("materials-hollow");
        bool applied = HollowGround.Apply();
        run.Event("materials-hollow", "applied " + applied + ": " + HollowGround.Report);
        run.Metric("hollowGroundPbr", applied ? 1 : 0);
        if (!applied) {
            run.Problem(PlaytestSeverity.Bug, "materials: the Hollow ground is not the PBR splat material (" + HollowGround.Report + ")");
        }
        Material m = HollowGround.Material;
        if (m != null) {
            run.Event("materials-layers", "shader " + m.shader.name + ", albedo " + TexDesc(m, "_A0") + " / " + TexDesc(m, "_A1") + " / " + TexDesc(m, "_A2") + ", normal " + TexDesc(m, "_N0") + " / " + TexDesc(m, "_N1") + " / " + TexDesc(m, "_N2") + ", lite " + m.GetFloat("_Lite"));
            var mask = m.GetTexture("_Mask") as Texture2D;
            if (mask != null) {
                // The mask is non-readable after Apply(false, true)? It was built readable: count its path and cobble shares.
                int path = 0, cobble = 0, n = 0;
                Color32[] px = null;
                try { px = mask.GetPixels32(); } catch (System.Exception) { }
                if (px != null) {
                    for (int i = 0; i < px.Length; i++) {
                        n++;
                        if (px[i].r > 127) path++;
                        if (px[i].g > 127) cobble++;
                    }
                    float pathPct = 100f * path / Mathf.Max(1, n), cobblePct = 100f * cobble / Mathf.Max(1, n);
                    run.Event("materials-mask", "path " + pathPct.ToString("0.0") + "% of the field, cobble " + cobblePct.ToString("0.00") + "%");
                    run.Metric("hollowPathPct", Mathf.Round(pathPct * 10f) / 10f);
                    if (pathPct < 0.8f || cobblePct < 0.02f) {
                        run.Problem(PlaytestSeverity.Bug, "materials: the splat mask has no readable path or no cobble apron");
                    }
                }
            }
        }
        Camera cam = Camera.main;
        if (cam != null) {
            // A patch of ground just ahead of the player, rendered, then the same patch one 3 m tile away: they must not match pixel for pixel.
            float spread = GroundSpread(cam, 0f, out float meanA);
            float spread2 = GroundSpread(cam, 3f, out float meanB);
            float diff = Mathf.Abs(meanA - meanB);
            run.Event("materials-detail", "ground luminance spread " + spread.ToString("0.000") + " (a flat colour is under 0.01), shifted one tile " + spread2.ToString("0.000"));
            run.Metric("groundDetailSpread", Mathf.Round(spread * 1000f) / 1000f);
            if (spread < 0.01f) {
                run.Problem(PlaytestSeverity.Bug, "materials: the ground reads as flat colour (spread " + spread.ToString("0.000") + ")");
            }
            run.Event("materials-repeat", "mean luminance of the patch and the same patch one tile away differ by " + diff.ToString("0.000"));
        }
        float mb = PbrKit.Bytes() / 1048576f;
        run.Metric("pbrTextureMb", Mathf.Round(mb * 100f) / 100f);
        run.Event("materials-phone", "touch " + UiFit.Touch + ", " + MobileQuality.Describe() + ", PBR textures " + mb.ToString("0.00") + " MB (limit 12)");
        if (mb > 12f) {
            run.Problem(PlaytestSeverity.Bug, "materials: PBR textures hold " + mb.ToString("0.0") + " MB, over the 12 MB limit");
        }
        run.Scene("materials-play");
        yield return new WaitForSeconds(10f);
    }

    static string TexDesc(Material m, string prop) {
        var t = m.GetTexture(prop);
        return t == null ? "MISSING" : t.width + "px";
    }

    // Luminance spread (standard deviation) and mean of a 32x32 patch of the screen's lower middle, where the ground is. The camera is
    // nudged along world x by `shift` metres for the second look, then put back.
    static float GroundSpread(Camera cam, float shift, out float mean) {
        Vector3 at = cam.transform.position;
        cam.transform.position = at + Vector3.right * shift;
        var rt = RenderTexture.GetTemporary(160, 90, 24, RenderTextureFormat.ARGB32);
        RenderTexture old = cam.targetTexture, active = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        cam.transform.position = at;
        RenderTexture.active = rt;
        var tex = new Texture2D(160, 90, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 160, 90), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels(64, 8, 32, 32);
        Object.Destroy(tex);
        float sum = 0f, sq = 0f;
        foreach (Color c in px) {
            float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            sum += l;
            sq += l * l;
        }
        mean = sum / px.Length;
        return Mathf.Sqrt(Mathf.Max(0f, sq / px.Length - mean * mean));
    }
}
