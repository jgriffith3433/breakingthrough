using UnityEngine;

// An AI companion that walks with Wallit in the chapters whose data names one (Story.Chapter.Companion). Slice is the
// first. She follows a step behind him, stabs the bots that come near, and goes down when her own health runs out: a
// downed companion lies where she fell until Wallit stands over her and holds E. Arcade has no ChapterRun, so nothing
// here ever exists there. While she walks, the circle's own Slice is away: her ring stays staked (it still has the
// health bar and still ends the run), but the figure on it is the companion.
public class Companion : MonoBehaviour {
    public struct Def {
        public string Id;
        public string Name;
        public int Health;
        public float Speed;       // walking pace; she closes faster when she is far behind
        public float Reach;       // a stab lands inside this, metres
        public float Cooldown;    // between stabs, seconds
        public float Damage;      // multiple of Combat.ArrowBase()
        public float Leash;       // she will not chase a bot further than this from Wallit
        public string DownLine;   // "<speaker>|<text>", said when she goes down
        public string UpLine;     // said when she is back on her feet
    }

    // One row per companion. A new one is a row here and a chapter's Companion field.
    public static readonly Def[] Defs = {
        new Def {
            Id = "slice", Name = "Slice", Health = 60, Speed = 6.2f, Reach = 1.7f, Cooldown = 1.1f, Damage = 0.6f, Leash = 14f,
            DownLine = "W|Slice! Hold on!", UpLine = "S|Noted.",
        },
    };

    public const float ReviveSeconds = 2f;
    public const float ReviveReach = 2.8f;
    public const float DownedHealth = 0.5f;   // share of her health she gets back

    public static Companion Instance { get; private set; }
    // CinematicDirector holds her on marks during a chapter opening: AI off, stride still ticks from HeldMove.
    public bool Held;

    public Def Data { get; private set; }
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public bool Downed { get { return Health <= 0; } }
    // Downed and Wallit is within reach: the touch controls show their Revive pad, and E, or the pad's X, held revives her.
    public bool ReviveReady {
        get { return Downed && Player != null && Flat(Player.position - transform.position).magnitude <= ReviveReach + 1.5f; }
    }
    public bool Alive { get { return Health > 0; } }
    public float ReviveProgress { get; private set; }
    public int Stabs { get; private set; }
    public int Downs { get; private set; }
    public int Revives { get; private set; }
    public float Walked { get; private set; }
    public float DistanceToPlayer { get { return Player != null ? Flat(Player.position - transform.position).magnitude : 0f; } }
    public Transform Player { get; private set; }
    public bool HasModel { get { return model != null; } }

    Transform model;
    Animator animator;
    bool hasClips;
    int clip = -1;
    TextMesh label;
    Renderer[] bodyRenderers;
    float stabIn;
    float strikeLeft;
    float strikeHitIn;
    bool strikeHitDone;
    Enemy strikeTarget;
    bool nextIsSlash;
    float hurtIn;
    float hurtLeft;
    float flash;
    float downTilt;
    Vector3 lastPos;

    // Her walk. Slice.fbx has no walk or run take (piano, standUp, stab, slash, sitDown, idle, hurt, defeat), so she
    // used to glide on idle. Her rig is a Mixamo humanoid with its own avatar, so humanoid clips already in the project
    // retarget onto her: Resources/Slice/SliceStride.controller lists Walking.fbx (a Mixamo walk, in place) and
    // ANIM_HERO_JOG.fbx (an in-place jog). They are laid over her controller full body (StrideLayer), crossfaded by
    // speed between WalkTop and JogFrom, each at her ground speed over the ground it covers, so her feet stay planted.
    // A clip that did not import as a humanoid motion is skipped: then Wallit's own run stands in for the jog, and with
    // no usable clip at all she gets a procedural stride (legs swing and a step bob) rather than a silent glide.
    public const float WalkTop = 1.9f;
    public const float JogFrom = 2.9f;
    // Planted-foot speed of each source clip at playback 1, in hip heights a second (measured off the FBX curves:
    // Walking 1.26 m/s at 0.80 m hips, ANIM_HERO_JOG 3.78 m/s at 0.97 m hips; Wallit's run 4.4 m/s, about 0.95 m hips).
    const float WalkHips = 1.57f;
    const float JogHips = 3.9f;
    const float WallitRunHips = 4.6f;
    StrideLayer stride;
    int walkInput = -1;
    int jogInput = -1;
    float[] strideSpeed = new float[2];
    float[] strideGuess = new float[2];
    float hipHeight = 0.9f;
    float groundSpeed;
    float groundSpeedVel;
    bool walking;
    float walkStop;
    public float StrideFade = 0.15f;
    public string WalkSource { get; private set; } = "none";
    public float GroundSpeed { get { return groundSpeed; } }
    public float StrideWeight { get { return stride != null ? stride.Weight : 0f; } }
    // No clip could be used: a hand-made stride instead.
    bool procedural;
    float stepPhase;
    Transform hips, upperLegL, upperLegR, lowerLegL, lowerLegR, footL, footR;
    Vector3 footLLocal, footRLocal;
    float footFloor = 999f;
    bool haveFeet;

