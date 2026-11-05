using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;

// The "character-gallery" scenario (Docs/CharacterModels.md): loads the CharacterGallery scene (a named setup shortcut: not
// reachable from the menu), logs the validation report (triangles per character, no primitive bodies), photographs every
// shot (line-up, silhouettes in black fill, groups, Wallit and Slice close), then exercises the rigs: idle fidgets seen on
// every character, and walk, tell, strike, hurt and death on Wallit, Slice, a Yo Bot, a Spider Bot and a boss, each logged
// with the pose energy it produced. No input is faked: the rigs are driven through their own public members.
public static class PlaytestCharacters {
    public static IEnumerator Run(PlaytestRun run) {
        run.Event("setup", "character-gallery: loading the CharacterGallery scene");
        SceneManager.LoadScene(CharacterGallery.SceneName);
        yield return run.Until(() => CharacterGallery.Instance != null && CharacterGallery.Instance.Cam != null, 30f, "the CharacterGallery scene to build");
        CharacterGallery g = CharacterGallery.Instance;
        if (g == null) {
            run.Problem(PlaytestSeverity.Bug, "character-gallery: the scene did not build");
            yield break;
        }
        yield return null;
        CharacterReport report = g.Report;
        foreach (string line in report.Lines) {
            run.Event("character-model", line);
        }
        run.Event("character-report", report.Summary());
        run.Metric("characters", (float)report.Characters);
        run.Metric("characterTriangles", (float)report.Triangles);
        run.Metric("characterPrimitives", (float)report.Primitives);
        foreach (string f in report.Failures) {
            run.Problem(PlaytestSeverity.Bug, "character-validation: " + f);
        }

        int photos = 0;
        for (int i = 0; i < g.Shots.Count; i++) {
            g.Go(i);
            CharacterGallery.Shot s = g.Shots[i];
            run.Scene(s.Name);
            run.Event("photo", s.Name + (s.Black ? " (black fill)" : ""));
            photos++;
            yield return new WaitForSecondsRealtime(1.4f);
        }
        g.SetBlack(false);
        run.Metric("characterPhotos", (float)photos);

        // Idle personality: wait for every character to have played a fidget, then log each.
        g.Go(0);
        yield return new WaitForSecondsRealtime(8f);
        int idle = 0;
        foreach (var kv in g.Rigs) {
            if (kv.Value.Fidgets > 0) {
                idle++;
            } else {
                run.Problem(PlaytestSeverity.Bug, "character-gallery: " + kv.Key + " never fidgeted in idle");
            }
            run.Event("idle", kv.Key + " fidgets=" + kv.Value.Fidgets + " poseEnergy=" + kv.Value.PoseEnergy().ToString("0.0"));
        }
        run.Metric("charactersWithIdle", (float)idle);

        string[] subjects = { "wallit", "slice", "yobot", "spiderbot", "foreman" };
        foreach (string id in subjects) {
            CharacterRig rig;
            if (!g.Rigs.TryGetValue(id, out rig)) {
                continue;
            }
            int shot = id == "wallit" ? 5 : id == "slice" ? 6 : id == "foreman" || id == "spiderbot" ? 3 : 2;
            g.Go(shot);
            yield return new WaitForSecondsRealtime(0.6f);
            float rest = rig.PoseEnergy();
            rig.Walk(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            float walk = rig.PoseEnergy();
            run.Scene(id + "-walk");
            rig.Walk(0f);
            rig.Tell(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            float tell = rig.PoseEnergy();
            run.Scene(id + "-tell");
            rig.Strike();
            yield return new WaitForSecondsRealtime(0.15f);
            float strike = rig.PoseEnergy();
            yield return new WaitForSecondsRealtime(0.4f);
            rig.HurtFlash();
            yield return new WaitForSecondsRealtime(0.1f);
            float hurt = rig.PoseEnergy();
            run.Scene(id + "-hurt");
            yield return new WaitForSecondsRealtime(0.5f);
            rig.Die();
            yield return new WaitForSecondsRealtime(1f);
            float dead = rig.PoseEnergy();
            run.Scene(id + "-death");
            run.Event("rig", id + " idle=" + rest.ToString("0") + " walk=" + walk.ToString("0") + " tell=" + tell.ToString("0") + " strike=" + strike.ToString("0") + " hurt=" + hurt.ToString("0") + " death=" + dead.ToString("0"));
            if (walk <= rest + 1f) { run.Problem(PlaytestSeverity.Bug, "character-gallery: " + id + " did not animate a walk"); }
            if (tell <= rest + 1f) { run.Problem(PlaytestSeverity.Bug, "character-gallery: " + id + " did not pose a tell"); }
            if (!rig.Dead || dead <= tell) { run.Problem(PlaytestSeverity.Bug, "character-gallery: " + id + " did not play a death"); }
            rig.Revive();
        }
        run.Event("gallery-done", "photos=" + photos + " characters=" + report.Characters);
    }
}
