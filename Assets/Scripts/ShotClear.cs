using Hawkes.Playtest;
using System.Collections.Generic;
using UnityEngine;

// Clearance for directed shots (boss entrances, the boss set piece, the act pull-up, in-engine scenes). A planned camera is
// checked before it is used: a sphere-cast from the subject to the camera pulls it in ahead of walls, it is pushed out of
// Wallit's and every bot's body to at least `Margin` from the surface, and kept above the ground. If the spot stays boxed in
// the next framing is tried (the same distance, turned round the subject). Observe() then counts any frame that still ends
// inside a body or a wall, and that is the metric cinematic.cameraInsideFrames.
public static class ShotClear {
    public const float Margin = 0.5f;
    public const float CastRadius = 0.5f;

    public static int InsideFrames;
    public static int ShotsAdjusted;
    public static int FramingsSwapped;
    public static float MinBodyClearance = 99f;

    static readonly RaycastHit[] hits = new RaycastHit[16];

    public static void Reset() {
        InsideFrames = 0;
        ShotsAdjusted = 0;
        FramingsSwapped = 0;
        MinBodyClearance = 99f;
    }

    // A body as a vertical capsule: base point, top y, radius.
    struct Body {
        public Vector3 at;
        public float top;
        public float radius;
    }

    static void Collect(List<Body> into) {
        into.Clear();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            Body b = new Body();
            b.at = player.transform.position;
            float s = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.BodyRadius : 0.5f;
            b.radius = Mathf.Max(0.5f, s);
            b.top = b.at.y + 2f * Mathf.Max(1f, b.radius / 0.5f);
            into.Add(b);
        }
        var roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive) {
                continue;
            }
            float sc = Mathf.Max(1f, e.BodyScale);
            Body b = new Body();
            b.at = e.transform.position;
            b.radius = 0.55f * sc;
            b.top = b.at.y + 1.6f * sc;
            // The drawn size wins when there is one (a Titan or a plated body is wider than its scale suggests).
            Bounds bounds = new Bounds(b.at, Vector3.zero);
            bool any = false;
            Renderer[] rs = e.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < rs.Length; r++) {
                if (rs[r] is ParticleSystemRenderer || rs[r] is TrailRenderer || rs[r] is LineRenderer) {
                    continue;
                }
                if (!any) {
                    bounds = rs[r].bounds;
                    any = true;
                } else {
                    bounds.Encapsulate(rs[r].bounds);
                }
            }
            if (any && bounds.size.y < 40f) {
                b.radius = Mathf.Max(b.radius, Mathf.Max(bounds.extents.x, bounds.extents.z));
                b.top = Mathf.Max(b.top, bounds.max.y);
            }
            into.Add(b);
        }
    }

    static readonly List<Body> scratchBodies = new List<Body>();

    // Horizontal and vertical distance from a point to a body's surface (negative inside).
    static float Surface(Vector3 p, Body b) {
        float dy = p.y < b.at.y ? b.at.y - p.y : (p.y > b.top ? p.y - b.top : 0f);
        float dh = new Vector2(p.x - b.at.x, p.z - b.at.z).magnitude - b.radius;
        if (dy > 0f) {
            return dh > 0f ? Mathf.Sqrt(dh * dh + dy * dy) : dy;
        }
        return dh;
    }

    static Vector3 PushOutOfBodies(Vector3 pos) {
        Collect(scratchBodies);
        for (int pass = 0; pass < 3; pass++) {
            bool moved = false;
            for (int i = 0; i < scratchBodies.Count; i++) {
                Body b = scratchBodies[i];
                if (Surface(pos, b) >= Margin) {
                    continue;
                }
                Vector3 flat = new Vector3(pos.x - b.at.x, 0f, pos.z - b.at.z);
                if (flat.sqrMagnitude < 0.0001f) {
                    flat = Vector3.back;
                }
                flat.Normalize();
                // Out sideways to the margin, unless the camera is already above the head, then up.
                if (pos.y > b.top - 0.2f) {
                    pos.y = Mathf.Max(pos.y, b.top + Margin);
                } else {
                    Vector3 p = new Vector3(b.at.x, pos.y, b.at.z) + flat * (b.radius + Margin);
                    pos.x = p.x;
                    pos.z = p.z;
                }
                moved = true;
            }
            if (!moved) {
                break;
            }
        }
        return pos;
    }

    // Pull in ahead of walls: the farthest clear distance from the subject along its line to the camera.
    static Vector3 CastIn(Vector3 subject, Vector3 pos, Transform ignore, out bool shortened) {
        shortened = false;
        Vector3 to = pos - subject;
        float d = to.magnitude;
        if (d < 0.1f) {
            return pos;
        }
        Vector3 dir = to / d;
        CamBlock.EnsureMatrix();
        int n = Physics.SphereCastNonAlloc(subject, CastRadius, dir, hits, d, CamBlock.SolidMask(), QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++) {
            Collider c = hits[i].collider;
            if (c == null || CamBlock.ShouldIgnore(c, ignore) || hits[i].distance <= 0.001f) {
                continue;
            }
            best = Mathf.Min(best, hits[i].distance);
        }
        if (best < d) {
            shortened = true;
            return subject + dir * Mathf.Max(0.8f, best);
        }
        return pos;
    }

    static bool Inside(Vector3 pos) {
        return CamBlock.PointInsideSolid(pos, null, Margin * 0.6f);
    }

    // The camera spot to use for a planned one. `subject` is what it looks at (a chest-high point on the body the shot is about).
    public static Vector3 Resolve(Vector3 subject, Vector3 planned) {
        Vector3 best = Fix(subject, planned);
        if (!Inside(best) && Close(subject, planned, best)) {
            return best;
        }
        // Boxed in or pulled to nothing: the next framing, the same reach turned round the subject.
        Vector3 off = planned - subject;
        float[] turns = { 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
        Vector3 pick = best;
        float pickScore = Score(subject, planned, best);
        for (int i = 0; i < turns.Length; i++) {
            Vector3 cand = Fix(subject, subject + Quaternion.Euler(0f, turns[i], 0f) * off);
            float s = Score(subject, planned, cand);
            if (s > pickScore) {
                pickScore = s;
                pick = cand;
            }
            if (!Inside(cand) && Close(subject, subject + Quaternion.Euler(0f, turns[i], 0f) * off, cand)) {
                FramingsSwapped++;
                return cand;
            }
        }
        if (pick != best) {
            FramingsSwapped++;
        }
        return pick;
    }

    static Vector3 Fix(Vector3 subject, Vector3 planned) {
        bool shortened;
        Vector3 p = CastIn(subject, planned, null, out shortened);
        p = PushOutOfBodies(p);
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out hit, 120f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            p.y = Mathf.Max(p.y, hit.point.y + 1.0f);
        }
        if ((p - planned).sqrMagnitude > 0.0004f) {
            ShotsAdjusted++;
        }
        return p;
    }

    // A framing is "close" when it kept at least 70% of the planned reach.
    static bool Close(Vector3 subject, Vector3 planned, Vector3 got) {
        float want = Vector3.Distance(subject, planned);
        return want < 0.5f || Vector3.Distance(subject, got) >= want * 0.7f;
    }

    static float Score(Vector3 subject, Vector3 planned, Vector3 got) {
        float want = Mathf.Max(0.5f, Vector3.Distance(subject, planned));
        float s = Mathf.Clamp01(Vector3.Distance(subject, got) / want);
        return Inside(got) ? s - 1f : s;
    }

    // Called once per frame with the camera a directed shot ended on. A frame inside a wall or within a body counts.
    public static void Observe(Vector3 camPos) {
        Collect(scratchBodies);
        bool inside = Inside(camPos);
        for (int i = 0; i < scratchBodies.Count; i++) {
            float s = Surface(camPos, scratchBodies[i]);
            if (s < MinBodyClearance) {
                MinBodyClearance = s;
            }
            if (s < 0.05f) {
                inside = true;
            }
        }
        if (inside) {
            InsideFrames++;
        }
    }

    public static void Report(PlaytestRun run) {
        if (run == null) {
            return;
        }
        run.Metric("cinematic.cameraInsideFrames", InsideFrames);
        run.Metric("cinematic.shotsAdjusted", ShotsAdjusted);
        run.Metric("cinematic.framingsSwapped", FramingsSwapped);
        run.Metric("cinematic.minBodyClearanceM", Mathf.Round(Mathf.Min(MinBodyClearance, 99f) * 100f) / 100f);
    }
}