    // Called by ChapterRun for a chapter whose data names a companion. Unknown ids do nothing.
    public static void Spawn(string id) {
        if (Instance != null || string.IsNullOrEmpty(id)) {
            return;
        }
        for (int i = 0; i < Defs.Length; i++) {
            if (Defs[i].Id == id) {
                GameObject go = new GameObject("Companion_" + id);
                Companion c = go.AddComponent<Companion>();
                c.Begin(Defs[i]);
                return;
            }
        }
    }

    public static void Despawn() {
        if (Instance != null) {
            Destroy(Instance.gameObject);
        }
    }

    // Playtest setup shortcut: put her down as a bot's blow would.
    public void KnockDown() {
        if (Alive) {
            Hurt(Health);
        }
    }

    // Wallit is back at a checkpoint after a defeat: she gets up where he stands, at full health, no longer downed (her tag and the
    // revive state go with her), instead of lying where the fight was, out of sight.
    public void Recover(Vector3 near) {
        Health = MaxHealth;
        ReviveProgress = 0f;
        downTilt = 0f;
        strikeLeft = 0f;
        strikeTarget = null;
        hurtLeft = 0f;
        hurtIn = 1f;
        stabIn = 0.5f;
        Player = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform : Player;
        Vector3 back = Player != null ? -Player.forward * 2.2f + Player.right * 1.4f : Vector3.zero;
        transform.position = Grounded(near + back);
        lastPos = transform.position;
        PlayClip(Musician.CLIP_IDLE, true);
        Refresh();
    }

    void Begin(Def def) {
        Data = def;
        MaxHealth = def.Health;
        Health = def.Health;
        Instance = this;
        PlayerGrowth pg = PlayerGrowth.Instance;
        Player = pg != null ? pg.transform : null;
        Vector3 at = Player != null ? Player.position - Player.forward * 2.2f + Player.right * 1.4f : Vector3.zero;
        transform.position = Grounded(at);
        lastPos = transform.position;
        BuildModel();
        BuildLabel();
        if (Musician.Instance != null) {
            Musician.Instance.Away = true;
        }
        Refresh();
    }

    void OnDestroy() {
        if (stride != null) {
            stride.Dispose();
            stride = null;
        }
        if (Instance == this) {
            Instance = null;
            Held = false;
            if (Musician.Instance != null) {
                Musician.Instance.Away = false;
            }
        }
    }

    void BuildModel() {
        GameObject prefab = Resources.Load<GameObject>("Slice/Slice");
        GameObject body;
        if (prefab != null) {
            body = Instantiate(prefab, transform);
            body.name = "Slice";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;
            Musician.ApplySliceSkins(body);
            animator = body.GetComponent<Animator>();
            if (animator == null) {
                animator = body.GetComponentInChildren<Animator>();
            }
            if (animator != null) {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                for (int i = 0; i < animator.parameterCount; i++) {
                    if (animator.GetParameter(i).name == "Clip") {
                        hasClips = true;
                    }
                }
            }
        } else {
            // No art in this build: a plain dark stand-in so she can still be seen and followed.
            body = new GameObject("Slice");
            body.transform.SetParent(transform, false);
            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.transform.SetParent(body.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1f, 0f);
            torso.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            Destroy(torso.GetComponent<Collider>());
            Renderer r = torso.GetComponent<Renderer>();
            r.material = new Material(SafeShader.Solid()) { color = new Color(0.22f, 0.14f, 0.3f) };
        }
        model = body.transform;
        bodyRenderers = body.GetComponentsInChildren<Renderer>();
        PlayClip(Musician.CLIP_IDLE, true);
        BuildStride();
    }

