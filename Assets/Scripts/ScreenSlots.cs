using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The named places on screen where text may sit.
public enum Slot {
    Hud,         // the top band: wave bar, counters, boss bar
    ActBanner,   // the act title and its line
    BossCard,    // the boss name card
    Caption,     // Wallit's, Slice's and the bosses' subtitles
    Toast,       // short notices
    RunEnd       // the run-end sheet's lines
}

// One layout service for them. Each slot has a zone computed from the safe area (UiRects.SafeArea: Screen.safeArea with
// the simulated notch and home bar) for desktop, landscape and portrait. A newcomer asks FitTop for its place: if the
// zone's spot is covered by text that is already up (the HUD, a caption) it shifts clear of it, and a slot that is busy
// (Busy) tells the caller to queue. ScreenSlots owns the caption band's top too (UiFit.PlaceCaption reads it), so the
// banner and the captions are laid out from the same numbers.
// The probe counts pairs of visible text rects that intersect (the playtest's ui.textOverlaps).
public static class ScreenSlots {
    // Units down from the canvas top where the caption band starts on a phone (below the wave counter row).
    public const float CaptionTopTall = 450f;
    public const float CaptionTopWide = 300f;

    // Fractions of the safe area's height, measured down from its top: from, to.
    static Vector2 Band(Slot slot, UiMode mode) {
        switch (mode) {
            case UiMode.Tall:
                switch (slot) {
                    case Slot.Hud: return new Vector2(0f, 0.38f);
                    case Slot.Caption: return new Vector2(0.38f, 0.56f);
                    case Slot.ActBanner: return new Vector2(0.57f, 0.72f);
                    case Slot.Toast: return new Vector2(0.73f, 0.78f);
                    case Slot.RunEnd: return new Vector2(0.25f, 0.75f);
                }
                break;
            case UiMode.Wide:
                switch (slot) {
                    case Slot.Hud: return new Vector2(0f, 0.30f);
                    case Slot.Caption: return new Vector2(0.45f, 0.66f);
                    case Slot.ActBanner: return new Vector2(0.66f, 0.84f);
                    case Slot.Toast: return new Vector2(0.86f, 0.92f);
                    case Slot.RunEnd: return new Vector2(0.2f, 0.8f);
                }
                break;
            default:
                switch (slot) {
                    case Slot.Hud: return new Vector2(0f, 0.15f);
                    case Slot.Caption: return new Vector2(0.78f, 0.90f);
                    case Slot.ActBanner: return new Vector2(0.15f, 0.30f);
                    case Slot.Toast: return new Vector2(0.31f, 0.36f);
                    case Slot.RunEnd: return new Vector2(0.2f, 0.8f);
                }
                break;
        }
        return new Vector2(0f, 1f);
    }

    // The slot's rect in screen pixels (origin bottom left).
    public static Rect Zone(Slot slot) {
        if (slot == Slot.BossCard) {
            // The card's own letterboxed shot: the upper third, as the stamp lands (CinematicDirector.ShowBossCard).
            return new Rect(0f, Screen.height * 0.69f, Screen.width, Screen.height * 0.18f);
        }
        Rect sa = UiRects.SafeArea();
        Vector2 band = Band(slot, UiFit.Mode);
        float top = sa.yMax - sa.height * band.x;
        float bottom = sa.yMax - sa.height * band.y;
        return Rect.MinMaxRect(sa.xMin, bottom, sa.xMax, top);
    }

    // Is the slot taken right now? A newcomer for the act banner queues while the boss card plays or the run is over.
    public static bool Busy(Slot slot) {
        switch (slot) {
            case Slot.BossCard:
                return CinematicDirector.BossOpen || CinematicDirector.BossCardUp;
            case Slot.RunEnd:
                return WaveDirector.IsLost;
            case Slot.ActBanner:
                return ArcadeActs.BannerUp;
            default:
                return false;
        }
    }

    // Screen pixels per canvas unit on a canvas set up by UiFit.Apply (or the desktop 1920 reference).
    public static float UnitScale() {
        return Screen.width / Mathf.Max(1f, UiFit.Size.x);
    }

    // Where the glyphs really are (the layout rect is looser): the preferred size, set by the text's alignment.
    public static Rect GlyphRect(Text t) {
        var c = new Vector3[4];
        t.rectTransform.GetWorldCorners(c);
        Rect r = new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
        float w = Mathf.Min(r.width, t.preferredWidth);
        float h = Mathf.Min(r.height, t.preferredHeight);
        if (t.verticalOverflow == VerticalWrapMode.Overflow) {
            h = t.preferredHeight;
        }
        float x;
        switch (t.alignment) {
            case TextAnchor.UpperLeft:
            case TextAnchor.MiddleLeft:
            case TextAnchor.LowerLeft:
                x = r.xMin;
                break;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                x = r.xMax - w;
                break;
            default:
                x = r.center.x - w * 0.5f;
                break;
        }
        float y;
        switch (t.alignment) {
            case TextAnchor.UpperLeft:
            case TextAnchor.UpperCenter:
            case TextAnchor.UpperRight:
                y = r.yMax - h;
                break;
            case TextAnchor.LowerLeft:
            case TextAnchor.LowerCenter:
            case TextAnchor.LowerRight:
                y = r.yMin;
                break;
            default:
                y = r.center.y - h * 0.5f;
                break;
        }
        return new Rect(x, y, w, h);
    }

