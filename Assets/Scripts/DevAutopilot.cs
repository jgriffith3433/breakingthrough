using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only balance probe. F9 toggles it: it plays like a perfect batter who is always at the bird nearest the
// piano (it swings at that bird and whatever is in reach of it, without walking there), picks the first card,
// skips breaks, and runs the clock faster. Read the [RunLog] lines it leaves.
public class DevAutopilot : MonoBehaviour {
    public static DevAutopilot Instance { get; private set; }
    public static bool On { get; private set; }

    public float Speed = 3f;
    public float Accuracy = 0.8f;
    public int PickIndex = -1;

    float swingIn;
    float sweepIn;
    int swings;

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

        swingIn -= Time.deltaTime;
        if (swingIn > 0f) {
            return;
        }
        swingIn = 0.4f / Mathf.Max(0.2f, RunBoons.DrawSpeed);
        Enemy target = Threat(null);
        if (target == null) {
            return;
        }
        swings++;
        RunLog.NoteSwing();
        if (Random.value > Accuracy) {
            return;
        }
        RunLog.NoteConnect();
        // The combo: two plain swings, then the finisher. Everything within the bat's reach of the target is hit.
        bool finisher = swings % 3 == 0;
        float scale = finisher ? Tuning.BatDamageFinisher : Tuning.BatDamage;
        Vector3 at = target.transform.position;
        float reach = BatEquipment.Reach;
        var struck = new List<Enemy>();
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy != null && enemy.Alive && !enemy.Climbing && (enemy.transform.position - at).sqrMagnitude <= reach * reach) {
                struck.Add(enemy);
            }
        }
        for (int i = 0; i < struck.Count; i++) {
            Enemy enemy = struck[i];
            if (!Combat.BatHit(enemy, scale, finisher ? 4f : 2f, true) && enemy != null) {
                Vector3 away = enemy.transform.position - at;
                away.y = 0f;
                enemy.Knock(away.normalized * (finisher ? Tuning.BatKnockFinisher : Tuning.BatKnock) + Vector3.up * 3f);
            }
        }
        if (finisher && MetaProgress.ShootsRockets()) {
            Combat.Burst(at, MetaProgress.SplashRadius(2f), Combat.BatBase() * 0.65f, DamageKind.Blast, target);
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
