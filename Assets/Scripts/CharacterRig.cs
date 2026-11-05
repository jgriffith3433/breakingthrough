using System.Collections.Generic;
using UnityEngine;

// Procedural animation for an authored character (Docs/CharacterModels.md, section 3). It drives the named parts of a
// CharacterKit hierarchy from their rest pose every frame: a breathing bob, a per-character idle fidget (the personality),
// Walk, Tell (the wind-up, 0 to 1: callers pass their own tell fraction so the timing stays theirs), Strike, Hurt and Die.
// Gameplay code calls the same few members on every character; a part that a character lacks is skipped.
public class CharacterRig : MonoBehaviour {
    public string Id;
    public float WalkSpeed;     // 0 standing, 1 full pace
    public float TellT;         // 0 none, 1 the wind-up is complete
    public bool Dead { get; private set; }
    public float Hurt01 { get; private set; }
    public float StrikeT { get; private set; }
    public float Fidget01 { get; private set; }     // 0..1..0 while the idle fidget plays
    public int Fidgets { get; private set; }        // how many fidgets have played
    public float DeadT { get; private set; }

    readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
    readonly Dictionary<Transform, Quaternion> restRot = new Dictionary<Transform, Quaternion>();
    readonly Dictionary<Transform, Vector3> restPos = new Dictionary<Transform, Vector3>();
    Renderer[] renderers;
    MaterialPropertyBlock block;
    float phase, nextFidget, fidgetStart = -10f, seed;
    const float FidgetLen = 1.5f;

    void Awake() {
        Collect();
    }

