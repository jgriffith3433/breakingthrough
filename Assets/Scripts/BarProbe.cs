using Hawkes.Playtest;
using UnityEngine;

// What the playtest reads about enemy health bars and trails: bars off the screen or wider than 60 CSS px count as
// ui.offscreenBars / ui.wideBars, and trail meshes that went degenerate count as quality.shardFrames.
public static class BarProbe
{
    public static int Offscreen;
    public static int Wide;
    public static int Seen;
    public static int ShardFrames;
    public static float WidestCss;

    public static void Note(Camera cam, Vector3 at, float cssWidth)
    {
        Seen++;
        WidestCss = Mathf.Max(WidestCss, cssWidth);
        if (cssWidth >= 60f)
            Wide++;
        Vector3 vp = cam.WorldToViewportPoint(at);
        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
            Offscreen++;
    }

    // A trail whose newest points sit on top of each other or whose width is zero draws a black wedge.
    public static bool Degenerate(TrailRenderer trail)
    {
        if (trail == null || !trail.emitting || trail.positionCount < 2)
            return false;
        Vector3 a = trail.GetPosition(trail.positionCount - 1);
        Vector3 b = trail.GetPosition(trail.positionCount - 2);
        return (a - b).sqrMagnitude < 1e-8f || float.IsNaN(a.x) || float.IsNaN(b.x);
    }

    public static void Report(PlaytestRun run)
    {
        run.Metric("ui.offscreenBars", Offscreen);
        run.Metric("ui.wideBars", Wide);
        run.Metric("ui.widestBarCssPx", Mathf.Round(WidestCss));
        run.Metric("quality.shardFrames", ShardFrames);
        run.Event("health-bars", Seen + " bar frames, " + Offscreen + " off screen, " + Wide + " at 60 px or more, widest " + Mathf.Round(WidestCss) + " px; shard frames " + ShardFrames);
        if (Offscreen > 0)
            run.Problem(PlaytestSeverity.Bug, Offscreen + " enemy health bar frames sat off the screen");
        if (Wide > 0)
            run.Problem(PlaytestSeverity.Bug, Wide + " enemy health bar frames were 60 px or wider");
    }
}
