using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// Gives memory back when the player moves between modes and chapters (menu, Arcade, case files, hub, a chapter, a level).
// Every single scene load (the old scene's objects are gone after two frames) runs one sweep:
//   1. log the heap before (Unity native + mono, and the browser's JS heap where there is one),
//   2. release the last chapter's on-demand stills and voices when no chapter is running (a chapter's own `Need` already
//      drops the other chapters),
//   3. prune statics that key on a finished scene's bots (AttackTell, BotSound, Enemy.Roster),
//   4. Resources.UnloadUnusedAssets once, then a GC,
//   5. log the heap after.
// One sweep at a time; a load that lands while one runs waits for it. The line is `[SceneMemory]` in the console and
// each sweep is kept in `Sweeps` for the playtest (soak). Nothing here runs per frame.
public class SceneMemory : MonoBehaviour {
    public struct Sweep {
        public string Reason;
        public string Scene;
        public int Cleared;       // chapters the first case file has finished, for the soak's Ch1/Ch3 comparison
        public float BeforeMb;    // Unity native + mono
        public float AfterMb;
        public float JsBeforeMb;  // 0 where the browser gives none
        public float JsAfterMb;
        public int Released;      // on-demand files dropped
        public float Seconds;
    }

    public static readonly List<Sweep> Sweeps = new List<Sweep>();
    public static Action<Sweep> OnSweep;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern float HawkesJsHeapMb();
#else
    static float HawkesJsHeapMb() {
        return 0f;
    }
#endif

    static SceneMemory instance;
    static bool running;
    static string waitingReason;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        running = false;
        waitingReason = null;
        Sweeps.Clear();
        OnSweep = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Ensure();
    }

    static void Ensure() {
        if (instance == null) {
            var go = new GameObject("SceneMemory");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneMemory>();
        }
    }

    static void OnLoaded(Scene scene, LoadSceneMode mode) {
        if (mode == LoadSceneMode.Single) {
            Request("scene " + scene.name);
        }
    }

    public static void Request(string reason) {
        Ensure();
        if (running) {
            waitingReason = reason;
            return;
        }
        running = true;
        instance.StartCoroutine(instance.Do(reason));
    }

    static float Mb() {
        return (Profiler.GetTotalAllocatedMemoryLong() + Profiler.GetMonoUsedSizeLong()) / 1048576f;
    }

    IEnumerator Do(string reason) {
        yield return null;   // the old scene's objects are destroyed at the end of the load frame
        yield return null;
        var s = new Sweep { Reason = reason, Scene = SceneManager.GetActiveScene().name };
        try {
            s.Cleared = CampaignSave.Get(0).cleared;
        } catch (Exception) {
            s.Cleared = 0;
        }
        float began = Time.realtimeSinceStartup;
        GC.Collect();
        s.BeforeMb = Mb();
        s.JsBeforeMb = HawkesJsHeapMb();
        if (ChapterRun.Instance == null) {
            s.Released = OnDemand.ReleaseChapters();
        }
        AttackTell.Prune();
        BotThreat.Prune();
        BotSound.Prune();
        Enemy.Roster.RemoveAll(e => e == null);
        yield return Resources.UnloadUnusedAssets();
        GC.Collect();
        s.AfterMb = Mb();
        s.JsAfterMb = HawkesJsHeapMb();
        s.Seconds = Time.realtimeSinceStartup - began;
        Sweeps.Add(s);
        Debug.Log("[SceneMemory] " + reason + " (" + s.Scene + "): heap " + s.BeforeMb.ToString("0.0") + " -> " + s.AfterMb.ToString("0.0") + " MB"
            + (s.JsBeforeMb > 0f ? ", JS heap " + s.JsBeforeMb.ToString("0") + " -> " + s.JsAfterMb.ToString("0") + " MB" : "")
            + ", released " + s.Released + " on-demand files, " + s.Seconds.ToString("0.00") + " s");
        if (OnSweep != null) {
            OnSweep(s);
        }
        running = false;
        if (waitingReason != null) {
            string next = waitingReason;
            waitingReason = null;
            Request(next);
        }
    }
}
