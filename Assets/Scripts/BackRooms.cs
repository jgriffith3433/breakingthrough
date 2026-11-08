using UnityEngine;

// The back rooms: one secret room on each Campaign chapter's map (Docs/Story.md, "The back rooms"), and one optional hard
// encounter, The Dead Letter, in Chapter 4's room, which pays Refund in Full once. Data is `BackRooms.All` (one row per chapter,
// never reordered: a bit per row is saved in CampaignSave.Slot.rooms). A room does not exist on the map until the chapter's lost
// thing (Journal.Finds) is in the case file: that thing carries the clue (a bearing and a distance), and the room is then built
// out past the playfield (no collider, nothing solid within 150 m). Walking into it keeps it, pays a little XP and says what it is.
// Voice: the Department's filing language, flat and polite; Slice clipped, Wallit money-minded.
public static class BackRooms {
    public class Room {
        public string Name;
        public float Bearing;   // degrees from the circle
        public string Clue;     // what the lost thing says, once kept
        public string Text;     // what the room is, shown once it is in the journal
        public string Enter;    // said on walking in
        public int Xp;          // paid the first time
    }

    // A room's Enter exchange ("Slice: \"...\" Wallit: \"...\"") as speaker lines ("S|...", "W|...") for ChapterRun to say
    // one at a time, each in its own voice.
    public static string[] Spoken(string enter) {
        var lines = new System.Collections.Generic.List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(enter ?? "", "(Slice|Wallit): \"([^\"]*)\"")) {
            lines.Add(m.Groups[1].Value.Substring(0, 1) + "|" + m.Groups[2].Value);
        }
        return lines.ToArray();
    }

    public const float Dist = 172f;       // beyond the 150 m playfield, inside the 205 m wall
    public const float EnterRadius = 5f;
    public const int EncounterChapter = 3;
    public const int DeadLetterBit = 1 << 6;   // CampaignSave.Slot.rooms: the Dead Letter fell and Refund in Full is earned
    public const string EncounterName = "The Dead Letter";
    public const string RewardName = "Refund in Full";
    public const string RewardText = "Refund returns more than it takes: a wider ring (12 m), back in 6 s, and Slice is paid back 8 with it.";

    // Index = chapter. Bearings stay off the ones the chapters use for their own stops (150, 200 to 210, 330) and the finds'.
    public static readonly Room[] All = {
        new Room { Name = "The Sorting Nook", Bearing = 240f, Xp = 20,
            Clue = "On the back of the postcard, in pencil: Door 1. 172 m south-west of the circle. Knock twice.",
            Text = "A cubby where undelivered mail waited its turn. One pigeonhole still holds a wet stamp pad.",
            Enter = "Slice: \"Nobody filed a room.\" Wallit: \"Then nobody can bill me for it.\"" },
        new Room { Name = "The Forgotten Door", Bearing = 20f, Xp = 25,
            Clue = "A tag on the key: FITS 172 m north-north-east. The door has no number.",
            Text = "A door with a handle and no wall. Behind it, one room, one chair, the key's own shadow on the floor.",
            Enter = "Wallit: \"The key fits.\" Slice: \"Of course it does. Somebody kept it.\"" },
        new Room { Name = "The Spare Parts Bin", Bearing = 300f, Xp = 30,
            Clue = "Inside the hat's label, under BRIM: Parts bin. 172 m north-west. Off the belt.",
            Text = "A bin where the Floor drops what it cannot use: half a claw, a lid, a lamp that never lit.",
            Enter = "Wallit: \"Unclaimed parts. Finders keepers.\" Slice: \"Finders pay.\"" },
        new Room { Name = "The Dead Letter Box", Bearing = 250f, Xp = 35,
            Clue = "At the bottom of the notice, a fourth stamp, never struck: RETURN TO THE BOX. 172 m west-south-west.",
            Text = "A box for papers with no address to hand them to. The ones that cannot be refused are kept here.",
            Enter = "Slice: \"Dead letters. Papers nobody can refuse.\" Wallit: \"Then somebody has to take one.\"" },
        new Room { Name = "The Closed Register", Bearing = 160f, Xp = 40,
            Clue = "The ledger leaf, held to the light, shows a margin note: CLOSED ENTRIES, 172 m south-south-east.",
            Text = "Where the Registrar kept what it never read aloud. Every page here is blank, and the stamp does not take.",
            Enter = "Wallit: \"Blank pages. Nothing owed.\" Slice: \"Nothing struck, either. Keep it that way.\"" },
        new Room { Name = "The Last Address", Bearing = 100f, Xp = 50,
            Clue = "Under the stamp's face, one word of ink: HERE. 172 m east, where the path stops.",
            Text = "A small room at the end of the path. A door number is chalked on it, the circle's own, and it is not struck.",
            Enter = "Slice: \"My number.\" Wallit: \"Yours. Mine's the one next door.\"" },
    };

    public static bool Entered(CampaignSave.Slot s, int chapter) {
        return s != null && s.used && chapter >= 0 && chapter < All.Length && (s.rooms & (1 << chapter)) != 0;
    }

    public static bool HasReward(CampaignSave.Slot s) {
        return s != null && s.used && (s.rooms & DeadLetterBit) != 0;
    }

    public static int Count(CampaignSave.Slot s) {
        int n = 0;
        for (int i = 0; i < All.Length; i++) {
            n += Entered(s, i) ? 1 : 0;
        }
        return n;
    }

    // The clue is known once the chapter's lost thing is in the case file.
    public static bool ClueHeld(CampaignSave.Slot s, int chapter) {
        return Journal.HasFind(s, chapter);
    }

    public static Vector3 Point(int chapter, Vector3 circle) {
        float a = All[Mathf.Clamp(chapter, 0, All.Length - 1)].Bearing * Mathf.Deg2Rad;
        Vector3 at = circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Dist;
        at.y = circle.y;
        if (Physics.Raycast(new Vector3(at.x, 80f, at.z), Vector3.down, out RaycastHit hit, 160f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            at.y = hit.point.y;
        }
        return at;
    }

    // Marks the room entered in the open file and saves it at once. False when it was already kept.
    public static bool Keep(int chapter) {
        if (CampaignSave.Active < 0 || chapter < 0 || chapter >= All.Length) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        int bit = 1 << chapter;
        if ((s.rooms & bit) != 0) {
            return false;
        }
        s.rooms |= bit;
        CampaignSave.Flush();
        return true;
    }

    // The Dead Letter's reward, once. False when the file already holds it.
    public static bool KeepReward() {
        if (CampaignSave.Active < 0) {
            return false;
        }
        CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
        if ((s.rooms & DeadLetterBit) != 0) {
            return false;
        }
        s.rooms |= DeadLetterBit;
        CampaignSave.Flush();
        return true;
    }
}

