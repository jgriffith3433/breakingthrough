using System.Collections.Generic;
using UnityEngine;

// Elite traits for Yo Bots. Keep the order.
public enum BotTrait
{
    None,
    Sprinter,
    Hopper,
    Bruiser,
    Juker,
    JumpJets,
    Splitter,
    Blinker,
    Heavy,
    Charger,
    Swarmer,
    Juggernaut
}

// A bot that walks at the musician, hops on its jump jets, and swipes (Yo Bots) or stabs (Spider Bots)
// when it gets there. From the climb wave, old kinds go for the player instead.
public class Enemy : MonoBehaviour
{
    public int HitsToDrop = 1;
    public BotKind Kind { get; private set; }
    public int DebutWave { get; private set; }
    public bool IsHeavy { get; private set; }
    public bool Elite { get; private set; }
    public bool Golden { get; private set; }
    public bool Boss { get; private set; }
    public bool IsSpiderFamily { get { return spider; } }
    // The Courier (Chapter 1's Alpha): it holds still and attacks in telegraphed slams (Courier.cs), never swipes.
    public bool IsCourier { get; private set; }
    public int BlowsTaken;      // blows that landed (the Server counts a refusal when this moves)
    public float PhaseAt;       // a chapter boss's phase threshold (share of health left); 0 none. The boss bar draws a notch there
    public int Phase = 1;       // 1, then 2 once Health01 falls to PhaseAt (BossPhases.EnterPhase2, Courier)
    public float MinHealth01;   // the Server's floor: blows cannot take its health below this share until it is lifted
    public string Title { get; private set; }
    public float Health01 { get { return Mathf.Clamp01(1f - taken / Mathf.Max(1f, HitsToDrop)); } }
    public float BodyScale { get { return bodyScale; } }
    public bool Burning { get { return burnLeft > 0f; } }
    public bool Slowed { get { return slowLeft > 0f || frozenLeft > 0f; } }
    public bool Shorted { get { return shortLeft > 0f; } }

    // Every bot on the hill, so effects do not have to search the scene.
    public static readonly List<Enemy> Roster = new List<Enemy>();

    [Header("Movement")]
    public float walkSpeed = 1.15f;
    public float chaseSpeed = 2.4f;
    public float chaseRange = 22f;
    public float stopDistance = 1.15f;

    [Header("Jump jets")]
    public float jumpVelocity = 4.2f;
    public float flightGravity = 7f;
    public float normalGravity = 26f;
    public float maxFlightHeight = 1.8f;
    public float flightDuration = 1.5f;
    public float minFlightInterval = 1.5f;
    public float maxFlightInterval = 3.5f;

    [Header("Grounding")]
    [Tooltip("Layers the ground raycast can hit. Bots stand on Ground.")]
    public LayerMask groundLayers = 1 << 3;
    public float groundRayDistance = 10f;
    public float groundRayStartHeight = 0.5f;

    public bool Alive { get { return !down; } }
    public CharacterRig Rig { get { return rig; } }
    public RigState AnimState { get { return rig != null ? rig.State : RigState.Idle; } }

    // The first living boss's animation state, for the playtest's boss-tell and boss-phase lines.
    public static string BossAnim()
    {
        for (int i = 0; i < Roster.Count; i++)
        {
            Enemy e = Roster[i];
            if (e != null && e.Boss && e.rig != null && !e.down)
                return CharacterRig.Describe(e.rig.State, e.rig.Rear, e.rig.Stagger);
        }
        return "no boss";
    }
    public bool Climbing { get { return climbing; } }
    public static int Climbers { get; private set; }

    Transform clawL;
    Transform clawR;
    Transform head;
    Transform legL;
    Transform legR;
    // Gen quadarm Yo Bot: back pair (LegBL/LegBR). Stand-in only has LegL/LegR.
    Transform legBL;
    Transform legBR;
    Transform model;
    // Spider Bots: LegL1..LegL4 then LegR1..LegR4, front pair first (Gen spider has six: 1..3 per side).
    readonly Transform[] spiderLegs = new Transform[8];
    readonly Quaternion[] spiderLegRest = new Quaternion[8];
    bool spider;
    bool swipeLeft;
    Quaternion clawLRest = Quaternion.identity;
    Quaternion clawRRest = Quaternion.identity;
    Quaternion legLRest = Quaternion.identity;
    Quaternion legRRest = Quaternion.identity;
    Quaternion legBLRest = Quaternion.identity;
    Quaternion legBRRest = Quaternion.identity;
    Quaternion headRestRot = Quaternion.identity;
    Quaternion modelRest = Quaternion.identity;
    bool modelled;
    bool lastStepSign;
    // The bot's animation state (CharacterRig.cs): walk cycle matched to ground speed, wind-up, strike, flinch, stagger, death.
    CharacterRig rig;
    bool winding;
    float windLeft;
    bool atCircleWas;
    // Within reach of Slice this frame (AttackTell pulses her circle while any bot has this set).
    public bool AtCircle { get; private set; }

    public Transform StrikingLimb() { return AttackLimb(); }

    // The claw (Yo Bot) or front leg (Spider Bot) that strikes next: the part AttackTell glows.
    Transform AttackLimb()
    {
        if (spider)
            return swipeLeft ? spiderLegs[0] : spiderLegs[4];
        return swipeLeft ? clawL : clawR;
    }
    float slideExcuse;
    Vector3 modelScaleRest = Vector3.one;
    float holdLeft;
    float topY = 0.9f;
    Vector3 lastPos;
    Color modelTint = Color.white;
    float modelTintAmount;
    float flash;
    bool statusGlow;
    Renderer[] skins;
    MaterialPropertyBlock flashBlock;
    static readonly int FlashId = Shader.PropertyToID("_Flash");

    float verticalVelocity;
    // A shove from the bat: slides off quickly on the ground, carries in the air. knockAir is low-gravity
    // flight time left (a home run).
    Vector3 knock;
    float knockAir;
    float flightTimer;
    float nextFlightTimer = 0.6f;
    float flightStartY;
    float fallTime;
    float taken;
    bool down;
    bool grounded;

    // Campaign levels (Level*.cs): there is no circle to strike, so awake bots walk at Wallit himself, and a bot placed in a
    // room is Dormant (it does nothing) until its encounter wakes it or a blow lands on it.
    public static bool Hunt;
    public bool Dormant;
    // In-engine cinematic: the director places and poses this bot; Update only animates, never hunts or climbs.
    public bool Scripted;
    bool flying;
    bool splitter;
    bool juker;
    bool blinker;
    bool drone;
    float bodyScale = 1f;
    float jukePhase;
    float blinkIn = 2.5f;
    bool ragdoll;
    bool climbing;
    bool tumbleRecover;
    bool charger;
    float tumbleLeft;
    float chargeLeft;
    float chargeWait = 1.4f;
    float climbHeight;
    float peckIn;
    Transform barRoot;
    Transform barFill;
    Vector3 shape = Vector3.one;
    float burnLeft;
    float burnDps;
    float burnShown;
    float slowLeft;
    float slowAmount;
    float frozenLeft;
    float shortLeft;
    float goldenLeft;
    float summonIn;
    Transform crest;
    Color crestColor = Color.clear;

    int GroundMask
    {
        get { return groundLayers.value; }
    }

    void Awake()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
        else
            Debug.LogWarning("Layer 'Enemy' does not exist. Create it in Project Settings > Tags and Layers.", this);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            groundLayers = 1 << groundLayer;
        else
            Debug.LogWarning("Layer 'Ground' does not exist. Create it in Project Settings > Tags and Layers.", this);

