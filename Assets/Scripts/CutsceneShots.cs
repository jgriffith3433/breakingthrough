using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// The shot-based cinematic player: the second way Cutscene.cs plays a scene. The text-panel scenes (Story.Chapters[i].Cutscene)
// are unchanged; a reel is data (Resources/Cinematics/<id>.json, see Docs/Story.md "Cinematics") and plays through the same
// object, so it freezes the game, blocks input and answers Cutscene.IsOpen exactly like a page scene does.
//
// A shot:  back (a still in Resources/Cinematics/Art, or video: a file in StreamingAssets/Cinematics, streamed by URL so
//          it is WebGL-safe; the still shows until the video is ready and stays if it never is), an optional front layer
//          that moves faster than the back (parallax), a Ken Burns move (from/to = pan x, pan y, zoom), a crossfade in,
//          letterbox bars, one voice line, a music level, and the line typed as a subtitle. A shot with "world" set shows the
//          game instead (wallit, slice or wide: the same camera shots the page scenes use) after the crossfade out.
// Voice:   W and S lines go through VoiceDirector ("cutscene", Key) and the shot lasts until the line has been said, so
//          lines never overlap; N and Y lines are held for reading time. Each shot cuts the last shot's voice first.
// Music:   one looping bed per reel (Resources/Cinematics/<bed> if the clip exists, else a quiet synthesised pad) that ducks
//          under any voice and fades out when the scene ends.
// Keys:    Space, click or tap: next shot. Enter, Escape or the Skip button: leave the scene at any point.
public partial class Cutscene {
    [Serializable]
    public class Shot {
        public string id;
        public string back;
        public string front;
        public string video;
        public string world;
        public string speaker = "N";
        public string line;
        public string alt;     // stand-in still in Art, shown while the generated one is not in the build
        public float seconds = 4f;
        public float fade = 0.6f;
        public float level = 1f;
        public float[] from;
        public float[] to;
    }

    [Serializable]
    public class Reel {
        public string id;
        public string bed;
        public Shot[] shots;
    }

    // Playtest and diagnostics.
    public static int ShotsShown { get; private set; }
    public static int ShotsSkipped { get; private set; }
    public static int SubtitleLines { get; private set; }
    public static float DuckedLowest { get; private set; } = 1f;

    public bool ShotMode { get { return shotMode; } }
    public int ShotIndex { get { return shotIndex; } }
    public int ShotCount { get { return reel != null && reel.shots != null ? reel.shots.Length : 0; } }
    public string ShotId { get { return reel != null && shotIndex >= 0 && shotIndex < ShotCount ? reel.shots[shotIndex].id : ""; } }
    public string ReelId { get { return reel != null && reel.id != null ? reel.id : ""; } }
    public string Speaker { get { return shotSpeaker ?? ""; } }
    public string Subtitle { get { return subtitle != null ? subtitle.text : ""; } }
    public float Zoom { get; private set; } = 1f;
    public float Pan { get; private set; }
    public float BedVolume { get { return bed != null ? bed.volume : 0f; } }
    public float Duck { get { return duck; } }
    public bool BedPlaying { get { return bed != null && bed.isPlaying; } }
    public bool VideoStreaming { get; private set; }

    class ShotSlot {
        public GameObject go;
        public CanvasGroup group;
        public RawImage back;
        public RawImage front;
        public RectTransform backRt;
        public RectTransform frontRt;
        public float alpha;
        public float target;
        public float rate = 1f;
    }

    bool shotMode;
    Reel reel;
    int shotIndex;
    float shotStart;
    float shotLength;
    bool shotSpoke;
    float typeSeconds;
    string shotSpeaker = "N";
    string shotLine = "";
    int current = -1;
    ShotSlot[] slots;
    GameObject shotRoot;
    Text subtitle;
    RectTransform subtitleRect;
    AudioSource bed;
    float duck = 1f;
    float bedLevel = 1f;
    bool bedWanted;
    VideoPlayer video;
    RenderTexture videoTexture;
    readonly List<Texture2D> loadedArt = new List<Texture2D>();
    static AudioClip padClip;

