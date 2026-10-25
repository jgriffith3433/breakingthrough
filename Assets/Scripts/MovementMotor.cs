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
        // Rising always falls back down. A stuck ground check used to freeze the jump speed and carry you upward.
        if (FallVelocity > 0.5f || !IsGrounded) {
            FallVelocity -= 90f * GravityMultiplier * Time.deltaTime;
        } else {
            FallVelocity = -2f;
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

        float feet = charController.center.y - charController.height * 0.5f;
        Vector3 origin = transform.position + Vector3.up * (feet + 0.25f);
        int mask = LayerMask.GetMask("Ground");
        if (mask == 0) {
            mask = 1 << 3;
        }
        return Physics.Raycast(origin, Vector3.down, 0.4f, mask, QueryTriggerInteraction.Ignore);
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
        get { return FallVelocity > 0.5f || !OnGround(); }
    }

    public bool TryJump(float jumpSpeed) {
        if (Time.time < jumpReadyAt) {
            return false;
        }
        if (!(OnGround() && FallVelocity <= 0.5f)) {
            return false;
        }
        FallVelocity = jumpSpeed;
        IsGrounded = false;
        jumpReadyAt = Time.time + 1f;
        return true;
    }
}