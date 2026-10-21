using System.Collections.Generic;
using UnityEngine;

// The bat: pure melee, in place of the bow. Click to swing a 3-hit combo (the third swing hits harder and knocks
// birds further). A small target, or an arc full of small ones, gets the bat swung down at the ground instead: a
// small area hit. Hold to wind up and let go for a home run: one bird flies far and bowls over the birds it meets.
// Without a lock you turn to the nearest bird in the arc. From the auto wave it swings by itself at birds in reach.
// Every hit goes through Combat.BatHit, so the run's boons apply; the bat boons that change the swing itself (Wide
// Arc, Follow Through, Sweet Spot, Line Drive, Grand Slam's spin, Long Ball's bolts, Blizzard Bat's frost) and the
// metal bat act here. Until the new model, the bat is a stand-in: two capsules on the right hand, posed in code, with
// the old recoil clip as the body's swing. UpgradeLook.DressBat puts each boon's mark on it.
public class BatEquipment : MonoBehaviour {
    public static BatEquipment Instance { get; private set; }

    // What the bat has done this run, for the playtest.
    public static int Swings { get; private set; }
    public static int Connects { get; private set; }
    public static int Finishers { get; private set; }
    public static int Smashes { get; private set; }
    public static int SmashHits { get; private set; }
    public static int HomeRuns { get; private set; }
    public static int HomeRunKnocks { get; private set; }
    public static int KnockedBack { get; private set; }
    // The bat boons, for the playtest: wide swings, spins, bolts batted and landed, line-drive hits, frost gusts,
    // metal-bat blasts.
    public static int WideSwings { get; private set; }
    public static int Spins { get; private set; }
    public static int SpinHits { get; private set; }
    public static int BoltsBatted { get; private set; }
    public static int BoltHits { get; private set; }
    public static int LineDriveHits { get; private set; }
    public static int FrostGusts { get; private set; }
    public static int MetalBlasts { get; private set; }
    public static int AnimContacts { get; private set; }
    // One line per thing worth seeing ("swing 3: 2 birds hit..."). The playtest drains it; capped so nobody has to.
    public static readonly List<string> Events = new List<string>();

    public static bool IsAnimatingAction {
        get { return Instance != null && (Instance.move != Move.None || Instance.charging); }
    }

    enum Move {
        None,
        Swing1,
        Swing2,
        Swing3,
        Smash,
        HomeRun,
        Spin
    }

    Animator anim;
    ZeldaStyleCamera cam;
    Transform hand;
    Transform modelBat;
    Vector3 modelBatTip = new Vector3(0f, 0f, 0.85f);
    Transform root;

    Move move = Move.None;
    Move queued = Move.None;
    float moveTime;
    float moveLength;
    float windup;
    float sweep = Sweep;
    bool contactDone;
    Enemy faceTarget;
    Vector2 poseFrom;
    Vector2 pose = new Vector2(140f, 60f);
    Vector2 windPose;
    Vector2 sweepFrom;
    Vector2 sweepTo;
    float gripBlend;

    int comboStep;
    float idleTime = 99f;
    bool pressing;
    float pressTime;
    bool charging;
    float charge;
    bool chargeFull;
    float homeRunCharge;
    float autoIn;
    int swingCount;

    readonly List<Enemy> buffer = new List<Enemy>();
    readonly List<Enemy> hits = new List<Enemy>();
    readonly List<Flight> flights = new List<Flight>();
    System.Comparison<Enemy> byDistance;
    Vector3 sortOrigin;

    static readonly Vector2 Rest = new Vector2(140f, 60f);
    const float Sweep = 0.1f;

    // A bird the bat sent flying: whatever it flies into gets hit and shoved along.
    class Flight {
        public Enemy Bird;
        public float Left;
        public int HitsLeft;
        public float Scale;
        public bool HomeRun;
        public Vector3 Last;
        public Vector3 Heading;
        public int Bowled;
        public readonly List<Enemy> Struck = new List<Enemy>();
    }

    public static void BeginRun() {
        Swings = 0;
        Connects = 0;
        Finishers = 0;
        Smashes = 0;
        SmashHits = 0;
        HomeRuns = 0;
        HomeRunKnocks = 0;
        KnockedBack = 0;
        WideSwings = 0;
        Spins = 0;
        SpinHits = 0;
        BoltsBatted = 0;
        BoltHits = 0;
        LineDriveHits = 0;
        FrostGusts = 0;
        MetalBlasts = 0;
        AnimContacts = 0;
        Events.Clear();
    }

    // Movement slows a little while a swing is out, more while winding up. CharacterMovement multiplies by it.
    public static float MoveFactor {
        get {
            BatEquipment bat = Instance;
            if (bat == null) {
                return 1f;
            }
            if (bat.charging) {
                return 0.5f;
            }
            return bat.move != Move.None ? 0.72f : 1f;
        }
    }

    // 0..1 while winding up a home run.
    public static float Charge {
        get { return Instance != null && Instance.charging ? Instance.charge : 0f; }
    }

    // How far the bat reaches from your middle, in meters.
    public static float Reach {
        get {
            float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
            return Tuning.BatReach * grown;
        }
    }

