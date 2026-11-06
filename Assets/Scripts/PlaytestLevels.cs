using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "levels-1-3" scenario (Docs/Levels1to3.md, section 6): Chapters 1, 2 and 3 as the three different places they are now.
// For each chapter, with real input on the menus and the sandbox save (never the real one): Campaign, case file 1 standing on the
// chapter, Begin, the chapter's scene fills in (event level-load with seconds and memory: Chapter 1 is its own level scene, 2 and 3
// are the chapter maps over the game scene), the opening scene is skipped with Enter. Then the scene scan (`scene-scan`: renderers,
// built-in primitive meshes, triangles, estimated draw calls, lights; a bug for any primitive, over 150,000 visible triangles or
// over 400 draw calls) and a tour of the chapter's regions, each photographed by a named setup shortcut (a photo camera put at the
// region's vantage point; the player is not moved) with the landmark it shows (event `photo`: region, landmark, visible triangles,
// draw calls, lights; `photo-region` lists every region). The objectives, set pieces, dialogues and encounters of the three chapters
// are played end to end by campaign-beats (1), chapter2 and chapter3.
// The "levels-4-6" scenario (Docs/Levels4to6AndHub.md, section 11) is the same for the hub office and chapters 4, 5 and 6: the hub is
// opened by the case file's Hub button and walked end to end with real input (the stick), scanned and photographed (doors, board,
// desk, windows, rug, lodgers, back wall; then the office after all six chapters, a named setup shortcut); each of the three chapters
// is then begun from the menu, scanned and photographed region by region, boss arena and its approach included (the Reading Room's
// Gallery is raised by the named set-piece shortcut first). The chapters' objectives, doors, set pieces, dialogues and boss fights are
// played end to end by chapter4, chapter5, chapter6, hub, hub-doors, hub-npcs and hub-life.
public static class PlaytestLevels {
    static PlaytestRun run;
    static Camera rig;

    struct Region {
        public string Name, Landmark;
        public Vector3 Pos, Look;
        public float Fov;
        public bool Trigger;   // the map's set piece is made to happen first (a named setup shortcut)
    }

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Wait(float seconds) {
        yield return new WaitForSecondsRealtime(seconds);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CampaignSave.EnterSandbox();
        run.Event("setup", "levels-1-3: sandbox case file; the photo camera is a named setup shortcut");
        int photos = 0;
        int bad = 0;
        for (int index = 0; index < 3 && !run.Done; index++) {
            yield return Chapter(index);
            photos += lastPhotos;
            bad += lastPrimitives;
        }
        DestroyRig();
        run.Metric("levelPhotos", photos);
        run.Metric("levelPrimitivesTotal", bad);
        if (!run.Done) {
            run.Event("levels-done", photos + " photos, " + bad + " primitive renderers over chapters 1 to 3");
        }
    }

    // The hub office and chapters 4 to 6.
    public static IEnumerator RunLate(PlaytestRun r) {
        run = r;
        CampaignSave.EnterSandbox();
        run.Event("setup", "levels-4-6: sandbox case file; the photo camera is a named setup shortcut");
        int photos = 0;
        int bad = 0;
        yield return Hub();
        photos += lastPhotos;
        bad += lastPrimitives;
        for (int index = 3; index < 6 && !run.Done; index++) {
            yield return Chapter(index);
            photos += lastPhotos;
            bad += lastPrimitives;
        }
        DestroyRig();
        run.Metric("levelPhotos", photos);
        run.Metric("levelPrimitivesTotal", bad);
        if (!run.Done) {
            run.Event("levels-done", photos + " photos, " + bad + " primitive renderers over the hub and chapters 4 to 6");
        }
    }

    static IEnumerator WalkHub(ChapterHub hub, Vector3 target, float seconds) {
        float start = Time.realtimeSinceStartup;
        while (!run.Done && Time.realtimeSinceStartup - start < seconds) {
            Vector3 to = target - hub.Player.position;
            to.y = 0f;
            if (to.magnitude < 0.6f) {
                break;
            }
            PlayInput.Axis("Horizontal", Mathf.Clamp(to.x, -1f, 1f));
            PlayInput.Axis("Vertical", Mathf.Clamp(to.z, -1f, 1f));
            yield return null;
        }
        PlayInput.Release();
    }

