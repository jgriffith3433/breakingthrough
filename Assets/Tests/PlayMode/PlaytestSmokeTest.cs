using System;
using System.Collections;
using System.Reflection;
using Hawkes.Playtest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Runs the Hawkes playtest "smoke" scenario the way the browser runner does, through HawkesPlaytest.StartScenario.
// The game registers its bot at startup (Assets/Scripts/Playtest.cs); this test only boots the menu and watches.
public class PlaytestSmokeTest {
    [UnityTest, Timeout(240000)]
    public IEnumerator SmokeScenarioFinishes() {
        // Errors become problems in the report; the test judges the outcome, not each log line.
        LogAssert.ignoreFailingMessages = true;
        SceneManager.LoadScene("MainMenu");
        float until = Time.realtimeSinceStartup + 60f;
        while (!(HawkesPlaytest.Registered && HawkesPlaytest.IsReady) && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        Assert.IsTrue(HawkesPlaytest.Registered, "HawkesPlaytest.Register was not called at startup");
        Assert.AreEqual("theplacedepartment", HawkesPlaytest.Product);
        Assert.IsTrue(HawkesPlaytest.IsReady, "the game never reported ready");

        PlaytestRun run = HawkesPlaytest.StartScenario("smoke", new PlaytestOptions { seed = 1, speed = 1f });
        until = Time.realtimeSinceStartup + 180f;
        while (!run.Done && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        string report = HawkesPlaytest.ReportJson(run);
        Debug.Log("[Playtest] smoke report: " + report);
        Assert.IsTrue(run.Done, "the smoke scenario did not finish in 180 s");
        Assert.That(run.Outcome == PlaytestOutcome.Done || run.Outcome == PlaytestOutcome.Lost,
            "smoke ended " + run.Outcome + ": " + report);
        Time.timeScale = 1f;
    }

    [UnityTest, Timeout(60000)]
    public IEnumerator BackToBackLevelUpsCloseOnNumberKeys() {
        LogAssert.ignoreFailingMessages = true;
        Type metaProgress = Type.GetType("MetaProgress, Assembly-CSharp", true);
        Type runBoons = Type.GetType("RunBoons, Assembly-CSharp", true);
        Type levelUpScreen = Type.GetType("LevelUpScreen, Assembly-CSharp", true);
        Type playInput = Type.GetType("PlayInput, Assembly-CSharp", true);

        MethodInfo enterSandbox = metaProgress.GetMethod("EnterSandbox", BindingFlags.Public | BindingFlags.Static);
        MethodInfo addFeathers = runBoons.GetMethod("AddFeathers", BindingFlags.Public | BindingFlags.Static);
        MethodInfo need = runBoons.GetMethod("Need", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo levelProp = runBoons.GetProperty("Level", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo picksProp = runBoons.GetProperty("Picks", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo nextAtProp = runBoons.GetProperty("NextAt", BindingFlags.Public | BindingFlags.Static);
        PropertyInfo isOpenProp = levelUpScreen.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Static);
        MethodInfo tap = playInput.GetMethod("Tap", BindingFlags.Public | BindingFlags.Static);

        enterSandbox.Invoke(null, new object[] { true });
        Time.timeScale = 1f;
        SceneManager.LoadScene("scene2");
        float until = Time.realtimeSinceStartup + 30f;
        while ((SceneManager.GetActiveScene().name != "scene2" || (int)nextAtProp.GetValue(null) <= 0) && Time.realtimeSinceStartup < until) {
            yield return null;
        }
        yield return null;

        int picksStart = (int)picksProp.GetValue(null);
        int levelStart = (int)levelProp.GetValue(null);
        int needFirst = (int)need.Invoke(null, new object[] { levelStart });
        int needSecond = (int)need.Invoke(null, new object[] { levelStart + 1 });

        // Grant two levels at once (chained XP carry-over) and pick both via number keys in quick succession.
        addFeathers.Invoke(null, new object[] { (float)(needFirst + needSecond) });
        Assert.AreEqual(levelStart + 2, (int)levelProp.GetValue(null));

        yield return null;
        Assert.IsTrue((bool)isOpenProp.GetValue(null), "first level-up card did not open");
        Assert.AreEqual(0f, Time.timeScale, 0.001f);

        tap.Invoke(null, new object[] { KeyCode.Alpha1 });
        yield return null;
        Assert.IsFalse((bool)isOpenProp.GetValue(null), "first level-up card did not close on number key");
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
        Assert.AreEqual(picksStart + 1, (int)picksProp.GetValue(null));

        yield return null;
        Assert.IsTrue((bool)isOpenProp.GetValue(null), "second chained level-up card did not open");
        Assert.AreEqual(0f, Time.timeScale, 0.001f);

        tap.Invoke(null, new object[] { KeyCode.Alpha2 });
        yield return null;
        Assert.IsFalse((bool)isOpenProp.GetValue(null), "second chained level-up card did not close on number key");
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
        Assert.AreEqual(picksStart + 2, (int)picksProp.GetValue(null));

        // Also grant a third level right after the second closed and pick via number key immediately.
        int needThird = (int)need.Invoke(null, new object[] { (int)levelProp.GetValue(null) });
        addFeathers.Invoke(null, new object[] { (float)needThird });
        yield return null;
        Assert.IsTrue((bool)isOpenProp.GetValue(null), "back-to-back level-up card did not open");

        tap.Invoke(null, new object[] { KeyCode.Alpha3 });
        yield return null;
        Assert.IsFalse((bool)isOpenProp.GetValue(null), "back-to-back level-up card did not close on number key");
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
        Assert.AreEqual(picksStart + 3, (int)picksProp.GetValue(null));
    }
}
