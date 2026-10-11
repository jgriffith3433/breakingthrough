using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Powers that turn on by wave, listed down the left once they are on.
public class AbilityList : MonoBehaviour {
    Text body;
    int shownWave = -1;
    int cachedWave = -2;
    int freshFrom = int.MaxValue;
    float freshUntil;
    bool cachedFresh;

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
        if (FindAnyObjectByType<AbilityList>() != null) {
            return;
        }
        var go = new GameObject("Ability List");
        go.AddComponent<AbilityList>();
    }

    void Awake() {
        var canvasGo = new GameObject("Abilities");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Lines");
        go.transform.SetParent(canvasGo.transform, false);
        body = go.AddComponent<Text>();
        body.font = font;
        body.fontSize = 24;
        body.alignment = TextAnchor.UpperLeft;
        body.color = Color.white;
        body.raycastTarget = false;
        body.supportRichText = true;
        body.horizontalOverflow = HorizontalWrapMode.Overflow;
        body.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        RectTransform rect = body.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(36f, -108f);
        rect.sizeDelta = new Vector2(360f, 280f);
    }

    void Update() {
        if (Time.timeScale == 0f) {
            if (body.text.Length > 0) {
                body.text = "";
                cachedWave = -2;
            }
            return;
        }

        int wave = Mathf.Max(1, WaveDirector.Wave);
        if (wave != shownWave) {
            if (shownWave >= 0 && wave > shownWave) {
                freshFrom = shownWave + 1;
                freshUntil = Time.unscaledTime + 4.5f;
            } else if (wave < shownWave) {
                freshFrom = int.MaxValue;
            }
            shownWave = wave;
        }

        bool fresh = Time.unscaledTime < freshUntil;
        if (wave == cachedWave && fresh == cachedFresh) {
            return;
        }
        cachedWave = wave;
        cachedFresh = fresh;
        body.text = Build(wave, fresh);
    }

    string Build(int wave, bool highlight) {
        var lines = new StringBuilder();
        Add(lines, wave >= 20, 20, "Jump land", highlight);
        Add(lines, wave >= MetaProgress.AutoWave(), MetaProgress.AutoWave(), "Auto shoot", highlight);
        Add(lines, wave >= MetaProgress.StompWave(), MetaProgress.StompWave(), "Stomp", highlight);
        Add(lines, wave >= PlayerGrowth.ClimbWave, PlayerGrowth.ClimbWave, "Shake off", highlight);
        return lines.ToString();
    }

    void Add(StringBuilder lines, bool unlocked, int atWave, string name, bool highlight) {
        if (!unlocked) {
            return;
        }
        if (lines.Length == 0) {
            lines.Append("<color=#C9B48A>Abilities</color>");
        }
        bool justOn = highlight && atWave >= freshFrom;
        string color = justOn ? "FFE08A" : "FFF8E6";
        lines.Append("\n<color=#").Append(color).Append(">·  ").Append(name).Append("</color>");
    }
}