    static IEnumerator Hub() {
        lastPhotos = 0;
        lastPrimitives = 0;
        CampaignSave.Slot slot = CampaignSave.Get(0);
        slot.used = true;
        slot.chapter = 3;
        slot.reached = 3;
        slot.cleared = 3;
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "levels-4-6: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return Wait(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1.5f);
        float hubAt = Time.realtimeSinceStartup;
        Tap(ChapterScene.Instance.HubButton.gameObject);
        run.Event("tap", "Hub (from the chapter card of a file with 3 chapters filed)");
        yield return run.Until(() => ChapterHub.Instance != null && ChapterHub.Instance.Shown && ChapterMap.Current != null && ChapterMap.Current.Own != null, 45f, "the hub office");
        if (run.Done) {
            yield break;
        }
        ChapterHub hub = ChapterHub.Instance;
        float loadSeconds = Time.realtimeSinceStartup - hubAt;
        yield return Wait(2.5f);
        float mb = Mathf.Round(ChapterMap.Memory() / 1048576f);
        run.Metric("hubLoadSeconds", Mathf.Round(loadSeconds * 10f) / 10f);
        run.Metric("hubMemoryMB", mb);
        run.Event("level-load", "hub: office in " + loadSeconds.ToString("0.0") + " s, memory " + mb + " MB, scene " + ChapterMap.Current.MapName + ", doors " + hub.DoorText);
        if (loadSeconds > 25f) {
            run.Problem(PlaytestSeverity.Bug, "levels-4-6: the hub took " + loadSeconds.ToString("0.0") + " s to load (budget 25 s)");
        }
        if (mb > 1024f) {
            run.Problem(PlaytestSeverity.Bug, "levels-4-6: the hub holds " + mb + " MB after loading (budget 1024 MB)");
        }

        SceneScanReport scan = SceneValidation.ScanChapter();
        run.Event("scene-scan", "hub " + scan.Summary());
        run.Metric("hubRenderers", scan.Renderers);
        run.Metric("hubKitPieces", scan.KitPieces);
        run.Metric("hubTriangles", scan.Triangles);
        run.Metric("hubVisibleTriangles", scan.VisibleTriangles);
        run.Metric("hubDrawCalls", scan.DrawCalls);
        run.Metric("hubPrimitives", scan.Primitives);
        run.Metric("hubLights", scan.Lights);
        run.Metric("hubColliders", scan.Colliders);
        lastPrimitives = scan.Primitives;
        foreach (string f in scan.Failures) {
            run.Problem(PlaytestSeverity.Bug, "scene-scan hub: " + f);
        }

        // Walk the room end to end on the stick: the left wall, the back wall by the doors, the right wall, back to the ring.
        float walkedFrom = hub.Walked;
        Vector3[] route = {
            new Vector3(-12.5f, 0f, -6f), new Vector3(-12.5f, 0f, 12.5f), new Vector3(0f, 0f, 13.2f),
            new Vector3(12.5f, 0f, 12.5f), new Vector3(12.5f, 0f, -6f), new Vector3(3f, 0f, -4f),
        };
        foreach (Vector3 stop in route) {
            yield return WalkHub(hub, stop, 14f);
        }
        float walked = hub.Walked - walkedFrom;
        run.Metric("hubWalkedEndToEnd", Mathf.Round(walked * 10f) / 10f);
        run.Event("hub-walk-end-to-end", "Wallit walked " + walked.ToString("0.0") + " m along both walls and the doors and back to the ring, ending at " + hub.Player.position.ToString("0.0"));
        if (walked < 70f) {
            run.Problem(PlaytestSeverity.Bug, "levels-4-6: the hub is not walkable end to end (" + walked.ToString("0.0") + " m walked of about 85)");
        }

        Region[] regions = HubRegions();
        foreach (Region g in regions) {
            run.Event("photo-region", "hub " + g.Name + " shows " + g.Landmark);
        }
        foreach (Region g in regions) {
            yield return Photo(g, "hub");
        }
        // The office after all six chapters (a named setup shortcut: dress it as a finished file): dawn in the windows, the door number.
        hub.Office.Dress(6, 12, 12);
        run.Event("setup", "hub office dressed as all six filed (OfficeMap.Dress): " + hub.Office.Look);
        yield return Wait(0.8f);
        SceneScanReport filed = SceneValidation.ScanChapter();
        run.Event("scene-scan", "hub-filed " + filed.Summary());
        run.Metric("hubFiledTriangles", filed.Triangles);
        run.Metric("hubFiledDrawCalls", filed.DrawCalls);
        run.Metric("hubFiledPrimitives", filed.Primitives);
        foreach (string f in filed.Failures) {
            run.Problem(PlaytestSeverity.Bug, "scene-scan hub filed: " + f);
        }
        yield return Photo(R("hub-filed-back-wall", "the back wall with the brass door number, seals beside the doors, sconces and dawn in the windows", new Vector3(0f, 5f, 0f), new Vector3(0f, 3f, 15f), 70f), "hub");
        yield return Photo(R("hub-filed-windows", "the left windows in dawn light with the skyline and the pinned slips on the notice wall", new Vector3(8f, 3f, 0f), new Vector3(-15f, 2.5f, 2f), 70f), "hub");
        DestroyRig();
        run.Event("map-leave", "leaving the hub for the menu (setup shortcut)");
        PlayInput.Release();
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after leaving the hub");
        yield return Wait(1f);
    }

