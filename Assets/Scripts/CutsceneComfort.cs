using UnityEngine;
using UnityEngine.UI;

// Comfort for the campaign's scenes (a partial of Cutscene): gamepad buttons, the pause key, and the ComfortOptions options.
//   Gamepad: A turns the page / steps a reel, Y skips the scene, X and Y pick choice 1 and 2 (CutsceneChoice), the stick or
//            d-pad moves the choice and A confirms, Start pauses (PauseMenu then offers Skip scene and Restart chapter).
//   Touch:   tap turns the page, the Skip button skips, the choice buttons are tapped.
//   Options: ComfortOptions.SubtitleScale grows the dialogue box and the reel subtitles, HighContrast gives them a solid
//            backing and white text, ReducedMotion turns off the camera sweep (Shoot) and the reels' pan and zoom (MoveCamera).
public partial class Cutscene {
    public static int PadSkips { get; private set; }
    public static int PausesOver { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;   // the frame a scene ended, so the key that skipped it doesn't also pause

    public bool SubtitleBacked { get { return subtitleBack != null && subtitleBack.enabled; } }
    public int SubtitleFontSize { get { return subtitle != null ? subtitle.fontSize : 0; } }
    public int LineFontSize { get { return lineText != null ? lineText.fontSize : 0; } }

    Image subtitleBack;
    int comfortStamp = -1;
    float stickGate;

    // Runs first in an open scene's tick. True: leave the frame alone (the pause menu is up, or the key was the pause key).
    bool ComfortTick() {
        if (comfortStamp != ComfortOptions.Stamp) {
            comfortStamp = ComfortOptions.Stamp;
            Layout();
            LayoutSubtitle();
        }
        if (PauseMenu.IsOpen) {
            return true;
        }
        if (PlayInput.GetKeyDown(KeyCode.P) || MenuNav.PadStart) {
            PausesOver++;   // PauseMenu opens on the same key; this scene must not also turn a page with it
            return true;
        }
        return false;
    }

    // The pause menu's Skip scene.
    public void SkipFromPause() {
        Close(true);
    }

    // The pad's Y skips the scene, except on a choice page where it picks the second option.
    bool PadSkip() {
        if (choosing || !PlayInput.GetKeyDown(KeyCode.JoystickButton3)) {
            return false;
        }
        PadSkips++;
        return true;
    }

    // Left/right on the stick or d-pad, once per push, and the pad's A and X/Y on a choice page.
    int PadChoiceMove() {
        float x = PlayInput.GetAxis("Horizontal");
        if (Mathf.Abs(x) < 0.6f) {
            stickGate = 0f;
            return 0;
        }
        if (stickGate != 0f) {
            return 0;
        }
        stickGate = Mathf.Sign(x);
        return x < 0f ? -1 : 1;
    }

    // ---------------------------------------------------------------- options

    Color DialogueBack() {
        return ComfortOptions.HighContrast ? new Color(0f, 0f, 0f, 1f) : new Color(0.05f, 0.04f, 0.06f, 0.9f);
    }

    Color DialogueInk() {
        return ComfortOptions.HighContrast ? Color.white : new Color(1f, 0.97f, 0.9f);
    }

    // Called at the end of Layout: size and colour of the dialogue box's text from the options.
    void ApplyComfort() {
        if (panel == null) {
            return;
        }
        var back = panel.GetComponent<Image>();
        if (back != null) {
            back.color = DialogueBack();
        }
        Color ink = DialogueInk();
        lineText.color = ink;
        hintText.color = new Color(ink.r, ink.g, ink.b, ComfortOptions.HighContrast ? 0.95f : 0.7f);
        var outline = lineText.GetComponent<Outline>();
        if (outline != null) {
            outline.effectDistance = ComfortOptions.HighContrast ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);
            outline.effectColor = new Color(0f, 0f, 0f, ComfortOptions.HighContrast ? 1f : 0.85f);
        }
    }

    // The reels' subtitle gets a solid plate behind it in high contrast.
    void UpdateSubtitleBack() {
        if (subtitle == null) {
            return;
        }
        if (subtitleBack == null) {
            var go = new GameObject("Subtitle Plate", typeof(RectTransform));
            go.transform.SetParent(subtitle.transform.parent, false);
            go.transform.SetSiblingIndex(subtitle.transform.GetSiblingIndex());
            subtitleBack = go.AddComponent<Image>();
            subtitleBack.color = Color.black;
            subtitleBack.raycastTarget = false;
            subtitleBack.enabled = false;
        }
        bool want = ComfortOptions.HighContrast && subtitle.gameObject.activeSelf && subtitle.text.Length > 0;
        subtitleBack.enabled = want;
        subtitle.color = DialogueInk();
        if (!want) {
            return;
        }
        RectTransform rect = subtitleBack.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        float width = subtitleRect.sizeDelta.x;
        float height = Mathf.Min(subtitleRect.sizeDelta.y * 1.8f, subtitle.preferredHeight + 28f);
        rect.sizeDelta = new Vector2(width + 40f, height);
        rect.anchoredPosition = subtitleRect.anchoredPosition + new Vector2(0f, -14f);
    }
}
