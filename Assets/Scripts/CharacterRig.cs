using System.Collections.Generic;
using UnityEngine;

// The animation state of one Yo Bot or Spider Bot body: idle, walk, wind-up, strike, flinch, stagger and death. Enemy owns one
// per bot, feeds it the bot's ground speed every frame and reads its pose weights back onto the body's parts (the legs, claws,
// head and body that BindParts found); the rig itself touches no transform, so the same clip drives the shared bots and the
// authored bosses built on them (the Courier and the Plated Alpha from Yo Bots; the Server, Registrar and Order from Spider Bots).
//
// The walk is one clip: a cycle of CycleSeconds at playback 1 that covers StrideLength of ground. Playback is ground speed over
// the clip's speed (StrideLength / CycleSeconds), so a bot that is slowed, shoved or running faster in phase two plants its feet
// at the pace it really moves and never skates. Every frame can be traced (Tracing): state, speed, playback and cycle, with a
// count of frames where the state and the motion disagree (Mismatches), which a regression must keep at zero.
public enum RigState { Idle, Walk, Windup, Strike, Flinch, Stagger, Death }

public sealed class CharacterRig {
    public const float CycleSeconds = 0.8f;
    public const float YoStride = 1.3f;        // model-space metres one walk cycle covers
    public const float SpiderStride = 1.6f;
    public const float YoWind = 0.45f;         // the claw goes up this long before the swipe lands
    public const float SpiderWind = 0.50f;     // the front leg rears this long before the stab
    public const float StrikeSeconds = 0.22f;
    public const float FlinchSeconds = 0.35f;
    public const float DeathSeconds = 0.9f;    // the death clip: stumble, limbs fold, drop
    public const float DeathTotal = 1.5f;      // the body lies still, then shrinks out over the last DeathFade
    public const float DeathFade = 0.3f;
    const float MoveFloor = 0.12f;             // model-space speed under which a bot counts as standing

    // ---- pose weights, read by Enemy
    public RigState State { get; private set; }
    public float Walk;        // 0..1 how much the stride shows
    public float Cycle;       // radians, one full turn per walk cycle
    public float Step;        // sin(Cycle)
    public float Wind;        // 0..1 the ordinary attack wind-up
    public float Strike;      // 1 at the moment of the blow, easing to 0
    public float Rear;        // 0..1 a boss's tell pose, held while its ground marks fill
    public float FlinchAmp;   // 0..1.4 the hit reaction
    public float Stagger;     // 0..1 a boss's phase-two stagger, or an interrupted wind-up
    public float Death;       // 0..1 progress of the death clip
    public float Fade;        // 0..1 the last moments before the body is removed
    public float Playback;    // ground speed over the clip's speed
    public float Speed;       // smoothed ground speed in model space

    public readonly bool Spider;
    public readonly float StrideLength;
    public float ClipSpeed { get { return StrideLength / CycleSeconds; } }
    public float WindSeconds { get { return Spider ? SpiderWind : YoWind; } }
    public float DeathAge { get; private set; }
    public bool Dying { get { return dying; } }

    readonly int id;
    string label;
    bool tell, dying, slam;
    float pendingStagger;
    bool sawWalk, sawIdle;
    float windT, windDur, strikeT, flinchT, staggerT, staggerHold;
    float rawWindow, rawTime, distWindow, cycleWindow, strideTime, mismatchTime;
    float peakRear, peakStagger;

    public CharacterRig(int id, string label, bool spider) {
        this.id = id;
        this.label = label;
        Spider = spider;
        StrideLength = spider ? SpiderStride : YoStride;
    }

    public void Label(string title) { label = title; }
    public bool InTell { get { return tell; } }

    // ---- what the body is told

    public void BeginWindup(float seconds) {
        if (dying)
            return;
        windDur = Mathf.Max(0.05f, seconds);
        windT = 0f;
        Windups++;
    }

    public void BeginStrike() {
        if (dying)
            return;
        windDur = 0f;
        windT = 0f;
        strikeT = StrikeSeconds;
        Strike = 1f;
        Strikes++;
    }

    public void CancelWind() {
        windDur = 0f;
        windT = 0f;
    }

    public void Flinch(float strength) {
        if (dying)
            return;
        float amp = Mathf.Clamp(strength, 0.3f, 1.4f);
        if (flinchT > FlinchSeconds * 0.6f && amp <= FlinchAmp)
            return;   // already reeling harder
        flinchT = FlinchSeconds;
        FlinchAmp = amp;
        FlinchesPlayed++;
    }

    // A boss's ground-mark tell: rear back and hold until EndTell. Distinct from the ordinary wind-up (both arms overhead for a
    // Yo Bot body, the front legs and the chest up for a Spider Bot body, with a tremble).
    public void BeginTell() {
        if (dying)
            return;
        tell = true;
        peakRear = Rear;
        Tells++;
    }