    static Region[] HubRegions() {
        return new[] {
            R("hub-overview", "the whole office from the hub camera: rug, ring, doors, windows, board and desk", new Vector3(0f, 9f, -13f), new Vector3(0f, 1f, 3f), 55f),
            R("hub-doors", "the six themed doors on the back wall: post-office slot, civic arch, steel hatch, iron lamp frame, paper door, pale round arch", new Vector3(0f, 3f, 4f), new Vector3(0f, 2.4f, 15f), 72f),
            R("hub-door-left", "the doors of chapters 1 and 2 with their leaves, lamps and seals", new Vector3(-8f, 2.6f, 7f), new Vector3(-9.2f, 2.2f, 15f), 60f),
            R("hub-door-right", "the doors of chapters 5 and 6, the paper door and the pale round arch", new Vector3(8f, 2.6f, 7f), new Vector3(9.2f, 2.2f, 15f), 60f),
            R("hub-board", "the chapter board on its posts with six slip frames, shelves and a window behind", new Vector3(-6f, 2.6f, -1f), new Vector3(-12.2f, 2f, 3f), 62f),
            R("hub-desk", "Tick's two desks with lamp and papers, pigeonholes and the pneumatic tube on the right wall", new Vector3(5f, 3f, -2f), new Vector3(11.8f, 1.2f, 4f), 62f),
            R("hub-windows", "the window bays in the left wall with the Department's roofs outside", new Vector3(3f, 2.6f, -2f), new Vector3(-15f, 2.4f, 1f), 74f),
            R("hub-ring-rug", "Slice's brass ring and the runner rug to the doors under the hanging lamps", new Vector3(0f, 7f, -9f), new Vector3(0f, 0f, 7f), 62f),
            R("hub-lodgers", "the four lodgers (Tick the clock, Brim the hat, Spare the half-built Yo Bot, Chime the bell) at their spots", new Vector3(0f, 5.5f, -10f), new Vector3(0f, 1f, 1f), 64f),
            R("hub-back-corner", "the filing cabinets, shelves and plant in the back left corner", new Vector3(-4f, 4f, 6f), new Vector3(-14f, 1f, 12f), 66f),
        };
    }

