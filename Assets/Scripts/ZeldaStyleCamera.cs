using System.Collections.Generic;
using UnityEngine;


public class ZeldaStyleCamera : MonoBehaviour {
    [SerializeField]
    private float FirstPersonLookUpAndDownSpeed = 2.0f;
    [SerializeField]
    private float FirstPersonTurnLeftAndRightSpeed = 5.0f;
    private Animator Animator;
    [SerializeField]
    public GameObject Target;
    [SerializeField]
    private float MaxDistance;
    [SerializeField]
    private float MinDistance;
    [SerializeField]
    private float DistanceUp;
    private float CurrentCameraDistance;

    [SerializeField]
    private float LeftAndRightLockOnDistance = 2.0f;

    private float TimeStoppedMoving;
    private Vector3 LastChaaracterPos;
    private bool StoppedMoving;

    [SerializeField]
    private float Smooth;
    private GameObject CrosshairObj;
    private Transform FollowXForm;

    private Vector3 TargetPosition;
    private Vector3 LookDir;
    private Vector3 VelocityCamSmooth = Vector3.zero;
    [SerializeField]
    private float LockedOnCamSpeed = 20.0f;
    [SerializeField]
    private float CamSmoothDampTime = 0.1f;
    [SerializeField]
    private float CamCirclingSmoothDampTime = 40.0f;
    [SerializeField]
    private float LockOnSnapSmoothDampTime = 20.0f;
    [SerializeField]
    private float UnlockDistance = 8.0f;

    float orbitYaw;
    float orbitPitch = 18f;
    public float BodyScale = 1f;
    float aimPitch = 8f;
    bool resumeLock;
    bool orbitReady;

    private bool HitWall;
    // Set by ConfigureForLevel: levels have real walls; CamBlock colliders also cover chapter kit walls.
    private bool levelMode;
    static readonly RaycastHit[] wallBuffer = new RaycastHit[12];
    // Sphere-cast pull-in: current distance along the look ray, with a faster pull and a slower ease-out.
    float followDist;
    float followDistVel;
    const float CamSphereRadius = 0.25f;
    const float PullInSmooth = 0.08f;
    const float PullOutSmooth = 0.28f;
    const float IdleRecentreSeconds = 2f;
    // Capped turn rate for every eased yaw (idle recentre, lock-on, release) and for the final view, so a slow frame never snaps.
    const float MaxYawRate = 200f;
    const float MaxViewRate = 300f;
    Quaternion lastViewRot;
    Vector3 lastViewTarget;
    bool viewPrimed;
    const float MinFov = 60f;
    const float MaxFov = 75f;
    const float DefaultFov = 65f;
    float lockYawVel;
    float lastLockSnapDeg;
    bool wasLocked;
    // Touch only: the nearest bot in swing reach is locked without the camera turning to it (no Lock button, no Shift).
    bool softLock;
    float lastDragAt = -10f;
    public static float FeelOrbitYaw;
    public static float FeelMoveYaw = -1f;   // world yaw of the stick's heading, -1 with the stick at rest
    public static float FeelDragAt = -10f;
    const float TouchFollowRate = 1.6f;
    float unlockYaw;
    float unlockBlend;   // 1 = just released, eases to 0
    public static float FeelFollowDist;
    public static float FeelFov;
    public static bool FeelRecentred;
    public static float FeelMaxLockSnapDeg;
    public static float FeelPullInMeters;
    public CamStates CamState = CamStates.Behind;
    [SerializeField]
    private Vector2 FirstPersonXAxisClamp = new Vector2(-85.0f, 85.0f);
    public const string PARAMETER_STATE = "State";
    public GameObject XHair;
    private Vector3 Pos;
    private LockOn LockOn;
    public GameObject LockedOnGameObject;
    private bool LockOnSnap;
    readonly List<Enemy> lockList = new List<Enemy>();
    System.Comparison<Enemy> byDistance;
    Vector3 lockOrigin;
    public static int LockSwitches;
    static float shakeAmount;
    static float shakeUntil;
    static float shakeLength = 0.2f;
    Vector3 shakeOffset;
    [SerializeField]
    private GameObject LockedOnCrosshair;
    [SerializeField]
    private GameObject FirstPersonCamPos;
    
