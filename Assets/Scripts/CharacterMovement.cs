using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMovement : MonoBehaviour {
    private MovementMotor motor;
    public float MoveMagnitude = 0.05f;
    public float Speed = 4.0f;
    public float SpeedMoveWhileAttack = 0.1f;
    public float SpeedAttack = 1.5f;
    public float TurnSpeed = 20f;
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

    public static readonly int[] ClipCounts = new int[17];
    public static int LastClip { get; private set; } = -1;

    static readonly int ClipHash = Animator.StringToHash(PARAMETER_CLIP);
    static readonly int StateHash = Animator.StringToHash(PARAMETER_STATE);
    static readonly int FireSpeedHash = Animator.StringToHash("FireSpeed");
    bool hasFireSpeed;

    public static void ResetClipCounts() {
        for (int i = 0; i < ClipCounts.Length; i++) {
            ClipCounts[i] = 0;
        }
        LastClip = -1;
    }

    public static Animator EnsureWallitOn(GameObject player) {
        if (player == null) {
            return null;
        }

        Transform existing = player.transform.Find("Wallit");
        if (existing != null && existing.gameObject.activeSelf) {
            existing.gameObject.tag = "playerchild";
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

    public static void PlayClip(Animator anim, int clipId, bool forceRestart = false, float crossFade = 0.06f) {
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
            anim.SetInteger(ClipHash, clipId);
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
                    anim.CrossFade(stateHash, crossFade, 0, 0f);
                }
            }
        }
    }

    void Awake() {
        ResetClipCounts();
        motor = GetComponent<MovementMotor>();
        Anim = EnsureWallitOn(gameObject);
    }

    void Start() {
        if (Anim == null) {
            Anim = EnsureWallitOn(gameObject);
        }
        if (Anim != null) {
            Anim.applyRootMotion = false;
            foreach (AnimatorControllerParameter parameter in Anim.parameters) {
                if (parameter.nameHash == FireSpeedHash) {
                    hasFireSpeed = true;
                }
            }
        }
    }

    void Update() {
        MovementAndJumping();
    }

    void AnimationMove(float magnitude, Vector3 worldMove, float hori, float vert) {
        if (Anim == null) {
            return;
        }
        if (WaveDirector.IsLost) {
            return;
        }
        if (BatEquipment.IsAnimatingAction) {
            return;
        }
        if (PlayerGrowth.Instance != null && PlayerGrowth.Instance.IsPlayingActionClip) {
            if (magnitude > MoveMagnitude && PlayerGrowth.Instance.IsPlayingVictory) {
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

        if (magnitude > MoveMagnitude) {
            float speedAnimation = Mathf.Clamp(magnitude * 1.6f, 1f, 1.45f);
            Anim.speed = speedAnimation;
            if (Aiming()) {
                Vector3 flat = new Vector3(worldMove.x, 0f, worldMove.z);
                Vector3 local = flat.sqrMagnitude > 0.0001f
                    ? transform.InverseTransformDirection(flat.normalized)
                    : new Vector3(hori, 0f, vert);
                int clip;
                if (Mathf.Abs(hori) > 0.2f && Mathf.Abs(hori) >= Mathf.Abs(vert) * 0.65f) {
                    clip = hori < 0f ? CLIP_STRAFE_LEFT : CLIP_STRAFE_RIGHT;
                } else if (Mathf.Abs(local.x) > 0.25f && Mathf.Abs(local.x) >= Mathf.Abs(local.z) * 0.65f) {
                    clip = local.x < 0f ? CLIP_STRAFE_LEFT : CLIP_STRAFE_RIGHT;
                } else if (vert < -0.2f || local.z < -0.2f) {
                    clip = CLIP_BACK_PEDAL;
                } else {
                    clip = CLIP_RUN;
                }
                PlayClip(Anim, clip, false, 0.08f);
            } else {
                PlayClip(Anim, CLIP_RUN, false, 0.08f);
            }
        } else {
            Anim.speed = 1f;
            PlayClip(Anim, CLIP_IDLE, false, 0.08f);
        }
    }

    bool Aiming() {
        return ZeldaCam != null && ZeldaCam.LockedOnGameObject != null;
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
                AnimationMove(0f, Vector3.zero, 0f, 0f);
            } else if (ZeldaCam != null && ZeldaCam.LockedOnGameObject != null) {
                var vert = PlayInput.GetAxis("Vertical");
                var hori = PlayInput.GetAxis("Horizontal");
                var directionTowardLockedOnObject = ZeldaCam.LockedOnGameObject.transform.position - transform.position;
                directionTowardLockedOnObject.y = 0;

                var cameraForward = new Vector3(ZeldaCam.transform.forward.x, 0, ZeldaCam.transform.forward.z) * 4;
                var cameraRight = new Vector3(ZeldaCam.transform.right.x, 0, ZeldaCam.transform.right.z) * 4;
                var playerForward = new Vector3(transform.forward.x, 0, transform.forward.z);
                var playerRight = new Vector3(transform.right.x, 0, transform.right.z);
                playerForward += cameraForward * 4;
                playerRight += cameraRight * 4;

                var directionToMove = ((cameraForward + playerForward) * vert) + ((cameraRight + playerRight) * hori);
                directionToMove = directionToMove / 16 * Speed * BatEquipment.MoveFactor;

                if (directionTowardLockedOnObject.sqrMagnitude > 0.01f) {
                    var newRotation = Quaternion.LookRotation(directionTowardLockedOnObject.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity.magnitude * 0.1f, directionToMove, hori, vert);
            } else {
                var vert = PlayInput.GetAxis("Vertical");
                var hori = PlayInput.GetAxis("Horizontal");
                Vector3 camFwd = ZeldaCam != null ? ZeldaCam.transform.forward : transform.forward;
                Vector3 camRight = ZeldaCam != null ? ZeldaCam.transform.right : transform.right;
                var cameraForward = new Vector3(camFwd.x * vert, 0, camFwd.z * vert);
                var cameraRight = new Vector3(camRight.x * hori, 0, camRight.z * hori);
                var directionToMove = cameraForward + cameraRight;
                directionToMove *= Speed * BatEquipment.MoveFactor;

                if (directionToMove.sqrMagnitude > 0.001f) {
                    var newRotation = Quaternion.LookRotation(directionToMove.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity.magnitude * 0.1f, directionToMove, hori, vert);
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
