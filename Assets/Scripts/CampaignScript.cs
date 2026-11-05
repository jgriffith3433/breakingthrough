using System.Collections.Generic;
using UnityEngine;

// The Campaign screenplay as data (Docs/CampaignBible.md is rendered from Resources/Story/campaign_script.json).
// Read-only: nothing here changes Story.Chapters. The `bible` playtest uses Check to prove the data is complete.
public static class CampaignScript {
    [System.Serializable] public class Option { public string key, label, set, now, later; }
    [System.Serializable] public class Choice { public string id, at, prompt; public Option[] options; }
    [System.Serializable] public class Scene { public string id, name, where, beat, status; public string[] lines; }
    [System.Serializable] public class Shot { public string shot, camera, speaker, line, music; }
    [System.Serializable] public class Map { public string id, setting, route, bossArena; public string[] landmarks, hazards, palette, props; }
    [System.Serializable] public class SetPiece { public string name, status, text; }
    [System.Serializable] public class Chapter {
        public int n; public string id, title, stamp, emotion;
        public Scene[] scenes; public Choice[] choices; public Shot[] opening, closing; public Map map; public SetPiece scriptedSetPiece, setPiece;
    }
    [System.Serializable] public class Question { public string id, q, assumed; }
    [System.Serializable] public class Doc {
        public int schemaVersion; public string[] canonRequired; public Chapter[] chapters; public Chapter ending; public Question[] openQuestions;
    }

    static Doc doc;

    public static Doc Load() {
        if (doc == null) {
            TextAsset t = Resources.Load<TextAsset>("Story/campaign_script");
            doc = t != null ? JsonUtility.FromJson<Doc>(t.text) : null;
        }
        return doc;
    }

    public static string Raw() {
        TextAsset t = Resources.Load<TextAsset>("Story/campaign_script");
        return t != null ? t.text : "";
    }

    // Everything the acceptance list asks of the bible; each string is one problem (empty list: all good).
    public static List<string> Check(Doc d, string raw, out int lines, out int speakers) {
        var bad = new List<string>();
        var named = new HashSet<string>();
        lines = 0;
        if (d == null || d.chapters == null) {
            bad.Add("campaign_script.json is missing or unreadable");
            speakers = 0;
            return bad;
        }
        if (d.chapters.Length != 6) {
            bad.Add("expected 6 chapters, found " + d.chapters.Length);
        }
        var all = new List<Chapter>(d.chapters);
        if (d.ending != null) {
            all.Add(d.ending);
        } else {
            bad.Add("no ending");
        }
        foreach (Chapter c in all) {
            string who = c.n > 0 ? "chapter " + c.n : "ending";
            int sceneLines = 0;
            if (c.scenes != null) {
                foreach (Scene s in c.scenes) {
                    foreach (string l in s.lines ?? new string[0]) {
                        string code = Story.Speaker(l);
                        sceneLines++;
                        if (code != null && code != "N") {
                            named.Add(code);
                        }
                    }
                }
            }
            lines += sceneLines;
            if (sceneLines < 8) bad.Add(who + ": script under 8 lines");
            if (c.choices == null || c.choices.Length < 1) bad.Add(who + ": no dialogue choice");
            else foreach (Choice ch in c.choices) if (ch.options == null || ch.options.Length < 2) bad.Add(who + ": choice " + ch.id + " has under 2 options");
            if (c.opening == null || c.opening.Length < 3) bad.Add(who + ": opening shot list under 3 shots");
            if (c.closing == null || c.closing.Length < 3) bad.Add(who + ": closing shot list under 3 shots");
            foreach (Shot s in (c.opening ?? new Shot[0])) if (string.IsNullOrEmpty(s.shot) || string.IsNullOrEmpty(s.camera) || string.IsNullOrEmpty(s.music)) bad.Add(who + ": an opening shot lacks shot, camera or music");
            foreach (Shot s in (c.closing ?? new Shot[0])) if (string.IsNullOrEmpty(s.shot) || string.IsNullOrEmpty(s.camera) || string.IsNullOrEmpty(s.music)) bad.Add(who + ": a closing shot lacks shot, camera or music");
            Map m = c.map;
            if (m == null || string.IsNullOrEmpty(m.setting) || m.landmarks == null || m.landmarks.Length == 0 || string.IsNullOrEmpty(m.route)
                || m.hazards == null || m.hazards.Length == 0 || string.IsNullOrEmpty(m.bossArena) || m.palette == null || m.palette.Length == 0 || m.props == null || m.props.Length == 0) {
                bad.Add(who + ": map design sheet incomplete");
            }
            if ((c.scriptedSetPiece == null || string.IsNullOrEmpty(c.scriptedSetPiece.name)) && (c.setPiece == null || string.IsNullOrEmpty(c.setPiece.name))) bad.Add(who + ": no scripted set piece");
        }
        string low = raw.ToLowerInvariant();
        if (low.Contains("war" + "rok")) bad.Add("the retired cast is named in the script");
        foreach (string need in d.canonRequired ?? new string[0]) {
            if (!raw.Contains(need)) bad.Add("canon word missing: " + need);
        }
        speakers = named.Count;
        return bad;
    }
}