    // One photograph: the camera goes to the region, the view is scanned (visible triangles, draw calls, lights).
    static IEnumerator Photo(Region g, string chapter) {
        if (g.Trigger && ChapterMap.Current != null && ChapterMap.Current.Own != null) {
            run.Event("setup", g.Name + ": the map's set piece is made to happen first (OwnMap.Trigger)");
            ChapterMap.Current.Own.Trigger();
            yield return Wait(4.5f);
        }
        Shoot(g.Pos, g.Look, g.Fov);
        yield return Wait(1.2f);
        SceneScanReport view = SceneValidation.Scan(SceneValidation.ChapterRoots(), rig);
        run.Scene(g.Name);
        run.Event("photo", g.Name + " chapter=" + chapter + " landmark=" + g.Landmark + " visibleTriangles=" + view.VisibleTriangles + " drawCalls=" + view.DrawCalls + " materials=" + view.Materials + " lights=" + view.Lights);
        if (view.VisibleTriangles > SceneValidation.MaxVisibleTriangles) {
            run.Problem(PlaytestSeverity.Bug, "levels-4-6: " + g.Name + " shows " + view.VisibleTriangles + " triangles, over " + SceneValidation.MaxVisibleTriangles);
        }
        lastPhotos++;
    }

    static int lastPhotos;
    static int lastPrimitives;

    static IEnumerator Chapter(int index) {
        lastPhotos = 0;
        lastPrimitives = 0;
        string n = (index + 1).ToString();
        CampaignSave.Slot slot = CampaignSave.Get(0);
        slot.used = true;
        slot.chapter = index;
        slot.reached = index;
        slot.cleared = index;
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "levels-1-3: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return Wait(1f);
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        float cardAt = Time.realtimeSinceStartup;
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
        if (run.Done) {
            yield break;
        }
        float cardSeconds = Time.realtimeSinceStartup - cardAt;
        yield return Wait(2f);
        float beginAt = Time.realtimeSinceStartup;
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin chapter " + n);
        if (index == 0) {
            yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 60f, "Chapter 1's level scene");
        } else {
            yield return run.Until(() => ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the chapter's game scene");
            if (run.Done) {
                yield break;
            }
            yield return run.Until(() => ChapterMap.Current != null && ChapterMap.Current.Own != null, 30f, "the chapter's own map scene to fill in");
            if (run.Done) {
                yield break;
            }
            yield return run.Until(() => Cutscene.IsOpen || ChapterRun.Instance.PlaySeconds > 3f, 12f, "the opening scene");
            if (Cutscene.IsOpen) {
                yield return Wait(0.6f);
                PlayInput.Tap(KeyCode.Return);
                yield return run.Until(() => !Cutscene.IsOpen, 8f, "the opening scene to close");
            }
        }
        if (run.Done) {
            yield break;
        }
        float loadSeconds = Time.realtimeSinceStartup - beginAt;
        yield return Wait(2.5f);   // the chapter's lighting goes on a few frames after the map
        float mb = Mathf.Round(ChapterMap.Memory() / 1048576f);
        run.Metric("levelCardSeconds" + n, Mathf.Round(cardSeconds * 10f) / 10f);
        run.Metric("levelLoadSeconds" + n, Mathf.Round(loadSeconds * 10f) / 10f);
        run.Metric("levelMemoryMB" + n, mb);
        run.Event("level-load", "chapter " + n + ": card in " + cardSeconds.ToString("0.0") + " s, scene in " + loadSeconds.ToString("0.0") + " s, memory " + mb + " MB, scene " + (index == 0 ? SceneManager.GetActiveScene().name : ChapterMap.Current.MapName));
        if (cardSeconds + loadSeconds > 25f) {
            run.Problem(PlaytestSeverity.Bug, "levels-1-3: chapter " + n + " took " + (cardSeconds + loadSeconds).ToString("0.0") + " s to load (budget 25 s)");
        }
        if (mb > 1024f) {
            run.Problem(PlaytestSeverity.Bug, "levels-1-3: chapter " + n + " holds " + mb + " MB after loading (budget 1024 MB)");
        }

        SceneScanReport scan = SceneValidation.ScanChapter();
        run.Event("scene-scan", "chapter " + n + " " + scan.Summary());
        run.Metric("levelRenderers" + n, scan.Renderers);
        run.Metric("levelKitPieces" + n, scan.KitPieces);
        run.Metric("levelTriangles" + n, scan.Triangles);
        run.Metric("levelVisibleTriangles" + n, scan.VisibleTriangles);
        run.Metric("levelDrawCalls" + n, scan.DrawCalls);
        run.Metric("levelPrimitives" + n, scan.Primitives);
        run.Metric("levelLights" + n, scan.Lights);
        run.Metric("levelColliders" + n, scan.Colliders);
        lastPrimitives = scan.Primitives;
        foreach (string f in scan.Failures) {
            run.Problem(PlaytestSeverity.Bug, "scene-scan chapter " + n + ": " + f);
        }

        Region[] regions = Regions(index);
        foreach (Region g in regions) {
            run.Event("photo-region", "chapter " + n + " " + g.Name + " shows " + g.Landmark);
        }
        foreach (Region g in regions) {
            yield return Photo(g, n);
        }
        DestroyRig();

        run.Event("map-leave", "leaving chapter " + n + " for the menu (setup shortcut)");
        PlayInput.Release();
        if (index == 0) {
            SceneManager.LoadScene(MetaProgress.MenuScene);
        } else if (ChapterRun.Instance != null) {
            ChapterRun.Instance.Abandon();
        }
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after leaving");
        yield return Wait(1f);
    }

