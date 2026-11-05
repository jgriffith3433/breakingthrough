using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The Model Kit's validation as a test (Docs/ModelKit.md section 5). The game scripts live in Assembly-CSharp, which this
// test assembly cannot reference, so it calls KitValidation.Run() through reflection. Fails if any kit prefab's mesh is a
// built-in primitive (Cube, Sphere, Capsule, Cylinder, Plane, Quad), a piece has fewer than 24 vertices without a
// documented reason, or the triangle and texture budgets are broken. Prints triangles and texture memory per piece and total.
public class KitValidationTests {
    [Test]
    public void KitHasNoPrimitiveMeshesAndStaysInBudget() {
        Type validation = Type.GetType("KitValidation, Assembly-CSharp", true);
        MethodInfo run = validation.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        object report = run.Invoke(null, null);
        Type rt = report.GetType();
        string text = (string)rt.GetMethod("Text").Invoke(report, null);
        Debug.Log("[Kit] validation report\n" + text);
        int primitives = (int)rt.GetField("Primitives").GetValue(report);
        int pieces = (int)rt.GetField("Pieces").GetValue(report);
        bool ok = (bool)rt.GetProperty("Ok").GetValue(report);
        Assert.AreEqual(0, primitives, "kit pieces that are built-in primitive meshes:\n" + text);
        Assert.GreaterOrEqual(pieces, 60, "the kit needs at least 60 pieces");
        Assert.IsTrue(ok, text);
    }
}