    // ---------------------------------------------------------------- loading and starting

    public static Reel LoadReel(string id) {
        TextAsset asset = Resources.Load<TextAsset>("Cinematics/" + id);
        if (asset == null) {
            return null;
        }
        Reel r = JsonUtility.FromJson<Reel>(asset.text);
        if (r == null || r.shots == null || r.shots.Length == 0) {
            return null;
        }
        foreach (Shot s in r.shots) {
            if (s.seconds <= 0f) {
                s.seconds = 4f;
            }
            if (string.IsNullOrEmpty(s.speaker)) {
                s.speaker = "N";
            }
            if (s.from == null || s.from.Length < 3) {
                s.from = new[] { 0f, 0f, 1.08f };
            }
            if (s.to == null || s.to.Length < 3) {
                s.to = new[] { 0f, 0f, 1.08f };
            }
            if (s.level <= 0f) {
                s.level = 1f;
            }
        }
        return r;
    }

    // Plays a reel by id over the running game. done runs when it ends, read through or skipped.
    public static void PlayShots(string id, Action done) {
        PlayShots(LoadReel(id), done);
    }

    public static void PlayShots(Reel r, Action done) {
        if (r == null || r.shots == null || r.shots.Length == 0) {
            if (done != null) {
                done();
            }
            return;
        }
        if (instance == null) {
            instance = new GameObject("Cutscene").AddComponent<Cutscene>();
        }
        instance.BeginShots(r, done);
    }

    void BeginShots(Reel r, Action onDone) {
        if (open) {
            Close(true);
        }
        reel = r;
        shotMode = true;
        shotIndex = -1;
        current = -1;
        page = 0;
        pageCount = r.shots.Length;
        done = onDone;
        open = true;
        barsOn = true;
        Focus = "wide";
        savedScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        root.SetActive(true);
        panel.gameObject.SetActive(false);
        shotRoot.SetActive(true);
        subtitle.gameObject.SetActive(true);
        SkipButton.gameObject.SetActive(true);
        foreach (ShotSlot s in slots) {
            s.alpha = 0f;
            s.target = 0f;
            s.group.alpha = 0f;
        }
        readyAt = Time.unscaledTime + 0.4f;
        VideoStreaming = false;
        StartBed(r.bed);
        Events.Add("open|" + pageCount + "|shots");
        RunAnalytics.Screen("cutscene");
        NextShot();
    }

    // ---------------------------------------------------------------- shots

    void NextShot() {
        WallitVoice.Silence();
        SliceVoice.Silence();
        shotIndex++;
        if (shotIndex >= reel.shots.Length) {
            Close(false);
            return;
        }
        StartShot(reel.shots[shotIndex]);
    }

    void StartShot(Shot s) {
        shotStart = Time.unscaledTime;
        shotSpoke = false;
        shotLength = Mathf.Max(0.5f, s.seconds);
        shotLine = s.line ?? "";
        shotSpeaker = string.IsNullOrEmpty(s.speaker) ? "N" : s.speaker;
        subtitle.text = "";
        page = shotIndex + 1;
        ShotsShown++;
        bedLevel = Mathf.Clamp01(s.level);
        float rate = 1f / Mathf.Max(0.05f, s.fade);
        if (!string.IsNullOrEmpty(s.world)) {
            // The game itself: fade the last picture out and let the page scenes' camera shot take over.
            Focus = s.world.Trim().ToLowerInvariant();
            if (current >= 0) {
                slots[current].target = 0f;
                slots[current].rate = rate;
            }
            current = -1;
            Events.Add("camera|" + Focus);
        } else {
            int next = current >= 0 ? 1 - current : (slots[0].alpha <= slots[1].alpha ? 0 : 1);
            ShotSlot slot = slots[next];
            ReleaseSlot(slot);
            FillSlot(slot, s);
            slot.alpha = s.fade <= 0.01f ? 1f : 0f;
            slot.target = 1f;
            slot.rate = rate;
            slot.group.alpha = slot.alpha;
            slot.go.transform.SetAsLastSibling();
            current = next;
        }
        Events.Add("shot|" + (shotIndex + 1) + "|" + reel.shots.Length + "|" + (s.id ?? ""));
    }

