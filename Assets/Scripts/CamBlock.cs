using UnityEngine;

// Camera-only blockers on kit walls and buildings. Chapter maps were dressed without colliders so bots walk straight
// lines; these sit on the CamBlock layer, which movers ignore, so the camera's sphere-cast can pull in without turning
// the playfield solid. LevelDress.Proxy boxes (Chapter 1) stay on Default and already stop both Wallit and the camera.
public static class CamBlock {
    public const string LayerName = "CamBlock";

    static bool matrixReady;
    static readonly Collider[] scratch = new Collider[32];

    public static int Layer() {
        int l = LayerMask.NameToLayer(LayerName);
        return l >= 0 ? l : 7;
    }

    public static int Mask() {
        return 1 << Layer();
    }

    // Solids the camera must not enter: Default (LevelDress.Proxy) + CamBlock. Ground is a walk surface under
    // Wallit, not a wall — excluding it stops ClosestPoint-on-nonconvex-mesh from calling the floor "inside".
    public static int SolidMask() {
        int mask = (1 << 0) | Mask();
        int wall = LayerMask.NameToLayer("Default");
        if (wall >= 0) {
            mask |= 1 << wall;
        }
        return mask;
    }

    // For the feel detector only: same as SolidMask (walls and buildings, not the Ground mesh).
    public static int FeelMask() {
        return SolidMask();
    }

    public static void EnsureMatrix() {
        if (matrixReady) {
            return;
        }
        matrixReady = true;
        int cam = Layer();
        Physics.IgnoreLayerCollision(0, cam, true);   // Default (Wallit / CharacterController)
        int enemy = LayerMask.NameToLayer("Enemy");
        if (enemy >= 0) {
            Physics.IgnoreLayerCollision(enemy, cam, true);
        }
        int ground = LevelDress.GroundLayer();
        // Ground stays collidable with Default; CamBlock does not shove the controller either way.
        Physics.IgnoreLayerCollision(ground, cam, true);
    }

    // Walls and buildings: named that way in the Model Kit, or tall enough to hide the camera.
    public static bool IsWallOrBuilding(string id, Bounds localBounds) {
        if (!string.IsNullOrEmpty(id)) {
            string s = id.ToLowerInvariant();
            if (s.Contains("wall") || s.Contains("tower") || s.Contains("booth") || s.Contains("arch")
                || s.Contains("pillar") || s.Contains("shopfront") || s.Contains("door") || s.Contains("shutter")
                || s.Contains("cabinet") || s.Contains("rack") || s.Contains("dais") || s.Contains("press_frame")
                || s.Contains("gate_") || s.Contains("clock_tower") || s.Contains("chimney") || s.Contains("roof_")
                || s.Contains("slot_frame") || s.Contains("house") || s.EndsWith("_bay") || s.Contains("facade")
                || s.Contains("form_rack") || s.Contains("lectern") || s.Contains("shelf") || s.Contains("desk")
                || (s.Contains("board") && !s.Contains("notice")) || s.Contains("of_door") || s.Contains("of_wall")) {
                return localBounds.size.y >= 0.35f;
            }
        }
        return localBounds.size.y >= 1.6f && localBounds.size.x * localBounds.size.z >= 0.4f;
    }

    public static void Attach(GameObject go, string id) {
        if (go == null) {
            return;
        }
        if (go.GetComponent<Collider>() != null) {
            return;
        }
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) {
            return;
        }
        Bounds b = mf.sharedMesh.bounds;
        if (!IsWallOrBuilding(id, b)) {
            return;
        }
        EnsureMatrix();
        var box = go.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size * 1.02f;   // a hair past the mesh so the sphere cast meets it before the near plane
        SetLayerRecursive(go, Layer());
    }

    static void SetLayerRecursive(GameObject go, int layer) {
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++) {
            SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }

    // True when a point sits inside a Box/Sphere/Capsule wall (CamBlock / Proxy). MeshColliders are skipped for
    // containment: Unity's ClosestPoint on non-convex meshes is unreliable and the Ground mesh would false-positive.
    public static bool PointInsideSolid(Vector3 point, Transform ignoreRoot, float pad = 0.02f) {
        int n = Physics.OverlapSphereNonAlloc(point, Mathf.Max(0.02f, pad), scratch, FeelMask(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            Collider c = scratch[i];
            if (c == null || ShouldIgnore(c, ignoreRoot) || c is MeshCollider) {
                continue;
            }
            Vector3 closest = c.ClosestPoint(point);
            if ((closest - point).sqrMagnitude <= pad * pad) {
                return true;
            }
        }
        return false;
    }

    public static float NearestSolidDistance(Vector3 point, Transform ignoreRoot, float max) {
        float best = max;
        // SphereCast in 6 axes + overlap on box colliders only — avoids MeshCollider ClosestPoint lies.
        int n = Physics.OverlapSphereNonAlloc(point, max, scratch, FeelMask(), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            Collider c = scratch[i];
            if (c == null || ShouldIgnore(c, ignoreRoot) || c is MeshCollider) {
                continue;
            }
            Vector3 closest = c.ClosestPoint(point);
            float d = Vector3.Distance(point, closest);
            if (d < best) {
                best = d;
            }
        }
        return best;
    }

    public static bool ShouldIgnore(Collider c, Transform ignoreRoot) {
        if (c == null) {
            return true;
        }
        Transform t = c.transform;
        if (ignoreRoot != null && (t == ignoreRoot || t.IsChildOf(ignoreRoot))) {
            return true;
        }
        if (c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<Companion>() != null
            || c.GetComponentInParent<Musician>() != null || c.GetComponentInParent<CharacterMovement>() != null) {
            return true;
        }
        return false;
    }
}