    void Collect() {
        if (parts.Count > 0) {
            return;
        }
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) {
            if (t == transform) {
                continue;
            }
            if (!parts.ContainsKey(t.name)) {
                parts[t.name] = t;
            }
            restRot[t] = t.localRotation;
            restPos[t] = t.localPosition;
        }
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        seed = Random.value * 20f;
        nextFidget = Time.time + 1.2f + Random.value * 1.5f;
    }

    public bool Has(string part) { Collect(); return parts.ContainsKey(part); }
    public Transform Part(string name) { Collect(); Transform t; return parts.TryGetValue(name, out t) ? t : null; }

    // Pose helpers: rotate / move a part from its rest pose.
    void R(string name, float x, float y = 0f, float z = 0f) {
        Transform t;
        if (parts.TryGetValue(name, out t)) {
            t.localRotation = t.localRotation * Quaternion.Euler(x, y, z);
        }
    }

    void P(string name, float x, float y, float z) {
        Transform t;
        if (parts.TryGetValue(name, out t)) {
            t.localPosition += new Vector3(x, y, z);
        }
    }

    // A total of how far the parts are from rest (degrees plus 100 per metre): a number the playtest can compare.
    public float PoseEnergy() {
        Collect();
        float e = 0f;
        foreach (KeyValuePair<Transform, Quaternion> kv in restRot) {
            if (kv.Key == null) {
                continue;
            }
            e += Quaternion.Angle(kv.Key.localRotation, kv.Value);
            e += (kv.Key.localPosition - restPos[kv.Key]).magnitude * 100f;
        }
        return e;
    }

    public void Tell(float t01) { TellT = Mathf.Clamp01(t01); }
    public void Walk(float speed01) { WalkSpeed = Mathf.Clamp01(speed01); }
    public void Strike() { StrikeT = 0.001f; TellT = 0f; }
    public void HurtFlash() { Hurt01 = 1f; }
    public void Die() { if (!Dead) { Dead = true; DeadT = 0f; } }
    public void Revive() { Dead = false; DeadT = 0f; Hurt01 = 0f; }

    void Update() {
        Collect();
        float dt = Time.deltaTime;
        foreach (KeyValuePair<Transform, Quaternion> kv in restRot) {
            if (kv.Key != null) {
                kv.Key.localRotation = kv.Value;
                kv.Key.localPosition = restPos[kv.Key];
            }
        }
        float t = Time.time + seed;
        if (Dead) {
            DeadT += dt;
            DeathPose(Mathf.Clamp01(DeadT / 0.8f));
            Flash(0f);
            return;
        }
        // Fidget schedule: one move every 3 to 6 seconds, FidgetLen long.
        if (Time.time >= nextFidget) {
            fidgetStart = Time.time;
            nextFidget = Time.time + 3f + Random.value * 3f;
            Fidgets++;
        }
        float fp = (Time.time - fidgetStart) / FidgetLen;
        Fidget01 = fp >= 0f && fp <= 1f ? Mathf.Sin(fp * Mathf.PI) : 0f;

        float breathe = Mathf.Sin(t * 2.2f);
        P("Body", 0f, breathe * 0.008f, 0f);
        Idle(t, breathe, Fidget01, fp);
        if (WalkSpeed > 0.02f) {
            phase += dt * (5f + WalkSpeed * 6f);
            WalkPose(Mathf.Sin(phase) * WalkSpeed, Mathf.Abs(Mathf.Cos(phase)) * WalkSpeed);
        }
        if (TellT > 0f) {
            TellPose(TellT);
        }
        if (StrikeT > 0f) {
            StrikeT += dt / 0.3f;
            StrikePose(Mathf.Clamp01(StrikeT));
            if (StrikeT >= 1f) {
                StrikeT = 0f;
            }
        }
        if (Hurt01 > 0f) {
            Hurt01 = Mathf.Max(0f, Hurt01 - dt / 0.35f);
            R("Body", -14f * Hurt01);
            R("Head", -10f * Hurt01);
            P("Model", 0f, 0f, -0.05f * Hurt01);
        }
        Flash(Hurt01);
    }

    void Flash(float v) {
        if (renderers == null) {
            return;
        }
        if (v <= 0.001f) {
            if (flashed) {
                foreach (Renderer r in renderers) {
                    if (r != null) {
                        r.SetPropertyBlock(null);
                    }
                }
                flashed = false;
            }
            return;
        }
        flashed = true;
        foreach (Renderer r in renderers) {
            if (r == null) {
                continue;
            }
            r.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.3f) * v);
            r.SetPropertyBlock(block);
        }
    }
    bool flashed;

    // The personality: what each character does standing still. f is the fidget bump (0..1..0), fp its progress.
    void Idle(float t, float breathe, float f, float fp) {
        bool alt = Fidgets % 2 == 0;
        switch (Id) {
            case "wallit":      // bounces on his toes, taps the bat on his palm, checks the pocket
                P("Model", 0f, Mathf.Abs(Mathf.Sin(t * 3.2f)) * 0.02f, 0f);
                R("ArmR", -8f + Mathf.Sin(t * 1.3f) * 3f, 0f, 0f);
                if (alt) { R("ArmR", f * 18f * Mathf.Sin(fp * 6f * Mathf.PI), 0f, 0f); } else { R("Bill", 0f, 0f, f * 25f); R("Head", f * 8f, 0f, 0f); }
                R("Head", 0f, 0f, Mathf.Sin(t * 1.1f) * 3f);
                break;
            case "slice":       // nearly still: slow breath; turns the blade over; shifts a boot; eyes flick to a side
                R("ArmR", f * -20f, 0f, 0f);
                R("Prop", 0f, f * 360f * (alt ? 1f : 0f), 0f);
                R("LegR", alt ? 0f : f * -10f, 0f, 0f);
                R("Head", 0f, (alt ? 0f : 1f) * f * 14f * Mathf.Sign(Mathf.Sin(t * 0.3f)), 0f);
                break;
            case "yobot":       // chirpy: head tilt, antenna wobble, claws click
                R("Head", 0f, 0f, Mathf.Sin(t * 2.4f) * 6f);
                R("Antenna", Mathf.Sin(t * 7f) * 12f, 0f, Mathf.Sin(t * 5f) * 12f);
                R("ClawL", f * -25f, 0f, 0f);
                R("ClawR", f * -25f * Mathf.Sin(fp * 5f * Mathf.PI), 0f, 0f);
                break;
            case "spiderbot":
            case "server":
                SpiderIdle(t, f, fp);
                break;
            case "courier":     // checks the clipboard, ticks something, looks up and chirps
                R("Head", f * 18f, 0f, 0f);
                R("ClawL", f * -10f, 0f, 0f);
                R("ClawR", Mathf.Sin(t * 1.5f) * 2f, 0f, 0f);
                P("Model", 0f, Mathf.Abs(Mathf.Sin(t * 2f)) * 0.012f, 0f);
                break;
            case "foreman":     // lifts the clipboard, taps the pen, glances at the clock
                R("ClawL", f * -55f, 0f, 0f);
                R("Head", f * 20f, alt ? 0f : f * -16f, 0f);
                R("ClawR", Mathf.Sin(t * 0.9f) * 3f, 0f, 0f);
                break;
            case "orderbearer": // stillness: the scroll edge lifts, the head turns exactly to its mark
                R("Scroll", 0f, 0f, Mathf.Sin(t * 0.8f) * 1.5f);
                R("Head", 0f, f * 40f * Mathf.Sign(Mathf.Sin(t * 0.17f)), 0f);
                P("Model", 0f, Mathf.Sin(t * 0.7f) * 0.02f, 0f);
                break;
            case "tick":        // winds himself, glances at others' time, the pendulum swings
                R("Key", 0f, 0f, f * 720f);
                R("Tail", 0f, 0f, Mathf.Sin(t * 3.1f) * 22f);
                R("Hands", 0f, 0f, -t * 6f);
                R("Body", f * 6f, 0f, Mathf.Sin(t * 1.2f) * 3f);
                break;
            case "brim":        // hovers, tips to the room, turns slowly: is this my head?
                P("Model", 0f, 0.05f + Mathf.Sin(t * 1.6f) * 0.04f, 0f);
                R("Body", 0f, f * 360f * (alt ? 1f : 0f), f * 16f * (alt ? 0f : 1f));
                R("Feather", Mathf.Sin(t * 2.5f) * 6f, 0f, 0f);
                break;
            case "spare":       // sweeps with the wrong arm, lid slips, chirps at nothing
                R("ClawR", Mathf.Sin(t * 3f) * 18f, 0f, 0f);
                R("Head", 0f, 0f, f * 14f);
                R("Body", 0f, Mathf.Sin(t * 1.5f) * 6f, 0f);
                break;
            case "chime":       // sways and rings once toward the next door
                R("Body", 0f, 0f, Mathf.Sin(t * 1.4f) * 4f + f * 14f * Mathf.Sin(fp * 7f * Mathf.PI));
                R("Clapper", 0f, 0f, f * 35f * Mathf.Sin(fp * 11f * Mathf.PI));
                P("Model", 0f, 0.03f + Mathf.Sin(t * 1.8f) * 0.015f, 0f);
                break;
        }
    }

    void SpiderIdle(float t, float f, float fp) {
        P("Body", 0f, Mathf.Sin(t * 1.5f) * 0.02f, 0f);
        R("Head", 0f, Mathf.Sin(t * 0.8f) * 12f + f * 20f, 0f);
        string[] names = { "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" };
        for (int i = 0; i < names.Length; i++) {
            R(names[i], 0f, Mathf.Sin(t * 2f + i * 1.1f) * 3f + f * 10f * Mathf.Sin(fp * 6f * Mathf.PI + i), 0f);
        }
        if (Id == "server") {
            // Offers the paper, waits, withdraws, offers again.
            float offer = Mathf.Sin(t * 0.5f) * 0.5f + 0.5f;
            R("LegL1", 0f, offer * -18f, offer * 10f);
            R("Prop", 0f, 0f, Mathf.Sin(t * 2.2f) * 2f);
        }
    }

    void WalkPose(float s, float lift) {
        R("LegL", s * 38f); R("LegR", -s * 38f);
        R("ArmL", -s * 22f); R("ArmR", s * 14f);
        R("ClawL", -s * 24f); R("ClawR", s * 24f);
        P("Model", 0f, lift * 0.035f, 0f);
        R("Body", 0f, s * 5f, 0f);
        // Spiders move legs in two alternating sets of three.
        string[] set = { "LegL1", "LegR2", "LegL3", "LegR1", "LegL2", "LegR3" };
        for (int i = 0; i < set.Length; i++) {
            float side = set[i].StartsWith("LegL") ? -1f : 1f;
            float sign = i < 3 ? 1f : -1f;
            R(set[i], 0f, sign * s * 20f * side, Mathf.Max(0f, sign * s) * 12f * side);
        }
    }

    // The wind-up: arms and the front legs rise, the head leans back, the body sinks. t runs 0 to 1.
    void TellPose(float t) {
        R("ArmR", -125f * t);
        R("ClawL", -95f * t); R("ClawR", -105f * t);
        R("Prop", -20f * t);
        R("Head", -16f * t);
        R("Body", -8f * t);
        P("Model", 0f, -0.03f * t, 0f);
        R("LegL1", 0f, 0f, -48f * t);
        R("LegR1", 0f, 0f, 48f * t);
    }

    void StrikePose(float s) {
        float k = 1f - s;           // the wind-up releases: from raised to driven forward
        R("ArmR", -125f * k + 45f * s);
        R("ClawL", -95f * k + 30f * s); R("ClawR", -105f * k + 40f * s);
        R("LegL1", 0f, 0f, -48f * k + 15f * s);
        R("LegR1", 0f, 0f, 48f * k - 15f * s);
        R("Body", 12f * Mathf.Sin(s * Mathf.PI));
    }

    // Humans topple, bots drop and their limbs fold. e runs 0 to 1.
    void DeathPose(float e) {
        bool legged = Has("LegL1");
        R("Model", legged ? 0f : 0f, 0f, legged ? 0f : 82f * e);
        P("Model", 0f, legged ? -0.25f * e : 0f, 0f);
        R("Head", 24f * e);
        R("ArmR", 40f * e); R("ClawR", 50f * e); R("ClawL", 50f * e);
        string[] ls = { "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" };
        for (int i = 0; i < ls.Length; i++) {
            float side = ls[i].StartsWith("LegL") ? -1f : 1f;
            R(ls[i], 0f, 0f, 55f * e * side);
        }
    }
}