    void BuildStride() {
        if (animator != null && animator.isHuman) {
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            upperLegL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            upperLegR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lowerLegL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lowerLegR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            // Her bind pose stands, so the hips' height over her feet scales the clips' strides to her legs.
            if (hips != null) {
                float h = hips.position.y - transform.position.y;
                if (h > 0.4f && h < 2.5f) {
                    hipHeight = h;
                }
            }
            AnimationClip walk = null;
            AnimationClip jog = null;
            RuntimeAnimatorController pack = Resources.Load<RuntimeAnimatorController>("Slice/SliceStride");
            if (pack != null) {
                foreach (AnimationClip c in pack.animationClips) {
                    if (!StrideLayer.Usable(c)) {
                        continue;
                    }
                    // Walking.fbx's take is "mixamo.com" (1.4 s); ANIM_HERO_JOG's is "Unreal Matinee Take" (0.77 s).
                    bool isWalk = c.name.IndexOf("mixamo", System.StringComparison.OrdinalIgnoreCase) >= 0 || c.length > 1.1f;
                    if (isWalk && walk == null) {
                        walk = c;
                    } else if (!isWalk && jog == null) {
                        jog = c;
                    }
                }
            }
            float jogHips = JogHips;
            string jogName = "ANIM_HERO_JOG";
            if (jog == null) {
                jog = WallitRun();
                jogHips = WallitRunHips;
                jogName = "Wallit run";
            }
            var clips = new System.Collections.Generic.List<AnimationClip>();
            if (walk != null) {
                walkInput = clips.Count;
                strideGuess[clips.Count] = WalkHips * hipHeight;
                clips.Add(walk);
            }
            if (jog != null) {
                jogInput = clips.Count;
                strideGuess[clips.Count] = jogHips * hipHeight;
                clips.Add(jog);
            }
            for (int i = 0; i < clips.Count; i++) {
                strideSpeed[i] = strideGuess[i];
            }
            if (clips.Count > 0) {
                stride = StrideLayer.Create(animator, clips.ToArray(), null, "SliceStride");
            }
            if (stride != null) {
                WalkSource = (walk != null ? "Walking" : "") + (walk != null && jog != null ? " + " : "") + (jog != null ? jogName : "");
                return;
            }
            walkInput = -1;
            jogInput = -1;
        }
        procedural = model != null;
        WalkSource = procedural ? "procedural" : "none";
    }

    // Wallit's run off his own controller (a Mixamo humanoid take from Wallit.fbx), for when the jog did not import.
    static AnimationClip WallitRun() {
        GameObject w = Resources.Load<GameObject>("Wallit/Wallit");
        Animator a = w != null ? w.GetComponentInChildren<Animator>(true) : null;
        if (a == null || a.runtimeAnimatorController == null) {
            return null;
        }
        foreach (AnimationClip c in a.runtimeAnimatorController.animationClips) {
            if (c != null && (c.name == "run" || c.name.EndsWith("|run") || c.name.EndsWith("_run")) && StrideLayer.Usable(c)) {
                return c;
            }
        }
        return null;
    }

