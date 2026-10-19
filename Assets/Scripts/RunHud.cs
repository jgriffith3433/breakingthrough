using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Level bar, streak counter, boss bar, golden gobbler clock, and the boons you hold.
public class RunHud : MonoBehaviour {
    Font font;
    Image xpFill;
    Text levelText;
    Text comboText;
    GameObject bossRoot;
    Image bossFill;
    Text bossName;
    Text goldenText;
    Text boonText;
    Text eventText;
    float shownXp;
    int boonStamp = -1;
    CanvasScaler scaler;
    Image xpBack;
    Image bossBack;
    float xpWidth = 1236f;
    float bossWidth = 814f;
    int boonLines;
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
        if (FindAnyObjectByType<RunHud>() != null) {
            return;
        }
        new GameObject("Run Hud").AddComponent<RunHud>();
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("Run Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 125;
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        Transform top = canvasGo.transform;

        xpBack = Bar(top, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1240f, 14f), new Color(0.05f, 0.08f, 0.12f, 0.85f));
        xpFill = Bar(xpBack.transform, new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(0f, 10f), new Color(0.45f, 0.92f, 1f, 1f));
        xpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        levelText = Label(top, "", 26, FontStyle.Bold, TextAnchor.LowerRight, new Vector2(0.5f, 0f), new Vector2(-640f, 4f), new Vector2(240f, 34f));
        levelText.rectTransform.pivot = new Vector2(1f, 0f);
        levelText.color = new Color(0.6f, 0.95f, 1f);

        comboText = Label(top, "", 40, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(150f, 40f), new Vector2(400f, 60f));
        comboText.rectTransform.pivot = new Vector2(0f, 0.5f);

        bossRoot = new GameObject("Boss", typeof(RectTransform));
        bossRoot.transform.SetParent(top, false);
        RectTransform bossRect = (RectTransform)bossRoot.transform;
        bossRect.anchorMin = Vector2.zero;
        bossRect.anchorMax = Vector2.one;
        bossRect.offsetMin = Vector2.zero;
        bossRect.offsetMax = Vector2.zero;
        bossName = Label(bossRoot.transform, "", 28, FontStyle.Bold, TextAnchor.LowerCenter, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(900f, 36f));
        bossName.color = new Color(1f, 0.55f, 0.45f);
        bossBack = Bar(bossRoot.transform, new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(820f, 22f), new Color(0.12f, 0.04f, 0.04f, 0.92f));
        bossFill = Bar(bossBack.transform, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(814f, 16f), new Color(0.9f, 0.18f, 0.12f));
        bossFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        bossRoot.SetActive(false);

        goldenText = Label(top, "", 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(900f, 40f));
        goldenText.color = new Color(1f, 0.85f, 0.3f);

