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
        if (Instance == this) {
            Instance = null;
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
    }

    void BuildLabel() {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        label = go.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        Refresh();
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
