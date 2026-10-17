using System.Collections.Generic;
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
    bool upperShot;
    float upperTime = 1f;
    int shotCount;

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
        bool firing = false;
        if (anim.layerCount > 1) {
            AnimatorStateInfo upper = anim.GetCurrentAnimatorStateInfo(1);
            firing = upper.IsName("Shoot");
            if (firing && (!upperShot || upper.normalizedTime + 0.2f < upperTime)) {
                Launch();
            }
            upperShot = firing;
            upperTime = firing ? upper.normalizedTime : 1f;
        }
        bool show = firing || state == 0 || state == 2 || state == 3 || state == 4;
        bow.SetActive(show);
        nocked.SetActive(show && state != 2 && !firing);

        if (anim.layerCount < 2 && state == 2 && lastState != 2) {
            Launch();
        }
        lastState = state;
    }

    void OnGUI() {
        bool confirm = UpgradeLook.HitConfirm;
        if (cam == null || LevelUpScreen.IsOpen || PauseMenu.IsOpen || WaveDirector.IsLost || (cam.LockedOnGameObject != null && !confirm)) {
            return;
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        Color previous = GUI.color;
        bool quick = MetaProgress.Rank(UpgradeId.Draw) > 0 && WaveDirector.Wave >= MetaProgress.AutoWave();
        GUI.color = confirm
            ? new Color(1f, 0.9f, 0.35f, 1f)
            : quick ? new Color(1f, 0.82f, 0.28f, 0.95f) : new Color(1f, 0.96f, 0.88f, 0.92f);
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
        if (MetaProgress.ShootsRockets()) {
            Destroy(nocked);
            nocked = UpgradeLook.BuildRocket();
            nocked.transform.SetParent(bow.transform, false);
            nocked.name = "NockedRocket";
        }
        arrowLongLocal = Orient(nocked, ArrowLength, transform.forward, transform.up);

        equipped = true;
        Debug.Log("Bow and arrow equipped on " + hand.name + ".");
    }

    // One draw. Split Shot fans extra arrows, Volley adds a ring now and then.
    void Launch() {
        if (nocked == null) {
            return;
        }

        Vector3 dir = AimDirection();
        int count = 1 + RunBoons.ExtraArrows;
        float fan = count > 3 ? 5.5f : 7f;
        for (int i = 0; i < count; i++) {
            float yaw = (i - (count - 1) * 0.5f) * fan;
            Fire(Quaternion.AngleAxis(yaw, Vector3.up) * dir, false);
        }
        shotCount++;
        int every = RunBoons.VolleyEvery;
        if (every > 0 && shotCount % every == 0) {
            Volley();
        }
        RunLog.NoteShot();
        bool rocket = MetaProgress.ShootsRockets();
        GameAudio.Play("shot", rocket ? 0.45f : 0.8f, Random.Range(0.94f, 1.06f));
    }

    void Volley() {
        int ring = 10 + 4 * RunBoons.Rank(BoonId.Volley) + (RunBoons.Has(BoonId.Hailstorm) ? 4 : 0);
        float tilt = -0.06f;
        float start = Random.Range(0f, 360f);
        for (int i = 0; i < ring; i++) {
            float angle = (start + i * 360f / ring) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), tilt, Mathf.Cos(angle)).normalized;
            Fire(dir, true);
        }
        GameAudio.Play("shot", 0.5f, 0.75f);
    }

    void Fire(Vector3 dir, bool volley) {
        float lead = Mathf.Lerp(0.05f, 0.35f, Mathf.Clamp01(dir.y + 0.85f));
        Vector3 visual = nocked.transform.TransformDirection(arrowLongLocal);
        if (visual.sqrMagnitude < 0.0001f) {
            visual = transform.forward;
        }
        Quaternion turn = Quaternion.FromToRotation(visual, dir);
        bool rocket = MetaProgress.ShootsRockets();
        GameObject shot = Instantiate(nocked, nocked.transform.position + dir * lead, turn * nocked.transform.rotation);
        shot.name = rocket ? "Rocket" : "Arrow";
        shot.SetActive(true);
        shot.transform.SetParent(null, true);

        ArrowFlight flight = shot.AddComponent<ArrowFlight>();
        float speed = rocket ? 44f : ArrowSpeed;
        flight.Begin(dir * speed, transform, arrowLongLocal, PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f);
        if (volley) {
            flight.MakeVolley(RunBoons.Has(BoonId.Hailstorm));
        }
        if (rocket) {
            UpgradeLook.LightRocket(shot, arrowLongLocal);
        }
        UpgradeLook.KeenArrow(shot);
        UpgradeLook.BoonTrail(shot, volley);
    }

    Vector3 AimDirection() {
        if (cam != null && cam.LockedOnGameObject != null && nocked != null) {
            return LockedAim(cam.LockedOnGameObject, nocked.transform.position);
        }
        Vector3 forward = cam != null ? cam.AimRay() : transform.forward;
        if (forward.sqrMagnitude < 0.0001f) {
            forward = transform.forward;
        }
        return forward.normalized;
    }

    Vector3 LockedAim(GameObject target, Vector3 from) {
        Vector3 aim = ChestPoint(target, from);
        Vector3 dir = aim - from;
        Vector3 toTarget = target.transform.position - from;
        toTarget.y = 0f;
        Vector3 flatDir = dir;
        flatDir.y = 0f;
        bool away = toTarget.sqrMagnitude > 0.04f && Vector3.Dot(flatDir, toTarget) <= 0f;
        if (dir.sqrMagnitude < 0.25f || away) {
            Vector3 through = toTarget.sqrMagnitude > 0.04f ? toTarget.normalized : transform.forward;
            float scale = Mathf.Max(1f, target.transform.lossyScale.y);
            dir = through * Mathf.Max(8f, scale * 0.5f) + Vector3.up * (aim.y - from.y);
        }
        if (dir.sqrMagnitude < 0.0001f) {
            dir = toTarget.sqrMagnitude > 0.04f ? toTarget : transform.forward;
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
    float scale = 1f;
    int pierceLeft;
    int bouncesLeft;
    float homing;
    float seekIn;
    bool chill;
    Enemy seek;
    readonly List<Enemy> struck = new List<Enemy>();
    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public void Begin(Vector3 velocity, Transform owner, Vector3 longLocal, float power) {
        this.velocity = velocity;
        this.owner = owner;
        this.longLocal = longLocal;
        this.power = Mathf.Max(1f, power);
        pierceLeft = RunBoons.PierceCount;
        bouncesLeft = RunBoons.Bounces;
        homing = RunBoons.Rank(BoonId.Homing);
    }

    public void MakeVolley(bool frost) {
        chill = frost;
        scale = 0.6f;
        life = 1.6f;
    }

    void Update() {
        age += Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) {
            Destroy(gameObject);
            return;
        }

        if (homing > 0f && age > 0.05f) {
            Steer();
        }

        float distance = velocity.magnitude * Time.deltaTime;
        Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
        int count = Physics.RaycastNonAlloc(transform.position, direction, hitBuffer, distance + 0.08f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hitBuffer, 0, count, HitOrder.Instance);
        for (int i = 0; i < count; i++) {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null) {
                continue;
            }
            if (owner != null && (hit.transform.IsChildOf(owner) || hit.transform == owner)) {
                continue;
            }
            Enemy foe = hit.collider.GetComponentInParent<Enemy>();
            if (foe != null && (struck.Contains(foe) || !foe.Alive)) {
                continue;
            }
            if (MetaProgress.ShootsRockets()) {
                Blast(hit.point, foe);
                Vanish();
                return;
            }
            if (foe != null) {
                struck.Add(foe);
                Combat.ArrowHit(foe, power, scale, true);
                if (chill) {
                    foe.Chill(0.6f, 2f, 1f);
                }
                if (pierceLeft > 0) {
                    pierceLeft--;
                    scale *= 0.85f;
                    continue;
                }
                if (bouncesLeft > 0 && Bounce(hit.point)) {
                    return;
                }
            }
            transform.position = hit.point - direction * 0.05f;
            velocity = Vector3.zero;
            Destroy(gameObject, foe != null ? 0.35f : 4f);
            enabled = false;
            return;
        }

        transform.position += direction * distance;
        Face();
    }

    void Face() {
        if (velocity.sqrMagnitude > 0.0001f && longLocal.sqrMagnitude > 0.0001f) {
            Vector3 visual = transform.TransformDirection(longLocal);
            transform.rotation = Quaternion.FromToRotation(visual, velocity.normalized) * transform.rotation;
        }
    }

    void Steer() {
        seekIn -= Time.deltaTime;
        if (seekIn <= 0f || seek == null || !seek.Alive) {
            seekIn = 0.12f;
            seek = Combat.Nearest(transform.position + velocity.normalized * 7f, 9f + 4f * homing, struck);
        }
        if (seek == null) {
            return;
        }
        Vector3 aim = seek.transform.position + Vector3.up * 0.5f * Mathf.Max(1f, seek.BodyScale) - transform.position;
        float turn = (2.2f + 2.2f * homing) * Time.deltaTime;
        velocity = Vector3.RotateTowards(velocity, aim.normalized * velocity.magnitude, turn, 0f);
    }

    bool Bounce(Vector3 from) {
        Enemy next = Combat.Nearest(from, 16f, struck);
        if (next == null) {
            return false;
        }
        bouncesLeft--;
        scale *= 0.8f;
        Vector3 to = next.transform.position + Vector3.up * 0.5f * Mathf.Max(1f, next.BodyScale) - from;
        velocity = to.normalized * Mathf.Max(30f, velocity.magnitude);
        transform.position = from + to.normalized * 0.25f;
        life = Mathf.Max(life, 1.2f);
        Face();
        return true;
    }

    void Blast(Vector3 point, Enemy direct) {
        if (direct != null) {
            Combat.ArrowHit(direct, power, scale, true);
        }
        float splash = MetaProgress.SplashRadius(power);
        List<Enemy> roster = Enemy.Roster;
        var caught = new List<Enemy>();
        for (int i = 0; i < roster.Count; i++) {
            Enemy other = roster[i];
            if (other == null || other == direct || !other.Alive) {
                continue;
            }
            float reach = splash + other.BodyScale * 0.3f;
            if ((other.transform.position - point).sqrMagnitude <= reach * reach) {
                caught.Add(other);
            }
        }
        for (int i = 0; i < caught.Count; i++) {
            Combat.ArrowHit(caught[i], power, scale * 0.65f, false);
        }
        UpgradeLook.Blast(point, splash);
    }

    void Vanish() {
        Renderer[] shown = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < shown.Length; i++) {
            shown[i].enabled = false;
        }
        Destroy(gameObject);
    }
}

sealed class HitOrder : IComparer<RaycastHit> {
    public static readonly HitOrder Instance = new HitOrder();

    public int Compare(RaycastHit a, RaycastHit b) {
        return a.distance.CompareTo(b.distance);
    }
}
