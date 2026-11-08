using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

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
        swing = default(Tally);
        swingLegsSum = 0f;
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

    // The planted foot's slide while a bat swing or a home-run wind-up plays and Wallit covers ground (the legs run
    // under the swing through StrideLayer), and how much of the legs that layer had on average.
    static Tally swing;
    static float swingLegsSum;

    public static void AddSwing(float slide, float legsWeight) {
        swing.Samples++;
        swing.SlideSum += slide;
        if (slide > swing.SlideMax) {
            swing.SlideMax = slide;
        }
        swingLegsSum += legsWeight;
    }

    public static int SwingSamples { get { return swing.Samples; } }
    public static float SwingSlideMean { get { return swing.Samples > 0 ? swing.SlideSum / swing.Samples : 0f; } }
    public static float SwingSlideMax { get { return swing.Samples > 0 ? swing.SlideMax : 0f; } }
    public static float SwingLegsMean { get { return swing.Samples > 0 ? swingLegsSum / swing.Samples : 0f; } }
}

// A second animation output laid over a humanoid Animator's own controller: sorted after it and started from its
// pose (PreviousInputs), so where this layer has no say the controller's pose shows through. It holds a few looping
// clips that crossfade and play in step at one shared phase, each at its own playback speed, and an optional
// AvatarMask so only some body parts take them. Wallit's legs keep running under a bat swing through it (legs mask),
// and Slice walks with it (no mask). Nothing here runs by itself: the owner calls Tick once a frame from Update,
// before the Animator evaluates, and Dispose when it goes.
public sealed class StrideLayer {
    // After the Animator's own controller output, and before an Animation Rigging output (1000) if one is ever added.
    public const ushort SortingOrder = 900;
    // Seconds for the clips inside the layer to crossfade into each other.
    public const float CrossFade = 0.14f;

    PlayableGraph graph;
    AnimationPlayableOutput output;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable[] inputs;
    float[] lengths;
    float[] rates;
    float[] weights;
    float[] targets;
    bool playing;

    public Animator Target { get; private set; }
    public int Count { get { return inputs != null ? inputs.Length : 0; } }
    // How much of the pose this layer has, 0..1 (its output weight over the controller).
    public float Weight { get; private set; }
    // Where in the cycle every clip is, 0..1 of each clip's own length.
    public float Phase { get; private set; }
    public float CyclesPerSecond { get; private set; }
    public bool Valid { get { return graph.IsValid(); } }

    // A humanoid clip that can be retargeted onto any humanoid avatar: it imported and it carries muscle curves.
    public static bool Usable(AnimationClip clip) {
        return clip != null && !clip.empty && clip.humanMotion && clip.length > 0.05f;
    }

    public static StrideLayer Create(Animator animator, AnimationClip[] clips, AvatarMask mask, string name) {
        if (animator == null || clips == null || clips.Length == 0) {
            return null;
        }
        for (int i = 0; i < clips.Length; i++) {
            if (!Usable(clips[i])) {
                return null;
            }
        }
        var s = new StrideLayer();
        s.Target = animator;
        s.graph = PlayableGraph.Create(name);
        s.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        s.mixer = AnimationMixerPlayable.Create(s.graph, clips.Length);
        s.inputs = new AnimationClipPlayable[clips.Length];
        s.lengths = new float[clips.Length];
        s.rates = new float[clips.Length];
        s.weights = new float[clips.Length];
        s.targets = new float[clips.Length];
        for (int i = 0; i < clips.Length; i++) {
            AnimationClipPlayable p = AnimationClipPlayable.Create(s.graph, clips[i]);
            p.SetApplyFootIK(true);
            p.SetApplyPlayableIK(false);
            // Time is set by hand each frame (Tick), so every clip sits at the same phase.
            p.SetSpeed(0.0);
            s.graph.Connect(p, 0, s.mixer, i);
            s.inputs[i] = p;
            s.lengths[i] = clips[i].length;
            s.rates[i] = 1f;
            s.weights[i] = i == 0 ? 1f : 0f;
            s.targets[i] = s.weights[i];
            s.mixer.SetInputWeight(i, s.weights[i]);
        }
        Playable top = s.mixer;
        if (mask != null) {
            AnimationLayerMixerPlayable layers = AnimationLayerMixerPlayable.Create(s.graph, 1);
            s.graph.Connect(s.mixer, 0, layers, 0);
            layers.SetInputWeight(0, 1f);
            layers.SetLayerMaskFromAvatarMask(0, mask);
            top = layers;
        }
        s.output = AnimationPlayableOutput.Create(s.graph, name, animator);
        s.output.SetSourcePlayable(top);
        s.output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        s.output.SetSortingOrder(SortingOrder);
        s.output.SetWeight(0f);
        return s;
    }

