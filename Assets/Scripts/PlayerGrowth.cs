using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Every bot you drop makes you a little bigger. Size unlocks the later powers.
public class PlayerGrowth : MonoBehaviour {
    public static PlayerGrowth Instance { get; private set; }
    public static string Banner { get; private set; }
    public static int HitReacts { get; private set; }
    public static int Landings { get; private set; }

    public const int ClimbWave = Tuning.ClimbWave;

    public bool Rooted { get; private set; }
    public bool IsPlayingActionClip { get { return actionClipLeft > 0f; } }
    public bool IsPlayingVictory { get { return actionClipLeft > 0f && currentActionClip == CharacterMovement.CLIP_VICTORY; } }

    public float Scale { get; private set; } = 1f;
    public float BodyRadius { get; private set; } = 0.5f;
    public float BodyHeight { get; private set; } = 2f;

    CharacterController controller;
    CharacterMovement movement;
    Transform visual;
    Vector3 baseVisual = Vector3.one;
    float baseHeight = 2f;
    float baseRadius = 0.5f;
    float baseCenter = 1f;
    float baseSpeed = 4f;
    float bannerLeft;
    float stompIn;
    float rootedLeft;
    float actionClipLeft;
    int currentActionClip = -1;
    float nextHitReactAt;
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
        HitReacts = 0;
        Landings = 0;
        CharacterMovement.EnsureWallitOn(gameObject);
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
        Animator anim = CharacterMovement.EnsureWallitOn(gameObject);
        if (anim != null && anim.updateMode != AnimatorUpdateMode.Normal) {
            anim.updateMode = AnimatorUpdateMode.Normal;
        }
        ApplyScale();
    }

    public void RefreshStats() {
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
        if (visual != null && visual.gameObject.activeInHierarchy) {
            return;
        }
        Animator animator = CharacterMovement.EnsureWallitOn(gameObject);
        GameObject marked = GameObject.FindGameObjectWithTag("playerchild");
        if (marked != null) {
            visual = marked.transform;
        } else if (animator != null) {
            visual = animator.transform;
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
            movement.Speed = baseSpeed * Mathf.Lerp(1f, 1.7f, Mathf.Clamp01((Scale - 1f) / 4f)) * MetaProgress.Stride() * RunBoons.MoveMult;
        }
        ZeldaStyleCamera cam = FindAnyObjectByType<ZeldaStyleCamera>();
        if (cam != null) {
            cam.BodyScale = Scale;
        }
        if (!toldJump && wave >= Tuning.JumpWave) {
            toldJump = true;
            Show("Land a jump on Yo Bots to crush them. Hold Space to keep hopping.", 5f);
        }
        if (!toldStomp && wave >= MetaProgress.StompWave()) {
            toldStomp = true;
            Show("Stomp. Nearby Yo Bots, one foot at a time.", 5f);
        }
        if (!toldBoom && MetaProgress.MetalBat()) {
            toldBoom = true;
            Show("Aluminum bat. The third swing of a combo goes off.", 5f);
        }
        if (!toldClimb && wave >= ClimbWave) {
            toldClimb = true;
            Show("Older bots climb you now instead of the piano. Press F.", 6f);
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

        if (actionClipLeft > 0f) {
            if (BatEquipment.IsAnimatingAction && currentActionClip != CharacterMovement.CLIP_SHAKE_OFF) {
                actionClipLeft = 0f;
                currentActionClip = -1;
            } else {
                actionClipLeft -= Time.deltaTime;
                if (actionClipLeft <= 0f) {
                    currentActionClip = -1;
                }
            }
        }

        if (controller == null) {
            return;
        }

        if (rootedLeft > 0f) {
            rootedLeft -= Time.deltaTime;
            if (rootedLeft <= 0f) {
                Rooted = false;
                Animator anim = CharacterMovement.EnsureWallitOn(gameObject);
                if (anim != null && !BatEquipment.IsAnimatingAction && !WaveDirector.IsLost) {
                    CharacterMovement.PlayClip(anim, CharacterMovement.CLIP_IDLE, false, 0.08f);
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
            if (PlayInput.GetKeyDown(KeyCode.F)) {
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
            PlayActionClip(CharacterMovement.CLIP_LANDING, 0.45f);
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
        List<Enemy> enemies = Enemy.Roster;
        for (int i = 0; i < enemies.Count; i++) {
            Enemy enemy = enemies[i];
            if (enemy == null || !enemy.Alive || enemy.Kind != BotKind.YoBot || enemy.Climbing) {
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
            if (leftGround && airTime > 0.18f) {
                Landings++;
                if (wave >= Tuning.JumpWave) {
                    LandStomp();
                } else {
                    PlayActionClip(CharacterMovement.CLIP_LANDING, 0.38f);
                }
            }
            airTime = 0f;
            leftGround = false;
        } else {
            airTime += Time.deltaTime;
            leftGround = true;
        }
    }

    public void LandStomp() {
        PlayActionClip(CharacterMovement.CLIP_LANDING, 0.45f);
        GameAudio.Play("stomp", 1f);
        Pound(BodyRadius + 1.15f * Scale, false, true);
    }

    void StompFoot() {
        PlayActionClip(CharacterMovement.CLIP_LANDING, 0.4f);
        GameAudio.Play("stomp", 0.7f);
        Pound(BodyRadius + 1.35f, true, false);
    }

    void Pound(float radius, bool turkeysOnly, bool onlyOlder) {
        Vector3 origin = transform.position;
        var snapshot = new List<Enemy>(Enemy.Roster);
        for (int i = 0; i < snapshot.Count; i++) {
            Enemy enemy = snapshot[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            if (turkeysOnly && enemy.Kind != BotKind.YoBot) {
                continue;
            }
            if (onlyOlder && WaveDirector.Wave - enemy.DebutWave < Tuning.JumpLandGap) {
                continue;
            }
            Vector3 flat = enemy.transform.position - origin;
            flat.y = 0f;
            if (flat.magnitude <= radius) {
                enemy.Stomp(Scale);
            }
        }

        Fx.Ring(origin, radius * 2f, new Color(0.95f, 0.82f, 0.45f, 0.9f), 0.35f);
        Fx.Emit(FxKind.Dust, origin, 10, new Color(0.62f, 0.58f, 0.5f, 0.55f), 0.6f * Scale, 2.2f * Scale, radius * 0.6f);
    }

    public void Shake() {
        stomping = false;
        Rooted = true;
        rootedLeft = 1.2f;
        PlayActionClip(CharacterMovement.CLIP_SHAKE_OFF, 1.1f);
        Enemy.ShakeOffClimbers(Scale);
        GameAudio.Play("shake", 0.7f);
    }

    public void OnHit(Enemy by = null) {
        if (WaveDirector.IsLost || BatEquipment.IsAnimatingAction || Time.time < nextHitReactAt) {
            return;
        }
        nextHitReactAt = Time.time + 1.5f;
        HitReacts++;
        PlayActionClip(CharacterMovement.CLIP_HIT_REACT, 0.38f);
    }

    public void OnVictory() {
        if (WaveDirector.IsLost || BatEquipment.IsAnimatingAction) {
            return;
        }
        PlayActionClip(CharacterMovement.CLIP_VICTORY, 1.4f);
    }

    public void CancelVictory() {
        if (currentActionClip == CharacterMovement.CLIP_VICTORY) {
            actionClipLeft = 0f;
            currentActionClip = -1;
        }
    }

    public void OnDeath() {
        actionClipLeft = 99f;
        currentActionClip = CharacterMovement.CLIP_DEATH;
        Animator anim = CharacterMovement.EnsureWallitOn(gameObject);
        if (anim != null) {
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.speed = 1f;
            CharacterMovement.PlayClip(anim, CharacterMovement.CLIP_DEATH, true, 0.06f);
        }
    }

    void PlayActionClip(int clipId, float duration) {
        Animator anim = CharacterMovement.EnsureWallitOn(gameObject);
        if (anim == null) {
            return;
        }
        currentActionClip = clipId;
        actionClipLeft = duration;
        anim.speed = 1f;
        CharacterMovement.PlayClip(anim, clipId, true, 0.05f);
    }

    void Show(string text, float seconds) {
        Banner = text;
        bannerLeft = seconds;
    }
}
