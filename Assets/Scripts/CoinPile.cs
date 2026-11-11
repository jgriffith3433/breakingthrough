using System.Collections.Generic;
using UnityEngine;

// Slice's fee, seen (Docs/Story.md, "The fee"). A pile of gold coins sits at the edge of the circle, beside her: the Department's
// filing fee that Wallit is paid to guard ("I'm covering you"). Its height is her health. When a bot's blow lands on Slice the
// top coins fly off and arc to the bot that struck her (it takes its cut, with a coin clink), so the pile shrinks on screen; when
// she heals the coins drop back one by one. Chunky turned discs, no sparks. Added by StakesFeel.Ensure, hit from StakesFeel.Hit.
public class CoinPile : MonoBehaviour {
    public const int Slots = 24;
    const int Stacks = 4;
    const float CoinRadius = 0.26f, CoinHeight = 0.07f;
    // Where the pile sits against Slice, in the world: inside the ring, toward the camera side, clear of her feet.
    static readonly Vector3 Offset = new Vector3(1.9f, 0f, -0.9f);

    public static readonly List<string> Events = new List<string>();
    public static CoinPile Instance { get; private set; }
    public static int Shown { get { return Instance != null ? Instance.shown : 0; } }
    public static int Wanted { get { return Instance != null ? Instance.Target() : 0; } }
    public static int Taken { get; private set; }
    public static int Flying { get { return Instance != null ? Instance.flyers.Count : 0; } }

    class Flyer {
        public Transform t;
        public Vector3 from, to;
        public float age, life;
    }

