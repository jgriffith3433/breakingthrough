using System;
using System.Reflection;
using NUnit.Framework;

// Every voice clip must use import settings WebGL can play (see VoiceImportCheck, an editor class, called by reflection).
public class VoiceImportTests {
    [Test]
    public void VoiceClipsAreWebGlSafe() {
        Type t = Type.GetType("VoiceImportCheck, Assembly-CSharp-Editor", false);
        if (t == null) Assert.Ignore("editor assembly not available in this run");
        string problems = (string)t.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        Assert.AreEqual("", problems, problems);
    }
}
