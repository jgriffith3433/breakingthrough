using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMovement : MonoBehaviour {
    public static CharacterMovement Instance { get; private set; }

    private MovementMotor motor;
    public float MoveMagnitude = 0.05f;
    public float Speed = 4.0f;
    public float SpeedMoveWhileAttack = 0.1f;
    public float SpeedAttack = 1.5f;
    public float TurnSpeed = 20f;
    // Free running turns smoothly instead of snapping; the legs are bent to the real direction of travel meanwhile.
    public float FreeTurnSpeed = 12f;
    // Locomotion: m/s the body must move to start or keep the move clips, and the most the legs are bent to travel.
    public float StartSpeed = 0.6f;
    public float StopSpeed = 0.3f;
    public float StopHold = 0.1f;
    public float MaxWarp = 60f;
    public float SpeedJump = 20f;
    float jumpAnimLeft;
    private Vector3 Tar;

    [SerializeField]
    public ZeldaStyleCamera ZeldaCam;
    private Animator Anim;

    public const string PARAMETER_STATE = "State";
    public const string PARAMETER_CLIP = "Clip";

    public const int CLIP_IDLE = 0;
    public const int CLIP_RUN = 1;
    public const int CLIP_STRAFE_LEFT = 2;
    public const int CLIP_STRAFE_RIGHT = 3;
    public const int CLIP_STRAFE_L = CLIP_STRAFE_LEFT;
    public const int CLIP_STRAFE_R = CLIP_STRAFE_RIGHT;
    public const int CLIP_BACK_PEDAL = 4;
    public const int CLIP_SWING_1 = 5;
    public const int CLIP_SWING_2 = 6;
    public const int CLIP_SWING_3 = 7;
    public const int CLIP_GROUND_SMASH = 8;
    public const int CLIP_HOMERUN_CHARGE = 9;
    public const int CLIP_HOMERUN_RELEASE = 10;
    public const int CLIP_JUMP = 11;
    public const int CLIP_LANDING = 12;
    public const int CLIP_HIT_REACT = 13;
    public const int CLIP_SHAKE_OFF = 14;
    public const int CLIP_VICTORY = 15;
    public const int CLIP_DEATH = 16;

    public static readonly string[] ClipNames = {
        "idle", "run", "strafeLeft", "strafeRight", "backPedal",
        "swing1", "swing2", "swing3", "groundSmash",
        "homeRunCharge", "homeRunRelease",
        "jump", "landing", "hitReact", "shakeOff", "victory", "death"
    };

    // Ground speed each locomotion clip covers at playback speed 1, for a Wallit at scale 1. Starting guesses: the
    // planted foot is measured every frame (WallitStride) and these are pulled toward what the rig really does.
    static readonly float[] DefaultStride = { 0f, 4.4f, 2.6f, 2.6f, 2.4f };
    readonly float[] stride = (float[])DefaultStride.Clone();

    public static readonly int[] ClipCounts = new int[17];
    public static int LastClip { get; private set; } = -1;
    static float actionClipUntil;

    static readonly int ClipHash = Animator.StringToHash(PARAMETER_CLIP);
    static readonly int StateHash = Animator.StringToHash(PARAMETER_STATE);
    static readonly int FireSpeedHash = Animator.StringToHash("FireSpeed");
    static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    bool hasFireSpeed;
    bool hasMoveSpeed;

    // Locomotion state.
    Transform model;
    Transform footL;
    Transform footR;
    Transform hips;
    Vector3 footLLocal, footRLocal, footLWorld, footRWorld, hipsLocal;
    bool haveFeet;
    float footFloor = 999f;
    bool moving;
    float stopTimer;
    int moveClip = CLIP_RUN;
    Vector3 travel = Vector3.forward;
    float warp;
    float warpVel;
    float animSpeed = 1f;
    float animSpeedVel;
    public static float GroundSpeed { get; private set; }
    public static float AnimSpeed { get; private set; }

    public static void ResetClipCounts() {
        for (int i = 0; i < ClipCounts.Length; i++) {
            ClipCounts[i] = 0;
        }
        LastClip = -1;
        actionClipUntil = 0f;
    }

    public static int ClipCount(int clipId) {
        if (clipId < 0 || clipId >= ClipCounts.Length) {
            return 0;
        }
        return ClipCounts[clipId];
    }

    public static void PlayWallitClip(int clipId, float duration = 0f) {
        if (clipId < 0 || clipId >= ClipNames.Length) {
            return;
        }
        Animator targetAnim = Instance != null ? Instance.Anim : null;
        if (targetAnim == null) {
            CharacterMovement cm = FindAnyObjectByType<CharacterMovement>();
            if (cm != null) {
                Instance = cm;
                if (cm.Anim == null) {
                    cm.Anim = EnsureWallitOn(cm.gameObject);
                }
                targetAnim = cm.Anim;
            }
        }
        if (duration > 0f && !BatEquipment.IsAnimatingAction) {
            actionClipUntil = Time.unscaledTime + duration;
        }
        if (targetAnim != null) {
            targetAnim.speed = 1f;
            PlayClip(targetAnim, clipId, true, 0.05f);
        } else {
            ClipCounts[clipId]++;
            LastClip = clipId;
        }
    }

    public static void ClearActionClip() {
        actionClipUntil = 0f;
    }

    public static Animator EnsureWallitOn(GameObject player) {
        if (player == null) {
            return null;
        }

        Transform existing = player.transform.Find("Wallit");
        if (existing != null && existing.gameObject.activeSelf) {
            existing.gameObject.tag = "playerchild";
            BirdArt.CalmEmission(existing.gameObject);
            ApplyWallitSkins(existing.gameObject);
            Animator existingAnim = existing.GetComponentInChildren<Animator>();
            if (existingAnim != null) {
                if (existingAnim.gameObject.GetComponent<WallitAnimRelay>() == null) {
                    existingAnim.gameObject.AddComponent<WallitAnimRelay>();
                }
                return existingAnim;
            }
        }

        var oldChildren = new List<GameObject>();
        foreach (Transform child in player.transform) {
            if (child == existing) {
                continue;
            }
            string n = child.name;
            if (n == "ptarget" || n == "LockOn" || n == "FirstPersonCamPos" || n == "EquippedBat" || n == "Bat" || n == "BoltPouch") {
                continue;
            }
            if (child.CompareTag("playerchild") || child.GetComponent<Animator>() != null || n.Contains("Warrok") || n.Contains("Missing")) {
                oldChildren.Add(child.gameObject);
            }
        }
        for (int i = 0; i < oldChildren.Count; i++) {
            GameObject old = oldChildren[i];
            old.tag = "Untagged";
            old.SetActive(false);
            Destroy(old);
        }

        GameObject wallitPrefab = Resources.Load<GameObject>("Wallit/Wallit");
        if (wallitPrefab != null) {
            GameObject wallit = Instantiate(wallitPrefab, player.transform);
            wallit.name = "Wallit";
            BirdArt.CalmEmission(wallit);
            ApplyWallitSkins(wallit);
            wallit.tag = "playerchild";
            wallit.transform.SetAsFirstSibling();
            wallit.transform.localPosition = Vector3.zero;
            wallit.transform.localRotation = Quaternion.identity;
            wallit.transform.localScale = Vector3.one;

            Animator anim = wallit.GetComponent<Animator>();
            if (anim == null) {
                anim = wallit.GetComponentInChildren<Animator>();
            }
            if (anim != null) {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (anim.gameObject.GetComponent<WallitAnimRelay>() == null) {
                    anim.gameObject.AddComponent<WallitAnimRelay>();
                }
            }

            FitWallitToPlayer(player, wallit);

            CharacterMovementController cmc = player.GetComponent<CharacterMovementController>();
            if (cmc != null && anim != null) {
                cmc.Animator = anim;
            }
            return anim;
        }

        return player.GetComponentInChildren<Animator>();
    }

    static Material wallitBodyMat;
    static Material wallitBatMat;

    // Binds the painted WallitSkin and WallitBatSkin textures on Knoll/BotSkin (with _Glow = 0) so Wallit and his
    // bat never fall back to the FBX importer's white-silhouette material in WebGL.
    public static void ApplyWallitSkins(GameObject wallit) {
        if (wallit == null) {
            return;
        }
        WallitMeshClean.Apply(wallit);
        Shader shader = SafeShader.Find("Knoll/BotSkin");
        if (shader == null) {
            return;
        }
        if (wallitBodyMat == null) {
            Texture2D bodySkin = Resources.Load<Texture2D>("Wallit/WallitSkin");
            if (bodySkin != null) {
                wallitBodyMat = new Material(shader);
                wallitBodyMat.name = "WallitSkin";
                wallitBodyMat.mainTexture = bodySkin;
                wallitBodyMat.color = Color.white;
                wallitBodyMat.SetFloat("_Lift", 1.18f);
                wallitBodyMat.SetFloat("_AmbientBoost", 0.28f);
                wallitBodyMat.SetFloat("_RimStrength", 0.25f);
                wallitBodyMat.SetFloat("_Glow", 0f);
            }
        }
        if (wallitBatMat == null) {
            Texture2D batSkin = Resources.Load<Texture2D>("Wallit/WallitBatSkin");
            if (batSkin != null) {
                wallitBatMat = new Material(shader);
                wallitBatMat.name = "WallitBatSkin";
                wallitBatMat.mainTexture = batSkin;
                wallitBatMat.color = Color.white;
                wallitBatMat.SetFloat("_Lift", 1.18f);
                wallitBatMat.SetFloat("_AmbientBoost", 0.28f);
                wallitBatMat.SetFloat("_RimStrength", 0.22f);
                wallitBatMat.SetFloat("_Glow", 0f);
            }
        }
        foreach (Renderer r in wallit.GetComponentsInChildren<Renderer>(true)) {
            if (r == null) {
                continue;
            }
            string n = r.gameObject.name.ToLowerInvariant();
            if (n == "handle" || n == "barrel" || r.transform.parent != null && r.transform.parent.name == "Marks") {
                continue;
            }
            if (n.Contains("bat")) {
                if (wallitBatMat != null) {
                    r.sharedMaterial = wallitBatMat;
                }
            } else if (wallitBodyMat != null) {
                r.sharedMaterial = wallitBodyMat;
            }
        }
    }

    static void FitWallitToPlayer(GameObject player, GameObject model) {
        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) {
            if (renderer.gameObject.name.IndexOf("bat", System.StringComparison.OrdinalIgnoreCase) >= 0
                || renderer.gameObject.name == "Handle" || renderer.gameObject.name == "Barrel") {
                continue;
            }
            if (!any) {
                bounds = renderer.bounds;
                any = true;
            } else {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!any) {
            return;
        }
        float height = bounds.size.y;
        if (height > 2.6f || height < 1.1f) {
            model.transform.localScale = Vector3.one * (1.8f / Mathf.Max(0.1f, height));
        }
    }

    public static bool IsLocomotion(int clipId) {
        return clipId >= CLIP_RUN && clipId <= CLIP_BACK_PEDAL;
    }

    static readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();

    static float ClipLength(Animator anim, string stateName) {
        float length;
        if (clipLengths.TryGetValue(stateName, out length)) {
            return length;
        }
        length = 0f;
        if (anim.runtimeAnimatorController != null) {
            foreach (AnimationClip c in anim.runtimeAnimatorController.animationClips) {
                if (c != null && c.name == stateName) {
                    length = c.length;
                }
            }
        }
        if (length > 0f) {
            clipLengths[stateName] = length;
        }
        return length;
    }

    // Moves between locomotion clips at the same point of the stride (so a foot does not jump to the start of a new
    // cycle), over a fixed time in seconds; every other change starts the clip from its beginning.
    public static void PlayLoco(Animator anim, int clipId, float seconds) {
        float phase = 0f;
        if (IsLocomotion(LastClip) && IsLocomotion(clipId) && LastClip != clipId) {
            AnimatorStateInfo from = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
            phase = from.normalizedTime - Mathf.Floor(from.normalizedTime);
        }
        PlayClip(anim, clipId, false, seconds, phase);
    }

    public static void PlayClip(Animator anim, int clipId, bool forceRestart = false, float crossFade = 0.06f, float phase = -1f) {
        if (anim == null || clipId < 0 || clipId >= ClipNames.Length) {
            return;
        }
        bool changed = LastClip != clipId;
        if (changed || forceRestart) {
            ClipCounts[clipId]++;
            LastClip = clipId;
        }

        bool hasClip = false;
        bool hasState = false;
        foreach (AnimatorControllerParameter p in anim.parameters) {
            if (p.nameHash == ClipHash) {
                hasClip = true;
            } else if (p.nameHash == StateHash) {
                hasState = true;
            }
        }

        if (hasClip) {
            // A timed cross fade below is the whole move; an Any State hit on this Clip would restart the clip at 0.
            anim.SetInteger(ClipHash, phase >= 0f ? -1 : clipId);
        }
        if (hasState) {
            int legacyState = clipId == CLIP_IDLE ? 0 : clipId == CLIP_JUMP ? 3 : clipId == CLIP_SHAKE_OFF ? 7 : clipId == CLIP_LANDING ? 5 : 1;
            anim.SetInteger(StateHash, legacyState);
        }

        string stateName = ClipNames[clipId];
        int stateHash = Animator.StringToHash(stateName);
        if (anim.HasState(0, stateHash)) {
            if (forceRestart) {
                anim.CrossFade(stateHash, crossFade, 0, 0f);
            } else if (changed) {
                AnimatorStateInfo cur = anim.GetCurrentAnimatorStateInfo(0);
                if (!cur.IsName(stateName)) {
                    float length = phase >= 0f ? ClipLength(anim, stateName) : 0f;
                    if (length > 0f) {
                        anim.CrossFadeInFixedTime(stateHash, crossFade, 0, phase * length);
                    } else {
                        anim.CrossFade(stateHash, crossFade, 0, 0f);
                    }
                }
            }
        }
    }

    void Awake() {
        Instance = this;
        ResetClipCounts();
        motor = GetComponent<MovementMotor>();
        Anim = EnsureWallitOn(gameObject);
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Start() {
        Instance = this;
        if (Anim == null) {
            Anim = EnsureWallitOn(gameObject);
        }
        if (Anim != null) {
            Anim.applyRootMotion = false;
            foreach (AnimatorControllerParameter parameter in Anim.parameters) {
                if (parameter.nameHash == FireSpeedHash) {
                    hasFireSpeed = true;
                }
                if (parameter.nameHash == MoveSpeedHash) {
                    hasMoveSpeed = true;
                }
            }
        }
    }

    void Update() {
        warpTarget = 0f;
        MovementAndJumping();
    }

    float warpTarget;

    // The legs run along the clip's own axis (forward, left, right, back). When travel is between two axes the
    // body is turned a little toward the travel direction so the clip's foot speed lands along the real motion.
    static float ClipAxis(int clip) {
        return clip == CLIP_STRAFE_RIGHT ? 90f : clip == CLIP_STRAFE_LEFT ? -90f : clip == CLIP_BACK_PEDAL ? 180f : 0f;
    }

    void SetMoveSpeed(float s) {
        AnimSpeed = s;
        if (hasMoveSpeed) {
            Anim.SetFloat(MoveSpeedHash, s);
            Anim.speed = 1f;
        } else {
            Anim.speed = s;
        }
    }

    void AnimationMove(Vector3 velocity) {
        if (Anim == null) {
            return;
        }
        if (WaveDirector.IsLost) {
            return;
        }
        if (BatEquipment.IsAnimatingAction) {
            return;
        }
        if (Time.unscaledTime < actionClipUntil) {
            return;
        }
        if (PlayerGrowth.Instance != null && PlayerGrowth.Instance.IsPlayingActionClip) {
            if (velocity.sqrMagnitude > MoveMagnitude * MoveMagnitude * 100f && PlayerGrowth.Instance.IsPlayingVictory) {
                PlayerGrowth.Instance.CancelVictory();
            } else {
                return;
            }
        }

        if (jumpAnimLeft > 0f) {
            jumpAnimLeft -= Time.deltaTime;
            if (motor != null && !motor.Airborne && jumpAnimLeft < 0.7f) {
                jumpAnimLeft = 0f;
            } else {
                Anim.speed = 1f;
                PlayClip(Anim, CLIP_JUMP, false, 0.05f);
                return;
            }
        }
        if (motor != null && motor.Airborne) {
            Anim.speed = 1f;
            PlayClip(Anim, CLIP_JUMP, false, 0.05f);
            return;
        }

        Locomote(velocity, Aiming());
    }

    // Idle, run, strafe or back pedal, matched to how fast the body really moves over the ground: the playback speed
    // is the ground speed over the speed the clip covers, so a planted foot stays planted; the clip follows the
    // direction of travel against the body (with a hold so it does not flicker at the diagonals); a start or a stop
    // is a short cross fade (a stop waits a moment so a turn through zero speed does not drop to idle).
    void Locomote(Vector3 velocity, bool aiming) {
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        float speed = flat.magnitude;
        GroundSpeed = speed;
        float scale = VisualScale;
        if (speed > 0.05f) {
            travel = flat / speed;
        }

        bool started = false;
        if (moving) {
            stopTimer = speed < StopSpeed ? stopTimer + Time.deltaTime : 0f;
            if (stopTimer >= StopHold) {
                moving = false;
            }
        } else if (speed > StartSpeed) {
            moving = true;
            started = true;
            stopTimer = 0f;
        }
        if (!moving) {
            SetMoveSpeed(1f);
            PlayLoco(Anim, CLIP_IDLE, 0.2f);
            return;
        }

        Vector3 local = transform.InverseTransformDirection(travel);
        float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        int clip = CLIP_RUN;
        if (aiming) {
            if (IsLocomotion(moveClip) && Mathf.Abs(Mathf.DeltaAngle(ClipAxis(moveClip), angle)) < 60f) {
                clip = moveClip;
            } else if (Mathf.Abs(angle) <= 45f) {
                clip = CLIP_RUN;
            } else if (angle > 45f && angle < 135f) {
                clip = CLIP_STRAFE_RIGHT;
            } else if (angle < -45f && angle > -135f) {
                clip = CLIP_STRAFE_LEFT;
            } else {
                clip = CLIP_BACK_PEDAL;
            }
        }
        moveClip = clip;
        warpTarget = Mathf.Clamp(Mathf.DeltaAngle(ClipAxis(clip), angle), -MaxWarp, MaxWarp);

        float want = Mathf.Clamp(speed / (Mathf.Max(0.5f, stride[clip]) * scale), 0.45f, 1.8f);
        animSpeed = started ? want : Mathf.SmoothDamp(animSpeed, want, ref animSpeedVel, 0.06f);
        SetMoveSpeed(animSpeed);
        PlayLoco(Anim, clip, 0.14f);
    }

    void LateUpdate() {
        if (Anim == null) {
            return;
        }
        if (model == null && Anim.transform != transform) {
            model = Anim.transform;
        }
        if (model != null) {
            warp = Mathf.SmoothDampAngle(warp, warpTarget, ref warpVel, 0.09f);
            model.localRotation = Quaternion.Euler(0f, warp, 0f);
        }
        TrackFeet();
    }

    // Reads the planted foot off the posed rig, after the Animator has run: how fast it moves over the ground (a
    // slide) and against the body (the stride the clip really covers). The stride pulls the playback speed in line.
    void TrackFeet() {
        if (model == null || !Anim.isHuman) {
            return;
        }
        if (footL == null || footR == null) {
            footL = Anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            footR = Anim.GetBoneTransform(HumanBodyBones.RightFoot);
            hips = Anim.GetBoneTransform(HumanBodyBones.Hips);
            haveFeet = false;
            if (footL == null || footR == null) {
                return;
            }
        }
        float dt = Time.deltaTime;
        Vector3 wl = footL.position;
        Vector3 wr = footR.position;
        Vector3 ll = model.InverseTransformPoint(wl);
        Vector3 lr = model.InverseTransformPoint(wr);
        Vector3 lh = hips != null ? model.InverseTransformPoint(hips.position) : Vector3.zero;
        footFloor = Mathf.Min(footFloor + 0.05f * dt, Mathf.Min(wl.y, wr.y));
        float scale = VisualScale;

        if (haveFeet && dt > 0.0001f && Time.timeScale > 0.9f) {
            AnimatorStateInfo cur = Anim.GetCurrentAnimatorStateInfo(0);
            bool transition = Anim.IsInTransition(0);
            if (hips != null && OnFoot(cur) && (!transition || OnFoot(Anim.GetNextAnimatorStateInfo(0)))) {
                float jump = (lh - hipsLocal).magnitude / scale;
                WallitStride.AddPop(jump, jump > 0.2f);
            }
            if (moving && IsLocomotion(moveClip) && !transition && cur.IsName(ClipNames[moveClip])) {
                bool leftLower = wl.y <= wr.y;
                Vector3 now = leftLower ? wl : wr;
                Vector3 before = leftLower ? footLWorld : footRWorld;
                Vector3 nowLocal = leftLower ? ll : lr;
                Vector3 beforeLocal = leftLower ? footLLocal : footRLocal;
                if (now.y - footFloor < WallitStride.PlantHeight * scale) {
                    Vector3 world = (now - before) / dt;
                    Vector3 body = (nowLocal - beforeLocal) / dt;
                    world.y = 0f;
                    body.y = 0f;
                    // At playback speed 1, for a Wallit at scale 1.
                    float implied = body.magnitude / Mathf.Max(0.3f, AnimSpeed);
                    WallitStride.Add(moveClip, world.magnitude / scale, implied);
                    float d = DefaultStride[moveClip];
                    if (implied > d * 0.5f && implied < d * 2f) {
                        stride[moveClip] = Mathf.Lerp(stride[moveClip], implied, 0.03f);
                    }
                }
            }
        }
        footLWorld = wl;
        footRWorld = wr;
        footLLocal = ll;
        footRLocal = lr;
        hipsLocal = lh;
        haveFeet = true;
    }

    // Idle or a locomotion clip: the poses where the hips have no business jumping.
    static bool OnFoot(AnimatorStateInfo info) {
        for (int i = CLIP_IDLE; i <= CLIP_BACK_PEDAL; i++) {
            if (info.IsName(ClipNames[i])) {
                return true;
            }
        }
        return false;
    }

    float VisualScale {
        get { return Mathf.Max(0.2f, model != null ? model.lossyScale.y : transform.lossyScale.y); }
    }

    public float Stride(int clip) {
        return clip >= 0 && clip < stride.Length ? stride[clip] : 0f;
    }

    bool Aiming() {
        return ZeldaCam != null && ZeldaCam.LockedOnGameObject != null;
    }

    // Frame-rate independent ease toward a facing (a plain Slerp by deltaTime * rate snaps once a frame is long).
    void FaceToward(Quaternion target, float rate) {
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
    }

    void MovementAndJumping() {
        if (Anim == null) {
            Anim = EnsureWallitOn(gameObject);
        }
        if (Anim) {
            if (hasFireSpeed) {
                Anim.SetFloat(FireSpeedHash, 1.35f * RunBoons.DrawSpeed);
            }

            bool rooted = PlayerGrowth.Instance != null && PlayerGrowth.Instance.Rooted;
            if (rooted) {
                motor.Move(Vector3.zero);
                AnimationMove(Vector3.zero);
            } else if (ZeldaCam != null && ZeldaCam.LockedOnGameObject != null) {
                Vector2 input = Vector2.ClampMagnitude(new Vector2(PlayInput.GetAxis("Horizontal"), PlayInput.GetAxis("Vertical")), 1f);
                Vector3 toward = ZeldaCam.LockedOnGameObject.transform.position - transform.position;
                toward.y = 0;

                // Locked on, forward is toward the target and left and right circle it, whatever the camera lags to.
                Vector3 fwd = toward.sqrMagnitude > 0.01f ? toward.normalized : new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
                Vector3 right = new Vector3(fwd.z, 0, -fwd.x);
                var directionToMove = (fwd * input.y + right * input.x) * (Speed * 1.15f * BatEquipment.MoveFactor);

                if (toward.sqrMagnitude > 0.01f) {
                    FaceToward(Quaternion.LookRotation(toward.normalized), TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity);
            } else {
                Vector2 input = Vector2.ClampMagnitude(new Vector2(PlayInput.GetAxis("Horizontal"), PlayInput.GetAxis("Vertical")), 1f);
                Vector3 camFwd = ZeldaCam != null ? ZeldaCam.transform.forward : transform.forward;
                Vector3 camRight = ZeldaCam != null ? ZeldaCam.transform.right : transform.right;
                var cameraForward = new Vector3(camFwd.x * input.y, 0, camFwd.z * input.y);
                var cameraRight = new Vector3(camRight.x * input.x, 0, camRight.z * input.x);
                var directionToMove = cameraForward + cameraRight;
                directionToMove *= Speed * BatEquipment.MoveFactor;

                if (directionToMove.sqrMagnitude > 0.001f) {
                    FaceToward(Quaternion.LookRotation(directionToMove.normalized), FreeTurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity);
            }

            if (motor.charController.velocity.sqrMagnitude > 1.2f) {
                UpgradeLook.Stride(transform.position);
            }

            bool wantJump = WaveDirector.Wave >= Tuning.JumpWave
                ? PlayInput.GetKey(KeyCode.Space)
                : PlayInput.GetKeyDown(KeyCode.Space);
            bool groundedAct = Enemy.Climbers == 0 && (PlayerGrowth.Instance == null || !PlayerGrowth.Instance.Rooted);
            if (groundedAct && wantJump && motor.TryJump(SpeedJump)) {
                Anim.speed = 1f;
                PlayClip(Anim, CLIP_JUMP, true, 0.05f);
                jumpAnimLeft = 0.9f;
            }
        }
    }
}