    void Awake() {
        Instance = this;
        BeginRun();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Start() {
        BindAnimator();
        CharacterMovement movement = GetComponent<CharacterMovement>();
        if (movement != null) {
            cam = movement.ZeldaCam;
        }
        BuildBat();
    }

    void BindAnimator() {
        Animator found = GetComponentInChildren<Animator>();
        if (found == null || found == anim) {
            return;
        }
        anim = found;
        if (anim.gameObject.GetComponent<WallitAnimRelay>() == null) {
            anim.gameObject.AddComponent<WallitAnimRelay>();
        }
        EnsureClipEvents(anim);
        hand = FindBone(anim.transform, "RightHand");
        BindModelBat();
        if (root != null && anim != null && root.parent != anim.transform) {
            root.SetParent(anim.transform, false);
        }
    }

    void BindModelBat() {
        modelBat = null;
        if (hand != null) {
            Transform childBat = hand.Find("Bat");
            if (childBat != null) {
                MeshFilter mf = childBat.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && childBat.Find("Barrel") == null) {
                    modelBat = childBat;
                    Bounds b = mf.sharedMesh.bounds;
                    if (b.size.z >= b.size.y && b.size.z >= b.size.x) {
                        modelBatTip = new Vector3(0f, 0f, b.center.z >= 0f ? b.size.z : -b.size.z);
                    } else if (b.size.y >= b.size.x) {
                        modelBatTip = new Vector3(0f, b.center.y >= 0f ? b.size.y : -b.size.y, 0f);
                    } else {
                        modelBatTip = new Vector3(b.center.x >= 0f ? b.size.x : -b.size.x, 0f, 0f);
                    }
                } else {
                    Destroy(childBat.gameObject);
                }
            }
        }
        if (root != null) {
            bool showFallback = modelBat == null;
            Transform handlePart = root.Find("Handle");
            Transform barrelPart = root.Find("Barrel");
            if (handlePart != null && handlePart.GetComponent<Renderer>() != null) {
                handlePart.GetComponent<Renderer>().enabled = showFallback;
            }
            if (barrelPart != null && barrelPart.GetComponent<Renderer>() != null) {
                barrelPart.GetComponent<Renderer>().enabled = showFallback;
            }
        }
    }

