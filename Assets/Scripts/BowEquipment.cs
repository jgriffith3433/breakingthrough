using UnityEngine;

// Shows the bow during the aim poses and fires the arrow on the existing shoot state.
public class BowEquipment : MonoBehaviour {
    [SerializeField] GameObject bowModel;
    [SerializeField] GameObject arrowModel;

    const float BowLength = 1.35f;
    const float ArrowLength = 0.85f;
    const float ArrowSpeed = 34f;

    Animator anim;
    ZeldaStyleCamera cam;
    GameObject bow;
    GameObject nocked;
    Vector3 arrowLongLocal;
    int lastState = -1;
    bool equipped;

    void Start() {
        anim = GetComponentInChildren<Animator>();
        CharacterMovement movement = GetComponent<CharacterMovement>();
        if (movement != null) {
            cam = movement.ZeldaCam;
        }
        Equip();
    }

    void Update() {
        if (!equipped) {
            return;
        }

        int state = anim.GetInteger(CharacterMovement.PARAMETER_STATE);
        bool firstPerson = cam != null && cam.CamState == ZeldaStyleCamera.CamStates.FirstPerson;
        bool show = !firstPerson && (state == 0 || state == 2 || state == 4);
        bow.SetActive(show);
        nocked.SetActive(show && state != 2);

        if (state == 2 && lastState != 2) {
            Launch();
        }
        lastState = state;
    }

    void Equip() {
        if (bowModel == null || arrowModel == null || anim == null) {
            Debug.LogWarning("Bow and arrow models are not assigned.");
            return;
        }

        Transform hand = FindBone(anim.transform, "LeftHand");
        if (hand == null) {
            Debug.LogWarning("Left hand bone was not found, so the bow was not equipped.");
            return;
        }

        bow = Instantiate(bowModel, hand, false);
        bow.name = "EquippedBow";
        DisableHelperBoxes(bow);
        Orient(bow, BowLength, transform.up, transform.forward);
        bow.transform.position += PalmOffset(hand);

        nocked = Instantiate(arrowModel, bow.transform, false);
        nocked.name = "NockedArrow";
        DisableHelperBoxes(nocked);
        arrowLongLocal = Orient(nocked, ArrowLength, transform.forward, transform.up);

        equipped = true;
        Debug.Log("Bow and arrow equipped on " + hand.name + ".");
    }

    void Launch() {
        if (nocked == null) {
            return;
        }

        Vector3 dir = AimDirection();
        Vector3 visual = nocked.transform.TransformDirection(arrowLongLocal);
        if (visual.sqrMagnitude < 0.0001f) {
            visual = transform.forward;
        }
        Quaternion turn = Quaternion.FromToRotation(visual, dir);
        GameObject shot = Instantiate(nocked, nocked.transform.position + dir * 0.4f, turn * nocked.transform.rotation);
        shot.name = "Arrow";
        shot.SetActive(true);
        shot.transform.SetParent(null, true);

        ArrowFlight flight = shot.AddComponent<ArrowFlight>();
        flight.Begin(dir * ArrowSpeed, transform, arrowLongLocal);
    }

    Vector3 AimDirection() {
        Vector3 dir = transform.forward;
        if (cam != null && cam.CamState == ZeldaStyleCamera.CamStates.LockOn && cam.LockedOnGameObject != null) {
            dir = cam.LockedOnGameObject.transform.position + Vector3.up * 1.1f - nocked.transform.position;
        } else if (cam != null && cam.CamState == ZeldaStyleCamera.CamStates.Target) {
            dir = cam.transform.forward;
        }

        if (dir.sqrMagnitude < 0.0001f) {
            dir = transform.forward;
        }
        return dir.normalized;
    }

