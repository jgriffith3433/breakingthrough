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
    public int HitsToDrop = 2;
    public bool IsGobbler { get; private set; }

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
    public static int Climbers { get; private set; }

    Transform wingL;
    Transform wingR;
    Transform head;

    float verticalVelocity;
    float flightTimer;
    float nextFlightTimer = 0.6f;
    float flightStartY;
    float fallTime;
    int hits;
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
    Transform barRoot;
    Transform barFill;

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
    }

    void OnDestroy()
    {
        if (climbing)
        {
            climbing = false;
            Climbers = Mathf.Max(0, Climbers - 1);
        }
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

        if (!down && PlayerGrowth.Instance != null && PlayerGrowth.Instance.Scale >= PlayerGrowth.ClimbScale)
            TryClimb();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 toPlayer = Vector3.zero;
        float distance = float.MaxValue;

        if (player != null)
        {
            toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            distance = toPlayer.magnitude;
        }

        bool chasing = !down && player != null &&
                       distance > stopDistance && distance < chaseRange;

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
            Vector3 wish = toPlayer.normalized;
            float speed = flying ? chaseSpeed : walkSpeed;
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

    void StartFlightAttempt()
    {
        flying = true;
        grounded = false;
        flightTimer = flightDuration;
        flightStartY = transform.position.y;
        verticalVelocity = jumpVelocity;
    }

    public void Hit(float power = 1f)
    {
        if (down)
            return;

        int damage = 1;
        if (power >= 2.8f)
            damage = 2;
        if (power >= 4.2f)
            damage = HitsToDrop;
        hits += damage;
        RefreshBar();
        GameAudio.Play("hit", 0.7f);
        verticalVelocity = 2.2f;

        if (hits < HitsToDrop)
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
        PlayerGrowth.RegisterKill();
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
        hits += scale >= 4.8f ? 2 : 1;
        RefreshBar();
        if (hits >= HitsToDrop)
        {
            down = true;
            flying = false;
            ClearLock();
            PlayerGrowth.RegisterKill();
            BeginRagdoll(scale, false);
            return;
        }
        BeginRagdoll(scale, true);
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

    void TryClimb()
    {
        PlayerGrowth growth = PlayerGrowth.Instance;
        if (growth == null)
            return;

        Vector3 flat = growth.transform.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > growth.BodyRadius + 0.45f)
            return;

        climbing = true;
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
        transform.position = growth.transform.position + outDir * (growth.BodyRadius * 0.92f) + Vector3.up * climbHeight;
        transform.rotation = Quaternion.LookRotation(-outDir);
    }

    void StopClimbing()
    {
        if (!climbing)
            return;
        climbing = false;
        Climbers = Mathf.Max(0, Climbers - 1);
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
            GameAudio.Play("death", 0.8f);
            if (power >= PlayerGrowth.BoomScale)
                GameAudio.Play("launch", 0.7f);
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

        Transform back = MakeBarQuad("Back", shader, new Color(0.12f, 0.08f, 0.08f));
        back.SetParent(barRoot, false);
        back.localScale = new Vector3(0.72f, 0.09f, 1f);

        barFill = MakeBarQuad("Fill", shader, new Color(0.35f, 0.85f, 0.32f));
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
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = color;
        }
        return quad.transform;
    }

    void RefreshBar()
    {
        if (barFill == null)
            return;
        float remaining = Mathf.Clamp01(1f - hits / Mathf.Max(1f, HitsToDrop));
        barFill.localScale = new Vector3(0.68f * remaining, 0.06f, 1f);
        barFill.localPosition = new Vector3(-0.34f + barFill.localScale.x * 0.5f, 0f, -0.01f);
        Renderer renderer = barFill.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
            renderer.sharedMaterial.color = remaining > 0.5f
                ? new Color(0.35f, 0.85f, 0.32f)
                : new Color(0.9f, 0.28f, 0.2f);
    }

    void FaceBar()
    {
        if (barRoot == null || Camera.main == null)
            return;
        float size = Mathf.Max(0.01f, transform.localScale.x);
        barRoot.localScale = Vector3.one * (1.15f / size);
        barRoot.rotation = Quaternion.LookRotation(Camera.main.transform.position - barRoot.position);
    }

    void BuildTurkey()
    {
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
