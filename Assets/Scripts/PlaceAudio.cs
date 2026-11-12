using System.Collections.Generic;
using UnityEngine;

// The air of each place (docs/world-bible.md, Areas): the surface under Wallit's feet and the spot emitters that sit on real
// objects of the scene. The looping bed itself is MusicDirector.Ambience (fetched with its place, never in the first download);
// this adds what the bed cannot: a buzzing lamp that gets louder as Wallit walks up to it, a creaking sign, a drip, a ringing
// phone. One PlaceAudio lives under the place's own object (Hollow: ArcadeActs, hub: ChapterHub, chapters: ChapterRun / LevelScene),
// so it goes with the scene and its clips go with it. Spot sounds are files (Resources/Sfx/spot_<name>.mp3, Hawkes Gen audio.sfx)
// or, until a file is in, the synthesised ones in SfxSynth. Everything is 3D with a linear rolloff, on random intervals, quieter
// under a voice line like the bed.
public class PlaceAudio : MonoBehaviour {
    public enum Place { Hollow, Hub, Ch1, Ch2, Ch3, Ch4, Ch5, Ch6 }
    public enum Surface { Grass, Stone, Wood, Metal }

    public static PlaceAudio Instance { get; private set; }
    public static readonly List<string> Log = new List<string>();
    public static int Steps { get; private set; }
    public static int Lands { get; private set; }
    public static readonly int[] StepsOn = new int[4];
    public static string LastSurface = "";

    class Spot {
        public string id, clip;
        public string[] hints;
        public Vector3 offset;       // from the anchor when no object of the scene carries the hint
        public float minGap, maxGap, minDist, maxDist, volume;
        public AudioSource source;
        public float next;
        public string on;            // what it is attached to
    }

    // Per place: the bed's name and its spots. Hints are looked for in object names (first nearest the anchor).
    static Spot S(string id, string clip, string[] hints, Vector3 offset, float minGap, float maxGap, float vol) {
        return new Spot { id = id, clip = clip, hints = hints, offset = offset, minGap = minGap, maxGap = maxGap, minDist = 2.5f, maxDist = 34f, volume = vol };
    }

    static Spot[] SpotsFor(Place p) {
        switch (p) {
            case Place.Hollow:
                return new[] {
                    S("lamp", "spot_lamp", new[] { "lantern", "lamp" }, new Vector3(9f, 3f, 7f), 4f, 9f, 0.55f),
                    S("sign", "spot_sign", new[] { "Dead Letter Office", "hl_office" }, new Vector3(-14f, 4f, -12f), 5f, 12f, 0.6f),
                    S("gutter", "spot_drip", new[] { "yd_post_box", "mailbox", "gutter" }, new Vector3(-10f, 1f, 12f), 3f, 7f, 0.5f),
                };
            case Place.Hub:
                return new[] {
                    S("phone", "spot_phone", new[] { "desk", "phone" }, new Vector3(6f, 1f, 4f), 9f, 18f, 0.6f),
                    S("typewriter", "spot_type", new[] { "typewriter", "Tick" }, new Vector3(-7f, 1f, 5f), 5f, 11f, 0.5f),
                    S("radiator", "spot_tick", new[] { "radiator", "clock" }, new Vector3(0f, 1.5f, -8f), 2f, 4f, 0.4f),
                };
            case Place.Ch1:
                return new[] {
                    S("lamp", "spot_lamp", new[] { "lamp" }, new Vector3(10f, 3f, 8f), 4f, 9f, 0.55f),
                    S("sign", "spot_sign", new[] { "sign", "post_box", "trestle" }, new Vector3(-12f, 3f, 10f), 5f, 12f, 0.55f),
                    S("gutter", "spot_drip", new[] { "gutter", "sack" }, new Vector3(8f, 1f, -12f), 3f, 7f, 0.5f),
                };
            case Place.Ch2:
                return new[] {
                    S("lamp", "spot_lamp", new[] { "lamp", "watchtower" }, new Vector3(10f, 3f, 10f), 4f, 9f, 0.55f),
                    S("press", "spot_machine", new[] { "press", "stamp" }, new Vector3(-14f, 2f, 14f), 7f, 14f, 0.6f),
                    S("turnstile", "spot_sign", new[] { "turnstile", "booth", "gate" }, new Vector3(14f, 2f, -10f), 5f, 12f, 0.5f),
                };
            case Place.Ch3:
                return new[] {
                    S("crusher", "spot_machine", new[] { "crusher", "press" }, new Vector3(-12f, 2f, 12f), 5f, 10f, 0.65f),
                    S("belt", "spot_sign", new[] { "belt", "conveyor" }, new Vector3(12f, 1f, 10f), 4f, 9f, 0.5f),
                    S("pipe", "spot_drip", new[] { "pipe", "stack", "chute" }, new Vector3(0f, 2f, -14f), 3f, 7f, 0.5f),
                };
            case Place.Ch4:
                return new[] {
                    S("lamp", "spot_lamp", new[] { "lamp", "stilt" }, new Vector3(9f, 4f, 9f), 4f, 9f, 0.55f),
                    S("mist", "spot_drip", new[] { "slab", "spire" }, new Vector3(-9f, 0.5f, 12f), 3f, 8f, 0.45f),
                    S("sign", "spot_sign", new[] { "sign", "dais" }, new Vector3(14f, 2f, -8f), 6f, 13f, 0.5f),
                };
            case Place.Ch5:
                return new[] {
                    S("clock", "spot_tick", new[] { "clock", "chandelier" }, new Vector3(0f, 4f, 12f), 2f, 4f, 0.4f),
                    S("typewriter", "spot_type", new[] { "lectern", "desk" }, new Vector3(-10f, 1f, 8f), 6f, 12f, 0.5f),
                    S("chandelier", "spot_sign", new[] { "chandelier", "rack" }, new Vector3(11f, 4f, 7f), 6f, 13f, 0.45f),
                };
            default:
                return new[] {
                    S("doorframe", "spot_sign", new[] { "door", "frame" }, new Vector3(-10f, 2f, 10f), 5f, 12f, 0.5f),
                    S("drip", "spot_drip", new[] { "stump", "seal" }, new Vector3(10f, 0.5f, 8f), 3f, 8f, 0.45f),
                    S("lamp", "spot_lamp", new[] { "lamp" }, new Vector3(0f, 3f, -12f), 5f, 10f, 0.45f),
                };
        }
    }

