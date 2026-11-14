using System.Collections.Generic;
using UnityEngine;

// One story turn per chapter that used to be a narration line on a card, now an event in play that happens around Wallit
// (docs/world-bible.md, beat 16). Each is a kit piece that rises or lights where the play camera looks (about 7 m ahead, so
// it is on a 390x844 screen without leaving the touch controls), a soft light, and an existing sound. No new art or audio,
// no collider, no particles. Called from the code that already knows the moment (LevelYard, ChapterRun); the explore
// playtest calls Preview(chapter) as a named setup shortcut.
public static class ChapterBeats {
    public static readonly List<string> Events = new List<string>();
    static readonly HashSet<int> fired = new HashSet<int>();

    public static int Count { get { return fired.Count; } }
    public static bool Has(int chapter) { return fired.Contains(chapter); }

    struct Beat {
        public string Name, Piece, Sound;
        public bool Rise;          // the piece climbs out of the ground (a door, a gate) instead of simply standing lit
        public int Copies;         // pieces side by side (lamp pairs)
        public Color Glow;
        public float Scale;
    }

    // Chapter 1..6, in order. The line each one replaces is named in Docs/Story.md ("Chapter beats in play").
    static readonly Beat[] Beats = {
        new Beat { Name = "yard-lamp", Piece = "st_lamp_post", Sound = "courier_clip", Copies = 1, Glow = new Color(1f, 0.82f, 0.45f), Scale = 1.4f },
        new Beat { Name = "gate-answers", Piece = "gt_chalk_lamp", Sound = "stamp_heavy", Copies = 2, Rise = true, Glow = new Color(1f, 0.35f, 0.25f), Scale = 1.6f },
        new Beat { Name = "belt-stops", Piece = "fl_cage_lamp", Sound = "stomp", Copies = 1, Glow = new Color(1f, 0.3f, 0.2f), Scale = 1.6f },
        new Beat { Name = "last-lamps", Piece = "wk_lamp_stilt", Sound = "slot_shutter", Copies = 2, Rise = true, Glow = new Color(1f, 0.82f, 0.4f), Scale = 1.2f },
        new Beat { Name = "white-door", Piece = "rd_white_door", Sound = "gallery_rise", Copies = 1, Rise = true, Glow = new Color(1f, 0.95f, 0.85f), Scale = 1.2f },
        new Beat { Name = "filed-lanterns", Piece = "fd_lantern", Sound = "slot_shutter", Copies = 2, Rise = true, Glow = new Color(1f, 0.85f, 0.5f), Scale = 1.8f },
    };

    public static void NewRun() {
        fired.Clear();
        Events.Clear();
    }

    // chapter is 0-based. Once per chapter per run.
    public static bool Play(int chapter) {
        if (chapter < 0 || chapter >= Beats.Length) {
            return false;
        }
        Beat b = Beats[chapter];
        if (GameObject.Find("Chapter Beat " + b.Name) != null) {
            return false;   // already standing in this scene
        }
        Camera cam = Camera.main;
        Transform player = Musician.Instance != null ? Musician.Instance.transform : null;
        CharacterMovement who = Object.FindAnyObjectByType<CharacterMovement>();
        Vector3 from = who != null ? who.transform.position : (player != null ? player.position : Vector3.zero);
        Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        fired.Add(chapter);

        GameObject root = new GameObject("Chapter Beat " + b.Name);
        Vector3 centre = from + fwd * 7f;
        for (int i = 0; i < b.Copies; i++) {
            float side = b.Copies == 1 ? 0f : (i == 0 ? -1.8f : 1.8f);
            Vector3 at = centre + right * side;
            at.y = GroundAt(at, from.y);
            GameObject piece = LevelDress.Put(root.transform, b.Piece, at, Quaternion.LookRotation(-fwd).eulerAngles.y, b.Scale);
            Light light = new GameObject("Beat Light").AddComponent<Light>();
            light.transform.SetParent(piece.transform, false);
            light.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            light.type = LightType.Point;
            light.color = b.Glow;
            light.range = 14f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            BeatRise rise = root.AddComponent<BeatRise>();
            rise.Piece = piece.transform;
            rise.Light = light;
            rise.Ground = at.y;
            rise.Lift = b.Rise;
            rise.Delay = i * 0.35f;
        }
        root.AddComponent<BeatLife>().Seconds = 90f;
        GameAudio.Play(b.Sound, 0.8f);
        Events.Add("chapter-beat|" + (chapter + 1) + "|" + b.Name);
        return true;
    }

    // Playtest shortcut (a named setup, not a teleport): fire chapter n's beat where the player stands.
    public static bool Preview(int chapter) {
        return Play(chapter);
    }

    static float GroundAt(Vector3 at, float fallback) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 60f, at.z), Vector3.down, out RaycastHit hit, 160f, ~0, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return fallback;
    }
}

// Raises or lights one piece over about a second and a half, then holds it.
public class BeatRise : MonoBehaviour {
    public Transform Piece;
    public Light Light;
    public float Ground;
    public bool Lift;
    public float Delay;
    float t;

    void Update() {
        if (Piece == null) {
            return;
        }
        t += Time.deltaTime;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - Delay) / 1.5f));
        if (Lift) {
            Vector3 p = Piece.position;
            p.y = Ground - (1f - k) * 3.5f;
            Piece.position = p;
        }
        if (Light != null) {
            Light.intensity = 2.2f * k;
        }
    }
}

// Removes the beat's props after a while so a long chapter does not carry them.
public class BeatLife : MonoBehaviour {
    public float Seconds;

    void Start() {
        Destroy(gameObject, Seconds);
    }
}
