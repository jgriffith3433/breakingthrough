using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Chapter 1, "Return to Sender", the Sorting Yard (Docs/CampaignDesign.md, 4.1 and slice 1). A moonlit dead-letter yard,
// built in code inside Assets/Maps/Level_1_Yard.unity with real colliders (stones, trunks, the walls). The route:
//   the doors (start) -> the Ring, where Slice stands guard (talk) -> the west trail: three notices and the lost Mailbox
//   Flag (pick it up) -> the Courier's drop, where the Clearing Crew wakes (the fight) -> the return slot (the exit).
// No bot is awake until the flag is lifted or Wallit walks up to the crew. The level's hazard is the stamp floor: RETURN TO
// SENDER falls on the ground outside the Ring (a telegraph, then the slam). It shoves Wallit one step back and hits any bot
// under it; the Ring is the one place it cannot push. Nothing here reads the Arcade's wave flow: encounters are placed.
public class LevelYard : MonoBehaviour {
    public static readonly Vector3 RingCenter = new Vector3(0f, 0f, -10f);
    public const float RingRadius = 5f;
    public static readonly Vector3 SlicePos = new Vector3(0f, 0f, -9f);
    public static readonly Vector3 FlagPos = new Vector3(-38.6f, 0f, 12.6f);
    public static readonly Vector3 DropPos = new Vector3(34f, 0f, 10f);
    public static readonly Vector3 ExitPos = new Vector3(30f, 0f, 49f);
    public static readonly Vector3[] NoticePos = {
        new Vector3(-15f, 0f, -3f), new Vector3(-26f, 0f, 4.5f), new Vector3(-34f, 0f, 12f),
    };
    static readonly Vector3[] Trail = {
        new Vector3(-5f, 0f, -10f), new Vector3(-12f, 0f, -6f), new Vector3(-19f, 0f, 0f), new Vector3(-26f, 0f, 5f),
        new Vector3(-33f, 0f, 10.5f), new Vector3(-37f, 0f, 12.5f),
    };
    static readonly string[] NoticeBoard = {
        "NOTICE OF RETURN\nItem: wallet, leather\nSender: none on file",
        "RETURNED: 6\nDELIVERED: 0\nSee: Clearing Crew",
        "NOTICE OF STRIKING\nAddress: the Hollow\nSigned: ________",
    };
    static readonly string[] NoticeRead = {
        "NOTICE 1 of 3: Item, wallet, leather. Returned to sender. Sender: none on file.",
        "NOTICE 2 of 3: Returned, six. Delivered, none. Signed for by: the Clearing Crew.",
        "NOTICE 3 of 3: Notice of striking. Address: the Hollow. Signature: (blank).",
    };
    static readonly string[] NoticeReact = {
        "Item one: wallet, leather. That's me. Where's my cut?",
        "Zero delivered. Six returned. Somebody's cooking the ledger.",
        "It's not signed.",
    };
    static readonly string[] WallitIdle = {
        "Forty. Forty-one. Somebody short-changed me.",
        "Tap. Tap. Hear that? Hollow. Like a bank at closing.",
        "Nine stones. Looks like padding on the bill.",
        "Is that a mailbox or a bad loan?",
        "Waiting's billable.",
    };
    static readonly string[] SliceIdle = { "Holding.", "Noted.", "Closer." };

    public enum Beat { Arrive, Talked, Flag, Fight, Clear, Done }

    // For the playtest bot and the HUD.
    public Beat Stage { get; private set; }
    public Vector3 PlayerStart { get { return new Vector3(0f, 0f, -48f); } }
    public Vector3 PlayerFacing { get { return Vector3.forward; } }
    public readonly List<string> Timeline = new List<string>();
    public bool Talked { get; private set; }
    public bool Talking { get; private set; }
    public int NoticesRead { get; private set; }
    public bool FlagTaken { get; private set; }
    public bool CrewWoken { get; private set; }
    public bool CrewCleared { get; private set; }
    public bool ExitOpen { get; private set; }
    public bool SliceJoined { get { return Companion.Instance != null; } }
    public float SecondsBeforeFirstCombat { get; private set; } = -1f;
    public float CombatSeconds { get; private set; }   // seconds the Clearing Crew was awake (the only encounter in this level)
    public const int Encounters = 1;
    public float CombatShare { get { return scene != null && scene.Seconds > 0f ? CombatSeconds / scene.Seconds : 0f; } }
    public float WalkedBeforeCombat { get; private set; }
    public int StampsLanded { get; private set; }
    public int StampHits { get; private set; }
    public int BotsStamped { get; private set; }
    public bool RingSafeShown { get; private set; }
    public int StonesPassed { get; private set; }
    public string Prompt { get { return scene != null && scene.Hud != null ? scene.Hud.Prompt.text : ""; } }
    public Vector3 CrewCenter { get { return DropPos + new Vector3(5f, 0f, 0f); } }
    public List<Enemy> Crew { get { return crew; } }
    public int CrewAlive {
        get {
            int n = 0;
            for (int i = 0; i < crew.Count; i++) {
                if (crew[i] != null && crew[i].Alive) {
                    n++;
                }
            }
            return n;
        }
    }
    public Vector3 SliceFigureAt { get { return sliceFigure != null ? sliceFigure.position : SlicePos; } }

    LevelScene scene;
    Transform root;
    Transform sliceFigure;
    Animator sliceAnim;
    Transform flagItem;
    GameObject shutter;
    Light exitLight;
    Transform carried;
    readonly bool[] noticeRead = new bool[3];
    readonly bool[] stonePassed = new bool[9];
    readonly Vector3[] stonePos = new Vector3[9];
    readonly List<Enemy> crew = new List<Enemy>();
    readonly List<Vector3> crewHome = new List<Vector3>();
    readonly List<int> lateWave = new List<int>();
    class Stamp {
        public GameObject Go;
        public Renderer Ink;
        public Transform Head;
        public Vector3 At;
        public float Born;
        public bool Slammed;
        public float SlammedAt;
    }
    readonly List<Stamp> stamps = new List<Stamp>();
    class Spot {
        public Vector3 At;
        public string Prompt;
        public Func<bool> On;
        public Action Use;
    }
    readonly List<Spot> spots = new List<Spot>();
    float nextStampAt;
    float lastIdleAt;
    float lateAt = -1f;
    bool lateWoken;
    bool nearRingSaid;
    bool benchSaid;
    bool wallitWordSaid;
    float completedAt = -1f;
    Vector3 sliceLook;
    bool sliceLooksWest;
    System.Random rng = new System.Random(11);

    public void Note(string name, string detail) {
        Timeline.Add(name + "|" + detail);
    }

    // ---------------------------------------------------------------- the place

    // The ground's height: flat where the story walks (the start, the Ring, the notices, the flag, the dock, the Return Slot),
    // spoil banks and low hills round it, and the land rising past the yard's edge so the skyline sits on hills.
    static float Hill(float x, float z, float cx, float cz, float r, float h) {
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
        return Mathf.SmoothStep(h, 0f, Mathf.Clamp01(d / r));
    }

