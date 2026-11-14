using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "characters" scenario (Docs/CharacterModels.md): opens the CharacterGallery (a named setup shortcut: a scene built in code),
// logs the character validation report (triangles per character, primitives, budgets), and photographs the row of every
// character in colour and as black-fill silhouettes, then the heroes, bots, bosses and the hub's lodgers, each stop held long
// enough for the idle behaviours to show. Events: character-report, character, photo, silhouette-size, characters-done.
public static class PlaytestCharacters {
    static float Luma(Color32 c) {
        return (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
    }

    // Renders the camera through the real grade and counts bright pixels that sit OUTSIDE the dark outline: walking in from each
    // side of a row, the first pixel that breaks from the sky is bright and a dark ink pixel follows within 4 px.
    static int HaloPx(Camera cam, int w, int h) {
        if (cam == null) {
            return 0;
        }
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture oldTarget = cam.targetTexture, active = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = oldTarget;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = tex.GetPixels32();
        Object.Destroy(tex);
        int count = 0;
        for (int y = 0; y < h; y++) {
            for (int side = 0; side < 2; side++) {
                int step = side == 0 ? 1 : -1;
                int x = side == 0 ? 1 : w - 2;
                float prev = Luma(px[y * w + (side == 0 ? 0 : w - 1)]);
                for (; x > 3 && x < w - 4; x += step) {
                    float l = Luma(px[y * w + x]);
                    if (Mathf.Abs(l - prev) > 0.08f) {
                        if (l > 0.5f) {
                            for (int k = 1; k <= 4; k++) {
                                if (Luma(px[y * w + x + step * k]) < 0.2f) {
                                    count++;
                                    break;
                                }
                            }
                        }
                        break;
                    }
                    prev = l;
                }
            }
        }
        return count;
    }

    public static IEnumerator Run(PlaytestRun run) {
        run.Event("setup", "characters: opening the character gallery");
        KitReport report = CharacterValidation.Run();
        foreach (string line in report.Lines) {
            run.Event("character-report", line);
        }
        run.Event("character-summary", report.Summary());
        run.Metric("characterPrimitives", (float)report.Primitives);
        run.Metric("characterMaxTriangles", (float)report.MaxPieceTriangles);
        foreach (string f in report.Failures) {
            run.Problem(PlaytestSeverity.Bug, "character-validation: " + f);
        }
        // Which bodies and landmark props come from the Gen bakes (Tools/Bake) and which are still the code version.
        string[] genBodies = { "courier", "foreman", "server", "order_bearer" };
        string[] genLodgers = { "of_lodger_tick", "of_lodger_brim", "of_lodger_spare", "of_lodger_chime" };
        string[] genProps = { "of_stamp_press", "gt_gate_arch", "gt_ticket_booth", "of_pneumatic_tube", "yd_notice_board", "rd_lectern", "gt_turnstile", "rd_great_ledger", "yd_clock_tower", "gt_stamp_press_large" };
        int genCount = 0;
        foreach (string id in genBodies) {
            bool baked = BakedMesh.Has(id);
            genCount += baked ? 1 : 0;
            run.Event("gen-body", id + " baked=" + baked + (baked ? " parts=" + BakedMesh.Load(id).Count : " (code body in use)"));
        }
        foreach (string id in genLodgers) {
            bool baked = BakedMesh.Has(id);
            genCount += baked ? 1 : 0;
            run.Event("gen-body", id + " baked=" + baked);
        }
        int propCount = 0;
        foreach (string id in genProps) {
            bool baked = BakedMesh.Has(id);
            propCount += baked ? 1 : 0;
            run.Event("gen-prop", id + " baked=" + baked);
        }
        run.Metric("genBodies", (float)genCount);
        run.Metric("genProps", (float)propCount);
        if (genCount < genBodies.Length + genLodgers.Length) {
            run.Problem(PlaytestSeverity.Warning, "characters: only " + genCount + " of 8 bosses and lodgers are Gen bodies");
        }
        CharacterGallery gallery = CharacterGallery.Open();
        yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
        if (gallery.Entries.Count < 14) {
            run.Problem(PlaytestSeverity.Bug, "characters: only " + gallery.Entries.Count + " of 14 characters stand in the gallery");
        }
        foreach (CharacterGallery.Entry e in gallery.Entries) {
            run.Event("character", e.Id + " group=" + e.Group + " triangles=" + e.Triangles + " idle=" + e.Idle);
        }
        run.Metric("characterCount", (float)gallery.Entries.Count);
        int photos = 0, haloDesktop = 0, haloPhone = 0;
        for (int i = 0; i < gallery.Shots.Count; i++) {
            gallery.Go(i);
            CharacterGallery.Shot s = gallery.Shots[i];
            yield return null;
            run.Scene("characters-" + s.Name);
            run.Event("photo", s.Name + " silhouette=" + s.Silhouette + " visibleTriangles=" + gallery.LastVisibleTriangles);
            if (s.Name == "all-silhouette") {
                foreach (CharacterGallery.Entry e in gallery.Entries) {
                    float px = gallery.ScreenHeight(e);
                    run.Event("silhouette-size", e.Id + " pixels=" + px.ToString("0"));
                    if (px < 40f) {
                        run.Problem(PlaytestSeverity.Warning, "characters: " + e.Id + " is only " + px.ToString("0") + " px tall in the wide shot");
                    }
                }
            }
            if (!s.Silhouette) {
                // Edge check from real pixels: desktop frame and the 390x844 phone frame.
                int d = HaloPx(gallery.Cam, 960, 540), p = HaloPx(gallery.Cam, 390, 844);
                haloDesktop += d;
                haloPhone += p;
                if (d + p > 0) {
                    run.Event("halo", s.Name + " desktop=" + d + " phone=" + p);
                }
            }
            photos++;
            // long enough to see the idle behaviours move
            yield return new WaitForSecondsRealtime(s.Name == "all" || s.Name.EndsWith("silhouette") ? 1.6f : 2.6f);
        }
        run.Metric("characterPhotos", (float)photos);
        run.Metric("char.haloPx", (float)(haloDesktop + haloPhone));
        run.Metric("char.haloPx.phone", (float)haloPhone);
        run.Event("halo-summary", "char.haloPx desktop=" + haloDesktop + " phone=" + haloPhone);
        if (haloDesktop + haloPhone > 0) {
            run.Problem(PlaytestSeverity.Bug, "characters: bright fringe outside the outline, char.haloPx=" + (haloDesktop + haloPhone));
        }
        gallery.SetSilhouette(false);
        gallery.Close();
        run.Event("characters-done", "photos=" + photos);
    }
}
