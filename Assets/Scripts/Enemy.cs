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
    public string Title { get; private set; }
    public float Health01 { get { return Mathf.Clamp01(1f - taken / Mathf.Max(1f, HitsToDrop)); } }
    public float BodyScale { get { return bodyScale; } }
    public bool Burning { get { return burnLeft > 0f; } }

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
    public bool Climbing { get { return climbing; } }
    public static int Climbers { get; private set; }

    Transform clawL;
    Transform clawR;
    Transform head;
    Transform legL;
    Transform legR;
    Transform model;
    // Spider Bots: LegL1..LegL4 then LegR1..LegR4, front pair first.
    readonly Transform[] spiderLegs = new Transform[8];
    readonly Quaternion[] spiderLegRest = new Quaternion[8];
    bool spider;
    bool swipeLeft;
    Quaternion clawLRest = Quaternion.identity;
    Quaternion clawRRest = Quaternion.identity;
    Quaternion legLRest = Quaternion.identity;
    Quaternion legRRest = Quaternion.identity;
    Quaternion headRestRot = Quaternion.identity;
    Quaternion modelRest = Quaternion.identity;
    bool modelled;
    float stridePhase;
    bool lastStepSign;
    float peckAnim;
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

    void BuildCrest()
    {
        if (crestColor.a <= 0f)
            return;
        GameObject gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(gem.GetComponent<Collider>());
        gem.name = "Crest";
        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = new Vector3(0f, topY + 0.35f, 0f);
        gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
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

        if (Boss && !down)
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

    void OnEnable()
    {
        Roster.Add(this);
    }

    void OnDisable()
    {
        Roster.Remove(this);
    }

    void OnDestroy()
    {
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
        // Every Yo Bot gets its own paint job; the bigger kinds keep their authored colors.
        if (def.Kind == BotKind.YoBot)
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
        Title = TraitTitle(trait);
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

        if (ragdoll)
        {
            TickRagdoll(dt);
            return;
        }

        if (climbing)
        {
            TickClimb(dt);
            return;
        }

        if (!TickStatus(dt))
            return;

        bool swarm = GoesForPlayer();
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

        if (swarm)
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
        if (!down && musician != null && musician.Alive && (wall != null || atMusician))
        {
            peckIn -= dt;
            if (peckIn <= 0f)
            {
                // A claw swipe from a Yo Bot, a stab of a front leg from a Spider Bot.
                peckIn = 1.05f;
                peckAnim = 1f;
                swipeLeft = !swipeLeft;
                if (wall != null && wall.Alive)
                    wall.Stop(this);
                else if (atMusician)
                    musician.Hurt(Boss ? 3 : Elite ? 2 : 1, this);
                verticalVelocity = Mathf.Max(verticalVelocity, 1.5f);
            }
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

    void ProbeAhead(Vector3 toTarget, float distance, out DefencePiece wall, out bool sceneryBlock)
    {
        wall = null;
        sceneryBlock = false;
        if (toTarget.sqrMagnitude < 0.04f)
            return;

        Vector3 wish = toTarget.normalized;
        float probe = 0.65f + bodyScale * 0.24f;
        Vector3 origin = transform.position + Vector3.up * 0.42f;
        RaycastHit[] ahead = Physics.RaycastAll(origin, wish, probe, ~0, QueryTriggerInteraction.Ignore);
        float best = probe + 1f;
        foreach (RaycastHit hit in ahead)
        {
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
        if (velocity.y > 0f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
            grounded = false;
        }
        flying = false;
        knockAir = Mathf.Max(knockAir, Boss ? 0f : airSeconds);
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

        taken += amount;
        RefreshBar();
        Vector3 mark = transform.position + Vector3.up * (0.7f * Mathf.Max(1f, bodyScale));
        if (kind != DamageKind.Burn)
        {
            if (!Boss)
                verticalVelocity = Mathf.Max(verticalVelocity, 2.2f);
            Fx.Hit(mark, Mathf.Sqrt(Mathf.Max(1f, bodyScale)), crit);
            if (kind == DamageKind.Arrow || kind == DamageKind.Plain)
            {
                GameAudio.Play("hit", crit ? 0.9f : 0.6f, crit ? 1.25f : Random.Range(0.92f, 1.08f));
                UpgradeLook.MarkHit();
            }
            else if (kind == DamageKind.Bat)
            {
                // BatEquipment plays the crack once per swing, not once per bird.
                UpgradeLook.MarkHit();
            }
        }
        if (kind != DamageKind.Burn)
            flash = crit ? 1f : Mathf.Max(flash, 0.7f);
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
        flying = false;
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
            BeginRagdoll(scale, true);
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
    }

    void CelebrateDeath() {
        bool boss = DebutWave >= 10 || Boss || Golden;
        Fx.Death(transform.position, transform.lossyScale.y, Paint, boss || Elite);
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

        climbing = true;
        SetSolid(false);
        Climbers++;
        climbHeight = Random.Range(0.35f, Mathf.Max(0.8f, growth.BodyHeight * 0.35f));
        flying = false;
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
        tumbleLeft = recover ? 1.35f : 3.2f;
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
            GameAudio.Play("death", 0.55f);
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
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, 1.15f, 0f);

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Transform back = MakeBarQuad("Back", shader, new Color(0.1f, 0.07f, 0.06f, 0.94f));
        back.SetParent(barRoot, false);
        back.localScale = new Vector3(1f, 0.18f, 1f);

        barFill = MakeBarQuad("Fill", shader, new Color(0.45f, 0.82f, 0.38f));
        barFill.SetParent(barRoot, false);
        RefreshBar();
    }

    static Transform MakeBarQuad(string name, Shader shader, Color color)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Collider box = quad.GetComponent<Collider>();
        if (box != null)
            Destroy(box);
        Renderer renderer = quad.GetComponent<Renderer>();
        if (shader != null && renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = color;
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
        if (renderer != null && renderer.sharedMaterial != null)
            renderer.sharedMaterial.color = remaining > 0.45f
                ? Color.Lerp(new Color(0.92f, 0.72f, 0.22f), new Color(0.45f, 0.82f, 0.38f), (remaining - 0.45f) / 0.55f)
                : Color.Lerp(new Color(0.85f, 0.16f, 0.12f), new Color(0.92f, 0.72f, 0.22f), remaining / 0.45f);
    }

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
        Vector3 size = transform.lossyScale;
        float wide = Mathf.Max(0.01f, size.x);
        float tall = Mathf.Max(0.01f, size.y);
        float deep = Mathf.Max(0.01f, size.z);
        float barWorld = Mathf.Clamp(Mathf.Max(wide, tall) * 0.55f, 1.6f, 8f);
        barRoot.localScale = new Vector3(barWorld / wide, barWorld / tall, barWorld / deep);
        barRoot.localPosition = new Vector3(0f, topY + 0.12f, 0f);
        Vector3 away = barRoot.position - cam.transform.position;
        if (away.sqrMagnitude < 0.0001f)
            return;
        barRoot.rotation = Quaternion.LookRotation(away, cam.transform.up);
    }

    // Walk cycle, jump jets, head turn and the attack, all from code on the model's parts:
    // Yo Bots stomp on two legs and swipe a claw, Spider Bots walk on eight and stab with a front leg.
    void AnimateModel(float dt)
    {
        Vector3 moved = transform.position - lastPos;
        lastPos = transform.position;
        moved.y = 0f;
        float scale = Mathf.Max(0.2f, transform.lossyScale.y);
        float speed = dt > 0f ? moved.magnitude / dt / scale : 0f;
        float walk = Mathf.Clamp01(speed / 2.5f);
        if (down || frozenLeft > 0f)
            walk = 0f;
        stridePhase += dt * (spider ? 6f + speed * 4f : 5f + speed * 3.2f);
        float step = Mathf.Sin(stridePhase);
        bool stepSign = step > 0f;
        if (stepSign != lastStepSign && walk > 0.3f && bodyScale > 2.5f && grounded)
            Fx.Footstep(transform.position, bodyScale * 0.6f);
        lastStepSign = stepSign;

        peckAnim = Mathf.MoveTowards(peckAnim, 0f, dt * 4f);
        // Wind up slowly, then strike fast.
        float strike = peckAnim > 0.35f ? (1f - peckAnim) / 0.65f : peckAnim / 0.35f;

        if (model != null)
        {
            float sway = Mathf.Sin(stridePhase) * (spider ? 1.5f : 3f) * walk;
            float bounce = Mathf.Abs(Mathf.Cos(stridePhase)) * (spider ? 0.015f : 0.025f) * walk;
            model.localRotation = modelRest * Quaternion.Euler(walk * 4f + (spider ? 0f : strike * 8f), 0f, sway);
            model.localPosition = new Vector3(0f, bounce, 0f);
        }

        if (spider)
            AnimateSpiderLegs(walk, strike);
        else
            AnimateYoBotLimbs(walk, step, strike);

        if (head != null)
        {
            float bob = Mathf.Sin(stridePhase * 2f) * 3f * walk + strike * (spider ? -12f : 10f);
            float look = Mathf.Sin(Time.time * 0.9f + jukePhase * 2f) * (spider ? 14f : 20f) * (1f - walk);
            head.localRotation = headRestRot * Quaternion.Euler(bob, look, 0f);
        }
    }

    void AnimateYoBotLimbs(float walk, float step, float strike)
    {
        // Legs tuck under the jets in a hop.
        float tuck = flying ? -28f : 0f;
        if (legL != null)
            legL.localRotation = legLRest * Quaternion.Euler(tuck + step * 30f * walk, 0f, 0f);
        if (legR != null)
            legR.localRotation = legRRest * Quaternion.Euler(tuck - step * 30f * walk, 0f, 0f);

        AnimateClaw(clawL, clawLRest, -step, walk, swipeLeft ? strike : 0f);
        AnimateClaw(clawR, clawRRest, step, walk, swipeLeft ? 0f : strike);
    }

    void AnimateClaw(Transform claw, Quaternion rest, float step, float walk, float swipe)
    {
        if (claw == null)
            return;
        float side = Mathf.Sign(claw.localPosition.x + 0.0001f);
        // Arms swing against the legs, spread for balance in a hop and grab while climbing.
        float swing = step * 22f * walk;
        float spread = flying ? 38f : climbing ? 25f + Mathf.Sin(Time.time * 14f) * 15f : 6f;
        // The swipe raises the claw forward and sweeps it across the body.
        float raise = -95f * swipe;
        float sweep = -side * 55f * swipe;
        claw.localRotation = rest * Quaternion.Euler(swing + raise, sweep, side * spread);
    }

    void AnimateSpiderLegs(float walk, float strike)
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
            float phase = stridePhase + group * Mathf.PI;
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
                    lift += 55f * strike;
                    swing += 30f * strike;
                }
            }
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
            flash = Mathf.MoveTowards(flash, 0f, dt * 7f);
        // Status shows on the bird itself: embers for fire, ice for frost, shimmer for gold.
        float glowAmount = 0f;
        Color glowTint = Color.white;
        if (burnLeft > 0f)
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

    bool BuildModel()
    {
        BotKind look = drone ? BotKind.YoBot : Kind;
        GameObject made = BotArt.Make(look, modelTint, modelTintAmount);
        if (made == null)
            return false;
        made.transform.SetParent(transform, false);
        made.transform.localPosition = Vector3.zero;
        model = made.transform;
        spider = BotRoster.IsSpider(look);
        head = FindPart(model, "Head");
        clawL = FindPart(model, "ClawL");
        clawR = FindPart(model, "ClawR");
        legL = FindPart(model, "LegL");
        legR = FindPart(model, "LegR");
        for (int i = 0; i < spiderLegs.Length; i++)
        {
            spiderLegs[i] = FindPart(model, (i < 4 ? "LegL" : "LegR") + (i % 4 + 1));
            if (spiderLegs[i] != null)
                spiderLegRest[i] = spiderLegs[i].localRotation;
        }
        // The head must lead. If the export axes ever flip, turn the model around.
        if (head != null && Vector3.Dot(head.position - transform.position, transform.forward) < 0f)
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
        modelRest = model.localRotation;
        if (clawL != null) clawLRest = clawL.localRotation;
        if (clawR != null) clawRRest = clawR.localRotation;
        if (legL != null) legLRest = legL.localRotation;
        if (legR != null) legRRest = legR.localRotation;
        if (head != null)
            headRestRot = head.localRotation;

        // Fit the hit capsule to the body, head and legs, ignoring the swinging claws.
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshRenderer renderer in made.GetComponentsInChildren<MeshRenderer>())
        {
            string part = renderer.transform.name;
            if (part.StartsWith("Claw"))
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

    static Transform FindPart(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }
}
