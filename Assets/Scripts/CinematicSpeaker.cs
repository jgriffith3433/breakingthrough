using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// Portrait dialogue framing: on a tall phone a shot with a spoken line closes in on the speaker so the head is at least
// 8% of the screen height, inside the frame and above the caption band. Desktop and landscape framing are untouched.
// Every spoken shot reports the smallest head size and the off-screen frames it showed (cinematic.speakerHeadPct,
// cinematic.speakerOffscreen).
public partial class CinematicDirector {
    [System.Serializable] public class SpeakerRecord {
        public string reel, shot, who;
        public float minHeadPct;
        public int frames, offscreen;
    }
    public static readonly List<SpeakerRecord> SpeakerShots = new List<SpeakerRecord>();
    public static bool SpeakerPhone { get { return Phone(); } }

    const float SpeakerAimPct = 0.105f;     // framed a little above the 8% bar so motion never dips under it
    const float SpeakerMinPct = 0.08f;
    const float SpeakerHeadY = 0.62f;       // viewport height of the head: above the captions (top 0.30), under the bars
    const float CaptionTop01 = 0.30f;

    SpeakerRecord speakerRec;

    Transform SpeakerActor(Shot s) {
        if (s == null || string.IsNullOrEmpty(s.line)) {
            return null;
        }
        switch (s.speaker) {
            case "W": return ActorOf("wallit");
            case "S": return ActorOf("slice");
            case "K": return ActorOf("courier");
            case "Y": return ActorOf("yobot");
            case "M": return ActorOf("mega");
            case "V": return ActorOf("spider");
        }
        return null;
    }

    // Head centre and head height from the actor's rendered bounds (the top 30% of the body, 0.4 to 1.2 m).
    static bool HeadOf(Transform t, out Vector3 centre, out float height) {
        centre = t.position + Vector3.up * 1.4f;
        height = 0.5f;
        var rs = t.GetComponentsInChildren<Renderer>();
        bool have = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs) {
            if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) {
                continue;
            }
            if (!have) { b = r.bounds; have = true; } else { b.Encapsulate(r.bounds); }
        }
        if (!have) {
            return true;
        }
        height = Mathf.Clamp(b.size.y * 0.3f, 0.4f, 1.2f);
        centre = new Vector3(b.center.x, b.max.y - height * 0.5f, b.center.z);
        return true;
    }

    // Moves the camera in on the speaker and tips it so the head sits in the band above the captions.
    void TightenForSpeaker(Shot s, ref Vector3 viewPos, ref Vector3 look, ref float fov) {
        Transform who = SpeakerActor(s);
        if (who == null) {
            return;
        }
        Vector3 head;
        float headH;
        HeadOf(who, out head, out headH);
        fov = Mathf.Min(fov, 48f);
        float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float dist = headH / (SpeakerAimPct * 2f * tan);
        Vector3 away = viewPos - head;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) {
            away = -who.forward;
            away.y = 0f;
        }
        away.Normalize();
        // Slightly from the front-side of the line of sight, a little above the head so the tilt stays small.
        viewPos = head + away * dist + Vector3.up * Mathf.Min(0.35f * headH, 0.4f);
        // Head at SpeakerHeadY of the frame: aim below it by the gap between that and the centre.
        float frameH = 2f * dist * tan;
        look = head - Vector3.up * ((SpeakerHeadY - 0.5f) * frameH);
    }

    void MeasureSpeaker() {
        if (reel == null || shotIndex < 0 || shotIndex >= reel.shots.Length || cam == null || caption == null || string.IsNullOrEmpty(caption.text)) {
            return;
        }
        Shot s = reel.shots[shotIndex];
        Transform who = SpeakerActor(s);
        if (who == null || s.speaker != SubtitleSpeaker) {
            return;
        }
        if (speakerRec == null || speakerRec.shot != s.id) {
            speakerRec = new SpeakerRecord { reel = ReelId, shot = s.id, who = s.speaker, minHeadPct = 100f };
            SpeakerShots.Add(speakerRec);
        }
        Vector3 head;
        float headH;
        HeadOf(who, out head, out headH);
        Vector3 up = cam.transform.up * (headH * 0.5f);
        Vector3 top = cam.WorldToViewportPoint(head + up);
        Vector3 bot = cam.WorldToViewportPoint(head - up);
        Vector3 mid = cam.WorldToViewportPoint(head);
        float pct = Mathf.Abs(top.y - bot.y) * 100f;
        bool off = mid.z <= 0.2f || mid.x < 0.05f || mid.x > 0.95f || top.y > 0.95f || bot.y < CaptionTop01;
        speakerRec.frames++;
        speakerRec.minHeadPct = Mathf.Min(speakerRec.minHeadPct, mid.z <= 0.2f ? 0f : pct);
        if (off) {
            speakerRec.offscreen++;
        }
    }

    void FlushSpeaker() {
        if (speakerRec != null && speakerRec.frames > 0) {
            Events.Add("speaker|" + speakerRec.reel + "|" + speakerRec.shot + "|" + speakerRec.who + "|head=" + speakerRec.minHeadPct.ToString("0.0")
                + "|offscreen=" + speakerRec.offscreen + "|frames=" + speakerRec.frames);
        }
        speakerRec = null;
    }

    // Playtest: one event and two metrics per spoken shot since `from`; on a phone a head under 8% or off screen is a bug.
    public static void ReportSpeakers(PlaytestRun run, string scenario, int from, bool phone) {
        for (int i = from; i < SpeakerShots.Count; i++) {
            SpeakerRecord r = SpeakerShots[i];
            string key = r.reel + "." + r.shot;
            run.Event("speaker-shot", key + " who=" + r.who + " headPct=" + r.minHeadPct.ToString("0.0") + " offscreen=" + r.offscreen + "/" + r.frames);
            run.Metric("cinematic.speakerHeadPct." + key, r.minHeadPct);
            run.Metric("cinematic.speakerOffscreen." + key, r.offscreen);
            if (phone && r.minHeadPct + 0.001f < SpeakerMinPct * 100f) {
                run.Problem(PlaytestSeverity.Bug, scenario + ": speaker head " + r.minHeadPct.ToString("0.0") + "% of screen height in " + key + " (< 8)");
            }
            if (phone && r.offscreen > 0) {
                run.Problem(PlaytestSeverity.Bug, scenario + ": speaker off screen or under the captions for " + r.offscreen + " frames in " + key);
            }
        }
        float min = 100f;
        int off = 0;
        for (int i = from; i < SpeakerShots.Count; i++) {
            min = Mathf.Min(min, SpeakerShots[i].minHeadPct);
            off += SpeakerShots[i].offscreen;
        }
        if (SpeakerShots.Count > from) {
            run.Metric("cinematic.speakerHeadPct", min);
            run.Metric("cinematic.speakerOffscreen", off);
        }
    }
}
