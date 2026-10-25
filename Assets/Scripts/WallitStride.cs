using UnityEngine;

// What Wallit's feet do on the ground, measured on the live rig, so locomotion can be matched to it and judged.
// CharacterMovement feeds it once a frame while a locomotion clip plays alone (no blend, no action).
//   Slide  = world speed of the planted foot (the lower ankle, within PlantHeight of the floor). 0 is a foot nailed down.
//   Stride = the ground speed a clip covers at playback speed 1 (the planted foot's speed against the body, at scale 1).
//            CharacterMovement divides the real ground speed by it to pick the playback speed, and keeps learning it.
//   Pop    = the largest one-frame jump of the hips against the body, a pop that blending should never show.
public static class WallitStride {
    public const float PlantHeight = 0.05f;

    public struct Tally {
        public int Samples;
        public float SlideSum;
        public float SlideMax;
        public float StrideSum;
    }

    static readonly Tally[] tallies = new Tally[CharacterMovement.ClipNames.Length];
    public static float PopMax { get; private set; }
    public static int PopFrames { get; private set; }

    public static void Reset() {
        for (int i = 0; i < tallies.Length; i++) {
            tallies[i] = default(Tally);
        }
        PopMax = 0f;
        PopFrames = 0;
    }

    public static void Add(int clip, float slide, float stride) {
        if (clip < 0 || clip >= tallies.Length) {
            return;
        }
        Tally t = tallies[clip];
        t.Samples++;
        t.SlideSum += slide;
        t.StrideSum += stride;
        if (slide > t.SlideMax) {
            t.SlideMax = slide;
        }
        tallies[clip] = t;
    }

    public static void AddPop(float jump, bool popped) {
        if (jump > PopMax) {
            PopMax = jump;
        }
        if (popped) {
            PopFrames++;
        }
    }

    public static int Samples(int clip) {
        return clip >= 0 && clip < tallies.Length ? tallies[clip].Samples : 0;
    }

    public static float SlideMean(int clip) {
        return Samples(clip) > 0 ? tallies[clip].SlideSum / tallies[clip].Samples : 0f;
    }

    public static float SlideMax(int clip) {
        return Samples(clip) > 0 ? tallies[clip].SlideMax : 0f;
    }

    public static float StrideMean(int clip) {
        return Samples(clip) > 0 ? tallies[clip].StrideSum / tallies[clip].Samples : 0f;
    }
}
