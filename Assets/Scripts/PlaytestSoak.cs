using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;

// Scenario "soak": about 20 minutes in one page, to catch the slowdowns and leaks only a long session shows. Arcade from
// wave 1 for 12 minutes (the bot plays and picks cards), back to the menu, then Campaign chapters 1 to 3 with the openings
// skipped (Enter, as in "campaign"). On a phone viewport (390x844) the bot plays with fingers only.
// Every minute the SoakSampler (it survives scene loads) reports fps p5 and average, Unity's reserved/allocated memory, the
// JS heap where the browser has one, and the live particle systems, lights and audio sources: event soak-minute and the
// metrics soak.<name>.<minute>. soak.crashed is 0 while the page answers and 1 when a frame took over 20 s (a hang) or
// the bot lost the page; a page that dies outright sends no more minutes, and the last soak-minute says when.
public static partial class Playtest {
    const float SoakArcadeSeconds = 720f;
    const float SoakCampaignSeconds = 480f;

    static void PrepareSoak() {
        bool touch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(touch);
        if (touch) {
            MobileQuality.Apply();
        }
        SoakSampler.Begin(run);
        // Every mode or chapter change sweeps (SceneMemory): the heap before and after goes on the timeline, and Ch1 against Ch3
        // is compared at the end (the sweep after a chapter is cleared is tagged with the chapters cleared so far).
        SceneMemory.OnSweep = s => {
            int n = SceneMemory.Sweeps.Count;
            run.Event("heap-sweep", s.Reason + " (" + s.Scene + ", cleared " + s.Cleared + "): heap " + s.BeforeMb.ToString("0.0") + " -> " + s.AfterMb.ToString("0.0")
                + " MB (" + (s.AfterMb < s.BeforeMb ? "dropped" : "no drop") + "), JS heap " + s.JsBeforeMb.ToString("0") + " -> " + s.JsAfterMb.ToString("0")
                + " MB, released " + s.Released + " files");
            run.Metric("soak.sweepBeforeMb." + n, Mathf.Round(s.BeforeMb));
            run.Metric("soak.sweepAfterMb." + n, Mathf.Round(s.AfterMb));
        };
    }

    // Heap after the last sweep that followed `cleared` finished chapters (-1 when there was none).
    static float HeapAfterChapters(int cleared) {
        float found = -1f;
        foreach (SceneMemory.Sweep s in SceneMemory.Sweeps) {
            if (s.Cleared == cleared) {
                found = s.JsAfterMb > 0f ? s.JsAfterMb : s.AfterMb;
            }
        }
        return found;
    }

    static void SoakHeapReport() {
        float ch1 = HeapAfterChapters(1);
        float ch3 = HeapAfterChapters(3);
        run.Metric("soak.sweeps", SceneMemory.Sweeps.Count);
        if (ch1 > 0f && ch3 > 0f) {
            float pct = (ch3 - ch1) / ch1 * 100f;
            run.Metric("soak.heapAfterCh1Mb", Mathf.Round(ch1));
            run.Metric("soak.heapAfterCh3Mb", Mathf.Round(ch3));
            run.Metric("soak.heapCh3VsCh1Pct", Mathf.Round(pct * 10f) / 10f);
            run.Event("heap-ch3-vs-ch1", "heap after Ch1 " + ch1.ToString("0") + " MB, after Ch3 " + ch3.ToString("0") + " MB (" + pct.ToString("+0.0;-0.0") + "%, 15% allowed)");
            if (pct > 15f) {
                run.Problem(PlaytestSeverity.Bug, "soak: heap after Ch3 is " + pct.ToString("0.0") + "% above the heap after Ch1 (15% allowed)");
            }
        } else {
            run.Event("heap-ch3-vs-ch1", "not measured: sweeps after chapters 1 and 3 needed (got " + ch1.ToString("0") + ", " + ch3.ToString("0") + ")");
        }
    }