    public enum CamStates {
        Behind,
        Target,
        Circling,
        FirstPerson,
        Free,
        LockOn
    }

    // A camera made in code (the campaign levels, Level*.cs) takes the numbers scene2's camera has serialized.
    public void ConfigureForLevel(GameObject follow, GameObject crosshair, GameObject firstPerson) {
        Target = follow;
        LockedOnCrosshair = crosshair;
        FirstPersonCamPos = firstPerson;
        levelMode = true;
        MaxDistance = 5f;
        MinDistance = 3f;
        DistanceUp = 2f;
        LeftAndRightLockOnDistance = 2f;
        Smooth = 1f;
        LockedOnCamSpeed = 10f;
        CamSmoothDampTime = 0.12f;
        CamCirclingSmoothDampTime = 40f;
        LockOnSnapSmoothDampTime = 0.35f;
        UnlockDistance = 5f;
        FirstPersonLookUpAndDownSpeed = 2f;
        FirstPersonTurnLeftAndRightSpeed = 5f;
        ClampFov();
    }

    void Start() {
        LastChaaracterPos = Target.transform.position;
        FollowXForm = GameObject.FindWithTag("Player").transform;
        Animator = GameObject.FindWithTag("playerchild").GetComponent<Animator>();
        LockOn = GameObject.Find("LockOn").GetComponent<LockOn>();
        LookDir = FollowXForm.forward;
        orbitYaw = FollowXForm.eulerAngles.y;
        followDist = MaxDistance;
        ClampFov();
        CameraFeel.Ensure(GetComponent<Camera>());
        CamBlock.EnsureMatrix();
        LockMouse();
        Cursor.visible = false;
    }

    void ClampFov() {
        Camera cam = GetComponent<Camera>();
        if (cam == null) {
            cam = Camera.main;
        }
        if (cam == null) {
            return;
        }
        if (cam.nearClipPlane < 0.1f) {
            cam.nearClipPlane = 0.15f;
        }
        if (cam.fieldOfView < MinFov || cam.fieldOfView > MaxFov) {
            cam.fieldOfView = DefaultFov;
        } else {
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, MinFov, MaxFov);
        }
        FeelFov = cam.fieldOfView;
    }

    // Phones and tablets have no pointer lock: asking logs "Cursor locking is not supported on this browser." each frame.
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesGuardPointerLock();
    static bool pointerGuarded;
