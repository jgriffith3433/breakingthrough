using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "characters" scenario (Docs/CharacterModels.md): opens the CharacterGallery (a named setup shortcut: a scene built in code),
// logs the character validation report (triangles per character, primitives, budgets), and photographs the row of every
// character in colour and as black-fill silhouettes, then the heroes, bots, bosses and the hub's lodgers, each stop held long
// enough for the idle behaviours to show. Events: character-report, character, photo, silhouette-size, characters-done.
public static class PlaytestCharacters {
    public static IEnumerator Run(PlaytestRun run) {
        run.Event("setup", "characters: opening the character gallery");
        KitReport report = CharacterValidation.Run();
        foreach (string line in report.Lines) {
            run.Event("character-report", line);
        }
        run.Event("character-summary", report.Summary());
        run.Metric("characterPrimitives", (float)report.Primitives);
        run.Metric("characterMaxTriangles", (float)report.MaxPieceTriangles);
        foreach (string f in report.Failures) {
            run.Problem(PlaytestSeverity.Bug, "character-validation: " + f);
        }
        CharacterGallery gallery = CharacterGallery.Open();
        yield return null;
        yield return new WaitForSecondsRealtime(0.6f);
        if (gallery.Entries.Count < 14) {
            run.Problem(PlaytestSeverity.Bug, "characters: only " + gallery.Entries.Count + " of 14 characters stand in the gallery");
        }
        foreach (CharacterGallery.Entry e in gallery.Entries) {
            run.Event("character", e.Id + " group=" + e.Group + " triangles=" + e.Triangles + " idle=" + e.Idle);
        }
        run.Metric("characterCount", (float)gallery.Entries.Count);
        int photos = 0;
        for (int i = 0; i < gallery.Shots.Count; i++) {
            gallery.Go(i);
            CharacterGallery.Shot s = gallery.Shots[i];
            yield return null;
            run.Scene("characters-" + s.Name);
            run.Event("photo", s.Name + " silhouette=" + s.Silhouette + " visibleTriangles=" + gallery.LastVisibleTriangles);
            if (s.Name == "all-silhouette") {
                foreach (CharacterGallery.Entry e in gallery.Entries) {
                    float px = gallery.ScreenHeight(e);
                    run.Event("silhouette-size", e.Id + " pixels=" + px.ToString("0"));
                    if (px < 40f) {
                        run.Problem(PlaytestSeverity.Warning, "characters: " + e.Id + " is only " + px.ToString("0") + " px tall in the wide shot");
                    }
                }
            }
            photos++;
            // long enough to see the idle behaviours move
            yield return new WaitForSecondsRealtime(s.Name == "all" || s.Name.EndsWith("silhouette") ? 1.6f : 2.6f);
        }
        run.Metric("characterPhotos", (float)photos);
        gallery.SetSilhouette(false);
        gallery.Close();
        run.Event("characters-done", "photos=" + photos);
    }
}