    public void EndTell(bool slamDown) {
        if (!tell)
            return;
        tell = false;
        TellPeaks.Add(peakRear);
        if (slamDown && !dying) {
            slam = true;
            strikeT = StrikeSeconds * 1.6f;
            Strike = 1f;
            Strikes++;
        }
        // A stagger asked for in the middle of the tell (a phase change) plays once the tell is over.
        if (pendingStagger > 0f) {
            float wait = pendingStagger;
            pendingStagger = 0f;
            BeginStagger(wait);
        }
    }

    // Phase two begins, or a wind-up is broken: the body lurches and buckles for the given time.
    public void BeginStagger(float seconds) {
        if (dying)
            return;
        if (tell) {
            pendingStagger = Mathf.Max(pendingStagger, seconds);
            return;
        }
        windDur = 0f;
        staggerT = 0f;
        staggerHold = Mathf.Max(0.3f, seconds);
        peakStagger = 0f;
        Staggers++;
    }

    public void BeginDeath() {
        if (dying)
            return;
        dying = true;
        DeathAge = 0f;
        windDur = 0f;
        if (tell)
            EndTell(false);
        Deaths++;
    }

    // ---- the frame

    // dt is the frame's game time, speed the ground speed in model space (metres per second over the body's scale), excused is
    // true while the bot is shoved, in the air, frozen or held (its state may then differ from its motion, by design).
    public void Tick(float dt, float rawSpeed, bool excused) {
        if (dt <= 0f)
            return;

        // Death owns the body.
        if (dying) {
            DeathAge += dt;
            Death = Mathf.Clamp01(DeathAge / DeathSeconds);
            Fade = Mathf.Clamp01((DeathAge - (DeathTotal - DeathFade)) / DeathFade);
            Walk = Mathf.MoveTowards(Walk, 0f, dt * 8f);
            Wind = Rear = Stagger = 0f;
            FlinchAmp = 0f;
            flinchShown = 0f;
            State = RigState.Death;
            Record(dt, rawSpeed);
            return;
        }

        // Ground speed, smoothed a hair so a one-frame hitch does not flick the legs.
        Speed = Mathf.Lerp(Speed, rawSpeed, 1f - Mathf.Exp(-dt / 0.06f));
        bool moving = Speed > MoveFloor;
        float want = moving ? Mathf.Clamp(Speed / (ClipSpeed * 1.5f), 0.45f, 1f) : 0f;
        Walk = Mathf.MoveTowards(Walk, want, dt * 7f);
        Playback = moving ? Speed / ClipSpeed : 0f;
        // The phase advances by distance covered: the feet keep pace with the ground at any speed.
        if (moving) {
            float turn = Speed / StrideLength * dt * Mathf.PI * 2f;
            Cycle = Mathf.Repeat(Cycle + turn, Mathf.PI * 2f);
        }
        Step = Mathf.Sin(Cycle);

        // Wind-up eases in, the strike snaps out, flinch and stagger ease away.
        if (windDur > 0f) {
            windT += dt;
            Wind = Mathf.Clamp01(windT / windDur);
            Wind = 1f - (1f - Wind) * (1f - Wind);
        }
        else
            Wind = Mathf.MoveTowards(Wind, 0f, dt * 6f);
        if (strikeT > 0f) {
            strikeT -= dt;
            Strike = Mathf.Clamp01(strikeT / StrikeSeconds);
            if (strikeT <= 0f) {
                Strike = 0f;
                slam = false;
            }
        }
        if (flinchT > 0f) {
            flinchT -= dt;
            float k = Mathf.Clamp01(flinchT / FlinchSeconds);
            float shape = k > 0.8f ? (1f - k) / 0.2f : k / 0.8f;   // a quick rock back, then easing home
            flinchShown = shape * FlinchAmp;
            if (flinchT <= 0f) {
                flinchShown = 0f;
                FlinchAmp = 0f;
            }
        }
        else
            flinchShown = 0f;
        staggerT += dt;
        if (staggerHold > 0f) {
            float s = staggerT < 0.15f ? staggerT / 0.15f : staggerT < staggerHold ? 1f : Mathf.Clamp01(1f - (staggerT - staggerHold) / 0.35f);
            Stagger = s;
            peakStagger = Mathf.Max(peakStagger, s);
            if (staggerT >= staggerHold + 0.35f) {
                StaggerPeaks.Add(peakStagger);
                staggerHold = 0f;
                Stagger = 0f;
            }
        }
        Rear = Mathf.MoveTowards(Rear, tell ? 1f : 0f, dt * (tell ? 4f : 9f));
        if (tell)
            peakRear = Mathf.Max(peakRear, Rear);

        // The label: the most urgent thing the body is doing.
        if (Stagger > 0.05f)
            State = RigState.Stagger;
        else if (flinchShown > 0.12f)
            State = RigState.Flinch;
        else if (Strike > 0.05f)
            State = RigState.Strike;
        else if (tell || Wind > 0.05f || Rear > 0.05f)
            State = RigState.Windup;
        else
            State = moving ? RigState.Walk : RigState.Idle;

        Check(dt, rawSpeed, excused);
        Record(dt, rawSpeed);
    }

