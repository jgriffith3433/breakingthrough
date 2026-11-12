using UnityEngine;

// The hub's idle life (Docs/Story.md, "The hub and Wallit's account"): each lodger does its own thing while nobody talks to it,
// and Slice and Wallit trade a line or two whenever Wallit stands in her ring. What they say changes with the chapters filed.
// Added to the Hub object by ChapterHub.Build. Collider-free, no particles; it only moves the lodgers' model children, so
// ChapterHub keeps the root positions (the hover of Brim and Chime stays there).
public class HubAmbience : MonoBehaviour {
    public const float RingReach = 3.6f;
    const float LineSeconds = 3.4f;
    const float Cooldown = 5f;

    ChapterHub hub;
    Transform[] models;
    Transform slice;
    float[] phase;
    float busyUntil;
    float readyAt;
    string[] queue;
    int queueIndex;
    float queueStart;
    readonly int[] turns = new int[8];

    // What each lodger is doing right now, for the playtest and the footage review (index = Story.Lodgers row).
    public string[] Actions { get; private set; }
    public string Line { get; private set; } = "";
    public Color LineColor { get; private set; } = Color.white;
    public string LastBanterId { get; private set; } = "";
    public string LastBanterText { get; private set; } = "";
    public int Banters { get; private set; }
    public bool NearSlice { get; private set; }

    public static HubAmbience Attach(ChapterHub owner, Transform[] lodgerRoots, Transform sliceFigure) {
        var ambience = owner.gameObject.AddComponent<HubAmbience>();
        ambience.hub = owner;
        ambience.slice = sliceFigure;
        ambience.models = new Transform[lodgerRoots.Length];
        ambience.phase = new float[lodgerRoots.Length];
        ambience.Actions = new string[lodgerRoots.Length];
        for (int i = 0; i < lodgerRoots.Length; i++) {
            ambience.models[i] = lodgerRoots[i].Find("Lodger Model");
            ambience.phase[i] = i * 2.3f;
            ambience.Actions[i] = "still";
        }
        return ambience;
    }

    // 0 to 1 over a window of a repeating cycle: rises, holds, falls.
    static float Pulse(float t, float from, float rise, float hold, float fall) {
        if (t < from || t > from + rise + hold + fall) {
            return 0f;
        }
        float u = t - from;
        if (u < rise) {
            return Mathf.SmoothStep(0f, 1f, u / rise);
        }
        if (u < rise + hold) {
            return 1f;
        }
        return 1f - Mathf.SmoothStep(0f, 1f, (u - rise - hold) / fall);
    }

    void Update() {
        if (hub == null || !hub.Shown) {
            return;
        }
        // A two-shot owns the speakers' facing/gestures; other lodgers keep their idle so the room still breathes.
        // Ring banter waits until the stage closes.
        if (ConversationStage.IsOpen) {
            Line = "";
            for (int i = 0; i < models.Length; i++) {
                if (models[i] == null) {
                    continue;
                }
                Transform root = models[i].parent;
                bool inShot = ConversationStage.Holds(root) || ConversationStage.Holds(models[i]);
                if (!inShot) {
                    Idle(i);
                }
            }
            return;
        }
        for (int i = 0; i < models.Length; i++) {
            if (models[i] != null) {
                Idle(i);
            }
        }
        TickBanter();
    }

