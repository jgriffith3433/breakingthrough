using UnityEngine;
using UnityEngine.Rendering;

// Draws many copies of one shared mesh with Graphics.DrawMeshInstanced. BuildArena used to CombineMeshes each
// sector into its own ArenaScatter_*.asset (about 40 MB in the WebGL data file); the matrices here are a few hundred
// KB and the FBX mesh is shared, so the first download stays small. ShadowCastingMode is set per batch (trees on,
// grass off) the same way the old combined meshes were.
public class ArenaScatterDraw : MonoBehaviour {
    const int Batch = 1023;

    [SerializeField] Mesh mesh;
    [SerializeField] Material material;
    [SerializeField] ShadowCastingMode shadows = ShadowCastingMode.On;
    [SerializeField] Matrix4x4[] matrices;

    static MaterialPropertyBlock block;
    Matrix4x4[] scratch;

    public void Set(Mesh shared, Material mat, ShadowCastingMode mode, Matrix4x4[] poses) {
        mesh = shared;
        material = mat;
        shadows = mode;
        matrices = poses;
    }

    void LateUpdate() {
        if (mesh == null || material == null || matrices == null || matrices.Length == 0) {
            return;
        }
        if (!material.enableInstancing) {
            material.enableInstancing = true;
        }
        if (block == null) {
            block = new MaterialPropertyBlock();
        }
        if (scratch == null || scratch.Length != Batch) {
            scratch = new Matrix4x4[Batch];
        }
        int left = matrices.Length;
        int at = 0;
        while (left > 0) {
            int n = left > Batch ? Batch : left;
            for (int i = 0; i < n; i++) {
                scratch[i] = matrices[at + i];
            }
            Graphics.DrawMeshInstanced(mesh, 0, material, scratch, n, block, shadows, true, gameObject.layer);
            at += n;
            left -= n;
        }
    }
}