    // The amplitude Enemy applies for a flinch (the shape runs inside Tick).
    float flinchShown;
    public float FlinchPose { get { return flinchShown; } }

    // ---- the checks: the label against the motion, the feet against the ground

    void Check(float dt, float rawSpeed, bool excused) {
        if (!Tracing)
            return;
        bool walkOrIdle = State == RigState.Walk || State == RigState.Idle;
        if (!walkOrIdle || excused) {
            rawWindow = rawTime = distWindow = cycleWindow = strideTime = mismatchTime = 0f;
            sawWalk = sawIdle = false;
            return;
        }
        // The label against the raw ground speed over a quarter second. A window in which the label changed (the bot started or
        // stopped inside it) says nothing: only a label held the whole window can disagree with the motion.
        rawWindow += rawSpeed * dt;
        rawTime += dt;
        if (State == RigState.Walk)
            sawWalk = true;
        else
            sawIdle = true;
        if (rawTime >= 0.25f) {
            float avg = rawWindow / rawTime;
            if ((sawWalk && !sawIdle && avg < MoveFloor * 0.5f) || (sawIdle && !sawWalk && avg > MoveFloor * 3f)) {
                Mismatches++;
                Note("mismatch|" + label + "|" + State + "|speed " + avg.ToString("0.00"));
            }
            rawWindow = rawTime = 0f;
            sawWalk = sawIdle = false;
        }
        // The feet against the ground over a second: cycles walked times the stride against the distance covered.
        if (State == RigState.Walk) {
            distWindow += rawSpeed * dt;
            cycleWindow += Speed / StrideLength * dt;
            strideTime += dt;
            if (strideTime >= 1f) {
                if (distWindow > 0.4f) {
                    float ratio = cycleWindow * StrideLength / distWindow;
                    if (ratio < 0.7f || ratio > 1.3f) {
                        Mismatches++;
                        Note("slide|" + label + "|feet cover " + (cycleWindow * StrideLength).ToString("0.00") + " of " + distWindow.ToString("0.00") + " m");
                    }
                }
                distWindow = cycleWindow = strideTime = 0f;
            }
        }
        else
            distWindow = cycleWindow = strideTime = 0f;
    }

    // ---- the trace

    public struct Sample {
        public float time;
        public int id;
        public RigState state;
        public float speed, playback, cycle;
    }

    public static bool Tracing;
    public static int Mismatches, Windups, Strikes, FlinchesPlayed, Staggers, Deaths, Tells, Frames;
    public static readonly List<Sample> Trace = new List<Sample>();
    public static readonly List<float> TellPeaks = new List<float>();      // each boss tell's highest Rear
    public static readonly List<float> StaggerPeaks = new List<float>();   // each stagger's highest weight
    public static readonly List<float> DeathClips = new List<float>();     // seconds of death clip at removal, one per bot
    public static readonly List<string> Notes = new List<string>();
    public static readonly int[] StateFrames = new int[7];
    public const int TraceCap = 120000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() {
        Tracing = false;
        Mismatches = Windups = Strikes = FlinchesPlayed = Staggers = Deaths = Tells = Frames = 0;
        Trace.Clear();
        TellPeaks.Clear();
        StaggerPeaks.Clear();
        DeathClips.Clear();
        Notes.Clear();
        for (int i = 0; i < StateFrames.Length; i++)
            StateFrames[i] = 0;
    }

    public static void StartTrace() {
        Reset();
        Tracing = true;
    }

    void Record(float dt, float rawSpeed) {
        if (!Tracing)
            return;
        Frames++;
        StateFrames[(int)State]++;
        if (Trace.Count < TraceCap)
            Trace.Add(new Sample { time = Time.time, id = id, state = State, speed = rawSpeed, playback = Playback, cycle = Cycle });
    }

    static void Note(string line) {
        if (Notes.Count < 200)
            Notes.Add(line);
    }

    // Called by Enemy when the dead body is taken away: how long the death clip had played.
    public void Removed() {
        if (!dying)
            return;
        DeathClips.Add(DeathAge);
        Note("removed|" + label + "|death clip " + DeathAge.ToString("0.00") + " s");
    }

    public static string Describe(RigState state, float rear, float stagger) {
        return state + " rear " + rear.ToString("0.00") + " stagger " + stagger.ToString("0.00");
    }
}
