using UnityEngine;
using UnityEngine.UI;

// The choice page of the chapter cutscene (a partial of Cutscene). "Q|<id>" in a scene stops on the row's prompt with two
// buttons above the dialogue box. 1 and 2 (or the keypad), Left/Right then Space, E or Enter-free confirm, a click or a tap
// choose; no other key turns the page, and Enter or Skip still leaves the whole scene (nothing is saved for a skipped choice).
// The pick is stored in the open case file (Choices.Pick) and the option's Now lines play next, then the scene goes on.
public partial class Cutscene {
    public bool Choosing { get { return choosing; } }
    public string ChoiceId { get { return choiceRow != null ? choiceRow.Id : ""; } }
    public string ChoiceLabel(int option) { return choiceLabels != null && option >= 0 && option < 2 ? choiceLabels[option].text : ""; }
    public bool IsReel { get { return shotMode; } }
    public static int ChoicesMade { get; private set; }

    bool choosing;
    Choices.Row choiceRow;
    int choiceFocus;
    Button[] choiceButtons;
    Text[] choiceLabels;
    Image[] choiceImages;

    void BuildChoice() {
        choiceButtons = new Button[2];
        choiceLabels = new Text[2];
        choiceImages = new Image[2];
        for (int i = 0; i < 2; i++) {
            var go = new GameObject("Choice " + (i + 1), typeof(RectTransform));
            go.transform.SetParent(panel, false);
            var image = go.AddComponent<Image>();
            UiSkin.Round(image, 0.6f);
            image.color = new Color(0.28f, 0.22f, 0.12f, 0.96f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            int option = i;
            button.onClick.AddListener(() => Choose(option));
            MenuButtonFx.Add(button);
            Text label = MakeText(go.transform, "", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(14f, 6f);
            label.rectTransform.offsetMax = new Vector2(-14f, -6f);
            choiceButtons[i] = button;
            choiceLabels[i] = label;
            choiceImages[i] = image;
            go.SetActive(false);
        }
        LayoutChoice();
    }

    // Two buttons side by side on the top edge of the dialogue box. Text stays at 20 units or more, buttons at 88 on phones.
    void LayoutChoice() {
        if (choiceButtons == null || panel == null) {
            return;
        }
        bool compact = UiFit.Mode != UiMode.Desktop;
        float width = panel.sizeDelta.x;
        float each = (width - 20f) * 0.5f;
        float height = compact ? (UiFit.Mode == UiMode.Tall ? 130f : 96f) : 104f;
        for (int i = 0; i < 2; i++) {
            var rect = (RectTransform)choiceButtons[i].transform;
            rect.anchorMin = rect.anchorMax = new Vector2(i == 0 ? 0f : 1f, 1f);
            rect.pivot = new Vector2(i == 0 ? 0f : 1f, 0f);
            rect.anchoredPosition = new Vector2(0f, 12f);
            rect.sizeDelta = new Vector2(each, height);
            choiceLabels[i].fontSize = compact ? 28 : 30;
        }
    }

    void ShowChoice(Choices.Row row) {
        choosing = true;
        choiceRow = row;
        choiceFocus = 0;
        page++;
        PagesShown++;
        speaker = "N";
        full = row.Prompt;
        typed = 0f;
        nameText.text = "CHOOSE";
        nameText.color = SpeakerColor("N");
        lineText.text = "";
        lineText.fontStyle = FontStyle.Bold;
        SetPortrait("N");
        readyAt = Time.unscaledTime + PageGap;
        for (int i = 0; i < 2; i++) {
            choiceLabels[i].text = (i + 1) + "   " + row.Labels[i];
            choiceButtons[i].gameObject.SetActive(true);
        }
        PaintChoice();
        hintText.text = UiFit.Touch ? "Tap a choice" : "1 / 2, or click      Left / Right + Space      Enter: skip the scene";
        Events.Add("choice-open|" + row.Id + "|" + row.Labels[0] + "|" + row.Labels[1]);
    }

    void PaintChoice() {
        for (int i = 0; i < 2; i++) {
            choiceImages[i].color = i == choiceFocus ? new Color(0.62f, 0.46f, 0.12f, 0.98f) : new Color(0.28f, 0.22f, 0.12f, 0.96f);
        }
    }

    void TickChoice() {
        if (PlayInput.GetKeyDown(KeyCode.Alpha1) || PlayInput.GetKeyDown(KeyCode.Keypad1)) {
            Choose(0);
        } else if (PlayInput.GetKeyDown(KeyCode.Alpha2) || PlayInput.GetKeyDown(KeyCode.Keypad2)) {
            Choose(1);
        } else if (PlayInput.GetKeyDown(KeyCode.LeftArrow) || PlayInput.GetKeyDown(KeyCode.A)) {
            choiceFocus = 0;
            PaintChoice();
        } else if (PlayInput.GetKeyDown(KeyCode.RightArrow) || PlayInput.GetKeyDown(KeyCode.D)) {
            choiceFocus = 1;
            PaintChoice();
        } else if (PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.E)) {
            Choose(choiceFocus);
        }
    }

    void Choose(int option) {
        if (!choosing || choiceRow == null || Time.unscaledTime < readyAt) {
            return;
        }
        Choices.Row row = choiceRow;
        Choices.Pick(row.Id, option);
        ChoicesMade++;
        Events.Add("choice|" + row.Id + "|" + row.Keys[option]);
        RunAnalytics.Choice(row.Id, row.Keys[option]);
        HideChoice();
        // The option's own lines play now, ahead of the rest of the scene.
        string[] now = row.Now[option];
        var next = new string[script.Length - cursor + now.Length];
        System.Array.Copy(now, 0, next, 0, now.Length);
        System.Array.Copy(script, cursor, next, now.Length, script.Length - cursor);
        script = next;
        cursor = 0;
        pageCount += now.Length;
        NextPage();
    }

    void HideChoice() {
        choosing = false;
        choiceRow = null;
        if (choiceButtons != null) {
            foreach (Button b in choiceButtons) {
                b.gameObject.SetActive(false);
            }
        }
    }
}