    // Visible on-screen text: drawn, readable, on a screen canvas, with something to say, and at least partly on screen.
    public static bool Visible(Text t) {
        if (t == null || !t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.canvasRenderer == null) {
            return false;
        }
        Canvas canvas = t.canvas;
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) {
            return false;
        }
        return t.color.a * t.canvasRenderer.GetInheritedAlpha() > 0.1f;
    }

    // The glyph rects of every visible text outside `exclude` (null: all).
    public static List<Rect> Occupied(Transform exclude) {
        var list = new List<Rect>();
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!Visible(t) || (exclude != null && t.transform.IsChildOf(exclude))) {
                continue;
            }
            Rect g = GlyphRect(t);
            if (g.Overlaps(screen)) {
                list.Add(g);
            }
        }
        return list;
    }

    // The top edge (screen y) for a box `width` x `height` pixels wide and tall, centred on the screen, inside the slot's zone
    // and clear of text already up. It starts at the zone's top and steps down under whatever covers it, then tries
    // upward; `clear` is false only when there is no free spot at all (the box then keeps the zone's top).
    public static float FitTop(Slot slot, float width, float height, Transform exclude, out bool clear) {
        Rect zone = Zone(slot);
        Rect sa = UiRects.SafeArea();
        float gap = 8f * UiRects.PixelsPerPoint;
        float x = Mathf.Clamp(Screen.width * 0.5f - width * 0.5f, sa.xMin, Mathf.Max(sa.xMin, sa.xMax - width));
        List<Rect> others = Occupied(exclude);

        float top = zone.yMax;
        for (int i = 0; i < 10; i++) {
            float lowest;
            if (!Hits(others, x, top, width, height, out lowest, true)) {
                if (top - height >= sa.yMin + gap) {
                    clear = true;
                    return top;
                }
                break;
            }
            top = lowest - gap;
        }
        top = zone.yMax;
        for (int i = 0; i < 10; i++) {
            float highest;
            if (!Hits(others, x, top, width, height, out highest, false)) {
                if (top <= sa.yMax - gap) {
                    clear = true;
                    return top;
                }
                break;
            }
            top = highest + gap + height;
        }
        clear = false;
        return zone.yMax;
    }

    // Does a box with this top cross any rect? Gives the lowest bottom (down) or the highest top (up) among those it crosses.
    static bool Hits(List<Rect> others, float x, float top, float width, float height, out float edge, bool down) {
        var box = new Rect(x, top - height, width, height);
        bool hit = false;
        edge = down ? float.MaxValue : float.MinValue;
        foreach (Rect r in others) {
            if (!r.Overlaps(box)) {
                continue;
            }
            hit = true;
            edge = down ? Mathf.Min(edge, r.yMin) : Mathf.Max(edge, r.yMax);
        }
        return hit;
    }

    // ---- the probe: pairs of visible text rects that intersect ----

    public static int OverlapNow;          // across different screen canvases, this frame
    public static int OverlapSameCanvas;   // inside one canvas, this frame (a HUD's own stacked layers; reported, not counted)
    public static int OverlapMax;
    public static int OverlapSameCanvasMax;
    public static int ProbeFrames;
    public static string OverlapWorst = "";

    // Pairs counted by the last Count(): "a" over "b".
    public static int Count(List<string> describe) {
        var texts = new List<Text>();
        var rects = new List<Rect>();
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!Visible(t)) {
                continue;
            }
            Rect g = GlyphRect(t);
            if (g.width < 1f || g.height < 1f || !g.Overlaps(screen)) {
                continue;
            }
            texts.Add(t);
            rects.Add(g);
        }
        int cross = 0;
        int same = 0;
        for (int i = 0; i < texts.Count; i++) {
            for (int j = i + 1; j < texts.Count; j++) {
                if (!Shrink(rects[i]).Overlaps(Shrink(rects[j]))) {
                    continue;
                }
                if (texts[i].canvas.rootCanvas == texts[j].canvas.rootCanvas) {
                    same++;
                    continue;
                }
                cross++;
                if (describe != null && describe.Count < 6) {
                    describe.Add("'" + texts[i].text.Replace("\n", " ") + "' (" + texts[i].canvas.rootCanvas.name + ") over '"
                        + texts[j].text.Replace("\n", " ") + "' (" + texts[j].canvas.rootCanvas.name + ")");
                }
            }
        }
        OverlapSameCanvas = same;
        OverlapNow = cross;
        return cross;
    }

    // A two-pixel margin, so text that only touches edge to edge is not a collision.
    static Rect Shrink(Rect r) {
        return new Rect(r.x + 2f, r.y + 2f, Mathf.Max(0f, r.width - 4f), Mathf.Max(0f, r.height - 4f));
    }

    public static void ResetProbe() {
        OverlapNow = OverlapSameCanvas = OverlapMax = OverlapSameCanvasMax = ProbeFrames = 0;
        OverlapWorst = "";
    }

    static ScreenSlotsProbe probe;

    // The playtest turns it on: every frame it counts the pairs and keeps the worst.
    public static void StartProbe() {
        ResetProbe();
        if (probe == null) {
            probe = new GameObject("Screen Slots Probe").AddComponent<ScreenSlotsProbe>();
            Object.DontDestroyOnLoad(probe.gameObject);
        }
        probe.enabled = true;
    }

    public static void StopProbe() {
        if (probe != null) {
            probe.enabled = false;
        }
    }
}

// Runs ScreenSlots.Count once a frame while the playtest asks for it.
public class ScreenSlotsProbe : MonoBehaviour {
    void LateUpdate() {
        var why = new List<string>();
        int n = ScreenSlots.Count(why);
        ScreenSlots.ProbeFrames++;
        if (ScreenSlots.OverlapSameCanvas > ScreenSlots.OverlapSameCanvasMax) {
            ScreenSlots.OverlapSameCanvasMax = ScreenSlots.OverlapSameCanvas;
        }
        if (n > ScreenSlots.OverlapMax) {
            ScreenSlots.OverlapMax = n;
            ScreenSlots.OverlapWorst = string.Join("; ", why.ToArray());
        }
    }
}
