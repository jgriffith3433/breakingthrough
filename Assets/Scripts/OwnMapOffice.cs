using System.Collections.Generic;
using UnityEngine;

// The hub: the office (Docs/Levels4to6AndHub.md, section 2). The Place Department's office, the room ChapterHub makes walkable
// (Wallit, Slice's ring, the chapter board, six chapter doors), built entirely from the Model Kit in its own scene (Map_Office):
// a board floor, walls in bays with wainscot and pilasters, window bays with a skyline outside, six themed door bays on the back
// wall, ceiling beams with hanging lamps, a runner rug, shelves, cabinets, benches, plants and Tick's desk. ChapterHub keeps the
// people, the pickups, the door leaves, the board and the UI. Hub coordinates are the hub's own (ChapterHub.DoorPosition...), and
// nothing here has a collider (ChapterHub clamps the walk).
//
// The office is dressed for how far the case file has come (`cleared` chapters filed) and re-dressed when it changes
// (ChapterHub.Refresh): the window light goes from night to dawn, a seal on a door's side once its chapter is filed, the filing
// cabinets along the left wall grow, the ring's brass warms from lamp-blue to gold, and once all six are filed the wall carries the
// brass door number.
public class OfficeMap : OwnMap {
    public override string Key { get { return "office"; } }
    Transform stage;
    public int Stage { get; private set; } = -1;
    public string StageName { get; private set; } = "";
    Light window;
    GameObject ring;
    readonly List<GameObject> windows = new List<GameObject>();

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

    // The six chapters' door surrounds (the main surface's colour; the frame's own texture shows through).
    static readonly Color[] DoorTints = {
        new Color(0.95f, 0.78f, 0.7f), new Color(0.82f, 0.88f, 1f), new Color(1.08f, 0.82f, 0.6f),
        new Color(0.62f, 0.62f, 0.86f), new Color(1.05f, 1f, 0.9f), new Color(1.08f, 1.08f, 1.14f),
    };

    protected override void Build() {
        MakeMusic(12f, new[] { 82.4f, 123.5f, 164.8f }, 2f, 0f, 0f, 6f, 440f, 0.14f);
        BuildRoom();
        Features.Add("route:the runner rug from Slice's ring to the row of six chapter doors on the back wall, under hanging lamps");
        Features.Add("landmark:six themed chapter doors (post-office slot, civic arch, steel hatch, iron lamp frame, paper door, pale round arch), each with its own leaf");
        Features.Add("landmark:window bays in both side walls with the Department's skyline and the clock tower outside, lit by the hour");
        Features.Add("setpiece:The Office Changes: as the case file advances the window light goes from night to dawn, the filing cabinets grow, the ring warms and the brass door number appears when all six are filed");
        CampaignSave.Slot slot = CampaignSave.Get(CampaignSave.Active);
        Dress(slot.cleared, HubLife.SideCount(slot), HubLife.HeardCount(slot));
    }

    // ---- the room itself: built once -------------------------------------------------------------------------------------

