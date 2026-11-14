using UnityEngine;
using UnityEngine.UI;

// The mode picker of a cold start (J, first five minutes): the page opens straight into wave 1 with the bat in hand, and
// only after the first kill does a small card offer Campaign and Settings beside "Keep fighting". It never pauses the
// run and never covers the stick or Swing: a compact card in the middle band, gone after a few seconds if ignored.
// Combat.OnKill calls Offer on the first kill of an Arcade run; it shows only when the menu was skipped at boot (or a
// scenario sets Armed), so a player who chose Play from the menu is not asked again.
public class ModePicker : MonoBehaviour {
    public const float ShowDelay = 1.6f;
    public const float Lifetime = 14f;

    // Set by MainMenu when it skipped itself on a cold start; a playtest may set it to see the card.
    public static bool Armed;
    public static ModePicker Instance { get; private set; }
    public static int Shown { get; private set; }
    public static float ShownAt { get; private set; }
    public static bool IsUp { get { return Instance != null && Instance.root != null && Instance.root.activeSelf; } }
    public static System.Action<string> OnShown;

    GameObject root;
    float showAt, hideAt;
    int laidStamp = -1;

    public static void Offer() {
        if (!Armed || Instance != null || ChapterRun.Instance != null) {
            return;
        }
        Armed = false;
        var go = new GameObject("Mode Picker");
        Instance = go.AddComponent<ModePicker>();
        Instance.showAt = Time.unscaledTime + ShowDelay;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Update() {
        if (root == null) {
            if (Time.unscaledTime < showAt) {
                return;
            }
            if (LevelUpScreen.IsOpen || PauseMenu.IsOpen || StoryScreen.IsOpen || Cutscene.IsOpen || WaveDirector.IsLost) {
                showAt = Time.unscaledTime + 0.5f;
                return;
            }
            Build();
            hideAt = Time.unscaledTime + Lifetime;
            Shown++;
            ShownAt = FirstMinute.Now;
            RunAnalytics.Screen("mode_picker");
            if (OnShown != null) {
                OnShown("after the first kill, " + (Time.timeScale > 0.01f ? "run still live" : "paused"));
            }
            return;
        }
        if (laidStamp != UiFit.Stamp) {
            Destroy(root);
            Build();
        }
        if (Time.unscaledTime >= hideAt || WaveDirector.IsLost) {
            Dismiss();
        }
    }

    void Dismiss() {
        Destroy(gameObject);
    }

    void Build() {
        laidStamp = UiFit.Stamp;
        root = new GameObject("Mode Picker Canvas");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 330;
        var scaler = root.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);
        root.AddComponent<GraphicRaycaster>();

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        var rt = (RectTransform)card.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.42f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 400f);
        var bg = card.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.07f, 0.1f, 0.88f);
        UiSkin.Round(bg);

        Font font = UiSkin.Font(UiSkin.FontRole.Body);
        MakeLabel(card.transform, font, "Clean kill. Where to?", 28, new Vector2(0f, 158f), new Vector2(400f, 44f));
        MakeButton(card.transform, font, "Keep fighting", new Vector2(0f, 82f), new Color(0.78f, 0.2f, 0.2f, 1f), Dismiss);
        MakeButton(card.transform, font, "Campaign", new Vector2(0f, -16f), new Color(0.2f, 0.24f, 0.34f, 1f), OpenCampaign);
        MakeButton(card.transform, font, "Settings", new Vector2(0f, -114f), new Color(0.2f, 0.24f, 0.34f, 1f), () => SettingsPanel.Open(null));
    }

    void OpenCampaign() {
        MainMenu.OpenCampaignNext = true;
        Dismiss();
        PauseMenu.QuitToMenu();
    }

    static void MakeLabel(Transform parent, Font font, string text, int size, Vector2 pos, Vector2 box) {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
    }

    static void MakeButton(Transform parent, Font font, string label, Vector2 pos, Color fill, UnityEngine.Events.UnityAction act) {
        var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360f, 88f);
        var img = go.GetComponent<Image>();
        img.color = fill;
        UiSkin.Round(img);
        var b = go.GetComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(act);
        MenuButtonFx.Add(b);
        MakeLabel(go.transform, font, label, 32, Vector2.zero, new Vector2(340f, 80f));
    }
}
