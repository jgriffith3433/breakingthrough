using UnityEngine;
using UnityEngine.UI;

public class ControlsHelp : MonoBehaviour {
    const string HintClosed = "Press H for help";
    const string HintOpen = "Press H to close";
    const string HelpBody =
        "Controls\n\n" +
        "W A S D or the arrow keys move. Movement follows the camera, and the character turns to face the direction you are going.\n\n" +
        "Hold F to jump. This only works in the normal follow camera. It does nothing while locked on or in first person.\n\n" +
        "Hold E to aim the camera along the direction the character is facing. Release E to put the camera back behind the character.\n\n" +
        "Press Q to enter first person, and press Q again to leave. You cannot enter first person while locked on. In first person, A and D turn left and right, and W and S look up and down. Letting go of W or S levels the view. You do not walk in this view.\n\n" +
        "Press Space to lock onto the nearest target, if there is one. Press Space again to drop the lock. The lock also drops if you get too far from the target. While locked on, W A S D still move you, and the character keeps facing the target.\n\n" +
        "Standing still for about two seconds makes the camera circle on its own. Walking puts it back behind you.\n\n" +
        "Press H to close this window.";

    GameObject window;
    Text hint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
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

        var hintGo = CreateText(canvasGo.transform, font, HintClosed, 28, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.95f));
        hint = hintGo.GetComponent<Text>();
        var hintRect = hintGo.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(1f, 0f);
        hintRect.anchorMax = new Vector2(1f, 0f);
        hintRect.pivot = new Vector2(1f, 0f);
        hintRect.anchoredPosition = new Vector2(-28f, 24f);
        hintRect.sizeDelta = new Vector2(520f, 48f);
        var hintOutline = hintGo.AddComponent<Outline>();
        hintOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        hintOutline.effectDistance = new Vector2(1.5f, -1.5f);

        window = new GameObject("Controls Window");
        window.transform.SetParent(canvasGo.transform, false);
        var panel = window.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        var panelRect = window.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 760f);

        var body = CreateText(window.transform, font, HelpBody, 24, TextAnchor.UpperLeft, Color.white);
        var bodyText = body.GetComponent<Text>();
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(36f, 28f);
        bodyRect.offsetMax = new Vector2(-36f, -28f);

        window.SetActive(false);
    }

    void Update() {
        if (!Input.GetKeyDown(KeyCode.H)) {
            return;
        }

        var open = !window.activeSelf;
        window.SetActive(open);
        hint.text = open ? HintOpen : HintClosed;
    }

    static GameObject CreateText(Transform parent, Font font, string value, int size, TextAnchor anchor, Color color) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.supportRichText = false;
        return go;
    }
}