    void BuildLabel() {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        label = go.AddComponent<TextMesh>();
        label.font = UiSkin.Font(UiSkin.FontRole.Label);
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.09f;
        label.fontSize = 48;
        label.color = new Color(0.95f, 0.9f, 1f);
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    static Vector3 Grounded(Vector3 p) {
        if (Physics.Raycast(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, out RaycastHit hit, 40f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            p.y = hit.point.y;
        }
        return p;
    }

    bool Frozen {
        get { return Time.deltaTime <= 0f || PauseMenu.IsOpen || LevelUpScreen.IsOpen || StoryScreen.IsOpen || Cutscene.IsOpen || WaveDirector.IsLost; }
    }

    void Update() {
        if (Player == null) {
            PlayerGrowth pg = PlayerGrowth.Instance;
            Player = pg != null ? pg.transform : null;
            return;
        }
        if (Held) {
            float dtHeld = Mathf.Max(0.0001f, Time.deltaTime);
            TickStride(dtHeld);
            Refresh();
            return;
        }
        if (Frozen) {
            return;
        }
        float dt = Time.deltaTime;
        if (flash > 0f) {
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
        }
        if (Downed) {
            TickDowned(dt);
        } else {
            TickAlive(dt);
        }
        float moved = Flat(transform.position - lastPos).magnitude;
        Walked += moved;
        lastPos = transform.position;
        // A catch-up jump to Wallit is a teleport, not a stride.
        float speedNow = moved > 3f ? 0f : moved / dt;
        groundSpeed = Mathf.SmoothDamp(groundSpeed, speedNow, ref groundSpeedVel, 0.08f);
        TickStride(dt);
        Refresh();
    }

    // CinematicDirector places her each frame; stride rate follows the real distance covered (no fixed fake speed).
    public void HeldMove(Vector3 pos, bool walk) {
        Vector3 delta = pos - transform.position;
        delta.y = 0f;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        transform.position = pos;
        lastPos = pos;
        walking = walk && delta.sqrMagnitude > 0.0004f;
        groundSpeed = walking ? Mathf.Clamp(delta.magnitude / dt, 0f, Data.Speed * 1.1f) : 0f;
        if (walking && delta.sqrMagnitude > 0.01f) {
            transform.rotation = Quaternion.LookRotation(delta.normalized);
        }
        if (!walk) {
            walkStop = 0.2f;
        }
    }

    // Once a frame, before her Animator runs: walk while she covers ground, idle (the controller) when she stops,
    // and let a stab, a slash or her fall own the body.
    void TickStride(float dt) {
        if (walking) {
            walkStop = groundSpeed < 0.25f ? walkStop + dt : 0f;
            if (walkStop >= 0.1f) {
                walking = false;
            }
        } else if (groundSpeed > 0.35f) {
            walking = true;
            walkStop = 0f;
        }
        float want = 0f;
        if (Alive && walking && strikeLeft <= 0f) {
            want = clip == Musician.CLIP_HURT && hurtLeft > 0f ? 0.5f : 1f;
        }
        if (procedural) {
            float step = Mathf.Max(0.3f, hipHeight * 0.85f);
            stepPhase = want > 0f ? Mathf.Repeat(stepPhase + dt * groundSpeed / (2f * step), 1f) : 0f;
            return;
        }
        if (stride == null) {
            return;
        }
        if (want > 0f && stride.Weight <= 0f) {
            stride.StartAt(0f);
        }
        if (walkInput >= 0 && jogInput >= 0) {
            float t = Mathf.InverseLerp(WalkTop, JogFrom, groundSpeed);
            stride.Mix(walkInput, jogInput, t, stride.Weight <= 0f);
        } else {
            stride.Lead(0, stride.Weight <= 0f);
        }
        for (int i = 0; i < stride.Count && i < strideSpeed.Length; i++) {
            stride.SetRate(i, Mathf.Clamp(groundSpeed / Mathf.Max(0.3f, strideSpeed[i]), 0.3f, 3f));
        }
        stride.Tick(want, StrideFade, dt);
    }

    // After her Animator: learn each clip's real stride from the planted foot (as Wallit's legs do), and, with no
    // clip at all, swing her legs and bob her step by hand.
    void LateUpdate() {
        if (model == null || Time.deltaTime <= 0f) {
            return;
        }
        if (procedural) {
            TickProceduralStride();
            return;
        }
        LearnStride(Time.deltaTime);
    }

    void LearnStride(float dt) {
        if (stride == null || footL == null || footR == null) {
            return;
        }
        Vector3 wl = footL.position;
        Vector3 wr = footR.position;
        Vector3 ll = model.InverseTransformPoint(wl);
        Vector3 lr = model.InverseTransformPoint(wr);
        footFloor = Mathf.Min(footFloor + 0.05f * dt, Mathf.Min(wl.y, wr.y));
        if (haveFeet && stride.Weight > 0.99f && groundSpeed > 0.5f) {
            // Only while one clip has the legs alone, so the measure belongs to it.
            int lead = walkInput >= 0 && jogInput >= 0 ? (groundSpeed <= WalkTop ? walkInput : groundSpeed >= JogFrom ? jogInput : -1) : 0;
            bool leftLower = wl.y <= wr.y;
            Vector3 now = leftLower ? ll : lr;
            Vector3 before = leftLower ? footLLocal : footRLocal;
            float height = (leftLower ? wl.y : wr.y) - footFloor;
            if (lead >= 0 && height < 0.05f * hipHeight) {
                // The planted foot's speed against her body is the ground the clip covers at this playback speed.
                Vector3 body = (now - before) / dt;
                body.y = 0f;
                float rate = Mathf.Clamp(groundSpeed / Mathf.Max(0.3f, strideSpeed[lead]), 0.3f, 3f);
                float implied = body.magnitude * model.lossyScale.y / Mathf.Max(0.3f, rate);
                float guess = strideGuess[lead];
                if (implied > guess * 0.5f && implied < guess * 2f) {
                    strideSpeed[lead] = Mathf.Lerp(strideSpeed[lead], implied, 0.03f);
                }
            }
        }
        footLLocal = ll;
        footRLocal = lr;
        haveFeet = true;
    }

    // The honest fallback: thighs swing opposite each other, the trailing knee folds, and the body dips at each
    // footfall, paced so a step covers about 0.85 hip heights. Feet still slip a little; it is not a glide.
    void TickProceduralStride() {
        if (stepPhase <= 0f && groundSpeed < 0.35f) {
            return;
        }
        float a = stepPhase * Mathf.PI * 2f;
        float swing = Mathf.Sin(a);
        float amount = Mathf.Clamp01(groundSpeed / 1.5f);
        model.localPosition += Vector3.down * (Mathf.Abs(Mathf.Cos(a)) * 0.04f * hipHeight * amount);
        Vector3 side = transform.right;
        if (upperLegL != null && upperLegR != null) {
            upperLegL.rotation = Quaternion.AngleAxis(-28f * swing * amount, side) * upperLegL.rotation;
            upperLegR.rotation = Quaternion.AngleAxis(28f * swing * amount, side) * upperLegR.rotation;
        }
        if (lowerLegL != null && lowerLegR != null) {
            lowerLegL.rotation = Quaternion.AngleAxis(40f * Mathf.Max(0f, -swing) * amount, side) * lowerLegL.rotation;
            lowerLegR.rotation = Quaternion.AngleAxis(40f * Mathf.Max(0f, swing) * amount, side) * lowerLegR.rotation;
        }
    }

    void TickAlive(float dt) {
        downTilt = Mathf.MoveTowards(downTilt, 0f, dt * 220f);
        TickHurt(dt);
        Enemy foe = strikeLeft > 0f ? strikeTarget : Nearest();
        Vector3 goal = Follow();
        float stop = 0.6f;
        if (foe != null && foe.Alive) {
            goal = foe.transform.position;
            stop = Data.Reach * 0.7f;
        }
        Vector3 to = Flat(goal - transform.position);
        float far = Flat(Player.position - transform.position).magnitude;
        if (far > 45f) {
            // Left far behind (a long stride, a respawn): she catches up rather than getting lost.
            transform.position = Grounded(Player.position - Player.forward * 2f);
            to = Vector3.zero;
        }
        bool striking = strikeLeft > 0f;
        if (!striking && to.magnitude > stop) {
            float pace = Data.Speed * (far > 8f ? 1.9f : far > 4f ? 1.3f : 1f);
            Vector3 step = to.normalized * Mathf.Min(pace * dt, to.magnitude - stop * 0.5f);
            transform.position = Grounded(transform.position + step);
            Face(to, dt);
            if (clip != Musician.CLIP_HURT || hurtLeft <= 0f) {
                PlayClip(Musician.CLIP_IDLE);
            }
        } else if (to.sqrMagnitude > 0.01f && foe != null) {
            Face(to, dt);
        }
        stabIn -= dt;
        if (striking) {
            strikeLeft -= dt;
            strikeHitIn -= dt;
            if (!strikeHitDone && strikeHitIn <= 0f) {
                StrikeHit();
            }
            if (strikeLeft <= 0f) {
                strikeTarget = null;
            }
        } else if (foe != null && stabIn <= 0f && Flat(foe.transform.position - transform.position).magnitude - foe.BodyScale * 0.22f <= Data.Reach) {
            BeginStrike(foe);
        }
        // Stab, slash and hurt do not loop: once one is over and she is standing, go back to idle rather than hold the
        // last frame of the swing.
        if (strikeLeft <= 0f && to.magnitude <= stop && clip != Musician.CLIP_IDLE && (clip != Musician.CLIP_HURT || hurtLeft <= 0f)) {
            PlayClip(Musician.CLIP_IDLE);
        }
        if (hurtLeft > 0f) {
            hurtLeft -= dt;
        }
    }

    // A step behind Wallit and to his right, so she is in the picture without standing in the swing.
    Vector3 Follow() {
        Vector3 back = -Player.forward;
        back.y = 0f;
        if (back.sqrMagnitude < 0.01f) {
            back = Vector3.back;
        }
        return Player.position + back.normalized * 2.4f + Player.right * 1.4f;
    }

    void Face(Vector3 dir, float dt) {
        if (dir.sqrMagnitude < 0.0001f) {
            return;
        }
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, Mathf.Min(1f, dt * 9f));
    }

    // The nearest bot she will go for: near her, and not so far from Wallit that she leaves him.
    Enemy Nearest() {
        Enemy best = null;
        float bestGap = 9f;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy e = Enemy.Roster[i];
            if (e == null || !e.Alive || e.Climbing) {
                continue;
            }
            Vector3 p = e.transform.position;
            if (Flat(p - Player.position).magnitude > Data.Leash) {
                continue;
            }
            float gap = Flat(p - transform.position).magnitude;
            if (gap < bestGap) {
                bestGap = gap;
                best = e;
            }
        }
        return best;
    }