    public static float H(float x, float z) {
        float y = Hill(x, z, -30f, 30f, 17f, 3.4f) + Hill(x, z, -56f, -12f, 15f, 2.6f) + Hill(x, z, -60f, 40f, 16f, 3f)
            + Hill(x, z, 58f, -8f, 16f, 2.8f) + Hill(x, z, 60f, 38f, 17f, 3.6f) + Hill(x, z, 4f, 40f, 12f, 1.2f);
        float edge = Mathf.Max(Mathf.Abs(x) - 62f, Mathf.Max(-61f - z, z - 66f));
        if (edge > 0f) {
            y += Mathf.SmoothStep(0f, 1f, edge / 45f) * (9f + 15f * Mathf.PerlinNoise(x * 0.02f + 4f, z * 0.02f + 9f));
        }
        return y;
    }

    static Vector3 On(float x, float z) {
        return new Vector3(x, H(x, z), z);
    }

    static readonly string[] Doors5 = { "st_door_wall", "st_house_wall", "st_door_wall", "st_house_wall", "st_door_wall", "st_house_wall", "st_door_wall", "st_house_wall", "st_door_wall" };
    static readonly string[] Hall7 = { "st_house_wall", "st_house_wall", "st_shopfront", "st_door_wall", "st_shopfront", "st_house_wall", "st_house_wall" };
    static readonly string[] Houses = { "st_house_wall", "st_shopfront", "st_house_wall", "st_house_wall" };
    static readonly string[] Houses3 = { "st_house_wall", "st_door_wall", "st_house_wall" };

    public void Build(LevelScene owner) {
        scene = owner;
        root = new GameObject("Sorting Yard").transform;
        root.SetParent(transform, false);
        Ground();
        Buildings();
        Doors();
        RingAndStones();
        TrailAndTrunks();
        Notices();
        Mailbox();
        DropOff();
        ReturnSlot();
        Lamps();
        Dressing();
        SliceFigure();
        Crew5();
        BuildSpots();
        Physics.SyncTransforms();
    }

    // The ground is one heightfield mesh mapped with the kit's gravel (a collider on the Ground layer: bots and Slice stand on
    // it, the camera ray reads it), and a coarse skirt of hills round it, wider than the walls, for the skyline to stand on.
    void Ground() {
        Material gravel = LevelDress.GroundMaterial("yard", "concrete", new Color(0.5f, 0.56f, 0.7f));
        Mesh inner = LevelDress.GridMesh("Yard Ground", -72f, -72f, 72f, 72f, 2.5f, H, 4f);
        LevelDress.Surface(root, "Ground", inner, gravel, true, 3);
        Mesh skirt = LevelDress.GridMesh("Yard Hills", -300f, -300f, 300f, 324f, 12f, H, 6f, (x, z) => x > -72f && x < 72f && z > -72f && z < 72f);
        LevelDress.Surface(root, "Hills", skirt, LevelDress.GroundMaterial("yard", "stone", new Color(0.4f, 0.46f, 0.6f)), false, 0);
        // The yard's edge: invisible walls behind the facades, fences and trees, so nobody walks off the heightfield.
        LevelDress.Proxy(root, "Edge W", new Vector3(-67f, 4f, 4f), new Vector3(2f, 10f, 140f));
        LevelDress.Proxy(root, "Edge E", new Vector3(67f, 4f, 4f), new Vector3(2f, 10f, 140f));
        LevelDress.Proxy(root, "Edge S", new Vector3(0f, 4f, -62f), new Vector3(138f, 10f, 2f));
        LevelDress.Proxy(root, "Edge N", new Vector3(0f, 4f, 70f), new Vector3(138f, 10f, 2f));
        // The scar: burnt stamp ink on the stones, the dark patches the Department leaves where it has struck.
        Material ink = LevelDress.GroundMaterial("yard", "concrete", new Color(0.55f, 0.16f, 0.16f));
        for (int i = 0; i < 7; i++) {
            Vector3 p = new Vector3(Rand(-20f, 20f), 0f, Rand(-30f, 6f));
            if (Vector3.Distance(LevelKit.Flat(p), RingCenter) < RingRadius + 2f) {
                continue;
            }
            GameObject scar = LevelKit.Decal(root, "Scar", new Vector3(p.x, H(p.x, p.z) + 0.04f + i * 0.004f, p.z), new Vector2(Rand(1.6f, 3.4f), Rand(1.6f, 3.4f)), Color.white);
            scar.GetComponent<MeshRenderer>().sharedMaterial = ink;
            scar.transform.rotation = Quaternion.Euler(0f, Rand(0f, 90f), 0f);
        }
    }

    // Buildings that close the yard without boxing it in: the Return Hall behind the start (two wings and an alley), the Dead
    // Letter Office with its clock tower in the north, houses along the west and east with alleys between, and a skyline of
    // roofs beyond the edge. Each one is facade bays and roofs from the kit with one simple collider for its footprint.
    void Buildings() {
        Transform b = new GameObject("Buildings").transform;
        b.SetParent(root, false);
        LevelDress.Facade(b, "Return Hall", On(0f, -60f), 0f, Doors5, 2, root);
        LevelDress.Facade(b, "Return Hall West Wing", On(-31f, -60f), 0f, Houses, 2, root);
        LevelDress.Facade(b, "Return Hall East Wing", On(31f, -60f), 0f, Houses, 2, root);
        LevelDress.Facade(b, "Dead Letter Office", On(0f, 66f), 180f, Hall7, 3, root);
        LevelDress.Facade(b, "Office West Row", On(-32f, 66f), 180f, Houses3, 2, root);
        LevelDress.Facade(b, "Office East Row", On(34f, 66f), 180f, Houses3, 2, root);
        LevelDress.Facade(b, "West Houses A", On(-65f, -34f), 90f, Houses, 2, root);
        LevelDress.Facade(b, "West Houses B", On(-65f, 4f), 90f, Houses3, 2, root, true);
        LevelDress.Facade(b, "West Houses C", On(-65f, 46f), 90f, Houses, 2, root);
        LevelDress.Facade(b, "East Houses A", On(65f, -36f), -90f, Houses3, 2, root);
        LevelDress.Facade(b, "East Houses B", On(65f, 28f), -90f, Houses, 2, root, true);
        // The clock tower stands behind the office: a lit face seen from the whole yard.
        GameObject tower = LevelDress.Put(b, "yd_clock_tower", On(0f, 74f), 0f, 1.5f);
        tower.name = "Clock Tower";
        LevelDress.PointLight(b, new Vector3(0f, 17f, 62f), new Color(1f, 0.82f, 0.45f), 40f, 1.4f);
        // The skyline: roofs beyond the edge, each a single row, lower and darker the farther they stand.
        var rng = new System.Random(31);
        Transform far = new GameObject("Skyline").transform;
        far.SetParent(b, false);
        for (int i = 0; i < 9; i++) {
            float a = (i * 40f + 12f) * Mathf.Deg2Rad;
            float d = 105f + (i % 3) * 16f;
            Vector3 p = new Vector3(Mathf.Sin(a) * d, 0f, 4f + Mathf.Cos(a) * d * 0.9f);
            p.y = H(p.x, p.z);
            Vector3 toYard = -new Vector3(p.x, 0f, p.z - 4f);
            float yaw = Mathf.Atan2(toYard.x, toYard.z) * Mathf.Rad2Deg;
            LevelDress.Facade(far, "Far Roofs " + i, p, yaw, i % 2 == 0 ? Houses3 : Houses, 2 + i % 3, null, i % 4 == 3);
        }
    }