    static void Shoot(Vector3 pos, Vector3 look, float fov) {
        if (rig == null) {
            var go = new GameObject("Photo Camera");
            rig = go.AddComponent<Camera>();
            rig.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            rig.nearClipPlane = 0.3f;
            rig.farClipPlane = 600f;
            rig.depth = 50f;
        }
        Camera main = Camera.main;
        if (main != null && main != rig) {
            rig.backgroundColor = main.backgroundColor;
            rig.allowHDR = main.allowHDR;
        }
        rig.fieldOfView = fov;
        rig.transform.position = pos;
        rig.transform.rotation = Quaternion.LookRotation(look - pos);
        rig.enabled = true;
    }

    static void DestroyRig() {
        if (rig != null) {
            Object.Destroy(rig.gameObject);
            rig = null;
        }
    }

    // Chapters 4 to 6: a polar point about the circle, so each region is told as bearing, distance and height.
    static Region[] LateRegions(int index, Vector3 c, float routeBearing) {
        System.Func<float, float, float, Vector3> Pol = (bearing, dist, up) => {
            float a = bearing * Mathf.Deg2Rad;
            return c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * dist + Vector3.up * up;
        };
        if (index == 3) {
            float w = routeBearing;
            float arena = (w + 180f) % 360f;
            return new[] {
                R("ch4-causeway", "the pale slab causeway with its railings, bollards and lamp stilts running out through the mist", Pol(w, 6f, 3.4f), Pol(w, 96f, 5f)),
                R("ch4-first-lamp", "the first lamp stop: a pair of folded-leg stilts with violet heads", Pol(w, 12f, 3.2f), Pol(w, 32f, 4f), 60f),
                R("ch4-rest", "the middle lamp's stone bench, arrow sign and urn off the road", Pol(w, 44f, 3.2f), Pol(w, 63f, 1.5f), 66f),
                R("ch4-broken-end", "the last slabs broken off and fallen into the pit past the last lamp", Pol(w, 86f, 3.6f), Pol(w, 118f, -2f), 64f),
                R("ch4-spires", "the pale spires (needle, banded tower, obelisk) standing out of the mist", Pol(w, 20f, 4f), Pol(w + 70f, 300f, 20f), 70f),
                R("ch4-arena-from-route", "the Servers' Dais seen from the circle: the violet ring in the mist, away from the causeway", Pol(w, 4f, 3.6f), Pol(arena, 78f, 4f), 60f),
                R("ch4-arena", "the Servers' Dais close: the tiered round dais with its violet ring, twelve folded-leg pillars and the paper stand", Pol(arena, 38f, 9f), Pol(arena, 78f, 2.5f), 68f),
            };
        }
        if (index == 4) {
            return new[] {
                R("ch5-aisle", "the red carpet aisle between files of shelves and reading desks, running to the white door", Pol(180f, 12f, 3.6f), Pol(180f, 110f, 6f), 60f),
                R("ch5-door", "the white door at the hall's end, ruled like a form", Pol(180f, 90f, 4f), Pol(180f, 122f, 7f), 64f),
                R("ch5-forms-wall", "the racks of identical forms on the wall under the tall arched windows", Pol(232f, 40f, 4f), Pol(52f, 123f, 9f), 68f),
                R("ch5-vault", "the ribbed vault with hanging chandeliers over the colonnade", Pol(180f, 12f, 3f), Pol(0f, 30f, 55f), 76f),
                R("ch5-desk-lecterns", "the empty desk and the page lecterns round the brass circle", Pol(180f, 14f, 3.6f), Pol(25f, 3f, 1.5f), 64f),
                R("ch5-arena-sunk", "the floor behind the desk where the Gallery is sunk until the fifth page is read", Pol(180f, 6f, 3.6f), Pol(0f, 80f, 0f), 60f),
                RT("ch5-arena-risen", "the Registrar's Gallery risen: three ringed steps, the Great Ledger, the rail and tall columns", Pol(180f, 6f, 3.6f), Pol(0f, 80f, 3f), 60f),
                R("ch5-arena-close", "the Gallery close: ringed steps in brass and stone round the Ledger", Pol(0f, 42f, 8f), Pol(0f, 80f, 2f), 70f),
            };
        }
        return new[] {
            R("ch6-path", "the pale slab path with its banners and lanterns running to nothing", Pol(180f, 14f, 3.6f), Pol(180f, 150f, 6f), 60f),
            R("ch6-seal", "the great gold seal hanging past the path's end", Pol(180f, 140f, 4f), Pol(180f, 196f, 15f), 60f),
            R("ch6-stumps", "the broken stumps of the Department wall and the freestanding doorframes in the grass", Pol(0f, 0f, 4f), Pol(110f, 190f, 3f), 70f),
            R("ch6-circle", "the circle drawn in white on the dawn grass", Pol(180f, 40f, 12f), Pol(0f, 0f, 0f), 66f),
            R("ch6-threshold-from-route", "the Threshold seen from the circle: a bare white doorframe on a dais, away from the path", Pol(0f, 4f, 3.6f), Pol(20f, 80f, 6f), 58f),
            R("ch6-threshold", "the Threshold close: the two-step dais with its gold ring, the doorframe, pillars, lanterns and the red stamp marks", Pol(20f, 42f, 7f), Pol(20f, 80f, 3f), 68f),
            R("ch6-grass", "the rolling dawn grass with tufts and the low sun", Pol(60f, 10f, 2f), Pol(90f, 120f, 10f), 70f),
        };
    }

