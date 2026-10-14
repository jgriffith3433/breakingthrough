using System.Collections.Generic;
using UnityEngine;

// Feathers that birds drop, and the treasure chests from bosses and golden gobblers.
public class Pickups : MonoBehaviour {
    static readonly List<Pickups> live = new List<Pickups>();
    static Material featherMat;
    static Material rareMat;
    static float nextChime;
    static int chimeStep;
    static float chimeReset;

    int value;
    bool chest;
    bool pulled;
    float age;
    float speed;
    Vector3 velocity;
    Vector3 rest;
    Transform spin;

    public static void DropFeathers(Vector3 at, int total) {
        if (total <= 0) {
            return;
        }
        int pieces = Mathf.Clamp(total / 3 + 1, 1, 6);
        int each = Mathf.Max(1, total / pieces);
        int left = total;
        for (int i = 0; i < pieces && left > 0; i++) {
            int v = i == pieces - 1 ? left : Mathf.Min(each, left);
            left -= v;
            Spawn(at, v, false);
        }
    }

    public static void DropChest(Vector3 at) {
        Spawn(at, 0, true);
        GameAudio.Play("coin", 0.8f, 0.8f);
    }

    // Wave over. Everything on the ground flies to you.
    public static void Vacuum() {
        for (int i = 0; i < live.Count; i++) {
            if (live[i] != null) {
                live[i].pulled = true;
                live[i].speed = Mathf.Max(live[i].speed, 18f);
            }
        }
    }

    public static int Count {
        get { return live.Count; }
    }

    static void Spawn(Vector3 at, int value, bool chest) {
        var go = new GameObject(chest ? "Chest" : "Feather");
        go.transform.position = at + Vector3.up * 0.6f;
        Pickups pickup = go.AddComponent<Pickups>();
        pickup.value = value;
        pickup.chest = chest;
        Vector3 toss = Random.insideUnitSphere * (chest ? 1.5f : 3.2f);
        toss.y = chest ? 6f : Random.Range(3.5f, 6f);
        pickup.velocity = toss;
        pickup.Build();
    }

    void Build() {
        if (chest) {
            GameObject model = ArtLibrary.Make("Chest");
            if (model != null) {
                model.transform.SetParent(transform, false);
                model.transform.localScale = Vector3.one * 1.1f;
                spin = model.transform;
            } else {
                spin = new GameObject("Spin").transform;
                spin.SetParent(transform, false);
                Box(spin, new Vector3(0f, 0.32f, 0f), new Vector3(1f, 0.6f, 0.7f), new Color(0.55f, 0.3f, 0.12f));
                Box(spin, new Vector3(0f, 0.72f, 0f), new Vector3(1.04f, 0.26f, 0.74f), new Color(0.65f, 0.38f, 0.15f));
                Box(spin, new Vector3(0f, 0.5f, 0f), new Vector3(1.08f, 0.12f, 0.78f), new Color(1f, 0.8f, 0.25f));
                Box(spin, new Vector3(0f, 0.5f, 0.38f), new Vector3(0.18f, 0.22f, 0.06f), new Color(1f, 0.92f, 0.5f));
            }
            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.8f, 0.35f);
            glow.range = 6f;
            glow.intensity = 2.2f;
            glow.shadows = LightShadows.None;
            return;
        }

