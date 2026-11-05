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
        "Unsigned? Then it isn't even valid.",
    };
    static readonly string[] WallitIdle = {
        "Forty. Forty-one. Somebody short-changed me.",
        "Tap. Tap. Hear that? Hollow. Like a bank at closing.",
        "Nine stones. Looks like padding on the bill.",
        "Is that a mailbox or a bad loan?",
    };
    static readonly string[] SliceIdle = { "Left.", "Quiet.", "Closer." };

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
    GameObject exitGlow;
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
    bool wallitWordSaid;
    float completedAt = -1f;
    Vector3 sliceLook;
    bool sliceLooksWest;
    System.Random rng = new System.Random(11);

    public void Note(string name, string detail) {
        Timeline.Add(name + "|" + detail);
    }

    // ---------------------------------------------------------------- the place

    public void Build(LevelScene owner) {
        scene = owner;
        root = new GameObject("Sorting Yard").transform;
        root.SetParent(transform, false);
        Ground();
        Walls();
        Doors();
        RingAndStones();
        TrailAndTrunks();
        Notices();
        Mailbox();
        DropOff();
        ReturnSlot();
        Lamps();
        SliceFigure();
        Crew5();
        BuildSpots();
        Physics.SyncTransforms();
    }

    void Ground() {
        GameObject g = LevelKit.Block(root, "Ground", PrimitiveType.Cube, new Vector3(0f, -0.5f, 0f), new Vector3(300f, 1f, 300f), new Color(0.065f, 0.11f, 0.15f), true);
        g.layer = 3;   // Ground: bots and Slice stand on it, the camera ray reads it
        LevelKit.Block(root, "Paving", PrimitiveType.Cube, new Vector3(0f, 0.02f, -10f), new Vector3(30f, 0.04f, 30f), new Color(0.11f, 0.15f, 0.26f));
        // The scar: burnt stamp ink on the stones, the dark patches the Department leaves where it has struck.
        for (int i = 0; i < 7; i++) {
            Vector3 p = new Vector3(Rand(-20f, 20f), 0.05f, Rand(-30f, 6f));
            if (Vector3.Distance(LevelKit.Flat(p), RingCenter) < RingRadius + 2f) {
                continue;
            }
            GameObject scar = LevelKit.Block(root, "Scar", PrimitiveType.Cylinder, p, new Vector3(Rand(1.6f, 3.4f), 0.01f, Rand(1.6f, 3.4f)), new Color(0.22f, 0.07f, 0.07f));
            scar.transform.rotation = Quaternion.Euler(0f, Rand(0f, 90f), 0f);
        }
    }

    void Walls() {
        Color wall = new Color(0.05f, 0.07f, 0.12f);
        LevelKit.Block(root, "WallW", PrimitiveType.Cube, new Vector3(-66f, 2f, 2f), new Vector3(2f, 4f, 126f), wall, true);
        LevelKit.Block(root, "WallE", PrimitiveType.Cube, new Vector3(66f, 2f, 2f), new Vector3(2f, 4f, 126f), wall, true);
        LevelKit.Block(root, "WallS", PrimitiveType.Cube, new Vector3(0f, 2f, -61f), new Vector3(134f, 4f, 2f), wall, true);
        LevelKit.Block(root, "WallN", PrimitiveType.Cube, new Vector3(0f, 2f, 65f), new Vector3(134f, 4f, 2f), wall, true);
    }

    // Five shut doors behind the start: every one of them stamped him RETURN TO SENDER, and none of them opens.
    void Doors() {
        for (int i = 0; i < 5; i++) {
            float x = (i - 2) * 7f;
            LevelKit.Block(root, "DoorFrame", PrimitiveType.Cube, new Vector3(x, 2.4f, -59.4f), new Vector3(3.6f, 4.8f, 0.6f), new Color(0.16f, 0.15f, 0.2f), true);
            LevelKit.Block(root, "Door", PrimitiveType.Cube, new Vector3(x, 2.1f, -59f), new Vector3(2.8f, 4.1f, 0.3f), new Color(0.26f, 0.22f, 0.28f));
            TextMesh t = LevelKit.Text(root, "RETURNED", new Vector3(x, 2.8f, -58.8f), 0.07f, new Color(0.76f, 0.23f, 0.23f), false);
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            LevelKit.Block(root, "Ink", PrimitiveType.Cube, new Vector3(x, 0.03f, -56.4f), new Vector3(3f, 0.02f, 1.6f), new Color(0.45f, 0.12f, 0.12f));
        }
    }

    void RingAndStones() {
        LevelKit.Block(root, "RingGold", PrimitiveType.Cylinder, RingCenter + new Vector3(0f, 0.05f, 0f), new Vector3(RingRadius * 2f + 0.5f, 0.01f, RingRadius * 2f + 0.5f), new Color(0.82f, 0.66f, 0.26f));
        LevelKit.Block(root, "RingFloor", PrimitiveType.Cylinder, RingCenter + new Vector3(0f, 0.07f, 0f), new Vector3(RingRadius * 2f - 0.5f, 0.01f, RingRadius * 2f - 0.5f), new Color(0.15f, 0.19f, 0.3f));
        // Nine standing stones; the gap faces the doors.
        int placed = 0;
        for (int i = 0; i < 9; i++) {
            float a = (270f + 20f + i * 40f) * Mathf.Deg2Rad;
            Vector3 p = RingCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 10.5f;
            stonePos[i] = p;
            GameObject stone = LevelKit.Block(root, "Stone" + i, PrimitiveType.Cylinder, p + Vector3.up * 1.6f, new Vector3(1.5f, 1.6f, 1.5f), new Color(0.24f, 0.28f, 0.4f), true);
            stone.transform.rotation = Quaternion.Euler(0f, i * 31f, 0f);
            LevelKit.Block(root, "Rune", PrimitiveType.Cube, p + new Vector3(0f, 3.25f, 0f), new Vector3(0.9f, 0.14f, 0.9f), new Color(0.79f, 0.84f, 1f));
            placed++;
        }
        // Chalk stepping stones: the trail from the doors to the Ring.
        for (float z = -54f; z < -17f; z += 3.2f) {
            LevelKit.Block(root, "Chalk", PrimitiveType.Cylinder, new Vector3(Rand(-0.5f, 0.5f), 0.045f, z), new Vector3(1f, 0.01f, 0.8f), new Color(0.85f, 0.88f, 0.95f));
        }
    }

    void TrailAndTrunks() {
        for (int i = 0; i < Trail.Length - 1; i++) {
            for (float t = 0.25f; t < 1f; t += 0.5f) {
                Vector3 p = Vector3.Lerp(Trail[i], Trail[i + 1], t);
                LevelKit.Block(root, "Chalk", PrimitiveType.Cylinder, p + new Vector3(0f, 0.045f, 0f), new Vector3(0.9f, 0.01f, 0.7f), new Color(0.85f, 0.88f, 0.95f));
            }
        }
        // The east way, to the drop.
        for (float x = 8f; x < 30f; x += 3.4f) {
            float z = Mathf.Lerp(-8f, 8f, (x - 8f) / 22f);
            LevelKit.Block(root, "Chalk", PrimitiveType.Cylinder, new Vector3(x, 0.045f, z), new Vector3(0.9f, 0.01f, 0.7f), new Color(0.85f, 0.88f, 0.95f));
        }
        // West of the Ring: dead trunks, thin and solid, with a clear lane along the chalk.
        int made = 0;
        for (int tries = 0; tries < 400 && made < 30; tries++) {
            Vector3 p = new Vector3(Rand(-62f, -17f), 0f, Rand(-42f, 36f));
            if (NearTrail(p, 4.4f) || Vector3.Distance(p, FlagPos) < 6f || Vector3.Distance(LevelKit.Flat(p), RingCenter) < 15f) {
                continue;
            }
            float h = Rand(2.6f, 4.6f);
            GameObject trunk = LevelKit.Block(root, "Trunk", PrimitiveType.Cylinder, p + Vector3.up * h * 0.5f, new Vector3(Rand(0.35f, 0.6f), h * 0.5f, Rand(0.35f, 0.6f)), new Color(0.13f, 0.11f, 0.16f), true);
            trunk.transform.rotation = Quaternion.Euler(Rand(-4f, 4f), Rand(0f, 180f), Rand(-4f, 4f));
            GameObject branch = LevelKit.Block(root, "Branch", PrimitiveType.Cube, p + new Vector3(0.5f, h * 0.8f, 0f), new Vector3(1.4f, 0.12f, 0.12f), new Color(0.13f, 0.11f, 0.16f));
            branch.transform.rotation = Quaternion.Euler(0f, Rand(0f, 180f), Rand(10f, 30f));
            made++;
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
            LevelKit.Block(root, "NoticePost", PrimitiveType.Cube, p + new Vector3(0f, 1.1f, 0f), new Vector3(0.22f, 2.2f, 0.22f), new Color(0.2f, 0.16f, 0.15f), true);
            GameObject board = LevelKit.Block(root, "NoticeBoard", PrimitiveType.Cube, p + new Vector3(0f, 1.9f, 0f), new Vector3(2.1f, 1.3f, 0.1f), new Color(0.85f, 0.81f, 0.68f));
            Vector3 away = LevelKit.Flat(p - RingCenter);
            board.transform.rotation = Quaternion.LookRotation(away.normalized);
            TextMesh t = LevelKit.Text(board.transform, NoticeBoard[i], new Vector3(0f, 0f, -0.6f), 0.07f, new Color(0.18f, 0.1f, 0.1f), false);
            t.transform.localScale = new Vector3(1f / 2.1f, 1f / 1.3f, 1f / 0.1f * 0.1f);
            t.characterSize = 0.036f;
        }
    }

    // The fallen mailbox, the stump it came off, and the flag that lay beside it: a lost thing, waving at nobody.
    void Mailbox() {
        Vector3 m = new Vector3(-41.5f, 0f, 14.5f);
        LevelKit.Block(root, "MailboxStump", PrimitiveType.Cylinder, m + new Vector3(1.4f, 0.8f, 0.5f), new Vector3(0.3f, 0.8f, 0.3f), new Color(0.22f, 0.17f, 0.15f), true);
        GameObject box = LevelKit.Block(root, "Mailbox", PrimitiveType.Cube, m + new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.9f), new Color(0.32f, 0.38f, 0.52f), true);
        box.transform.rotation = Quaternion.Euler(0f, 28f, 8f);
        LevelKit.Block(box.transform, "Slot", PrimitiveType.Cube, new Vector3(0.52f, 0.1f, 0f), new Vector3(0.1f, 0.08f, 0.9f), new Color(0.05f, 0.05f, 0.08f));
        var item = new GameObject("MailboxFlag");
        item.transform.SetParent(root, false);
        item.transform.position = FlagPos + Vector3.up * 0.8f;
        LevelKit.Block(item.transform, "Pole", PrimitiveType.Cube, Vector3.zero, new Vector3(0.09f, 1.3f, 0.09f), new Color(0.7f, 0.7f, 0.76f));
        LevelKit.Block(item.transform, "Cloth", PrimitiveType.Cube, new Vector3(0.3f, 0.45f, 0f), new Vector3(0.6f, 0.4f, 0.05f), new Color(0.76f, 0.23f, 0.23f));
        flagItem = item.transform;
    }

    void DropOff() {
        LevelKit.Block(root, "DropBox", PrimitiveType.Cube, DropPos + new Vector3(0f, 1.1f, 0f), new Vector3(3.2f, 2.2f, 2.4f), new Color(0.55f, 0.36f, 0.12f));
        LevelKit.Block(root, "DropSlot", PrimitiveType.Cube, DropPos + new Vector3(-1.62f, 1.4f, 0f), new Vector3(0.06f, 0.2f, 1.6f), new Color(0.04f, 0.04f, 0.06f));
        TextMesh t = LevelKit.Text(root, "DROP", DropPos + new Vector3(-1.7f, 0.7f, 0f), 0.09f, new Color(0.95f, 0.8f, 0.45f), false);
        t.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        for (float z = -3f; z < 3.1f; z += 3f) {
            LevelKit.Block(root, "Mat", PrimitiveType.Cube, DropPos + new Vector3(-4.5f, 0.04f, z), new Vector3(2f, 0.02f, 2.4f), new Color(0.4f, 0.3f, 0.1f));
        }
    }

    // The way out: a doorframe with a red-brown shutter that lifts once the crew is down.
    void ReturnSlot() {
        Vector3 p = ExitPos;
        LevelKit.Block(root, "SlotPostL", PrimitiveType.Cube, p + new Vector3(-2.3f, 2.2f, 0f), new Vector3(0.7f, 4.4f, 0.8f), new Color(0.2f, 0.18f, 0.26f), true);
        LevelKit.Block(root, "SlotPostR", PrimitiveType.Cube, p + new Vector3(2.3f, 2.2f, 0f), new Vector3(0.7f, 4.4f, 0.8f), new Color(0.2f, 0.18f, 0.26f), true);
        LevelKit.Block(root, "SlotLintel", PrimitiveType.Cube, p + new Vector3(0f, 4.6f, 0f), new Vector3(5.3f, 0.7f, 0.8f), new Color(0.2f, 0.18f, 0.26f), true);
        exitGlow = LevelKit.Block(root, "SlotGlow", PrimitiveType.Cube, p + new Vector3(0f, 2.1f, 0.5f), new Vector3(3.9f, 4.2f, 0.05f), new Color(0.35f, 0.3f, 0.14f));
        shutter = LevelKit.Block(root, "Shutter", PrimitiveType.Cube, p + new Vector3(0f, 2.1f, -0.1f), new Vector3(3.9f, 4.2f, 0.5f), new Color(0.33f, 0.14f, 0.14f), true);
        TextMesh t = LevelKit.Text(shutter.transform, "RETURN SLOT", new Vector3(0f, 0.2f, -0.6f), 0.5f, new Color(0.9f, 0.7f, 0.5f), false);
        t.transform.localScale = new Vector3(1f / 3.9f, 1f / 4.2f, 1f);
        t.characterSize = 0.085f;
    }

    void Lamps() {
        Vector3[] at = { new Vector3(-7f, 0f, -16f), new Vector3(7f, 0f, -16f), new Vector3(-8f, 0f, -3f), DropPos + new Vector3(-6f, 0f, 3f) };
        for (int i = 0; i < at.Length; i++) {
            LevelKit.Block(root, "LampPost", PrimitiveType.Cylinder, at[i] + Vector3.up * 1.7f, new Vector3(0.14f, 1.7f, 0.14f), new Color(0.12f, 0.12f, 0.16f));
            LevelKit.Block(root, "LampGlass", PrimitiveType.Cube, at[i] + Vector3.up * 3.55f, new Vector3(0.5f, 0.55f, 0.5f), new Color(1f, 0.85f, 0.5f));
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = at[i] + Vector3.up * 3.4f;
            light.type = LightType.Point;
            light.range = 15f;
            light.intensity = 1.25f;
            light.color = i == 3 ? new Color(1f, 0.7f, 0.35f) : new Color(1f, 0.86f, 0.55f);
            light.shadows = LightShadows.None;
        }
    }

    // Slice, on guard at the Ring: the painted model on its idle (stand-in capsule without it).
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
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = LevelKit.Flat(new Color(0.14f, 0.12f, 0.2f));
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        }
        go.name = "Slice (on guard)";
        go.transform.SetParent(root, false);
        go.transform.position = SlicePos + (prefab != null ? Vector3.zero : Vector3.up);
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

    IEnumerator Opening() {
        yield return new WaitForSeconds(2.2f);
        LevelBarks.Say('W', "Return to sender. Which sender? I want a refund.", "arrive", true);
    }

    string Objective() {
        switch (Stage) {
            case Beat.Arrive:
                return "Find out why you were stamped. Slice is on guard at the Ring, straight ahead.";
            case Beat.Talked:
                return "Read the notices on the west trail (" + NoticesRead + "/3). Find what was lost.";
            case Beat.Flag:
                return "The Clearing Crew has your scent. Return to sender: east, at the drop.";
            case Beat.Fight:
                return "Clear the Crew (" + CrewAlive + " left). Stay out from under the stamps.";
            case Beat.Clear:
                return "The return slot is open: north-east. Chapter 1 goes on from there.";
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
            "Nobody signed it.",
            "Then who pays?",
            "Look west. The trail. Lost things, lying where they landed.",
            "Lost means finders keepers.",
            "Wallit.",
            "Finders keepers. Quietly.",
            "Closer. Stay close.",
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
        scene.Hud.ShowReceipt(NoticeRead[i], 6f);
        GameAudio.Play("coin", 0.35f, 1.4f);
        Note("notice", (i + 1) + " of 3: " + NoticeRead[i]);
        LevelBarks.Say('W', NoticeReact[i], "notice", true);
        if (i == 2 && Companion.Instance != null) {
            StartCoroutine(SliceAnswers());
        }
    }

    IEnumerator SliceAnswers() {
        yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.3f);
        LevelBarks.Say('S', "Valid enough.", "notice", true);
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
            LevelBarks.Say('S', "Keep it.", "pickup", true);
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
        LevelBarks.Say('Y', "Late delivery! Late delivery!", "late", true);
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
        if (exitGlow != null) {
            exitGlow.GetComponent<Renderer>().material.color = new Color(1f, 0.82f, 0.4f);
        }
        GameAudio.Play("stamp", 0.5f, 1.3f);
        Note("encounter-clear", "the Clearing Crew is down");
        StartCoroutine(AfterClear());
    }

    IEnumerator AfterClear() {
        yield return new WaitForSeconds(1.2f);
        LevelBarks.Say('S', "Clean.", "clear", true);
        yield return new WaitForSeconds(LevelBarks.LastSeconds + 0.2f);
        LevelBarks.Say('W', "Return to sender. Paid in full.", "clear", true);
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
        scene.Hud.Prompt.text = "CHAPTER 1: RETURN TO SENDER\nThe Sorting Yard is filed. The Courier waits past the slot.\n" + Interact.Key + ": back to the hub";
    }

    // ---------------------------------------------------------------- stamps

    public void DropStampAt(Vector3 at, string cause) {
        at.y = 0.03f;
        var go = new GameObject("StampMark");
        go.transform.SetParent(root, false);
        go.transform.position = at;
        GameObject ink = LevelKit.Block(go.transform, "Ink", PrimitiveType.Cube, Vector3.zero, new Vector3(3.8f, 0.03f, 2.7f), new Color(0.76f, 0.23f, 0.23f, 0.25f));
        Renderer r = ink.GetComponent<Renderer>();
        r.material = new Material(r.sharedMaterial);
        TextMesh t = LevelKit.Text(go.transform, "RETURN\nTO SENDER", new Vector3(0f, 0.06f, 0f), 0.07f, new Color(1f, 0.85f, 0.85f, 0.9f), true);
        t.name = "Words";
        GameObject head = LevelKit.Block(go.transform, "StampHead", PrimitiveType.Cube, new Vector3(0f, 8f, 0f), new Vector3(3.4f, 1.1f, 2.4f), new Color(0.23f, 0.1f, 0.1f));
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
                s.Head.localPosition = new Vector3(0f, Mathf.Lerp(8f, 0.6f, drop * drop), 0f);
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
            LevelBarks.Say('S', "Inside, you exist.", "ring", false);
        } else if (under) {
            StampHits++;
            scene.Shove(flat.sqrMagnitude > 0.05f ? flat : -scene.Player.forward, 3.4f);
            scene.Hud.ShowReceipt("RETURN TO SENDER. -1 fee.", 2f);
            Note("hazard-hit", "stamp floor shoved Wallit back");
            Note("verb-stamp-floor", "shoved off a mark outside the Ring");
            LevelBarks.Say('W', "That's a fee.", "stamp", false);
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
    void TickSpots() {
        Spot best = null;
        float bestD = 2.7f;
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