    static Vector3 PalmOffset(Transform hand) {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform child in hand) {
            sum += child.position;
            count++;
        }
        if (count == 0) {
            return Vector3.zero;
        }
        Vector3 towardFingers = (sum / count) - hand.position;
        if (towardFingers.sqrMagnitude < 0.0001f) {
            return Vector3.zero;
        }
        return towardFingers.normalized * 0.08f;
    }

    static Vector3 Orient(GameObject go, float targetLength, Vector3 worldLong, Vector3 worldThin) {
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        Bounds bounds = MeshBounds(go);
        int longAxis = LargestAxis(bounds.size);
        int thinAxis = SmallestAxis(bounds.size, longAxis);
        Vector3 localLong = Axis(longAxis);
        Vector3 localThin = Axis(thinAxis);

        Vector3 worldMeshLong = go.transform.TransformDirection(localLong);
        go.transform.rotation = Quaternion.FromToRotation(worldMeshLong, worldLong.normalized) * go.transform.rotation;

        Vector3 projected = Vector3.ProjectOnPlane(go.transform.TransformDirection(localThin), worldLong.normalized);
        Vector3 desired = Vector3.ProjectOnPlane(worldThin, worldLong.normalized);
        if (projected.sqrMagnitude > 0.0001f && desired.sqrMagnitude > 0.0001f) {
            go.transform.rotation = Quaternion.FromToRotation(projected, desired.normalized) * go.transform.rotation;
        }

        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest > 0.0001f) {
            go.transform.localScale = Vector3.one * (targetLength / longest);
        }

        bounds = MeshBounds(go);
        Vector3 worldCenter = go.transform.TransformPoint(bounds.center);
        go.transform.position += go.transform.parent.position - worldCenter;
        return localLong;
    }

    static void DisableHelperBoxes(GameObject go) {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) {
            if (t.name != "Box001") {
                continue;
            }
            Renderer renderer = t.GetComponent<Renderer>();
            if (renderer != null) {
                renderer.enabled = false;
            }
        }
    }

    static Bounds MeshBounds(GameObject go) {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (MeshFilter filter in go.GetComponentsInChildren<MeshFilter>(true)) {
            if (filter.sharedMesh == null || filter.transform.name == "Box001") {
                continue;
            }
            Bounds mesh = filter.sharedMesh.bounds;
            Vector3 c = mesh.center;
            Vector3 e = mesh.extents;
            for (int i = 0; i < 8; i++) {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = go.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any) {
                    bounds = new Bounds(local, Vector3.zero);
                    any = true;
                } else {
                    bounds.Encapsulate(local);
                }
            }
        }
        return bounds;
    }

    static Transform FindBone(Transform root, string suffix) {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            if (t.name.EndsWith(suffix)) {
                return t;
            }
        }
        return null;
    }

    static int LargestAxis(Vector3 size) {
        int axis = 0;
        if (size.y > size[axis]) axis = 1;
        if (size.z > size[axis]) axis = 2;
        return axis;
    }

    static int SmallestAxis(Vector3 size, int except) {
        int axis = except == 0 ? 1 : 0;
        for (int i = 0; i < 3; i++) {
            if (i != except && size[i] < size[axis]) {
                axis = i;
            }
        }
        return axis;
    }

    static Vector3 Axis(int axis) {
        if (axis == 0) return Vector3.right;
        if (axis == 1) return Vector3.up;
        return Vector3.forward;
    }
}

public class ArrowFlight : MonoBehaviour {
    Vector3 velocity;
    Transform owner;
    Vector3 longLocal;
    float life = 2.8f;
    float age;

    public void Begin(Vector3 velocity, Transform owner, Vector3 longLocal) {
        this.velocity = velocity;
        this.owner = owner;
        this.longLocal = longLocal;
    }

    void Update() {
        age += Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) {
            Destroy(gameObject);
            return;
        }

        float distance = velocity.magnitude * Time.deltaTime;
        Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance + 0.08f, ~0, QueryTriggerInteraction.Ignore)) {
            if (owner == null || (!hit.transform.IsChildOf(owner) && hit.transform != owner)) {
                transform.position = hit.point - direction * 0.05f;
                velocity = Vector3.zero;
                Destroy(gameObject, 6f);
                enabled = false;
                return;
            }
        }

        transform.position += direction * distance;
        if (velocity.sqrMagnitude > 0.0001f && longLocal.sqrMagnitude > 0.0001f) {
            Vector3 visual = transform.TransformDirection(longLocal);
            transform.rotation = Quaternion.FromToRotation(visual, velocity.normalized) * transform.rotation;
        }
    }
}
