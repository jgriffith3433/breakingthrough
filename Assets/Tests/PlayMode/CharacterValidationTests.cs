using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The character models' validation as a test (Docs/CharacterModels.md). Calls CharacterValidation.Run() through reflection
// (the game scripts live in Assembly-CSharp). Fails if a character body is a built-in primitive mesh (Cube, Sphere, Capsule,
// Cylinder, Plane, Quad), a rig is missing a part the animators bind, two characters share a mesh shape, or a triangle budget
// (6,000 a character, 12,000 a boss) is broken. Prints triangles per character.
public class CharacterValidationTests {
    [Test]
    public void CharactersAreAuthoredMeshesWithinBudget() {
        Type validation = Type.GetType("CharacterValidation, Assembly-CSharp", true);
        MethodInfo run = validation.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        object report = run.Invoke(null, null);
        Type rt = report.GetType();
        string text = (string)rt.GetMethod("Text").Invoke(report, null);
        Debug.Log("[Characters] validation report\n" + text);
        int primitives = (int)rt.GetField("Primitives").GetValue(report);
        bool ok = (bool)rt.GetProperty("Ok").GetValue(report);
        Assert.AreEqual(0, primitives, "character bodies that are built-in primitive meshes:\n" + text);
        Assert.IsTrue(ok, text);
    }
}