    void BuildRoom() {
        Transform room = Group("Office Room");
        float hw = ChapterHub.RoomHalfWidth + 0.8f;

        // The board floor.
        Mesh floorMesh = LevelDress.GridMesh("Office Floor", -hw, -10.5f, hw, 15.1f, 1.5f, (x, z) => 0f, 3f);
        made.Add(floorMesh);
        Material floorMat = LevelDress.GroundMaterial("office", "wood", C(0.86f, 0.72f, 0.58f));
        made.Add(floorMat);
        GameObject floor = LevelDress.Surface(room, "Office Floor", floorMesh, floorMat, false, 0);
        floor.transform.position = new Vector3(0f, -0.02f, 0f);

        // Side walls in 3 m bays: windows on the even bays to the left, the odd bays to the right; pilasters at the joints.
        for (int i = 0; i < 8; i++) {
            float z = -7.5f + i * 3f;
            bool leftWindow = i % 2 == 0;
            bool rightWindow = i % 2 == 1;
            GameObject lw = LevelDress.Put(room, leftWindow ? "of_wall_window" : "of_wall_bay", new Vector3(-hw - 0.125f, 0f, z), 90f);
            GameObject rw = LevelDress.Put(room, rightWindow ? "of_wall_window" : "of_wall_bay", new Vector3(hw + 0.125f, 0f, z), -90f);
            if (leftWindow) {
                windows.Add(lw);
            }
            if (rightWindow) {
                windows.Add(rw);
            }
        }
        for (int k = 0; k <= 8; k += 2) {
            float z = -9f + k * 3f;
            LevelDress.Put(room, "of_pilaster", new Vector3(-hw + 0.05f, 0f, z), 90f);
            LevelDress.Put(room, "of_pilaster", new Vector3(hw - 0.05f, 0f, z), -90f);
        }

        // The back wall: six door bays of 4.6 m, a corner piece at each end.
        for (int i = 0; i < CampaignSave.ChapterCount; i++) {
            Vector3 d = ChapterHub.DoorPosition(i);
            GameObject frame = LevelDress.Put(room, "of_door_frame_" + (i + 1), new Vector3(d.x, 0f, d.z + 0.15f), 180f);
            frame.name = "Door " + (i + 1) + " Frame";
            if (i > 0) {
                LevelDress.Tint(frame, 0, DoorTints[i]);
            }
        }
        LevelDress.Put(room, "of_wall_corner", new Vector3(-hw + 0.7f, 0f, 15.15f), 180f);
        LevelDress.Put(room, "of_wall_corner", new Vector3(hw - 0.7f, 0f, 15.15f), 180f);

        // What is outside the windows: the Department's roofs, and the Dead Letter Office's clock tower far off.
        LevelDress.Put(room, "of_skyline_view", new Vector3(-hw - 18f, 0f, 3f), 90f);
        LevelDress.Put(room, "of_skyline_view", new Vector3(hw + 18f, 0f, 3f), -90f);
        LevelDress.Put(room, "yd_clock_tower", new Vector3(-hw - 60f, 0f, 36f), 90f);

        // Beams across the room, and the lamps that hang from them.
        float[] beams = { -6f, 0f, 6f, 12f };
        foreach (float z in beams) {
            LevelDress.Put(room, "of_ceiling_beam", new Vector3(0f, 5f, z), 0f);
        }
        Vector3[] lamps = { new Vector3(0f, 2.55f, 0f), new Vector3(-7f, 2.55f, 6f), new Vector3(7f, 2.55f, 6f), new Vector3(0f, 2.55f, 12f) };
        foreach (Vector3 at in lamps) {
            LevelDress.Put(room, "of_hanging_lamp", at, 0f);
            LevelDress.PointLight(room, at + Vector3.down * 0.1f, C(1f, 0.82f, 0.54f), 15f, 1.1f);
        }
        window = LevelDress.PointLight(room, new Vector3(0f, 4.4f, 12.5f), C(1f, 0.86f, 0.55f), 16f, 0.5f);

        // The runner from the ring to the doors, the ring itself, a rug under the desk.
        for (int i = 0; i < 3; i++) {
            LevelDress.Put(room, "rd_runner", new Vector3(0f, 0.01f, 5.6f + i * 3f), 0f);
        }
        ring = LevelDress.Put(room, "of_ring_inlay", new Vector3(0f, 0.03f, 0f), 0f);
        LevelDress.Put(room, "of_rug", new Vector3(10.2f, 0.01f, 3f), 90f);
        LevelDress.Put(room, "of_rug", new Vector3(-9.8f, 0.01f, -5f), 14f);

        // Tick's desk on the right: two desks butted, a lamp, papers, a chair pulled out.
        Vector3 desk = ChapterHub.DeskPosition;
        LevelDress.Put(room, "of_desk", new Vector3(desk.x + 0.4f, 0f, desk.z - 0.85f), -90f);
        LevelDress.Put(room, "of_desk", new Vector3(desk.x + 0.4f, 0f, desk.z + 0.85f), -90f);
        LevelDress.Put(room, "of_desk_lamp", new Vector3(desk.x + 0.4f, 0.77f, desk.z - 1.0f), 90f);
        LevelDress.Put(room, "of_paper_stack", new Vector3(desk.x + 0.4f, 0.77f, desk.z + 1.1f), 30f, 0.9f);
        LevelDress.Put(room, "of_chair", new Vector3(desk.x + 2.3f, 0f, desk.z + 2.6f), 200f);
        LevelDress.PointLight(room, desk + new Vector3(0.4f, 1.6f, -1.0f), C(1f, 0.8f, 0.5f), 7f, 0.9f);

        // The left wall: shelves in the first and fifth bays, the notice wall in the fourth (slips are pinned there), a coat stand.
        LevelDress.Put(room, "rd_bookshelf", new Vector3(-hw + 0.3f, 0f, -5.3f), 90f);
        LevelDress.Put(room, "rd_bookshelf", new Vector3(-hw + 0.3f, 0f, -3.9f), 90f);
        LevelDress.Put(room, "rd_bookshelf", new Vector3(-hw + 0.3f, 0f, 6.8f), 90f);
        LevelDress.Put(room, "rd_bookshelf", new Vector3(-hw + 0.3f, 0f, 8.2f), 90f);
        LevelDress.Put(room, "of_coat_stand", new Vector3(-13.2f, 0f, 9.6f), 0f);

        // The right wall: a bench and a plant, shelves, pigeonholes with the pneumatic tube, the stamp desk.
        LevelDress.Put(room, "of_waiting_bench", new Vector3(hw - 0.5f, 0f, -7.5f), -90f);
        LevelDress.Put(room, "of_plant", new Vector3(hw - 0.7f, 0f, -9.2f), 0f);
        LevelDress.Put(room, "rd_bookshelf", new Vector3(hw - 0.3f, 0f, -2.2f), -90f);
        LevelDress.Put(room, "rd_bookshelf", new Vector3(hw - 0.3f, 0f, -0.8f), -90f);
        for (int i = 0; i < 3; i++) {
            LevelDress.Put(room, "of_pigeonholes", new Vector3(hw - 0.35f, 0f, 3.7f + i * 1.2f), -90f);
            LevelDress.Put(room, "of_pigeonholes", new Vector3(hw - 0.35f, 1.26f, 3.7f + i * 1.2f), -90f);
        }
        LevelDress.Put(room, "of_pneumatic_tube", new Vector3(hw - 0.4f, 0f, 9.4f), -90f);
        LevelDress.Put(room, "of_desk", new Vector3(hw - 1.4f, 0f, 11.2f), -90f);
        LevelDress.Put(room, "of_stamp_press", new Vector3(hw - 1.4f, 0.77f, 11.4f), -90f);
        LevelDress.Put(room, "of_chair", new Vector3(hw - 3.1f, 0f, 11.4f), 90f);

        // The front edge: benches and plants where the room opens to the camera, and a plant in each back corner.
        LevelDress.Put(room, "of_waiting_bench", new Vector3(-5.5f, 0f, -8.6f), 0f);
        LevelDress.Put(room, "of_waiting_bench", new Vector3(5.5f, 0f, -8.6f), 8f);
        LevelDress.Put(room, "of_plant", new Vector3(-12.4f, 0f, -8.3f), 40f);
        LevelDress.Put(room, "of_plant", new Vector3(12.4f, 0f, -8.3f), 120f);
        LevelDress.Put(room, "of_plant", new Vector3(-12.8f, 0f, 13.7f), 200f);
        LevelDress.Put(room, "of_plant", new Vector3(12.8f, 0f, 13.7f), 300f);
        LevelDress.Put(room, "of_coat_stand", new Vector3(12.9f, 0f, 7.4f), 20f);
    }