    public Place Where { get; private set; }
    readonly List<Spot> spots = new List<Spot>();
    public int SpotCount { get { return spots.Count; } }
    Transform listener;

    // Starts the place's spots under parent (replacing the last place's).
    public static PlaceAudio Enter(Place place, Transform parent) {
        if (Instance != null) {
            Destroy(Instance.gameObject);
        }
        var go = new GameObject("Place Audio");
        if (parent != null) {
            go.transform.SetParent(parent, false);
        }
        var pa = go.AddComponent<PlaceAudio>();
        pa.Begin(place);
        return pa;
    }

    public static Place ForChapter(int chapterIndex) {
        return (Place)Mathf.Clamp(2 + chapterIndex, 2, 7);
    }

    void Begin(Place place) {
        Instance = this;
        Where = place;
        Steps = 0;
        Lands = 0;
        System.Array.Clear(StepsOn, 0, StepsOn.Length);
        Log.Clear();
        Log.Add("place|" + place);
        var player = FindAnyObjectByType<CharacterMovement>();
        Vector3 anchor = player != null ? player.transform.position : Vector3.zero;
        if (place == Place.Hollow) {
            GameObject piano = GameObject.Find("PianoRecital");
            if (piano != null) {
                anchor = piano.transform.position;
            }
        }
        Transform[] all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (Spot s in SpotsFor(place)) {
            Transform host = Find(all, s.hints, anchor);
            var obj = new GameObject("Spot " + s.id);
            obj.transform.SetParent(transform, false);
            if (host != null) {
                obj.transform.position = host.position + Vector3.up * 0.5f;
                s.on = host.name;
            } else {
                obj.transform.position = anchor + s.offset;
                s.on = "(placed)";
            }
            var src = obj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = s.minDist;
            src.maxDistance = s.maxDist;
            src.dopplerLevel = 0f;
            src.spread = 30f;
            s.source = src;
            s.next = Time.unscaledTime + Random.Range(0.8f, 2.5f + spots.Count);
            spots.Add(s);
            Log.Add("spot|" + s.id + "|" + s.clip + "|on " + s.on + "|" + obj.transform.position.x.ToString("0") + "," + obj.transform.position.z.ToString("0"));
        }
    }