        GameObject gemModel = ArtLibrary.Make("Feather", true);
        bool rare = value >= 6;
        if (gemModel != null) {
            gemModel.transform.SetParent(transform, false);
            gemModel.transform.localScale = Vector3.one * (rare ? 0.7f : 0.45f);
            spin = gemModel.transform;
            return;
        }
        var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(gem.GetComponent<Collider>());
        gem.transform.SetParent(transform, false);
        gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        gem.transform.localScale = Vector3.one * (rare ? 0.36f : 0.22f);
        Renderer skin = gem.GetComponent<Renderer>();
        skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        skin.receiveShadows = false;
        skin.sharedMaterial = rare ? RareMat() : FeatherMat();
        spin = gem.transform;
    }

    static Material FeatherMat() {
        if (featherMat == null) {
            featherMat = new Material(Shader.Find("Unlit/Color"));
            featherMat.color = new Color(0.45f, 0.95f, 1f);
        }
        return featherMat;
    }

    static Material RareMat() {
        if (rareMat == null) {
            rareMat = new Material(Shader.Find("Unlit/Color"));
            rareMat.color = new Color(1f, 0.45f, 0.95f);
        }
        return rareMat;
    }

    static void Box(Transform parent, Vector3 pos, Vector3 size, Color color) {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = pos;
        box.transform.localScale = size;
        UpgradeLook.Paint(box, color);
    }

    void OnEnable() {
        live.Add(this);
    }

    void OnDisable() {
        live.Remove(this);
    }

    void Update() {
        float dt = Time.deltaTime;
        age += dt;
        if (spin != null) {
            spin.Rotate(0f, (chest ? 60f : 180f) * dt, 0f, Space.World);
        }
        PlayerGrowth player = PlayerGrowth.Instance;
        if (player == null) {
            return;
        }
        Vector3 target = player.transform.position + Vector3.up * player.BodyHeight * 0.5f;
        Vector3 to = target - transform.position;
        float reach = chest ? player.BodyRadius + 1.6f : RunBoons.PickupRadius + player.BodyRadius;

        if (!pulled && age > 0.45f && to.magnitude <= reach) {
            pulled = true;
        }

        if (pulled) {
            speed = Mathf.Min(speed + dt * 40f, 60f);
            float step = Mathf.Max(8f, speed) * dt;
            if (to.magnitude <= Mathf.Max(step, player.BodyRadius + 0.4f)) {
                Collect();
                return;
            }
            transform.position += to.normalized * step;
            return;
        }

        // Toss, land, then bob.
        if (age < 1.2f) {
            velocity.y -= 18f * dt;
            Vector3 next = transform.position + velocity * dt;
            float ground = GroundY(next);
            if (next.y < ground + 0.5f) {
                next.y = ground + 0.5f;
                velocity = Vector3.zero;
            }
            transform.position = next;
            rest = transform.position;
        } else {
            transform.position = rest + Vector3.up * Mathf.Sin(age * 3f + rest.x) * 0.12f;
        }
    }

    void Collect() {
        if (chest) {
            Fx.Ring(transform.position - Vector3.up * 0.4f, 6f, new Color(1f, 0.85f, 0.35f, 0.9f), 0.6f);
            RunBoons.AddChest();
            UpgradeLook.Say("Treasure!", 1.2f, 40f);
            GameAudio.Play("coin", 0.9f, 1.2f);
        } else {
            RunBoons.AddFeathers(value);
            Chime();
        }
        Destroy(gameObject);
    }

    static void Chime() {
        if (Time.unscaledTime < nextChime) {
            return;
        }
        if (Time.unscaledTime > chimeReset) {
            chimeStep = 0;
        }
        nextChime = Time.unscaledTime + 0.05f;
        chimeReset = Time.unscaledTime + 0.6f;
        chimeStep = Mathf.Min(chimeStep + 1, 16);
        GameAudio.Play("coin", 0.18f, 1.3f + chimeStep * 0.05f);
    }

    static float GroundY(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 30f, at.z), Vector3.down, out RaycastHit hit, 80f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y - 10f;
    }
}

// Models made in Blender and dropped in Resources/Models. Falls back to primitives when missing.
public static class ArtLibrary {
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

    static readonly Dictionary<string, Material> glow = new Dictionary<string, Material>();

    // Unlit keeps feathers and quills bright on the night hill.
    public static GameObject Make(string name, bool unlit = false) {
        GameObject source;
        if (!cache.TryGetValue(name, out source)) {
            source = Resources.Load<GameObject>("Models/" + name);
            cache[name] = source;
        }
        if (source == null) {
            return null;
        }
        GameObject made = Object.Instantiate(source);
        foreach (Collider collider in made.GetComponentsInChildren<Collider>()) {
            Object.Destroy(collider);
        }
        foreach (Renderer renderer in made.GetComponentsInChildren<Renderer>()) {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!unlit) {
                continue;
            }
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) {
                if (mats[i] == null) {
                    continue;
                }
                Material bright;
                if (!glow.TryGetValue(mats[i].name, out bright)) {
                    bright = new Material(Shader.Find("Unlit/Color"));
                    bright.color = mats[i].HasProperty("_Color") ? mats[i].color : Color.white;
                    glow[mats[i].name] = bright;
                }
                mats[i] = bright;
            }
            renderer.sharedMaterials = mats;
        }
        return made;
    }
}
