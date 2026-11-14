using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// The "film-light" scenario (FilmLight.cs): the Moonlit Hollow lit like a film set, one look per Arcade act. It plays Arcade, uses
// the testing wave key (a named setup shortcut) to reach waves 1, 11 and 21, and at each act reads the key light colour, the
// frame the camera draws (mean colour and brightness through the grade) and a silhouette check: how far Wallit, Slice and a Yo Bot
// and Spider Bot stand out from the ring of background around them. At each act change it samples frames for the whole blend and
// times it. On a phone viewport (mobile, 390x844) every move, swing and card is a finger. Events film-look, film-silhouette,
// film-blend, film-fps; metrics filmKeyDistance_1_2, filmKeyDistance_2_3, filmBlendSeconds, filmBlendMinLuma, filmRimMin,
// filmFpsP5_<act>.
public static partial class Playtest {
    struct FilmFrame { public float Luma; public Color Mean; }

    static FilmFrame FilmSample(Camera cam, out Texture2D tex) {
        // The sample has the screen's own shape, and the camera's aspect is put back after it: a 16:9 sample of a phone camera used to
        // leave it stretched until RenderFit's next half-second pass.
        int W = 192, H = Mathf.Clamp(Mathf.RoundToInt(192f * Screen.height / Mathf.Max(1, Screen.width)), 64, 400);
        var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
        RenderTexture old = cam.targetTexture, active = RenderTexture.active;
        cam.targetTexture = rt;
        NightGrade.Sampling = true;
        cam.Render();
        NightGrade.Sampling = false;
        cam.targetTexture = old;
        if (old == null) {
            cam.ResetAspect();
        }
        RenderTexture.active = rt;
        tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        var f = new FilmFrame();
        Color[] px = tex.GetPixels();
        foreach (Color c in px) {
            f.Mean += c;
            f.Luma += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }
        f.Mean /= px.Length;
        f.Luma /= px.Length;
        return f;
    }

    static float FilmLuma(Color c) {
        return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }

