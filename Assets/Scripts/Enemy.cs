using System.Collections.Generic;
using UnityEngine;

public enum TurkeyTrait
{
    None,
    Sprinter,
    Hopper,
    Bruiser,
    Juker,
    Glider,
    Splitter,
    Blinker,
    Gobbler,
    Charger,
    Swarmer,
    Titan
}

// A turkey that waddles, chases the player, attempts short flapping flights,
// and drops after two hits.
public class Enemy : MonoBehaviour
{
    public int HitsToDrop = 1;
    public FowlKind Kind { get; private set; }
    public int DebutWave { get; private set; }
    public bool IsGobbler { get; private set; }
    public bool Elite { get; private set; }
    public bool Golden { get; private set; }
    public bool Boss { get; private set; }
    public string Title { get; private set; }
    public float Health01 { get { return Mathf.Clamp01(1f - taken / Mathf.Max(1f, HitsToDrop)); } }
    public float BodyScale { get { return bodyScale; } }
    public bool Burning { get { return burnLeft > 0f; } }

    // Every bird on the hill, so effects do not have to search the scene.
    public static readonly List<Enemy> Roster = new List<Enemy>();

    [Header("Movement")]
    public float walkSpeed = 1.15f;
    public float chaseSpeed = 2.4f;
    public float chaseRange = 22f;
    public float stopDistance = 1.15f;

    [Header("Flight")]
    public float jumpVelocity = 4.2f;
    public float flightGravity = 7f;
    public float normalGravity = 26f;
    public float maxFlightHeight = 1.8f;
    public float flightDuration = 1.5f;
    public float minFlightInterval = 1.5f;
    public float maxFlightInterval = 3.5f;

    [Header("Grounding")]
    [Tooltip("Layers the ground raycast can hit. Turkeys stand on Ground.")]
    public LayerMask groundLayers = 1 << 3;
    public float groundRayDistance = 10f;
    public float groundRayStartHeight = 0.5f;

    public bool Alive { get { return !down; } }
    public bool Climbing { get { return climbing; } }
    public static int Climbers { get; private set; }

    Transform wingL;
    Transform wingR;
    Transform head;
    Transform legL;
    Transform legR;
    Transform model;
    Vector3 headRest;
    Quaternion wingLRest = Quaternion.identity;
    Quaternion wingRRest = Quaternion.identity;
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
    bool hatchling;
    float bodyScale = 1f;
    float jukePhase;
    float blinkIn = 2.5f;
    Color bodyTint = Color.white;
    Color wingTint = Color.white;
    Color headTint = Color.white;
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
        if (transform.Find("Turkey") == null)
            BuildTurkey();
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
        // Keep the gem a readable size in the world no matter how big the bird is.
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
                UpgradeLook.Say("The Golden Gobbler got away", 2f, 120f);
                UpgradeLook.Feathers(transform.position, 10, true);
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