    // Four different idle actions, one per lodger, none of them the same motion:
    //   Tick   swings like a pendulum, then strikes the hour (a stretch up and a shudder).
    //   Brim   looks about (turns left, right) and tips forward as if raising a hat it does not have.
    //   Spare  tinkers: the loose claw side twitches and it hops with a chirp.
    //   Chime  rings: rocks hard on its loop, a few swings, then rests.
    void Idle(int i) {
        Transform m = models[i];
        string id = Story.Lodgers[i].Id;
        float t = Time.unscaledTime + phase[i];
        Vector3 pos = Vector3.zero;
        Vector3 euler = Vector3.zero;
        Vector3 scale = Vector3.one;
        string action = "still";
        float yaw = 180f;
        switch (id) {
            case "tick": {
                float c = t % 9f;
                float strike = Pulse(c, 6f, 0.3f, 1.2f, 0.4f);
                if (strike > 0.01f) {
                    action = "striking the hour";
                    scale = new Vector3(1f - 0.05f * strike, 1f + 0.12f * strike, 1f);
                    euler.z = Mathf.Sin(t * 30f) * 2.5f * strike;
                } else {
                    action = "swinging like a pendulum";
                    euler.z = Mathf.Sin(t * 2.2f) * 5f;
                }
                break;
            }
            case "brim": {
                float c = t % 10f;
                float tip = Pulse(c, 6.5f, 0.5f, 0.8f, 0.5f);
                if (tip > 0.01f) {
                    action = "tipping an absent hat";
                    euler.x = 18f * tip;
                    pos.y = -0.05f * tip;
                } else {
                    action = "looking about";
                    yaw += Mathf.Sin(t * 0.7f) * 35f;
                }
                break;
            }
            case "spare": {
                float c = t % 6f;
                float hop = Pulse(c, 3.5f, 0.15f, 0.05f, 0.25f);
                if (hop > 0.01f) {
                    action = "hopping with a chirp";
                    pos.y = 0.28f * hop;
                } else {
                    action = "tinkering with its claw";
                    euler.z = Mathf.Sin(t * 9f) * 3f * Pulse(c, 0.5f, 0.2f, 2.2f, 0.3f);
                    euler.y = Mathf.Sin(t * 5f) * 6f * Pulse(c, 0.5f, 0.2f, 2.2f, 0.3f);
                }
                break;
            }
            default: {
                float c = t % 8f;
                float ring = Pulse(c, 5f, 0.2f, 1.6f, 0.5f);
                if (ring > 0.01f) {
                    action = "ringing";
                    euler.z = Mathf.Sin(t * 11f) * 14f * ring;
                } else {
                    action = "rocking on its loop";
                    euler.z = Mathf.Sin(t * 1.3f) * 2.5f;
                }
                break;
            }
        }
        m.localPosition = pos;
        m.localRotation = Quaternion.Euler(euler.x, yaw + euler.y, euler.z);
        m.localScale = scale;
        Actions[i] = action;
    }

    // Stand in Slice's ring and the two of them trade the next exchange for the chapters filed. A fresh one each time, round and round.
    void TickBanter() {
        if (hub.Player == null || slice == null) {
            return;
        }
        NearSlice = (new Vector3(hub.Player.position.x, 0f, hub.Player.position.z) - new Vector3(slice.position.x, 0f, slice.position.z)).magnitude <= RingReach;
        if (queue != null) {
            int index = Mathf.FloorToInt((Time.unscaledTime - queueStart) / LineSeconds);
            if (index >= queue.Length) {
                queue = null;
                Line = "";
                readyAt = Time.unscaledTime + Cooldown;
            } else {
                string raw = queue[index];
                Line = (raw[0] == 'W' ? "Wallit" : "Slice") + ": \"" + raw.Substring(2) + "\"";
                LineColor = raw[0] == 'W' ? new Color(1f, 0.82f, 0.38f) : new Color(0.62f, 0.95f, 0.86f);
            }
            return;
        }
        // The arrival lines speak first; wait for them to finish.
        if (!NearSlice || Time.unscaledTime < readyAt || hub.BoardOpen || hub.ArrivalShown < Story.HubArrival[Mathf.Clamp(CampaignSave.Get(CampaignSave.Active).cleared, 1, Story.HubArrival.Length) - 1].Length) {
            return;
        }
        int cleared = Mathf.Clamp(CampaignSave.Get(CampaignSave.Active).cleared, 1, Story.HubRing.Length);
        string[][] pairs = Story.HubRing[cleared - 1];
        int turn = turns[cleared - 1]++ % pairs.Length;
        queue = pairs[turn];
        queueStart = Time.unscaledTime;
        Banters++;
        LastBanterId = "c" + cleared + "-" + (turn + 1);
        LastBanterText = string.Join(" / ", queue);
        RunAnalytics.Screen("hub_ring_banter");
    }
}