    static IEnumerator SoakRun(PlaytestOptions options) {
        run.Event("soak-start", "Arcade " + (SoakArcadeSeconds / 60f).ToString("0") + " min, then Campaign chapters 1-3 (" + (SoakCampaignSeconds / 60f).ToString("0") + " min cap), touch=" + UiFit.Touch);
        float arcadeEnd = Time.realtimeSinceStartup + SoakArcadeSeconds;
        while (!run.Done && Time.realtimeSinceStartup < arcadeEnd) {
            yield return Play(options, arcadeEnd - Time.realtimeSinceStartup, false);
            if (run.Done) {
                yield break;
            }
            if (WaveDirector.IsLost && Time.realtimeSinceStartup < arcadeEnd) {
                // Slice fell: a new Arcade run so the soak keeps its minutes (Play's game-over path returned to the menu).
                run.Event("soak-retry", "Arcade run lost at wave " + WaveDirector.Wave + ", starting again");
                yield return StartRun();
            } else {
                break;
            }
        }
        if (run.Done) {
            yield break;
        }
        run.Event("soak-phase", "Arcade done after " + SoakSampler.Minutes + " minutes, off to the campaign");
        SoakSampler.Phase = "campaign";
        yield return ToMenu();
        if (run.Done) {
            yield break;
        }
        pacingRun = true;
        watchCutscene = false;
        startChapter = 0;
        quitDone = false;
        lastFullMap = null;
        float campaignEnd = Time.realtimeSinceStartup + SoakCampaignSeconds;
        IEnumerator campaign = CampaignRun();
        while (!run.Done && campaign.MoveNext()) {
            yield return campaign.Current;
            if (Time.realtimeSinceStartup > campaignEnd) {
                run.Event("soak-phase", "campaign time cap reached (cleared " + CampaignSave.Get(0).cleared + " chapters)");
                break;
            }
        }
        if (run.Done) {
            yield break;
        }
        SoakSampler.Finish();
        SoakHeapReport();
        run.Finish(PlaytestOutcome.Done);
    }
}

// Samples once a minute of real time; one per page, kept across scene loads.
public class SoakSampler : MonoBehaviour {
    const float MinuteSeconds = 60f;
    const float HangSeconds = 20f;

    static SoakSampler instance;
    public static string Phase = "arcade";
    public static int Minutes { get { return instance != null ? instance.minute : 0; } }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern float HawkesJsHeapMb();

    [DllImport("__Internal")]
    static extern void HawkesSoakBeat(int minute);
#else
    static float HawkesJsHeapMb() {
        return 0f;
    }

    static void HawkesSoakBeat(int minute) {
    }
#endif

    PlaytestRun run;
    readonly List<float> frames = new List<float>();
    float windowStart;
    int minute;
    bool crashed;
    float peakReserved;
    float startedAt;
    float worstWindowFrame;   // the slowest frame between 100 and 220 s (the phone Arcade criterion: none under 10 fps)
    // Live counts by wave, last sample of each: wave -> particle systems, lights, bots, pickups.
    readonly SortedDictionary<int, int[]> byWave = new SortedDictionary<int, int[]>();
    float firstReserved = -1f;

    public static void Begin(PlaytestRun r) {
        if (instance != null) {
            Destroy(instance.gameObject);
        }
        var go = new GameObject("SoakSampler");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SoakSampler>();
        instance.run = r;
        instance.windowStart = Time.realtimeSinceStartup;
        instance.startedAt = Time.realtimeSinceStartup;
        Phase = "arcade";
        r.Metric("soak.crashed", 0);
    }

