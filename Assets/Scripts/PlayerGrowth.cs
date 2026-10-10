using UnityEngine;

// Every turkey you drop makes you a little bigger. Size unlocks the later powers.
public class PlayerGrowth : MonoBehaviour {
    public static PlayerGrowth Instance { get; private set; }
    public static string Banner { get; private set; }

    public const float StompScale = 1.6f;
    public const float BoomScale = 2.2f;
    public const float ClimbScale = 6.3f;

    public float Scale { get; private set; } = 1f;
    public float BodyRadius { get; private set; } = 0.5f;
    public float BodyHeight { get; private set; } = 2f;

    CharacterController controller;
    CharacterMovement movement;
    Transform visual;
    Vector3 baseVisual;
    float baseHeight = 2f;
    float baseRadius = 0.5f;
    float baseCenter = 1f;
    float baseSpeed = 4f;
    float airTime;
    bool leftGround;
    float shake;
    float bannerLeft;
    bool toldStomp;
    bool toldBoom;
    bool toldClimb;
    int kills;
    int wave = 1;
    int killsThisWave;
    float punch = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || player.GetComponent<PlayerGrowth>() != null) {
            return;
        }
        player.AddComponent<PlayerGrowth>();
    }

    void Awake() {
        Instance = this;
    }

    void Start() {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<CharacterMovement>();
        if (controller != null) {
            baseHeight = controller.height;
            baseRadius = controller.radius;
            baseCenter = controller.center.y;
            BodyRadius = baseRadius;
            BodyHeight = baseHeight;
        }
        if (movement != null) {
            baseSpeed = movement.Speed;
        }
        EnsureVisual();
        ApplyScale();
    }

    public static int DamageAtWave(int waveNumber) {
        float raw = 1f + (Mathf.Max(1, waveNumber) - 1) * 0.5f;
        return Mathf.Max(1, Mathf.FloorToInt(raw + 0.001f) + MetaProgress.BonusDamage());
    }

    public int ArrowDamage {
        get {
            float raw = 1f + (Mathf.Max(1, wave) - 1) * 0.5f + killsThisWave * 0.01f;
            return Mathf.Max(1, Mathf.FloorToInt(raw + 0.001f) + MetaProgress.BonusDamage());
        }
    }

    public void SyncToWave(int waveNumber) {
        int next = Mathf.Max(1, waveNumber);
        if (next > wave) {
            punch = 1.18f;
        }
        wave = next;
        killsThisWave = 0;
        ApplyScale();
    }

    public static void RegisterKill() {
        if (Instance == null) {
            return;
        }
        Instance.kills++;
        Instance.killsThisWave++;
        Instance.punch = 1.22f;
        Instance.ApplyScale();
    }

    void EnsureVisual() {
        if (visual != null) {
            return;
        }
        GameObject marked = GameObject.FindGameObjectWithTag("playerchild");
        if (marked != null) {
            visual = marked.transform;
        } else {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null) {
                visual = animator.transform;
            }
        }
        if (visual != null) {
            baseVisual = visual.localScale;
        }
    }

    void ApplyScale() {
        EnsureVisual();
        float progress = (Mathf.Max(1, wave) - 1) + killsThisWave * 0.035f;
        Scale = 1f + progress * MetaProgress.GrowthPerWave();
        if (controller != null) {
            controller.height = baseHeight * Scale;
            controller.radius = baseRadius * Scale;
            controller.center = new Vector3(controller.center.x, baseCenter * Scale, controller.center.z);
            BodyRadius = controller.radius;
            BodyHeight = controller.height;
        }
        if (movement != null) {
            movement.Speed = baseSpeed * Mathf.Lerp(1f, 1.7f, Mathf.Clamp01((Scale - 1f) / 4f)) * MetaProgress.Stride();
        }
        ZeldaStyleCamera cam = FindAnyObjectByType<ZeldaStyleCamera>();
        if (cam != null) {
            cam.BodyScale = Scale;
        }

        if (!toldStomp && Scale >= MetaProgress.StompScale()) {
            toldStomp = true;
            Show("Stomp. Jump, then land on them.", 5f);
        }
        if (!toldBoom && Scale >= MetaProgress.BoomScale()) {
            toldBoom = true;
            Show("Your arrows are big enough to blast them.", 5f);
        }
        if (!toldClimb && Scale >= ClimbScale) {
            toldClimb = true;
            Show("They're climbing you. Wiggle the mouse.", 6f);
        }
    }

    void LateUpdate() {
        if (visual != null) {
            visual.localScale = baseVisual * Scale * punch;
        }
    }

    void Update() {
        if (punch > 1.001f) {
            punch = Mathf.MoveTowards(punch, 1f, Time.deltaTime * 1.4f);
        }

        if (bannerLeft > 0f) {
            bannerLeft -= Time.deltaTime;
            if (bannerLeft <= 0f) {
                Banner = null;
            }
        }

        if (controller == null) {
            return;
        }

        bool grounded = controller.isGrounded;
        if (!grounded) {
            airTime += Time.deltaTime;
            leftGround = true;
        } else {
            if (leftGround && airTime > 0.18f && Scale >= MetaProgress.StompScale()) {
                Stomp();
            }
            airTime = 0f;
            leftGround = false;
        }

        if (Enemy.Climbers > 0) {
            Banner = "They're climbing you. Wiggle the mouse.";
            bannerLeft = 0.4f;
            shake += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")).magnitude;
            if (shake >= 5.5f) {
                shake = 0f;
                Enemy.ShakeOffClimbers(Scale);
                GameAudio.Play("launch", 1f);
            }
        }
    }

    void Stomp() {
        GameAudio.Play("stomp", 1f);
        Vector3 origin = transform.position;
        float radius = BodyRadius + 1.15f * Scale;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            Vector3 flat = enemy.transform.position - origin;
            flat.y = 0f;
            if (flat.magnitude <= radius) {
                enemy.Stomp(Scale);
            }
        }

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Stomp";
        ring.transform.position = origin + Vector3.up * 0.08f;
        ring.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);
        Renderer renderer = ring.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null && renderer != null) {
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        }
        Destroy(ring, 0.28f);
    }

    void Show(string text, float seconds) {
        Banner = text;
        bannerLeft = seconds;
    }
}
