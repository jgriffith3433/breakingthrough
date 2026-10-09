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
    private Vector3 Tar;

    [SerializeField]
    public ZeldaStyleCamera ZeldaCam;
    private Animator Anim;

    public const string PARAMETER_STATE = "State";

    void Awake() {
        motor = GetComponent<MovementMotor>();
        Anim = GetComponentInChildren<Animator>();
    }

    void Start() {
        Anim.applyRootMotion = false;
    }

    void Update() {
        MovementAndJumping();
    }

    void AnimationMove(float magnitude) {
        if (magnitude > MoveMagnitude) {

            float speed_Animation = magnitude * 2f;

            if (speed_Animation < 1f) {
                speed_Animation = 1f;
            }

            if (Anim.GetInteger(PARAMETER_STATE) != 2) {
                Anim.SetInteger(PARAMETER_STATE, 1);
                Anim.speed = speed_Animation;
                if (ZeldaCam.CamState == ZeldaStyleCamera.CamStates.LockOn && Anim.GetInteger(PARAMETER_STATE) != 2) {
                    Anim.SetInteger(PARAMETER_STATE, 4);
                }
            }

        } else {
            if (Anim.GetInteger(PARAMETER_STATE) != 2) {
                Anim.SetInteger(PARAMETER_STATE, 0);
            }
        }
    }

    void MovementAndJumping() {
        if (Anim) {
            if (ZeldaCam.CamState == ZeldaStyleCamera.CamStates.LockOn) {
                var vert = Input.GetAxis("Vertical");
                var hori = Input.GetAxis("Horizontal");
                var directionTowardLockedOnObject = ZeldaCam.LockedOnGameObject.transform.position - transform.position;
                directionTowardLockedOnObject.y = 0;

                var cameraForward = new Vector3(ZeldaCam.transform.forward.x, 0, ZeldaCam.transform.forward.z) * 4;
                var cameraRight = new Vector3(ZeldaCam.transform.right.x, 0, ZeldaCam.transform.right.z) * 4;
                var playerForward = new Vector3(transform.forward.x, 0, transform.forward.z);
                var playerRight = new Vector3(transform.right.x, 0, transform.right.z);
                playerForward += cameraForward * 4;
                playerRight += cameraRight * 4;

                var directionToMove = ((cameraForward + playerForward) * vert) + ((cameraRight + playerRight) * hori);
                directionToMove = directionToMove / 16 * Speed;


                if (directionToMove.magnitude > 0) {
                    var newRotation = Quaternion.LookRotation(directionTowardLockedOnObject.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity.magnitude * 0.1f);
            } else if (ZeldaCam.CamState != ZeldaStyleCamera.CamStates.FirstPerson) {
                var vert = Input.GetAxis("Vertical");
                var hori = Input.GetAxis("Horizontal");
                var cameraForward = new Vector3(ZeldaCam.transform.forward.x * vert, 0, ZeldaCam.transform.forward.z * vert);
                var cameraRight = new Vector3(ZeldaCam.transform.right.x * hori, 0, ZeldaCam.transform.right.z * hori);
                var directionToMove = cameraForward + cameraRight;
                directionToMove *= Speed;

                if (directionToMove.normalized.magnitude > 0) {
                    var newRotation = Quaternion.LookRotation(directionToMove.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity.magnitude * 0.1f);

                if (Input.GetKey(KeyCode.F)) {
                    Anim.SetInteger(PARAMETER_STATE, 3);
                    motor.Jump(SpeedJump);
                }
            }
        }
    }
}











