    // Five shut doors in the Return Hall's facade (the first five bays of Doors5 that are door walls): every one of them
    // stamped him RETURN TO SENDER, and none of them opens. Ink lies on every step.
    void Doors() {
        Material ink = LevelDress.GroundMaterial("yard", "concrete", new Color(0.62f, 0.16f, 0.16f));
        for (int i = 0; i < 5; i++) {
            float x = -16f + i * 8f;
            TextMesh t = LevelKit.Text(root, "RETURNED", On(x, -59.1f) + new Vector3(0f, 1.25f, 0f), 0.045f, new Color(0.9f, 0.3f, 0.3f), false);
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            GameObject mark = LevelKit.Decal(root, "Ink", new Vector3(x, 0.05f, -56.4f), new Vector2(3f, 1.6f), Color.white);
            mark.GetComponent<MeshRenderer>().sharedMaterial = ink;
        }
        LevelDress.PointLight(root, new Vector3(-10f, 4f, -55f), new Color(1f, 0.86f, 0.55f), 15f, 1.1f);
    }

    void RingAndStones() {
        // The gold ring: two bands of kerb segments round the Ring, and nine standing stones; the gap faces the doors.
        for (int i = 0; i < 10; i++) {
            float a = i * 36f;
            float rad = a * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            LevelDress.Put(root, "yd_ring_segment", RingCenter + dir * 4.96f + Vector3.up * 0.02f, -a, 1.6f);
            LevelDress.Put(root, "yd_ring_segment", RingCenter + dir * 3.1f + Vector3.up * 0.02f, -a, 1f);
        }
        LevelDress.PointLight(root, RingCenter + new Vector3(0f, 3f, 0f), new Color(0.95f, 0.8f, 0.4f), 18f, 1.2f);
        for (int i = 0; i < 9; i++) {
            float a = (270f + 20f + i * 40f) * Mathf.Deg2Rad;
            Vector3 p = RingCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10.5f;
            stonePos[i] = p;
            LevelDress.Put(root, "yd_standing_stone", On(p.x, p.z), i * 31f, 1.7f + (i % 3) * 0.12f);
            LevelDress.Proxy(root, "Stone " + i, On(p.x, p.z) + Vector3.up * 2f, new Vector3(1.5f, 4f, 1.5f), i * 31f);
        }
        // Chalk stepping slabs: the trail from the doors to the Ring.
        for (float z = -54f; z < -17f; z += 2.6f) {
            Vector3 s = new Vector3(Rand(-0.6f, 0.6f), 0f, z);
            LevelDress.PutFlat(root, "yd_gravel_slab", On(s.x, s.z), Rand(0f, 90f), 1.35f, 0.3f);
        }
    }

    void TrailAndTrunks() {
        for (int i = 0; i < Trail.Length - 1; i++) {
            for (float t = 0.2f; t < 1f; t += 0.38f) {
                Vector3 p = Vector3.Lerp(Trail[i], Trail[i + 1], t);
                LevelDress.PutFlat(root, "yd_gravel_slab", On(p.x, p.z), Rand(0f, 90f), 1.2f, 0.3f);
            }
        }
        // The east way, to the drop.
        for (float x = 8f; x < 30f; x += 2.8f) {
            float z = Mathf.Lerp(-8f, 8f, (x - 8f) / 22f);
            LevelDress.PutFlat(root, "yd_gravel_slab", On(x, z), Rand(0f, 90f), 1.3f, 0.3f);
        }
        // West of the Ring: dead trees and stumps with a clear lane along the chalk (a collider each, thin).
        Transform trees = new GameObject("Dead Trees").transform;
        trees.SetParent(root, false);
        int made = 0;
        for (int tries = 0; tries < 600 && made < 46; tries++) {
            Vector3 p = new Vector3(Rand(-62f, -17f), 0f, Rand(-48f, 58f));
            if (NearTrail(p, 4.6f) || Vector3.Distance(p, FlagPos) < 7f || Vector3.Distance(LevelKit.Flat(p), RingCenter) < 15f
                || Vector3.Distance(p, NoticePos[0]) < 3f || Vector3.Distance(p, NoticePos[1]) < 3f || Vector3.Distance(p, NoticePos[2]) < 3f) {
                continue;
            }
            float sc = Rand(0.8f, 1.35f);
            Vector3 at = On(p.x, p.z);
            LevelDress.Put(trees, made % 5 == 4 ? "yd_stump" : "yd_dead_tree", at, Rand(0f, 360f), made % 5 == 4 ? Rand(1f, 1.6f) : sc);
            LevelDress.Proxy(trees, "Trunk", at + Vector3.up * 2f, new Vector3(0.7f, 4f, 0.7f));
            made++;
        }
        // The spoil bank's secret: seven stumps round a carved stone, where the chalk arrow points.
        Vector3 top = On(-30f, 30f);
        LevelDress.Put(root, "yd_standing_stone", top, 20f, 1.3f);
        for (int i = 0; i < 7; i++) {
            float a = i * 51.4f * Mathf.Deg2Rad;
            Vector3 p = new Vector3(top.x + Mathf.Cos(a) * 3.6f, 0f, top.z + Mathf.Sin(a) * 3.6f);
            LevelDress.Put(root, "yd_stump", On(p.x, p.z), i * 47f, 0.8f + (i % 3) * 0.15f);
        }
        LevelDress.PointLight(root, top + new Vector3(0f, 2.4f, 0f), new Color(0.6f, 0.75f, 1f), 10f, 0.6f);
        // East and north-east: a lower bank of dead trees, a few stumps and the dock's fence.
        for (int i = 0; i < 12; i++) {
            Vector3 p = new Vector3(Rand(48f, 62f), 0f, Rand(-30f, 55f));
            if (Vector3.Distance(p, DropPos) < 16f || Vector3.Distance(p, ExitPos) < 10f) {
                continue;
            }
            LevelDress.Put(trees, "yd_dead_tree", On(p.x, p.z), Rand(0f, 360f), Rand(0.9f, 1.4f));
            LevelDress.Proxy(trees, "Trunk", On(p.x, p.z) + Vector3.up * 2f, new Vector3(0.7f, 4f, 0.7f));
        }
    }

    static bool NearTrail(Vector3 p, float d) {
        for (int i = 0; i < Trail.Length - 1; i++) {
            Vector3 a = Trail[i];
            Vector3 b = Trail[i + 1];
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            if ((a + ab * t - p).magnitude < d) {
                return true;
            }
        }
        return false;
    }

