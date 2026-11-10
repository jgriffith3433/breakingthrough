using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;

// The "explore" scenario: the bot walks the live map on the stick in eight headings out from where it stands, hugging whatever
// stops it, and counts stuck cells (5 m cells where it made no headway for 3 s), falls (below the ground it started on) and
// reachability (the share of probe points on the rings it could walk to). Real input only. Results are metrics keyed by map
// (scene) name and a `map-explore` event. Also exposes the state a reviewer reads (PlaytestExplore.Publish).
public static class PlaytestExplore {
    public static void Publish(PlaytestRun run) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        var cam = Camera.main;
        var chapter = ChapterRun.Instance;
        var sb = new System.Text.StringBuilder();
        sb.Append("chapter=").Append(chapter == null ? "none" : chapter.name);
        sb.Append(" objective=").Append(chapter == null ? "" : chapter.ObjectiveLine);
        if (player != null) {
            Vector3 p = player.transform.position;
            var cc = player.GetComponent<CharacterController>();
            sb.Append(" pos=").Append(p.x.ToString("0.0")).Append(',').Append(p.y.ToString("0.0")).Append(',').Append(p.z.ToString("0.0"));
            sb.Append(" grounded=").Append(cc != null && cc.isGrounded);
        }
        if (cam != null) {
            Vector3 c = cam.transform.position;
            sb.Append(" camera=").Append(c.x.ToString("0.0")).Append(',').Append(c.y.ToString("0.0")).Append(',').Append(c.z.ToString("0.0"));
        }
        run.Note(sb.ToString());
    }

    static void Steer(Vector3 dir) {
        var cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        PlayInput.Axis("Horizontal", Mathf.Clamp(Vector3.Dot(dir, right) * 1.4f, -1f, 1f));
        PlayInput.Axis("Vertical", Mathf.Clamp(Vector3.Dot(dir, fwd) * 1.4f, -1f, 1f));
    }

    public static IEnumerator Run(PlaytestRun run) {
        string map = SceneManager.GetActiveScene().name;
        yield return run.Until(() => Object.FindAnyObjectByType<CharacterMovement>() != null, 20f, "player");
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        if (player == null) {
            run.Problem(PlaytestSeverity.Bug, "explore: no player on map " + map);
            yield break;
        }
        Transform t = player.transform;
        Vector3 origin = t.position;
        float floor = origin.y;
        var stuck = new HashSet<Vector2Int>();
        int falls = 0;
        int probes = 0;
        int reached = 0;
        float[] radii = { 10f, 25f, 45f, 70f };
        for (int r = 0; r < radii.Length && !run.Done; r++) {
            for (int k = 0; k < 8 && !run.Done; k++) {
                float a = k * Mathf.PI / 4f;
                Vector3 goal = origin + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radii[r];
                probes++;
                float best = Vector3.Distance(Flat(t.position), Flat(goal));
                float lastGain = Time.realtimeSinceStartup;
                float start = Time.realtimeSinceStartup;
                bool ok = false;
                while (Time.realtimeSinceStartup - start < 25f) {
                    Vector3 to = Flat(goal) - Flat(t.position);
                    float d = to.magnitude;
                    if (d < 2.5f) {
                        ok = true;
                        break;
                    }
                    if (t.position.y < floor - 15f) {
                        falls++;
                        run.Event("fall", map + " at " + t.position.ToString("0") + " heading " + k);
                        t.position = origin;
                        break;
                    }
                    if (d < best - 0.5f) {
                        best = d;
                        lastGain = Time.realtimeSinceStartup;
                    } else if (Time.realtimeSinceStartup - lastGain > 3f) {
                        var cell = new Vector2Int(Mathf.RoundToInt(t.position.x / 5f), Mathf.RoundToInt(t.position.z / 5f));
                        if (stuck.Add(cell)) {
                            run.Event("stuck-cell", map + " " + cell + " toward " + goal.ToString("0"));
                        }
                        // hug the wall: slide sideways, then jump
                        Steer(Quaternion.Euler(0f, 90f, 0f) * to.normalized);
                        PlayInput.Tap(KeyCode.Space);
                        yield return new WaitForSecondsRealtime(1f);
                        break;
                    }
                    Steer(to.normalized);
                    if (Time.frameCount % 30 == 0) {
                        Publish(run);
                    }
                    yield return null;
                }
                PlayInput.Axis("Horizontal", 0f);
                PlayInput.Axis("Vertical", 0f);
                if (ok) {
                    reached++;
                }
                // walk back to the origin side for the next ray
                float back = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - back < 12f && Flat(t.position - origin).magnitude > 3f) {
                    Steer(Flat(origin - t.position).normalized);
                    yield return null;
                }
            }
        }
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
        float reach = probes == 0 ? 0f : 100f * reached / probes;
        run.Event("map-explore", map + ": stuck " + stuck.Count + ", falls " + falls + ", reachable " + reach.ToString("0") + "% of " + probes);
        run.Metric("explore.stuckCells." + map, stuck.Count);
        run.Metric("explore.falls." + map, falls);
        run.Metric("explore.reachablePct." + map, reach);
        if (falls > 0) {
            run.Problem(PlaytestSeverity.Bug, "explore: fell through " + map + " " + falls + " time(s)");
        }
        if (stuck.Count > 0) {
            run.Problem(PlaytestSeverity.Warning, "explore: " + stuck.Count + " stuck cell(s) on " + map);
        }
    }

    static Vector3 Flat(Vector3 v) {
        return new Vector3(v.x, 0f, v.z);
    }
}