    static void EnsureClipEvents(Animator animator) {
        if (animator == null || animator.runtimeAnimatorController == null) {
            return;
        }
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null) {
            return;
        }
        for (int i = 0; i < clips.Length; i++) {
            AnimationClip clip = clips[i];
            if (clip == null) {
                continue;
            }
            float norm = ContactNormForClip(clip.name);
            if (norm <= 0f) {
                continue;
            }
            bool hasEvent = false;
            AnimationEvent[] existing = clip.events;
            if (existing != null) {
                for (int e = 0; e < existing.Length; e++) {
                    if (existing[e] != null && existing[e].functionName == "OnBatContact") {
                        hasEvent = true;
                        break;
                    }
                }
            }
            if (!hasEvent && clip.length > 0.05f) {
                AnimationEvent ev = new AnimationEvent {
                    functionName = "OnBatContact",
                    time = clip.length * norm
                };
                clip.AddEvent(ev);
            }
        }
    }

    static float ContactNormForClip(string clipName) {
        if (string.IsNullOrEmpty(clipName)) {
            return -1f;
        }
        if (clipName.EndsWith("swing1")) return 0.42f;
        if (clipName.EndsWith("swing2")) return 0.42f;
        if (clipName.EndsWith("swing3")) return 0.45f;
        if (clipName.EndsWith("groundSmash")) return 0.48f;
        if (clipName.EndsWith("homeRunRelease")) return 0.35f;
        return -1f;
    }

    // AnimationEvent receiver (relayed from Wallit's Animator by WallitAnimRelay) at the exact frame the bat connects.
    public void OnBatContact() {
        if (move != Move.None && !contactDone) {
            contactDone = true;
            AnimContacts++;
            Contact();
        }
    }

    void Update() {
        if (anim == null || !anim.gameObject.activeInHierarchy) {
            BindAnimator();
        }
        float dt = Time.deltaTime;
        TickFlights(dt);

        bool canAct = !WaveDirector.IsBuilding && !WaveDirector.IsLost && !LevelUpScreen.IsOpen && !LevelUpScreen.HoldFire
            && !PauseMenu.IsOpen && !PauseMenu.HoldFire;
        if (!canAct) {
            pressing = false;
            charging = false;
            charge = 0f;
            queued = Move.None;
        } else {
            ReadInput(dt);
        }

        if (move != Move.None) {
            moveTime += dt;
            if (!contactDone && moveTime >= windup + sweep * 0.5f) {
                contactDone = true;
                AnimContacts++;
                Contact();
            }
            // A click during a swing waits for the bat to come through, then goes straight into the next one.
            bool chainable = contactDone && moveTime >= windup + sweep + 0.08f / Mathf.Max(0.3f, RunBoons.DrawSpeed);
            if (queued != Move.None && chainable) {
                Begin(queued);
            } else if (moveTime >= moveLength) {
                move = Move.None;
                idleTime = 0f;
            }
        } else {
            idleTime += dt;
            if (idleTime > Tuning.ComboGap) {
                comboStep = 0;
            }
            if (queued != Move.None) {
                Begin(queued);
            }
        }

        autoIn -= dt;
        bool auto = canAct && !pressing && move == Move.None && queued == Move.None && WaveDirector.Wave >= MetaProgress.AutoWave();
        if (auto && autoIn <= 0f && Nearest(Reach * 1.05f, 180f) != null) {
            float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
            autoIn = Mathf.Lerp(0.5f, 0.28f, Mathf.Clamp01((grown - 1f) / 4f)) * MetaProgress.DrawHaste() / RunBoons.DrawSpeed;
            Begin(Move.Swing1);
        }
    }

    // A quick click swings the next combo swing; a hold winds up, and letting go past HomeRunMin is a home run.
    void ReadInput(float dt) {
        bool held = PlayInput.GetMouseButton(0);
        if (PlayInput.GetMouseButtonDown(0)) {
            pressing = true;
            pressTime = 0f;
        }
        if (!pressing) {
            return;
        }
        if (held) {
            pressTime += dt;
            if (pressTime >= Tuning.ChargeStart && (move == Move.None || moveTime >= moveLength - 0.05f)) {
                if (!charging) {
                    charging = true;
                    charge = 0f;
                    chargeFull = false;
                    move = Move.None;
                    CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HOMERUN_CHARGE);
                } else if (anim != null) {
                    anim.SetInteger("Clip", CharacterMovement.CLIP_HOMERUN_CHARGE);
                }
                charge = Mathf.Min(1f, charge + dt * RunBoons.DrawSpeed / Tuning.ChargeSeconds);
                if (charge >= 1f && !chargeFull) {
                    chargeFull = true;
                    Fx.Ring(transform.position, Reach * 0.9f, new Color(1f, 0.85f, 0.45f, 0.8f), 0.35f);
                    GameAudio.Play("batswing", 0.35f, 0.6f);
                }
            }
            return;
        }
        pressing = false;
        if (charging && charge >= Tuning.HomeRunMin) {
            homeRunCharge = charge;
            charging = false;
            charge = 0f;
            queued = Move.HomeRun;
            return;
        }
        charging = false;
        charge = 0f;
        queued = Move.Swing1;
    }

    // Starts a swing. Swing1 means "the next one in the combo", which may turn into a ground smash.
    void Begin(Move wanted) {
        queued = Move.None;
        Move next = wanted;
        faceTarget = PickTarget();
        if (wanted != Move.HomeRun) {
            next = comboStep == 0 ? Move.Swing1 : comboStep == 1 ? Move.Swing2 : Move.Swing3;
            comboStep = (comboStep + 1) % 3;
            if (ShouldSmash(faceTarget)) {
                next = Move.Smash;
            }
            // Grand Slam: every Nth swing is a full spin instead.
            int every = RunBoons.SpinEvery;
            if (every > 0 && (swingCount + 1) % every == 0) {
                next = Move.Spin;
                comboStep = 0;
            }
        } else {
            comboStep = 0;
        }

        move = next;
        moveTime = 0f;
        contactDone = false;
        poseFrom = pose;
        sweep = Sweep;
        float speed = Mathf.Max(0.3f, RunBoons.DrawSpeed);
        int wallitClip = CharacterMovement.CLIP_SWING_1;
        switch (next) {
            case Move.Swing1:
                windup = 0.09f;
                moveLength = 0.36f;
                windPose = new Vector2(115f, 15f);
                sweepFrom = windPose;
                sweepTo = new Vector2(-115f, 0f);
                wallitClip = CharacterMovement.CLIP_SWING_1;
                break;
            case Move.Swing2:
                windup = 0.09f;
                moveLength = 0.36f;
                windPose = new Vector2(-120f, 20f);
                sweepFrom = windPose;
                sweepTo = new Vector2(110f, 0f);
                wallitClip = CharacterMovement.CLIP_SWING_2;
                break;
            case Move.Swing3:
                windup = 0.15f;
                moveLength = 0.52f;
                windPose = new Vector2(135f, 40f);
                sweepFrom = windPose;
                sweepTo = new Vector2(-135f, -15f);
                wallitClip = CharacterMovement.CLIP_SWING_3;
                break;
            case Move.Smash:
                windup = 0.15f;
                moveLength = 0.48f;
                windPose = new Vector2(0f, 105f);
                sweepFrom = windPose;
                sweepTo = new Vector2(0f, -55f);
                wallitClip = CharacterMovement.CLIP_GROUND_SMASH;
                break;
            case Move.HomeRun:
                windup = 0.05f;
                moveLength = 0.62f;
                windPose = new Vector2(155f, 20f);
                sweepFrom = windPose;
                sweepTo = new Vector2(-150f, 5f);
                wallitClip = CharacterMovement.CLIP_HOMERUN_RELEASE;
                break;
            case Move.Spin:
                // All the way round: the bat goes from behind your right shoulder through 360 degrees.
                windup = 0.08f;
                moveLength = 0.6f;
                sweep = 0.26f;
                windPose = new Vector2(175f, 10f);
                sweepFrom = windPose;
                sweepTo = new Vector2(-185f, 5f);
                wallitClip = CharacterMovement.CLIP_SWING_3;
                break;
        }
        // Wide Arc: the combo swings come round further either side.
        float wide = RunBoons.ArcBonus * 0.5f;
        if (wide > 0f && (next == Move.Swing1 || next == Move.Swing2 || next == Move.Swing3)) {
            windPose.x += Mathf.Sign(windPose.x) * wide;
            sweepFrom = windPose;
            sweepTo.x += Mathf.Sign(sweepTo.x) * wide;
        }
        windup /= speed;
        moveLength /= speed;

        Swings++;
        swingCount++;
        RunLog.NoteSwing();
        bool big = next == Move.HomeRun || next == Move.Swing3 || next == Move.Spin;
        GameAudio.Play("batswing", big ? 0.6f : 0.42f, Random.Range(0.92f, 1.08f) * (next == Move.Swing3 ? 0.85f : next == Move.Spin ? 0.75f : 1f));
        CharacterMovement.PlayWallitClip(wallitClip, moveLength);
        if (anim != null && anim.layerCount > 1) {
            anim.SetTrigger("Fire");
        }
    }

    // The bird this swing is for: the locked one if you have it, else the nearest in front of you.
    Enemy PickTarget() {
        GameObject locked = cam != null ? cam.LockedOnGameObject : null;
        if (locked != null) {
            Enemy enemy = locked.GetComponent<Enemy>();
            if (enemy != null && enemy.Alive) {
                return enemy;
            }
        }
        return Nearest(Reach * 1.35f, 100f);
    }

    // Swing down at the ground when the target is small, or most birds in the arc are.
    bool ShouldSmash(Enemy target) {
        float limit = Tuning.SmashBelow * (PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f);
        bool close = target != null && Flat(target.transform.position - transform.position).magnitude <= Reach * 1.5f;
        if (close && target.BodyScale < limit) {
            return true;
        }
        Gather(Reach * 1.15f, ArcFor(Move.Swing1) * 0.5f, transform.forward, buffer);
        int small = 0;
        for (int i = 0; i < buffer.Count; i++) {
            if (buffer[i].BodyScale < limit) {
                small++;
            }
        }
        return buffer.Count > 0 && small * 2 > buffer.Count;
    }

    static float ArcFor(Move kind) {
        float arc = kind == Move.Swing3 ? Tuning.BatArcFinisher : Tuning.BatArc;
        return Mathf.Min(300f, arc + RunBoons.ArcBonus);
    }

    // The moment the bat meets whatever is in front.
    void Contact() {
        Vector3 forward = Flat(transform.forward);
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        float reach = Reach;
        hits.Clear();

        if (move == Move.HomeRun) {
            HomeRun(forward, reach, grown);
            return;
        }

        if (move == Move.Spin) {
            SpinContact(reach);
            LongBall(reach);
            return;
        }

        if (move == Move.Smash) {
            Vector3 center = transform.position + forward * reach * 0.55f;
            float radius = reach * 0.6f;
            List<Enemy> roster = Enemy.Roster;
            for (int i = 0; i < roster.Count; i++) {
                Enemy enemy = roster[i];
                if (enemy == null || !enemy.Alive || enemy.Climbing) {
                    continue;
                }
                Vector3 to = enemy.transform.position - center;
                to.y *= 0.5f;
                float r = radius + enemy.BodyScale * 0.3f;
                if (to.sqrMagnitude <= r * r) {
                    hits.Add(enemy);
                }
            }
            Fx.Ring(center, radius * 2f, new Color(0.8f, 0.72f, 0.58f, 0.75f), 0.4f);
            Fx.Emit(FxKind.Dust, center, 6, new Color(0.7f, 0.64f, 0.55f, 0.6f), 0.8f * Mathf.Sqrt(grown), 1.6f, radius * 0.5f);
            GameAudio.Play("batsmash", 0.8f, Random.Range(0.94f, 1.04f));
            Smashes++;
            int struck = Strike(hits, Tuning.SmashDamage, 1.5f, 3f, 5.5f);
            SmashHits += struck;
            Say("ground smash (target " + Size(faceTarget) + " under " + Tuning.SmashBelow.ToString("0.00") + " of yours): " + struck + " bot" + (struck == 1 ? "" : "s") + " hit");
            if (struck > 0) {
                Juice.HitStop(0.05f);
                ZeldaStyleCamera.Shake(0.08f, 0.16f);
            }
            LongBall(reach);
            return;
        }

        bool finisher = move == Move.Swing3;
        float halfArc = ArcFor(move) * 0.5f;
        Gather(reach, halfArc, forward, hits);
        // Follow Through: the nearest few in the arc; more with rank.
        int most = RunBoons.SwingHits(finisher);
        int inArc = hits.Count;
        if (hits.Count > most) {
            hits.RemoveRange(most, hits.Count - most);
        }
        if (RunBoons.ArcBonus > 0f) {
            WideSwings++;
        }
        Blizzard(reach, halfArc, forward, finisher);
        int count = Strike(hits, finisher ? Tuning.BatDamageFinisher : Tuning.BatDamage, finisher ? 4f : 2f,
            finisher ? Tuning.BatKnockFinisher : Tuning.BatKnock, finisher ? 5f : 2f);
        if (finisher && count > 0) {
            Finishers++;
        }
        string name = move == Move.Swing1 ? "swing 1" : move == Move.Swing2 ? "swing 2" : "swing 3 (finisher)";
        if (RunBoons.ArcBonus > 0f) {
            name += " (" + ArcFor(move).ToString("0") + " degree arc)";
        }
        if (count > 0) {
            Crack(finisher ? 1f : 0.75f, finisher ? 0.9f : Random.Range(0.98f, 1.1f));
            Juice.HitStop(finisher ? 0.07f : 0.035f);
            ZeldaStyleCamera.Shake(finisher ? 0.11f : 0.05f, finisher ? 0.2f : 0.12f);
            if (finisher && MetaProgress.MetalBat()) {
                MetalBlast(hits[0].transform.position, hits[0]);
            }
            Say(name + ": " + count + " bot" + (count == 1 ? "" : "s") + " hit" + (inArc > count ? " (" + inArc + " in the arc, " + most + " a swing)" : "")
                + ", knocked back " + (finisher ? Tuning.BatKnockFinisher : Tuning.BatKnock).ToString("0") + " m/s");
        } else {
            Say(name + ": a miss");
        }
        LongBall(reach);
    }

    // The crack of a hit. The metal bat rings higher and louder.
    static void Crack(float volume, float pitch) {
        if (MetaProgress.MetalBat()) {
            GameAudio.Play("batcrack", Mathf.Min(1f, volume * 1.25f), pitch * 1.3f);
        } else {
            GameAudio.Play("batcrack", volume, pitch);
        }
    }

    // The bat on each bot in the list. Knock is meters per second along the swing, lift straight up. A bot out on
    // the bat's outer part is on the sweet spot. Returns the count.
    int Strike(List<Enemy> targets, float scale, float power, float knock, float lift) {
        int count = 0;
        int lineDrive = RunBoons.LineDrive;
        float sweetFrom = Reach * Tuning.SweetSpotFrom;
        if (MetaProgress.MetalBat()) {
            knock *= Tuning.MetalKnock;
        }
        buffer.Clear();
        buffer.AddRange(targets);
        for (int i = 0; i < buffer.Count; i++) {
            Enemy enemy = buffer[i];
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            count++;
            Vector3 away = Flat(enemy.transform.position - transform.position);
            bool tip = away.magnitude - enemy.BodyScale * 0.3f >= sweetFrom;
            bool dropped = Combat.BatHit(enemy, scale, power, true, tip);
            if (!dropped && enemy != null) {
                enemy.Knock(KnockOf(enemy, away, knock, lift));
                KnockedBack++;
                if (lineDrive > 0) {
                    Track(enemy, 0.6f, lineDrive, 0.6f + 0.15f * lineDrive, false);
                }
            }
        }
        if (count > 0) {
            Connects++;
            RunLog.NoteConnect();
        }
        return count;
    }

    void HomeRun(Vector3 forward, float reach, float grown) {
        Enemy target = faceTarget;
        if (target == null || !target.Alive || Flat(target.transform.position - transform.position).magnitude - target.BodyScale * 0.3f > reach * 1.2f) {
            Gather(reach, ArcFor(Move.Swing1) * 0.5f, forward, hits);
            target = hits.Count > 0 ? hits[0] : null;
        }
        HomeRuns++;
        if (target == null) {
            Say("home run swing (charge " + Mathf.RoundToInt(homeRunCharge * 100f) + "%): a miss");
            return;
        }
        float power = Mathf.Lerp(0.6f, 1f, homeRunCharge);
        Vector3 away = Flat(target.transform.position - transform.position);
        if (away.sqrMagnitude < 0.01f) {
            away = forward;
        }
        away.Normalize();
        float heft = Heft(target);
        // A line drive: low enough to plough through the birds behind.
        Vector3 launch = away * Tuning.HomeRunSpeed * power * heft + Vector3.up * 5.5f * Mathf.Sqrt(heft);
        string kind = target.Title ?? target.name;

        // The launch first, so the bird flies even if the blow drops it (it then tumbles the same way).
        target.Knock(launch, 1.2f);
        Connects++;
        RunLog.NoteConnect();
        bool dropped = Combat.BatHit(target, Tuning.HomeRunDamage * power, 12f * power, true);
        if (dropped && target != null) {
            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null) {
                body.linearVelocity = launch;
            }
        }
        Track(target, 2.2f, 8, 0.9f * power, true);
        GameAudio.Play("homerun", 0.95f, Random.Range(0.97f, 1.03f));
        Fx.Ring(target.transform.position, 3f * Mathf.Sqrt(Mathf.Max(1f, target.BodyScale)), new Color(1f, 0.9f, 0.55f, 0.85f), 0.4f);
        Juice.HitStop(0.12f);
        ZeldaStyleCamera.Shake(0.2f, 0.28f);
        Say("home run (charge " + Mathf.RoundToInt(homeRunCharge * 100f) + "%): " + kind + " launched at " + launch.magnitude.ToString("0") + " m/s" + (dropped ? ", dropped" : ""));
        UpgradeLook.Say("HOME RUN!", 1.2f, -40f);
    }

    // Follows a bird the bat sent flying and hits whatever it flies into.
    void Track(Enemy bird, float seconds, int hitsLeft, float scale, bool homeRun) {
        var flight = new Flight {
            Bird = bird,
            Left = seconds,
            HitsLeft = hitsLeft,
            Scale = scale,
            HomeRun = homeRun,
            Last = bird.transform.position,
            Heading = Flat(bird.transform.position - transform.position)
        };
        flight.Struck.Add(bird);
        flights.Add(flight);
    }

    void TickFlights(float dt) {
        for (int f = flights.Count - 1; f >= 0; f--) {
            Flight flight = flights[f];
            flight.Left -= dt;
            if (flight.Bird == null || flight.Left <= 0f || flight.HitsLeft <= 0 || (!flight.HomeRun && !flight.Bird.Knocked)) {
                if (flight.HomeRun && flight.Bowled > 0) {
                    Say("home run knock: the launched bot bowled over " + flight.Bowled + " bot" + (flight.Bowled == 1 ? "" : "s"));
                }
                flights.RemoveAt(f);
                continue;
            }
            Vector3 at = flight.Bird.transform.position;
            Vector3 moved = Flat(at - flight.Last);
            if (moved.sqrMagnitude > 0.0004f) {
                flight.Heading = moved.normalized;
            }
            flight.Last = at;
            float radius = 0.5f + 0.35f * flight.Bird.BodyScale;
            buffer.Clear();
            buffer.AddRange(Enemy.Roster);
            for (int i = 0; i < buffer.Count && flight.HitsLeft > 0; i++) {
                Enemy other = buffer[i];
                if (other == null || !other.Alive || other.Climbing || flight.Struck.Contains(other)) {
                    continue;
                }
                Vector3 to = other.transform.position - at;
                to.y *= 0.5f;
                float reach = radius + other.BodyScale * 0.3f;
                if (to.sqrMagnitude > reach * reach) {
                    continue;
                }
                flight.Struck.Add(other);
                flight.HitsLeft--;
                flight.Bowled++;
                if (flight.HomeRun) {
                    HomeRunKnocks++;
                } else {
                    LineDriveHits++;
                    if (LineDriveHits <= 3) {
                        Say("line drive: a knocked-back " + (flight.Bird.Title ?? "bot") + " hit " + (other.Title ?? "a bot") + " behind it");
                    }
                }
                bool dropped = Combat.BatHit(other, flight.Scale, 5f, false);
                if (!dropped && other != null) {
                    Vector3 side = Flat(to);
                    Vector3 push = (flight.Heading + side.normalized * 0.6f).normalized;
                    other.Knock(KnockOf(other, push, flight.HomeRun ? 10f : 6f, flight.HomeRun ? 5f : 3f));
                    KnockedBack++;
                }
                GameAudio.Play("batcrack", 0.45f, Random.Range(1.2f, 1.4f));
            }
        }
    }

    // Grand Slam: the spin hits every bot around you, harder than a combo swing. Thunder Bat calls lightning too.
    void SpinContact(float reach) {
        float radius = reach * Tuning.SpinReach;
        hits.Clear();
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 to = Flat(enemy.transform.position - transform.position);
            float r = radius + enemy.BodyScale * 0.3f;
            if (to.sqrMagnitude <= r * r) {
                hits.Add(enemy);
            }
        }
        Spins++;
        Blizzard(radius, 180f, Flat(transform.forward), false);
        int count = Strike(hits, Tuning.SpinDamage, 4f, Tuning.BatKnockFinisher * 0.8f, 4f);
        SpinHits += count;
        bool thunder = RunBoons.Has(BoonId.StormQuiver);
        Fx.Ring(transform.position + Vector3.up * 0.3f, radius * 2f, thunder ? new Color(0.6f, 0.82f, 1f, 0.85f) : new Color(1f, 0.88f, 0.6f, 0.8f), 0.45f);
        if (thunder) {
            Combat.Lightning(transform.position + Vector3.up * 2f * (PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f), null, 5, Combat.BatBase() * 0.8f);
        }
        if (count > 0) {
            Crack(1f, 0.85f);
            Juice.HitStop(0.06f);
            ZeldaStyleCamera.Shake(0.1f, 0.18f);
        }
        Say("grand slam spin (360 degrees, " + radius.ToString("0.0") + " m): " + count + " bot" + (count == 1 ? "" : "s") + " hit" + (thunder ? ", lightning called down" : ""));
        UpgradeLook.Say("GRAND SLAM!", 0.9f, -40f);
    }

    // Blizzard Bat: frost blows through the swing's arc a little past the bat, slowing everything in it; the
    // finisher freezes.
    void Blizzard(float reach, float halfArc, Vector3 forward, bool freeze) {
        if (!RunBoons.Has(BoonId.Hailstorm)) {
            return;
        }
        Gather(reach * 1.4f, halfArc, forward, buffer);
        for (int i = 0; i < buffer.Count; i++) {
            Combat.Frost(buffer[i], freeze);
        }
        FrostGusts++;
        Fx.Emit(FxKind.Smoke, transform.position + forward * reach * 0.8f + Vector3.up * 0.6f, 4, new Color(0.82f, 0.93f, 1f, 0.5f), 1.4f * Mathf.Sqrt(Mathf.Max(1f, reach / Tuning.BatReach)), 2.5f, reach * 0.5f);
        if (FrostGusts <= 2) {
            Say("blizzard bat: frost through the arc, " + buffer.Count + " bot" + (buffer.Count == 1 ? "" : "s") + " slowed" + (freeze ? " and frozen" : ""));
        }
    }

    // Long Ball: the swing also bats a bolt at the nearest bot out of the bat's reach.
    void LongBall(float reach) {
        int rank = RunBoons.Rank(BoonId.Homing);
        if (rank <= 0) {
            return;
        }
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        float far = reach + Tuning.LongBallRange * grown * (1f + 0.5f * (rank - 1));
        Enemy best = null;
        float bestDist = far * far;
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 to = Flat(enemy.transform.position - transform.position);
            float d = to.sqrMagnitude;
            float gap = to.magnitude - enemy.BodyScale * 0.3f;
            if (gap <= reach || d >= bestDist) {
                continue;
            }
            bestDist = d;
            best = enemy;
        }
        if (best == null) {
            return;
        }
        Vector3 from = root != null ? root.position + root.up * 0.8f * grown : transform.position + Vector3.up * grown;
        BattedBolt.Launch(from, best, 0.55f + 0.25f * rank, grown);
        BoltsBatted++;
        if (BoltsBatted <= 3) {
            Say("long ball: a bolt batted at " + (best.Title ?? "a bot") + " " + Mathf.Sqrt(bestDist).ToString("0") + " m away (reach " + reach.ToString("0.0") + " m)");
        }
    }

    public static void NoteBoltHit() {
        BoltHits++;
    }

    // Aluminum! Not wood!: the finisher's crack goes off in a small blast around the bot.
    static void MetalBlast(Vector3 point, Enemy skip) {
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        float radius = MetaProgress.BlastRadius() * Mathf.Sqrt(grown);
        Combat.Burst(point, radius, Combat.BatBase() * Tuning.MetalBlastDamage, DamageKind.Blast, skip);
        UpgradeLook.Blast(point + Vector3.up * 0.4f, radius);
        MetalBlasts++;
        if (MetalBlasts <= 2) {
            Say("aluminum bat: the finisher blasts " + radius.ToString("0.0") + " m around the bot");
        }
    }

    // Big birds barely budge; birds your size or smaller fly.
    static float Heft(Enemy enemy) {
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        return 1f / Mathf.Sqrt(Mathf.Max(1f, enemy.BodyScale / Mathf.Max(0.5f, grown)));
    }

    static Vector3 KnockOf(Enemy enemy, Vector3 direction, float speed, float lift) {
        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : enemy.transform.forward;
        float heft = Heft(enemy);
        return dir * speed * heft + Vector3.up * lift * heft;
    }

    // Every live bird within reach of you and inside the arc either side of forward.
    void Gather(float reach, float halfArc, Vector3 forward, List<Enemy> into) {
        into.Clear();
        Vector3 origin = transform.position;
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 to = enemy.transform.position - origin;
            if (Mathf.Abs(to.y) > reach + enemy.BodyScale) {
                continue;
            }
            to.y = 0f;
            if (to.magnitude - enemy.BodyScale * 0.3f > reach) {
                continue;
            }
            if (to.sqrMagnitude > 0.04f && Vector3.Angle(forward, to) > halfArc) {
                continue;
            }
            into.Add(enemy);
        }
        sortOrigin = origin;
        if (byDistance == null) {
            byDistance = (a, b) => (a.transform.position - sortOrigin).sqrMagnitude.CompareTo((b.transform.position - sortOrigin).sqrMagnitude);
        }
        into.Sort(byDistance);
    }

    Enemy Nearest(float reach, float halfArc) {
        Gather(reach, halfArc, Flat(transform.forward), buffer);
        return buffer.Count > 0 ? buffer[0] : null;
    }

    void LateUpdate() {
        // Without a lock, turn to the bird the swing is for while the bat comes round.
        if (move != Move.None && !contactDone && faceTarget != null && faceTarget.Alive) {
            Vector3 to = Flat(faceTarget.transform.position - transform.position);
            if (to.sqrMagnitude > 0.01f) {
                Quaternion look = Quaternion.LookRotation(to.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-30f * Time.deltaTime));
            }
        }
        PoseBat();
    }

    void BuildBat() {
        if (anim != null) {
            hand = FindBone(anim.transform, "RightHand");
        }
        if (hand == null) {
            Debug.LogWarning("Right hand bone was not found, so the bat is held at the chest.");
        }
        Transform parent = anim != null ? anim.transform : transform;
        root = new GameObject("EquippedBat").transform;
        root.SetParent(parent, false);
        Part("Handle", new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.25f, 0.06f), new Color(0.12f, 0.09f, 0.08f));
        Part("Barrel", new Vector3(0f, 0.66f, 0f), new Vector3(0.11f, 0.36f, 0.11f), new Color(0.5f, 0.36f, 0.22f));
        BindModelBat();
        UpgradeLook.DressBat(root, transform);
        PoseBat();
    }

    void Part(string name, Vector3 at, Vector3 size, Color color) {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = at;
        part.transform.localScale = size;
        UpgradeLook.Paint(part, color);
    }

    // Where the bat points, as (around from forward toward your right, up from level) in degrees, through the
    // swing: wind up, a fast sweep, back over the shoulder.
    void PoseBat() {
        if (root == null) {
            return;
        }
        if (modelBat != null && modelBat.gameObject.activeInHierarchy) {
            Vector3 worldTip = modelBat.TransformVector(modelBatTip);
            if (worldTip.sqrMagnitude > 0.0001f) {
                root.position = modelBat.position;
                root.rotation = Quaternion.FromToRotation(Vector3.up, worldTip.normalized);
                Vector3 parentScale = (root.parent != null ? root.parent : transform).lossyScale;
                float unitScale = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
                root.localScale = Vector3.one * (worldTip.magnitude / unitScale);
                return;
            }
        }
        float dt = Time.deltaTime;
        bool swinging = move != Move.None;
        if (charging) {
            float wobble = chargeFull ? Mathf.Sin(Time.time * 38f) * 3f : 0f;
            pose = Vector2.Lerp(pose, new Vector2(155f, 20f + wobble), 1f - Mathf.Exp(-14f * dt));
        } else if (swinging) {
            float sweepEnd = windup + sweep;
            if (moveTime < windup) {
                pose = Vector2.Lerp(poseFrom, windPose, Smooth(moveTime / Mathf.Max(0.001f, windup)));
            } else if (moveTime < sweepEnd) {
                float k = (moveTime - windup) / sweep;
                pose = Vector2.Lerp(sweepFrom, sweepTo, 1f - (1f - k) * (1f - k));
            } else {
                pose = Vector2.Lerp(sweepTo, Rest, Smooth((moveTime - sweepEnd) / Mathf.Max(0.001f, moveLength - sweepEnd)));
            }
        } else {
            pose = Vector2.Lerp(pose, Rest, 1f - Mathf.Exp(-10f * dt));
        }
        gripBlend = Mathf.MoveTowards(gripBlend, swinging || charging ? 1f : 0f, dt * 8f);

        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        float az = pose.x * Mathf.Deg2Rad;
        float el = pose.y * Mathf.Deg2Rad;
        Vector3 local = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        Vector3 dir = transform.TransformDirection(local);

        Vector3 chest = transform.position + Vector3.up * 1.2f * grown;
        Vector3 held = hand != null ? hand.position : chest + transform.right * 0.3f * grown;
        Vector3 flatDir = Flat(dir);
        Vector3 swingGrip = chest + (flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : transform.forward) * 0.35f * grown + Vector3.up * dir.y * 0.25f * grown;
        root.position = hand != null ? held : Vector3.Lerp(held, swingGrip, gripBlend);
        root.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        Vector3 parent = (root.parent != null ? root.parent : transform).lossyScale;
        float unit = Mathf.Max(0.0001f, Mathf.Abs(parent.x));
        root.localScale = Vector3.one * grown / unit;
    }

    // A small bar under the middle of the screen while you wind up a home run.
    void OnGUI() {
        if (!charging || LevelUpScreen.IsOpen || PauseMenu.IsOpen || WaveDirector.IsLost) {
            return;
        }
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float w = 150f * unit;
        float h = 8f * unit;
        float x = Screen.width * 0.5f - w * 0.5f;
        float y = Screen.height * 0.62f;
        Color previous = GUI.color;
        GUI.color = new Color(0.08f, 0.06f, 0.1f, 0.75f);
        GUI.DrawTexture(new Rect(x - unit, y - unit, w + 2f * unit, h + 2f * unit), Texture2D.whiteTexture);
        bool ready = charge >= Tuning.HomeRunMin;
        GUI.color = chargeFull ? new Color(1f, 0.85f, 0.35f, 1f) : ready ? new Color(1f, 0.7f, 0.3f, 0.95f) : new Color(0.85f, 0.8f, 0.7f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w * charge, h), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    static void Say(string line) {
        Events.Add(line);
        if (Events.Count > 32) {
            Events.RemoveAt(0);
        }
    }

    static string Size(Enemy enemy) {
        if (enemy == null) {
            return "none, most in the arc small";
        }
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        return (enemy.Title ?? enemy.name) + " size " + enemy.BodyScale.ToString("0.00") + " vs your " + grown.ToString("0.00");
    }

    static float Smooth(float t) {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    static Transform FindBone(Transform root, string suffix) {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            if (t.name.EndsWith(suffix)) {
                return t;
            }
        }
        return null;
    }
}