    void Notices() {
        for (int i = 0; i < 3; i++) {
            Vector3 p = NoticePos[i];
            Vector3 toRing = LevelKit.Flat(RingCenter - p);
            float yaw = Mathf.Atan2(toRing.x, toRing.z) * Mathf.Rad2Deg;
            GameObject board = LevelDress.Put(root, "yd_notice_board", On(p.x, p.z), yaw, 1f);
            LevelDress.Proxy(root, "Notice post", On(p.x, p.z) + Vector3.up * 1.1f, new Vector3(2.1f, 2.2f, 0.3f), yaw);
            TextMesh t = LevelKit.Text(board.transform, NoticeBoard[i], new Vector3(0f, 1.68f, 0.075f), 0.024f, new Color(0.2f, 0.1f, 0.1f), false);
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    // The fallen mailbox, the stump it came off, and the flag that lay beside it: a lost thing, waving at nobody.
    void Mailbox() {
        Vector3 m = new Vector3(-41.5f, 0f, 14.5f);
        GameObject box = LevelDress.Put(root, "yd_fallen_mailbox", On(m.x, m.z), 28f, 1.5f);
        box.name = "Fallen Mailbox";
        LevelDress.Proxy(root, "Mailbox collider", On(m.x, m.z) + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 1.6f), 28f);
        LevelDress.Put(root, "yd_stump", On(m.x + 2.6f, m.z + 1.4f), 40f, 1.1f);
        var item = new GameObject("MailboxFlag");
        item.transform.SetParent(root, false);
        item.transform.position = FlagPos + Vector3.up * 0.8f;
        GameObject flag = LevelDress.Put(item.transform, "yd_mail_flag", FlagPos + Vector3.up * 0.1f, 0f, 1f);
        LevelDress.Tint(flag, 1, new Color(0.85f, 0.2f, 0.2f));
        flag.transform.localPosition = new Vector3(0f, -0.7f, 0f);
        flagItem = item.transform;
        LevelDress.PointLight(root, FlagPos + new Vector3(0f, 2.2f, 0f), new Color(1f, 0.85f, 0.5f), 9f, 0.7f);
    }

    // The sorting dock: the drop box on its mats, trestles, sacks of dead letters, post boxes and crates, with a fence along the
    // north. The Clearing Crew sleeps east of it.
    void DropOff() {
        Vector3 d = DropPos;
        GameObject drop = LevelDress.Put(root, "yd_drop_box", On(d.x, d.z), -90f, 1.4f);
        drop.name = "Drop Box";
        // No collider on the drop box: the crew marches west past it in straight lines.
        TextMesh t = LevelKit.Text(root, "DROP", On(d.x, d.z) + new Vector3(-2.2f, 1.3f, 0f), 0.06f, new Color(0.95f, 0.8f, 0.45f), false);
        t.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        for (float z = -3f; z < 3.1f; z += 3f) {
            LevelDress.PutFlat(root, "yd_gravel_slab", On(d.x - 4.5f, d.z + z), 0f, 1.7f, 0.35f);
        }
        LevelDress.Put(root, "yd_sorting_trestle", On(27f, 20f), 90f, 1f);
        LevelDress.Put(root, "yd_sorting_trestle", On(27f, 15.5f), 90f, 1f);
        LevelDress.Put(root, "yd_sorting_trestle", On(26f, -1f), 80f, 1f);
        LevelDress.Proxy(root, "Trestle run", On(27f, 18f) + Vector3.up * 0.7f, new Vector3(1f, 1.4f, 9f));
        LevelDress.Proxy(root, "Trestle run 2", On(26f, -1f) + Vector3.up * 0.7f, new Vector3(1f, 1.4f, 2.8f), 80f);
        LevelDress.Put(root, "yd_sack_pile", On(29.5f, 22.5f), 30f, 1.1f);
        LevelDress.Put(root, "yd_sack_pile", On(24.5f, 12f), 120f, 1f);
        LevelDress.Put(root, "yd_sack_pile", On(28f, -4.5f), 200f, 0.9f);
        LevelDress.Put(root, "yd_post_box", On(31f, 25f), 160f, 1.2f);
        LevelDress.Put(root, "yd_post_box", On(33.5f, 24.6f), 190f, 1.1f);
        LevelDress.Put(root, "yd_post_box", On(23f, 1f), 70f, 1.15f);
        LevelDress.Proxy(root, "Post boxes", On(32.2f, 24.8f) + Vector3.up * 0.9f, new Vector3(3.6f, 1.8f, 0.9f));
        LevelDress.Proxy(root, "Post box W", On(23f, 1f) + Vector3.up * 0.9f, new Vector3(0.9f, 1.8f, 0.9f));
        for (int i = 0; i < 4; i++) {
            Vector3 c = On(47f + (i % 2) * 1.1f, 20.5f + (i / 2) * 1.1f);
            LevelDress.Put(root, "st_crate", c, 10f * i, 1.1f);
            if (i == 1) {
                LevelDress.Put(root, "st_crate", c + Vector3.up * 0.96f, 33f, 0.9f);
            }
        }
        LevelDress.Proxy(root, "Crates", On(47.5f, 21f) + Vector3.up * 0.5f, new Vector3(2.6f, 1f, 2.6f));
        LevelDress.Put(root, "fl_barrel", On(50f, 3f), 0f, 1.1f);
        LevelDress.Put(root, "fl_barrel", On(51.2f, 4.1f), 40f, 1f);
        LevelDress.Proxy(root, "Barrels", On(50.6f, 3.5f) + Vector3.up * 0.5f, new Vector3(2.4f, 1f, 2.4f));
        for (int i = 0; i < 5; i++) {
            LevelDress.Put(root, "yd_fence", On(35f + i * 2f, 27f), 0f, 1f);
        }
        LevelDress.Proxy(root, "Dock fence", On(40f, 27f) + Vector3.up * 0.6f, new Vector3(10f, 1.2f, 0.3f));
    }

    // The way out: a stone frame with a rusty roller shutter that lifts once the crew is down, an amber strip over it.
    void ReturnSlot() {
        Vector3 p = ExitPos;
        Vector3 at = On(p.x, p.z);
        GameObject frame = LevelDress.Put(root, "yd_slot_frame", at, 180f, 1f);
        frame.name = "Return Slot";
        shutter = new GameObject("Shutter");
        shutter.transform.SetParent(root, false);
        shutter.transform.position = at;
        GameObject leaf = LevelDress.Put(shutter.transform, "yd_slot_shutter", at + new Vector3(0f, 0f, -0.1f), 180f, 1f);
        LevelDress.Proxy(shutter.transform, "Shutter collider", at + new Vector3(0f, 2.1f, -0.1f), new Vector3(3.9f, 4.2f, 0.6f));
        TextMesh t = LevelKit.Text(leaf.transform, "RETURN SLOT", new Vector3(0f, 2.25f, 0.2f), 0.04f, new Color(0.15f, 0.07f, 0.04f), false);
        t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        LevelDress.Proxy(root, "Slot post L", at + new Vector3(-2.05f, 2.2f, 0f), new Vector3(0.9f, 4.4f, 1f));
        LevelDress.Proxy(root, "Slot post R", at + new Vector3(2.05f, 2.2f, 0f), new Vector3(0.9f, 4.4f, 1f));
        for (int s = -1; s <= 1; s += 2) {
            for (int i = 0; i < 4; i++) {
                LevelDress.Put(root, "yd_fence", On(p.x + s * (4.2f + i * 2f), p.z), 0f, 1f);
            }
            LevelDress.Proxy(root, "Slot fence", On(p.x + s * 8.2f, p.z) + Vector3.up * 0.6f, new Vector3(8f, 1.2f, 0.3f));
            LevelDress.Put(root, "yd_dead_tree", On(p.x + s * 13f, p.z + 1.5f), s * 70f, 1.1f);
        }
        exitLight = LevelDress.PointLight(root, at + new Vector3(0f, 3.2f, 2f), new Color(1f, 0.7f, 0.3f), 14f, 0.5f);
    }

    // Lamps: lantern posts along the chalk and at the Ring and dock, with a light at each lantern.
    void Lamps() {
        Vector3[] at = { new Vector3(-7f, 0f, -16f), new Vector3(7f, 0f, -16f), new Vector3(-8f, 0f, -3f), DropPos + new Vector3(-6f, 0f, 3f), new Vector3(-3.4f, 0f, -42f), new Vector3(3.4f, 0f, -30f), new Vector3(10f, 0f, 38f) };
        bool[] lit = { true, true, true, true, true, false, true };
        for (int i = 0; i < at.Length; i++) {
            float yaw = i % 2 == 0 ? 0f : 180f;
            Vector3 pos = On(at[i].x, at[i].z);
            LevelDress.Put(root, "st_lamp_post", pos, yaw, 1f);
            LevelDress.Proxy(root, "Lamp post", pos + Vector3.up * 1.6f, new Vector3(0.5f, 3.2f, 0.5f));
            if (lit[i]) {
                Vector3 lantern = pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.62f, 3.4f, 0f);
                LevelDress.PointLight(root, lantern, i == 3 ? new Color(1f, 0.7f, 0.35f) : new Color(1f, 0.86f, 0.55f), 15f, 1.25f);
            }
        }
    }