    public static void Finish() {
        if (instance == null) {
            return;
        }
        instance.Sample();
        instance.run.Metric("soak.crashed", instance.crashed ? 1 : 0);
        instance.run.Metric("soak.minutes", instance.minute);
        if (instance.firstReserved > 0f) {
            instance.run.Metric("soak.reservedGrowthMb", Mathf.Round(instance.peakReserved - instance.firstReserved));
        }
        instance.ReportWaves();
        instance.run.Metric("soak.worstFrameMs100to220", Mathf.Round(instance.worstWindowFrame * 1000f));
        instance.run.Event("soak-end", instance.minute + " minutes, crashed=" + instance.crashed + ", reserved " + instance.firstReserved.ToString("0") + " -> peak " + instance.peakReserved.ToString("0") + " MB");
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) {
            return;
        }
        if (dt > HangSeconds && !crashed) {
            crashed = true;
            run.Metric("soak.crashed", 1);
            run.Problem(PlaytestSeverity.Bug, "soak.crashed: one frame took " + dt.ToString("0") + " s (the page stopped answering) in " + Phase + " at minute " + minute);
        }
        frames.Add(dt);
        float since = Time.realtimeSinceStartup - startedAt;
        if (Phase == "arcade" && since >= 100f && since <= 220f && dt > worstWindowFrame) {
            worstWindowFrame = dt;
            if (dt > 0.1f && UiFit.Touch) {
                run.Event("soak-dip", "frame of " + (dt * 1000f).ToString("0") + " ms (under 10 fps) at " + since.ToString("0") + " s, wave " + WaveDirector.Wave + ", bots " + Enemy.Roster.Count);
                run.Problem(PlaytestSeverity.Warning, "soak: a frame of " + (dt * 1000f).ToString("0") + " ms between 100 and 220 s (10 fps floor)");
            }
        }
        if (Time.realtimeSinceStartup - windowStart >= MinuteSeconds) {
            Sample();
        }
    }

    static int Live(ParticleSystem[] systems) {
        int n = 0;
        foreach (ParticleSystem p in systems) {
            if (p != null && p.isPlaying) {
                n++;
            }
        }
        return n;
    }

    // Live counts per wave, and the criterion: from wave 5 on, particle systems and lights stay within 10% of wave 5's.
    void ReportWaves() {
        int baseParticles = -1, baseLights = -1;
        foreach (KeyValuePair<int, int[]> row in byWave) {
            run.Metric("soak.wave" + row.Key + ".particleSystems", row.Value[0]);
            run.Metric("soak.wave" + row.Key + ".lights", row.Value[1]);
            run.Metric("soak.wave" + row.Key + ".bots", row.Value[2]);
            run.Metric("soak.wave" + row.Key + ".pickups", row.Value[3]);
            run.Event("soak-wave", "wave " + row.Key + ": live particle systems " + row.Value[0] + ", lights " + row.Value[1] + ", bots " + row.Value[2] + ", pickups " + row.Value[3]);
            if (row.Key < 5) {
                continue;
            }
            if (baseParticles < 0) {
                baseParticles = row.Value[0];
                baseLights = row.Value[1];
            } else if (Mathf.Abs(row.Value[0] - baseParticles) > Mathf.Max(1f, baseParticles * 0.1f) || Mathf.Abs(row.Value[1] - baseLights) > Mathf.Max(1f, baseLights * 0.1f)) {
                run.Problem(PlaytestSeverity.Warning, "soak: live counts drift at wave " + row.Key + " (particle systems " + row.Value[0] + " vs " + baseParticles + " at wave 5, lights " + row.Value[1] + " vs " + baseLights + ")");
            }
        }
    }

    void Sample() {
        float span = Mathf.Max(0.1f, Time.realtimeSinceStartup - windowStart);
        windowStart = Time.realtimeSinceStartup;
        minute++;
        float avg = 0f, p5 = 0f;
        if (frames.Count > 0) {
            frames.Sort();   // ascending frame time: the 95th percentile is the slow end, its fps is p5
            float total = 0f;
            foreach (float f in frames) {
                total += f;
            }
            avg = frames.Count / Mathf.Max(0.001f, total);
            int idx = Mathf.Min(frames.Count - 1, Mathf.FloorToInt(frames.Count * 0.95f));
            p5 = 1f / Mathf.Max(0.0001f, frames[idx]);
        }
        float worstMs = frames.Count > 0 ? frames[frames.Count - 1] * 1000f : 0f;
        frames.Clear();
        float reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f;
        float allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        float mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f;
        float js = HawkesJsHeapMb();
        int particles = Live(FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        int lights = 0;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
            if (l.enabled) {
                lights++;
            }
        }
        int sources = 0;
        foreach (AudioSource a in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
            if (a.isPlaying) {
                sources++;
            }
        }
        if (firstReserved < 0f) {
            firstReserved = reserved;
        }
        peakReserved = Mathf.Max(peakReserved, reserved);
        int wave = WaveDirector.Wave;
        int[] counts;
        if (Phase == "arcade" && wave > 0) {
            if (!byWave.TryGetValue(wave, out counts)) {
                counts = new int[4];
                byWave[wave] = counts;
            }
            counts[0] = particles;
            counts[1] = lights;
            counts[2] = Enemy.Roster.Count;
            counts[3] = Pickups.Count;
        }
        HawkesSoakBeat(minute);
        string m = "." + minute;
        run.Metric("soak.fpsP5" + m, Mathf.Round(p5 * 10f) / 10f);
        run.Metric("soak.fpsAvg" + m, Mathf.Round(avg * 10f) / 10f);
        run.Metric("soak.reservedMb" + m, Mathf.Round(reserved));
        run.Metric("soak.allocatedMb" + m, Mathf.Round(allocated));
        run.Metric("soak.jsHeapMb" + m, Mathf.Round(js));
        run.Metric("soak.particleSystems" + m, particles);
        run.Metric("soak.lights" + m, lights);
        run.Metric("soak.audioSources" + m, sources);
        run.Metric("soak.bots" + m, Enemy.Roster.Count);
        run.Metric("soak.pickups" + m, Pickups.Count);
        run.Event("soak-minute", "minute " + minute + " (" + Phase + " wave " + wave + ", bots " + Enemy.Roster.Count + ", pickups " + Pickups.Count + ", " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", " + span.ToString("0") + " s): fps p5 " + p5.ToString("0.0") + ", avg " + avg.ToString("0.0")
            + ", worst frame " + worstMs.ToString("0") + " ms; Unity reserved " + reserved.ToString("0") + " MB, allocated " + allocated.ToString("0") + " MB, mono " + mono.ToString("0") + " MB, JS heap " + (js > 0f ? js.ToString("0") + " MB" : "n/a")
            + "; live particle systems " + particles + ", lights " + lights + ", audio sources " + sources);
        if (p5 > 0f && p5 < 30f && UiFit.Touch) {
            run.Problem(PlaytestSeverity.Warning, "soak: fps p5 " + p5.ToString("0.0") + " in minute " + minute + " (30 wanted on a phone)");
        }
    }
}
