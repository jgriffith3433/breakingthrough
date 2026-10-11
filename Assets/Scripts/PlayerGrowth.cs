using UnityEngine;
using UnityEngine.SceneManagement;

// Every turkey you drop makes you a little bigger. Size unlocks the later powers.
public class PlayerGrowth : MonoBehaviour {
    public static PlayerGrowth Instance { get; private set; }
    public static string Banner { get; private set; }

    public const int ClimbWave = 50;

    public bool Rooted { get; private set; }

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
    float bannerLeft;
    float stompIn;
    float rootedLeft;
    bool stomping;
    float airTime;
    bool leftGround;
    bool toldJump;
    bool toldStomp;
    bool toldBoom;
    bool toldClimb;
    int kills;
    int wave = 1;
    int killsThisWave;
    float punch = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
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
        if (!toldJump && wave >= 20) {
            toldJump = true;
            Show("Jump lands hurt birds from 20 levels back.", 5f);
        }
        if (!toldStomp && wave >= MetaProgress.StompWave()) {
            toldStomp = true;
            Show("Stomp. Nearby turkeys, one foot at a time.", 5f);
        }
        if (!toldBoom && MetaProgress.ShootsRockets()) {
            toldBoom = true;
            Show("Rockets, not arrows. They explode.", 5f);
        }
        if (!toldClimb && wave >= ClimbWave) {
            toldClimb = true;
            Show("Birds from 5 waves back climb you. Press F.", 6f);
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

        if (rootedLeft > 0f) {
            rootedLeft -= Time.deltaTime;
            if (rootedLeft <= 0f) {
                Rooted = false;
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null) {
                    anim.SetInteger(CharacterMovement.PARAMETER_STATE, 0);
                }
            }
        }

        bool swarmed = Enemy.Climbers > 0;
        if (swarmed) {
            stomping = false;
            airTime = 0f;
            leftGround = false;
            Banner = "They're climbing you. Press F.";
            bannerLeft = 0.35f;
            if (Input.GetKeyDown(KeyCode.F)) {
                Shake();
            }
            return;
        }

        TrackLanding();

        if (wave < MetaProgress.StompWave() || !controller.isGrounded || !TurkeyNear()) {
            stomping = false;
            return;
        }

        if (!stomping) {
            stomping = true;
            stompIn = 0f;
            PlayBody("Stomp", 5);
        }

        Rooted = true;
        rootedLeft = 0.3f;
        stompIn -= Time.deltaTime;
        if (stompIn > 0f) {
            return;
        }

        stompIn = 0.45f;
        StompFoot();
    }

    bool TurkeyNear() {
        float radius = BodyRadius + 1.35f;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive || enemy.Kind != FowlKind.Turkey || enemy.Climbing) {
                continue;
            }
            Vector3 flat = enemy.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= radius) {
                return true;
            }
        }
        return false;
    }

    void TrackLanding() {
        if (controller.isGrounded) {
            if (leftGround && airTime > 0.18f && wave >= 20) {
                LandStomp();
            }
            airTime = 0f;
            leftGround = false;
        } else {
            airTime += Time.deltaTime;
            leftGround = true;
        }
    }

    void LandStomp() {
        GameAudio.Play("stomp", 1f);
        Pound(BodyRadius + 1.15f * Scale, false, true);
    }

    void StompFoot() {
        GameAudio.Play("stomp", 0.7f);
        Pound(BodyRadius + 1.35f, true, false);
    }

    void Pound(float radius, bool turkeysOnly, bool onlyOlder) {
        Vector3 origin = transform.position;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            if (turkeysOnly && enemy.Kind != FowlKind.Turkey) {
                continue;
            }
            if (onlyOlder && WaveDirector.Wave - enemy.DebutWave < 20) {
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

    void Shake() {
        stomping = false;
        Rooted = true;
        rootedLeft = 2.1f;
        PlayBody("Shake", 7);
        Enemy.ShakeOffClimbers(Scale);
        GameAudio.Play("shake", 0.7f);
    }

    void PlayBody(string stateName, int stateId) {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) {
            return;
        }
        anim.SetInteger(CharacterMovement.PARAMETER_STATE, stateId);
        if (anim.HasState(0, Animator.StringToHash(stateName))) {
            anim.CrossFade(stateName, 0.05f, 0, 0f);
        }
    }

    void Show(string text, float seconds) {
        Banner = text;
        bannerLeft = seconds;
    }
}