        eventText = Label(top, "", 24, FontStyle.Bold, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -84f), new Vector2(600f, 30f));
        eventText.rectTransform.pivot = new Vector2(0f, 1f);

        boonText = Label(top, "", 22, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -300f), new Vector2(420f, 520f));
        boonText.rectTransform.pivot = new Vector2(0f, 1f);
        boonText.supportRichText = true;
        Layout();
    }

    // Desktop is the layout above. Phones: the level bar between the touch stick and buttons (Wide) or above
    // them (Tall), the boss bar under the wave line, and a shorter boon list down the left.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler);
        boonStamp = -1;
        if (laidOut == UiMode.Desktop) {
            boonLines = 0;
            xpWidth = 1236f;
            bossWidth = 814f;
            Place(xpBack.rectTransform, new Vector2(0f, 10f), new Vector2(1240f, 14f));
            Place(levelText, 26, new Vector2(-640f, 4f), new Vector2(240f, 34f), TextAnchor.LowerRight, new Vector2(1f, 0f));
            Place(bossName, 28, new Vector2(0f, -196f), new Vector2(900f, 36f));
            Place(bossBack.rectTransform, new Vector2(0f, -236f), new Vector2(820f, 22f));
            Place(goldenText, 28, new Vector2(0f, -268f), new Vector2(900f, 40f));
            Place(eventText, 24, new Vector2(36f, -84f), new Vector2(600f, 30f));
            Place(boonText, 22, new Vector2(36f, -300f), new Vector2(420f, 520f));
        } else if (laidOut == UiMode.Wide) {
            Vector2 size = UiFit.Size;
            float bar = Mathf.Max(300f, size.x - 660f);
            float middle = Mathf.Max(360f, size.x - 800f);
            boonLines = 5;
            xpWidth = bar - 4f;
            bossWidth = Mathf.Min(814f, middle - 6f);
            Place(xpBack.rectTransform, new Vector2(0f, 12f), new Vector2(bar, 14f));
            Place(levelText, 24, new Vector2(-bar * 0.5f - 10f, 4f), new Vector2(120f, 32f), TextAnchor.LowerRight, new Vector2(1f, 0f));
            Place(bossName, 24, new Vector2(0f, -200f), new Vector2(middle, 32f));
            Place(bossBack.rectTransform, new Vector2(0f, -236f), new Vector2(bossWidth + 6f, 22f));
            Place(goldenText, 24, new Vector2(0f, -262f), new Vector2(middle, 32f));
            Place(eventText, 22, new Vector2(24f, -96f), new Vector2(360f, 30f));
            Place(boonText, 20, new Vector2(24f, -210f), new Vector2(360f, 160f));
        } else {
            Vector2 size = UiFit.Size;
            float bar = size.x - 140f;
            boonLines = 8;
            xpWidth = bar - 4f;
            bossWidth = size.x - 106f;
            Place(xpBack.rectTransform, new Vector2(0f, 450f), new Vector2(bar, 14f));
            Place(levelText, 24, new Vector2(-bar * 0.5f, 468f), new Vector2(160f, 32f), TextAnchor.LowerLeft, new Vector2(0f, 0f));
            Place(bossName, 24, new Vector2(0f, -256f), new Vector2(size.x - 48f, 32f));
            Place(bossBack.rectTransform, new Vector2(0f, -290f), new Vector2(bossWidth + 6f, 22f));
            Place(goldenText, 24, new Vector2(0f, -316f), new Vector2(size.x - 48f, 32f));
            Place(eventText, 22, new Vector2(24f, -340f), new Vector2(300f, 30f));
            Place(boonText, 20, new Vector2(24f, -484f), new Vector2(300f, 220f));
        }
        bossFill.rectTransform.sizeDelta = new Vector2(bossWidth, 16f);
        eventText.horizontalOverflow = laidOut == UiMode.Desktop ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
        boonText.horizontalOverflow = eventText.horizontalOverflow;
    }

    static void Place(RectTransform rect, Vector2 position, Vector2 size) {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Place(Text text, int fontSize, Vector2 position, Vector2 box) {
        text.fontSize = fontSize;
        Place(text.rectTransform, position, box);
    }

    static void Place(Text text, int fontSize, Vector2 position, Vector2 box, TextAnchor align, Vector2 pivot) {
        text.alignment = align;
        text.rectTransform.pivot = pivot;
        Place(text, fontSize, position, box);
    }

    void Update() {
        if (xpFill == null) {
            return;
        }
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        bool hidden = WaveDirector.IsLost;
        float target = RunBoons.LevelProgress;
        shownXp = Mathf.MoveTowards(shownXp, target, Time.unscaledDeltaTime * 2.5f);
        if (target < shownXp - 0.3f) {
            shownXp = target;
        }
        xpFill.rectTransform.sizeDelta = new Vector2(xpWidth * shownXp, 10f);
        levelText.text = hidden ? "" : "Lv " + RunBoons.Level;

        int combo = Combo.Count;
        if (combo >= 5 && !hidden) {
            comboText.text = combo + "x";
            float pop = 1f + Combo.Pop * 0.35f;
            comboText.rectTransform.localScale = Vector3.one * pop;
            float heat = Mathf.Clamp01(combo / 100f);
            Color cool = new Color(1f, 0.95f, 0.75f, 0.9f);
            Color hot = new Color(1f, 0.35f, 0.15f, 1f);
            Color c = Color.Lerp(cool, hot, heat);
            c.a = Mathf.Clamp01(Combo.Left / Tuning.ComboWindow + 0.35f);
            comboText.color = c;
        } else {
            comboText.text = "";
        }

        Enemy boss = null;
        Enemy golden = null;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            if (enemy.Boss && boss == null) {
                boss = enemy;
            }
            if (enemy.Golden && golden == null) {
                golden = enemy;
            }
        }
        bool showBoss = boss != null && !hidden;
        if (bossRoot.activeSelf != showBoss) {
            bossRoot.SetActive(showBoss);
        }
        if (showBoss) {
            bossName.text = boss.Title;
            bossFill.rectTransform.sizeDelta = new Vector2(bossWidth * boss.Health01, 16f);
        }
        goldenText.text = golden != null && !hidden
            ? "Golden Yo Bot!  Catch it:  " + Mathf.CeilToInt(golden.GoldenLeft) + "s"
            : "";

        string ev = WaveDirector.EventName;
        eventText.text = string.IsNullOrEmpty(ev) || hidden ? "" : "★ " + ev;
        eventText.color = new Color(0.85f, 0.6f, 1f);

        int stamp = RunBoons.Picks * 100 + RunBoons.Rerolls;
        if (stamp != boonStamp) {
            boonStamp = stamp;
            boonText.text = BoonLines(boonLines);
        }
    }

    // max > 0 keeps the list to that many boons and counts the rest.
    static string BoonLines(int max) {
        var lines = new StringBuilder();
        int listed = 0;
        int more = 0;
        for (int i = 0; i < RunBoons.All.Length; i++) {
            BoonInfo info = RunBoons.All[i];
            int rank = RunBoons.Rank(info.Id);
            if (rank <= 0) {
                continue;
            }
            if (lines.Length == 0) {
                lines.Append("<color=#8FD8FF>Boons</color>");
            }
            if (max > 0 && listed >= max) {
                more++;
                continue;
            }
            listed++;
            Color tint = RunBoons.RarityColor(info.Rarity);
            string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(tint, Color.white, 0.35f));
            lines.Append("\n<color=#").Append(hex).Append(">").Append(info.Name);
            if (info.MaxRank > 1) {
                lines.Append("  ");
                for (int r = 0; r < info.MaxRank; r++) {
                    lines.Append(r < rank ? "●" : "○");
                }
            }
            lines.Append("</color>");
        }
        if (more > 0) {
            lines.Append("\n<color=#8FD8FF>+").Append(more).Append(" more</color>");
        }
        return lines.ToString();
    }

    Image Bar(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color) {
        var go = new GameObject("Bar");
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        UiSkin.Bar(image);
        image.color = color;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    Text Label(Transform parent, string value, int size, FontStyle style, TextAnchor align, Vector2 anchor, Vector2 position, Vector2 box) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = align;
        text.color = new Color(1f, 0.97f, 0.9f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        var rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        return text;
    }
}
