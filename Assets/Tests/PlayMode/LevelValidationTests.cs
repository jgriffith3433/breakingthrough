using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// The scene validation for chapters 1 to 6 and the hub (Docs/Levels1to3.md section 6, Docs/Levels4to6AndHub.md section 11). Loads each chapter's scene in play mode (Chapter 1's
// level scene, and the Gate Hall and Clearing Floor maps over the game scene), lets it fill in, then runs SceneValidation.ScanChapter
// through reflection (the game scripts live in Assembly-CSharp, which this test assembly cannot reference). Fails if any renderer
// uses a built-in primitive mesh (Cube, Sphere, Capsule, Cylinder, Plane, Quad) apart from the documented accents, or if the
// scene's visible triangles or estimated draw calls break the budget in Docs/ModelKit.md. Prints triangles and draw calls.
public class LevelValidationTests {
    static Type Game(string name) {
        return Type.GetType(name + ", Assembly-CSharp", true);
    }

    static object Call(Type t, string method, params object[] args) {
        return t.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    }

    static object Prop(Type t, string name) {
        return t.GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
    }

    static void Sandbox() {
        Call(Game("MetaProgress"), "EnterSandbox", true);
        MethodInfo enter = Game("CampaignSave").GetMethod("EnterSandbox", BindingFlags.Public | BindingFlags.Static);
        enter.Invoke(null, enter.GetParameters().Length == 0 ? new object[0] : new object[] { null });
    }

    static void Check(string chapter, int minRenderers = 100) {
        object report = Call(Game("SceneValidation"), "ScanChapter");
        Type rt = report.GetType();
        string text = (string)rt.GetMethod("Text").Invoke(report, null);
        Debug.Log("[Levels] " + chapter + "\n" + text);
        int primitives = (int)rt.GetField("Primitives").GetValue(report);
        int renderers = (int)rt.GetField("Renderers").GetValue(report);
        bool ok = (bool)rt.GetProperty("Ok").GetValue(report);
        Assert.AreEqual(0, primitives, chapter + ": renderers on a built-in primitive mesh:\n" + text);
        Assert.Greater(renderers, minRenderers, chapter + ": the scene did not fill in:\n" + text);
        Assert.IsTrue(ok, chapter + ":\n" + text);
    }

    [UnityTest]
    public IEnumerator Chapter1YardHasNoPrimitivesAndStaysInBudget() {
        Sandbox();
        SceneManager.LoadScene("Level_1_Yard");
        float until = Time.realtimeSinceStartup + 40f;
        while (Prop(Game("LevelScene"), "Instance") == null && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        Assert.IsNotNull(Prop(Game("LevelScene"), "Instance"), "Level_1_Yard did not start its level");
        for (int i = 0; i < 10; i++) {
            yield return null;
        }
        Check("chapter 1 (Level_1_Yard)");
    }

    IEnumerator ChapterMap(string map, string label) {
        Sandbox();
        SceneManager.LoadScene((string)Game("MetaProgress").GetField("GameScene", BindingFlags.Public | BindingFlags.Static).GetValue(null));
        for (int i = 0; i < 20; i++) {
            yield return null;
        }
        Type chapterMap = Game("ChapterMap");
        Call(chapterMap, "Load", map);
        float until = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < until) {
            object current = Prop(chapterMap, "Current");
            if (current != null && chapterMap.GetProperty("Own").GetValue(current, null) != null) {
                break;
            }
            yield return null;
        }
        for (int i = 0; i < 10; i++) {
            yield return null;
        }
        Check(label);
        Call(chapterMap, "Unload");
    }

    [UnityTest]
    public IEnumerator Chapter2GateHallHasNoPrimitivesAndStaysInBudget() {
        yield return ChapterMap("hollow-edge", "chapter 2 (Map_Edge)");
    }

    [UnityTest]
    public IEnumerator Chapter3ClearingFloorHasNoPrimitivesAndStaysInBudget() {
        yield return ChapterMap("clearing-floor", "chapter 3 (Map_Floor)");
    }

    [UnityTest]
    public IEnumerator Chapter4LongWalkHasNoPrimitivesAndStaysInBudget() {
        yield return ChapterMap("long-walk", "chapter 4 (Map_Walk, with the Servers' Dais)");
    }

    [UnityTest]
    public IEnumerator Chapter5ReadingRoomHasNoPrimitivesAndStaysInBudget() {
        yield return ChapterMap("reading-room", "chapter 5 (Map_Reading, with the Registrar's Gallery)");
    }

    [UnityTest]
    public IEnumerator Chapter6FiledHollowHasNoPrimitivesAndStaysInBudget() {
        yield return ChapterMap("hollow-filed", "chapter 6 (Map_Filed, with the Threshold)");
    }

    // The hub: the case file (sandbox) has three chapters filed, the campaign scene opens on the hub, the office's scene fills in and
    // ChapterHub adds its people, pickups, door leaves and board; SceneValidation.ChapterRoots covers both.
    [UnityTest]
    public IEnumerator HubOfficeHasNoPrimitivesAndStaysInBudget() {
        Sandbox();
        Type save = Game("CampaignSave");
        object slot = Call(save, "Get", 0);
        Type slotType = slot.GetType();
        slotType.GetField("used").SetValue(slot, true);
        slotType.GetField("cleared").SetValue(slot, 3);
        slotType.GetField("chapter").SetValue(slot, 3);
        slotType.GetField("reached").SetValue(slot, 3);
        Call(save, "Open", 0);
        Game("ChapterScene").GetField("HubNext", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);
        SceneManager.LoadScene((string)save.GetField("Scene", BindingFlags.Public | BindingFlags.Static).GetValue(null));
        Type hub = Game("ChapterHub");
        Type chapterMap = Game("ChapterMap");
        float until = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < until) {
            object current = Prop(chapterMap, "Current");
            if (Prop(hub, "Instance") != null && current != null && chapterMap.GetProperty("Own").GetValue(current, null) != null) {
                break;
            }
            yield return null;
        }
        Assert.IsNotNull(Prop(hub, "Instance"), "the campaign scene did not open the hub");
        for (int i = 0; i < 20; i++) {
            yield return null;
        }
        Check("hub (Map_Office with ChapterHub's world)", 60);
    }
}