// Long Ball's bolt: a hex bolt batted off the barrel at a bot out of reach. It flies straight at the bot (a little
// homing, so a walking bot is still met) and hits it as the bat would, so the run's boons ride along.
public class BattedBolt : MonoBehaviour {
    Enemy target;
    float scale;
    float speed;
    float life = 1.6f;
    Transform spin;

    public static void Launch(Vector3 from, Enemy target, float scale, float grown) {
        var go = new GameObject("BattedBolt");
        go.transform.position = from;
        BattedBolt bolt = go.AddComponent<BattedBolt>();
        bolt.target = target;
        bolt.scale = scale;
        bolt.speed = Tuning.LongBallSpeed * Mathf.Sqrt(Mathf.Max(1f, grown));
        bolt.spin = UpgradeLook.BuildBolt(go.transform, 0.9f * Mathf.Sqrt(Mathf.Max(1f, grown)));
    }

    void Update() {
        life -= Time.deltaTime;
        if (target == null || !target.Alive || life <= 0f) {
            Destroy(gameObject);
            return;
        }
        Vector3 aim = target.transform.position + Vector3.up * 0.6f * Mathf.Max(1f, target.BodyScale);
        Vector3 to = aim - transform.position;
        float step = speed * Time.deltaTime;
        if (spin != null) {
            spin.Rotate(720f * Time.deltaTime, 540f * Time.deltaTime, 0f, Space.Self);
        }
        if (to.magnitude <= step + 0.3f + target.BodyScale * 0.3f) {
            Combat.BatHit(target, scale, 2f, false);
            BatEquipment.NoteBoltHit();
            GameAudio.Play("batcrack", 0.35f, 1.6f);
            Destroy(gameObject);
            return;
        }
        transform.position += to.normalized * step;
    }
}