    // Everything between: fence runs along the chalk, a signpost, benches and bins at the Return Hall, crates by the alley, and
    // the Wallit-sized clutter that makes the ground read as a yard in use.
    void Dressing() {
        for (int s = -1; s <= 1; s += 2) {
            for (int i = 0; i < 6; i++) {
                LevelDress.Put(root, "yd_fence", On(s * 7.2f, -42f + i * 2f), 90f, 1f);
            }
            LevelDress.Proxy(root, "Chalk fence", On(s * 7.2f, -36f) + Vector3.up * 0.6f, new Vector3(0.3f, 1.2f, 12f));
        }
        LevelDress.Put(root, "yd_signpost", On(3.2f, -26f), 200f, 1f);
        LevelDress.Proxy(root, "Signpost", On(3.2f, -26f) + Vector3.up * 1f, new Vector3(0.4f, 2f, 0.4f));
        LevelDress.Put(root, "st_bench", On(-24f, -53f), 0f, 1f);
        LevelDress.Put(root, "st_bin", On(-21.5f, -53.5f), 0f, 1f);
        LevelDress.Put(root, "st_bin", On(-20.8f, -53.4f), 40f, 1f);
        LevelDress.Put(root, "st_hydrant", On(-12.5f, -54f), 0f, 1f);
        LevelDress.Put(root, "st_crate", On(21f, -53f), 15f, 1.1f);
        LevelDress.Put(root, "st_crate", On(22.2f, -53.2f), 75f, 0.95f);
        LevelDress.Put(root, "st_crate", On(21.6f, -53.1f) + Vector3.up * 0.9f, 40f, 0.9f);
        LevelDress.Put(root, "yd_sack_pile", On(25f, -52f), 160f, 1f);
        LevelDress.Proxy(root, "Hall clutter W", On(-22.5f, -53f) + Vector3.up * 0.6f, new Vector3(4f, 1.2f, 1.2f));
        LevelDress.Proxy(root, "Hall clutter E", On(22.5f, -53f) + Vector3.up * 0.6f, new Vector3(4.5f, 1.2f, 1.5f));
        // Dead-letter pillar boxes beside the first notices, and a trestle with the sorted and unsorted.
        LevelDress.Put(root, "yd_post_box", On(-12f, -14f), 20f, 1.1f);
        LevelDress.Put(root, "yd_sorting_trestle", On(-18f, -22f), 55f, 1f);
        LevelDress.Put(root, "yd_sack_pile", On(-17f, -24f), 10f, 0.9f);
        LevelDress.Proxy(root, "Post box NW", On(-12f, -14f) + Vector3.up * 0.8f, new Vector3(0.9f, 1.6f, 0.9f));
        LevelDress.Proxy(root, "Trestle NW", On(-18f, -22f) + Vector3.up * 0.7f, new Vector3(1f, 1.4f, 2.8f), 55f);
    }

