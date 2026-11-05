using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;

// The "kit-preview" scenario (Docs/ModelKit.md): loads the KitPreview scene (a named setup shortcut: the scene is not
// reachable from the menu), logs the kit validation report, then takes a photo at every stop: the street corner vignette,
// the interior room vignette, and the gallery (every piece on a labelled grid, a few pieces to a photo). Each stop is a
// run.Scene (the runner screenshots at each) plus a `photo` event naming the stop and its visible triangle count.
public static class PlaytestKit {
    public static IEnumerator Run(PlaytestRun run) {
        run.Event("setup", "kit-preview: loading the KitPreview scene");
        SceneManager.LoadScene(KitPreview.SceneName);
        yield return run.Until(() => KitPreview.Instance != null && KitPreview.Instance.Cam != null, 30f, "the KitPreview scene to build");
        KitPreview preview = KitPreview.Instance;
        if (preview == null) {
            run.Problem(PlaytestSeverity.Bug, "kit-preview: the KitPreview scene did not build");
            yield break;
        }
        yield return null;
        KitReport report = preview.Report;
        foreach (string line in report.Lines) {
            if (line.StartsWith("palette ")) {
                run.Event("kit-palette", line.Substring(8));
            }
        }
        run.Event("kit-report", report.Summary());
        run.Metric("kitPieces", (float)(report.Pieces));
        run.Metric("kitTriangles", (float)(report.Triangles));
        run.Metric("kitVertices", (float)(report.Vertices));
        run.Metric("kitTextureMB", (float)(System.Math.Round(report.TextureMB, 2)));
        run.Metric("kitPrimitives", (float)(report.Primitives));
        run.Metric("kitMaxPieceTriangles", (float)(report.MaxPieceTriangles));
        run.Metric("kitTriangleBudget", (float)(KitValidation.MaxVisibleTriangles));
        run.Metric("kitTextureBudgetMB", (float)(KitValidation.MaxTextureBytes / 1048576));
        run.Metric("kitVignettePieces", (float)(preview.VignettePieces));
        run.Metric("kitGalleryPieces", (float)(preview.GalleryPieces));
        foreach (string f in report.Failures) {
            run.Problem(PlaytestSeverity.Bug, "kit-validation: " + f);
        }
        if (report.Pieces < KitValidation.MinPieces) {
            run.Problem(PlaytestSeverity.Bug, "kit-preview: only " + report.Pieces + " kit pieces");
        }
        int photos = 0;
        for (int i = 0; i < preview.Shots.Count; i++) {
            preview.Go(i);
            KitPreview.Shot s = preview.Shots[i];
            run.Scene(s.Name);
            run.Event("photo", s.Name + " zone=" + s.Zone + " palette=" + s.Palette + " pieces=" + s.Pieces + " visibleTriangles=" + preview.LastVisibleTriangles);
            if (preview.LastVisibleTriangles > KitValidation.MaxVisibleTriangles) {
                run.Problem(PlaytestSeverity.Bug, "kit-preview: " + s.Name + " shows " + preview.LastVisibleTriangles + " triangles, over " + KitValidation.MaxVisibleTriangles);
            }
            photos++;
            yield return new WaitForSecondsRealtime(1.6f);
        }
        run.Metric("kitPhotos", (float)(photos));
        run.Metric("kitMaxVisibleTriangles", (float)(preview.MaxVisibleTriangles));
        run.Event("vignette-street", "street corner photographed");
        run.Event("vignette-office", "interior room photographed");
        run.Event("kit-done", "photos=" + photos + " maxVisible=" + preview.MaxVisibleTriangles);
    }
}
