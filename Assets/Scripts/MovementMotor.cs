using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementMotor : MonoBehaviour {
    public float GravityMultiplier = 1f;
    public float LerpTime = 10f;
    // Ground speed ramps in a straight line, so a start, stop or turn is one smooth change (m/s per second). The
    // walk animation follows the speed that results, so the feet keep up with it.
    public float Accel = 28f;
    public float Decel = 36f;
    // The ground velocity the controller moved at last frame, without the fall.
    public Vector3 GroundVelocity { get; private set; }
    public Animator Anim;
    private Vector3 MoveDirection = Vector3.zero;
    private Vector3 TargetDirection = Vector3.zero;
    private float FallVelocity = 0f;
    float jumpReadyAt;
    public float DistanceToGround = 0.1f;
    private bool IsGrounded;
    // When the feet last touched ground: a short grace (coyote) window keeps a jump pressed just off a ledge, and stops a
    // one-frame gap on a slope from flashing the jump clip or counting as a landing.
    float lastGroundedAt = -9f;
    public const float Coyote = 0.12f;
    private Collider MyCollider;

    [SerializeField]
    private ZeldaStyleCamera gamecam;

    [HideInInspector]
    public UnityEngine.CharacterController charController;


    void Awake() {
        charController = GetComponent<UnityEngine.CharacterController>();
        MyCollider = GetComponent<Collider>();
    }

    void Start() {
        DistanceToGround = MyCollider.bounds.extents.y;
        gamecam = GameObject.FindWithTag("MainCamera").GetComponent<ZeldaStyleCamera>();
        Anim = GetComponentInChildren<Animator>();
    }

    void Update() {
        IsGrounded = OnGround();
        if (IsGrounded) {
            lastGroundedAt = Time.time;
        }
        // Rising always falls back down. A stuck ground check used to freeze the jump speed and carry you upward.
        if (FallVelocity > 0.5f || !IsGrounded) {
            FallVelocity -= 90f * GravityMultiplier * Time.deltaTime;
        } else {
            // Hold to the ground hard enough to follow a downhill slope at full speed (a fixed -2 m/s left him hovering
            // off the yard's slopes above ~6 m/s, then "landing" with a stomp every few steps).
            float slope = Mathf.Tan(Mathf.Min(charController.slopeLimit, 40f) * Mathf.Deg2Rad);
            FallVelocity = -Mathf.Max(2f, GroundVelocity.magnitude * slope + 1f);
        }
        if (FallVelocity < -40f) {
            FallVelocity = -40f;
        }

        Vector3 flat = new Vector3(MoveDirection.x, 0f, MoveDirection.z);
        Vector3 want = new Vector3(TargetDirection.x, 0f, TargetDirection.z);
        float rate = want.sqrMagnitude >= flat.sqrMagnitude ? Accel : Decel;
        flat = Vector3.MoveTowards(flat, want, rate * Time.deltaTime);
        MoveDirection = new Vector3(flat.x, FallVelocity, flat.z);
        GroundVelocity = flat;
        charController.Move(MoveDirection * Time.deltaTime);
    }

    public bool OnGround() {
        if (charController == null) {
            return false;
        }

        // Ignore the jump itself. isGrounded can stay true for the frame you leave the floor.
        if (FallVelocity <= 0.5f && charController.isGrounded) {
            return true;
        }

        if (FallVelocity > 0.5f) {
            return false;
        }
        // A sphere the width of the capsule's foot, not one centre ray: it finds sloped ground under either edge, and its
        // reach grows with the capsule (PlayerGrowth scales radius and height).
        int mask = LayerMask.GetMask("Ground");
        if (mask == 0) {
            mask = 1 << 3;
        }
        float r = charController.radius;
        Vector3 origin = transform.position + charController.center + Vector3.down * (charController.height * 0.5f - r) + Vector3.up * 0.05f;
        RaycastHit hit;
        if (!Physics.SphereCast(origin, r * 0.9f, Vector3.down, out hit, 0.1f * r + charController.skinWidth + 0.25f, mask, QueryTriggerInteraction.Ignore)) {
            return false;
        }
        return Vector3.Angle(hit.normal, Vector3.up) <= charController.slopeLimit + 5f;
    }

    public void Move(Vector3 dir) {
        TargetDirection = dir;
    }

    public void Stop() {
        MoveDirection = Vector3.zero;
        TargetDirection = Vector3.zero;
        GroundVelocity = Vector3.zero;
    }

    public bool Airborne {
        get { return FallVelocity > 0.5f || (Time.time - lastGroundedAt > Coyote && !OnGround()); }
    }

    public bool TryJump(float jumpSpeed) {
        if (Time.time < jumpReadyAt) {
            return false;
        }
        if (FallVelocity > 0.5f || (!OnGround() && Time.time - lastGroundedAt > Coyote)) {
            return false;
        }
        FallVelocity = jumpSpeed;
        IsGrounded = false;
        lastGroundedAt = -9f;
        jumpReadyAt = Time.time + 1f;
        return true;
    }
}