// Rides on the ChapterRun object: builds the chapter's room once its clue is held, watches for Wallit walking in, and runs the
// Dead Letter in Chapter 4's. Campaign only (Arcade has no ChapterRun, so no room).
public class BackRoom : MonoBehaviour {
    public static BackRoom Instance { get; private set; }

    public bool Built { get; private set; }
    public bool Entered { get; private set; }          // walked in during this play
    public bool EncounterUp { get { return letter != null && letter.Alive; } }
    public bool EncounterSpawned { get; private set; }
    public bool RewardGiven { get; private set; }      // paid during this play
    public int Chapter { get; private set; }
    public Vector3 At { get; private set; }
    public string Clue { get { return BackRooms.All[Chapter].Clue; } }
    public Enemy Letter { get { return letter; } }
    public int Pulses { get; private set; }

    GameObject room;
    Enemy letter;
    float ring;
    bool clueSaid;

    void Awake() {
        Instance = this;
    }

    void Start() {
        ChapterRun run = ChapterRun.Instance;
        Chapter = run != null ? run.Chapter : 0;
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        At = BackRooms.Point(Chapter, origin);
        CampaignSave.Slot slot = CampaignSave.Active >= 0 ? CampaignSave.Get(CampaignSave.Active) : null;
        if (BackRooms.ClueHeld(slot, Chapter)) {
            clueSaid = true;   // the clue was already in the file: the room is simply there
            Build();
        }
        ChapterRun.Events.Add("secret|room " + BackRooms.All[Chapter].Name + (Built ? " built (clue held) at " : " not built (no clue yet), would stand at ") + At.ToString("0") + ", " + BackRooms.Dist + " m out");
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (room != null) {
            Destroy(room);
        }
    }

