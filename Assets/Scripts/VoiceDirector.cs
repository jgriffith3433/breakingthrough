using System.Collections.Generic;
using UnityEngine;

// The one voice director. Every spoken line in the game (WallitVoice's barks, beats and replies, SliceVoice's lines and
// banter) asks Allow() before it speaks. Three priorities:
//   Key    story beats, boss waves, Slice running low and Wallit's answer: always play. If the floor is busy they wait
//          for it (never over another line), and they ignore the gaps.
//   Normal wave starts, level-ups, chests, home runs, golden kills, the opening hello: play when the floor is free and
//          the short NormalGap has passed since the last line.
//   Cutscene lines ("cutscene") are Key and are the only lines allowed while a scene is up (Scene).
//   Bark   ambient and combat chatter (kills, streaks, big hits, hurt, near-misses, idle, banter): dropped when anyone is
//          talking, when less than BarkGap seconds have passed since the last line of any priority, or when the same
//          line or reason spoke inside its cooldown.
// The numbers are public fields so they can be tuned in one place. Runs on unscaled time.
public static class VoiceDirector {
    public enum Priority { Bark = 0, Normal = 1, Key = 2 }

    // Global minimum gap between a bark and the last line of any kind.
    public static float BarkGap = 16f;
    // Minimum gap before a Normal line.
    public static float NormalGap = 3f;
    // The same line may not play again inside this many seconds (Bark and Normal).
    public static float LineCooldown = 60f;
    // One reason ("kill", "hurt", "idle"...) may not bark again inside this many seconds.
    public static float ReasonCooldown = 30f;

    // The event barks (first hit, home run, Slice low, boss phase two, a new kind of bot, a wave cleared): each line is said at
    // most once in this many seconds.
    public static float EventCooldown = 300f;
    // The bots' chorus (reasons "bot-..."): its own, shorter gaps, measured from the last bot line, so it never starves
    // Wallit and Slice, and a line (60 s, LineCooldown) is not heard twice.
    public static float BotGap = 7f;
    public static float BotReasonCooldown = 8f;

    public static bool IsEvent(string why) {
        switch (why) {
            case "first-hit": case "home-run": case "slice-low": case "low": case "boss-phase": case "new-type": case "wave-clear":
            case "boss-homerun": case "boss-win": case "boss-low":
                return true;
            default:
                return false;
        }
    }

    // True while Cutscene.cs is on screen: only its own lines ("cutscene") may speak, so no bark talks over a scene.
    public static bool Scene;

    // Playtest and diagnostics.
    public static int Played { get; private set; }
    public static int Denied { get; private set; }
    public static int KeyPlayed { get; private set; }
    public static float LastLineAt { get; private set; } = -999f;
    static float lastCastAt = -999f;   // the last line that was not a bot's
    public static float SmallestGap { get; private set; } = 999f;
    public static float SmallestBarkGap { get; private set; } = 999f;
    public static readonly List<string> Events = new List<string>();

    static readonly Dictionary<string, float> lineAt = new Dictionary<string, float>();
    static readonly Dictionary<string, float> reasonAt = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() {
        Scene = false;
        Played = 0;
        Denied = 0;
        KeyPlayed = 0;
        LastLineAt = -999f;
        lastCastAt = -999f;
        SmallestGap = 999f;
        SmallestBarkGap = 999f;
        Events.Clear();
        lineAt.Clear();
        reasonAt.Clear();
    }

    // Which priority a reason (the "why" a line is said for) has.
    public static Priority PriorityOf(string why) {
        switch (why) {
            case "beat":
            case "boss-wave":
            case "boss-phase":
            case "boss-win":
            case "low":
            case "slice-low":
            case "stakes-line":
            case "cutscene":
                return Priority.Key;
            case "wave":
            case "level-up":
            case "chest":
            case "home-run":
            case "boss-homerun":
            case "boss-low":
            case "first-hit":
            case "new-type":
            case "wave-clear":
            case "golden":
            case "setup":
            case "callout-reply":
                return Priority.Normal;
            default:
                return Priority.Bark;
        }
    }

    // The player's voice-line frequency (Settings): the gaps and cooldowns above are multiplied by this. Key lines (story
    // beats, cutscenes, Slice low) always play. "Key lines" drops every Bark and Normal line.
    public static float GapScale(int chatter) {
        return chatter <= 1 ? 2.5f : chatter == 2 ? 1f : 0.5f;
    }

    // True when the cast gap for this priority has cooled (Key always ready). Used to hold a queued Normal reply
    // without burning a Deny each frame.
    public static bool GapReady(Priority priority) {
        if (priority == Priority.Key) {
            return true;
        }
        float scale = GapScale(SoundSettings.Chatter);
        float since = Time.unscaledTime - lastCastAt;
        float need = (priority == Priority.Bark ? BarkGap : NormalGap) * scale;
        return since >= need;
    }

    // True when a line may be said right now. Every priority needs a free floor (Key lines wait for it, they are not
    // dropped). A line that is allowed is not yet logged: call Spoke() when it really starts.
    public static bool Allow(string line, string why, Priority priority) {
        float now = Time.unscaledTime;
        if (Scene && why != "cutscene") {
            return Deny(why, "cutscene");
        }
        if (!WallitVoice.FloorFree) {
            return false;
        }
        if (priority == Priority.Key) {
            return true;
        }
        int chatter = SoundSettings.Chatter;
        if (chatter <= SoundSettings.ChatterOff) {
            return Deny(why, "voices-off");
        }
        float scale = GapScale(chatter);
        bool bot = why.StartsWith("bot-");
        float since = now - (bot ? LastLineAt : lastCastAt);
        if (since < (bot ? BotGap : priority == Priority.Bark ? BarkGap : NormalGap) * scale) {
            return Deny(why, "gap");
        }
        float at;
        float cooldown = IsEvent(why) ? EventCooldown : LineCooldown;
        if (lineAt.TryGetValue(line, out at) && now - at < cooldown * scale) {
            return Deny(why, "line-cooldown");
        }
        if (priority == Priority.Bark && reasonAt.TryGetValue(why, out at) && now - at < (bot ? BotReasonCooldown : ReasonCooldown) * scale) {
            return Deny(why, "reason-cooldown");
        }
        return true;
    }

    static bool Deny(string why, string cause) {
        Denied++;
        Events.Add("drop|" + why + "|" + cause);
        return false;
    }

    // A line started: stamp the gaps.
    public static void Spoke(string line, string why, Priority priority) {
        float now = Time.unscaledTime;
        float since = now - LastLineAt;
        if (LastLineAt > -900f) {
            SmallestGap = Mathf.Min(SmallestGap, since);
            if (priority == Priority.Bark) {
                SmallestBarkGap = Mathf.Min(SmallestBarkGap, since);
            }
        }
        LastLineAt = now;
        if (!why.StartsWith("bot-")) {
            lastCastAt = now;
        }
        lineAt[line] = now;
        reasonAt[why] = now;
        Played++;
        if (priority == Priority.Key) {
            KeyPlayed++;
        }
        Events.Add("play|" + why + "|" + priority + "|" + line);
        GameAudio.NoteOther(GameAudio.Bus.Voice, line);
    }
}