    int dressKey = -1;
    public int Pinned { get; private set; }
    public int Lamps { get; private set; }
    // A fingerprint of what the office looks like now, for the playtest: it changes whenever anything on the walls does.
    public string Look { get; private set; } = "";

    // The office after `cleared` chapters, with `pinned` side errands finished (slips on the notice wall) and `heard` hub
    // conversations had (a lit sconce each along the right wall).
    public void Dress(int cleared, int pinned = 0, int heard = 0) {
        cleared = Mathf.Clamp(cleared, 0, CampaignSave.ChapterCount);
        int key = (cleared * 64 + pinned) * 64 + heard;
        if (key == dressKey) {
            return;
        }
        dressKey = key;
        Pinned = pinned;
        Lamps = heard;
        if (stage != null) {
            Destroy(stage.gameObject);
        }
        Stage = cleared;
        stage = Group("Office Stage " + cleared);
        float dawn = cleared / (float)CampaignSave.ChapterCount;
        Color glow = Color.Lerp(C(0.5f, 0.62f, 0.95f), C(1f, 0.86f, 0.55f), dawn);
        Color brass = C(0.9f, 0.72f, 0.36f);
        StageName = cleared >= 6 ? "filed" : cleared >= 4 ? "late" : cleared >= 2 ? "middle" : "early";
        SetSky(Color.Lerp(C(0.02f, 0.03f, 0.08f), C(0.12f, 0.2f, 0.45f), dawn), Color.Lerp(C(0.1f, 0.14f, 0.3f), C(0.95f, 0.8f, 0.55f), dawn), C(0.06f, 0.07f, 0.12f), Color.Lerp(C(0.8f, 0.85f, 1f), C(1f, 0.9f, 0.6f), dawn), new Vector3(0.1f, 0.3f, 0.9f), 0.02f, 0.4f);
        if (Sky != null) {
            if (Sky.HasProperty("_Tint")) {
                // The painted hub sky warms with the file, as the gradient did.
                Sky.SetColor("_Tint", Color.Lerp(C(0.78f, 0.84f, 1f), C(1f, 0.93f, 0.8f), dawn));
            }
            RenderSettings.skybox = Sky;
        }
        float hw = ChapterHub.RoomHalfWidth + 0.8f;

        // The hour in the glass, and a glow behind the doors that warms with the file.
        foreach (GameObject w in windows) {
            LevelDress.Tint(w, 1, glow);
        }
        window.color = glow;
        window.intensity = 0.5f + dawn * 0.6f;
        LevelDress.Tint(ring, 0, Color.Lerp(C(0.55f, 0.66f, 0.85f), brass, dawn));
        LevelDress.Tint(ring, 1, Color.Lerp(C(0.45f, 0.62f, 0.95f), C(1f, 0.8f, 0.4f), dawn));

        // A seal beside every filed door.
        for (int i = 0; i < cleared; i++) {
            Vector3 d = ChapterHub.DoorPosition(i);
            GameObject seal = LevelDress.Put(stage, "of_stamp_seal", new Vector3(d.x + 1.95f, 3.2f, d.z - 0.04f), 180f + (i % 2 == 0 ? 8f : -6f));
            seal.name = "Filed Seal " + (i + 1);
            LevelDress.Tint(seal, 0, C(0.78f, 0.12f, 0.12f));
            LevelDress.Tint(seal, 1, C(0.55f, 0.08f, 0.08f));
        }

        // Filing cabinets along the back of the left wall: one more bank for every chapter filed.
        for (int i = 0; i < cleared + 1; i++) {
            GameObject cab = LevelDress.Put(stage, "of_filing_cabinet", new Vector3(-hw + 0.42f, 0f, 14.4f - i * 0.58f), 90f);
            cab.name = "Cabinet " + i;
        }
        // Stacks of paper grow on the floor by the desk.
        for (int i = 0; i < cleared; i++) {
            LevelDress.Put(stage, "of_paper_stack", new Vector3(ChapterHub.DeskPosition.x - 2f - (i % 3) * 0.7f, (i / 3) * 0.4f, ChapterHub.DeskPosition.z + 2.2f), i * 17f, 0.85f + (i % 3) * 0.1f);
        }

        if (cleared >= CampaignSave.ChapterCount) {
            // All six filed: the door number in brass over the middle of the back wall.
            LevelDress.Put(stage, "of_number_plate", new Vector3(0f, 4.15f, 14.95f), 180f).name = "Number Plate";
            var g = new GameObject("Door Number");
            g.transform.SetParent(stage, false);
            g.transform.position = new Vector3(0f, 4.15f, 14.8f);
            g.transform.rotation = Quaternion.identity;
            var t = g.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.font = font;
            t.text = "1";
            t.characterSize = 0.3f;
            t.fontSize = 48;
            t.fontStyle = FontStyle.Bold;
            t.anchor = TextAnchor.MiddleCenter;
            t.alignment = TextAlignment.Center;
            t.color = C(0.2f, 0.12f, 0.05f);
            g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        // Notices: a slip pinned on the left wall's notice bay for every side errand finished, four to a row.
        for (int i = 0; i < pinned && i < 12; i++) {
            Vector3 at = new Vector3(-hw + 0.04f, 1.5f + (i / 4) * 0.9f, 0.45f + (i % 4) * 0.72f);
            LevelDress.PutEuler(stage, "of_notice_slip", at, new Vector3(0f, 90f, i % 2 == 0 ? 4f : -4f)).name = "Pinned Slip " + i;
        }
        // Sconces: one lit on the right wall for every conversation had, so the room is brighter the more it has said.
        for (int i = 0; i < heard && i < 12; i++) {
            LevelDress.Put(stage, "of_wall_lamp", new Vector3(hw - 0.04f, 4.5f, -8f + i * 1.4f), -90f).name = "Wall Lamp " + i;
        }
        if (heard > 0) {
            LevelDress.PointLight(stage, new Vector3(hw - 2f, 3.4f, 0f), C(1f, 0.86f, 0.55f), 14f, 0.15f * heard);
        }
        Look = "stage " + StageName + ", window " + ColorUtility.ToHtmlStringRGB(glow) + ", " + (cleared + 1) + " cabinet banks, " + cleared + " stamps, " + pinned + " slips pinned, " + heard + " wall lamps lit";
        Beat("office-dressed|cleared=" + cleared + "|" + StageName);
    }

    public override void Trigger() {
        Dress(Mathf.Min(Stage + 1, CampaignSave.ChapterCount));
    }
}
