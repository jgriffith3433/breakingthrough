using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// The mesh audit as a test: fails by count on inside-out meshes, zero or NaN normals and negative scale (MeshAudit.Run).
public class MeshAuditTests {
    [Test]
    public void NoInsideOutZeroNormalOrNegativeScaleMeshes() {
        Type audit = Type.GetType("MeshAudit, Assembly-CSharp", true);
        object report = audit.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        Type rt = report.GetType();
        string text = (string)rt.GetMethod("Text").Invoke(report, null);
        Debug.Log("[MeshAudit] report\n" + text);
        Assert.AreEqual(0, (int)rt.GetField("Inside").GetValue(report), text);
        Assert.AreEqual(0, (int)rt.GetField("ZeroNormals").GetValue(report), text);
        Assert.AreEqual(0, (int)rt.GetField("NegativeScales").GetValue(report), text);
    }
}