    void FillSlot(ShotSlot slot, Shot s) {
        slot.back.texture = LoadArt(s.back);
        if (slot.back.texture == null && !string.IsNullOrEmpty(s.alt)) {
            slot.back.texture = LoadArt(s.alt);
        }
        slot.back.enabled = slot.back.texture != null;
        slot.front.texture = LoadArt(s.front);
        slot.front.enabled = slot.front.texture != null;
        Cover(slot.back);
        Cover(slot.front);
        slot.backRt.localScale = Vector3.one;
        slot.frontRt.localScale = Vector3.one;
        if (!string.IsNullOrEmpty(s.video)) {
            StartVideo(slot, s.video);
        }
    }

    void ReleaseSlot(ShotSlot slot) {
        slot.back.texture = null;
        slot.front.texture = null;
    }

    Texture2D LoadArt(string name) {
        if (string.IsNullOrEmpty(name)) {
            return null;
        }
        Texture2D tex = Resources.Load<Texture2D>("Cinematics/Art/" + name);
        if (tex == null) {
            Events.Add("missing-art|" + name);
            return null;
        }
        if (!loadedArt.Contains(tex)) {
            loadedArt.Add(tex);
        }
        return tex;
    }

    // Crops the picture to fill the screen at any aspect (no stretching).
    void Cover(RawImage image) {
        Texture tex = image.texture;
        if (tex == null) {
            return;
        }
        Rect rect = ((RectTransform)root.transform).rect;
        float screen = rect.height > 1f ? rect.width / rect.height : 16f / 9f;
        float art = (float)tex.width / Mathf.Max(1, tex.height);
        if (screen > art) {
            float h = art / screen;
            image.uvRect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        } else {
            float w = screen / art;
            image.uvRect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
    }

    // Streams a clip from StreamingAssets/Cinematics by URL (the WebGL way). The still stays up until it is ready.
    void StartVideo(ShotSlot slot, string file) {
        try {
            if (video == null) {
                video = gameObject.AddComponent<VideoPlayer>();
                video.playOnAwake = false;
                video.audioOutputMode = VideoAudioOutputMode.None;
                video.isLooping = true;
                video.renderMode = VideoRenderMode.RenderTexture;
                video.errorReceived += (p, message) => Events.Add("video-error|" + message);
            }
            if (videoTexture == null) {
                videoTexture = new RenderTexture(960, 540, 0);
                video.targetTexture = videoTexture;
            }
            video.Stop();
            video.source = VideoSource.Url;
            video.url = Application.streamingAssetsPath + "/Cinematics/" + file;
            video.prepareCompleted += OnVideoReady;
            videoSlot = slot;
            video.Prepare();
        } catch (Exception e) {
            Events.Add("video-error|" + e.Message);
        }
    }

    ShotSlot videoSlot;

    void OnVideoReady(VideoPlayer player) {
        player.prepareCompleted -= OnVideoReady;
        if (!shotMode || videoSlot == null) {
            return;
        }
        videoSlot.back.texture = videoTexture;
        videoSlot.back.uvRect = new Rect(0f, 0f, 1f, 1f);
        videoSlot.back.enabled = true;
        player.Play();
        VideoStreaming = true;
        Events.Add("video|playing");
    }

    void TickShots(float dt) {
        float now = Time.unscaledTime;
        float t = now - shotStart;
        if (shotIndex < 0 || shotIndex >= reel.shots.Length) {
            return;
        }
        Shot shot = reel.shots[shotIndex];
        foreach (ShotSlot s in slots) {
            s.alpha = Mathf.MoveTowards(s.alpha, s.target, dt * s.rate);
            s.group.alpha = s.alpha;
        }
        if (current >= 0 && slots[current].alpha >= 0.999f) {
            ShotSlot old = slots[current == 0 ? 1 : 0];
            old.alpha = 0f;
            old.target = 0f;
            old.group.alpha = 0f;
        }
        MoveCamera(shot, Mathf.Clamp01(t / shotLength));
        if (!shotSpoke && t >= 0.35f) {
            shotSpoke = true;
            SayShotLine();
        }
        TickSubtitle(t);
        if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PlayInput.GetKeyDown(KeyCode.Escape)) {
            Close(true);
            return;
        }
        if (now < readyAt) {
            return;
        }
        if (PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetMouseButtonDown(0) || TouchBegan()) {
            if (UiFit.Touch && TapOnSkip()) {
                return;
            }
            NextShot();
            return;
        }
        if (shotSpoke && t >= shotLength && WallitVoice.FloorFree) {
            NextShot();
        }
    }

