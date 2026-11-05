using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Character models' validation as a test (Docs/CharacterModels.md section 4). Calls CharacterValidation.Run() through
// reflection (the game scripts are in Assembly-CSharp). Fails if a character's body mesh is a built-in primitive (or a bare
// box), a required part is missing or a budget is broken, and prints the triangle count of every character.
public class CharacterValidationTests {
    [Test]
    public void CharactersAreAuthoredMeshesWithinBudget() {
        Type validation = Type.GetType("CharacterValidation, Assembly-CSharp", true);
        object report = validation.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        Type rt = report.GetType();
        string text = (string)rt.GetMethod("Text").Invoke(report, null);
        Debug.Log("[Characters] validation report\n" + text);
        Assert.AreEqual(0, (int)rt.GetField("Primitives").GetValue(report), "primitive character meshes:\n" + text);
        Assert.GreaterOrEqual((int)rt.GetField("Characters").GetValue(report), 12, "all twelve characters");
        Assert.IsTrue((bool)rt.GetProperty("Ok").GetValue(report), text);
    }
}
