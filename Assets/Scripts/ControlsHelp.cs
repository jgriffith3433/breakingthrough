using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One or two lines for the rule that matters on this wave or in this mode.
public class ControlsHelp : MonoBehaviour {
    Text line;
    RectTransform lineRect;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
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
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Line");
        go.transform.SetParent(canvasGo.transform, false);
        line = go.AddComponent<Text>();
        line.font = font;
        line.fontSize = 26;
        line.alignment = TextAnchor.MiddleCenter;
        line.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        line.raycastTarget = false;
        line.horizontalOverflow = HorizontalWrapMode.Wrap;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        lineRect = line.rectTransform;
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(1100f, 88f);
    }

    void Update() {
        bool building = WaveDirector.IsBuilding;
        line.text = building ? BuildLesson() : CombatLesson(Mathf.Max(1, WaveDirector.Wave));
        lineRect.anchoredPosition = new Vector2(0f, building ? 176f : 28f);
    }

    static string BuildLesson() {
        string turn = "Q and E turn the piece. Tab switches.";
        BuildMode build = BuildMode.Instance;
        if (build != null && build.IsPlacing) {
            return turn + "\n" + DefencePiece.PlaceLesson(build.Selected);
        }
        return turn + "\nClick between the rings, away from the piano.";
    }

    static string CombatLesson(int wave) {
        int auto = MetaProgress.AutoWave();
        bool chain = wave >= 20;
        string jump = chain
            ? "Hold Space to keep jumping."
            : "Release Space to jump again. One second between jumps.";
        if (wave == 100) {
            return "The Thunderbird is bigger than you.\nIt takes a lot of arrows.";
        }
        if (wave > 100 && wave % 10 == 0) {
            return "Same birds, all of them larger.\n" + jump;
        }
        if (wave == 70) {
            return "Arrows explode when they hit.\nBoss wave. About your height, and it takes a lot of arrows.";
        }
        if (wave == 50) {
            return "Small turkeys fly at you and climb. Press F.\nJump and stomp stop until you shake them off.";
        }
        if (wave == 40) {
            return "Stomp nearby turkeys, one foot at a time.\nYou stay put. Jump still works. Boss wave.";
        }
        if (wave == 10) {
            return "Boss wave. The Hen is about your height.\nIt takes a lot of arrows. Aim locks the biggest bird.";
        }
        if (wave % 10 == 0 && wave <= 90) {
            return "Boss wave. The new bird is about your height.\nIt takes a lot of arrows. " + jump;
        }
        if (wave > 70) {
            return "Arrows explode when they hit.\n" + jump;
        }
        if (wave > 50) {
            return "Press F if turkeys climb you.\n" + jump;
        }
        if (wave > 40) {
            return "Stomp nearby turkeys. You stay in place.\n" + jump;
        }
        if (wave == auto) {
            return "The bow fires on its own.\n" + jump;
        }
        if (wave == 20) {
            return "Hold Space to keep jumping.\nOne second between jumps. Hold click to shoot.";
        }
        if (wave == 1) {
            return "Hold click to shoot.\n" + jump;
        }
        if (!chain) {
            string aim = wave > 10 ? "Aim stays on the biggest bird. " : "";
            return jump + "\n" + aim + "Hold click to shoot.";
        }
        if (wave < auto) {
            return jump + "\nHold click to shoot.";
        }
        return jump;
    }
}