    // The Ken Burns move: pan and zoom from one frame to another over the shot, the front layer travelling faster.
    void MoveCamera(Shot s, float u) {
        if (current < 0) {
            return;
        }
        ShotSlot slot = slots[current];
        float k = Mathf.SmoothStep(0f, 1f, u);
        float z = Mathf.Max(1f, Mathf.Lerp(s.from[2], s.to[2], k));
        float x = Mathf.Clamp(Mathf.Lerp(s.from[0], s.to[0], k), -1f, 1f);
        float y = Mathf.Clamp(Mathf.Lerp(s.from[1], s.to[1], k), -1f, 1f);
        Vector2 size = ((RectTransform)root.transform).rect.size;
        slot.backRt.localScale = new Vector3(z, z, 1f);
        slot.backRt.anchoredPosition = new Vector2(-x * (z - 1f) * size.x * 0.5f, -y * (z - 1f) * size.y * 0.5f);
        float zf = z * 1.12f;
        slot.frontRt.localScale = new Vector3(zf, zf, 1f);
        slot.frontRt.anchoredPosition = new Vector2(Mathf.Clamp(-x * 1.8f, -1f, 1f) * (zf - 1f) * size.x * 0.5f, Mathf.Clamp(-y * 1.8f, -1f, 1f) * (zf - 1f) * size.y * 0.5f);
        Zoom = z;
        Pan = x;
    }

    // One voice at a time: the last shot's voice was cut in NextShot. A W or S line asks the director and holds the shot
    // for as long as it takes to say; the other speakers are held for reading time.
    void SayShotLine() {
        if (shotLine.Length == 0) {
            return;
        }
        float seconds = 0f;
        if (shotSpeaker == "W" || shotSpeaker == "S") {
            WallitVoice.Silence();
            SliceVoice.Silence();
            if (!WallitVoice.FloorFree) {
                Overlaps++;
                Events.Add("overlap|" + shotSpeaker + "|" + shotLine);
            } else if (VoiceDirector.Allow(shotLine, "cutscene", VoiceDirector.Priority.Key)) {
                seconds = shotSpeaker == "W" ? WallitVoice.Cutscene(shotLine) : SliceVoice.Cutscene(shotLine);
                VoiceLines++;
                Events.Add("voice|" + shotSpeaker + "|" + seconds.ToString("0.00"));
            }
        } else {
            Cast.Spoke(shotSpeaker, shotLine);
            seconds = PlayCastVoice();
        }
        float reading = Mathf.Max(seconds, shotLine.Length / 16f + 0.8f);
        typeSeconds = Mathf.Max(0.8f, Mathf.Min(reading, seconds > 0f ? seconds * 0.85f : reading * 0.8f));
        shotLength = Mathf.Max(shotLength, 0.35f + reading + 0.5f);
        SubtitleLines++;
        Events.Add("subtitle|" + shotSpeaker + "|" + shotLine.Replace('\n', ' '));
    }