    // A region whose map set piece is made to happen first (the Reading Room's Gallery rises).
    static Region RT(string name, string landmark, Vector3 pos, Vector3 look, float fov = 62f) {
        Region g = R(name, landmark, pos, look, fov);
        g.Trigger = true;
        return g;
    }

    static Region R(string name, string landmark, Vector3 pos, Vector3 look, float fov = 62f) {
        return new Region { Name = name, Landmark = landmark, Pos = pos, Look = look, Fov = fov };
    }

    static Vector3 Ground(float x, float z, float up) {
        return new Vector3(x, LevelYard.H(x, z) + up, z);
    }

    static Region[] Regions(int index) {
        Vector3 c = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        if (index == 0) {
            return new[] {
                R("ch1-return-hall", "the Return Hall's five shut doors, wings, roofs and chimneys", Ground(0f, -42f, 3.4f), Ground(0f, -60f, 4f)),
                R("ch1-ring-and-tower", "the gold ring and nine standing stones with the lit clock tower behind the Dead Letter Office", Ground(0f, -27f, 3.6f), Ground(0f, 50f, 9f), 58f),
                R("ch1-west-trail", "the notice boards, dead trees and the fallen mailbox on the spoil bank trail", Ground(-17f, 0f, 3.4f), Ground(-40f, 14f, 2.5f)),
                R("ch1-spoil-bank", "the stump ring and carved stone on the spoil bank above the trail", Ground(-18f, 52f, 6f), Ground(-30f, 30f, 2f)),
                R("ch1-dock", "the sorting dock: drop box, trestles, sacks, post boxes and crates", Ground(12f, -6f, 3.6f), Ground(38f, 12f, 2f)),
                R("ch1-return-slot", "the Return Slot's frame and shutter with its amber strip", Ground(22f, 30f, 3.8f), Ground(30f, 49f, 3f)),
                R("ch1-skyline", "the skyline of roofs and the clock tower over the Dead Letter Office", Ground(0f, 20f, 9f), Ground(0f, 80f, 14f), 64f),
            };
        }
        float b = Story.Chapters[index].Bearing;
        if (Story.Chapters[index].Stops != null && Story.Chapters[index].Stops.Length > 0) {
            b = Story.Chapters[index].Stops[Story.Chapters[index].Stops.Length - 1].Bearing;
        }
        if (index >= 3) {
            return LateRegions(index, c, b);
        }
        float rad = b * Mathf.Deg2Rad;
        Vector3 a = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Vector3 r = new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
        System.Func<float, float, float, Vector3> P = (along, side, up) => c + a * along + r * side + Vector3.up * up;
        if (index == 1) {
            return new[] {
                R("ch2-road", "the chalk road with its paired lamps running to the gate", P(8f, 0f, 3.4f), P(70f, 0f, 6f)),
                R("ch2-press", "the big stamp press across the road with its red mark", P(30f, 11f, 4.5f), P(46f, 0f, 4f)),
                R("ch2-plaza-gate", "the paved plaza, turnstiles and INVALID boards before the gate arch with its handleless door", P(48f, 5f, 3.6f), P(76f, 0f, 6f)),
                R("ch2-loop", "the blank signpost loop with the torn postmark stone", P(28f, -8f, 4f), P(43f, 29f, 2f)),
                R("ch2-wall", "the Department wall in bays and towers with the watchtower", P(40f, -30f, 6f), P(76f, 10f, 8f), 66f),
                R("ch2-skyline", "the towers and roofs of the skyline", P(20f, 0f, 8f), P(110f, 0f, 12f), 66f),
            };
        }
        float sb = 71f * Mathf.Deg2Rad;
        Vector3 stack = c + new Vector3(Mathf.Sin(sb), 0f, Mathf.Cos(sb)) * 275f + Vector3.up * 24f;
        return new[] {
            R("ch3-lane-gantries", "the lane under six hook gantries hung with Yo Bot chassis", P(10f, 0f, 3.4f), P(70f, 0f, 5f)),
            R("ch3-broken-gate", "the broken gate, fallen lintel and racks of chassis at the first stop", P(20f, -6f, 3.5f), P(36f, 0f, 3f)),
            R("ch3-belts", "the conveyor belts carrying a hat, a clock and a bell", P(55f, 0f, 4f), P(80f, 17f, 2f)),
            R("ch3-crushers", "a press slamming on its red mark beside the lane", P(48f, 10f, 4f), P(62f, -6.5f, 3f)),
            R("ch3-lever", "the lever stand under its red lamp", P(82f, 0f, 4f), P(108f, 0f, 8f)),
            R("ch3-rim", "the smokestacks on the rim with glowing mouths", P(40f, 0f, 3.5f), stack, 66f),
        };
    }
}
