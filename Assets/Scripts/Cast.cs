using System;
using System.Collections.Generic;
using UnityEngine;

// The cast: who can speak in a dialogue box. Data lives in Resources/Cast/cast.json (every character in
// Docs/CampaignBible.md and Docs/Story.md except Warrok, who is retired). The dialogue boxes (Cutscene, StoryScreen)
// ask here for a speaker code's name, colour, portrait and voice hook, so a new character is a row in the file.
public static class Cast {
    [Serializable]
    public class Member {
        public string id;
        public string code;
        public string name;
        public string colour;
        public string portrait;
        public string glyph;
        public string voice;      // wallit, slice, bark or none
        public string bible;
        public string voiceId;    // the Hawkes Gen voice (hv_* house voice, or Wallit's ElevenLabs id)
        public string voiceName;
        public string direction;  // how the voice delivers a line
        public string provider;   // open or elevenlabs
    }

    [Serializable]
    class File {
        public int schemaVersion;
        public Member[] members;
    }

    // Hook for every spoken page: (member, line). Wallit and Slice also speak through their own voices; the rest are
    // text plus this hook until their voice files exist (Resources/Voice/<id>_<line>.mp3).
    public static event Action<Member, string> LineSpoken;

    static List<Member> members;
    static readonly Dictionary<string, Member> byCode = new Dictionary<string, Member>();
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        members = null;
        byCode.Clear();
        sprites.Clear();
        LineSpoken = null;
    }

    public static IReadOnlyList<Member> All {
        get {
            Load();
            return members;
        }
    }

    static void Load() {
        if (members != null) {
            return;
        }
        members = new List<Member>();
        TextAsset asset = Resources.Load<TextAsset>("Cast/cast");
        File file = asset != null ? JsonUtility.FromJson<File>(asset.text) : null;
        if (file != null && file.members != null) {
            foreach (Member m in file.members) {
                if (m == null || string.IsNullOrEmpty(m.code) || m.id == "warrok") {
                    continue;
                }
                members.Add(m);
                byCode[m.code] = m;
            }
        } else {
            Debug.LogWarning("Cast: Resources/Cast/cast.json is missing or unreadable");
        }
    }

    public static Member Find(string code) {
        Load();
        Member m;
        return byCode.TryGetValue(code, out m) ? m : null;
    }

    public static string NameOf(string code) {
        Member m = Find(code);
        return m != null ? m.name : "";
    }

    public static Color ColourOf(string code) {
        Member m = Find(code);
        Color c;
        if (m != null && ColorUtility.TryParseHtmlString(m.colour, out c)) {
            return c;
        }
        return new Color(1f, 0.92f, 0.72f);
    }

    // The member's portrait art if Resources has it, else null (the box draws a stand-in disc).
    public static Sprite PortraitOf(string code) {
        Member m = Find(code);
        if (m == null || string.IsNullOrEmpty(m.portrait)) {
            return null;
        }
        Sprite s;
        if (sprites.TryGetValue(m.id, out s)) {
            return s;
        }
        Texture2D tex = OnDemand.Texture(m.portrait);
        s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        if (s != null || !OnDemand.Indexed(m.portrait)) {
            sprites[m.id] = s;   // a portrait still on its way is asked for again, not remembered as missing
        }
        return s;
    }

    public static string GlyphOf(string code) {
        Member m = Find(code);
        return m != null && !string.IsNullOrEmpty(m.glyph) ? m.glyph : "";
    }

    // True when the code has a portrait slot (everyone but the narrator).
    public static bool HasPortrait(string code) {
        Member m = Find(code);
        return m != null && !string.IsNullOrEmpty(m.name);
    }

    public static void Spoke(string code, string line) {
        Member m = Find(code);
        if (m != null && LineSpoken != null) {
            LineSpoken(m, line);
        }
    }

    // One line per cast member, for the dialogue playtest and a quick look at the box.
    public static string[] Roll() {
        Load();
        var lines = new List<string>();
        foreach (Member m in members) {
            if (m.id == "narrator") {
                lines.Add("N|The Hollow, and everyone in it.");
            } else {
                lines.Add(m.code + "|" + m.name.Substring(0, 1) + m.name.Substring(1).ToLowerInvariant() + " on the books.");
            }
        }
        return lines.ToArray();
    }
}
