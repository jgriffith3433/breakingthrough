using Hawkes.Playtest;
using System.Collections.Generic;
using UnityEngine;

// grade.cardDeltaE: how far a place's first play frame is from its loading card (CardMatch.DeltaE, CIE76 in Lab between the card's
// mean colour and the mean colour of the camera's frame through the real grade). One reading per chapter and the hub; the
// criterion is under CardMatch.Budget for each. Called by the transitions scenario (a phone run, so the phone tier's grade).
public static partial class Playtest {
    static readonly Dictionary<string, float> cardDeltas = new Dictionary<string, float>();

    static void CardDelta(string id) {
        Camera cam = Camera.main;
        if (cam == null || !CardMatch.Known(id)) {
            run.Problem(PlaytestSeverity.Bug, "card-match: no camera or no card palette for " + id);
            return;
        }
        RenderTexture rt = null;
        Texture2D tex = null;
        Color mean = CardMatch.MeanOfFrame(cam, ref rt, ref tex);
        Object.Destroy(tex);
        rt.Release();
        Object.Destroy(rt);
        Color card = CardMatch.TargetOf(id);
        float de = CardMatch.DeltaE(mean, card);
        cardDeltas[id] = de;
        run.Metric("grade.cardDeltaE." + id, Mathf.Round(de * 10f) / 10f);
        run.Event("card-delta", id + ": grade.cardDeltaE " + de.ToString("0.0") + " (card " + card.r.ToString("0.00") + "/" + card.g.ToString("0.00") + "/" + card.b.ToString("0.00")
            + ", frame " + mean.r.ToString("0.00") + "/" + mean.g.ToString("0.00") + "/" + mean.b.ToString("0.00") + ", " + UiFit.Mode + " " + Screen.width + "x" + Screen.height + ")");
        if (de >= CardMatch.Budget) {
            run.Problem(PlaytestSeverity.Bug, "card-match: " + id + " first play frame is " + de.ToString("0.0") + " from its loading card (want under " + CardMatch.Budget + ")");
        }
    }

    static void FinishCardDelta() {
        if (cardDeltas.Count == 0) {
            return;
        }
        float sum = 0f, worst = 0f;
        foreach (float v in cardDeltas.Values) {
            sum += v;
            worst = Mathf.Max(worst, v);
        }
        run.Metric("grade.cardDeltaE", Mathf.Round(sum / cardDeltas.Count * 10f) / 10f);
        run.Metric("grade.cardDeltaE.worst", Mathf.Round(worst * 10f) / 10f);
        run.Event("card-delta-summary", cardDeltas.Count + " places, mean " + (sum / cardDeltas.Count).ToString("0.0") + ", worst " + worst.ToString("0.0"));
        cardDeltas.Clear();
    }
}
