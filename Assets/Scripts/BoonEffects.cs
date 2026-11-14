using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Boons that act on their own: whirling blades around the player, the circle's encore, second wind, standing ovation,
// tower song accompaniment, fleet foot slipstream, and the player's family build crest.
public class BoonEffects : MonoBehaviour {
    public static int EncorePulses { get; private set; }
    readonly List<Transform> blades = new List<Transform>();
    readonly Dictionary<Enemy, float> bladeCooldown = new Dictionary<Enemy, float>();
    readonly List<Enemy> expired = new List<Enemy>();
    float spin;
    float encoreIn = 3.5f;
    float ovationIn = 3.2f;
    float wardIn = 3.8f;
    float songIn = 2.2f;
    float fleetIn = 0.35f;
    Vector3 lastFleetPos;
    bool windUsed;
    int windWave = -1;
    Material bladeMat;

    LineRenderer crestOuter;
    LineRenderer crestInner;
    LineRenderer sliceCrest;
    readonly Vector3[] outerPts = new Vector3[24];
    readonly Vector3[] innerPts = new Vector3[24];
    readonly Vector3[] slicePts = new Vector3[16];
    float crestSpin;

    public static void BeginRun() {
        EncorePulses = 0;
    }

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
            HideCrests();
            return;
        }
        TickCrest();
        TickBlades();
        TickEncore();
        TickSecondWind();
        TickOvation();
        TickTowerSong();
        TickFleet();
    }

    void HideCrests() {
        if (crestOuter != null) crestOuter.enabled = false;
        if (crestInner != null) crestInner.enabled = false;
        if (sliceCrest != null) sliceCrest.enabled = false;
    }

    static LineRenderer MakeCrestLine(Transform parent, string name) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Material mat = UpgradeLook.SharedLineMaterial();
        if (mat != null) {
            lr.sharedMaterial = mat;
        }
        lr.enabled = false;
        return lr;
    }

    // Draws the player's build crest on the ground in the first-picked boon family's colour and shape, so two runs with
    // different first picks look unmistakably different in screenshots even at phone camera distance.
    void TickCrest() {
        BoonFamily lead = RunBoons.LeadFamily;
        if (lead == BoonFamily.None) {
            HideCrests();
            return;
        }
        if (crestOuter == null) crestOuter = MakeCrestLine(transform, "BuildCrestOuter");
        if (crestInner == null) crestInner = MakeCrestLine(transform, "BuildCrestInner");
        if (sliceCrest == null) sliceCrest = MakeCrestLine(transform, "SliceBuildCrest");

        float dt = Time.deltaTime;
        crestSpin += dt;
        BoonFx.PulseBoost = Mathf.MoveTowards(BoonFx.PulseBoost, 0f, dt * 2.2f);
        float pulse = 1f + 0.16f * BoonFx.PulseBoost + 0.06f * Mathf.Sin(crestSpin * 4.5f);
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        Color col = RunBoons.FamilyColor(lead);
        Color bright = Color.Lerp(col, Color.white, 0.22f * BoonFx.PulseBoost);
        bright.a = 0.92f;
        Color innerCol = BoonFx.LastProcTime > 0f && Time.unscaledTime - BoonFx.LastProcTime < 1.2f
            ? RunBoons.ColorOf(BoonFx.LastBoon)
            : Color.Lerp(col, Color.white, 0.18f);
        innerCol.a = 0.82f;

        Vector3 center = transform.position + Vector3.up * 0.11f;
        float w = (0.22f + 0.08f * BoonFx.PulseBoost) * Mathf.Sqrt(Mathf.Max(1f, grown));
        crestOuter.startWidth = w;
        crestOuter.endWidth = w;
        crestOuter.startColor = bright;
        crestOuter.endColor = bright;

        float iw = w * 0.68f;
        crestInner.startWidth = iw;
        crestInner.endWidth = iw;
        crestInner.startColor = innerCol;
        crestInner.endColor = innerCol;

        int outerCount = 16;
        int innerCount = 12;
        switch (lead) {
            case BoonFamily.Element:
                // 8-pointed blazing star + counter-rotating fire octagon.
                outerCount = 16;
                for (int i = 0; i < outerCount; i++) {
                    float a = crestSpin * 0.8f + i * Mathf.PI * 2f / outerCount;
                    float r = ((i % 2 == 0) ? 2.65f : 1.55f) * grown * pulse;
                    outerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                innerCount = 8;
                for (int i = 0; i < innerCount; i++) {
                    float a = -crestSpin * 1.1f + i * Mathf.PI * 2f / innerCount;
                    float r = 1.25f * grown;
                    innerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                }
                break;
            case BoonFamily.Reach:
                // Wide 3-lobe cyclone sweep + wide outer cyan halo.
                outerCount = 24;
                for (int i = 0; i < outerCount; i++) {
                    float u = i * Mathf.PI * 2f / outerCount;
                    float a = -crestSpin * 1.15f + u;
                    float r = (2.55f + 0.55f * Mathf.Sin(u * 3f)) * grown * pulse;
                    outerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                innerCount = 24;
                for (int i = 0; i < innerCount; i++) {
                    float a = crestSpin * 0.7f + i * Mathf.PI * 2f / innerCount;
                    float r = 3.25f * grown;
                    innerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                }
                break;
            case BoonFamily.Impact:
                // Bold 4-pointed heavy diamond + rotated inner impact square.
                outerCount = 8;
                for (int i = 0; i < outerCount; i++) {
                    float a = crestSpin * 0.35f + i * Mathf.PI * 2f / outerCount;
                    float r = ((i % 2 == 0) ? 2.85f : 1.65f) * grown * pulse;
                    outerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                innerCount = 4;
                for (int i = 0; i < innerCount; i++) {
                    float a = crestSpin * 0.35f + Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                    float r = 1.6f * grown;
                    innerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                }
                break;
            case BoonFamily.Guard:
                // Hexagonal emerald aegis shield + inner ward ring.
                outerCount = 6;
                for (int i = 0; i < outerCount; i++) {
                    float a = crestSpin * 0.45f + i * Mathf.PI * 2f / outerCount;
                    float r = 2.45f * grown * pulse;
                    outerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                innerCount = 12;
                for (int i = 0; i < innerCount; i++) {
                    float a = -crestSpin * 0.6f + i * Mathf.PI * 2f / innerCount;
                    float r = 1.45f * grown;
                    innerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                }
                break;
            default:
                // Tricks: 5-pointed trickster star + counter-rotating inner star.
                outerCount = 10;
                for (int i = 0; i < outerCount; i++) {
                    float a = crestSpin * 1.1f + i * Mathf.PI * 2f / outerCount;
                    float r = ((i % 2 == 0) ? 2.7f : 1.35f) * grown * pulse;
                    outerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                }
                innerCount = 10;
                for (int i = 0; i < innerCount; i++) {
                    float a = -crestSpin * 1.1f + i * Mathf.PI * 2f / innerCount;
                    float r = ((i % 2 == 0) ? 1.5f : 0.85f) * grown;
                    innerPts[i] = center + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r);
                }
                break;
        }

        crestOuter.positionCount = outerCount;
        for (int i = 0; i < outerCount; i++) {
            crestOuter.SetPosition(i, outerPts[i]);
        }
        crestOuter.enabled = true;

        crestInner.positionCount = innerCount;
        for (int i = 0; i < innerCount; i++) {
            crestInner.SetPosition(i, innerPts[i]);
        }
        crestInner.enabled = true;

        bool guardHeld = lead == BoonFamily.Guard || RunBoons.Has(BoonId.Ovation) || RunBoons.Has(BoonId.Encore)
            || RunBoons.Has(BoonId.SecondWind) || RunBoons.Has(BoonId.Turret) || RunBoons.Has(BoonId.Orbit) || RunBoons.Has(BoonId.GrandFinale);
        if (guardHeld && Musician.Instance != null && Musician.Instance.Alive) {
            Color gCol = RunBoons.FamilyColor(BoonFamily.Guard);
            gCol.a = 0.85f;
            sliceCrest.startWidth = 0.2f;
            sliceCrest.endWidth = 0.2f;
            sliceCrest.startColor = gCol;
            sliceCrest.endColor = gCol;
            Vector3 sCenter = Musician.Instance.transform.position + Vector3.up * 0.1f;
            sliceCrest.positionCount = slicePts.Length;
            for (int i = 0; i < slicePts.Length; i++) {
                float a = crestSpin * 0.5f + i * Mathf.PI * 2f / slicePts.Length;
                float r = 3.4f + 0.15f * Mathf.Sin(crestSpin * 3f + i);
                slicePts[i] = sCenter + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                sliceCrest.SetPosition(i, slicePts[i]);
            }
            sliceCrest.enabled = true;
        } else {
            sliceCrest.enabled = false;
        }
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
                    BoonFx.Proc(BoonId.Orbit, at, "WHIRLING BLADE", 1.9f);
                    break;
                }
            }
        }
    }

    Transform MakeBlade() {
        Color guardCol = RunBoons.FamilyColor(BoonFamily.Guard);
        if (bladeMat == null) {
            bladeMat = new Material(SafeShader.Find("Unlit/Color"));
            bladeMat.color = guardCol;
        }
        GameObject model = ArtLibrary.Make("Quill", true);
        if (model != null) {
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) {
                if (r != null && r.sharedMaterial != null) {
                    r.sharedMaterial = bladeMat;
                }
            }
            return model.transform;
        }
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(blade.GetComponent<Collider>());
        blade.name = "Quill";
        blade.transform.localScale = new Vector3(0.16f, 0.08f, 0.95f);
        var inner = blade.GetComponent<Renderer>();
        inner.sharedMaterial = bladeMat;
        inner.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var holder = new GameObject("Blade").transform;
        blade.transform.SetParent(holder, false);
        // On the holder, not the squashed quill: a trail on a non-uniform scale draws sheared wedges. None on the phone tier.
        TrailRenderer trail = UiFit.Touch ? null : holder.gameObject.AddComponent<TrailRenderer>();
        if (trail == null)
            return holder;
        trail.alignment = LineAlignment.View;
        trail.numCapVertices = 0;
        trail.minVertexDistance = 0.1f;
        trail.time = 0.14f;
        trail.startWidth = 0.16f;
        trail.endWidth = 0f;
        Material mat = UpgradeLook.SharedLineMaterial();
        if (mat != null) {
            trail.sharedMaterial = mat;
        }
        trail.startColor = new Color(guardCol.r, guardCol.g, guardCol.b, 0.85f);
        trail.endColor = new Color(guardCol.r, guardCol.g, guardCol.b, 0f);
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
        EncorePulses++;
        Color guardCol = RunBoons.FamilyColor(BoonFamily.Guard);
        UpgradeLook.Ring(at + Vector3.up * 0.2f, radius, guardCol, 0.45f);
        Combat.Burst(at, radius, Combat.ArrowBase() * (0.8f + 0.4f * rank), DamageKind.Pulse, null);
        BoonFx.Proc(BoonId.Encore, at + Vector3.up * 0.5f, "ENCORE!", Mathf.Min(6.5f, radius * 0.55f));
        if (finale) {
            BoonFx.Proc(BoonId.GrandFinale, at + Vector3.up * 0.9f, "GRAND FINALE!", Mathf.Min(8f, radius * 0.65f));
        }
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
        if (musician == null || !musician.Alive || WaveDirector.IsBuilding) {
            return;
        }
        Vector3 at = musician.transform.position;
        if (!windUsed && musician.Health < 25) {
            windUsed = true;
            musician.Heal(20);
            UpgradeLook.Ring(at + Vector3.up * 0.2f, 22f, RunBoons.FamilyColor(BoonFamily.Guard), 0.7f);
            Combat.Burst(at, 22f, Combat.ArrowBase() * 6f, DamageKind.Pulse, null);
            BoonFx.Proc(BoonId.SecondWind, at + Vector3.up * 0.8f, "SECOND WIND!", 8f);
            UpgradeLook.Say("Second Wind!", 1.8f, 60f);
            return;
        }
        if (!windUsed && Enemy.Roster.Count > 0) {
            wardIn -= Time.deltaTime;
            if (wardIn <= 0f) {
                wardIn = 4.8f;
                BoonFx.Proc(BoonId.SecondWind, at + Vector3.up * 0.6f, "SECOND WIND WARD", 3.5f);
            }
        }
    }

    void TickOvation() {
        int rank = RunBoons.Rank(BoonId.Ovation);
        Musician musician = Musician.Instance;
        if (rank <= 0 || musician == null || !musician.Alive || WaveDirector.IsBuilding || Enemy.Roster.Count == 0) {
            return;
        }
        ovationIn -= Time.deltaTime;
        if (ovationIn > 0f) {
            return;
        }
        ovationIn = 5.2f;
        if (musician.Health < musician.MaxHealth) {
            musician.Heal(1);
        }
        BoonFx.Proc(BoonId.Ovation, musician.transform.position + Vector3.up * 0.8f, "+OVATION", 3.4f);
    }

    void TickTowerSong() {
        int rank = RunBoons.Rank(BoonId.Turret);
        if (rank <= 0 || WaveDirector.IsBuilding || Enemy.Roster.Count == 0) {
            return;
        }
        songIn -= Time.deltaTime;
        if (songIn > 0f) {
            return;
        }
        Vector3 from = (Musician.Instance != null ? Musician.Instance.transform.position : transform.position) + Vector3.up * 1.6f;
        Enemy target = Combat.Nearest(from, MetaProgress.TowerRange() + 8f, null);
        if (target == null) {
            return;
        }
        songIn = Mathf.Max(1.2f, 3.6f / RunBoons.TowerHaste);
        Vector3 to = target.transform.position + Vector3.up * 0.7f;
        UpgradeLook.Bolt(from, to);
        target.Hit(1f);
        BoonFx.Proc(BoonId.Turret, to, "TOWER SONG", 2.1f);
    }

    void TickFleet() {
        int rank = RunBoons.Rank(BoonId.Fleet);
        if (rank <= 0 || WaveDirector.IsBuilding) {
            lastFleetPos = transform.position;
            return;
        }
        fleetIn -= Time.deltaTime;
        if (fleetIn > 0f) {
            return;
        }
        Vector3 flat = transform.position - lastFleetPos;
        flat.y = 0f;
        lastFleetPos = transform.position;
        if (flat.sqrMagnitude < 0.08f) {
            return;
        }
        fleetIn = 0.45f;
        BoonFx.Proc(BoonId.Fleet, transform.position + Vector3.up * 0.12f, "FLEET FOOT", 1.8f);
    }
}
