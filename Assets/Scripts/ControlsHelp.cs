using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One or two lines for the rule that matters on this wave or in this mode.
public class ControlsHelp : MonoBehaviour {
    Text line;
    RectTransform lineRect;
    string shown = "";
    float flash;
    CanvasScaler scaler;
    UiMode laidOut;
    int laidStamp = -1;
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
        scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
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
        Layout();
    }

    // Phones: between the touch stick and buttons in landscape, above them in portrait.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        UiFit.Apply(scaler);
        Vector2 size = UiFit.Size;
        if (laidOut == UiMode.Desktop) {
            line.fontSize = 42;
            lineRect.sizeDelta = new Vector2(1400f, 72f);
        } else {
            line.fontSize = 28;
            lineRect.sizeDelta = new Vector2(laidOut == UiMode.Tall ? size.x - 48f : Mathf.Max(300f, size.x - 660f), 72f);
        }
    }

    // Onboarding state, read by the playtest. Seconds are game seconds (paused time does not count).
    public const float HintSeconds = 8f;
    public const float NearReach = 6f;
    public static float PlayedSeconds { get; private set; }
    public static float HintHiddenAt { get; private set; } = -1f;
    public static string HintHiddenWhy { get; private set; } = "";
    public static float FirstNearAt { get; private set; } = -1f;
    public static float FirstNearDistance { get; private set; }
    public static bool ArrowShown { get; private set; }
    public static float ArrowDistance { get; private set; }

    Text arrow;
    RectTransform arrowRect;
    float hintAlpha = 1f;
    bool hintDone;

    void Start() {
        PlayedSeconds = 0f;
        HintHiddenAt = -1f;
        HintHiddenWhy = "";
        FirstNearAt = -1f;
        ArrowShown = false;
        BuildArrow();
    }

    void BuildArrow() {
        var go = new GameObject("Bot Arrow");
        go.transform.SetParent(transform.GetChild(0), false);
        arrow = go.AddComponent<Text>();
        arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrow.fontSize = 72;
        arrow.fontStyle = FontStyle.Bold;
        arrow.text = ">";
        arrow.alignment = TextAnchor.MiddleCenter;
        arrow.color = new Color(1f, 0.82f, 0.28f, 0.95f);
        arrow.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);
        arrowRect = arrow.rectTransform;
        arrowRect.sizeDelta = new Vector2(90f, 90f);
        go.SetActive(false);
    }

    static bool ArcadePlay() {
        return ChapterRun.Instance == null && WaveDirector.Instance != null && !WaveDirector.IsLost
            && !PauseMenu.IsOpen && !StoryScreen.IsOpen && !Cutscene.IsOpen && !LevelUpScreen.IsOpen;
    }

    // Arcade only: while no live bot is on screen, an arrow at the screen edge points to the nearest one.
    void TickArrow() {
        ArrowShown = false;
        Camera cam = Camera.main;
        Transform player = Musician.Instance != null ? Musician.Instance.transform : null;
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        Vector3 from = playerGo != null ? playerGo.transform.position : (player != null ? player.position : Vector3.zero);
        Enemy nearest = null;
        float best = float.MaxValue;
        bool onScreen = false;
        if (cam != null && ArcadePlay() && !WaveDirector.Waiting) {
            foreach (Enemy enemy in Enemy.Roster) {
                if (enemy == null) {
                    continue;
                }
                Vector3 v = cam.WorldToViewportPoint(enemy.transform.position + Vector3.up);
                if (v.z > 0f && v.x > 0.03f && v.x < 0.97f && v.y > 0.03f && v.y < 0.97f) {
                    onScreen = true;
                    break;
                }
                float d = (enemy.transform.position - from).sqrMagnitude;
                if (d < best) {
                    best = d;
                    nearest = enemy;
                }
            }
        }
        if (nearest == null || onScreen) {
            arrow.gameObject.SetActive(false);
            return;
        }
        Vector3 vp = cam.WorldToViewportPoint(nearest.transform.position + Vector3.up);
        Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) {
            dir = -dir;
        }
        if (dir.sqrMagnitude < 0.0001f) {
            dir = Vector2.down;
        }
        dir.Normalize();
        // Out to the edge of a rounded box, kept clear of the touch stick and buttons below.
        float scale = Mathf.Min(0.40f / Mathf.Max(0.001f, Mathf.Abs(dir.x)), 0.36f / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        Vector2 pos = new Vector2(0.5f, 0.5f) + dir * scale;
        arrowRect.anchorMin = pos;
        arrowRect.anchorMax = pos;
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f);
        arrow.color = new Color(1f, 0.82f, 0.28f, pulse);
        arrow.gameObject.SetActive(true);
        ArrowShown = true;
        ArrowDistance = Mathf.Sqrt(best);
    }

    void TickOnboarding() {
        if (!ArcadePlay()) {
            return;
        }
        PlayedSeconds += Time.deltaTime;
        if (FirstNearAt < 0f && Enemy.Roster.Count > 0 && !WaveDirector.Waiting) {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) {
                foreach (Enemy enemy in Enemy.Roster) {
                    if (enemy == null) {
                        continue;
                    }
                    Vector3 flat = enemy.transform.position - playerGo.transform.position;
                    flat.y = 0f;
                    if (flat.magnitude <= NearReach) {
                        FirstNearAt = PlayedSeconds;
                        FirstNearDistance = flat.magnitude;
                        break;
                    }
                }
            }
        }
        // The swing hint teaches, then gets out of the way: first swing, or 8 s, whichever comes first.
        if (!hintDone && WaveDirector.Wave <= 1) {
            if (BatEquipment.Swings > 0 || PlayedSeconds >= HintSeconds) {
                hintDone = true;
                HintHiddenAt = PlayedSeconds;
                HintHiddenWhy = BatEquipment.Swings > 0 ? "first swing" : "8 s";
            }
        }
    }

    void Update() {
        if (laidStamp != UiFit.Stamp) {
            Layout();
        }
        TickOnboarding();
        TickArrow();
        bool building = WaveDirector.IsBuilding;
        bool touch = UiFit.Touch;
        int wave = Mathf.Max(1, WaveDirector.Wave);
        string next = building ? BuildLesson(touch) : CombatLesson(wave, touch);
        // Wave 1's swing hint fades once taught; later lessons show as before.
        bool fading = !building && wave == 1 && hintDone && ChapterRun.Instance == null;
        hintAlpha = Mathf.MoveTowards(hintAlpha, fading ? 0f : 1f, Time.unscaledDeltaTime / 0.6f);
        // Never over the game-over panel.
        line.enabled = hintAlpha > 0.01f && !WaveDirector.IsLost;
        if (next != shown) {
            shown = next;
            line.text = next;
            flash = 1f;
        }
        float y = building ? 176f : 36f;
        if (laidOut == UiMode.Wide) {
            y = building ? 150f : 34f;
        } else if (laidOut == UiMode.Tall) {
            y = building ? 330f : 620f;
        }
        lineRect.anchoredPosition = new Vector2(0f, y);
        if (flash > 0f) {
            flash -= Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f);
            line.color = Color.Lerp(resting, new Color(1f, 0.82f, 0.28f, 1f), pulse);
            if (flash <= 0f) {
                line.color = resting;
            }
        } else {
            line.color = resting;
        }
        Color c = line.color;
        c.a *= hintAlpha;
        line.color = c;
    }

    static string BuildLesson(bool touch) {
        BuildMode build = BuildMode.Instance;
        string how = build != null && build.IsPlacing
            ? (touch ? "Tap between the rings." : "Click between the rings.")
            : (touch ? "Turn rotates. Tap a card to switch." : "Q and E turn. Tab switches.");
        if (build == null) {
            return how;
        }
        return how + "  " + DefencePiece.PlaceLesson(build.Selected);
    }

    static string CombatLesson(int wave, bool touch) {
        if (wave >= Tuning.ClimbWave) {
            return touch ? "Tap Shake to shake them off." : "Press F to shake them off.";
        }
        if (wave >= Tuning.StompWave) {
            return "Stomp Yo Bots at your feet.";
        }
        if (wave >= Tuning.AutoWave) {
            return "The bat swings on its own at bots in reach.";
        }
        if (wave >= Tuning.JumpWave) {
            return touch ? "Hold Jump to hop. Landings crush Yo Bots." : "Hold Space to hop. Landings crush Yo Bots.";
        }
        if (wave >= Tuning.GoldenFirstWave) {
            return touch ? "Lock locks on. Chase the Golden Yo Bot." : "Shift locks on, right click switches. Chase the Golden Yo Bot.";
        }
        if (wave >= 2) {
            return "Walk over bolts to level up.";
        }
        return touch ? "Tap Swing to swing. Hold it for a home run." : "Click to swing. Hold for a home run. Shift locks on.";
    }
}
