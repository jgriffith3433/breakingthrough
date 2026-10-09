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
    public CamStates CamState = CamStates.Behind;
    [SerializeField]
    private Vector2 FirstPersonXAxisClamp = new Vector2(-85.0f, 85.0f);
    public const string PARAMETER_STATE = "State";
    public GameObject XHair;
    private Vector3 Pos;
    private LockOn LockOn;
    public GameObject LockedOnGameObject;
    private bool LockOnSnap;
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

    void Start() {
        LastChaaracterPos = Target.transform.position;
        FollowXForm = GameObject.FindWithTag("Player").transform;
        Animator = GameObject.FindWithTag("playerchild").GetComponent<Animator>();
        LockOn = GameObject.Find("LockOn").GetComponent<LockOn>();
        LookDir = FollowXForm.forward;
        orbitYaw = FollowXForm.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update() {
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

        switch (CamState) {
            case CamStates.Behind:
                orbitYaw += Input.GetAxis("Mouse X") * 3.4f;
                orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * 2.1f, 8f, 46f);
                if (CurrentCameraDistance < MinDistance) {
                    CurrentCameraDistance = MaxDistance;
                }
                LookDir = Quaternion.Euler(0f, orbitYaw, 0f) * Vector3.forward;
                float body = Mathf.Max(1f, BodyScale);
                float camDist = CurrentCameraDistance * Mathf.Lerp(1f, body, 0.7f);
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
                orbitYaw += Input.GetAxis("Mouse X") * 2.6f;
                aimPitch = Mathf.Clamp(aimPitch - Input.GetAxis("Mouse Y") * 1.7f, -42f, 48f);
                FollowXForm.rotation = Quaternion.Euler(0f, orbitYaw, 0f);
                transform.position = FirstPersonCamPos.transform.position;
                transform.rotation = Quaternion.Euler(aimPitch, orbitYaw, 0f);
                TargetPosition = transform.position;
                break;
        }

        LastChaaracterPos = Target.transform.position;
        if (CamState != CamStates.Circling && CamState != CamStates.LockOn && CamState != CamStates.Target && CamState != CamStates.FirstPerson) {
            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, CamSmoothDampTime * Time.deltaTime);
        }
        if (CamState == CamStates.Target) {
            transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref VelocityCamSmooth, 5 * Time.deltaTime);
        }

        CompensateForWalls(characterOffset, ref TargetPosition);
        transform.LookAt(FollowXForm.position + Vector3.up * Mathf.Max(1f, DistanceUp * Mathf.Max(1f, BodyScale)));
        ShowLockMarker();
    }

    void AcquireTarget() {
        GameObject best = null;
        float bestDist = 12f;
        Vector3 origin = FollowXForm.position;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            float dist = Vector3.Distance(origin, enemy.transform.position);
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy.gameObject;
            }
        }
        LockedOnGameObject = best;
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
        LockedOnCrosshair.transform.position = LockedOnGameObject.transform.position + Vector3.up * 0.95f;
        LockedOnCrosshair.transform.LookAt(transform.position);
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
        return Quaternion.AngleAxis(-7f, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
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
        // Compensate for walls between camera
        RaycastHit wallHit = new RaycastHit();
        if (Physics.Linecast(fromObject, toTarget, out wallHit)) {
            if (wallHit.collider.tag == "wall") {
                HitWall = true;
                transform.position = new Vector3(wallHit.point.x, transform.position.y, wallHit.point.z);
            } else {
                HitWall = false;
            }
            HitWall = false;
        }

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