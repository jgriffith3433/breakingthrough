using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementMotor : MonoBehaviour {
    public float GravityMultiplier = 1f;
    public float LerpTime = 10f;
    public Animator Anim;
    private Vector3 MoveDirection = Vector3.zero;
    private Vector3 TargetDirection = Vector3.zero;
    private float FallVelocity = 0f;
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
        if (gamecam.CamState != ZeldaStyleCamera.CamStates.FirstPerson) {
            MoveDirection = Vector3.Lerp(MoveDirection, TargetDirection, Time.deltaTime * LerpTime);
            MoveDirection.y = FallVelocity;
            charController.Move(MoveDirection * Time.deltaTime);
        } else {
            Anim.SetInteger("State", 0);
        }
        if (!IsGrounded) {
            FallVelocity -= 90f * GravityMultiplier * Time.deltaTime;
        }
    }

    public bool OnGround() {
        if (charController.isGrounded) {
            return true;
        }

        RaycastHit hit;
        if (Physics.Raycast(MyCollider.bounds.center, -Vector3.up, out hit, DistanceToGround + 0.1f)) {
            return true;
        }

        return false;
    }

    public void Move(Vector3 dir) {
        TargetDirection = dir;
    }

    public void Stop() {
        MoveDirection = Vector3.zero;
        TargetDirection = Vector3.zero;
    }

    public void Jump(float jumpSpeed) {
        if (IsGrounded) {
            FallVelocity = jumpSpeed;
        }
    }
}