    void Update() {
        ChapterRun run = ChapterRun.Instance;
        if (run == null || run.Completed || run.Failed || WaveDirector.IsLost || PauseMenu.IsOpen || StoryScreen.IsOpen || LevelUpScreen.IsOpen || Cutscene.IsOpen || PlayerGrowth.Instance == null) {
            return;
        }
        if (!Built && run.FindKept) {
            Build();
        }
        if (Built && !clueSaid) {
            clueSaid = true;
            UpgradeLook.Say("The clue: " + Clue, 6f, 120f);
            ChapterRun.Events.Add("secret|clue " + Clue);
        }
        if (!Built) {
            return;
        }
        Vector3 d = At - PlayerGrowth.Instance.transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist <= 40f && Time.time >= ring) {
            ring = Time.time + 2f;
            Pulses++;
            Fx.Ring(At + Vector3.up * 0.2f, 7f, new Color(1f, 0.84f, 0.4f, 0.5f), 1.4f);
        }
        if (dist <= BackRooms.EnterRadius) {
            Enter();
            if (Chapter == BackRooms.EncounterChapter && !EncounterSpawned) {
                CampaignSave.Slot s = CampaignSave.Get(CampaignSave.Active);
                if (!BackRooms.HasReward(s)) {
                    SpawnLetter();
                }
            }
        }
        if (EncounterSpawned && !RewardGiven && (letter == null || !letter.Alive)) {
            Reward();
        }
    }

    void Enter() {
        if (Entered) {
            return;
        }
        Entered = true;
        BackRooms.Room r = BackRooms.All[Chapter];
        bool first = BackRooms.Keep(Chapter);
        if (first) {
            CampaignProgress.Grant(r.Xp);
            GameAudio.Play("coin", 0.7f, 1.2f);
        }
        Fx.Ring(At + Vector3.up * 0.2f, 12f, new Color(1f, 0.88f, 0.5f, 0.8f), 0.8f);
        // The room's name on screen; the exchange inside it is spoken, one voiced line at a time, not printed as a caption.
        UpgradeLook.Say(first ? "Found: " + r.Name + (r.Xp > 0 ? "  +" + r.Xp + " XP" : "") : r.Name, 2.6f, 120f);
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.TellLines(BackRooms.Spoken(r.Enter));
        }
        ChapterRun.Events.Add("secret|entered " + r.Name + (first ? " (first time, +" + r.Xp + " XP)" : " (already kept)"));
    }

    void SpawnLetter() {
        WaveDirector director = WaveDirector.Instance;
        if (director == null) {
            return;
        }
        EncounterSpawned = true;
        letter = director.SpawnMiniAt(BotKind.SpiderBot, BackRooms.EncounterName, 64, new Color(0.75f, 0.6f, 1.1f), At + new Vector3(0f, 0f, 0f));
        ChapterRun.Events.Add("secret|encounter " + BackRooms.EncounterName + " (" + letter.HitsToDrop + " hits)");
        UpgradeLook.Say("A paper with no address. Bring it down.", 3f, 80f);
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.TellLines(new[] { "W|Pay up." });
        }
    }

    void Reward() {
        RewardGiven = true;
        bool first = BackRooms.KeepReward();
        ChapterRun.Events.Add("secret|reward " + BackRooms.RewardName + (first ? " (first time)" : " (already held)"));
        if (first) {
            GameAudio.Play("coin", 0.9f, 1.4f);
            if (PlayerGrowth.Instance != null) {
                Fx.Ring(PlayerGrowth.Instance.transform.position + Vector3.up * 0.2f, 9f, new Color(1f, 0.82f, 0.38f, 0.9f), 0.8f);
            }
            UpgradeLook.Say("REFUND IN FULL!", 2.6f, 160f);
            if (ChapterRun.Instance != null) {
                ChapterRun.Instance.TellLines(new[] { "W|Received in full. Pay up.", "S|Clean." });
            }
        }
    }

    // A small room, flat and collider-free: a paper floor, three low walls and a doorway facing the circle, a desk and
    // pigeonholes, one amber lamp that is the only bright thing in it.
    void Build() {
        if (Built) {
            return;
        }
        Built = true;
        room = new GameObject("Back Room " + BackRooms.All[Chapter].Name);
        room.transform.position = At;
        Vector3 toCircle = Musician.Instance != null ? Musician.Instance.transform.position - At : -At;
        toCircle.y = 0f;
        room.transform.rotation = Quaternion.LookRotation(toCircle.sqrMagnitude > 0.01f ? toCircle.normalized : Vector3.forward, Vector3.up);
        // At 172 m the bowl slopes: lay the room on it, or one side of its 10 m floor is a metre under and the other in the air.
        room.transform.rotation = Quaternion.FromToRotation(Vector3.up, LevelDress.GroundNormal(At.x, At.z)) * room.transform.rotation;
        Color lamp = new Color(1f, 0.78f, 0.35f);
        // Built from the Model Kit (Docs/Levels1to3.md): mail-slot walls round three sides, the wainscot door on the fourth with
        // its gap, a desk with a lamp and pigeonholes; the paper floor is a textured decal. No colliders.
        GameObject floor = LevelKit.Decal(room.transform, "Paper Floor", new Vector3(0f, 0.06f, 0f), new Vector2(10f, 10f), Color.white);
        floor.GetComponent<MeshRenderer>().sharedMaterial = LevelDress.GroundMaterial("office", "wood", new Color(0.9f, 0.8f, 0.62f));
        for (int i = -1; i <= 1; i++) {
            KitAt("of_mail_slot_wall", new Vector3(i * 3f, 0f, -5f), 0f);
            KitAt("of_mail_slot_wall", new Vector3(-5f, 0f, i * 3f), 90f);
            KitAt("of_mail_slot_wall", new Vector3(5f, 0f, i * 3f), -90f);
        }
        KitAt("of_wainscot_door", new Vector3(-3f, 0f, 5f), 180f);
        KitAt("of_wainscot_door", new Vector3(3f, 0f, 5f), 180f);
        KitAt("of_desk", new Vector3(0f, 0f, -3.4f), 0f);
        KitAt("of_desk_lamp", new Vector3(0.6f, 0.78f, -3.5f), 0f);
        KitAt("of_pigeonholes", new Vector3(-4.3f, 0f, -1f), 90f);
        KitAt("of_filing_cabinet", new Vector3(4.2f, 0f, -2f), -90f);
        KitAt("of_rug", new Vector3(0f, 0.07f, 0f), 0f);
        Light glow = LevelDress.PointLight(room.transform, room.transform.position + room.transform.rotation * new Vector3(0f, 1.8f, -3.2f), lamp, 12f, 1.3f);
        glow.name = "Back Room Lamp";
        ChapterRun.Events.Add("secret|built " + BackRooms.All[Chapter].Name);
    }

    void KitAt(string id, Vector3 local, float yaw) {
        GameObject go = KitCatalog.Make(id, room.transform);
        if (go == null) {
            return;
        }
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