        var remove = new List<GameObject>();

        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Animator>() != null)
                remove.Add(child.gameObject);
        }

        foreach (GameObject child in remove)
            Destroy(child);

        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body == null)
            body = gameObject.AddComponent<CapsuleCollider>();

        body.direction = 1;
        body.center = new Vector3(0f, 0.38f, 0f);
        body.height = 0.78f;
        body.radius = 0.28f;

        nextFlightTimer = Random.Range(minFlightInterval, maxFlightInterval);
        jukePhase = Random.Range(0f, 6f);
        blinkIn = Random.Range(1.4f, 3f);
    }

    void Start()
    {
        if (transform.Find("Bot") == null)
            BuildModel();
        BuildHealthBar();
        BuildCrest();
    }

    static Mesh crestMesh;

    // About a unit tall and wide, like the cube it replaced, so the size maths below is unchanged.
    static Mesh CrestMesh()
    {
        if (crestMesh != null)
            return crestMesh;
        var k = new KitMesh { Name = "Crest Gem" };
        k.Lathe(new List<Vector2> {
            new Vector2(0f, -0.6f), new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.12f),
            new Vector2(0.3f, 0.4f), new Vector2(0f, 0.4f),
        }, 6, false);
        crestMesh = k.ToMesh();
        return crestMesh;
    }

    void BuildCrest()
    {
        if (crestColor.a <= 0f)
            return;
        // A cut gem (pavilion, girdle, crown, table) turned in KitMesh, never a primitive cube.
        GameObject gem = new GameObject("Crest");
        gem.AddComponent<MeshFilter>().sharedMesh = CrestMesh();
        gem.AddComponent<MeshRenderer>();
        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = new Vector3(0f, topY + 0.35f, 0f);
        // Keep the gem a readable size in the world no matter how big the bot is.
        float world = (Boss ? 0.55f : Golden ? 0.4f : 0.3f) + bodyScale * 0.04f;
        float size = world / Mathf.Max(0.2f, bodyScale);
        gem.transform.localScale = new Vector3(size / shape.x, size / shape.y, size / shape.z);
        UpgradeLook.Paint(gem, crestColor);
        Renderer skin = gem.GetComponent<Renderer>();
        if (skin != null)
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        crest = gem.transform;
    }

    // Burn, slow, freeze, boss calls, and the golden escape clock. False means skip moving this frame.
    bool TickStatus(float dt)
    {
        if (crest != null)
            crest.Rotate(0f, 140f * dt, 0f, Space.World);

        if (burnLeft > 0f)
        {
            burnLeft -= dt;
            float tick = burnDps * dt;
            burnShown += tick;
            bool showNow = burnShown >= Mathf.Max(1f, HitsToDrop * 0.08f) || burnLeft <= 0f;
            Damage(tick, DamageKind.Burn, false, showNow ? burnShown : 0f);
            if (showNow)
                burnShown = 0f;
            if (down)
                return false;
        }

        if (slowLeft > 0f)
        {
            slowLeft -= dt;
            if (slowLeft <= 0f)
                slowAmount = 0f;
        }

        if (frozenLeft > 0f)
        {
            frozenLeft -= dt;
            return false;
        }

        if (shortLeft > 0f)
        {
            shortLeft -= dt;
            return false;
        }

        if (holdLeft > 0f)
        {
            holdLeft -= dt;
            return false;
        }

        if (Boss && !IsCourier && !down)
        {
            summonIn -= dt;
            if (summonIn <= 0f)
            {
                summonIn = Tuning.BossSummonEvery;
                if (WaveDirector.Instance != null)
                    WaveDirector.Instance.BossCall(transform.position, 3 + WaveDirector.Wave / 10);
                Fx.Ring(transform.position, bodyScale * 3.2f, new Color(1f, 0.3f, 0.22f, 0.9f), 0.6f);
                Fx.Emit(FxKind.Dust, transform.position, 12, new Color(0.6f, 0.55f, 0.48f, 0.6f), bodyScale * 0.35f, bodyScale * 0.6f, bodyScale);
            }
        }

        if (Golden && !down)
        {
            goldenLeft -= dt;
            if (goldenLeft <= 0f)
            {
                UpgradeLook.Say("The Golden Yo Bot got away", 2f, 120f);
                UpgradeLook.Scrap(transform.position, 10, true);
                Destroy(gameObject);
                return false;
            }
        }
        return true;
    }

    public float GoldenLeft
    {
        get { return goldenLeft; }
    }

    public float SpeedFactor
    {
        get { return 1f - slowAmount; }
    }

    public void Ignite(float dps, float seconds)
    {
        if (down)
            return;
        burnDps = burnLeft > 0f ? Mathf.Max(burnDps, dps) : dps;
        burnLeft = Mathf.Max(burnLeft, seconds);
    }

    public void Chill(float amount, float seconds, float freeze)
    {
        if (down)
            return;
        slowAmount = Mathf.Max(slowAmount, Mathf.Clamp(amount, 0f, 0.8f));
        slowLeft = Mathf.Max(slowLeft, seconds);
        if (freeze > 0f && frozenLeft <= 0f && !Boss)
        {
            frozenLeft = freeze;
        }
    }

    // Live Wire: the bot shorts out and stands twitching for a moment. Bosses shrug off most of it.
    public void Short(float seconds)
    {
        if (down)
            return;
        shortLeft = Mathf.Max(shortLeft, Boss ? seconds * 0.3f : seconds);
    }

    void OnEnable()
    {
        Roster.Add(this);
        if (barRoot != null && !down)
            barRoot.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        Roster.Remove(this);
        if (barRoot != null)
            barRoot.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (barRoot != null)
            Destroy(barRoot.gameObject);
        if (climbing)
        {
            climbing = false;
            Climbers = Mathf.Max(0, Climbers - 1);
        }
    }

    public void ApplyBot(BotDef def)
    {
        Kind = def.Kind;
        spider = BotRoster.IsSpider(def.Kind);
        DebutWave = def.Kind == BotKind.YoBot ? 0 : def.UnlockWave;
        HitsToDrop = Mathf.Max(1, Mathf.RoundToInt(def.Hits * Tuning.HealthScale(WaveDirector.Wave)));
        Title = def.Name;
        walkSpeed = def.Walk * Random.Range(0.86f, 1.18f) * 1.5f;
        chaseSpeed = def.Chase * Random.Range(0.9f, 1.16f) * 1.5f;
        jumpVelocity = def.Jump * Random.Range(0.9f, 1.12f);
        maxFlightHeight = def.HopHeight;
        minFlightInterval = Random.Range(0.9f, 2.2f);
        maxFlightInterval = minFlightInterval + Random.Range(0.5f, 1.8f);
        juker = def.Kind == BotKind.YoBot ? Random.value < 0.55f : def.Weave && Random.value < 0.7f;
        charger = def.Charge;
        // Authored Yo Bots keep their gunmetal/cyan skin and share one instanced material; stand-in Yo Bots get a random coat.
        if (def.Kind == BotKind.YoBot && !BotArt.HasAuthoredModel(BotKind.YoBot))
        {
            modelTint = RandomPaint();
            modelTintAmount = 0.28f;
        }
        bodyScale = def.Scale;
        // The models carry their own proportions, so they scale evenly.
        shape = Vector3.one;
        transform.localScale = shape * def.Scale;
        gameObject.name = def.Name;
        float pace = WaveDirector.SpeedScale;
        walkSpeed *= pace;
        chaseSpeed *= pace;
        if (WaveDirector.FlightScale > 1f)
        {
            minFlightInterval /= WaveDirector.FlightScale;
            maxFlightInterval /= WaveDirector.FlightScale;
            maxFlightHeight *= WaveDirector.FlightScale;
        }
    }

    // A Yo Bot with a trait. Tougher, worth more bolts, and marked with a gem over its head.
    public void MakeElite(BotTrait trait)
    {
        int baseHealth = HitsToDrop;
        float before = bodyScale;
        bodyScale = 1f;
        ApplyTrait(trait);
        bodyScale = before * bodyScale;
        transform.localScale = shape * bodyScale;
        float toughness = trait == BotTrait.Juggernaut ? 6f : trait == BotTrait.Bruiser ? 4f : trait == BotTrait.Swarmer ? 0.6f : 2.5f;
        HitsToDrop = Mathf.Max(1, Mathf.RoundToInt(baseHealth * toughness));
        IsHeavy = false;
        Elite = true;
        Title = Kind == BotKind.YoBot ? TraitTitle(trait) : trait + " " + Title;
        gameObject.name = Title;
        crestColor = EliteColor(trait);
        modelTint = crestColor;
        modelTintAmount = 0.5f;
    }

    public void MakeGolden()
    {
        Golden = true;
        Title = "Golden Yo Bot";
        gameObject.name = Title;
        HitsToDrop = Mathf.RoundToInt((10f + WaveDirector.Wave * 3f) * Tuning.HealthScale(WaveDirector.Wave));
        walkSpeed *= 1.6f;
        chaseSpeed *= 1.4f;
        juker = true;
        goldenLeft = Tuning.GoldenEscapeSeconds;
        crestColor = new Color(1f, 0.85f, 0.25f);
        modelTint = new Color(1f, 0.8f, 0.22f);
        modelTintAmount = 0.92f;
    }

    // A boss's tell, played on its body: rear back and hold while the ground marks fill (BossPhases, Courier, Server), then slam
    // when it lands or drop the pose when the wind-up is broken. A stagger is phase two arriving, or the wind-up broken.
    public void BeginTell()
    {
        if (rig != null)
            rig.BeginTell();
    }

    public void EndTell(bool slam)
    {
        if (rig != null)
            rig.EndTell(slam);
    }

    public void Stagger(float seconds)
    {
        if (rig != null)
            rig.BeginStagger(seconds);
    }

    // Stand still for a while (the Courier's wind-up and recovery). Bat hits still land.
    public void Hold(float seconds)
    {
        holdLeft = Mathf.Max(0f, seconds);
    }

    // Chapter 1's Alpha Yo Bot, with a clipboard and a notice of striking. Its fight is Courier.cs.
    public void MakeCourier(string name, int hits)
    {
        MakeBoss();
        IsCourier = true;
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        modelTint = new Color(1.3f, 0.95f, 0.45f);
        modelTintAmount = 0.35f;
        PhaseAt = 0.5f;
        SetAuthored("courier");
        gameObject.AddComponent<Courier>();
    }

    // Chapter 4's Alpha, the first full Spider Bot. Its fight is Server.cs: it offers a paper, will not strike until the paper
    // is refused three times, and cannot fall before then (MinHealth01).
    public void MakeServer(string name, int hits)
    {
        MakeBoss();
        IsCourier = true;
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        MinHealth01 = 0.7f;
        modelTint = new Color(0.8f, 1.1f, 1.2f);
        modelTintAmount = 0.3f;
        PhaseAt = 0.4f;
        SetAuthored("server");
        gameObject.AddComponent<Server>();
    }

    // A boss's second phase: it speeds up, takes on a hotter tint and a deeper crest. The tell and the banner are the caller's.
    public void EnterPhase2(Color tint)
    {
        Phase = 2;
        walkSpeed *= 1.25f;
        chaseSpeed *= 1.25f;
        modelTint = tint;
        modelTintAmount = Mathf.Max(modelTintAmount, 0.45f);
        crestColor = new Color(1f, 0.12f, 0.08f);
    }

    // The Server has been refused enough: it behaves as a plain boss from here (swipes and stabs, calls bots).
    public void Unleash()
    {
        IsCourier = false;
        MinHealth01 = 0f;
    }

    // Chapter 3's Alpha: a Mega Yo Bot grown big and amber, with the order held flat on its chest (a pale sheet with ruled
    // lines, no collider). No special fight: it reads the order aloud and is a wall to break.
    public void MakeForeman(string name, int hits)
    {
        MakeBoss();
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        modelTint = new Color(1.2f, 1f, 0.62f);
        modelTintAmount = 0.3f;
        SetAuthored("foreman");
        if (model == null || FindPart(model, "Order Sheet") == null)
        {
            // The code body carries no sheet of its own (the Gen body does: a part named Order Sheet on its chest).
            GameObject sheet = CharacterKit.Prop("order_sheet");
            sheet.name = "Order Sheet";
            sheet.transform.SetParent(transform, false);
            sheet.transform.localPosition = new Vector3(0f, 0.36f, 0.24f);
            sheet.transform.localScale = Vector3.one * 0.8f;
            sheet.transform.localRotation = Quaternion.Euler(-6f, 0f, -3f);
        }
        PhaseAt = 0.5f;
        gameObject.AddComponent<BossPhases>().Setup(BossPhases.Fight.Foreman);
    }

    // Chapter 5's Alpha: a Titan Spider Bot in gold, the Registrar, with a ledger of gold-edged pages held open in front. No
    // special fight: it reads the pages with every leg and is the biggest wall to break so far.
    public void MakeRegistrar(string name, int hits)
    {
        MakeBoss();
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        SetAuthored("registrar");
        if (model == null || FindPart(model, "Ledger") == null)
        {
            // Fallback: the shared Titan Spider Bot with the ledger prop held open in front.
            GameObject ledger = CharacterKit.Prop("ledger");
            ledger.name = "Ledger";
            ledger.transform.SetParent(transform, false);
            ledger.transform.localPosition = new Vector3(0f, 0.55f, 0.66f);
            ledger.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
        }
        PhaseAt = 0.5f;
        gameObject.AddComponent<BossPhases>().Setup(BossPhases.Fight.Registrar);
    }

    // Chapter 6's Alpha, the Spider Bot that carries the order: a sealed scroll across its back with a red stamp, a wall to
    // break. No special fight. The scroll is flat, collider-free blocks.
    public void MakeOrderBearer(string name, int hits)
    {
        MakeBoss();
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        SetAuthored("order_bearer");
        PhaseAt = 0.5f;
        gameObject.AddComponent<BossPhases>().Setup(BossPhases.Fight.Order);
    }

    // Chapter 2's Alpha: a Plated Yo Bot grown big, with a stamped INVALID plate on its chest. No special fight: a wall
    // of plate to break. The plate is a flat red board with a white bar, built in code, no collider.
    public void MakePlatedAlpha(string name, int hits)
    {
        MakeBoss();
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        modelTint = new Color(1.15f, 0.8f, 0.78f);
        modelTintAmount = 0.3f;
        SetAuthored("plated_alpha");
        if (model == null || FindPart(model, "Invalid Plate") == null)
        {
            // Fallback: the shared Plated Yo Bot with the stamped plate on its chest.
            GameObject plate = CharacterKit.Prop("plate");
            plate.name = "Invalid Plate";
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = new Vector3(0f, 0.5f, 0.62f);
            plate.transform.localRotation = Quaternion.Euler(-8f, 0f, 4f);
        }
        PhaseAt = 0.5f;
        gameObject.AddComponent<BossPhases>().Setup(BossPhases.Fight.Plated);
    }

    // A chapter's new bot: an elite trait under the chapter's own name (Dispatch Yo Bot, Returned Yo Bot...).
    public void MakeVariant(BotTrait trait, string name)
    {
        MakeElite(trait);
        Title = name;
        gameObject.name = name;
    }

    // A mid-chapter mini-boss: an Alpha with its own name, tint and a short health bar, never bigger than the chapter's boss.
    public void MakeMini(string name, int hits, Color tint)
    {
        MakeBoss();
        Title = name;
        gameObject.name = name;
        HitsToDrop = Mathf.Max(1, hits);
        bodyScale *= 0.85f;
        transform.localScale = shape * bodyScale;
        modelTint = tint;
        modelTintAmount = 0.4f;
    }

    public void MakeBoss()
    {
        Boss = true;
        Title = "Alpha " + Title;
        gameObject.name = Title;
        HitsToDrop = Mathf.RoundToInt(HitsToDrop * Tuning.BossHealthScale);
        bodyScale *= 1.35f;
        transform.localScale = shape * bodyScale;
        walkSpeed *= 0.85f;
        summonIn = Tuning.BossSummonEvery * 0.6f;
        crestColor = new Color(1f, 0.3f, 0.22f);
    }

    static Color EliteColor(BotTrait trait)
    {
        switch (trait)
        {
            case BotTrait.Sprinter: return new Color(1f, 0.95f, 0.3f);
            case BotTrait.Bruiser: return new Color(1f, 0.35f, 0.25f);
            case BotTrait.Splitter: return new Color(1f, 0.6f, 0.2f);
            case BotTrait.Blinker: return new Color(0.85f, 0.4f, 1f);
            case BotTrait.Charger: return new Color(1f, 0.25f, 0.15f);
            case BotTrait.JumpJets: return new Color(0.4f, 0.75f, 1f);
            case BotTrait.Juggernaut: return new Color(0.55f, 0.3f, 0.75f);
            case BotTrait.Swarmer: return new Color(1f, 0.85f, 0.4f);
            default: return new Color(0.5f, 1f, 0.5f);
        }
    }

    // What an elite is called on the hill.
    public static string TraitTitle(BotTrait trait)
    {
        switch (trait)
        {
            case BotTrait.None: return "Yo Bot";
            case BotTrait.JumpJets: return "Jump-Jet Yo Bot";
            default: return trait + " Yo Bot";
        }
    }

    static Color RandomPaint()
    {
        Color[] palette =
        {
            new Color(1.15f, 0.9f, 0.55f),
            new Color(0.65f, 0.85f, 1.25f),
            new Color(1.25f, 0.68f, 0.4f),
            new Color(0.75f, 1.2f, 0.65f),
            new Color(1.3f, 0.5f, 0.42f),
            new Color(0.95f, 0.72f, 1.25f),
            new Color(1.05f, 0.82f, 0.45f),
            new Color(0.7f, 0.95f, 1.05f)
        };
        return palette[Random.Range(0, palette.Length)];
    }

    public void Apply(BotTrait primary, BotTrait secondary)
    {
        bodyScale = 1f;
        ApplyTrait(primary);
        if (secondary != BotTrait.None && secondary != primary)
            ApplyTrait(secondary);
        transform.localScale = Vector3.one * bodyScale;
        gameObject.name = TraitName(primary, secondary);
    }

    // A Splitter comes apart into two small drones.
    public void ApplyDrone()
    {
        drone = true;
        splitter = false;
        HitsToDrop = 1;
        walkSpeed = 1.9f;
        chaseSpeed = 2.8f;
        jumpVelocity = 3.2f;
        maxFlightHeight = 1.15f;
        minFlightInterval = 0.6f;
        maxFlightInterval = 1.4f;
        bodyScale = 0.58f;
        modelTint = new Color(1f, 0.85f, 0.45f);
        modelTintAmount = 0.6f;
        transform.localScale = Vector3.one * bodyScale;
        Title = "Drone";
        gameObject.name = Title;
    }

    void ApplyTrait(BotTrait trait)
    {
        switch (trait)
        {
            case BotTrait.Sprinter:
                walkSpeed *= 1.8f;
                chaseSpeed *= 1.45f;
                bodyScale *= 0.92f;
                break;
            case BotTrait.Hopper:
                jumpVelocity *= 1.2f;
                maxFlightHeight += 0.35f;
                minFlightInterval = 0.7f;
                maxFlightInterval = 1.5f;
                flightDuration *= 0.85f;
                break;
            case BotTrait.Bruiser:
                HitsToDrop += 2;
                walkSpeed *= 0.72f;
                chaseSpeed *= 0.72f;
                bodyScale *= 1.42f;
                break;
            case BotTrait.Juker:
                juker = true;
                walkSpeed *= 1.15f;
                break;
            case BotTrait.JumpJets:
                flightGravity = 4.2f;
                flightDuration = 2.5f;
                chaseSpeed *= 1.35f;
                maxFlightHeight += 0.55f;
                jumpVelocity *= 0.85f;
                break;
            case BotTrait.Splitter:
                splitter = true;
                break;
            case BotTrait.Blinker:
                blinker = true;
                break;
            case BotTrait.Charger:
                charger = true;
                walkSpeed *= 1.15f;
                break;
            case BotTrait.Swarmer:
                HitsToDrop = 1;
                walkSpeed *= 1.7f;
                chaseSpeed *= 1.45f;
                bodyScale *= 0.62f;
                jumpVelocity *= 0.8f;
                break;
            case BotTrait.Juggernaut:
                HitsToDrop += 4;
                walkSpeed *= 0.55f;
                chaseSpeed *= 0.6f;
                bodyScale *= 1.75f;
                break;
            case BotTrait.Heavy:
                IsHeavy = true;
                HitsToDrop = 5 + WaveDirector.Wave / 5;
                walkSpeed *= 0.62f;
                chaseSpeed *= 0.75f;
                jumpVelocity *= 0.75f;
                maxFlightHeight = 1.25f;
                bodyScale *= 1.85f;
                modelTint = new Color(1.7f, 0.28f, 0.22f);
                modelTintAmount = 0.5f;
                break;
        }
    }

    static string TraitName(BotTrait primary, BotTrait secondary)
    {
        if (secondary == BotTrait.None || secondary == primary)
            return TraitTitle(primary);
        return primary + " " + TraitTitle(secondary);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        FaceBar();

        if (Dormant && !down && !Scripted)
            return;

        if (Scripted && !down && !ragdoll)
        {
            if (modelled)
            {
                AnimateModel(dt);
                TickFlash(dt);
            }
            return;
        }

        if (ragdoll)
        {
            TickRagdoll(dt);
            if (modelled && this != null)
            {
                AnimateModel(dt);
                TickFlash(dt);
            }
            return;
        }

        if (climbing)
        {
            TickClimb(dt);
            return;
        }

        if (!TickStatus(dt))
        {
            // Held for a boss's tell, frozen or shorted: the body still plays its pose (the rear-up, the shiver) while it stands.
            if (modelled && !down && !ragdoll)
            {
                AnimateModel(dt);
                TickFlash(dt);
            }
            return;
        }

        bool swarm = GoesForPlayer();
        bool hunting = Hunt && !swarm && !down && !Golden && PlayerGrowth.Instance != null;
        if (swarm)
        {
            Vector3 flat = PlayerGrowth.Instance.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= ClimbReach())
                TryClimb();
        }

        Musician musician = Musician.Instance;
        Vector3 toTarget = Vector3.zero;
        float distance = float.MaxValue;
        DefencePiece wall = null;
        bool sceneryBlock = false;
        float reach = 1.15f + bodyScale * 0.22f;
        bool diving = false;

        if (swarm || hunting)
        {
            toTarget = PlayerGrowth.Instance.transform.position - transform.position;
            toTarget.y = 0f;
            distance = toTarget.magnitude;
            diving = true;
            ProbeAhead(toTarget, distance, out wall, out sceneryBlock);
        }

        if (Golden && !down && PlayerGrowth.Instance != null)
        {
            // Run from the player while circling the hill.
            Vector3 away = transform.position - PlayerGrowth.Instance.transform.position;
            away.y = 0f;
            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 around = Vector3.Cross(Vector3.up, flat.sqrMagnitude > 1f ? flat.normalized : Vector3.forward);
            Vector3 flee = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            if (away.magnitude > 22f)
                flee = Vector3.zero;
            Vector3 wish = flee + around * 0.7f;
            if (flat.magnitude > 55f)
                wish -= flat.normalized * 1.2f;
            toTarget = wish.normalized * 50f;
            distance = 50f;
            diving = true;
        }

        if (!diving && !down && musician != null && musician.Alive)
        {
            toTarget = musician.transform.position - transform.position;
            toTarget.y = 0f;
            distance = toTarget.magnitude;
            ProbeAhead(toTarget, distance, out wall, out sceneryBlock);
        }

        bool chasing = !down && distance > reach && wall == null && !sceneryBlock &&
                       (diving || (musician != null && musician.Alive));

        // Fire the jump jets only while chasing and standing on the ground.
        if (!down && chasing && grounded && !flying)
        {
            nextFlightTimer -= dt;

            if (nextFlightTimer <= 0f)
                StartFlightAttempt();
        }

        Vector3 horizontal = Vector3.zero;

        if (chasing)
        {
            Vector3 wish = toTarget.normalized;
            float speed = (flying ? chaseSpeed : walkSpeed) * SpeedFactor;
            if (charger && !flying)
            {
                chargeWait -= dt;
                if (chargeLeft > 0f)
                {
                    chargeLeft -= dt;
                    speed *= 3.3f;
                }
                else if (chargeWait <= 0f)
                {
                    chargeLeft = 0.38f;
                    chargeWait = Random.Range(1.5f, 2.6f);
                }
            }
            horizontal = wish * speed;
            if (juker)
            {
                Vector3 side = Vector3.Cross(Vector3.up, wish);
                horizontal += side * Mathf.Sin(Time.time * 4.2f + jukePhase) * 2.1f;
            }

            if (wish.sqrMagnitude > 0.001f)
            {
                Quaternion face = Quaternion.LookRotation(wish);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, face, dt * 5f);
            }
        }
        else if (wall != null && toTarget.sqrMagnitude > 0.01f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized);
            float sign = (GetInstanceID() & 1) == 0 ? 1f : -1f;
            horizontal = side * sign * walkSpeed * 0.8f;
            Quaternion face = Quaternion.LookRotation(toTarget.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, dt * 5f);
        }

        bool atMusician = !diving && !Golden && distance <= Mathf.Max(reach, sceneryBlock ? 3.2f : reach);
        bool atPlayer = false;
        if (!down && !Golden && knock.sqrMagnitude < 0.2f && PlayerGrowth.Instance != null)
        {
            Vector3 toPlayer = PlayerGrowth.Instance.transform.position - transform.position;
            toPlayer.y = 0f;
            atPlayer = toPlayer.magnitude <= PlayerGrowth.Instance.BodyRadius + 1.15f + bodyScale * 0.22f;
        }
        bool canAttack = !down && !IsCourier && (hunting || (musician != null && musician.Alive));
        AtCircle = atMusician && canAttack && musician != null && musician.Alive;
        if (AtCircle && !atCircleWas)
            AttackTell.ReachedCircle(this);
        atCircleWas = AtCircle;
        if (canAttack && winding)
        {
            // The tell: the claw goes up (or the front leg rears) for a beat before the blow, so the hit can be read and dodged.
            windLeft -= dt;
            if (windLeft <= 0f)
            {
                winding = false;
                // A claw swipe from a Yo Bot, a stab of a front leg from a Spider Bot.
                if (rig != null)
                    rig.BeginStrike();
                BotSound.Hit(this);
                AttackTell.Land(this);
                GameAudio.PlayAt("batswing", transform.position + Vector3.up, 0.4f, spider ? 0.85f : 1.5f);
                if (wall != null && wall.Alive)
                    wall.Stop(this);
                else if (atMusician)
                    musician.Hurt(Boss ? 3 : Elite ? 2 : 1, this);
                else if (atPlayer && PlayerGrowth.Instance != null)
                    PlayerGrowth.Instance.OnHit(this);
                verticalVelocity = Mathf.Max(verticalVelocity, 1.5f);
            }
        }
        else if (canAttack && (wall != null || atMusician || atPlayer))
        {
            peckIn -= dt;
            if (peckIn <= 0f)
            {
                peckIn = 1.05f;
                swipeLeft = !swipeLeft;
                winding = true;
                windLeft = rig != null ? rig.WindSeconds : 0.3f;
                if (rig != null)
                    rig.BeginWindup(windLeft);
                BotSound.Tell(this);
                // The tell you can see: the striking limb glows and the target's patch of ground fills red until the blow lands.
                Vector3 blowAt = wall != null ? wall.transform.position
                    : atMusician ? musician.transform.position
                    : PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform.position : transform.position;
                AttackTell.Begin(this, AttackLimb(), blowAt, Mathf.Clamp(0.85f + bodyScale * 0.22f, 1f, 2.4f), windLeft);
            }
        }
        else if (winding)
        {
            winding = false;
            AttackTell.Cancel(this);
            if (rig != null)
                rig.CancelWind();
        }

        if (flying)
        {
            flightTimer -= dt;

            // The jets hold it up: reduced gravity gives a short, floating descent.
            verticalVelocity -= flightGravity * dt;

            // Do not let a flight climb indefinitely.
            if (transform.position.y >= flightStartY + maxFlightHeight &&
                verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if (flightTimer <= 0f)
            {
                flying = false;
                if (!down)
                    nextFlightTimer = Random.Range(minFlightInterval, maxFlightInterval);
            }
        }
        else
        {
            verticalVelocity -= (knockAir > 0f ? 9f : normalGravity) * dt;
        }
        if (knockAir > 0f)
            knockAir -= dt;

        if (knock.sqrMagnitude > 0.01f)
        {
            // Being shoved beats walking.
            horizontal = horizontal * 0.15f + knock;
            float drag = grounded ? 12f + knock.magnitude * 1.2f : 1.5f;
            knock = Vector3.MoveTowards(knock, Vector3.zero, drag * dt);
        }

        if (blinker && !down && grounded && !flying)
        {
            blinkIn -= dt;
            if (blinkIn <= 0f)
                blinkIn = TryBlink() ? Random.Range(2.2f, 3.6f) : 0.45f;
        }

        Vector3 next = transform.position + horizontal * dt;
        next.y += verticalVelocity * dt;

        // A hard safety cap prevents runaway upward movement.
        if (flying && next.y > flightStartY + maxFlightHeight)
        {
            next.y = flightStartY + maxFlightHeight;
            if (verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        grounded = StickToGround(ref next);
        TickLaunch(dt, next);

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = 0f;
            if (flying)
            {
                flying = false;
                if (!down)
                    nextFlightTimer = Random.Range(minFlightInterval, maxFlightInterval);
            }
        }

        transform.position = next;

        if (modelled)
        {
            AnimateModel(dt);
            TickFlash(dt);
        }

        if (down)
        {
            fallTime += dt;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(-75f, transform.eulerAngles.y, 0f),
                dt * 6f);

            if (fallTime > 2.4f)
                Destroy(gameObject);
        }
    }

    // The probe is the costliest per-bot call in a crowd, so each bot re-asks about every 0.1 s (staggered by its id)
    // and reuses the answer between; the ray buffer is shared so the cast allocates nothing.
    static readonly RaycastHit[] ProbeHits = new RaycastHit[16];
    float probeNext = -1f;
    DefencePiece probeWall;
    bool probeScenery;

    void ProbeAhead(Vector3 toTarget, float distance, out DefencePiece wall, out bool sceneryBlock)
    {
        if (Time.time < probeNext)
        {
            wall = probeWall != null && probeWall.Alive && probeWall.Blocks ? probeWall : null;
            sceneryBlock = probeScenery;
            return;
        }
        probeNext = Time.time + 0.1f + (GetInstanceID() & 7) * 0.004f;
        ProbeAheadNow(toTarget, distance, out wall, out sceneryBlock);
        probeWall = wall;
        probeScenery = sceneryBlock;
    }

    void ProbeAheadNow(Vector3 toTarget, float distance, out DefencePiece wall, out bool sceneryBlock)
    {
        wall = null;
        sceneryBlock = false;
        if (toTarget.sqrMagnitude < 0.04f)
            return;

        Vector3 wish = toTarget.normalized;
        float probe = 0.65f + bodyScale * 0.24f;
        Vector3 origin = transform.position + Vector3.up * 0.42f;
        int aheadCount = Physics.RaycastNonAlloc(origin, wish, ProbeHits, probe, ~0, QueryTriggerInteraction.Ignore);
        float best = probe + 1f;
        for (int h = 0; h < aheadCount; h++)
        {
            RaycastHit hit = ProbeHits[h];
            if (hit.collider == null || hit.distance > best)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.normal.y > 0.7f)
                continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null)
                continue;
            if (hit.collider.GetComponentInParent<CharacterController>() != null)
                continue;
            if (hit.collider.GetComponentInParent<Musician>() != null)
                continue;
            DefencePiece piece = hit.collider.GetComponentInParent<DefencePiece>();
            if (piece != null && (!piece.Alive || !piece.Blocks))
                continue;
            if (piece == null && distance > 3.4f)
                continue;
            best = hit.distance;
            wall = piece != null && piece.Blocks ? piece : null;
            sceneryBlock = wall == null;
        }
    }

    // A jump-jet hop: a soft puff under the feet, then up.
    void StartFlightAttempt()
    {
        flying = true;
        grounded = false;
        flightTimer = flightDuration;
        flightStartY = transform.position.y;
        verticalVelocity = jumpVelocity;
        Fx.JetPuff(transform.position, transform.lossyScale.y);
    }

    // One plain blow at the player's arrow strength: spikes, towers, stomps.
    public void Hit(float power = 1f)
    {
        if (down)
            return;

        int damage = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.ArrowDamage : 1;
        Damage(damage, DamageKind.Plain, false, damage, power);
    }

    // The bat sends the bot along velocity (its y is the lift). airSeconds keeps it flying low-gravity, for a
    // home run. Bosses barely move.
    public void Knock(Vector3 velocity, float airSeconds = 0f)
    {
        if (down || ragdoll || climbing)
            return;
        if (Boss)
            velocity *= 0.25f;
        knock = new Vector3(velocity.x, 0f, velocity.z);
        slideExcuse = 0.4f;
        if (velocity.y > 0f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
            grounded = false;
        }
        flying = false;
        knockAir = Mathf.Max(knockAir, Boss ? 0f : airSeconds);
    }

    // Bat contact: the bot rocks back a beat (model only) and the shove slides it off. Counted for the playtest.
    float spinLeft;
    float spinAngle;
    bool launched;
    bool launchAirborne;
    float launchLeft;
    TrailRenderer launchTrail;
    Vector3 launchPrev;
    public static int Flinches { get; private set; }
    public static int HomeRunLandings { get; private set; }
    public static int HomeRunLaunches { get; private set; }
    public bool Launched { get { return launched; } }

    public void Flinch(float strength)
    {
        if (down)
            return;
        Flinches++;
        if (rig != null)
            rig.Flinch(strength);
    }

    // A home run: the bot tumbles end over end behind a trail until it lands, and puffs where it comes down.
    public void HomeRunLaunch(float seconds)
    {
        if (Boss)
            return;
        launched = true;
        launchAirborne = false;
        launchPrev = transform.position;
        BotChorus.OnLaunched(this);
        launchLeft = 4f;
        spinLeft = Mathf.Max(0.5f, seconds);
        spinAngle = 0f;
        HomeRunLaunches++;
        Shader trailShader = SafeShader.Find("Sprites/Default");
        if (trailShader == null || launchTrail != null || UiFit.Touch)   // the phone tier has the speed lines, not a ribbon
            return;
        var tip = new GameObject("LaunchTrail").transform;
        tip.SetParent(transform, false);
        tip.localPosition = Vector3.up * 0.6f;
        launchTrail = tip.gameObject.AddComponent<TrailRenderer>();
        launchTrail.time = 0.45f;
        launchTrail.minVertexDistance = 0.1f;
        launchTrail.alignment = LineAlignment.View;
        launchTrail.numCapVertices = 0;
        launchTrail.widthMultiplier = 0.7f * Mathf.Sqrt(Mathf.Max(1f, bodyScale));
        launchTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        if (launchTrailMat == null)
            launchTrailMat = new Material(trailShader);   // one for every launch: a Material per home run was never freed
        launchTrail.sharedMaterial = launchTrailMat;
        launchTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        launchTrail.receiveShadows = false;
        launchTrail.startColor = new Color(1f, 0.85f, 0.4f, 0.85f);
        launchTrail.endColor = new Color(1f, 0.6f, 0.3f, 0f);
    }

    void TickLaunch(float dt, Vector3 at)
    {
        if (!launched)
            return;
        launchLeft -= dt;
        if (!grounded)
            launchAirborne = true;
        if (launchAirborne && !down)
            DeptFx.SpeedLines(at + Vector3.up * 0.6f, at - launchPrev, Mathf.Sqrt(Mathf.Max(1f, bodyScale)));
        launchPrev = at;
        bool landed = grounded && launchAirborne;
        if (!landed && launchLeft > 0f && !down)
            return;
        launched = false;
        spinLeft = 0f;
        if (launchTrail != null)
        {
            launchTrail.emitting = false;
            Destroy(launchTrail.gameObject, 0.6f);
            launchTrail = null;
        }
        if (landed)
        {
            HomeRunLandings++;
            float root = Mathf.Sqrt(Mathf.Max(1f, bodyScale));
            Fx.Emit(FxKind.Dust, at, 8, new Color(0.85f, 0.75f, 0.6f, 0.7f), 1.2f * root, 2.5f, 1.2f);
            Fx.Ring(at + Vector3.up * 0.1f, 4f * root, new Color(1f, 0.9f, 0.6f, 0.7f), 0.4f);
            DeptFx.Landing(at, root);   // the flash and dust puff where it comes down
        }
    }

    public bool Knocked
    {
        get { return knock.sqrMagnitude > 1f || knockAir > 0f; }
    }

    // Every source of harm comes through here. True when this blow dropped the bot.
    public bool Damage(float amount, DamageKind kind, bool crit, float shown = -1f, float power = 1f)
    {
        if (down || amount <= 0f)
            return false;

        Dormant = false;
        taken += amount;
        BlowsTaken++;
        if (MinHealth01 > 0f)
            taken = Mathf.Min(taken, HitsToDrop * (1f - MinHealth01));
        RefreshBar();
        Vector3 mark = transform.position + Vector3.up * (0.7f * Mathf.Max(1f, bodyScale));
        if (kind != DamageKind.Burn)
        {
            if (!Boss)
                verticalVelocity = Mathf.Max(verticalVelocity, 2.2f);
            Fx.Hit(mark, Mathf.Sqrt(Mathf.Max(1f, bodyScale)), crit);
            if (kind == DamageKind.Arrow || kind == DamageKind.Plain)
            {
                GameAudio.PlayAt("hit", transform.position, crit ? 0.9f : 0.6f, crit ? 1.25f : Random.Range(0.92f, 1.08f));
                UpgradeLook.MarkHit();
            }
            else if (kind == DamageKind.Bat)
            {
                // BatEquipment plays the crack once per swing, not once per bird.
                UpgradeLook.MarkHit();
            }
        }
        if (kind != DamageKind.Burn)
        {
            flash = 1f;   // a full white hit flash on every blow (crit or not), so the hit reads from the high camera
            // Every blow that lands rocks the body (the bat adds its own harder Flinch on top, from BatEquipment).
            if (rig != null && (!Boss || crit))
                rig.Flinch(crit ? 0.9f : 0.55f);
        }
        if (kind == DamageKind.Bat && !Boss)
            WallitVoice.OnFirstHit();
        if (kind != DamageKind.Burn && !launched)
            BotChorus.OnHit(this);
        float number = shown < 0f ? amount : shown;
        if (number > 0f)
            UpgradeLook.Number(mark, number, crit, kind);

        if (taken < HitsToDrop)
        {
            if (kind == DamageKind.Arrow || kind == DamageKind.Plain)
                Nudge();
            return false;
        }

        Die(power);
        return true;
    }

    void Die(float power)
    {
        if (down)
            return;
        if (splitter && !drone)
        {
            SpawnDrone(new Vector3(0.75f, 0f, 0.35f));
            SpawnDrone(new Vector3(-0.75f, 0f, -0.35f));
        }

        down = true;
        if (!launched)
            BotChorus.OnFall(this);
        flying = false;
        winding = false;
        if (rig != null)
            rig.BeginDeath();
        StopClimbing();
        ClearLock();
        CelebrateDeath();
        PlayerGrowth.RegisterKill();
        int coins = MetaProgress.NoteBot(Kind);
        if (coins > 0) {
            UpgradeLook.Coins(transform.position, coins);
        }
        Combat.OnKill(this);
        burnLeft = 0f;
        BeginRagdoll(power, false);
    }

    public void Stomp(float scale)
    {
        if (down)
            return;
        StopClimbing();
        Hit(scale >= 3f ? 4f : 1.4f);
        if (!down)
        {
            BeginRagdoll(scale, true);
            Stagger(1.0f);   // knocked flat and flailing, then back up
        }
    }

    public static void ShakeOffClimbers(float scale)
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.climbing)
                enemy.Fling(scale);
        }
    }

    void Fling(float scale)
    {
        StopClimbing();
        float shove = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.ArrowDamage * (scale >= 4.8f ? 2f : 1f) : 1f;
        taken += shove;
        RefreshBar();
        if (taken >= HitsToDrop)
        {
            Die(scale);
            return;
        }
        BeginRagdoll(scale, true);
    }

    void Nudge() {
        Vector3 away = transform.position;
        if (PlayerGrowth.Instance != null) {
            away -= PlayerGrowth.Instance.transform.position;
        }
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) {
            away = transform.forward;
        }
        transform.position += away.normalized * 0.2f;
        slideExcuse = 0.4f;   // a shove, not a stride
    }

    void CelebrateDeath() {
        bool boss = DebutWave >= 10 || Boss || Golden;
        DeptFx.Death(this, transform.position, transform.lossyScale.y, Paint, boss || Elite, spider);   // STRUCK OFF stamp, then Fx.Death and the scraps
        if (Golden)
            Fx.Ring(transform.position, 5f, new Color(1f, 0.82f, 0.35f, 0.9f), 0.6f);
        if (Boss)
            Fx.Explosion(transform.position + Vector3.up * topY * 0.5f * transform.lossyScale.y, transform.lossyScale.y, new Color(1f, 0.6f, 0.3f));
        if (Kind == BotKind.TitanSpiderBot) {
            UpgradeLook.Say("The Titan falls", 2.8f);
        }
    }

    // The color of the plates that fly off this bot when it falls.
    Color Paint
    {
        get
        {
            if (Golden)
                return new Color(1f, 0.82f, 0.3f);
            if (Elite || drone || Kind == BotKind.YoBot)
                return Color.Lerp(BotArt.PaintOf(Kind), modelTint, Mathf.Max(0.25f, modelTintAmount));
            return BotArt.PaintOf(Kind);
        }
    }

    void SpawnDrone(Vector3 offset)
    {
        Vector3 spot = transform.position + offset;
        spot.y = GroundAt(spot + Vector3.up * 6f, spot.y) + 0.45f;
        var go = new GameObject("Drone");
        go.tag = "enemy";
        go.transform.position = spot;
        go.transform.rotation = transform.rotation;
        Enemy part = go.AddComponent<Enemy>();
        part.ApplyDrone();
    }

    bool TryBlink()
    {
        Vector3 side = Vector3.Cross(Vector3.up, transform.forward);
        if (side.sqrMagnitude < 0.01f)
            side = Vector3.right;
        side.Normalize();
        if (Random.value < 0.5f)
            side = -side;

        Vector3 dest = transform.position + side * Random.Range(2.1f, 3.3f);
        if (!Physics.Raycast(dest + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 24f, GroundMask, QueryTriggerInteraction.Ignore))
            return false;

        dest.y = hit.point.y;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector3.Distance(dest, player.transform.position) < 2.2f)
            return false;

        transform.position = dest;
        slideExcuse = 0.5f;
        return true;
    }

    static float GroundAt(Vector3 origin, float fallback)
    {
        RaycastHit[] found = Physics.RaycastAll(origin, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = fallback;
        foreach (RaycastHit hit in found)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<Enemy>() != null)
                continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                y = hit.point.y;
            }
        }
        return y;
    }

    bool StickToGround(ref Vector3 next)
    {
        if (!SampleGround(next, out float groundY))
            return false;

        // Under the hillside: put them back on top.
        if (next.y < groundY - 0.02f)
        {
            next.y = groundY + 0.05f;
            if (verticalVelocity < 0f)
                verticalVelocity = 0f;
            flying = false;
            return true;
        }

        if (verticalVelocity <= 0.05f && next.y <= groundY + 0.12f)
        {
            next.y = groundY;
            return true;
        }

        return false;
    }

    bool SampleGround(Vector3 at, out float groundY)
    {
        groundY = at.y;
        if (GroundMask == 0)
            return false;

        Vector3 origin = new Vector3(at.x, at.y + 40f, at.z);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, GroundMask, QueryTriggerInteraction.Ignore))
            return false;

        groundY = hit.point.y;
        return true;
    }

    bool GoesForPlayer()
    {
        if (down || PlayerGrowth.Instance == null || WaveDirector.Wave < PlayerGrowth.ClimbWave)
            return false;
        return WaveDirector.Wave - DebutWave >= 5;
    }

    float ClimbReach()
    {
        PlayerGrowth growth = PlayerGrowth.Instance;
        if (growth == null)
            return 1.2f;
        return growth.BodyRadius + 0.55f + Mathf.Min(1.6f, bodyScale * 0.14f);
    }

    void TryClimb()
    {
        PlayerGrowth growth = PlayerGrowth.Instance;
        if (growth == null)
            return;

        Vector3 flat = growth.transform.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > ClimbReach())
            return;
        // A dodge in progress is not climbed: the bot tries again when the window closes.
        if (CampaignMoves.Invulnerable || WallitHurt.Invulnerable)
            return;

        climbing = true;
        AttackTell.Climb(this);   // a grab, not a swing: it has no wind-up
        SetSolid(false);
        Climbers++;
        climbHeight = Random.Range(0.35f, Mathf.Max(0.8f, growth.BodyHeight * 0.35f));
        flying = false;
        growth.OnHit(this);
    }

    void TickClimb(float dt)
    {
        PlayerGrowth growth = PlayerGrowth.Instance;
        if (growth == null)
        {
            StopClimbing();
            return;
        }

        climbHeight += dt * 0.42f;
        climbHeight = Mathf.Min(climbHeight, growth.BodyHeight * 0.82f);
        Vector3 outDir = transform.position - growth.transform.position;
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 0.01f)
            outDir = growth.transform.forward;
        outDir.Normalize();
        float grip = growth.BodyRadius * 0.92f + Mathf.Min(1.1f, bodyScale * 0.06f);
        transform.position = growth.transform.position + outDir * grip + Vector3.up * climbHeight;
        transform.rotation = Quaternion.LookRotation(-outDir);
    }

    void StopClimbing()
    {
        if (!climbing)
            return;
        climbing = false;
        Climbers = Mathf.Max(0, Climbers - 1);
        SetSolid(true);
    }

    void SetSolid(bool solid) {
        Collider body = GetComponent<Collider>();
        if (body != null) {
            body.enabled = solid;
        }
    }

    void BeginRagdoll(float power, bool recover)
    {
        ragdoll = true;
        tumbleRecover = recover;
        tumbleLeft = recover ? 1.35f : CharacterRig.DeathTotal;
        flying = false;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(0.45f, bodyScale);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        Vector3 away = transform.position;
        if (PlayerGrowth.Instance != null)
            away -= PlayerGrowth.Instance.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Random.onUnitSphere;
        away.y = 0.65f;
        float force = 3f + power * 1.6f;
        body.linearVelocity = away.normalized * force;
        body.angularVelocity = Random.insideUnitSphere * (2.5f + power);
        if (!recover)
        {
            BotSound.Death(this);
        }
    }

    void TickRagdoll(float dt)
    {
        tumbleLeft -= dt;
        if (SampleGround(transform.position, out float groundY) && transform.position.y < groundY)
        {
            Vector3 parked = transform.position;
            parked.y = groundY + 0.05f;
            transform.position = parked;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && body.linearVelocity.y < 0f)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }
        }

        if (tumbleLeft > 0f)
            return;

        if (tumbleRecover && !down)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
                Destroy(body);
            ragdoll = false;
            tumbleRecover = false;
            return;
        }

        // The death clip has played out (CharacterRig.DeathTotal): now the body goes.
        if (rig != null)
            rig.Removed();
        Destroy(gameObject);
    }

    void ClearLock()
    {
        ZeldaStyleCamera cam = FindAnyObjectByType<ZeldaStyleCamera>();
        if (cam != null && cam.LockedOnGameObject == gameObject)
        {
            cam.LockedOnGameObject = null;
            cam.CamState = ZeldaStyleCamera.CamStates.Behind;
        }
    }

    void BuildHealthBar()
    {
        barRoot = new GameObject("Health").transform;
        // Not a child of the bot: its scale is non-uniform (shape * size) and a camera-facing child of a non-uniform,
        // yawed parent is sheared into black wedges and a bar far wider than the bot. FaceBar sizes it in world units.
        barRoot.position = transform.position + Vector3.up * 1.15f;

        // Shared materials: a Material made per bot was never freed (it grew every wave) and kept the bars from batching.
        Transform back = MakeBarQuad("Back", BarMaterial(-1));
        back.SetParent(barRoot, false);
        back.localScale = new Vector3(1f, 0.18f, 1f);

        barFill = MakeBarQuad("Fill", BarMaterial(BarSteps - 1));
        barFill.SetParent(barRoot, false);
        RefreshBar();
    }

    static Material launchTrailMat;
    const int BarSteps = 8;
    static Material barBackMat;
    static readonly Material[] barFillMats = new Material[BarSteps];

    // step -1 is the dark back, 0..BarSteps-1 the fill from red to green.
    static Material BarMaterial(int step)
    {
        Material m = step < 0 ? barBackMat : barFillMats[step];
        if (m != null)
            return m;
        Shader shader = SafeShader.Find("Unlit/Color");
        if (shader == null)
            shader = SafeShader.Find("Sprites/Default");
        if (shader == null)
            return null;
        m = new Material(shader);
        if (step < 0)
        {
            m.color = new Color(0.1f, 0.07f, 0.06f, 0.94f);
            barBackMat = m;
        }
        else
        {
            m.color = BarColor((step + 0.5f) / BarSteps);
            barFillMats[step] = m;
        }
        return m;
    }

    static Color BarColor(float remaining)
    {
        return remaining > 0.45f
            ? Color.Lerp(new Color(0.92f, 0.72f, 0.22f), new Color(0.45f, 0.82f, 0.38f), (remaining - 0.45f) / 0.55f)
            : Color.Lerp(new Color(0.85f, 0.16f, 0.12f), new Color(0.92f, 0.72f, 0.22f), remaining / 0.45f);
    }

    static Transform MakeBarQuad(string name, Material material)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Collider box = quad.GetComponent<Collider>();
        if (box != null)
            Destroy(box);
        Renderer renderer = quad.GetComponent<Renderer>();
        if (material != null && renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
        }
        return quad.transform;
    }

    void RefreshBar()
    {
        if (barFill == null)
            return;
        float remaining = Health01;
        barFill.localScale = new Vector3(0.92f * remaining, 0.12f, 1f);
        barFill.localPosition = new Vector3(-0.46f + barFill.localScale.x * 0.5f, 0f, -0.02f);
        Renderer renderer = barFill.GetComponent<Renderer>();
        Material step = BarMaterial(Mathf.Clamp(Mathf.FloorToInt(remaining * BarSteps), 0, BarSteps - 1));
        if (renderer != null && step != null && renderer.sharedMaterial != step)
            renderer.sharedMaterial = step;
    }

    const float BarCssPhone = 44f;
    const float BarCssDesktop = 72f;

    void FaceBar()
    {
        if (barRoot == null)
            return;
        if (down)
        {
            if (barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(false);
            return;
        }
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector3 anchor = transform.TransformPoint(new Vector3(0f, topY + 0.12f, 0f));
        Vector3 away = anchor - cam.transform.position;
        float depth = Vector3.Dot(away, cam.transform.forward);
        if (depth < 0.3f)
        {
            // Behind or on top of the camera: no bar (a bar there is a huge smear).
            if (barRoot.gameObject.activeSelf)
                barRoot.gameObject.SetActive(false);
            return;
        }
        if (!barRoot.gameObject.activeSelf)
            barRoot.gameObject.SetActive(true);
        // Sized in screen pixels: a phone bar is 44 CSS px, a desktop one 72, whatever the bot's size or distance.
        float cssWidth = UiFit.Touch ? BarCssPhone : BarCssDesktop;
        float pixels = cssWidth * UiFit.PixelRatio;
        float worldPerPixel = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth / Mathf.Max(1f, Screen.height);
        float barWorld = pixels * worldPerPixel;
        // Under its bot, inside the screen: pull the anchor back in along the camera's own axes.
        float halfW = pixels * 0.5f / Mathf.Max(1f, Screen.width);
        float halfH = pixels * 0.06f / Mathf.Max(1f, Screen.height);
        Vector3 vp = cam.WorldToViewportPoint(anchor);
        float cx = Mathf.Clamp(vp.x, halfW + 0.01f, 1f - halfW - 0.01f);
        float cy = Mathf.Clamp(vp.y, halfH + 0.12f, 1f - halfH - 0.02f);   // above the touch pads' band, below the top bars
        if (cx != vp.x || cy != vp.y)
            anchor = cam.ViewportToWorldPoint(new Vector3(cx, cy, depth));
        barRoot.position = anchor;
        barRoot.localScale = Vector3.one * barWorld;
        away = anchor - cam.transform.position;
        BarProbe.Note(cam, barRoot.position, pixels / UiFit.PixelRatio);
        barRoot.rotation = Quaternion.LookRotation(away, cam.transform.up);
    }

    // The body, from the rig's pose (CharacterRig.cs): the walk cycle at ground speed, the wind-up and the blow, the flinch, a
    // boss's rear-up tell and stagger, the death clip, the jump jets and the head turn, all on the model's parts. Yo Bots stomp on
    // legs and swipe a claw, Spider Bots walk on eight and stab with a front leg.
    void AnimateModel(float dt)
    {
        Vector3 moved = transform.position - lastPos;
        lastPos = transform.position;
        moved.y = 0f;
        if (moved.magnitude > 1.5f) {
            moved = Vector3.zero;   // a blink, a climb or a frame skipped here: not a stride (it flicked the legs and puffed a footstep)
        }
        float scale = Mathf.Max(0.2f, transform.lossyScale.y);
        float speed = dt > 0f ? moved.magnitude / dt / scale : 0f;
        // A planted bot (held for a tell, frozen, shorted, flying, climbing, tumbling) does not walk; a shoved one slides.
        bool planted = down || frozenLeft > 0f || shortLeft > 0f || holdLeft > 0f || ragdoll || flying || climbing || launched;
        slideExcuse = Mathf.Max(0f, slideExcuse - dt);
        bool slid = planted || knock.sqrMagnitude > 0.01f || knockAir > 0f || slideExcuse > 0f;
        if (planted)
            speed = 0f;
        if (rig == null)
            return;
        rig.Tick(dt, speed, slid);

        float walk = rig.Walk;
        float step = rig.Step;
        bool stepSign = step > 0f;
        if (stepSign != lastStepSign && walk > 0.3f && bodyScale > 2.5f && grounded)
            Fx.Footstep(transform.position, bodyScale * 0.6f);
        if (stepSign != lastStepSign && walk > 0.3f && grounded)
            BotSound.Step(this, Mathf.Clamp(0.5f + bodyScale * 0.25f, 0.6f, 1.6f));
        lastStepSign = stepSign;

        float wind = rig.Wind;
        float strike = rig.Strike;
        float rear = rig.Rear;
        float flinchPose = rig.FlinchPose;
        float stagger = rig.Stagger;
        float death = rig.Death;
        // The blow: raise through the wind-up, whip through the strike.
        float attack = Mathf.Max(wind, strike);

        if (model != null)
        {
            float sway = Mathf.Sin(rig.Cycle) * (spider ? 1.5f : 3f) * walk;
            float bounce = Mathf.Abs(Mathf.Cos(rig.Cycle)) * (spider ? 0.015f : 0.025f) * walk;
            float tremble = Mathf.Sin(Time.time * 42f + jukePhase) * rear;
            // Pitch: lean into the stride, back on the wind-up, forward on the blow, back on a hit, rear up on a boss's tell,
            // buckle on a stagger, fall back as it dies.
            float pitch = walk * 4f - wind * (spider ? 9f : 12f) + strike * (spider ? 10f : 14f) - flinchPose * 16f
                          - rear * (spider ? 24f : 16f) + stagger * Mathf.Sin(Time.time * 11f) * 10f - death * 38f;
            float roll = sway + tremble * 2.2f + stagger * Mathf.Sin(Time.time * 8f + 1f) * 9f + flinchPose * Mathf.Sign(Mathf.Sin(jukePhase * 7f)) * 5f + death * 14f;
            model.localRotation = modelRest * Quaternion.Euler(pitch, 0f, roll);
            if (spinLeft > 0f)
            {
                spinLeft -= dt;
                spinAngle += dt * 720f;
                model.localRotation = Quaternion.Euler(spinAngle * 0.35f, spinAngle, 0f) * model.localRotation;
            }
            float crouch = wind * 0.03f + rear * 0.05f + stagger * 0.04f + death * 0.07f;
            model.localPosition = new Vector3(0f, bounce - crouch, strike * 0.1f - flinchPose * 0.12f - wind * 0.04f);
            float fade = 1f - rig.Fade;
            model.localScale = modelScaleRest * Mathf.Max(0.02f, fade);
        }

        if (spider)
            AnimateSpiderLegs(walk, attack, wind, rear, stagger, death, flinchPose);
        else
            AnimateYoBotLimbs(walk, step, attack, strike, rear, stagger, death, flinchPose);

        if (head != null)
        {
            float bob = Mathf.Sin(rig.Cycle * 2f) * 3f * walk + attack * (spider ? -12f : 10f) + rear * 16f - flinchPose * 22f
                        + stagger * Mathf.Sin(Time.time * 9f) * 18f + death * 30f;
            float look = Mathf.Sin(Time.time * 0.9f + jukePhase * 2f) * (spider ? 14f : 20f) * (1f - Mathf.Max(walk, attack)) * (1f - death)
                         + flinchPose * 12f;
            head.localRotation = headRestRot * Quaternion.Euler(bob, look, 0f);
        }
    }

    void AnimateYoBotLimbs(float walk, float step, float attack, float strike, float rear, float stagger, float death, float flinchPose)
    {
        // Legs tuck under the jets in a hop, plant wide on a tell, buckle on a stagger and fold under as it falls.
        // Four-leg Gen models trot (FL+BR, FR+BL).
        float tuck = flying ? -28f : 0f;
        float brace = rear * 10f + stagger * Mathf.Sin(Time.time * 12f) * 14f + flinchPose * 8f;
        float fold = death * 62f;
        float frontSwing = step * 30f * walk;
        float backSwing = -step * 30f * walk;
        if (legL != null)
            legL.localRotation = legLRest * Quaternion.Euler(tuck + frontSwing + brace + fold, 0f, 0f);
        if (legR != null)
            legR.localRotation = legRRest * Quaternion.Euler(tuck - frontSwing + brace + fold, 0f, 0f);
        if (legBL != null)
            legBL.localRotation = legBLRest * Quaternion.Euler(tuck + backSwing - brace + fold, 0f, 0f);
        if (legBR != null)
            legBR.localRotation = legBRRest * Quaternion.Euler(tuck - backSwing - brace + fold, 0f, 0f);

        // The sweep peaks mid-blow: the claw comes down from the wind-up, across, and home.
        float sweepK = Mathf.Sin(Mathf.Clamp01(strike) * Mathf.PI);
        AnimateClaw(clawL, clawLRest, -step, walk, swipeLeft ? attack : 0f, swipeLeft ? sweepK : 0f, rear, stagger, death, flinchPose);
        AnimateClaw(clawR, clawRRest, step, walk, swipeLeft ? 0f : attack, swipeLeft ? 0f : sweepK, rear, stagger, death, flinchPose);
    }

    void AnimateClaw(Transform claw, Quaternion rest, float step, float walk, float raiseAmount, float strike, float rear, float stagger, float death, float flinchPose)
    {
        if (claw == null)
            return;
        float side = Mathf.Sign(claw.localPosition.x + 0.0001f);
        // Arms swing against the legs, spread for balance in a hop and grab while climbing.
        float swing = step * 22f * walk;
        float spread = flying ? 38f : climbing ? 25f + Mathf.Sin(Time.time * 14f) * 15f : 6f;
        // The wind-up raises the claw; the blow sweeps it across the body.
        float raise = -95f * raiseAmount;
        float sweep = -side * 55f * strike;
        // A boss's tell throws both arms overhead and shakes them; a stagger flails them; a hit flings them back; death drops them.
        raise += -125f * rear + stagger * Mathf.Sin(Time.time * 13f + side) * 40f + flinchPose * 30f + death * 55f;
        spread += rear * 22f * Mathf.Sin(Time.time * 42f) + death * 60f + stagger * 25f;
        claw.localRotation = rest * Quaternion.Euler(swing + raise, sweep, side * spread);
    }

    void AnimateSpiderLegs(float walk, float attack, float wind, float rear, float stagger, float death, float flinchPose)
    {
        for (int i = 0; i < spiderLegs.Length; i++)
        {
            Transform leg = spiderLegs[i];
            if (leg == null)
                continue;
            int pair = i % 4;
            // Read the side from where the leg sits: the FBX import mirrors X.
            float side = Mathf.Sign(leg.localPosition.x + 0.0001f);
            // Alternating fours: L1 R2 L3 R4 step together, then the other four.
            int group = (pair + (side > 0f ? 1 : 0)) % 2;
            float phase = rig.Cycle + group * Mathf.PI;
            float swing = Mathf.Sin(phase) * 16f * walk;
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * 16f * walk;
            if (flying)
            {
                swing = 0f;
                lift = -18f;
            }
            else if (climbing)
            {
                lift = 10f + Mathf.Sin(Time.time * 12f + i) * 10f;
            }
            if (pair == 0)
            {
                bool stabbing = side < 0f ? swipeLeft : !swipeLeft;
                if (stabbing)
                {
                    lift += 55f * attack;
                    swing += 30f * attack;
                }
                // A boss's tell rears both front legs high and shakes them.
                lift += rear * (70f + 8f * Mathf.Sin(Time.time * 42f));
                swing += rear * 18f;
            }
            else
            {
                // The back legs dig in under the rear-up, buckle on a stagger.
                lift -= rear * 6f;
                lift += stagger * (6f + 14f * Mathf.Sin(Time.time * 11f + i));
            }
            if (pair == 0)
                lift += stagger * (30f + 20f * Mathf.Sin(Time.time * 10f + i)) + flinchPose * 18f;
            // Death: every leg curls up into the body.
            lift += death * 70f;
            swing *= 1f - death;
            // Positive lift raises the foot on either side; positive swing brings it forward.
            leg.localRotation = spiderLegRest[i] * Quaternion.Euler(0f, -side * swing, side * lift);
        }
    }

    // White pop on every hit, gold on crits, fading fast.
    void TickFlash(float dt)
    {
        if (skins == null)
            return;
        float before = flash;
        if (flash > 0f)
            flash = Mathf.MoveTowards(flash, 0f, dt * 5f);
        // Status shows on the bird itself: embers for fire, ice for frost, shimmer for gold.
        float glowAmount = 0f;
        Color glowTint = Color.white;
        if (shortLeft > 0f)
        {
            glowTint = new Color(0.62f, 0.85f, 1f);
            glowAmount = Mathf.Sin(Time.time * 40f + jukePhase) > 0f ? 0.6f : 0.25f;
        }
        else if (burnLeft > 0f)
        {
            glowTint = new Color(1f, 0.42f, 0.1f);
            glowAmount = 0.35f + 0.15f * Mathf.Sin(Time.time * 9f + jukePhase);
        }
        else if (frozenLeft > 0f || slowLeft > 0f)
        {
            glowTint = new Color(0.55f, 0.8f, 1f);
            glowAmount = frozenLeft > 0f ? 0.55f : 0.22f;
        }
        else if (Golden)
        {
            glowTint = new Color(1f, 0.82f, 0.3f);
            glowAmount = 0.18f + 0.1f * Mathf.Sin(Time.time * 4f);
        }
        if (flash < glowAmount)
        {
            statusGlow = true;
            ApplyGlow(glowTint, glowAmount);
            return;
        }
        if (before <= 0f && flash <= 0f && !statusGlow)
            return;
        statusGlow = false;
        if (flash <= 0f)
        {
            // Drop the block so the bird batches with the flock again.
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] != null)
                    skins[i].SetPropertyBlock(null);
            }
            return;
        }
        ApplyGlow(Color.white, flash * 0.85f);
    }

    void ApplyGlow(Color tint, float amount)
    {
        if (flashBlock == null)
            flashBlock = new MaterialPropertyBlock();
        flashBlock.SetColor(FlashId, new Color(tint.r, tint.g, tint.b, amount));
        for (int i = 0; i < skins.Length; i++)
        {
            if (skins[i] != null)
                skins[i].SetPropertyBlock(flashBlock);
        }
    }

    // A named character's own authored body (CharacterKit): replaces the shared bot model, keeping the same part names so the walk,
    // the tell and the strike animate it. The hit capsule is refitted from the new body.
    string authoredId;
    void SetAuthored(string id)
    {
        authoredId = id;
        if (model != null)
        {
            model.name = "Bot (old)";
            model.gameObject.SetActive(false);
            Destroy(model.gameObject);
            model = null;
            BuildModel();
        }
    }

    bool BuildModel()
    {
        BotKind look = drone ? BotKind.YoBot : Kind;
        bool authoredBody = !drone && !string.IsNullOrEmpty(authoredId) && CharacterKit.Has(authoredId);
        GameObject made = authoredBody ? CharacterKit.Make(authoredId) : null;
        authoredBody = made != null;   // no authored rig: the shared bot stands in
        if (made == null)
            made = BotArt.Make(look, modelTint, modelTintAmount);
        if (made == null)
            return false;
        made.transform.SetParent(transform, false);
        made.transform.localPosition = Vector3.zero;
        model = made.transform;
        spider = authoredBody ? CharacterKit.IsSpider(authoredId) : BotRoster.IsSpider(look);
        BindParts(model, spider);
        // The head must lead (Gen models mark their front instead: the Yo Bot's lid sits behind its middle).
        // If the export axes ever flip, turn the model around.
        Transform lead = FindPart(model, "Front");
        if (lead == null)
            lead = head;
        if (lead != null && Vector3.Dot(lead.position - transform.position, transform.forward) < 0f)
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
        modelRest = model.localRotation;
        modelScaleRest = model.localScale;
        if (rig == null)
            rig = new CharacterRig(GetInstanceID(), gameObject.name, spider);
        else
            rig.Label(gameObject.name);
        if (clawL != null) clawLRest = clawL.localRotation;
        if (clawR != null) clawRRest = clawR.localRotation;
        if (legL != null) legLRest = legL.localRotation;
        if (legR != null) legRRest = legR.localRotation;
        if (legBL != null) legBLRest = legBL.localRotation;
        if (legBR != null) legBRRest = legBR.localRotation;
        if (head != null)
            headRestRot = head.localRotation;

        // Fit the hit capsule to the body, head and legs, ignoring the swinging claws.
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshRenderer renderer in made.GetComponentsInChildren<MeshRenderer>())
        {
            string part = renderer.transform.name;
            if (PartIsClaw(part))
                continue;
            Bounds world = renderer.bounds;
            Vector3 min = transform.InverseTransformPoint(world.min);
            Vector3 max = transform.InverseTransformPoint(world.max);
            Bounds local = new Bounds((min + max) * 0.5f, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)));
            if (!any)
            {
                bounds = local;
                any = true;
            }
            else
                bounds.Encapsulate(local);
        }
        if (any)
        {
            topY = bounds.max.y;
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.radius = Mathf.Max(0.12f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.42f);
                capsule.height = Mathf.Max(capsule.radius * 2f, bounds.size.y * 0.92f);
                capsule.center = new Vector3(0f, bounds.center.y, bounds.center.z * 0.5f);
            }
        }
        lastPos = transform.position;
        skins = made.GetComponentsInChildren<Renderer>();
        modelled = true;
        return true;
    }

    // Stand-in names (Body/Head/ClawL…) plus Gen robot template roles (body/arm_l/leg_fl/leg1…).
    void BindParts(Transform root, bool isSpider)
    {
        head = FindPart(root, "Head", "head");
        if (isSpider)
        {
            // Gen spider: leg1..leg6. Game stand-in: LegL1..4 / LegR1..4 (1 = front pair that stabs).
            spiderLegs[0] = FindPart(root, "LegL1", "leg1");
            spiderLegs[1] = FindPart(root, "LegL2", "leg3");
            spiderLegs[2] = FindPart(root, "LegL3", "leg5");
            spiderLegs[3] = FindPart(root, "LegL4");
            spiderLegs[4] = FindPart(root, "LegR1", "leg2");
            spiderLegs[5] = FindPart(root, "LegR2", "leg4");
            spiderLegs[6] = FindPart(root, "LegR3", "leg6");
            spiderLegs[7] = FindPart(root, "LegR4");
            for (int i = 0; i < spiderLegs.Length; i++)
            {
                if (spiderLegs[i] != null)
                    spiderLegRest[i] = spiderLegs[i].localRotation;
            }
            if (head == null)
                head = FindPart(root, "Body", "body");
            return;
        }
        clawL = FindPart(root, "ClawL", "arm_l", "Arm_L", "ArmL");
        clawR = FindPart(root, "ClawR", "arm_r", "Arm_R", "ArmR");
        legL = FindPart(root, "LegL", "LegFL", "leg_fl", "Leg_FL");
        legR = FindPart(root, "LegR", "LegFR", "leg_fr", "Leg_FR");
        legBL = FindPart(root, "LegBL", "leg_bl", "Leg_BL");
        legBR = FindPart(root, "LegBR", "leg_br", "Leg_BR");
    }

    static bool PartIsClaw(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        string n = name.ToLowerInvariant();
        return n.StartsWith("claw") || n.StartsWith("arm_") || n == "arm_l" || n == "arm_r" || n == "arml" || n == "armr";
    }

    static Transform FindPart(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
        {
            string want = names[n];
            if (string.IsNullOrEmpty(want))
                continue;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, want, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }
        }
        return null;
    }
}