    static Transform Find(Transform[] all, string[] hints, Vector3 anchor) {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (Transform t in all) {
            if (t == null || !t.gameObject.activeInHierarchy || t.name.StartsWith("Spot ") || t.name == "Place Audio") {
                continue;
            }
            foreach (string h in hints) {
                if (t.name.IndexOf(h, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    float d = (t.position - anchor).sqrMagnitude;
                    if (d > 36f && d < bestD && d < 120f * 120f) {
                        bestD = d;
                        best = t;
                    }
                    break;
                }
            }
        }
        return best;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // How loud the spot is at the listener (the linear rolloff the source uses): 0 beyond its range, 1 inside its near range.
    public static float Gain(Vector3 spot, Vector3 listenerAt, float minD, float maxD) {
        float d = Vector3.Distance(spot, listenerAt);
        return Mathf.Clamp01(1f - (d - minD) / Mathf.Max(0.01f, maxD - minD));
    }

    public float GainOf(int i, Vector3 at) {
        Spot s = spots[i];
        return Gain(s.source.transform.position, at, s.minDist, s.maxDist);
    }

    public Vector3 SpotPosition(int i) {
        return spots[i].source.transform.position;
    }

    public string SpotName(int i) {
        return spots[i].id;
    }

    void Update() {
        if (listener == null) {
            var cam = Camera.main;
            listener = cam != null ? cam.transform : null;
            if (listener == null) {
                return;
            }
        }
        bool voice = !WallitVoice.FloorFree || CastVoice.Speaking;
        float duck = voice ? (UiFit.Touch ? 0.38f : 0.5f) : 1f;
        float level = SoundSettings.On ? SoundSettings.EffectsLevel : 0f;
        foreach (Spot s in spots) {
            // A playing spot follows the voice dip like the bed does.
            s.source.volume = Mathf.MoveTowards(s.source.volume, s.volume * level * duck, Time.unscaledDeltaTime * 4f);
            if (Time.unscaledTime < s.next || s.source.isPlaying || Time.timeScale < 0.1f) {
                continue;
            }
            s.next = Time.unscaledTime + Random.Range(s.minGap, s.maxGap);
            if (Vector3.Distance(s.source.transform.position, listener.position) > s.maxDist + 4f) {
                continue;   // out of earshot: no voice spent on it
            }
            AudioClip clip = GameAudio.ClipOf(s.clip);
            if (clip == null || level <= 0f) {
                continue;
            }
            s.source.pitch = Random.Range(0.92f, 1.08f);
            s.source.clip = clip;
            s.source.volume = s.volume * level * duck;
            s.source.Play();
            if (Log.Count < 400) {
                Log.Add(Time.unscaledTime.ToString("0.0") + "|play|" + s.id + "|gain " + Gain(s.source.transform.position, listener.position, s.minDist, s.maxDist).ToString("0.00"));
            }
        }
    }

    // ---------------------------------------------------------------- feet

    static readonly string[] HintGrass = { "grass", "lawn", "moss", "turf", "dirt" };
    static readonly string[] HintStone = { "stone", "slab", "brick", "cobble", "concrete", "marble", "pave", "chalk" };
    static readonly string[] HintWood = { "wood", "plank", "board", "parquet", "carpet", "floor_wood" };
    static readonly string[] HintMetal = { "metal", "steel", "iron", "grat", "belt", "plate", "conveyor", "rust" };

    static bool Has(string name, string[] hints) {
        foreach (string h in hints) {
            if (name.IndexOf(h, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }

    // The ground material under a point, else what the place is made of (its anchor circle is paved).
    public static Surface SurfaceAt(Vector3 p) {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 0.8f, Vector3.down, out hit, 3f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            var rend = hit.collider.GetComponent<Renderer>();
            string n = (rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.name : "") + " " + hit.collider.name;
            if (Has(n, HintMetal)) {
                return Surface.Metal;
            }
            if (Has(n, HintWood)) {
                return Surface.Wood;
            }
            if (Has(n, HintStone)) {
                return Surface.Stone;
            }
            if (Has(n, HintGrass)) {
                return Surface.Grass;
            }
        }
        Place place = Instance != null ? Instance.Where : Place.Hollow;
        switch (place) {
            case Place.Hollow:
                return new Vector2(p.x, p.z).magnitude < 6.5f ? Surface.Stone : Surface.Grass;   // the circle is flagged
            case Place.Hub: return Surface.Wood;
            case Place.Ch1: return Mathf.Abs(p.x) < 4f ? Surface.Stone : Surface.Grass;          // the chalk slab path through the yard
            case Place.Ch2: return Mathf.Abs(p.z) > 40f ? Surface.Metal : Surface.Stone;          // plaza stone, grating at the gate
            case Place.Ch3: return Surface.Metal;
            case Place.Ch4: return Surface.Stone;
            case Place.Ch5: return Surface.Wood;
            default: return Surface.Stone;
        }
    }

    public static string StepName(Vector3 at) {
        Surface s = SurfaceAt(at);
        Steps++;
        StepsOn[(int)s]++;
        LastSurface = s.ToString();
        return "step_" + s.ToString().ToLowerInvariant();
    }

    // A landing after a jump: heavier than a step, on the same surface.
    public static void Land(Vector3 at) {
        Surface s = SurfaceAt(at);
        Lands++;
        LastSurface = s.ToString();
        GameAudio.PlayAt("land_" + s.ToString().ToLowerInvariant(), at, 0.5f, 1f);
    }
}