    void BeginStrike(Enemy foe) {
        bool slash = nextIsSlash;
        nextIsSlash = !nextIsSlash;
        strikeTarget = foe;
        strikeHitDone = false;
        strikeHitIn = slash ? 0.36f : 0.34f;
        strikeLeft = slash ? 0.78f : 0.9f;
        stabIn = Data.Cooldown + strikeHitIn;
        PlayClip(slash ? Musician.CLIP_SLASH : Musician.CLIP_STAB, true);
        GameAudio.Play("batswing", 0.35f, slash ? 1.55f : 1.7f);
    }

    // The same blade as the circle's Slice: small bots take a real stab, big ones a flinch-cut and a shove.
    void StrikeHit() {
        strikeHitDone = true;
        Enemy foe = strikeTarget;
        if (foe == null || !foe.Alive) {
            return;
        }
        if (Flat(foe.transform.position - transform.position).magnitude - foe.BodyScale * 0.22f > Data.Reach + 0.8f) {
            return;
        }
        bool big = foe.Boss || foe.IsHeavy || BotRoster.IsSpider(foe.Kind);
        float amount = Mathf.Max(1f, Combat.ArrowBase() * Data.Damage);
        if (big) {
            amount *= 0.25f;
        }
        Vector3 dir = Flat(foe.transform.position - transform.position).normalized;
        foe.Damage(amount, DamageKind.Blade, false);
        if (big) {
            foe.Knock(dir * 3.5f);
        } else {
            GameAudio.Play("hit", 0.35f, 1.35f);
        }
        Stabs++;
    }

