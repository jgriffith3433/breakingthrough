using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// In-world text that always reads left to right. Unity's TextMesh reads correctly only when the viewer looks along the text's
// +Z (its back to the camera), so a sign that was turned to face the camera, or that sits under a parent yawed 180 degrees,
// shows mirrored. Two ways to be right: a Billboard copies the camera's rotation (never LookAt, never a negative scale), a Floor
// sign lies flat and takes the camera's yaw so its top points away from the viewer. Both read the same way from every side.
public class SignFacing : MonoBehaviour {
    public bool Floor;

    public static SignFacing Add(GameObject go, bool floor) {
        SignFacing s = go.GetComponent<SignFacing>();
        if (s == null) {
            s = go.AddComponent<SignFacing>();
        }
        s.Floor = floor;
        s.LateUpdate();
        return s;
    }

    void LateUpdate() {
        Camera cam = Camera.main;
        if (cam == null) {
            return;
        }
        if (Floor) {
            transform.rotation = Quaternion.Euler(90f, cam.transform.eulerAngles.y, 0f);
        } else {
            transform.rotation = cam.transform.rotation;
        }
    }
}

// The sign scan: every TextMesh whose world matrix mirrors it (a negative scale on it or a parent), or that the camera sees
// from its back (the text's forward points against the camera's), is a quality.mirroredText. Run() is a one-shot over the
// loaded scenes; the Meter component repeats it once a second while a playtest runs, so every chapter and the hub are covered.
public static class SignAudit {
    public static int Scan(Camera cam, List<string> found) {
        int n = 0;
        foreach (TextMesh tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) {
            if (tm == null || !tm.gameObject.activeInHierarchy || string.IsNullOrEmpty(tm.text)) {
                continue;
            }
            Transform t = tm.transform;
            if (t.localToWorldMatrix.determinant < 0f) {
                n++;
                found.Add(Path(t) + " \"" + tm.text.Replace('\n', ' ') + "\": mirrored by a negative scale");
                continue;
            }
            if (cam == null || !tm.GetComponent<Renderer>().isVisible) {
                continue;
            }
            Vector3 toText = t.position - cam.transform.position;
            if (Vector3.Dot(toText, cam.transform.forward) <= 0f) {
                continue;
            }
            // Readable from behind its +Z: the text's forward must point the way the camera looks.
            if (Vector3.Dot(t.forward, cam.transform.forward) < -0.1f) {
                n++;
                found.Add(Path(t) + " \"" + tm.text.Replace('\n', ' ') + "\": seen from its back (reads mirrored)");
            }
        }
        return n;
    }

    static string Path(Transform t) {
        string p = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++) {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    public class Meter : MonoBehaviour {
        static Meter instance;
        static PlaytestRun run;
        static readonly HashSet<string> told = new HashSet<string>();
        static int worst;
        float next;

        public static void Begin(PlaytestRun current) {
            run = current;
            worst = 0;
            told.Clear();
            if (instance == null) {
                var go = new GameObject("Sign Meter");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Meter>();
            }
            run.Metric("quality.mirroredText", 0f);
        }

        public static void End() {
            run = null;
        }

        void Update() {
            if (run == null || run.Done || Time.unscaledTime < next) {
                return;
            }
            next = Time.unscaledTime + 1f;
            var found = new List<string>();
            int n = Scan(Camera.main, found);
            foreach (string f in found) {
                if (told.Add(f)) {
                    run.Problem(PlaytestSeverity.Bug, "quality.mirroredText: " + f);
                }
            }
            if (told.Count > worst) {
                worst = told.Count;
                run.Metric("quality.mirroredText", (float)worst);
            }
        }
    }
}