    Musician slice;
    Mesh coinMesh;
    Material gold;
    Transform[] coins;
    float[] pop;
    int shown;
    float refill;
    readonly List<Flyer> flyers = new List<Flyer>();
    Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Events.Clear();
        Instance = null;
        Taken = 0;
    }

    public static void Ensure(Musician musician) {
        if (musician == null || musician.GetComponent<CoinPile>() != null) {
            return;
        }
        musician.gameObject.AddComponent<CoinPile>().slice = musician;
    }

    // A blow landed on Slice (StakesFeel.Hit, after her health changed): the coins that no longer fit leave the pile.
    public static void OnSliceHit(Enemy by) {
        if (Instance != null) {
            Instance.Lose(by);
        }
    }

    int Target() {
        if (slice == null || !slice.Alive) {
            return 0;
        }
        return Mathf.Clamp(Mathf.CeilToInt(Slots * slice.Health / (float)Mathf.Max(1, slice.MaxHealth)), 1, Slots);
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        Taken = 0;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (root != null) {
            Destroy(root.gameObject);
        }
    }

    void Build() {
        var k = new KitMesh { Name = "Fee Coin" };
        k.Lathe(new List<Vector2> {
            new Vector2(0f, 0f), new Vector2(CoinRadius, 0f), new Vector2(CoinRadius, CoinHeight),
            new Vector2(CoinRadius * 0.82f, CoinHeight), new Vector2(CoinRadius * 0.82f, CoinHeight * 0.8f),
            new Vector2(0f, CoinHeight * 0.8f) }, 14, false);
        coinMesh = k.ToMesh();
        var white = new Color32[coinMesh.vertexCount];
        for (int i = 0; i < white.Length; i++) {
            white[i] = new Color32(255, 255, 255, 255);
        }
        coinMesh.colors32 = white;
        Shader shader = SafeShader.Find("Sprites/Default");
        if (shader == null) {
            return;
        }
        gold = new Material(shader);
        gold.color = new Color(1f, 0.82f, 0.3f);
        root = new GameObject("Slice's Fee").transform;
        coins = new Transform[Slots];
        pop = new float[Slots];
        for (int i = 0; i < Slots; i++) {
            coins[i] = MakeCoin("Coin " + i, 0.9f + 0.1f * ((i * 7) % 5) / 4f).transform;
            coins[i].SetParent(root, false);
            coins[i].gameObject.SetActive(false);
        }
        shown = Target();
        for (int i = 0; i < shown; i++) {
            coins[i].gameObject.SetActive(true);
            pop[i] = 1f;
        }
    }

    GameObject MakeCoin(string name, float shade) {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = coinMesh;
        var mr = go.AddComponent<MeshRenderer>();
        var m = new Material(gold);
        m.color = new Color(gold.color.r * shade, gold.color.g * shade, gold.color.b * shade);
        mr.sharedMaterial = m;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    // Slot i: four little stacks, each coin turned a little, so it reads as a heap and not a column.
    Vector3 SlotLocal(int i) {
        int stack = i % Stacks, level = i / Stacks;
        float a = stack / (float)Stacks * Mathf.PI * 2f + 0.5f;
        return new Vector3(Mathf.Cos(a) * 0.3f, level * CoinHeight * 1.05f + 0.03f, Mathf.Sin(a) * 0.3f);
    }

    static float GroundY(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 20f, at.z), Vector3.down, out RaycastHit hit, 60f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }

    void Lose(Enemy by) {
        if (coins == null) {
            return;
        }
        int want = Target();
        int gone = 0;
        while (shown > want) {
            shown--;
            Transform c = coins[shown];
            c.gameObject.SetActive(false);
            Launch(c.position, by, gone);
            gone++;
        }
        if (gone > 0) {
            Taken += gone;
            if (Events.Count < 40) {
                Events.Add("coins-taken|" + Time.unscaledTime.ToString("0.0") + "|" + gone + " off the pile, " + shown + "/" + Slots + " left" + (by != null ? ", to " + by.name : ""));
            }
            GameAudio.Play("coin", 0.55f, 0.7f);
        }
    }

    void Launch(Vector3 from, Enemy by, int n) {
        var f = new Flyer();
        f.t = MakeCoin("Taken Coin", 1f).transform;
        f.t.position = from;
        f.from = from;
        Vector3 to = by != null && by.Alive ? by.transform.position + Vector3.up * 1.1f : from + new Vector3(Mathf.Sin(n * 2.1f), 0.3f, Mathf.Cos(n * 2.1f)) * 3f;
        f.to = to;
        f.life = 0.55f + 0.06f * n;
        flyers.Add(f);
    }

    void Update() {
        if (slice == null) {
            return;
        }
        if (coins == null) {
            Build();
            if (coins == null) {
                return;
            }
        }
        Vector3 at = slice.transform.position + Offset;
        root.position = new Vector3(at.x, GroundY(at), at.z);
        root.gameObject.SetActive(!slice.Away);

        // she healed (or a wave revived her): coins drop back one by one
        int want = Target();
        if (shown < want) {
            refill -= Time.deltaTime;
            if (refill <= 0f) {
                refill = 0.07f;
                pop[shown] = 0f;
                coins[shown].gameObject.SetActive(true);
                shown++;
            }
        }
        for (int i = 0; i < shown; i++) {
            pop[i] = Mathf.MoveTowards(pop[i], 1f, Time.deltaTime * 6f);
            float s = Mathf.Lerp(1.6f, 1f, pop[i]);
            Vector3 p = SlotLocal(i);
            coins[i].localPosition = p + Vector3.up * (1f - pop[i]) * 0.5f;
            coins[i].localRotation = Quaternion.Euler(0f, i * 47f, (i % 3 - 1) * 4f);
            coins[i].localScale = new Vector3(s, 1f, s);
        }

        for (int i = flyers.Count - 1; i >= 0; i--) {
            Flyer f = flyers[i];
            f.age += Time.deltaTime;
            float u = Mathf.Clamp01(f.age / f.life);
            Vector3 p = Vector3.Lerp(f.from, f.to, u);
            p.y += Mathf.Sin(u * Mathf.PI) * 2.2f;
            f.t.position = p;
            f.t.rotation = Quaternion.Euler(u * 720f, u * 360f, 0f);
            if (u >= 1f) {
                Fx.Ring(f.to, 0.9f, new Color(1f, 0.85f, 0.4f, 0.8f), 0.3f);
                Destroy(f.t.gameObject);
                flyers.RemoveAt(i);
            }
        }
    }
}
