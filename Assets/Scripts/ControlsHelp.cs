using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ControlsHelp : MonoBehaviour {
    const string Controls =
        "Mouse looks around\n" +
        "WASD moves\n" +
        "Space jumps\n" +
        "Click shoots\n\n" +
        "A nearby turkey takes your aim.\n" +
        "Keep the musician alive. Turkeys want the piano.\n" +
        "Before waves 5, 15, 25... you get 30 seconds to build.\n" +
        "1 wall, 2 spikes, 3 tower. Q and E turn. Click places.\n" +
        "Hens drop a coin every 10 to 20.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        var existing = FindAnyObjectByType<ControlsHelp>();
        if (existing != null) {
            return;
        }

        var go = new GameObject("Controls Help");
        go.AddComponent<ControlsHelp>();
    }

    void Awake() {
        var canvasGo = new GameObject("Controls Help Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var panel = new GameObject("Controls");
        panel.transform.SetParent(canvasGo.transform, false);
        var image = panel.AddComponent<Image>();
        image.color = new Color(0.04f, 0.05f, 0.07f, 0.72f);
        image.raycastTarget = false;
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(24f, 24f);
        panelRect.sizeDelta = new Vector2(640f, 360f);

        var body = new GameObject("Text");
        body.transform.SetParent(panel.transform, false);
        var text = body.AddComponent<Text>();
        text.font = font;
        text.text = Controls;
        text.fontSize = 26;
        text.alignment = TextAnchor.UpperLeft;
        text.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = body.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(18f, 14f);
        bodyRect.offsetMax = new Vector2(-18f, -14f);
    }
}