    // Legs only (both legs and their foot IK): the hips, spine, arms and head stay with the controller, and so does
    // every transform that is not a human bone (the bat, the bills, a coat tail). Human bones stay listed as active so
    // the transform list never takes a say over the humanoid parts above.
    public static AvatarMask LegsMask(Animator animator) {
        var mask = new AvatarMask();
        for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++) {
            bool legs = part == AvatarMaskBodyPart.LeftLeg || part == AvatarMaskBodyPart.RightLeg
                || part == AvatarMaskBodyPart.LeftFootIK || part == AvatarMaskBodyPart.RightFootIK;
            mask.SetHumanoidBodyPartActive(part, legs);
        }
        if (animator != null && animator.isHuman) {
            var human = new System.Collections.Generic.HashSet<Transform>();
            for (HumanBodyBones b = HumanBodyBones.Hips; b < HumanBodyBones.LastBone; b++) {
                Transform t = animator.GetBoneTransform(b);
                if (t != null) {
                    human.Add(t);
                }
            }
            Transform root = animator.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++) {
                mask.SetTransformPath(i, PathFrom(root, all[i]));
                mask.SetTransformActive(i, human.Contains(all[i]));
            }
        }
        return mask;
    }

    static string PathFrom(Transform root, Transform t) {
        if (t == root) {
            return "";
        }
        string path = t.name;
        for (Transform p = t.parent; p != null && p != root; p = p.parent) {
            path = p.name + "/" + path;
        }
        return path;
    }

    // One clip leads: its share goes to 1 and the rest to 0, over CrossFade (or at once with snap).
    public void Lead(int index, bool snap = false) {
        Mix(index, index, 0f, snap);
    }

    // Two clips share the layer: a gets 1 - t, b gets t.
    public void Mix(int a, int b, float t, bool snap = false) {
        if (inputs == null) {
            return;
        }
        t = Mathf.Clamp01(t);
        for (int i = 0; i < targets.Length; i++) {
            targets[i] = i == a && i == b ? 1f : i == a ? 1f - t : i == b ? t : 0f;
            if (snap) {
                weights[i] = targets[i];
            }
        }
    }

    // Playback speed of one clip (1 = as authored).
    public void SetRate(int index, float playback) {
        if (inputs != null && index >= 0 && index < rates.Length) {
            rates[index] = Mathf.Max(0f, playback);
        }
    }

    public void StartAt(float phase) {
        Phase = Mathf.Repeat(phase, 1f);
    }

    // Fades the layer toward weight over fadeSeconds, crossfades the clips, and moves the shared phase on by the
    // blend of the clips' cycle rates.
    public void Tick(float weight, float fadeSeconds, float dt) {
        if (!Valid || inputs == null) {
            return;
        }
        weight = Mathf.Clamp01(weight);
        Weight = fadeSeconds > 0.0001f ? Mathf.MoveTowards(Weight, weight, dt / fadeSeconds) : weight;
        float sum = 0f;
        for (int i = 0; i < weights.Length; i++) {
            weights[i] = Mathf.MoveTowards(weights[i], targets[i], dt / CrossFade);
            sum += weights[i];
        }
        float cps = 0f;
        for (int i = 0; i < weights.Length; i++) {
            float w = sum > 0.0001f ? weights[i] / sum : (i == 0 ? 1f : 0f);
            mixer.SetInputWeight(i, w);
            cps += w * rates[i] / Mathf.Max(0.05f, lengths[i]);
        }
        CyclesPerSecond = cps;
        Phase = Mathf.Repeat(Phase + cps * dt, 1f);
        for (int i = 0; i < inputs.Length; i++) {
            inputs[i].SetTime(Phase * lengths[i]);
        }
        output.SetWeight(Weight);
        if (Weight > 0f && !playing) {
            graph.Play();
            playing = true;
        } else if (Weight <= 0f && playing) {
            graph.Stop();
            playing = false;
        }
    }

    public void Dispose() {
        if (graph.IsValid()) {
            graph.Destroy();
        }
        inputs = null;
        playing = false;
        Weight = 0f;
    }
}
