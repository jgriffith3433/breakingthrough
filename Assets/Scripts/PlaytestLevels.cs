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
public static class PlaytestLevels {
    static PlaytestRun run;
    static Camera rig;

    struct Region {
        public string Name, Landmark;
        public Vector3 Pos, Look;
        public float Fov;
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
            Shoot(g.Pos, g.Look, g.Fov);
            yield return Wait(1.2f);
            SceneScanReport view = SceneValidation.Scan(SceneValidation.ChapterRoots(), rig);
            run.Scene(g.Name);
            run.Event("photo", g.Name + " chapter=" + n + " landmark=" + g.Landmark + " visibleTriangles=" + view.VisibleTriangles + " drawCalls=" + view.DrawCalls + " materials=" + view.Materials + " lights=" + view.Lights);
            if (view.VisibleTriangles > SceneValidation.MaxVisibleTriangles) {
                run.Problem(PlaytestSeverity.Bug, "levels-1-3: " + g.Name + " shows " + view.VisibleTriangles + " triangles, over " + SceneValidation.MaxVisibleTriangles);
            }
            lastPhotos++;
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
        if (index == 2) {
            b = Story.Chapters[index].Stops[Story.Chapters[index].Stops.Length - 1].Bearing;
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
