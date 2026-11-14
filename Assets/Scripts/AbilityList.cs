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
    string cachedEvent = "";
    int eventLen;
    CanvasScaler scaler;
    UiMode laidOut;
    int laidStamp = -1;

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
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = UiSkin.Font(UiSkin.FontRole.Label);
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
        Layout();
    }

    // Phones list the abilities on one wrapped line under the coin line, to leave room for the boons.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler);
        cachedWave = -2;
        RectTransform rect = body.rectTransform;
        if (laidOut == UiMode.Desktop) {
            body.fontSize = 24;
            body.horizontalOverflow = HorizontalWrapMode.Overflow;
            rect.anchoredPosition = new Vector2(36f, -84f);   // the event line (when there is one) is the first line
            rect.sizeDelta = new Vector2(420f, 252f);
        } else {
            body.fontSize = 22;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            // The panel owns the whole slot the event line used to take: it ends where the boon list (RunHud) begins.
            rect.anchoredPosition = laidOut == UiMode.Tall ? new Vector2(24f, -340f) : new Vector2(24f, -96f);
            rect.sizeDelta = laidOut == UiMode.Tall ? new Vector2(300f, 144f) : new Vector2(360f, 118f);
        }
    }

    void Update() {
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        if (Time.timeScale == 0f || BossSetPiece.EntranceUp || ChapterColdBlow.Active) {   // a boss's name card owns the screen
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
        // One copy of the event line: not while its mid-screen announcement is up, then here, until the wave ends.
        string ev = WaveDirector.Event != WaveEvent.None && Time.unscaledTime >= WaveDirector.EventAnnouncedUntil
            ? WaveDirector.EventName
            : "";
        if (wave == cachedWave && fresh == cachedFresh && ev == cachedEvent) {
            return;
        }
        cachedWave = wave;
        cachedFresh = fresh;
        cachedEvent = ev;
        body.text = Build(wave, fresh, ev);
    }

    string Build(int wave, bool highlight, string ev) {
        var lines = new StringBuilder();
        if (ev.Length > 0) {
            lines.Append("<color=#D999FF>★ ").Append(ev).Append("</color>\n");
        }
        eventLen = lines.Length;
        Add(lines, wave >= Tuning.JumpWave, Tuning.JumpWave, "Jump land", highlight);
        Add(lines, wave >= MetaProgress.AutoWave(), MetaProgress.AutoWave(), "Auto swing", highlight);
        Add(lines, wave >= MetaProgress.StompWave(), MetaProgress.StompWave(), "Stomp", highlight);
        Add(lines, wave >= PlayerGrowth.ClimbWave, PlayerGrowth.ClimbWave, "Shake off", highlight);
        return lines.ToString();
    }

    void Add(StringBuilder lines, bool unlocked, int atWave, string name, bool highlight) {
        if (!unlocked) {
            return;
        }
        bool inline = laidOut != UiMode.Desktop;
        if (lines.Length == eventLen) {
            lines.Append(inline ? "<color=#C9B48A>Abilities:</color> " : "<color=#C9B48A>Abilities</color>");
        } else if (inline) {
            lines.Append("<color=#C9B48A>  ·  </color>");
        }
        bool justOn = highlight && atWave >= freshFrom;
        string color = justOn ? "FFE08A" : "FFF8E6";
        if (inline) {
            lines.Append("<color=#").Append(color).Append(">").Append(name).Append("</color>");
            return;
        }
        lines.Append("\n<color=#").Append(color).Append(">·  ").Append(name).Append("</color>");
    }
}
