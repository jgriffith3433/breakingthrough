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
    bool jumpLengthPending;
    private Vector3 Tar;

    [SerializeField]
    public ZeldaStyleCamera ZeldaCam;
    private Animator Anim;

    public const string PARAMETER_STATE = "State";
    static readonly int FireSpeedHash = Animator.StringToHash("FireSpeed");
    bool hasFireSpeed;

    void Awake() {
        motor = GetComponent<MovementMotor>();
        Anim = GetComponentInChildren<Animator>();
    }

    void Start() {
        Anim.applyRootMotion = false;
        foreach (AnimatorControllerParameter parameter in Anim.parameters) {
            if (parameter.nameHash == FireSpeedHash) {
                hasFireSpeed = true;
            }
        }
    }

    void Update() {
        MovementAndJumping();
    }

    void AnimationMove(float magnitude) {
        if (Anim.layerCount < 2 && Anim.GetInteger(PARAMETER_STATE) == 2) {
            return;
        }
        int bodyState = Anim.GetInteger(PARAMETER_STATE);
        if (bodyState == 5 || bodyState == 7) {
            jumpAnimLeft = 0f;
        } else if (jumpAnimLeft > 0f) {
            if (jumpLengthPending) {
                jumpLengthPending = false;
                AnimatorStateInfo clip = Anim.GetCurrentAnimatorStateInfo(0);
                if (!clip.IsName("Jump") && Anim.IsInTransition(0)) {
                    clip = Anim.GetNextAnimatorStateInfo(0);
                }
                if (clip.IsName("Jump") && clip.length > 0.2f) {
                    jumpAnimLeft = clip.length * Mathf.Max(0.05f, 1f - clip.normalizedTime);
                } else {
                    Anim.Play("Jump", 0, 0f);
                }
            }
            jumpAnimLeft -= Time.deltaTime;
            Anim.speed = 1f;
            return;
        }
        if (motor != null && motor.Airborne) {
            Anim.speed = 1f;
            return;
        }

        if (magnitude > MoveMagnitude) {
            float speed_Animation = magnitude * 2f;
            if (speed_Animation < 1f) {
                speed_Animation = 1f;
            }
            Anim.SetInteger(PARAMETER_STATE, Aiming() ? 4 : 1);
            Anim.speed = UpperShooting() ? 1f : speed_Animation;
        } else {
            Anim.SetInteger(PARAMETER_STATE, 0);
            Anim.speed = 1f;
        }
    }

    bool UpperShooting() {
        return Anim != null && Anim.layerCount > 1 && Anim.GetCurrentAnimatorStateInfo(1).IsName("Shoot");
    }

    bool Aiming() {
        return ZeldaCam.LockedOnGameObject != null;
    }

    void MovementAndJumping() {
        if (Anim) {
            // Swinging is BatEquipment's: it reads the button and fires the upper layer's recoil clip as the
            // stand-in swing. Swing speed boons speed that clip.
            if (hasFireSpeed) {
                Anim.SetFloat(FireSpeedHash, 1.4f * RunBoons.DrawSpeed);
            }
            if (Anim.layerCount < 2 && Anim.GetInteger(PARAMETER_STATE) == 2) {
                AnimatorStateInfo info = Anim.GetCurrentAnimatorStateInfo(0);
                if (info.IsName("Shoot") && info.normalizedTime >= 0.92f && !Anim.IsInTransition(0)) {
                    Anim.SetInteger(PARAMETER_STATE, 0);
                }
            }

            bool rooted = PlayerGrowth.Instance != null && PlayerGrowth.Instance.Rooted;
            if (rooted) {
                motor.Move(Vector3.zero);
            } else if (ZeldaCam.LockedOnGameObject != null) {
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
                AnimationMove(motor.charController.velocity.magnitude * 0.1f);
            } else {
                var vert = PlayInput.GetAxis("Vertical");
                var hori = PlayInput.GetAxis("Horizontal");
                var cameraForward = new Vector3(ZeldaCam.transform.forward.x * vert, 0, ZeldaCam.transform.forward.z * vert);
                var cameraRight = new Vector3(ZeldaCam.transform.right.x * hori, 0, ZeldaCam.transform.right.z * hori);
                var directionToMove = cameraForward + cameraRight;
                directionToMove *= Speed * BatEquipment.MoveFactor;

                if (directionToMove.sqrMagnitude > 0.001f) {
                    var newRotation = Quaternion.LookRotation(directionToMove.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * TurnSpeed);
                }
                motor.Move(directionToMove);
                AnimationMove(motor.charController.velocity.magnitude * 0.1f);
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
                // State 3's Any State transition will not restart Jump. Play the clip
                // directly and keep locomotion from stealing the body until it finishes.
                Anim.SetInteger(PARAMETER_STATE, -1);
                Anim.Play("Jump", 0, 0f);
                jumpAnimLeft = 1.2f;
                jumpLengthPending = true;
            }
        }
    }
}











































