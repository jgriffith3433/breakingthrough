using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "bot-marks" scenario (TraitMark.cs, BotThreat.cs): Arcade played by real input to wave 20 (fingers only on a phone, 390x844),
// then, through the real game camera, each of the eleven traits on a Yo Bot held in front of it for a few seconds (the Sprinter and
// the Blinker run free so their streaks and their flicker can be seen), then each of the eleven bot kinds in a row. Events
// `trait-marker` (one per trait), `kind-ring` (colour and size per kind), metrics marks.traitsSeen, marks.badgeMinPx,
// marks.flickers, marks.streakFrames, marks.crackFrames, marks.kindPairsAlike. The bots are named setup shortcuts, not teleports of the player.
public static partial class Playtest {
    static IEnumerator BotMarksRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Event("marks-setup", (animTouch ? "touch 390x844 phone tier, fingers only" : "keys and mouse") + ", badge at least " + TraitMark.BadgeMinCssPx.ToString("0") + " CSS px");
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 40f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        run.Scene("Marks: wave 1, fighting");
        yield return AnimFight(player, 15f, null);
        run.Scene("Marks: to wave 20");
        run.Event("setup", "testing wave key to wave 20");
        yield return GoWave(20);
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }
        yield return AnimFight(player, 8f, null);
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Release();

        BotTrait[] traits = {
            BotTrait.Sprinter, BotTrait.Hopper, BotTrait.Bruiser, BotTrait.Juker, BotTrait.JumpJets, BotTrait.Splitter,
            BotTrait.Blinker, BotTrait.Heavy, BotTrait.Charger, BotTrait.Swarmer, BotTrait.Juggernaut
        };
        for (int t = 0; t < traits.Length && !run.Done && !WaveDirector.IsLost; t++) {
            BotTrait trait = traits[t];
            run.Scene("Marks: trait " + trait);
            bool free = trait == BotTrait.Sprinter || trait == BotTrait.Blinker;
            Enemy bot = MarkSpawn(BotKind.YoBot, trait, 0f, 8f, !free);
            if (bot == null) {
                run.Problem(PlaytestSeverity.Bug, "could not place a " + trait + " bot");
                continue;
            }
            float until = Time.realtimeSinceStartup + (free ? 6f : 2.5f);
            yield return run.Until(() => (TraitMark.Seen.Contains(trait) && Time.realtimeSinceStartup > until - (free ? 4f : 1.5f))
                || bot == null || Time.realtimeSinceStartup > until + 4f, 20f, "the " + trait + " marker");
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, until - Time.realtimeSinceStartup));
            bool seen = TraitMark.Seen.Contains(trait);
            run.Event("trait-marker", trait + " marker " + (seen ? "drawn" : "MISSING") + " badgeMinPx=" + (TraitMark.BadgeMinPx == float.MaxValue ? 0 : Mathf.Round(TraitMark.BadgeMinPx))
                + " streakFrames=" + TraitMark.StreakFrames + " crackFrames=" + TraitMark.CrackFrames + " flickers=" + TraitMark.Flickers);
            if (!seen) {
                run.Problem(PlaytestSeverity.Bug, "the " + trait + " trait showed no marker");
            }
            if (bot != null) {
                Object.Destroy(bot.gameObject);
            }
        }

        run.Scene("Marks: all eleven traits at once");
        var line = new System.Collections.Generic.List<Enemy>();
        for (int t = 0; t < traits.Length; t++) {
            Enemy b = MarkSpawn(BotKind.YoBot, traits[t], (t - 5) * 1.7f, 9f + (t % 2) * 3f, true);
            if (b != null) line.Add(b);
        }
        yield return new WaitForSecondsRealtime(3f);
        foreach (Enemy b in line) if (b != null) Object.Destroy(b.gameObject);

        run.Scene("Marks: the eleven bot kinds, each with its own ring");
        BotKind[] kinds = (BotKind[])System.Enum.GetValues(typeof(BotKind));
        var kb = new System.Collections.Generic.List<Enemy>();
        for (int k = 0; k < kinds.Length; k++) {
            Enemy b = MarkSpawn(kinds[k], BotTrait.None, (k % 6 - 2.5f) * 3.4f, 10f + (k / 6) * 6f, true);
            if (b != null) kb.Add(b);
        }
        yield return new WaitForSecondsRealtime(4f);
        int alike = 0;
        for (int a = 0; a < kinds.Length; a++) {
            Color ca = BotThreat.KindColour(kinds[a]);
            run.Event("kind-ring", kinds[a] + " colour=#" + ColorUtility.ToHtmlStringRGB(ca) + " size=" + BotThreat.KindSize(kinds[a]).ToString("0.00"));
            for (int b = a + 1; b < kinds.Length; b++) {
                Color cb = BotThreat.KindColour(kinds[b]);
                float dc = Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b);
                float ds = Mathf.Abs(BotThreat.KindSize(kinds[a]) - BotThreat.KindSize(kinds[b]));
                if (dc < 0.45f && ds < 0.08f) {
                    alike++;
                    run.Problem(PlaytestSeverity.Bug, kinds[a] + " and " + kinds[b] + " have rings that look alike (colour gap " + dc.ToString("0.00") + ", size gap " + ds.ToString("0.00") + ")");
                }
            }
        }
        foreach (Enemy b in kb) if (b != null) Object.Destroy(b.gameObject);

        run.Metric("marks.traitsSeen", TraitMark.Seen.Count);
        run.Metric("marks.badgeMinPx", Mathf.Round(TraitMark.BadgeMinPx == float.MaxValue ? 0f : TraitMark.BadgeMinPx));
        run.Metric("marks.flickers", TraitMark.Flickers);
        run.Metric("marks.streakFrames", TraitMark.StreakFrames);
        run.Metric("marks.crackFrames", TraitMark.CrackFrames);
        run.Metric("marks.kindPairsAlike", alike);
        if (TraitMark.Flickers == 0) {
            run.Problem(PlaytestSeverity.Warning, "no Blinker flickered before a jump in this run");
        }
        if (TraitMark.StreakFrames == 0) {
            run.Problem(PlaytestSeverity.Bug, "no Sprinter streak was drawn");
        }
        if (animTouch && TraitMark.BadgeMinPx < 30f && TraitMark.BadgeMinPx > 0f) {
            run.Problem(PlaytestSeverity.Bug, "a trait badge was only " + TraitMark.BadgeMinPx.ToString("0") + " px across at 390x844 (want 30+)");
        }
        run.Finish(PlaytestOutcome.Done);
    }

    // A bot of a kind and trait put ahead of the game camera (right = metres to the camera's right, ahead = metres in front), on the ground.
    static Enemy MarkSpawn(BotKind kind, BotTrait trait, float right, float ahead, bool held) {
        Camera cam = Camera.main;
        if (cam == null || WaveDirector.Instance == null) {
            return null;
        }
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
        Vector3 rt = Vector3.Cross(Vector3.up, fwd);
        Vector3 spot = cam.transform.position + fwd * ahead + rt * right;
        RaycastHit hit;
        spot.y = Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out hit, 80f, 1 << 3, QueryTriggerInteraction.Ignore) ? hit.point.y + 0.3f : spot.y - 4f;
        Enemy e = WaveDirector.Instance.SpawnMarked(kind, trait, spot);
        if (e != null && held) {
            e.Hold(60f);
        }
        return e;
    }
}
