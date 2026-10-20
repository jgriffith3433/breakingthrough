using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Boons that act on their own: whirling quills around the player, the piano's encore, second wind.
public class BoonEffects : MonoBehaviour {
    readonly List<Transform> blades = new List<Transform>();
    readonly Dictionary<Enemy, float> bladeCooldown = new Dictionary<Enemy, float>();
    readonly List<Enemy> expired = new List<Enemy>();
    float spin;
    float encoreIn = 4f;
    bool windUsed;
    int windWave = -1;
    Material bladeMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || player.GetComponent<BoonEffects>() != null) {
            return;
        }
        player.AddComponent<BoonEffects>();
    }

    void Update() {
        if (WaveDirector.IsLost) {
            return;
        }
        TickBlades();
        TickEncore();
        TickSecondWind();
    }

    void TickBlades() {
        int want = RunBoons.Rank(BoonId.Orbit) * 2;
        while (blades.Count < want) {
            blades.Add(MakeBlade());
        }
        if (want == 0) {
            return;
        }
        PlayerGrowth growth = PlayerGrowth.Instance;
        float body = growth != null ? growth.Scale : 1f;
        float radius = (growth != null ? growth.BodyRadius : 0.5f) + 2.2f + body * 0.35f;
        spin += Time.deltaTime * (200f + 25f * RunBoons.Rank(BoonId.Orbit));
        Vector3 center = transform.position + Vector3.up * (growth != null ? growth.BodyHeight * 0.4f : 0.8f);
        for (int i = 0; i < blades.Count; i++) {
            float angle = (spin + i * 360f / blades.Count) * Mathf.Deg2Rad;
            Vector3 spot = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
            blades[i].position = spot;
            blades[i].rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle))) * Quaternion.Euler(0f, 0f, spin * 2f);
            blades[i].localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, Mathf.Clamp01((body - 1f) / 5f));
        }

        expired.Clear();
        foreach (KeyValuePair<Enemy, float> pair in bladeCooldown) {
            if (pair.Key == null || Time.time >= pair.Value) {
                expired.Add(pair.Key);
            }
        }
        for (int i = 0; i < expired.Count; i++) {
            bladeCooldown.Remove(expired[i]);
        }

        float reach = 0.9f * Mathf.Lerp(1f, 2.2f, Mathf.Clamp01((body - 1f) / 5f));
        float damage = Combat.ArrowBase() * 0.6f;
        List<Enemy> roster = Enemy.Roster;
        for (int e = roster.Count - 1; e >= 0; e--) {
            Enemy enemy = roster[e];
            if (enemy == null || !enemy.Alive || bladeCooldown.ContainsKey(enemy)) {
                continue;
            }
            Vector3 at = enemy.transform.position + Vector3.up * 0.4f * enemy.BodyScale;
            float hitReach = reach + enemy.BodyScale * 0.35f;
            for (int i = 0; i < blades.Count; i++) {
                if ((blades[i].position - at).sqrMagnitude <= hitReach * hitReach) {
                    bladeCooldown[enemy] = Time.time + 0.45f;
                    enemy.Damage(damage, DamageKind.Blade, false);
                    break;
                }
            }
        }
    }

    Transform MakeBlade() {
        if (bladeMat == null) {
            bladeMat = new Material(SafeShader.Find("Unlit/Color"));
            bladeMat.color = new Color(0.75f, 0.95f, 1f);
        }
        GameObject model = ArtLibrary.Make("Quill", true);
        if (model != null) {
            return model.transform;
        }
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(blade.GetComponent<Collider>());
        blade.name = "Quill";
        blade.transform.localScale = new Vector3(0.12f, 0.06f, 0.9f);
        var inner = blade.GetComponent<Renderer>();
        inner.sharedMaterial = bladeMat;
        inner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var holder = new GameObject("Blade").transform;
        blade.transform.SetParent(holder, false);
        TrailRenderer trail = blade.AddComponent<TrailRenderer>();
        trail.time = 0.12f;
        trail.startWidth = 0.14f;
        trail.endWidth = 0f;
        trail.material = new Material(SafeShader.Find("Sprites/Default"));
        trail.startColor = new Color(0.7f, 0.95f, 1f, 0.8f);
        trail.endColor = new Color(0.7f, 0.95f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return holder;
    }

    void TickEncore() {
        int rank = RunBoons.Rank(BoonId.Encore);
        Musician musician = Musician.Instance;
        if (rank <= 0 || musician == null || !musician.Alive || WaveDirector.IsBuilding) {
            return;
        }
        bool finale = RunBoons.Has(BoonId.GrandFinale);
        encoreIn -= Time.deltaTime;
        if (encoreIn > 0f) {
            return;
        }
        float every = Mathf.Max(1.5f, 6f - rank * 1f);
        if (finale) {
            every *= 0.5f;
        }
        encoreIn = every;
        float radius = 9f + rank * 3f + WaveDirector.Wave * 0.12f;
        if (finale) {
            radius *= 2f;
            musician.Heal(1);
        }
        Vector3 at = musician.transform.position;
        UpgradeLook.Ring(at + Vector3.up * 0.2f, radius, new Color(1f, 0.85f, 0.5f), 0.45f);
        Combat.Burst(at, radius, Combat.ArrowBase() * (0.8f + 0.4f * rank), DamageKind.Pulse, null);
        GameAudio.Play("stomp", 0.35f, 1.4f);
    }

    void TickSecondWind() {
        if (!RunBoons.Has(BoonId.SecondWind)) {
            return;
        }
        if (windWave != WaveDirector.Wave) {
            windWave = WaveDirector.Wave;
            windUsed = false;
        }
        Musician musician = Musician.Instance;
        if (windUsed || musician == null || !musician.Alive || musician.Health >= 25) {
            return;
        }
        windUsed = true;
        musician.Heal(20);
        Vector3 at = musician.transform.position;
        UpgradeLook.Ring(at + Vector3.up * 0.2f, 22f, new Color(0.6f, 1f, 0.7f), 0.7f);
        Combat.Burst(at, 22f, Combat.ArrowBase() * 6f, DamageKind.Pulse, null);
        UpgradeLook.Say("Second Wind!", 1.8f, 60f);
        GameAudio.Play("launch", 0.9f, 0.8f);
    }
}
