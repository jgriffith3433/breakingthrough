using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One or two lines for the rule that matters on this wave or in this mode.
public class ControlsHelp : MonoBehaviour {
    Text line;
    RectTransform lineRect;
    string shown = "";
    float flash;
    readonly Color resting = new Color(1f, 0.97f, 0.9f, 0.96f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<ControlsHelp>() != null) {
            return;
        }
        var go = new GameObject("Controls Help");
        go.AddComponent<ControlsHelp>();
    }

    void Awake() {
        var canvasGo = new GameObject("Lesson");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 180;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        UiSkin.Reference(scaler);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Line");
        go.transform.SetParent(canvasGo.transform, false);
        line = go.AddComponent<Text>();
        line.font = font;
        line.fontSize = 42;
        line.alignment = TextAnchor.MiddleCenter;
        line.color = resting;
        line.raycastTarget = false;
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);
        lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(1400f, 72f);
    }

    void Update() {
        bool building = WaveDirector.IsBuilding;
        string next = building ? BuildLesson() : CombatLesson(Mathf.Max(1, WaveDirector.Wave));
        if (next != shown) {
            shown = next;
            line.text = next;
            flash = 1f;
        }
        lineRect.anchoredPosition = new Vector2(0f, building ? 176f : 36f);
        if (flash > 0f) {
            flash -= Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f);
            line.color = Color.Lerp(resting, new Color(1f, 0.82f, 0.28f, 1f), pulse);
            if (flash <= 0f) {
                line.color = resting;
            }
        }
    }

    static string BuildLesson() {
        BuildMode build = BuildMode.Instance;
        if (build != null && build.IsPlacing) {
            return "Click between the rings.";
        }
        return "Q and E turn. Tab switches.";
    }

    static string CombatLesson(int wave) {
        if (wave >= Tuning.ClimbWave) {
            return "Press F to shake them off.";
        }
        if (wave >= Tuning.StompWave) {
            return "Stomp turkeys at your feet.";
        }
        if (wave >= Tuning.AutoWave) {
            return "The bow fires on its own.";
        }
        if (wave >= Tuning.JumpWave) {
            return "Hold Space to hop. Landings crush turkeys.";
        }
        if (wave >= Tuning.GoldenFirstWave) {
            return "Shift locks on. Chase gold turkeys for treasure.";
        }
        if (wave >= 2) {
            return "Walk over feathers to level up.";
        }
        return UiSkin.Touch ? "Tap Fire to shoot. Lock holds a target." : "Click to shoot. Shift locks on.";
    }
}
