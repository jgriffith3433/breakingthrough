using UnityEngine;

// Shows the bow during the aim poses and fires the arrow on the existing shoot state.
public class BowEquipment : MonoBehaviour {
    [SerializeField] GameObject bowModel;
    [SerializeField] GameObject arrowModel;

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
        //bool show = !firstPerson && (state == 0 || state == 2 || state == 4);
        bool show = state == 0 || state == 2 || state == 4;
        bow.SetActive(show);
        nocked.SetActive(show && state != 2);

        if (state == 2 && lastState != 2) {
            Launch();
        }
        lastState = state;
    }

    void OnGUI() {
        if (cam == null || cam.LockedOnGameObject != null) {
            return;
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        Color previous = GUI.color;
        GUI.color = new Color(1f, 0.96f, 0.88f, 0.92f);
        GUI.DrawTexture(new Rect(cx - 14f, cy - 1f, 8f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + 6f, cy - 1f, 8f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 1f, cy - 14f, 2f, 8f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 1f, cy + 6f, 2f, 8f), Texture2D.whiteTexture);
        GUI.color = previous;
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
        //DisableHelperBoxes(bow);
        bow.transform.localPosition = new Vector3(-0.009f, -0.066f, 0.035f);
        bow.transform.localRotation = Quaternion.Euler(13f, 350f, 358f);
        bow.transform.localScale = new Vector3(0.353789777f, 0.353789777f, 0.353789777f);

        nocked = Instantiate(arrowModel, bow.transform, false);
        nocked.name = "NockedArrow";
        //DisableHelperBoxes(nocked);
        arrowLongLocal = Orient(nocked, ArrowLength, transform.forward, transform.up);

        equipped = true;
        Debug.Log("Bow and arrow equipped on " + hand.name + ".");
    }

    void Launch() {
        if (nocked == null) {
            return;
        }

        Vector3 dir = AimDirection();
        float lead = Mathf.Lerp(0.05f, 0.35f, Mathf.Clamp01(dir.y + 0.85f));
        Vector3 visual = nocked.transform.TransformDirection(arrowLongLocal);
        if (visual.sqrMagnitude < 0.0001f) {
            visual = transform.forward;
        }
        Quaternion turn = Quaternion.FromToRotation(visual, dir);
        GameObject shot = Instantiate(nocked, nocked.transform.position + dir * lead, turn * nocked.transform.rotation);
        shot.name = "Arrow";
        shot.SetActive(true);
        shot.transform.SetParent(null, true);

        ArrowFlight flight = shot.AddComponent<ArrowFlight>();
        flight.Begin(dir * ArrowSpeed, transform, arrowLongLocal, PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f);
        GameAudio.Play("shot", 0.8f);
    }

    Vector3 AimDirection() {
        Vector3 dir = transform.forward;
        if (cam != null && cam.LockedOnGameObject != null && nocked != null) {
            dir = ChestPoint(cam.LockedOnGameObject, nocked.transform.position) - nocked.transform.position;
        } else if (cam != null) {
            dir = cam.ViewAim();
        }

        if (dir.sqrMagnitude < 0.0001f) {
            dir = transform.forward;
        }
        return dir.normalized;
    }

    // Body center when the shot is level. Lower when the bow is close or high, so the arrow meets the bird instead of passing over it.
    public static Vector3 ChestPoint(GameObject target, Vector3 from) {
        float scale = Mathf.Max(0.45f, target.transform.lossyScale.y);
        float body = 0.68f * scale;
        Vector3 feet = target.transform.position;
        float flat = Vector3.Distance(new Vector3(from.x, 0f, from.z), new Vector3(feet.x, 0f, feet.z));
        float above = Mathf.Clamp01((from.y - feet.y - 0.6f) / 3.5f);
        float near = 1f - Mathf.Clamp01(flat / 4.2f);
        float portion = Mathf.Lerp(0.4f, 0.2f, Mathf.Clamp01(near + above * 0.85f));
        return feet + Vector3.up * (body * portion);
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
    float power = 1f;

    public void Begin(Vector3 velocity, Transform owner, Vector3 longLocal, float power) {
        this.velocity = velocity;
        this.owner = owner;
        this.longLocal = longLocal;
        this.power = Mathf.Max(1f, power);
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
                Enemy foe = hit.collider.GetComponentInParent<Enemy>();
                if (foe != null) {
                    foe.Hit(power);
                    if (power >= MetaProgress.BoomScale()) {
                        Collider[] caught = Physics.OverlapSphere(hit.point, MetaProgress.SplashRadius(power));
                        foreach (Collider caughtCollider in caught) {
                            Enemy other = caughtCollider.GetComponentInParent<Enemy>();
                            if (other != null && other != foe && other.Alive) {
                                other.Hit(power * 0.65f);
                            }
                        }
                    }
                }
                transform.position = hit.point - direction * 0.05f;
                velocity = Vector3.zero;
                Destroy(gameObject, foe != null ? 0.35f : 6f);
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