    // The other speakers (Courier, Foreman, Alpha, Yo Bots, narration) speak from Resources/Voice/cinema/<reel>_<shot>.mp3 when
    // the file exists (made through Hawkes Gen audio.voice, one voice per character); with none the subtitle is the line.
    AudioSource castVoice;
    float PlayCastVoice() {
        if (castVoice != null) {
            castVoice.Stop();
        }
        if (reel == null || shotIndex < 0 || shotIndex >= ShotCount) {
            return 0f;
        }
        AudioClip clip = Resources.Load<AudioClip>("Voice/cinema/" + reel.id + "_" + reel.shots[shotIndex].id);
        if (clip == null) {
            return 0f;
        }
        if (castVoice == null) {
            castVoice = gameObject.AddComponent<AudioSource>();
            castVoice.playOnAwake = false;
        }
        castVoice.clip = clip;
        castVoice.volume = Mathf.Clamp01(SoundSettings.EffectsLevel);
        castVoice.Play();
        VoiceLines++;
        Events.Add("voice|" + shotSpeaker + "|" + clip.length.ToString("0.00"));
        return clip.length;
    }

    void TickSubtitle(float t) {
        if (!shotSpoke || shotLine.Length == 0) {
            return;
        }
        float into = Mathf.Max(0f, t - 0.35f);
        int shown = Mathf.Clamp(Mathf.CeilToInt(shotLine.Length * Mathf.Clamp01(into / typeSeconds)), 0, shotLine.Length);
        string name = Story.Name(shotSpeaker);
        string head = "";
        if (!string.IsNullOrEmpty(name)) {
            head = "<b><color=#" + ColorUtility.ToHtmlStringRGB(SpeakerColor(shotSpeaker)) + ">" + name + "</color></b>   ";
        }
        subtitle.fontStyle = shotSpeaker == "N" ? FontStyle.Italic : FontStyle.Normal;
        subtitle.text = head + shotLine.Substring(0, shown);
    }

    // ---------------------------------------------------------------- music

    void StartBed(string name) {
        if (bed == null) {
            bed = gameObject.AddComponent<AudioSource>();
            bed.loop = true;
            bed.playOnAwake = false;
            bed.spatialBlend = 0f;
            bed.ignoreListenerPause = true;
        }
        AudioClip clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Cinematics/" + name);
        if (clip == null && !string.IsNullOrEmpty(name)) {
            clip = Pad();
        }
        bedWanted = clip != null;
        duck = 1f;
        DuckedLowest = 1f;
        if (clip != null) {
            bed.clip = clip;
            bed.volume = 0f;
            bed.Play();
            Events.Add("bed|" + clip.name);
        }
    }

    // Placeholder music: a quiet low pad (A1, E2, A2 with a slow swell), an exact loop of 8 seconds. Real music replaces it
    // by dropping a clip named in the reel's "bed" into Resources/Cinematics.
    static AudioClip Pad() {
        if (padClip != null) {
            return padClip;
        }
        const int rate = 22050;
        const int seconds = 8;
        var data = new float[rate * seconds];
        for (int i = 0; i < data.Length; i++) {
            float t = (float)i / rate;
            float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
            float v = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.2f;
            data[i] = v * swell * 0.5f;
        }
        padClip = AudioClip.Create("pad", data.Length, 1, rate, false);
        padClip.SetData(data, 0);
        return padClip;
    }

    // Runs always (the scene's own fade-out happens after it has closed). Ducks under any voice line.
    void TickBed(float dt) {
        if (bed == null || !bed.isPlaying) {
            return;
        }
        bool voice = !WallitVoice.FloorFree || (castVoice != null && castVoice.isPlaying);
        duck = Mathf.MoveTowards(duck, voice ? 0.3f : 1f, dt * 3f);
        if (open && shotMode) {
            DuckedLowest = Mathf.Min(DuckedLowest, duck);
        }
        float want = open && bedWanted && SoundSettings.On ? SoundSettings.Music * 0.7f * bedLevel * duck : 0f;
        bed.volume = Mathf.MoveTowards(bed.volume, want, dt * (want > bed.volume ? 0.8f : 1.6f));
        if (!open && bed.volume <= 0.001f) {
            bed.Stop();
        }
    }

