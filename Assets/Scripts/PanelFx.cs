using System.Collections.Generic;
using UnityEngine;

// The open/close of the pause menu, Settings and the game-over panel: a short fade and slide with a paper sound, so no panel
// pops. Runs on unscaled time (the game is paused under these panels). Show/Hide replace SetActive on the panel's root. Every
// transition is logged (Log) with its seconds, frames and the biggest alpha step in one frame, for the playtest.
public class PanelFx : MonoBehaviour {
    public const float OpenSeconds = 0.2f;
    public const float CloseSeconds = 0.16f;
    const float Slide = 36f;

    public static readonly List<string> Log = new List<string>();

    CanvasGroup group;
    RectTransform slideTarget;
    Vector2 basePosition;
    string label;
    bool opening;
    bool closing;
    float startedAt;
    int startFrame;
    float lastAlpha;
    float maxStep;
    float duration;

    public static bool Closing(GameObject root) {
        PanelFx fx = root != null ? root.GetComponent<PanelFx>() : null;
        return fx != null && fx.closing;
    }

    // Activates root and eases it in. slide is the part that glides (null: the fade alone).
    public static void Show(GameObject root, string label, RectTransform slide = null) {
        if (root == null) {
            return;
        }
        PanelFx fx = Get(root);
        if (fx.closing && fx.slideTarget != null) {
            fx.slideTarget.anchoredPosition = fx.basePosition;
        }
        root.SetActive(true);
        fx.slideTarget = slide != null ? slide : fx.slideTarget;
        fx.Begin(label, true, OpenSeconds);
    }

    // Eases root out, then deactivates it.
    public static void Hide(GameObject root, string label) {
        if (root == null) {
            return;
        }
        if (!root.activeInHierarchy) {
            root.SetActive(false);
            return;
        }
        PanelFx fx = Get(root);
        if (fx.closing) {
            return;
        }
        fx.Begin(label, false, CloseSeconds);
    }

    static PanelFx Get(GameObject root) {
        PanelFx fx = root.GetComponent<PanelFx>();
        if (fx == null) {
            fx = root.AddComponent<PanelFx>();
            fx.group = root.GetComponent<CanvasGroup>();
            if (fx.group == null) {
                fx.group = root.AddComponent<CanvasGroup>();
            }
        }
        return fx;
    }

    void Begin(string name, bool open, float seconds) {
        label = name;
        opening = open;
        closing = !open;
        duration = seconds;
        startedAt = Time.unscaledTime;
        startFrame = Time.frameCount;
        if (!closing && slideTarget != null) {
            basePosition = slideTarget.anchoredPosition;
        } else if (closing && slideTarget != null && !opening) {
            // basePosition stays as it was when the panel opened.
            if (basePosition == Vector2.zero && slideTarget.anchoredPosition != Vector2.zero) {
                basePosition = slideTarget.anchoredPosition;
            }
        }
        lastAlpha = open ? 0f : 1f;
        maxStep = 0f;
        group.blocksRaycasts = open;
        Apply(open ? 0f : 1f);
        UiSound.Slide(open);
    }

    void Apply(float amount) {
        group.alpha = amount;
        if (slideTarget != null) {
            slideTarget.anchoredPosition = basePosition + Vector2.down * (1f - amount) * Slide;
        }
    }

    void Update() {
        if (!opening && !closing) {
            return;
        }
        float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / duration);
        float eased = t * t * (3f - 2f * t);
        float amount = opening ? eased : 1f - eased;
        Apply(amount);
        maxStep = Mathf.Max(maxStep, Mathf.Abs(amount - lastAlpha));
        lastAlpha = amount;
        if (t < 1f) {
            return;
        }
        bool wasOpening = opening;
        opening = false;
        group.blocksRaycasts = true;
        Log.Add(label + (wasOpening ? " open " : " close ") + (Time.unscaledTime - startedAt).ToString("0.000") + "s "
            + (Time.frameCount - startFrame) + " frames, biggest step " + maxStep.ToString("0.00"));
        if (closing) {
            closing = false;
            if (slideTarget != null) {
                slideTarget.anchoredPosition = basePosition;
            }
            group.alpha = 1f;
            gameObject.SetActive(false);
        } else {
            group.alpha = 1f;
            if (slideTarget != null) {
                slideTarget.anchoredPosition = basePosition;
            }
        }
    }

    void OnDisable() {
        // A panel switched off mid-glide (a scene change) must come back whole.
        if (slideTarget != null && (opening || closing)) {
            slideTarget.anchoredPosition = basePosition;
        }
        opening = false;
        closing = false;
        if (group != null) {
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }
    }
}

// The few interface sounds with variants: Resources/Sfx/<name>1..3 (card flip, stamp, panel slide, the build-up). A variant that
// is not there falls back to an older sound, so a missing file is never silent. Variants never repeat back to back.
public static class UiSound {
    static readonly Dictionary<string, int> last = new Dictionary<string, int>();
    public static readonly List<string> Played = new List<string>();

    public static void Variant(string family, float volume, float pitch, string fallback) {
        int pick = Random.Range(1, 4);
        int before;
        if (last.TryGetValue(family, out before) && before == pick) {
            pick = pick % 3 + 1;
        }
        last[family] = pick;
        string name = family + pick;
        if (Resources.Load<AudioClip>("Sfx/" + name) == null) {
            name = fallback;
        }
        Played.Add(name + " x" + pitch.ToString("0.00"));
        GameAudio.Play(name, volume, pitch);
    }

    public static void Slide(bool open) {
        Variant("panelslide", 0.55f, open ? 1f : 0.85f, "shot");
    }

    public static void Flip(float pitch) {
        Variant("cardflip", 0.7f, pitch, "coin");
    }

    public static void Stamp(float volume, float pitch) {
        Variant("stamp", volume, pitch, "coin");
    }

    public static void BuildUp() {
        Played.Add("buildup");
        if (Resources.Load<AudioClip>("Sfx/buildup") != null) {
            GameAudio.Play("buildup", 0.7f, 1f);
        } else {
            GameAudio.Play("coin", 0.5f, 0.7f);
        }
    }
}