#endif

    static void LockMouse() {
        if (!UiFit.Touch) {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The browser rejects a lock asked for without a click; keep that from being an unhandled rejection.
            if (!pointerGuarded) {
                pointerGuarded = true;
                HawkesGuardPointerLock();
            }
#endif
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void Update() {
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;
        Vector3 characterOffset = FollowXForm.position + new Vector3(0f, DistanceUp, 0f);
        if (!orbitReady && FollowXForm != null) {
            Vector3 fromPlayer = transform.position - FollowXForm.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude > 0.25f) {
                orbitYaw = Mathf.Atan2(fromPlayer.x, fromPlayer.z) * Mathf.Rad2Deg;
                CurrentCameraDistance = Mathf.Clamp(fromPlayer.magnitude, MinDistance, MaxDistance);
            }
            orbitReady = true;
        }

        AcquireTarget();
        CamState = CamStates.Behind;
        if (WaveDirector.IsBuilding || LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen) {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        } else {
            LockMouse();
            Cursor.visible = false;
        }

        TickIdleRecentre();
        TickTouchFollow();
        TickLockEase();

        switch (CamState) {
            case CamStates.Behind:
                if (!WaveDirector.IsBuilding && !LevelUpScreen.IsOpen && !PauseMenu.IsOpen && !Cutscene.IsOpen && !HardLocked && unlockBlend <= 0f) {
                    float mx = PlayInput.GetAxis("Mouse X");
                    float my = PlayInput.GetAxis("Mouse Y");
                    if (Mathf.Abs(mx) > 0.001f || Mathf.Abs(my) > 0.001f) {
                        lastDragAt = Time.unscaledTime;
                        FeelDragAt = lastDragAt;
                        StoppedMoving = false;
                        FeelRecentred = false;
                    }
                    orbitYaw += mx * 3.4f;
                    orbitPitch = Mathf.Clamp(orbitPitch - my * 2.1f, 5f, Mathf.Lerp(58f, 84f, Mathf.Clamp01((BodyScale - 1f) / 5f)));
                }
                if (CurrentCameraDistance < MinDistance) {
                    CurrentCameraDistance = MaxDistance;
                }
                LookDir = Quaternion.Euler(0f, orbitYaw, 0f) * Vector3.forward;
                float body = Mathf.Max(1f, BodyScale);
                float camDist = CurrentCameraDistance * Mathf.Lerp(1f, body, 0.18f);
                TargetPosition = FollowXForm.position + Vector3.up * (DistanceUp * body) + Quaternion.Euler(orbitPitch, orbitYaw, 0f) * new Vector3(0f, 0f, -camDist);
                break;
            case CamStates.Target:
                LookDir = FollowXForm.forward;
                TargetPosition = characterOffset + FollowXForm.up * DistanceUp - LookDir * MaxDistance;
                break;
            case CamStates.Circling:
                if (!HitWall) {
                    var rotateAroundVector = (Target.transform.position + (Quaternion.Euler(new Vector3(0, 0, 0)) *
                        ((FollowXForm.position + FollowXForm.up * DistanceUp - Target.transform.forward * MaxDistance) - Target.transform.position)));

                    TargetPosition = transform.position = Vector3.SmoothDamp(transform.position, rotateAroundVector, ref VelocityCamSmooth, CamCirclingSmoothDampTime * Time.deltaTime);
                }
                break;
            case CamStates.LockOn:
                LockedOnCrosshair.GetComponentInChildren<SpriteRenderer>().enabled = true;
                LockedOnCrosshair.transform.position = LockedOnGameObject.transform.position;
                LockedOnCrosshair.transform.LookAt(transform.position);
                var dirFromPlayerToLockedOnObject = Target.transform.position - LockedOnGameObject.transform.position;
                dirFromPlayerToLockedOnObject.Normalize();

                if (!LockOnSnap) {
                    var lockOnSnapVector = (Target.transform.position + (Quaternion.Euler(new Vector3(0, 0, 0)) *
                        ((FollowXForm.position + FollowXForm.up * DistanceUp + dirFromPlayerToLockedOnObject * MinDistance) - Target.transform.position)));

                    if (!HitWall) {
                        transform.position = Vector3.SmoothDamp(transform.position, lockOnSnapVector, ref VelocityCamSmooth, LockOnSnapSmoothDampTime * Time.deltaTime);
                    }
                    if ((transform.position - lockOnSnapVector).magnitude < 0.1f || HitWall) {
                        LockOnSnap = true;
                    }
                } else {
                    if (!HitWall) {
                        var leftTarget = (Target.transform.position + (Quaternion.Euler(new Vector3(0, 0, 0)) *
                        ((FollowXForm.position + FollowXForm.up * (DistanceUp / 2) + dirFromPlayerToLockedOnObject * MinDistance) - Target.transform.position))) - Vector3.Cross(dirFromPlayerToLockedOnObject, Vector3.up) * LeftAndRightLockOnDistance;

                        var rightTarget = (Target.transform.position + (Quaternion.Euler(new Vector3(0, 0, 0)) *
                            ((FollowXForm.position + FollowXForm.up * (DistanceUp / 2) + dirFromPlayerToLockedOnObject * MinDistance) - Target.transform.position))) + Vector3.Cross(dirFromPlayerToLockedOnObject, Vector3.up) * LeftAndRightLockOnDistance;

                        var rightTargetDistance = Vector3.Distance(transform.position, rightTarget);
                        var leftTargetDistance = Vector3.Distance(transform.position, leftTarget);
                        if (rightTargetDistance > leftTargetDistance) {
                            TargetPosition = Vector3.Lerp(transform.position, leftTarget, LockedOnCamSpeed * leftTargetDistance * Time.deltaTime);
                            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, CamSmoothDampTime * Time.deltaTime);
                        } else {
                            TargetPosition = Vector3.Lerp(transform.position, rightTarget, LockedOnCamSpeed * rightTargetDistance * Time.deltaTime);
                            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, CamSmoothDampTime * Time.deltaTime);
                        }
                    }
                }
                transform.LookAt((Target.transform.position + LockedOnGameObject.transform.position) / 2);

                if ((Target.transform.position - LockedOnGameObject.transform.position).magnitude > UnlockDistance) {
                    LockedOnCrosshair.GetComponentInChildren<SpriteRenderer>().enabled = false;
                    LockedOnGameObject = null;
                    CamState = CamStates.Behind;
                }
                break;
            case CamStates.FirstPerson:
                orbitYaw += PlayInput.GetAxis("Mouse X") * 2.6f;
                aimPitch = Mathf.Clamp(aimPitch - PlayInput.GetAxis("Mouse Y") * 1.7f, -42f, 48f);
                FollowXForm.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
                transform.position = FirstPersonCamPos.transform.position;
                transform.rotation = Quaternion.Euler(aimPitch, orbitYaw, 0f);
                TargetPosition = transform.position;
                break;
        }

        LastChaaracterPos = Target.transform.position;
        if (CamState != CamStates.Circling && CamState != CamStates.LockOn && CamState != CamStates.Target && CamState != CamStates.FirstPerson) {
            // Critically-damped follow: CamSmoothDampTime is a seconds constant (was wrongly scaled by deltaTime).
            float damp = Mathf.Max(0.04f, CamSmoothDampTime > 1f ? 0.12f : CamSmoothDampTime);
            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, damp);
        }
        if (CamState == CamStates.Target) {
            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, 0.2f);
        }

        CompensateForWalls(characterOffset, ref TargetPosition);
        transform.LookAt(FollowXForm.position + Vector3.up * Mathf.Max(1f, DistanceUp * Mathf.Max(1f, BodyScale)));
        LimitViewTurn();
        ShowLockMarker();
        ApplyShake();
        FeelFollowDist = followDist > 0f ? followDist : Vector3.Distance(transform.position, characterOffset);
    }

    // A lock the camera turns to (Shift on desktop); the touch soft lock only aims Wallit and the bat.
    bool HardLocked {
        get { return LockedOnGameObject != null && !softLock; }
    }

    // Touch: the camera swings behind the way the stick is pushed, damped and capped; a drag turns it and is left alone for a
    // moment; with no input at all TickIdleRecentre brings it behind Wallit after IdleRecentreSeconds.
    void TickTouchFollow() {
        FeelOrbitYaw = orbitYaw;
        FeelMoveYaw = -1f;
        if (!UiFit.Touch || FollowXForm == null || HardLocked || WaveDirector.IsBuilding || LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen) {
            return;
        }
        float h = PlayInput.GetAxis("Horizontal");
        float v = PlayInput.GetAxis("Vertical");
        if (h * h + v * v < 0.09f) {
            return;
        }
        float angle = Mathf.Atan2(h, v) * Mathf.Rad2Deg;   // 0 = straight ahead of the camera
        FeelMoveYaw = Mathf.Repeat(orbitYaw + angle, 360f);
        if (Mathf.Abs(angle) > 110f || Time.unscaledTime - lastDragAt < 0.8f) {
            return;   // walking back toward the camera never flips it; a drag in progress is respected
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float want = orbitYaw + Mathf.Clamp(angle, -60f, 60f);
        orbitYaw = LimitYaw(orbitYaw, Mathf.LerpAngle(orbitYaw, want, 1f - Mathf.Exp(-TouchFollowRate * dt)));
        StoppedMoving = false;
        FeelRecentred = false;
    }

    float LimitYaw(float from, float to) {
        float max = MaxYawRate * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        return from + Mathf.Clamp(Mathf.DeltaAngle(from, to), -max, max);
    }

    // Final guard on the view: the turn in one frame never exceeds MaxViewRate (at most 0.05 s of it), unless Wallit was moved
    // by a cut (teleport, scene start), which restarts the guard.
    void LimitViewTurn() {
        Vector3 at = Target != null ? Target.transform.position : Vector3.zero;
        if (viewPrimed && (at - lastViewTarget).sqrMagnitude > 64f) {
            viewPrimed = false;
        }
        lastViewTarget = at;
        if (viewPrimed) {
            float max = MaxViewRate * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (Quaternion.Angle(lastViewRot, transform.rotation) > max) {
                transform.rotation = Quaternion.RotateTowards(lastViewRot, transform.rotation, max);
            }
        }
        lastViewRot = transform.rotation;
        viewPrimed = true;
    }

    // After ~2 s with no look input and no lock, ease the orbit back behind Wallit's facing.
    void TickIdleRecentre() {
        if (FollowXForm == null || HardLocked || WaveDirector.IsBuilding || LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen) {
            StoppedMoving = false;
            return;
        }
        float mx = Mathf.Abs(PlayInput.GetAxis("Mouse X"));
        float my = Mathf.Abs(PlayInput.GetAxis("Mouse Y"));
        if (mx > 0.02f || my > 0.02f) {
            StoppedMoving = false;
            FeelRecentred = false;
            return;
        }
        if (!StoppedMoving) {
            StoppedMoving = true;
            TimeStoppedMoving = Time.unscaledTime;
            return;
        }
        if (Time.unscaledTime - TimeStoppedMoving < IdleRecentreSeconds) {
            return;
        }
        float wantYaw = FollowXForm.eulerAngles.y;
        float before = orbitYaw;
        orbitYaw = LimitYaw(orbitYaw, Mathf.LerpAngle(orbitYaw, wantYaw, 1f - Mathf.Exp(-3.2f * Mathf.Min(Time.unscaledDeltaTime, 0.05f))));
        orbitPitch = Mathf.Lerp(orbitPitch, 18f, 1f - Mathf.Exp(-2.4f * Time.unscaledDeltaTime));
        if (Mathf.Abs(Mathf.DeltaAngle(orbitYaw, wantYaw)) < 2.5f && Mathf.Abs(orbitPitch - 18f) < 2f) {
            FeelRecentred = true;
        }
        // Keep TimeStoppedMoving so we stay in the recentre until the player looks again.
        if (Mathf.Abs(Mathf.DeltaAngle(before, orbitYaw)) > 0.01f) {
            // still easing
        }
    }

    // Lock-on eases the orbit behind Wallit facing the target; release eases yaw back toward Wallit's facing.
    void TickLockEase() {
        if (FollowXForm == null) {
            return;
        }
        bool locked = HardLocked;
        if (wasLocked && !locked) {
            unlockYaw = orbitYaw;
            unlockBlend = 1f;
            lockYawVel = 0f;
        }
        wasLocked = locked;
        if (locked) {
            unlockBlend = 0f;
            Vector3 to = LockedOnGameObject.transform.position - FollowXForm.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.05f) {
                return;
            }
            // Camera sits opposite the target so Wallit stands between camera and lock.
            float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 180f;
            float before = orbitYaw;
            orbitYaw = LimitYaw(orbitYaw, Mathf.SmoothDampAngle(orbitYaw, wantYaw, ref lockYawVel, 0.35f, MaxYawRate, Mathf.Min(Time.unscaledDeltaTime, 0.05f)));
            lastLockSnapDeg = Mathf.Abs(Mathf.DeltaAngle(before, orbitYaw));
            if (lastLockSnapDeg > FeelMaxLockSnapDeg) {
                FeelMaxLockSnapDeg = lastLockSnapDeg;
            }
            return;
        }
        if (unlockBlend > 0f) {
            float before = orbitYaw;
            float want = FollowXForm.eulerAngles.y;
            unlockBlend = Mathf.MoveTowards(unlockBlend, 0f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) / 0.35f);
            float t = 1f - unlockBlend;
            // Smoothstep from the release yaw toward behind Wallit.
            float k = t * t * (3f - 2f * t);
            orbitYaw = LimitYaw(orbitYaw, Mathf.LerpAngle(unlockYaw, want, k));
            lastLockSnapDeg = Mathf.Abs(Mathf.DeltaAngle(before, orbitYaw));
            if (lastLockSnapDeg > FeelMaxLockSnapDeg) {
                FeelMaxLockSnapDeg = lastLockSnapDeg;
            }
        }
    }

    // Hold Shift to lock on to the nearest bird in reach. The lock stays on that bird while it lives and stays
    // close; right click (or Tab) moves it to the next nearest, round the list.
    void AcquireTarget() {
        bool holding = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
        if (UiFit.Touch && !holding) {
            AcquireSoft();
            return;
        }
        softLock = false;
        if (!holding || WaveDirector.IsBuilding) {
            LockedOnGameObject = null;
            return;
        }
        float reach = LockReach();
        Vector3 origin = FollowXForm.position;
        lockList.Clear();
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing || enemy.Dormant) {
                continue;
            }
            if ((enemy.transform.position - origin).sqrMagnitude <= reach * reach) {
                lockList.Add(enemy);
            }
        }
        if (lockList.Count == 0) {
            LockedOnGameObject = null;
            return;
        }
        lockOrigin = origin;
        if (byDistance == null) {
            byDistance = (a, b) => (a.transform.position - lockOrigin).sqrMagnitude.CompareTo((b.transform.position - lockOrigin).sqrMagnitude);
        }
        lockList.Sort(byDistance);

        int current = -1;
        if (LockedOnGameObject != null) {
            for (int i = 0; i < lockList.Count; i++) {
                if (lockList[i].gameObject == LockedOnGameObject) {
                    current = i;
                    break;
                }
            }
        }
        bool cycle = !LevelUpScreen.IsOpen && !PauseMenu.IsOpen && !Cutscene.IsOpen && (PlayInput.GetMouseButtonDown(1) || PlayInput.GetKeyDown(KeyCode.Tab));
        if (cycle && current >= 0) {
            LockedOnGameObject = lockList[(current + 1) % lockList.Count].gameObject;
            LockSwitches++;
        } else if (current < 0) {
            LockedOnGameObject = lockList[0].gameObject;
        }
    }

    // Touch: the nearest bot within a little past the bat's reach, kept until another is clearly nearer. No button, no cycling.
    void AcquireSoft() {
        softLock = true;
        if (FollowXForm == null || WaveDirector.IsBuilding || LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen) {
            LockedOnGameObject = null;
            return;
        }
        Vector3 origin = FollowXForm.position;
        float reach = BatEquipment.Reach * 1.5f;
        Enemy best = null;
        float bestSqr = reach * reach;
        Enemy held = null;
        List<Enemy> roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy enemy = roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing || enemy.Dormant) {
                continue;
            }
            float sqr = (enemy.transform.position - origin).sqrMagnitude;
            if (LockedOnGameObject == enemy.gameObject && sqr <= reach * reach * 1.2f) {
                held = enemy;
            }
            if (sqr < bestSqr) {
                bestSqr = sqr;
                best = enemy;
            }
        }
        if (held != null && best != null && best != held) {
            float heldSqr = (held.transform.position - origin).sqrMagnitude;
            if (heldSqr < bestSqr * 1.69f) {   // the new one must be a third closer to take over
                best = held;
            }
        } else if (held != null && best == null) {
            best = held;
        }
        LockedOnGameObject = best != null ? best.gameObject : null;
    }

    // How far the lock reaches: a walk, not a bowshot. Grows with you.
    public float LockReach() {
        float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
        return Mathf.Min(90f, 26f + Mathf.Max(0, WaveDirector.Wave - 1) * 0.6f) * Mathf.Lerp(1f, grown, 0.5f);
    }

    // A small jolt when the bat connects. Unscaled, so it still plays through hit-stop.
    public static void Shake(float amount, float seconds) {
        if (ComfortOptions.ReducedMotion) {
            return;
        }
        shakeAmount = Mathf.Max(shakeAmount * Mathf.Clamp01((shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, shakeLength)), amount);
        shakeLength = Mathf.Max(0.02f, seconds);
        shakeUntil = Time.unscaledTime + shakeLength;
    }

    // A push of the camera toward the hit that dies away on its own (quadratic ease, `seconds` long), with a short FOV
    // squeeze. Only BatEquipment calls it. Reduce Motion callers don't reach it (Juice.Contact), and it refuses too.
    static Vector3 kickDir;
    static float kickAmount, kickFov, kickLength = 0.2f, kickStart = -10f;
    static float kickBaseFov;
    public static float KickDecaySeconds { get; private set; }
    public static float KickPeak { get; private set; }
    public static void Kick(Vector3 toward, float meters, float fovDegrees, float seconds) {
        if (ComfortOptions.ReducedMotion) {
            return;
        }
        toward.y = 0f;
        if (toward.sqrMagnitude < 0.0001f) {
            return;
        }
        kickDir = toward.normalized;
        kickAmount = meters;
        kickFov = fovDegrees;
        kickLength = Mathf.Clamp(seconds, 0.05f, 0.29f);
        kickStart = Time.unscaledTime;
        KickPeak = meters;
        KickDecaySeconds = kickLength;
    }

    // Returns the offset to add this frame and drives the FOV punch.
    Vector3 KickOffset() {
        Camera view = GetComponent<Camera>();
        float t = (Time.unscaledTime - kickStart) / kickLength;
        if (t >= 1f || t < 0f || kickAmount <= 0f) {
            if (view != null && kickBaseFov > 0f) {
                view.fieldOfView = kickBaseFov;
                kickBaseFov = 0f;
            }
            return Vector3.zero;
        }
        float fade = (1f - t) * (1f - t);
        if (view != null && kickFov > 0f) {
            if (kickBaseFov <= 0f) {
                kickBaseFov = view.fieldOfView;
            }
            view.fieldOfView = kickBaseFov - kickFov * fade;
        }
        return kickDir * kickAmount * fade * Mathf.Sqrt(Mathf.Max(1f, BodyScale));
    }

    void ApplyShake() {
        Vector3 kick = KickOffset();
        if (kick != Vector3.zero) {
            shakeOffset += kick;
            transform.position += kick;
        }
        float left = shakeUntil - Time.unscaledTime;
        if (left <= 0f || shakeAmount <= 0f || LevelUpScreen.IsOpen || PauseMenu.IsOpen) {
            return;
        }
        float fade = left / shakeLength;
        float body = Mathf.Max(1f, BodyScale);
        Vector3 jolt = (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * shakeAmount * fade * Mathf.Sqrt(body);
        shakeOffset += jolt;
        transform.position += jolt;
    }

    void ShowLockMarker() {
        if (LockedOnCrosshair == null) {
            return;
        }
        SpriteRenderer marker = LockedOnCrosshair.GetComponentInChildren<SpriteRenderer>();
        if (marker == null) {
            return;
        }
        marker.enabled = LockedOnGameObject != null;
        if (LockedOnGameObject == null) {
            return;
        }
        float mark = 0.32f * Mathf.Max(0.45f, LockedOnGameObject.transform.lossyScale.y);
        LockedOnCrosshair.transform.position = LockedOnGameObject.transform.position + Vector3.up * mark;
        LockedOnCrosshair.transform.LookAt(transform.position);
    }

    public Vector3 AimRay() {
        Camera view = GetComponent<Camera>();
        if (view == null) {
            view = Camera.main;
        }
        if (view == null) {
            return ViewAim();
        }
        Ray ray = view.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (ray.direction.sqrMagnitude < 0.0001f) {
            return ViewAim();
        }
        return ray.direction.normalized;
    }

    public Vector3 ManualAim(Vector3 from) {
        Camera view = GetComponent<Camera>();
        if (view == null) {
            view = Camera.main;
        }
        if (view == null) {
            return ViewAim();
        }

        Ray ray = view.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 220f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Vector3 point = ray.GetPoint(80f);
        for (int i = 0; i < hits.Length; i++) {
            Transform hit = hits[i].collider != null ? hits[i].collider.transform : null;
            if (hit == null || IsShooter(hit)) {
                continue;
            }
            if (hits[i].distance < nearest) {
                nearest = hits[i].distance;
                point = hits[i].point;
            }
        }

        Vector3 dir = point - from;
        if (dir.sqrMagnitude < 0.04f) {
            dir = ray.direction;
        }
        return dir.normalized;
    }

    bool IsShooter(Transform hit) {
        if (FollowXForm == null) {
            return false;
        }
        return hit == FollowXForm || hit.IsChildOf(FollowXForm);
    }

    public Vector3 ViewAim() {
        if (CamState == CamStates.FirstPerson) {
            return transform.forward;
        }

        Vector3 flat = FollowXForm.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) {
            flat = FollowXForm.forward;
        }
        float drop = Mathf.Lerp(orbitPitch * 0.5f, orbitPitch * 0.95f, Mathf.Clamp01((BodyScale - 1f) / 5f));
        drop = Mathf.Clamp(drop, 6f, 78f);
        return Quaternion.AngleAxis(-drop, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
    }

    void ToggleLock() {
        if (CamState == CamStates.LockOn) {
            LockedOnCrosshair.GetComponentInChildren<SpriteRenderer>().enabled = false;
            LockedOnGameObject = null;
            resumeLock = false;
            CamState = CamStates.Behind;
            return;
        }

        if (LockOn.NearestObject == null) {
            return;
        }

        LockOnSnap = false;
        LockedOnGameObject = LockOn.NearestObject;
        CamState = CamStates.LockOn;
    }

    void SwitchLock(int side) {
        if (LockOn == null || LockedOnGameObject == null) {
            return;
        }

        GameObject next = LockOn.Neighbor(LockedOnGameObject, side);
        if (next == null) {
            return;
        }

        LockOnSnap = false;
        LockedOnGameObject = next;
    }

    private void CompensateForWalls(Vector3 fromObject, ref Vector3 toTarget) {
        if (FollowXForm == null) {
            return;
        }
        // Desired spot is toTarget (or the already-smoothed transform when LockOn wrote itself).
        Vector3 desired = toTarget;
        if (CamState == CamStates.LockOn || CamState == CamStates.Circling) {
            desired = transform.position;
        }
        Vector3 toCam = desired - fromObject;
        float want = toCam.magnitude;
        if (want < 0.05f) {
            return;
        }
        Vector3 dir = toCam / want;
        float blocked = want;
        HitWall = false;
        // Sphere cast from Wallit's head toward the camera; radius at or above the near plane.
        Camera cam = GetComponent<Camera>();
        float radius = Mathf.Max(CamSphereRadius, cam != null ? cam.nearClipPlane : 0.15f);
        int n = Physics.SphereCastNonAlloc(fromObject, radius, dir, wallBuffer, want, CamBlock.SolidMask(), QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++) {
            Collider c = wallBuffer[i].collider;
            if (c == null || CamBlock.ShouldIgnore(c, FollowXForm)) {
                continue;
            }
            best = Mathf.Min(best, wallBuffer[i].distance);
        }
        if (best < float.MaxValue) {
            blocked = Mathf.Max(0.7f, best - radius * 0.25f);
            HitWall = true;
        }
        if (followDist <= 0.01f) {
            followDist = want;
        }
        // Pull-in is snappy; ease-out is slower so the camera does not pop through a cleared wall.
        float smooth = blocked < followDist - 0.02f ? PullInSmooth : PullOutSmooth;
        float before = followDist;
        followDist = Mathf.SmoothDamp(followDist, Mathf.Min(want, blocked), ref followDistVel, smooth);
        if (HitWall && before - followDist > 0.01f) {
            FeelPullInMeters += before - followDist;
        }
        Vector3 pulled = fromObject + dir * followDist;
        transform.position = pulled;
        toTarget = pulled;
    }
    public void LockOnGameObject(GameObject lockedOnGameObject) {
        if (CamState != CamStates.LockOn) {
            LockOnSnap = false;
            LockedOnGameObject = lockedOnGameObject;
            CamState = CamStates.LockOn;
        }
    }

    public float ClampAngle(float currentValue, float minAngle, float maxAngle, float clampAroundAngle = 0) {
        var angle = currentValue - (clampAroundAngle + 180);
        while (angle < 0) {
            angle += 360;
        }
        angle = Mathf.Repeat(angle, 360);

        return Mathf.Clamp(angle - 180, minAngle, maxAngle) + 360 + clampAroundAngle;
    }
}