    // ---------------------------------------------------------------- ending and build

    void EndShots() {
        shotMode = false;
        if (castVoice != null) {
            castVoice.Stop();
        }
        bedWanted = false;
        if (video != null) {
            video.prepareCompleted -= OnVideoReady;
            video.Stop();
        }
        videoSlot = null;
        if (shotRoot != null) {
            foreach (ShotSlot s in slots) {
                ReleaseSlot(s);
                s.alpha = 0f;
                s.group.alpha = 0f;
            }
            shotRoot.SetActive(false);
            subtitle.text = "";
            subtitle.gameObject.SetActive(false);
        }
        foreach (Texture2D tex in loadedArt) {
            if (tex != null) {
                Resources.UnloadAsset(tex);
            }
        }
        loadedArt.Clear();
        if (videoTexture != null) {
            videoTexture.Release();
            Destroy(videoTexture);
            videoTexture = null;
            if (video != null) {
                video.targetTexture = null;
            }
        }
        current = -1;
        VideoStreaming = false;
    }

    void BuildShots() {
        shotRoot = new GameObject("Shots", typeof(RectTransform));
        shotRoot.transform.SetParent(root.transform, false);
        Stretch((RectTransform)shotRoot.transform);
        shotRoot.transform.SetAsFirstSibling();
        slots = new ShotSlot[2];
        for (int i = 0; i < 2; i++) {
            var s = new ShotSlot();
            s.go = new GameObject("Shot " + i, typeof(RectTransform));
            s.go.transform.SetParent(shotRoot.transform, false);
            Stretch((RectTransform)s.go.transform);
            s.group = s.go.AddComponent<CanvasGroup>();
            s.group.interactable = false;
            s.group.blocksRaycasts = false;
            var plate = new GameObject("Plate", typeof(RectTransform));
            plate.transform.SetParent(s.go.transform, false);
            Stretch((RectTransform)plate.transform);
            var plateImage = plate.AddComponent<Image>();
            plateImage.color = Color.black;
            plateImage.raycastTarget = false;
            s.back = MakeLayer(s.go.transform, "Back");
            s.front = MakeLayer(s.go.transform, "Front");
            s.backRt = s.back.rectTransform;
            s.frontRt = s.front.rectTransform;
            slots[i] = s;
        }
        var subGo = new GameObject("Subtitle", typeof(RectTransform));
        subGo.transform.SetParent(root.transform, false);
        subtitle = subGo.AddComponent<Text>();
        subtitle.font = font;
        subtitle.supportRichText = true;
        subtitle.alignment = TextAnchor.LowerCenter;
        subtitle.color = new Color(1f, 0.97f, 0.9f);
        subtitle.raycastTarget = false;
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitle.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = subGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = subtitleRect.anchorMax = new Vector2(0.5f, 0f);
        subtitleRect.pivot = new Vector2(0.5f, 0f);
        shotRoot.SetActive(false);
        subGo.SetActive(false);
        LayoutSubtitle();
    }

    RawImage MakeLayer(Transform parent, string name) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<RawImage>();
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        image.enabled = false;
        return image;
    }

    // Subtitles sit just above the bottom letterbox bar, 44 units on desktop and 34 or 30 on phones.
    void LayoutSubtitle() {
        if (subtitle == null) {
            return;
        }
        UiMode ui = UiFit.Mode;
        Vector2 size = UiFit.Size;
        subtitle.fontSize = ui == UiMode.Desktop ? 44 : ui == UiMode.Tall ? 30 : 34;
        float bar = size.y * BarFraction * Mathf.SmoothStep(0f, 1f, barsNow);
        subtitleRect.sizeDelta = new Vector2(Mathf.Min(1500f, size.x - 40f), ui == UiMode.Desktop ? 150f : 120f);
        subtitleRect.anchoredPosition = new Vector2(0f, bar + 24f);
    }
}