    // Bots at her side hurt her the way they hurt Wallit: a claw or a stab every so often.
    void TickHurt(float dt) {
        hurtIn -= dt;
        if (hurtIn > 0f) {
            return;
        }
        int dealt = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy e = Enemy.Roster[i];
            if (e == null || !e.Alive || e.IsCourier) {
                continue;
            }
            float gap = Flat(e.transform.position - transform.position).magnitude - e.BodyScale * 0.22f;
            if (gap <= 1.15f) {
                dealt += e.Boss ? 3 : e.Elite ? 2 : 1;
            }
        }
        if (dealt > 0) {
            hurtIn = 1.05f;
            Hurt(dealt);
        } else {
            hurtIn = 0.2f;
        }
    }

    void Hurt(int amount) {
        if (!Alive || amount <= 0) {
            return;
        }
        Health = Mathf.Max(0, Health - amount);
        flash = 1f;
        GameAudio.Play("hit", 0.4f);
        if (Alive) {
            hurtLeft = 0.58f;
            if (strikeLeft <= 0f) {
                PlayClip(Musician.CLIP_HURT, true);
            }
        } else {
            Down();
        }
    }

    void Down() {
        Downs++;
        strikeLeft = 0f;
        strikeTarget = null;
        ReviveProgress = 0f;
        PlayClip(Musician.CLIP_DEFEAT, true);
        ChapterRun.Say(Data.DownLine, 3f);
    }

    void TickDowned(float dt) {
        downTilt = Mathf.MoveTowards(downTilt, 82f, dt * 220f);
        bool near = Flat(Player.position - transform.position).magnitude <= ReviveReach;
        if (near && (PlayInput.GetKey(KeyCode.E) || PlayInput.GetKey(KeyCode.JoystickButton2))) {
            ReviveProgress += dt / ReviveSeconds;
            if (ReviveProgress >= 1f) {
                Revive();
            }
        } else {
            ReviveProgress = Mathf.MoveTowards(ReviveProgress, 0f, dt * 0.5f);
        }
    }

    void Revive() {
        Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * DownedHealth));
        ReviveProgress = 0f;
        Revives++;
        stabIn = 0.5f;
        hurtIn = 1f;
        PlayClip(Musician.CLIP_IDLE, true);
        Fx.Ring(transform.position + Vector3.up * 0.2f, 5f, new Color(1f, 0.88f, 0.5f, 0.7f), 0.9f);
        GameAudio.Play("hit", 0.3f, 0.8f);
        ChapterRun.Say(Data.UpLine, 2.6f);
    }

    void PlayClip(int id, bool restart = false) {
        bool changed = clip != id;
        clip = id;
        if (animator != null && hasClips && (changed || restart)) {
            animator.SetInteger("Clip", id);
            animator.CrossFadeInFixedTime(ClipName(id), 0.08f, 0, 0f);
        }
    }

    static string ClipName(int id) {
        switch (id) {
            case Musician.CLIP_STAB: return "stab";
            case Musician.CLIP_SLASH: return "slash";
            case Musician.CLIP_HURT: return "hurt";
            case Musician.CLIP_DEFEAT: return "defeat";
            default: return "idle";
        }
    }

    void Refresh() {
        if (model != null) {
            // With no defeat clip (or the stand-in) the body tips over by hand.
            model.localRotation = Quaternion.Euler(downTilt, 0f, 0f);
            model.localPosition = new Vector3(0f, downTilt > 0f ? 0.15f : 0f, 0f);
        }
        if (label == null) {
            return;
        }
        Camera cam = Camera.main;
        if (cam != null) {
            label.transform.rotation = cam.transform.rotation;
        }
        if (Downed) {
            int pips = Mathf.RoundToInt(ReviveProgress * 10f);
            label.text = Data.Name + " is down\n" + (UiFit.Touch ? "hold Revive" : "hold E") + " to revive\n[" + new string('#', pips) + new string('-', 10 - pips) + "]";
            label.color = new Color(1f, 0.55f, 0.45f);
        } else {
            int pips = Mathf.Clamp(Mathf.CeilToInt(10f * Health / MaxHealth), 1, 10);
            label.text = Data.Name + "\n[" + new string('#', pips) + new string('-', 10 - pips) + "]";
            label.color = Color.Lerp(new Color(0.95f, 0.9f, 1f), new Color(1f, 0.4f, 0.4f), flash);
        }
    }
}
