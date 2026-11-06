using UnityEngine;

// The hub's small life (Docs/Story.md, "The hub, alive"): what each lodger does while nobody talks to it, the props that move in
// the office, and Wallit's and Slice's banter beside the ring. All of it is data plus a pose function; ChapterHub calls it from
// Update. Nothing here fights, spawns or needs the wave director: the hub is a room, not an arena.
public static class HubAmbience {
    // Each lodger has two idle actions (Story.Lodgers order: Tick, Brim, Spare, Chime) and swaps between them every few seconds.
    // The names are what the playtest logs, so the footage and the timeline say the same thing.
    public static readonly string[][] Actions = {
        new[] { "pendulum swing", "strikes the hour" },
        new[] { "hovers and tips", "turns to look about" },
        new[] { "fidgets with its claw", "stretches up, unfinished" },
        new[] { "sways on its cord", "rings itself" },
    };
    public const float ActionSeconds = 4.5f;

    // Which of a lodger's two actions is playing at time t. Each lodger starts out of step with the others.
    public static int Phase(int lodger, float t) {
        return Mathf.FloorToInt((t + lodger * 2.3f) / ActionSeconds) & 1;
    }

    public static string Action(int lodger, float t) {
        return Actions[Mathf.Clamp(lodger, 0, Actions.Length - 1)][Phase(lodger, t)];
    }

    // Moves a lodger's model (never the root, which holds the label and the spot it was filed at). `at` is its spot; the root
    // is lifted by `lift` for the two that hover.
    public static void Pose(int lodger, Transform root, Transform model, Vector3 at, float t) {
        int phase = Phase(lodger, t);
        float local = Mathf.Repeat(t + lodger * 2.3f, ActionSeconds);
        float lift = 0f;
        Vector3 euler = Vector3.zero;
        Vector3 scale = Vector3.one;
        switch (lodger) {
            case 0:   // Tick, the clock: a slow pendulum lean; on the hour he jolts and settles.
                if (phase == 0) {
                    euler.z = Mathf.Sin(t * 2.2f) * 6f;
                } else {
                    float jolt = Mathf.Exp(-local * 3f) * Mathf.Sin(local * 28f);
                    euler.x = jolt * 8f;
                    lift = Mathf.Max(0f, jolt) * 0.06f;
                }
                break;
            case 1:   // Brim, the hat: hovers; tips forward, or slowly turns to look about.
                lift = 0.12f + Mathf.Sin(t * 1.6f) * 0.1f;
                if (phase == 0) {
                    euler.x = Mathf.Max(0f, Mathf.Sin(local * 1.4f)) * 22f;
                } else {
                    euler.y = Mathf.Sin(local / ActionSeconds * Mathf.PI * 2f) * 50f;
                }
                break;
            case 2:   // Spare, the half-built Yo Bot: twitches a quarter turn, or stretches tall.
                if (phase == 0) {
                    float step = Mathf.Floor(t * 3f);
                    euler.y = (Mathf.Sin(step * 12.9898f) * 43758.5453f % 1f) * 25f;
                    lift = (Mathf.Sin(t * 14f) > 0.8f) ? 0.05f : 0f;
                } else {
                    float s = 1f + Mathf.Sin(local / ActionSeconds * Mathf.PI) * 0.1f;
                    scale = new Vector3(1f / Mathf.Sqrt(s), s, 1f / Mathf.Sqrt(s));
                }
                break;
            default:  // Chime, the bell: swings on its cord; now and then rings, a fast shake.
                lift = 0.12f + Mathf.Sin(t * 1.6f + lodger) * 0.1f;
                if (phase == 0) {
                    euler.z = Mathf.Sin(t * 1.9f) * 12f;
                } else {
                    euler.z = Mathf.Sin(t * 26f) * 10f * Mathf.Exp(-local * 1.2f);
                }
                break;
        }
        root.position = at + Vector3.up * lift;
        model.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(euler);   // the model was put in facing the camera
        model.localScale = scale;
    }

    // ---------------------------------------------------------------- Banter

    // Beside Slice's ring Wallit and Slice talk, a few lines for each count of chapters cleared (index 0 is one cleared, like
    // Story.HubArrival). "<speaker>|<text>": Wallit money, Slice clipped. They are said in order, then the set rests and
    // starts again, so standing there for a while hears the whole of what the office has to say about the file.
    public static readonly string[][] Banter = {
        new[] { "W|One notice. One tip. Rounding it up.", "S|It was a notice. There is no tip.", "W|There's always a tip.", "S|Stand in the ring or stand out of it." },
        new[] { "W|Two doors down. The lease holds.", "S|Because I held it.", "W|And I paid the toll. Split it.", "S|No." },
        new[] { "W|The belt's stopped. Their time is mine to spend.", "S|Tick will want it back.", "W|Tick can invoice me.", "S|He will. In triplicate." },
        new[] { "W|Three refusals. I'd call that a bargain.", "S|The paper was a trap.", "W|Every bargain is. That's how you know it's a good one.", "S|Clean. Keep your hands where I can see them." },
        new[] { "W|Whole room of blank forms. Nobody's name on any.", "S|That is how it starts.", "W|Starts what?", "S|Filing. Don't look so pleased." },
        new[] { "W|All six filed. I want it in writing.", "S|It is in writing. It is on every wall.", "W|Then the circle's mine too.", "S|Ours. Say it.", "W|...Ours. Pay up." },
    };

    public const float BanterReach = 4.2f;
    public const float BanterSeconds = 4f;

    // The line to show for chapters cleared and a step count (steps wrap, with one rest step after the last line).
    public static string BanterLine(int cleared, int step) {
        string[] lines = Banter[Mathf.Clamp(cleared, 1, Banter.Length) - 1];
        int i = step % (lines.Length + 1);
        return i < lines.Length ? lines[i] : null;
    }
}