    public void ApplyFowl(FowlDef def)
    {
        Kind = def.Kind;
        DebutWave = def.Kind == FowlKind.Turkey ? 0 : def.UnlockWave;
        HitsToDrop = Mathf.Max(1, Mathf.RoundToInt(def.Hits * Tuning.HealthScale(WaveDirector.Wave)));
        Title = def.Name;
        walkSpeed = def.Walk * Random.Range(0.86f, 1.18f) * 1.5f;
        chaseSpeed = def.Chase * Random.Range(0.9f, 1.16f) * 1.5f;
        jumpVelocity = def.Jump * Random.Range(0.9f, 1.12f);
        maxFlightHeight = def.FlightHeight;
        minFlightInterval = Random.Range(0.9f, 2.2f);
        maxFlightInterval = minFlightInterval + Random.Range(0.5f, 1.8f);
        juker = def.Kind == FowlKind.Turkey ? Random.value < 0.55f : def.Weave && Random.value < 0.7f;
        charger = def.Charge;
        if (def.Kind == FowlKind.Turkey)
            DyeTint(RandomTurkeyTint());
        else
            DyeTint(def.Tint);
        if (def.Kind == FowlKind.Turkey)
        {
            modelTint = bodyTint;
            modelTintAmount = 0.28f;
        }
        bodyScale = def.Scale;
        // Blender birds carry their own proportions, so they scale evenly.
        shape = BirdArt.Has(def.Kind) ? Vector3.one : new Vector3(def.Wide, def.Tall, def.Deep);
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

    // A turkey with a trait. Tougher, worth more feathers, and marked with a gem over its head.
    public void MakeElite(TurkeyTrait trait)
    {
        int baseHealth = HitsToDrop;
        float before = bodyScale;
        bodyScale = 1f;
        ApplyTrait(trait);
        bodyScale = before * bodyScale;
        transform.localScale = shape * bodyScale;
        float toughness = trait == TurkeyTrait.Titan ? 6f : trait == TurkeyTrait.Bruiser ? 4f : trait == TurkeyTrait.Swarmer ? 0.6f : 2.5f;
        HitsToDrop = Mathf.Max(1, Mathf.RoundToInt(baseHealth * toughness));
        IsGobbler = false;
        Elite = true;
        Title = trait.ToString();
        gameObject.name = Title;
        crestColor = EliteColor(trait);
        modelTint = crestColor;
        modelTintAmount = 0.5f;
    }

    public void MakeGolden()
    {
        Golden = true;
        Title = "Golden Gobbler";
        gameObject.name = Title;
        HitsToDrop = Mathf.RoundToInt((10f + WaveDirector.Wave * 3f) * Tuning.HealthScale(WaveDirector.Wave));
        bodyTint = new Color(1.9f, 1.5f, 0.35f);
        wingTint = new Color(1.8f, 1.35f, 0.3f);
        headTint = new Color(1.9f, 1.6f, 0.5f);
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

    static Color EliteColor(TurkeyTrait trait)
    {
        switch (trait)
        {
            case TurkeyTrait.Sprinter: return new Color(1f, 0.95f, 0.3f);
            case TurkeyTrait.Bruiser: return new Color(1f, 0.35f, 0.25f);
            case TurkeyTrait.Splitter: return new Color(1f, 0.6f, 0.2f);
            case TurkeyTrait.Blinker: return new Color(0.85f, 0.4f, 1f);
            case TurkeyTrait.Charger: return new Color(1f, 0.25f, 0.15f);
            case TurkeyTrait.Glider: return new Color(0.4f, 0.75f, 1f);
            case TurkeyTrait.Titan: return new Color(0.55f, 0.3f, 0.75f);
            case TurkeyTrait.Swarmer: return new Color(1f, 0.85f, 0.4f);
            default: return new Color(0.5f, 1f, 0.5f);
        }
    }

    static Color RandomTurkeyTint()
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

    public void Apply(TurkeyTrait primary, TurkeyTrait secondary)
    {
        bodyScale = 1f;
        ApplyTrait(primary);
        if (secondary != TurkeyTrait.None && secondary != primary)
            ApplyTrait(secondary);
        transform.localScale = Vector3.one * bodyScale;
        gameObject.name = TraitName(primary, secondary);
    }

    public void ApplyHatchling()
    {
        hatchling = true;
        splitter = false;
        HitsToDrop = 1;
        walkSpeed = 1.9f;
        chaseSpeed = 2.8f;
        jumpVelocity = 3.2f;
        maxFlightHeight = 1.15f;
        bodyScale = 0.58f;
        DyeTint(new Color(1.45f, 0.9f, 0.4f));
        modelTint = new Color(1f, 0.85f, 0.45f);
        modelTintAmount = 0.6f;
        transform.localScale = Vector3.one * bodyScale;
        gameObject.name = "Hatchling";
    }

    void ApplyTrait(TurkeyTrait trait)
    {
        switch (trait)
        {
            case TurkeyTrait.Sprinter:
                walkSpeed *= 1.8f;
                chaseSpeed *= 1.45f;
                bodyScale *= 0.92f;
                DyeTint(new Color(1.4f, 1.15f, 0.5f));
                break;
            case TurkeyTrait.Hopper:
                jumpVelocity *= 1.2f;
                maxFlightHeight += 0.35f;
                minFlightInterval = 0.7f;
                maxFlightInterval = 1.5f;
                flightDuration *= 0.85f;
                DyeTint(new Color(1.25f, 0.85f, 0.45f));
                break;
            case TurkeyTrait.Bruiser:
                HitsToDrop += 2;
                walkSpeed *= 0.72f;
                chaseSpeed *= 0.72f;
                bodyScale *= 1.42f;
                DyeTint(new Color(1.35f, 0.4f, 0.32f));
                break;
            case TurkeyTrait.Juker:
                juker = true;
                walkSpeed *= 1.15f;
                DyeTint(new Color(0.55f, 1.25f, 0.55f));
                break;
            case TurkeyTrait.Glider:
                flightGravity = 4.2f;
                flightDuration = 2.5f;
                chaseSpeed *= 1.35f;
                maxFlightHeight += 0.55f;
                jumpVelocity *= 0.85f;
                DyeTint(new Color(0.55f, 0.8f, 1.45f));
                break;
            case TurkeyTrait.Splitter:
                splitter = true;
                DyeTint(new Color(1.55f, 0.65f, 0.25f));
                break;
            case TurkeyTrait.Blinker:
                blinker = true;
                DyeTint(new Color(1.15f, 0.45f, 1.45f));
                break;
            case TurkeyTrait.Charger:
                charger = true;
                walkSpeed *= 1.15f;
                DyeTint(new Color(1.2f, 0.35f, 0.25f));
                break;
            case TurkeyTrait.Swarmer:
                HitsToDrop = 1;
                walkSpeed *= 1.7f;
                chaseSpeed *= 1.45f;
                bodyScale *= 0.62f;
                jumpVelocity *= 0.8f;
                DyeTint(new Color(1.5f, 1.15f, 0.4f));
                break;
            case TurkeyTrait.Titan:
                HitsToDrop += 4;
                walkSpeed *= 0.55f;
                chaseSpeed *= 0.6f;
                bodyScale *= 1.75f;
                DyeTint(new Color(0.45f, 0.28f, 0.55f));
                break;
            case TurkeyTrait.Gobbler:
                IsGobbler = true;
                HitsToDrop = 5 + WaveDirector.Wave / 5;
                walkSpeed *= 0.62f;
                chaseSpeed *= 0.75f;
                jumpVelocity *= 0.75f;
                maxFlightHeight = 1.25f;
                bodyScale *= 1.85f;
                bodyTint = new Color(1.7f, 0.28f, 0.22f);
                wingTint = new Color(1.4f, 0.3f, 0.22f);
                headTint = new Color(1.7f, 1.25f, 0.35f);
                break;
        }
    }

    static string TraitName(TurkeyTrait primary, TurkeyTrait secondary)
    {
        if (primary == TurkeyTrait.None && secondary == TurkeyTrait.None)
            return "Turkey";
        if (secondary == TurkeyTrait.None || secondary == primary)
            return primary.ToString();
        return primary + " " + secondary;
    }

    void DyeTint(Color tint)
    {
        bodyTint = bodyTint == Color.white ? tint : Color.Lerp(bodyTint, tint, 0.5f);
        wingTint = wingTint == Color.white ? tint : Color.Lerp(wingTint, tint, 0.5f);
        headTint = headTint == Color.white ? tint : Color.Lerp(headTint, tint, 0.5f);
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

        // Start a flight attempt only while chasing and standing on the ground.
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
                peckIn = 1.05f;
                peckAnim = 1f;
                if (wall != null && wall.Alive)
                    wall.Stop(this);
                else if (atMusician)
                    musician.Hurt(Boss ? 3 : Elite ? 2 : 1);
                verticalVelocity = Mathf.Max(verticalVelocity, 1.5f);
            }
        }

        if (flying)
        {
            flightTimer -= dt;

            // Reduced gravity gives the turkey a short, visibly gliding descent.
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
            verticalVelocity -= normalGravity * dt;
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
        else
        {
            float flap = flying
                ? Mathf.Sin(Time.time * 32f)
                : Mathf.Sin(Time.time * 3f) * 0.2f;

            if (wingL != null)
                wingL.localRotation = Quaternion.Euler(0f, 0f, 18f + flap * 62f);

            if (wingR != null)
                wingR.localRotation = Quaternion.Euler(0f, 0f, -18f - flap * 62f);

            if (head != null)
            {
                head.localPosition = new Vector3(0f, 0.42f, 0.34f) +
                                     Vector3.up * Mathf.Sin(Time.time * 6f) * 0.02f;
            }
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

    void StartFlightAttempt()
    {
        flying = true;
        grounded = false;
        flightTimer = flightDuration;
        flightStartY = transform.position.y;
        verticalVelocity = jumpVelocity;
    }

    // One plain blow at the player's arrow strength: spikes, towers, stomps.
    public void Hit(float power = 1f)
    {
        if (down)
            return;

        int damage = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.ArrowDamage : 1;
        Damage(damage, DamageKind.Plain, false, damage, power);
    }

    // Every source of harm comes through here. True when this blow dropped the bird.
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
        if (splitter && !hatchling)
        {
            SpawnHatchling(new Vector3(0.75f, 0f, 0.35f));
            SpawnHatchling(new Vector3(-0.75f, 0f, -0.35f));
        }

        down = true;
        flying = false;
        StopClimbing();
        ClearLock();
        CelebrateDeath();
        PlayerGrowth.RegisterKill();
        int coins = MetaProgress.NoteBird(Kind);
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
        Fx.Death(transform.position, transform.lossyScale.y, Plumage, boss || Elite);
        if (Golden)
            Fx.Ring(transform.position, 5f, new Color(1f, 0.82f, 0.35f, 0.9f), 0.6f);
        if (Boss)
            Fx.Explosion(transform.position + Vector3.up * topY * 0.5f * transform.lossyScale.y, transform.lossyScale.y, new Color(1f, 0.6f, 0.3f));
        if (Kind == FowlKind.Thunderbird) {
            UpgradeLook.Say("The Thunderbird falls", 2.8f);
        }
    }

    // The feather color that flies off this bird when it is hit or falls.
    Color Plumage
    {
        get
        {
            if (Golden)
                return new Color(1f, 0.82f, 0.3f);
            if (Elite || hatchling)
                return Color.Lerp(new Color(0.6f, 0.45f, 0.3f), modelTint, 0.6f);
            switch (Kind)
            {
                case FowlKind.Hen: return new Color(0.8f, 0.5f, 0.25f);
                case FowlKind.Rooster: return new Color(0.85f, 0.35f, 0.15f);
                case FowlKind.Goose: return new Color(0.6f, 0.55f, 0.5f);
                case FowlKind.Swan: return new Color(0.95f, 0.95f, 0.92f);
                case FowlKind.Emu: return new Color(0.45f, 0.38f, 0.3f);
                case FowlKind.Ostrich: return new Color(0.92f, 0.9f, 0.85f);
                case FowlKind.Cassowary: return new Color(0.15f, 0.15f, 0.18f);
                case FowlKind.Terror: return new Color(0.45f, 0.36f, 0.27f);
                case FowlKind.Roc: return new Color(0.45f, 0.3f, 0.18f);
                case FowlKind.Thunderbird: return new Color(0.3f, 0.45f, 0.9f);
                default: return Color.Lerp(new Color(0.48f, 0.32f, 0.2f), modelTint, 0.25f);
            }
        }
    }

    void SpawnHatchling(Vector3 offset)
    {
        Vector3 spot = transform.position + offset;
        spot.y = GroundAt(spot + Vector3.up * 6f, spot.y) + 0.45f;
        var go = new GameObject("Hatchling");
        go.tag = "enemy";
        go.transform.position = spot;
        go.transform.rotation = transform.rotation;
        Enemy chick = go.AddComponent<Enemy>();
        chick.ApplyHatchling();
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

    static Color Dye(Color color, Color tint)
    {
        return new Color(
            Mathf.Clamp01(color.r * tint.r),
            Mathf.Clamp01(color.g * tint.g),
            Mathf.Clamp01(color.b * tint.b));
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
        Vector3 bird = transform.lossyScale;
        float wide = Mathf.Max(0.01f, bird.x);
        float tall = Mathf.Max(0.01f, bird.y);
        float deep = Mathf.Max(0.01f, bird.z);
        float barWorld = Mathf.Clamp(Mathf.Max(wide, tall) * 0.55f, 1.6f, 8f);
        barRoot.localScale = new Vector3(barWorld / wide, barWorld / tall, barWorld / deep);
        barRoot.localPosition = new Vector3(0f, topY + 0.12f, 0f);
        Vector3 away = barRoot.position - cam.transform.position;
        if (away.sqrMagnitude < 0.0001f)
            return;
        barRoot.rotation = Quaternion.LookRotation(away, cam.transform.up);
    }

    // Walk cycle, waddle, wing flaps, head bob and the peck, all from code on the Blender parts.
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
        stridePhase += dt * (5f + speed * 3.2f);
        float step = Mathf.Sin(stridePhase);
        bool stepSign = step > 0f;
        if (stepSign != lastStepSign && walk > 0.3f && bodyScale > 2.5f && grounded)
            Fx.Footstep(transform.position, bodyScale * 0.6f);
        lastStepSign = stepSign;

        if (legL != null)
            legL.localRotation = Quaternion.Euler(step * 34f * walk, 0f, 0f);
        if (legR != null)
            legR.localRotation = Quaternion.Euler(-step * 34f * walk, 0f, 0f);
        if (model != null)
        {
            float waddle = Mathf.Sin(stridePhase) * 7f * walk;
            float bounce = Mathf.Abs(Mathf.Cos(stridePhase)) * 0.03f * walk;
            model.localRotation = modelRest * Quaternion.Euler(walk * 6f, 0f, waddle);
            model.localPosition = new Vector3(0f, bounce, 0f);
        }

        float flapAngle;
        if (flying)
            flapAngle = 25f + Mathf.Sin(Time.time * 26f) * 55f;
        else if (climbing)
            flapAngle = 20f + Mathf.Sin(Time.time * 14f) * 25f;
        else
            flapAngle = 4f + Mathf.Sin(Time.time * 2.4f + jukePhase) * 3f + walk * Mathf.Abs(step) * 6f;
        // Positive flap lifts a wing on +X; the other side mirrors it.
        if (wingL != null)
            wingL.localRotation = wingLRest * Quaternion.Euler(0f, 0f, flapAngle * Mathf.Sign(wingL.localPosition.x + 0.0001f));
        if (wingR != null)
            wingR.localRotation = wingRRest * Quaternion.Euler(0f, 0f, flapAngle * Mathf.Sign(wingR.localPosition.x + 0.0001f));

        if (head != null)
        {
            peckAnim = Mathf.MoveTowards(peckAnim, 0f, dt * 4f);
            float peck = Mathf.Sin(peckAnim * Mathf.PI) * 45f;
            float bob = Mathf.Sin(stridePhase * 2f) * 6f * walk + Mathf.Sin(Time.time * 1.7f + jukePhase) * 3f;
            head.localRotation = headRestRot * Quaternion.Euler(bob + peck, Mathf.Sin(Time.time * 0.9f + jukePhase * 2f) * 10f * (1f - walk), 0f);
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
        FowlKind look = hatchling ? FowlKind.Turkey : Kind;
        GameObject made = BirdArt.Make(look, modelTint, modelTintAmount);
        if (made == null)
            return false;
        made.transform.SetParent(transform, false);
        made.transform.localPosition = Vector3.zero;
        model = made.transform;
        wingL = FindPart(model, "WingL");
        wingR = FindPart(model, "WingR");
        head = FindPart(model, "Head");
        legL = FindPart(model, "LegL");
        legR = FindPart(model, "LegR");
        // The head must lead. If the export axes ever flip, turn the model around.
        if (head != null && Vector3.Dot(head.position - transform.position, transform.forward) < 0f)
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
        modelRest = model.localRotation;
        if (wingL != null) wingLRest = wingL.localRotation;
        if (wingR != null) wingRRest = wingR.localRotation;
        if (head != null)
        {
            headRest = head.localPosition;
            headRestRot = head.localRotation;
        }

        // Fit the hit capsule to the body and head, ignoring spread wings.
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshRenderer renderer in made.GetComponentsInChildren<MeshRenderer>())
        {
            string part = renderer.transform.name;
            if (part.StartsWith("Wing"))
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

    void BuildTurkey()
    {
        if (BuildModel())
            return;

        Shader shader = Shader.Find("Knoll/VertexColorLit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        var root = new GameObject("Turkey");
        root.transform.SetParent(transform, false);

        AddMesh(root.transform, "Body", mat, BodyMesh());

        wingL = new GameObject("WingL").transform;
        wingL.SetParent(root.transform, false);
        wingL.localPosition = new Vector3(-0.16f, 0.22f, 0.02f);
        AddMesh(wingL, "Wing", mat, WingMesh());

        wingR = new GameObject("WingR").transform;
        wingR.SetParent(root.transform, false);
        wingR.localPosition = new Vector3(0.16f, 0.22f, 0.02f);
        wingR.localScale = new Vector3(-1f, 1f, 1f);
        AddMesh(wingR, "Wing", mat, WingMesh());

        head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new Vector3(0f, 0.42f, 0.34f);
        AddMesh(head, "Head", mat, HeadMesh());
    }

    static void AddMesh(Transform parent, string name, Material material, Mesh mesh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    Mesh BodyMesh()
    {
        var builder = new TurkeyMesh();
        builder.AddSphere(new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.24f, 0.42f), Dye(new Color(0.42f, 0.24f, 0.12f), bodyTint));
        builder.AddSphere(new Vector3(0f, 0.34f, -0.28f), new Vector3(0.16f, 0.2f, 0.08f), Dye(new Color(0.55f, 0.16f, 0.1f), bodyTint));
        builder.AddSphere(new Vector3(0.1f, 0.38f, -0.36f), new Vector3(0.05f, 0.16f, 0.1f), Dye(new Color(0.62f, 0.2f, 0.12f), bodyTint));
        builder.AddSphere(new Vector3(-0.1f, 0.38f, -0.36f), new Vector3(0.05f, 0.16f, 0.1f), Dye(new Color(0.5f, 0.14f, 0.1f), bodyTint));
        builder.AddSphere(new Vector3(0f, 0.12f, 0.08f), new Vector3(0.04f, 0.14f, 0.04f), Dye(new Color(0.72f, 0.5f, 0.28f), bodyTint));
        builder.AddSphere(new Vector3(0.1f, 0.12f, 0.02f), new Vector3(0.04f, 0.14f, 0.04f), Dye(new Color(0.72f, 0.5f, 0.28f), bodyTint));
        return builder.ToMesh("TurkeyBody");
    }

    Mesh WingMesh()
    {
        var builder = new TurkeyMesh();
        builder.AddSphere(new Vector3(-0.16f, 0f, 0f), new Vector3(0.22f, 0.05f, 0.16f), Dye(new Color(0.5f, 0.28f, 0.14f), wingTint));
        return builder.ToMesh("TurkeyWing");
    }

    Mesh HeadMesh()
    {
        var builder = new TurkeyMesh();
        builder.AddSphere(new Vector3(0f, 0.08f, 0.02f), new Vector3(0.05f, 0.16f, 0.05f), Dye(new Color(0.36f, 0.5f, 0.62f), headTint));
        builder.AddSphere(Vector3.zero, new Vector3(0.1f, 0.09f, 0.11f), Dye(new Color(0.32f, 0.48f, 0.66f), headTint));
        builder.AddSphere(new Vector3(0f, -0.02f, 0.1f), new Vector3(0.04f, 0.03f, 0.08f), Dye(new Color(0.9f, 0.62f, 0.18f), headTint));
        builder.AddSphere(new Vector3(0f, -0.06f, 0.04f), new Vector3(0.035f, 0.06f, 0.03f), Dye(new Color(0.72f, 0.1f, 0.08f), headTint));
        builder.AddSphere(new Vector3(0.04f, 0.03f, 0.06f), new Vector3(0.02f, 0.02f, 0.02f), new Color(0.05f, 0.05f, 0.05f));
        builder.AddSphere(new Vector3(-0.04f, 0.03f, 0.06f), new Vector3(0.02f, 0.02f, 0.02f), new Color(0.05f, 0.05f, 0.05f));
        return builder.ToMesh("TurkeyHead");
    }
}

sealed class TurkeyMesh
{
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> triangles = new List<int>();

    public void AddSphere(Vector3 center, Vector3 radius, Color color)
    {
        const int lat = 6;
        const int lon = 8;

        for (int y = 0; y < lat; y++)
        {
            float v0 = y / (float)lat;
            float v1 = (y + 1) / (float)lat;
            float y0 = Mathf.Cos(v0 * Mathf.PI);
            float y1 = Mathf.Cos(v1 * Mathf.PI);
            float r0 = Mathf.Sin(v0 * Mathf.PI);
            float r1 = Mathf.Sin(v1 * Mathf.PI);

            for (int x = 0; x < lon; x++)
            {
                float u0 = x / (float)lon * Mathf.PI * 2f;
                float u1 = (x + 1) / (float)lon * Mathf.PI * 2f;

                Vector3 p00 = center + Vector3.Scale(new Vector3(Mathf.Cos(u0) * r0, y0, Mathf.Sin(u0) * r0), radius);
                Vector3 p10 = center + Vector3.Scale(new Vector3(Mathf.Cos(u1) * r0, y0, Mathf.Sin(u1) * r0), radius);
                Vector3 p01 = center + Vector3.Scale(new Vector3(Mathf.Cos(u0) * r1, y1, Mathf.Sin(u0) * r1), radius);
                Vector3 p11 = center + Vector3.Scale(new Vector3(Mathf.Cos(u1) * r1, y1, Mathf.Sin(u1) * r1), radius);

                AddTriangle(p00, p01, p11, color);
                AddTriangle(p00, p11, p10, color);
            }
        }
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
