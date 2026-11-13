using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The chapter cold blow (J's critic, "first success never"): chapters 2-6 used to open on 30 s of cinematic with no fight in it. Now the
// first thing the chapter shows, as soon as its map is up, is Wallit's bat dropping a Yo Bot from a fixed high 3/4 camera: the
// hit-flash and plates, the damage number (Enemy.Damage -> UpgradeLook.Number) and a kill count plate that ticks 0 -> 1. It lasts
// about 4 s, then the directed opening plays as before. Arcade and Chapter 1 (LevelYard.FirstBlow) never come here.
public static class ChapterColdBlow {
    public static bool Active { get; private set; }
    public static bool Done { get; private set; }          // this chapter's cold open ran to its kill
    public static float KillSeconds { get; private set; }  // seconds from the camera going fixed to the bot falling
    public static float StartedAt { get; private set; }    // realtime of the camera going fixed
    public static int Numbers { get; private set; }        // blows that put a damage number up
    public static int KillCount { get; private set; }      // what the plate shows
    public static string PlateText { get; private set; } = "";
    public static string CameraKind { get; private set; } = "";

    public static void Reset() {
        Active = false;
        Done = false;
        StartedAt = 0f;
        KillSeconds = 0f;
        Numbers = 0;
        KillCount = 0;
        PlateText = "";
        CameraKind = "";
    }

    // Runs inside ChapterRun's opening coroutine. `forward` is the way the chapter heads (marker, else Wallit's facing).
    public static IEnumerator Run(ChapterRun host, Transform wallit, Vector3 forward) {
        Reset();
        if (wallit == null || WaveDirector.Instance == null) {
            yield break;
        }
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : wallit.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 origin = wallit.position;
        Enemy bot = WaveDirector.Instance.SpawnPlaced(BotKind.YoBot, origin + forward * 5.5f + side * 1.8f);
        if (bot == null) {
            yield break;
        }
        bot.Dormant = false;
        bot.HitsToDrop = 1;
        Active = true;
        StartedAt = Time.realtimeSinceStartup;

        // Fixed high 3/4 view: up and behind Wallit, looking at the ground between him and the bot. It never moves.
        Vector3 look = origin + forward * 2.6f + Vector3.up * 0.8f;
        Vector3 camPos = origin - forward * 5.5f + side * -2.2f + Vector3.up * 8.5f;
        Quaternion camRot = Quaternion.LookRotation(look - camPos, Vector3.up);
        ZeldaStyleCamera cam = Object.FindAnyObjectByType<ZeldaStyleCamera>();
        if (cam != null) {
            cam.CinematicHold(camPos, camRot);
            CameraKind = "fixed-3/4 pitch=" + camRot.eulerAngles.x.ToString("0");
        }

        GameObject plate = BuildPlate(host.transform, out Text plateLabel);
        SetPlate(plateLabel, 0, false);
        BatEquipment.OpeningAuto = true;
        ChapterRun.Events.Add("cold-open-start|" + CameraKind);

        int kills0 = RunLog.RunKills;
        float t0 = Time.realtimeSinceStartup;
        bool landed = false;
        while (bot != null && bot.Alive && Time.realtimeSinceStartup - t0 < 7f) {
            if (cam != null) {
                cam.CinematicHold(camPos, camRot);
            }
            if (!landed && bot.BlowsTaken > 0) {
                landed = true;
                Numbers++;
            }
            yield return null;
        }
        BatEquipment.OpeningAuto = false;
        bool down = bot == null || !bot.Alive;
        KillSeconds = Time.realtimeSinceStartup - t0;
        if (down) {
            Numbers = Mathf.Max(Numbers, 1);
            KillCount = Mathf.Max(1, RunLog.RunKills - kills0);
            SetPlate(plateLabel, KillCount, true);
            Vector3 at = bot != null ? bot.transform.position : origin + forward * 2.5f;
            Juice.HitStop(0.08f);
            Fx.Ring(at + Vector3.up * 0.5f, 5f, new Color(1f, 0.88f, 0.5f, 0.8f), 0.6f);
            GameAudio.Play("coin", 0.6f, 1.4f);
        } else if (bot != null) {
            Object.Destroy(bot.gameObject);
        }
        Done = down;
        ChapterRun.Events.Add("cold-open-kill|" + (down ? "down" : "still up") + " after " + KillSeconds.ToString("0.0") + " s");
        // Hold on the fallen bot and the count, the camera still fixed; the opening's first shot takes the view from here.
        float hold = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - hold < 1.1f) {
            if (cam != null) {
                cam.CinematicHold(camPos, camRot);
            }
            yield return null;
        }
        if (plate != null) {
            Object.Destroy(plate);
        }
        Active = false;
    }

    static void SetPlate(Text label, int count, bool flash) {
        PlateText = "YO BOTS DOWN  " + count;
        if (label != null) {
            label.text = PlateText;
            label.color = flash ? new Color(1f, 0.82f, 0.38f) : new Color(0.92f, 0.95f, 1f);
        }
    }

    // A small plate at the top middle under the musician's bar. Phone-safe: UiFit scales the canvas, text stays 22+ units.
    static GameObject BuildPlate(Transform parent, out Text label) {
        var canvasGo = new GameObject("Cold Open Canvas");
        canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 142;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler, 0.5f);

        var back = new GameObject("Kill Count", typeof(RectTransform));
        back.transform.SetParent(canvasGo.transform, false);
        var image = back.AddComponent<Image>();
        UiSkin.Round(image, 0.5f);
        image.color = new Color(0.05f, 0.06f, 0.1f, 0.78f);
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -150f);
        rect.sizeDelta = new Vector2(520f, 84f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(back.transform, false);
        label = textGo.AddComponent<Text>();
        label.font = UiSkin.Font(UiSkin.FontRole.Label);
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        return canvasGo;
    }
}
