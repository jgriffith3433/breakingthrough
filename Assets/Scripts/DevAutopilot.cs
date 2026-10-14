using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only balance probe. F9 toggles it: it plays like a perfect archer standing by the piano,
// picks the first card, skips breaks, and runs the clock faster. Read the [RunLog] lines it leaves.
public class DevAutopilot : MonoBehaviour {
    public static DevAutopilot Instance { get; private set; }
    public static bool On { get; private set; }

    public float Speed = 3f;
    public float Accuracy = 0.8f;
    public int PickIndex = -1;

    float shotIn;
    float sweepIn;
    int shots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (!Application.isEditor) {
            return;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<DevAutopilot>() != null) {
            return;
        }
        new GameObject("Dev Autopilot").AddComponent<DevAutopilot>();
    }

    void Awake() {
        Instance = this;
    }

    public static void Toggle(bool on) {
        On = on;
        if (on) {
            MetaProgress.EnterSandbox(false);
        }
        if (!on && Time.timeScale > 1f) {
            Time.timeScale = 1f;
        }
        Debug.Log("[Autopilot] " + (on ? "on" : "off"));
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.F9)) {
            Toggle(!On);
        }
        if (!On || WaveDirector.IsLost) {
            return;
        }
        if (LevelUpScreen.IsOpen) {
            LevelUpScreen.DevPick(PickIndex);
            return;
        }
        if (Time.timeScale > 0.9f && Time.timeScale < Speed) {
            Time.timeScale = Speed;
        }
        if (WaveDirector.Instance != null) {
            WaveDirector.Instance.DevSkipBreak();
        }

        sweepIn -= Time.deltaTime;
        if (sweepIn <= 0f) {
            sweepIn = 3f;
            Pickups.Vacuum();
        }

        shotIn -= Time.deltaTime;
        if (shotIn > 0f) {
            return;
        }
        shotIn = 0.7f / Mathf.Max(0.2f, RunBoons.DrawSpeed);
        shots++;
        RunLog.NoteShot();
        int arrows = 1 + RunBoons.ExtraArrows;
        var taken = new List<Enemy>();
        for (int i = 0; i < arrows; i++) {
            Enemy target = Threat(taken);
            if (target == null) {
                break;
            }
            taken.Add(target);
            if (Random.value > Accuracy) {
                continue;
            }
            Combat.ArrowHit(target, 2f, 1f, true);
            Vector3 at = target.transform.position;
            for (int p = 0; p < RunBoons.PierceCount + RunBoons.Bounces; p++) {
                Enemy next = Combat.Nearest(at, 4f, taken);
                if (next == null) {
                    break;
                }
                taken.Add(next);
                Combat.ArrowHit(next, 2f, 0.8f, true);
            }
            if (MetaProgress.ShootsRockets()) {
                Combat.Burst(at, MetaProgress.SplashRadius(2f), Combat.ArrowBase() * 0.65f, DamageKind.Blast, target);
            }
        }
        int every = RunBoons.VolleyEvery;
        if (every > 0 && shots % every == 0) {
            int hits = 3 + RunBoons.Rank(BoonId.Volley) * 2;
            for (int i = 0; i < hits; i++) {
                Enemy target = Threat(null);
                if (target != null) {
                    Combat.ArrowHit(target, 2f, 0.6f, false);
                }
            }
        }
    }

    // The bird closest to the musician, the one a sensible player shoots first.
    static Enemy Threat(List<Enemy> skip) {
        Vector3 piano = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Enemy best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive || (skip != null && skip.Contains(enemy))) {
                continue;
            }
            float dist = (enemy.transform.position - piano).sqrMagnitude;
            if (enemy.Golden) {
                dist *= 0.2f;
            }
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy;
            }
        }
        return best;
    }
}
