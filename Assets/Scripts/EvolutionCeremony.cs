using System.Collections.Generic;
using UnityEngine;

// The beat when two maxed boons become one: the existing stamp and home-run stingers, soft gold rings round Wallit and Slice's
// circle, one of Wallit's own recorded lines, and a banner ("EVOLVED  Thunder Bat") that the picker's close lets through.
// It never freezes anything (the picker's own stamp hold is the only pause) and never touches the touch pads, so a phone
// has its stick and Swing back the moment the card closes. The evolved boon then sits on the HUD (RunHud.EvolvedRow).
public static class EvolutionCeremony {
    // For the playtest: what played, in order ("<boon> wave <n>: stamp, homerun, rings, line, banner").
    public static readonly List<string> Played = new List<string>();
    public static int Count { get; private set; }
    public static float LastAt { get; private set; }
    public static string LastName { get; private set; } = "";

    public static void Reset() {
        Played.Clear();
        Count = 0;
        LastAt = -1f;
        LastName = "";
    }

    public static void Play(BoonId id) {
        BoonInfo info = RunBoons.Info(id);
        Count++;
        LastAt = Time.unscaledTime;
        LastName = info.Name;
        UiSound.Stamp(1f, 0.7f);
        GameAudio.Play("homerun", 0.8f, 0.85f);
        if (PlayerGrowth.Instance != null) {
            Vector3 at = PlayerGrowth.Instance.transform.position;
            float scale = PlayerGrowth.Instance.Scale;
            Fx.Ring(at, 7f * scale, new Color(1f, 0.82f, 0.3f, 0.9f), 0.7f);
            Fx.Ring(at, 13f * scale, new Color(1f, 0.93f, 0.7f, 0.55f), 1.0f);
        }
        if (Musician.Instance != null) {
            Fx.Ring(Musician.Instance.transform.position, 9f, new Color(1f, 0.82f, 0.3f, 0.6f), 0.9f);
        }
        WallitVoice.OnEvolve();
        UpgradeLook.Say("EVOLVED!  " + info.Name, 2.6f, 200f);
        Played.Add(info.Name + " wave " + WaveDirector.Wave + ": stamp, homerun, rings, line, banner");
    }
}