    // Slice, on guard at the Ring: the painted model on its idle (nothing without it).
    void SliceFigure() {
        GameObject prefab = Resources.Load<GameObject>("Slice/Slice");
        GameObject go;
        if (prefab != null) {
            go = Instantiate(prefab);
            Musician.ApplySliceSkins(go);
            sliceAnim = go.GetComponent<Animator>();
            if (sliceAnim == null) {
                sliceAnim = go.GetComponentInChildren<Animator>();
            }
            if (sliceAnim != null) {
                sliceAnim.applyRootMotion = false;
                int idle = Animator.StringToHash("idle");
                if (sliceAnim.HasState(0, idle)) {
                    sliceAnim.Play(idle, 0, 0f);
                }
            }
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) {
                Destroy(c);
            }
            foreach (MonoBehaviour m in go.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (m is SliceAnimRelay) {
                    Destroy(m);
                }
            }
        } else {
            go = new GameObject();
        }
        go.name = "Slice (on guard)";
        go.transform.SetParent(root, false);
        go.transform.position = SlicePos;
        go.transform.rotation = Quaternion.LookRotation(Vector3.back);
        sliceFigure = go.transform;
    }

    // Five Yo Bots of the Clearing Crew queue at the drop with their lids down: three march first, two more are late.
    void Crew5() {
        Vector3 c = CrewCenter;
        Vector3[] at = { c + new Vector3(0f, 0f, -4f), c + new Vector3(3f, 0f, 0f), c + new Vector3(0f, 0f, 4f), c + new Vector3(7f, 0f, -2f), c + new Vector3(7f, 0f, 3f) };
        for (int i = 0; i < at.Length; i++) {
            bool late = i >= 3;
            bool dispatch = i == 4;
            BotDef def = BotRoster.ForSpawn(BotKind.YoBot, 1);
            var go = new GameObject(dispatch ? "Dispatch Yo Bot" : def.Name);
            go.tag = "enemy";
            Vector3 p = at[i];
            p.y = 0.55f + def.Scale * 0.08f;
            go.transform.position = p;
            go.transform.rotation = Quaternion.LookRotation(Vector3.left);
            Enemy e = go.AddComponent<Enemy>();
            e.ApplyBot(def);
            if (dispatch) {
                e.MakeElite(BotTrait.Sprinter);
            }
            e.Dormant = true;
            crew.Add(e);
            crewHome.Add(p);
            if (late) {
                lateWave.Add(i);
            }
        }
    }

    void BuildSpots() {
        spots.Add(new Spot { At = SlicePos, Prompt = "talk to Slice", On = () => !Talked && !Talking && Stage != Beat.Done, Use = () => StartCoroutine(Talk()) });
        for (int i = 0; i < 3; i++) {
            int n = i;
            spots.Add(new Spot { At = NoticePos[i], Prompt = "read the notice", On = () => !noticeRead[n], Use = () => ReadNotice(n) });
        }
        spots.Add(new Spot { At = FlagPos, Prompt = "pick up the Mailbox Flag", On = () => !FlagTaken, Use = TakeFlag });
    }

    float Rand(float a, float b) {
        return a + (float)rng.NextDouble() * (b - a);
    }

    // ---------------------------------------------------------------- the beats

    public void Begin() {
        nextStampAt = Time.time + 6f;
        lastIdleAt = Time.time;
        scene.Hud.Objective.text = Objective();
        StartCoroutine(Opening());
        Note("level-start", "Level_1_Yard at " + PlayerStart);
    }

    // The hook: within the first second a stamp falls beside him (to his right, clear of the chalk walk) and slams RETURN TO
    // SENDER into the ground, and he answers it. The yard's rule is shown before anyone says it.
    IEnumerator Opening() {
        yield return new WaitForSeconds(0.8f);
        DropStampAt(PlayerStart + new Vector3(4.5f, 0f, 6f), "the opening");
        nextStampAt = Time.time + 9f;
        yield return new WaitForSeconds(Telegraph + 0.5f);
        LevelBarks.Say('W', "Return to sender. Which sender? I want a refund.", "arrive", true);
    }

    string Objective() {
        switch (Stage) {
            case Beat.Arrive:
                return "Talk to Slice at the Ring, straight ahead.";
            case Beat.Talked:
                return "Read the notices, west trail (" + NoticesRead + "/3).";
            case Beat.Flag:
                return "Return to sender: the drop, east.";
            case Beat.Fight:
                return "Clear the Crew (" + CrewAlive + " left).";
            case Beat.Clear:
                return "Out: the return slot, north-east.";
            default:
                return "Filed.";
        }
    }

    IEnumerator Talk() {
        Talking = true;
        Note("talk", "Slice at the Ring");
        string[] who = { "W", "S", "W", "S", "W", "S", "W", "S" };
        string[] text = {
            "Slice. Straight answer. Who stamped me?",
            "Nobody signs it. That's how it works.",
            "Then I'll make it a refund.",
            "Look west. The trail. Lost things, lying where they landed.",
            "None of this is mine. I'm keeping it anyway.",
            "Wallit.",
            "Finders keepers. Quietly.",
            "Wallit. Stay close.",
        };
        for (int i = 0; i < who.Length; i++) {
            if (i == 3) {
                sliceLooksWest = true;
            }
            if (i == 5) {
                sliceLooksWest = false;
            }
            while (LevelBarks.Busy || Cutscene.IsOpen) {
                yield return null;
            }
            LevelBarks.Say(who[i][0], text[i], "talk", true);
            yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.25f);
        }
        Talked = true;
        Talking = false;
        if (Stage == Beat.Arrive) {
            Stage = Beat.Talked;
        }
        JoinSlice();
    }

    // Slice steps off the Ring and walks with him (Companion.cs): from here she follows, fights and can go down.
    void JoinSlice() {
        Vector3 from = SliceFigureAt;
        Companion.Spawn("slice");
        if (Companion.Instance != null) {
            Companion.Instance.transform.position = from;
            if (sliceFigure != null) {
                Destroy(sliceFigure.gameObject);
                sliceFigure = null;
            }
            Note("companion-joined", "Slice walks with Wallit");
        }
    }

    void ReadNotice(int i) {
        noticeRead[i] = true;
        NoticesRead++;
        scene.Hud.ShowReceipt("NOTICE " + (i + 1) + " OF 3", 2.5f);
        GameAudio.Play("paper_slide", 0.6f);
        Note("notice", (i + 1) + " of 3: " + NoticeRead[i]);
        LevelBarks.Say('W', NoticeReact[i], "notice", true);
        if (i == 2 && Companion.Instance != null) {
            StartCoroutine(SliceAnswers());
        }
    }

    IEnumerator SliceAnswers() {
        yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.3f);
        LevelBarks.Say('S', "Noted.", "notice", true);
    }

    void TakeFlag() {
        FlagTaken = true;
        if (flagItem != null) {
            flagItem.SetParent(scene.Player, false);
            flagItem.localPosition = new Vector3(0.28f, 1.55f, -0.34f);
            flagItem.localRotation = Quaternion.Euler(0f, 0f, 12f);
            flagItem.localScale = Vector3.one * 0.55f;
            carried = flagItem;
        }
        GameAudio.Play("coin", 0.8f, 1.1f);
        Fx.Ring(FlagPos + Vector3.up * 0.2f, 4f, new Color(1f, 0.85f, 0.45f, 0.8f), 0.9f);
        scene.Hud.ShowReceipt("RECEIPT: Mailbox Flag. Somebody's. Still waving.", 6f);
        Note("pickup", "Mailbox Flag, carried on Wallit's back");
        if (Stage == Beat.Arrive || Stage == Beat.Talked) {
            Stage = Beat.Flag;
        }
        StartCoroutine(AfterFlag());
    }

    IEnumerator AfterFlag() {
        LevelBarks.Say('W', "Finders keepers.", "pickup", true);
        if (Companion.Instance != null) {
            yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.2f);
            LevelBarks.Say('S', "Yours.", "pickup", true);
        }
        yield return new WaitForSeconds(2.6f);
        // Lifting it was the signal: the crew at the drop has its report to close.
        WakeCrew("flag lifted");
    }

    void WakeCrew(string cause) {
        if (CrewWoken || Stage == Beat.Done) {
            return;
        }
        CrewWoken = true;
        if (SecondsBeforeFirstCombat < 0f) {
            SecondsBeforeFirstCombat = scene.Seconds;
            WalkedBeforeCombat = scene.Walked;
        }
        Stage = Beat.Fight;
        Note("encounter-wake", "crew at the drop (" + cause + "), " + crew.Count + " bots");
        LevelBarks.Say('Y', "Hello! Uploading victory! Striking this address now!", "wake", true);
        scene.Checkpoint = RingCenter + new Vector3(15f, 0f, -6f);
        StartCoroutine(MarchOut());
    }

    IEnumerator MarchOut() {
        yield return new WaitForSeconds(0.8f);
        LevelBarks.Quiet = true;   // no barks over the fight
        for (int i = 0; i < crew.Count; i++) {
            if (lateWave.Contains(i)) {
                continue;
            }
            if (crew[i] != null) {
                crew[i].Dormant = false;
                Note("enemy-woke", crew[i].Title + " " + (i + 1));
            }
            yield return new WaitForSeconds(0.5f);
        }
        lateAt = Time.time + 9f;
    }

    void WakeLate() {
        lateWoken = true;
        LevelBarks.Quiet = false;
        LevelBarks.Say('Y', "Rush delivery! Rush delivery!", "late", true);
        LevelBarks.Quiet = true;
        for (int i = 0; i < crew.Count; i++) {
            if (lateWave.Contains(i) && crew[i] != null) {
                crew[i].Dormant = false;
                Note("enemy-woke", crew[i].Title + " " + (i + 1) + " (late)");
            }
        }
    }

    // A defeat puts the bots back where they stood, asleep, so the fight can be tried again from the door.
    public void Resumed() {
        if (CrewCleared) {
            return;
        }
        for (int i = 0; i < crew.Count; i++) {
            if (crew[i] != null && crew[i].Alive) {
                crew[i].transform.position = crewHome[i];
                crew[i].Dormant = true;
            }
        }
        CrewWoken = false;
        lateWoken = false;
        lateAt = -1f;
        LevelBarks.Quiet = false;
        if (Stage == Beat.Fight) {
            Stage = FlagTaken ? Beat.Flag : Beat.Talked;
        }
    }

    void ClearedCrew() {
        CrewCleared = true;
        Stage = Beat.Clear;
        LevelBarks.Quiet = false;
        ExitOpen = true;
        if (shutter != null) {
            Destroy(shutter);
        }
        if (exitLight != null) {
            exitLight.intensity = 2.6f;
            exitLight.range = 20f;
        }
        GameAudio.Play("stamp", 0.5f, 1.3f);
        Note("encounter-clear", "the Clearing Crew is down");
        StartCoroutine(AfterClear());
    }

    IEnumerator AfterClear() {
        yield return new WaitForSeconds(1.2f);
        LevelBarks.Say('S', "Clean.", "clear", true);
        yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.2f);
        LevelBarks.Say('W', "Paid in full.", "clear", true);
        yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.4f);
        if (Companion.Instance != null) {
            LevelBarks.Say('S', "Out. North-east.", "exit", true);
        } else {
            LevelBarks.Say('W', "Slot's open. Collect.", "exit", true);
        }
    }

    void Complete() {
        Stage = Beat.Done;
        completedAt = Time.time;
        scene.Finish();
        Note("pacing", "encounters=" + Encounters + " (the Clearing Crew, placed asleep; nothing spawns on a timer) combatSeconds=" + Mathf.RoundToInt(CombatSeconds) + " playSeconds=" + Mathf.RoundToInt(scene.Seconds) + " combatShare=" + Mathf.RoundToInt(CombatShare * 100f) + "%");
        Note("level-complete", "exit reached after " + Mathf.RoundToInt(scene.Seconds) + " s, " + scene.Defeats + " defeats");
        scene.Hud.Objective.text = Objective();
        scene.Hud.Prompt.text = "Chapter 1 filed. " + Interact.Prompt("back to the hub");
    }

    // ---------------------------------------------------------------- stamps

    public void DropStampAt(Vector3 at, string cause) {
        at.y = H(at.x, at.z) + 0.05f;
        var go = new GameObject("StampMark");
        go.transform.SetParent(root, false);
        go.transform.position = at;
        GameObject ink = LevelKit.Decal(go.transform, "Ink", Vector3.zero, new Vector2(3.8f, 2.7f), new Color(0.76f, 0.23f, 0.23f, 0.25f));
        Renderer r = ink.GetComponent<Renderer>();
        r.material = new Material(r.sharedMaterial);
        TextMesh t = LevelKit.Text(go.transform, "RETURN\nTO SENDER", new Vector3(0f, 0.06f, 0f), 0.07f, new Color(1f, 0.85f, 0.85f, 0.9f), true);
        t.name = "Words";
        if (Camera.main != null) {
            t.transform.rotation = Quaternion.Euler(90f, Camera.main.transform.eulerAngles.y, 0f);   // reads top-up from where the camera looks
        }
        GameObject head = LevelDress.Put(go.transform, "gt_press_head", at + new Vector3(0f, 9f, 0f), 0f, 0.55f);
        head.name = "StampHead";
        stamps.Add(new Stamp { Go = go, Ink = r, Head = head.transform, At = at, Born = Time.time });
        Note("stamp-falls", cause + " at " + at.ToString("0"));
    }

    const float Telegraph = 1.5f;

    void TickStamps() {
        if (CrewWoken && !CrewCleared) {
            if (Time.time >= nextStampAt && stamps.Count < 4) {
                nextStampAt = Time.time + 5.5f;
                Enemy target = null;
                for (int i = 0; i < crew.Count; i++) {
                    if (crew[i] != null && crew[i].Alive && !crew[i].Dormant) {
                        target = crew[i];
                        break;
                    }
                }
                if (target != null) {
                    Vector3 lead = target.transform.position + target.transform.forward * 3.5f;
                    DropStampAt(lead, "under the Crew");
                }
            }
        } else if (!CrewCleared && Time.time >= nextStampAt && stamps.Count < 3 && !Talking) {
            nextStampAt = Time.time + Rand(8f, 11f);
            Vector3 dir = scene.Player.forward;
            Vector3 at = scene.Player.position + dir * Rand(6f, 9f) + scene.Player.right * Rand(-3.5f, 3.5f);
            if (Vector3.Distance(LevelKit.Flat(at), RingCenter) > RingRadius + 2.5f && Mathf.Abs(at.x) < 60f && at.z > -56f && at.z < 60f) {
                DropStampAt(at, "ahead of Wallit");
            }
        }

        for (int i = stamps.Count - 1; i >= 0; i--) {
            Stamp s = stamps[i];
            if (s.Go == null) {
                stamps.RemoveAt(i);
                continue;
            }
            float age = Time.time - s.Born;
            if (!s.Slammed) {
                float k = Mathf.Clamp01(age / Telegraph);
                s.Ink.material.color = new Color(0.8f, 0.2f, 0.2f, 0.18f + 0.4f * k * (0.6f + 0.4f * Mathf.Sin(age * 14f)));
                float drop = Mathf.Clamp01((age - (Telegraph - 0.45f)) / 0.45f);
                s.Head.position = s.At + new Vector3(0f, Mathf.Lerp(9f, 0.05f, drop * drop), 0f);
                if (age >= Telegraph) {
                    Slam(s);
                }
            } else {
                float since = Time.time - s.SlammedAt;
                if (s.Head != null && since > 0.35f) {
                    Destroy(s.Head.gameObject);
                    s.Head = null;
                }
                float fade = Mathf.Clamp01(1f - (since - 8f) / 8f);
                s.Ink.material.color = new Color(0.62f, 0.14f, 0.14f, 0.7f * fade);
                if (since > 16f) {
                    Destroy(s.Go);
                    stamps.RemoveAt(i);
                }
            }
        }
    }

    void Slam(Stamp s) {
        s.Slammed = true;
        s.SlammedAt = Time.time;
        StampsLanded++;
        Fx.Ring(s.At + Vector3.up * 0.1f, 5.5f, new Color(0.85f, 0.25f, 0.25f, 0.8f), 0.6f);
        UpgradeLook.BreakBurst(s.At);
        if (Vector3.Distance(s.At, scene.Player.position) < 40f) {
            GameAudio.Play("stamp", 0.55f, 0.8f);
        }
        Vector3 flat = scene.Player.position - s.At;
        flat.y = 0f;
        bool under = Mathf.Abs(flat.x) <= 1.9f + 0.4f && Mathf.Abs(flat.z) <= 1.35f + 0.4f;
        bool inRing = Vector3.Distance(LevelKit.Flat(scene.Player.position), RingCenter) <= RingRadius;
        if (under && inRing) {
            RingSafeShown = true;
            Note("ring-safe", "the stamp lands and Wallit stays: the Ring does not push");
            LevelBarks.Say('S', "You're inside. Inside, you exist.", "ring", false);
        } else if (under) {
            StampHits++;
            scene.Shove(flat.sqrMagnitude > 0.05f ? flat : -scene.Player.forward, 3.4f);
            scene.Hud.ShowReceipt("RETURN TO SENDER. -1 fee.", 2f);
            Note("hazard-hit", "stamp floor shoved Wallit back");
            Note("verb-stamp-floor", "shoved off a mark outside the Ring");
            LevelBarks.Say('W', "That's the fee!", "stamp", false);
        }
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e == null || !e.Alive || e.Dormant) {
                continue;
            }
            Vector3 d = e.transform.position - s.At;
            d.y = 0f;
            if (Mathf.Abs(d.x) <= 1.9f && Mathf.Abs(d.z) <= 1.35f) {
                e.Hit(1f);
                BotsStamped++;
                Note("hazard-hit", "stamp floor hit " + e.Title);
                Note("verb-stamp-floor", "a bot under the stamp");
            }
        }
    }

    // ---------------------------------------------------------------- per frame

    void Update() {
        if (scene == null || scene.Player == null) {
            return;
        }
        if (Stage == Beat.Done) {
            TickDone();
            return;
        }
        TickSpots();
        TickStones();
        TickBarks();
        TickStamps();
        TickCrew();
        TickSlice();
        if (carried != null) {
            carried.localRotation = Quaternion.Euler(0f, 0f, 12f + Mathf.Sin(Time.time * 3f) * 4f);
        } else if (flagItem != null && !FlagTaken) {
            flagItem.position = FlagPos + Vector3.up * (0.8f + Mathf.Sin(Time.time * 2.2f) * 0.12f);
            flagItem.Rotate(0f, 40f * Time.deltaTime, 0f);
            if (Time.frameCount % 150 == 0) {
                Fx.Ring(FlagPos + Vector3.up * 0.1f, 3f, new Color(1f, 0.85f, 0.45f, 0.5f), 1.1f);
            }
        }
        if (Time.frameCount % 200 == 0) {
            Fx.Ring(RingCenter + Vector3.up * 0.15f, RingRadius * 2.4f, new Color(1f, 0.82f, 0.4f, 0.35f), 1.8f);
        }
        scene.Hud.Objective.text = Objective();
        if (ExitOpen && !scene.Completed && Vector3.Distance(LevelKit.Flat(scene.Player.position), LevelKit.Flat(ExitPos)) < 2.8f) {
            Complete();
        }
    }

    void TickDone() {
        if (Time.time - completedAt < 1f) {
            return;
        }
        Interact.Offer("back to the hub");
        if (Interact.Pressed || scene.Hud.TakeTap()) {
            scene.LeaveToHub();
        }
    }

    // The nearest thing Wallit can use sets the prompt line; E (or a tap on the line) uses it.
    // How near Wallit has to be to use something: the hub's lodger reach, so a thumb on the stick does not have to park him
    // on top of Slice or the flag before the Interact button shows (2.7 m stranded phone players a step short).
    public const float UseReach = 3.4f;

    void TickSpots() {
        Spot best = null;
        float bestD = UseReach;
        Vector3 p = LevelKit.Flat(scene.Player.position);
        for (int i = 0; i < spots.Count; i++) {
            Spot s = spots[i];
            if (!s.On()) {
                continue;
            }
            Vector3 at = s == spots[0] ? LevelKit.Flat(SliceFigureAt) : LevelKit.Flat(s.At);
            float d = Vector3.Distance(p, at);
            if (d < bestD) {
                bestD = d;
                best = s;
            }
        }
        bool reviving = Companion.Instance != null && Companion.Instance.ReviveReady;
        scene.Hud.Prompt.text = best != null && !reviving ? Interact.Prompt(best.Prompt) : "";
        if (best != null && !reviving) {
            Interact.Offer(best.Prompt);
        }
        bool tapped = scene.Hud.TakeTap();
        if (best != null && !reviving && (PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.JoystickButton0) || tapped)) {
            best.Use();
        }
    }

    // A short bell on every stone he passes.
    void TickStones() {
        Vector3 p = scene.Player.position;
        for (int i = 0; i < stonePos.Length; i++) {
            if (!stonePassed[i] && Vector3.Distance(LevelKit.Flat(p), LevelKit.Flat(stonePos[i])) < 3.2f) {
                stonePassed[i] = true;
                StonesPassed++;
                GameAudio.Play("coin", 0.22f, 0.8f + i * 0.09f);
                Fx.Ring(stonePos[i] + Vector3.up * 0.1f, 3.2f, new Color(0.79f, 0.84f, 1f, 0.55f), 0.8f);
            }
        }
    }

    void TickBarks() {
        float toRing = Vector3.Distance(LevelKit.Flat(scene.Player.position), RingCenter);
        if (!nearRingSaid && toRing < 12f && sliceFigure != null) {
            nearRingSaid = LevelBarks.Say('S', "No sender. No refund. Stay in the circle.", "ring-near");
        }
        // The dead-letter bench at the Return Hall's west wing: a word for the walk past it.
        if (!benchSaid && !Talking && !CrewWoken && Vector3.Distance(LevelKit.Flat(scene.Player.position), new Vector3(-24f, 0f, -53f)) < 4f) {
            benchSaid = LevelBarks.Say('W', "Dead letters. Nobody's coming for these.", "bench");
            if (benchSaid) {
                Note("bench", "the dead-letter bench at the Return Hall");
            }
        }
        // Slice has had enough of him talking: three Wallit lines running, once.
        int n = LevelBarks.Log.Count;
        if (!wallitWordSaid && n >= 3 && !Talking && !CrewWoken && LevelBarks.Log[n - 1].StartsWith("W|") && LevelBarks.Log[n - 2].StartsWith("W|") && LevelBarks.Log[n - 3].StartsWith("W|")
            && Companion.Instance != null) {
            wallitWordSaid = LevelBarks.Say('S', "Wallit.", "enough");
        }
        // Still for a while in a safe room: the two of them fill the quiet.
        if (scene.SecondsSinceInput > 14f && Time.time - lastIdleAt > 18f && !Talking && !CrewWoken && Stage != Beat.Done) {
            bool slice = (Companion.Instance != null || sliceFigure != null) && Mathf.Abs(rng.Next(0, 100)) < 40;
            bool said = slice
                ? LevelBarks.Say('S', SliceIdle[rng.Next(0, SliceIdle.Length)], "idle")
                : LevelBarks.Say('W', WallitIdle[rng.Next(0, WallitIdle.Length)], "idle");
            if (said) {
                lastIdleAt = Time.time;
            }
        }
    }

    void TickCrew() {
        if (CrewWoken && !CrewCleared) {
            CombatSeconds += Time.deltaTime;
        }
        if (!CrewWoken && !CrewCleared && Vector3.Distance(LevelKit.Flat(scene.Player.position), LevelKit.Flat(CrewCenter)) < 16f) {
            WakeCrew("Wallit walked up to the drop");
        }
        if (CrewWoken && !lateWoken && lateAt > 0f && Time.time >= lateAt) {
            WakeLate();
        }
        if (CrewWoken && lateWoken && !CrewCleared && CrewAlive == 0) {
            ClearedCrew();
        }
        // Everyone down before the late two woke: wake them anyway so the fight is the whole crew.
        if (CrewWoken && !lateWoken && lateAt > 0f && CrewAlive == lateWave.Count) {
            lateAt = Time.time;
        }
    }

    // Slice on the Ring turns her whole body to him (and west when she says so) before she turns anything else.
    void TickSlice() {
        if (sliceFigure == null) {
            return;
        }
        Vector3 look = sliceLooksWest ? Vector3.left : LevelKit.Flat(scene.Player.position - sliceFigure.position);
        if (look.sqrMagnitude > 0.05f) {
            sliceFigure.rotation = Quaternion.Slerp(sliceFigure.rotation, Quaternion.LookRotation(look.normalized), 3f * Time.deltaTime);
        }
    }
}