    // How much brighter or different the character's own brightest edge is than the ring of background round it (0 = it melts in).
    static float FilmContrast(Camera cam, Texture2D tex, Renderer[] renderers) {
        Bounds b = default;
        bool any = false;
        foreach (Renderer r in renderers) {
            if (r != null && r.enabled && r.gameObject.activeInHierarchy) {
                if (!any) {
                    b = r.bounds;
                    any = true;
                } else {
                    b.Encapsulate(r.bounds);
                }
            }
        }
        if (!any) {
            return -1f;
        }
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        for (int i = 0; i < 8; i++) {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 sp = cam.WorldToViewportPoint(corner);
            if (sp.z <= 0f) {
                return -1f;
            }
            minX = Mathf.Min(minX, sp.x);
            maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, sp.y);
            maxY = Mathf.Max(maxY, sp.y);
        }
        int w = tex.width, h = tex.height;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * w), 0, w - 1), x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * w), 1, w);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * h), 0, h - 1), y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * h), 1, h);
        if (x1 - x0 < 3 || y1 - y0 < 3) {
            return -1f;
        }
        int padX = Mathf.Max(3, (x1 - x0) / 3), padY = Mathf.Max(3, (y1 - y0) / 3);
        float ring = 0f, inner = 0f;
        int ringN = 0;
        var inside = new List<float>();
        for (int y = Mathf.Max(0, y0 - padY); y < Mathf.Min(h, y1 + padY); y++) {
            for (int x = Mathf.Max(0, x0 - padX); x < Mathf.Min(w, x1 + padX); x++) {
                float l = FilmLuma(tex.GetPixel(x, y));
                if (x >= x0 && x < x1 && y >= y0 && y < y1) {
                    inside.Add(l);
                } else {
                    ring += l;
                    ringN++;
                }
            }
        }
        if (ringN == 0 || inside.Count == 0) {
            return -1f;
        }
        ring /= ringN;
        inside.Sort();
        inner = inside[Mathf.Min(inside.Count - 1, Mathf.FloorToInt(inside.Count * 0.9f))];   // its brightest tenth: the rim and the lit side
        return Mathf.Abs(inner - ring) / (ring + 0.04f);
    }

    static string FilmSilhouettes(Camera cam, out float lowest) {
        FilmFrame f = FilmSample(cam, out Texture2D tex);
        lowest = 999f;
        string line = "";
        var seen = new HashSet<string>();
        var subjects = new List<KeyValuePair<string, Renderer[]>>();
        CharacterMovement wallit = Object.FindAnyObjectByType<CharacterMovement>();
        if (wallit != null) {
            subjects.Add(new KeyValuePair<string, Renderer[]>("Wallit", wallit.GetComponentsInChildren<Renderer>()));
        }
        if (Musician.Instance != null) {
            subjects.Add(new KeyValuePair<string, Renderer[]>("Slice", Musician.Instance.GetComponentsInChildren<Renderer>()));
        }
        foreach (Enemy e in Enemy.Roster) {
            if (e != null && !e.Boss && seen.Add(e.Kind.ToString()) && Vector3.Distance(e.transform.position, cam.transform.position) < 40f) {
                subjects.Add(new KeyValuePair<string, Renderer[]>(e.Kind.ToString(), e.GetComponentsInChildren<Renderer>()));
            }
        }
        foreach (var s in subjects) {
            float c = FilmContrast(cam, tex, s.Value);
            if (c < 0f) {
                continue;
            }
            lowest = Mathf.Min(lowest, c);
            line += s.Key + "=" + c.ToString("0.00") + " ";
        }
        Object.Destroy(tex);
        return line.Length > 0 ? line.Trim() : "no subject in frame";
    }

    // A pass over every frame of a blend: never black, and the change is smooth (no frame jumps by more than a third).
    static IEnumerator FilmBlend(Camera cam, string name, int from, int to, System.Action<float, float> done) {
        float minLuma = 9f, worstJump = 0f, last = -1f;
        float start = Time.realtimeSinceStartup;
        int samples = 0;
        while (Time.realtimeSinceStartup - start < 2.2f && !run.Done) {
            FilmFrame f = FilmSample(cam, out Texture2D tex);
            Object.Destroy(tex);
            minLuma = Mathf.Min(minLuma, f.Luma);
            if (last >= 0f) {
                worstJump = Mathf.Max(worstJump, Mathf.Abs(f.Luma - last) / Mathf.Max(0.05f, last));
            }
            last = f.Luma;
            samples++;
            yield return new WaitForSecondsRealtime(0.1f);
        }
        run.Event("film-blend", name + ": " + samples + " frames sampled over the change, dimmest mean brightness " + minLuma.ToString("0.000")
            + ", biggest step " + (worstJump * 100f).ToString("0") + "%, blend took " + FilmLight.LastBlendSeconds.ToString("0.00") + " s, dimmest key " + FilmLight.MinKeyDuringBlend.ToString("0.00"));
        done(minLuma, worstJump);
    }

    static IEnumerator FilmAct(CharacterMovement player, Camera cam, int act, System.Action<Color, float> got) {
        yield return AnimFight(player, 4f, null);
        FilmFrame f = FilmSample(cam, out Texture2D tex);
        Object.Destroy(tex);
        float lowest;
        string sil = FilmSilhouettes(cam, out lowest);
        run.Scene("Film act " + (act + 1) + (UiFit.Touch ? " phone" : " desktop"));
        run.Event("film-look", "act " + (act + 1) + " wave " + WaveDirector.Wave + ": " + FilmLight.Describe() + " frame mean (" + f.Mean.r.ToString("0.00") + ", "
            + f.Mean.g.ToString("0.00") + ", " + f.Mean.b.ToString("0.00") + ") brightness " + f.Luma.ToString("0.00") + " " + ArcadeActs.Describe());
        run.Event("film-silhouette", "act " + (act + 1) + ": edge against background " + sil + (lowest < 998f ? " lowest " + lowest.ToString("0.00") : ""));
        got(FilmLight.KeyNow, lowest < 998f ? lowest : -1f);
    }

    static IEnumerator FilmRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Log("film-light at " + Screen.width + "x" + Screen.height + ", " + (animTouch ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        yield return run.Until(() => ArcadeActs.Instance != null && ArcadeActs.Act == 0 && FilmLight.Instance != null, 8f, "act 1 and its film light");
        if (run.Done) {
            yield break;
        }
        Camera cam = Camera.main;
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        var keys = new Color[3];
        float rimMin = 999f;
        float[] p5 = new float[3];
        float blendMax = 0f;

        for (int act = 0; act < 3 && !run.Done && !WaveDirector.IsLost; act++) {
            if (act > 0) {
                // Setup: the wave key up to the last wave of the act, then one more press crosses into the next, sampled from that frame.
                run.Event("setup", "testing wave key to wave " + (act * 10 + 1) + " (the last press is sampled)");
                yield return GoWave(act * 10);
                if (LevelUpScreen.IsOpen) {
                    PlayInput.Tap(KeyCode.Alpha1);
                    yield return new WaitForSecondsRealtime(0.3f);
                }
                PlayInput.Tap(KeyCode.Equals);
                float lumaMin = 0f, jump = 0f;
                yield return FilmBlend(cam, "act " + act + " to " + (act + 1), act - 1, act, (m, j) => { lumaMin = m; jump = j; });
                run.Metric("filmBlendMinLuma_" + (act + 1), Mathf.Round(lumaMin * 1000f) / 1000f);
                run.Metric("filmBlendSeconds", Mathf.Round(FilmLight.LastBlendSeconds * 100f) / 100f);
                run.Metric("light.actBlendSeconds_" + act + "_" + (act + 1), Mathf.Round(FilmLight.LastBlendSeconds * 100f) / 100f);
                run.Metric("light.actBlendSeconds", Mathf.Round(Mathf.Max(blendMax, FilmLight.LastBlendSeconds) * 100f) / 100f);
                blendMax = Mathf.Max(blendMax, FilmLight.LastBlendSeconds);
                if (lumaMin < 0.02f) {
                    run.Problem(PlaytestSeverity.Bug, "film-light: a black frame during the change into act " + (act + 1) + " (mean brightness " + lumaMin.ToString("0.000") + ")");
                }
                if (jump > 0.35f) {
                    run.Problem(PlaytestSeverity.Bug, "film-light: the change into act " + (act + 1) + " is a jump, not a blend (a frame moved " + (jump * 100f).ToString("0") + "%)");
                }
                if (FilmLight.LastBlendSeconds < 1.2f || FilmLight.LastBlendSeconds > 1.9f) {
                    run.Problem(PlaytestSeverity.Bug, "film-light: the blend into act " + (act + 1) + " took " + FilmLight.LastBlendSeconds.ToString("0.00") + " s, not about 1.5");
                }
            }
            FilmLight.ResetFrames();
            int a = act;
            float low = -1f;
            yield return FilmAct(player, cam, act, (k, l) => { keys[a] = k; low = l; });
            yield return AnimFight(player, 8f, null);
            p5[act] = FilmLight.FrameMsPercentile(0.95f);
            float fps5 = p5[act] > 0f ? 1000f / p5[act] : 0f;
            run.Event("film-fps", "act " + (act + 1) + ": 5th-percentile frame rate " + fps5.ToString("0.0") + " fps (" + p5[act].ToString("0.0") + " ms), " + Enemy.Roster.Count + " bots, " + (animTouch ? "phone tier" : "desktop tier"));
            run.Metric("filmFpsP5_" + (act + 1), Mathf.Round(fps5 * 10f) / 10f);
            if (animTouch && fps5 > 0f && fps5 < 30f) {
                run.Problem(PlaytestSeverity.Bug, "film-light: act " + (act + 1) + " held only " + fps5.ToString("0.0") + " fps on the phone (5th percentile)");
            }
            if (act == 1 && low >= 0f) {
                rimMin = low;
            }
            if (act == 1 && low >= 0f && low < 0.1f) {
                run.Problem(PlaytestSeverity.Bug, "film-light: in act 2 a character shows no rim against the background (edge contrast " + low.ToString("0.00") + ")");
            }
            if (animTouch && FilmLight.ShadowMode != 0) {
                run.Problem(PlaytestSeverity.Bug, "film-light: the phone tier still has real-time shadows");
            }
        }
        float[] hue = new float[3];
        for (int i = 0; i < 3; i++) {
            Color.RGBToHSV(keys[i], out float hh, out _, out _);
            hue[i] = hh * 360f;
            run.Metric("light.keyHue_" + (i + 1), Mathf.Round(hue[i]));
        }
        float h12 = Mathf.Abs(Mathf.DeltaAngle(hue[0], hue[1])), h23 = Mathf.Abs(Mathf.DeltaAngle(hue[1], hue[2])), h13 = Mathf.Abs(Mathf.DeltaAngle(hue[0], hue[2]));
        run.Metric("light.keyHueSpread", Mathf.Round(Mathf.Min(h12, Mathf.Min(h23, h13))));
        run.Event("film-hues", "key hue act 1 " + hue[0].ToString("0") + ", act 2 " + hue[1].ToString("0") + ", act 3 " + hue[2].ToString("0") + " degrees; closest pair " + Mathf.Min(h12, Mathf.Min(h23, h13)).ToString("0"));
        if (Mathf.Min(h12, Mathf.Min(h23, h13)) < 40f) {
            run.Problem(PlaytestSeverity.Bug, "film-light: two acts' key hues are under 40 degrees apart");
        }
        float d12 = ArcadeActs.SignatureDistance(keys[0], keys[1]);
        float d23 = ArcadeActs.SignatureDistance(keys[1], keys[2]);
        run.Metric("filmKeyDistance_1_2", Mathf.Round(d12 * 100f) / 100f);
        run.Metric("filmKeyDistance_2_3", Mathf.Round(d23 * 100f) / 100f);
        if (rimMin < 998f) {
            run.Metric("filmRimMin", Mathf.Round(rimMin * 100f) / 100f);
        }
        run.Event("film-keys", "key light colours: act 1 " + keys[0] + ", act 2 " + keys[1] + ", act 3 " + keys[2]);
        if (d12 < 0.5f || d23 < 0.5f) {
            run.Problem(PlaytestSeverity.Bug, "film-light: the three acts' key lights are not different enough (" + d12.ToString("0.00") + ", " + d23.ToString("0.00") + ")");
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
    }
}
