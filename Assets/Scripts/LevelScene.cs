using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// A campaign level: its own scene (Assets/Maps/Level_<n>_<name>.unity, an empty scene like Campaign.unity), built here in
// code with real colliders, a hand-placed cast and no timer (Docs/CampaignDesign.md, sections 0 and 7). This class is the
// rig every level shares: the camera, the moon, Wallit with his bat, his health, the shove a stamp gives, the way back to
// the hub. The level's own places and beats are in its content class (LevelYard for Chapter 1).
public class LevelScene : MonoBehaviour {
    public static LevelScene Instance { get; private set; }
    public AudioSource Bed { get; private set; }

    // Chapters that are levels, by index. Chapter 1 is the Sorting Yard; the others still play the old way until their slice.
    public static readonly string[] Scenes = { "Level_1_Yard" };
    // The old chapter-run playtests press Begin on Chapter 1 and expect that flow; they switch this off. Players always get the level.
    public static bool ChapterOneIsLevel = true;

    static int pending;

    public static bool Replaces(int chapter) {
        return chapter >= 0 && chapter < Scenes.Length && Scenes[chapter] != null && (chapter != 0 || ChapterOneIsLevel);
    }

    public static void Enter(int chapter) {
        pending = chapter;
        SceneManager.LoadScene(Scenes[chapter]);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (scene.name.StartsWith("Level_") && Instance == null) {
            new GameObject("Level").AddComponent<LevelScene>();
        }
    }

    public const int MaxHealth = 12;

    public int Chapter { get; private set; }
    public string SceneName { get; private set; }
    public Transform Player { get; private set; }
    public Camera Cam { get; private set; }
    public LevelHud Hud { get; private set; }
    public LevelYard Yard { get; private set; }
    public int Health { get; private set; }
    public int DamageTaken { get; private set; }
    public int Defeats { get; private set; }
    public float Walked { get; private set; }
    public float Seconds { get; private set; }
    public bool Completed { get; private set; }
    public bool Leaving { get; private set; }
    public float SecondsSinceInput { get { return Time.time - lastInputAt; } }
    public Vector3 Checkpoint { get; set; }
    public int Shoves { get; private set; }

    CharacterController body;
    Vector3 lastPos;
    float invulnUntil;
    float lastInputAt;
    float shoveLeft;
    Vector3 shoveDir;
    float respawnAt = -1f;
    float escAt = -9f;

    void Awake() {
        Instance = this;
        SceneName = SceneManager.GetActiveScene().name;
        Chapter = Mathf.Max(0, pending);
        Time.timeScale = 1f;
        Enemy.Hunt = true;
        LevelBarks.Reset();
        PlayerGrowth.Struck += OnStruck;
        Health = MaxHealth;
        lastInputAt = Time.time;
        if (CampaignSave.Active < 0) {
            CampaignSave.Open(Mathf.Max(0, CampaignSave.Last));   // opened straight from the editor
        }
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }
        Moon();
        Hud = LevelHud.Make(transform);
        Yard = gameObject.AddComponent<LevelYard>();
        Yard.Build(this);
        MakeCamera();
        MakePlayer(Yard.PlayerStart);
        Checkpoint = Yard.PlayerStart;
        Yard.Begin();
        MusicDirector.Play(MusicDirector.ChapterTrack(Chapter));
        Bed = SceneMood.Bed(transform, SceneMood.Kind.Yard);
        RunAnalytics.Screen("level_" + (Chapter + 1));
    }

    void OnDestroy() {
        PlayerGrowth.Struck -= OnStruck;
        Enemy.Hunt = false;
        if (Instance == this) {
            Instance = null;
        }
    }

    // Past midnight over the yard: a moon from behind and above, a trilight night, fog the far roofs sink into, and a gradient sky
    // with a pale moon disc (Docs/Levels1to3.md, 2). The lamps are the level's own point lights.
    static void Moon() {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.08f, 0.15f);
        RenderSettings.fogDensity = 0.009f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.27f, 0.33f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.16f, 0.2f, 0.3f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.11f);
        Material sky = LevelDress.Sky("Sky Sorting Yard", new Color(0.03f, 0.06f, 0.16f), new Color(0.15f, 0.21f, 0.35f), new Color(0.03f, 0.04f, 0.07f), new Color(0.85f, 0.9f, 1f), new Vector3(-0.35f, 0.5f, 0.8f), 0.016f, 0.5f);
        if (sky != null) {
            RenderSettings.skybox = sky;
        }
        var sun = new GameObject("Moon").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.62f, 0.72f, 1f);
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
    }

    // The camera is scene2's: ZeldaStyleCamera with the numbers that scene serializes, a night grade over it.
    void MakeCamera() {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Cam = go.AddComponent<Camera>();
        Cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.04f, 0.06f, 0.12f);
        Cam.fieldOfView = 60f;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 520f;
        Cam.allowHDR = !UiFit.Touch;
        go.AddComponent<AudioListener>();
        go.transform.position = Yard.PlayerStart + new Vector3(0f, 4f, -6f);
        go.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        SceneMood.Apply(Cam, SceneMood.Yard(), false);
        go.AddComponent<ZeldaStyleCamera>();
    }

    // Wallit: the same parts scene2's Player carries (CharacterController, MovementMotor, CharacterMovement, PlayerGrowth,
    // BatEquipment, the lock-on box), added in the order their Awakes need. CharacterMovement dresses him in the painted model.
    void MakePlayer(Vector3 at) {
        var p = new GameObject("Player");
        p.tag = "Player";
        p.transform.position = at + Vector3.up * 0.2f;
        p.transform.rotation = Quaternion.LookRotation(Yard.PlayerFacing);
        Player = p.transform;

        body = p.AddComponent<CharacterController>();
        body.height = 2f;
        body.radius = 0.5f;
        body.center = new Vector3(0f, 1.08f, 0f);
        body.slopeLimit = 45f;
        body.stepOffset = 0.3f;
        body.skinWidth = 0.08f;
        body.minMoveDistance = 0.001f;

        var follow = new GameObject("ptarget");
        follow.transform.SetParent(p.transform, false);
        follow.transform.localPosition = new Vector3(0f, 1.767f, 0f);

        var lockOn = new GameObject("LockOn");
        lockOn.transform.SetParent(p.transform, false);
        lockOn.transform.localPosition = new Vector3(0f, 1.52f, 1.81f);
        var box = lockOn.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3.5f, 5f, 5f);
        lockOn.AddComponent<LockOn>();

        var firstPerson = new GameObject("FirstPersonCamPos");
        firstPerson.transform.SetParent(p.transform, false);
        firstPerson.transform.localPosition = new Vector3(0f, 1.898f, 0.211f);

        ZeldaStyleCamera zelda = Cam.GetComponent<ZeldaStyleCamera>();
        zelda.ConfigureForLevel(follow, LockMarker(), firstPerson);

        p.AddComponent<MovementMotor>();
        CharacterMovement movement = p.AddComponent<CharacterMovement>();
        movement.ZeldaCam = zelda;
        p.AddComponent<PlayerGrowth>();
        p.AddComponent<BatEquipment>();
        lastPos = p.transform.position;
    }

    // The ring that sits on a locked-on bot (scene2 keeps it as a prefab; here it is a sprite drawn once).
    static GameObject LockMarker() {
        var go = new GameObject("LockedOnCrosshair");
        var sr = go.AddComponent<SpriteRenderer>();
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < n; y++) {
            for (int x = 0; x < n; x++) {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.1f);
                tex.SetPixel(x, y, new Color(1f, 0.82f, 0.3f, a));
            }
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 40f);
        sr.enabled = false;
        return go;
    }

    void OnStruck(Enemy by) {
        if (Completed || Time.time < invulnUntil || respawnAt >= 0f) {
            return;
        }
        Health -= by != null && (by.Boss || by.Elite) ? 2 : 1;
        DamageTaken++;
        invulnUntil = Time.time + 0.9f;
        if (Health <= 0) {
            Health = 0;
            Defeated();
        }
    }

    // Wallit goes down: a beat on the floor, then back at the last checkpoint with pickups kept (Docs/CampaignDesign.md, 5.5).
    void Defeated() {
        Defeats++;
        respawnAt = Time.time + 1.8f;
        Yard.Note("defeat", "Wallit is down (" + Defeats + ")");
        CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_DEATH, 1.6f);
        Hud.ShowReceipt("RETURNED TO SENDER. Fee: one coin.", 2.4f);
        GameAudio.Play("stomp", 0.8f, 0.7f);
    }

    void Resume() {
        respawnAt = -1f;
        body.enabled = false;
        Player.position = Checkpoint + Vector3.up * 0.2f;
        body.enabled = true;
        lastPos = Player.position;
        Health = MaxHealth;
        invulnUntil = Time.time + 2f;
        CharacterMovement.ClearActionClip();
        CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
        Yard.Resumed();
        Yard.Note("checkpoint-resume", "back at the last door, pickups kept");
    }

    // A stamp's shove: Wallit slides `distance` metres along `dir` over a quarter second. No damage, no shake.
    public void Shove(Vector3 dir, float distance) {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) {
            dir = -Player.forward;
        }
        shoveDir = dir.normalized;
        shoveLeft = distance;
        Shoves++;
    }

    public void Finish() {
        if (Completed) {
            return;
        }
        Completed = true;
        GameAudio.Play("stamp", 0.7f);
        CampaignSave.Complete(Chapter);
        RunAnalytics.Screen("level_complete_" + (Chapter + 1));
    }

    public void LeaveToHub() {
        if (Leaving) {
            return;
        }
        Leaving = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ChapterScene.HubNext = true;
        SceneManager.LoadScene(CampaignSave.Scene);
    }

    void Update() {
        Seconds += Time.deltaTime;
        if (Player == null || Leaving) {
            return;
        }
        Vector3 moved = Player.position - lastPos;
        moved.y = 0f;
        if (moved.magnitude < 3f) {
            Walked += moved.magnitude;
        }
        lastPos = Player.position;
        if (Mathf.Abs(PlayInput.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(PlayInput.GetAxis("Vertical")) > 0.1f || PlayInput.GetMouseButton(0) || PlayInput.GetKey(KeyCode.E)) {
            lastInputAt = Time.time;
        }
        if (shoveLeft > 0f && body != null && body.enabled) {
            float step = Mathf.Min(shoveLeft, 13f * Time.deltaTime);
            body.Move(shoveDir * step);
            shoveLeft -= step;
        }
        if (respawnAt >= 0f && Time.time >= respawnAt) {
            Resume();
        }
        Hud.Health.text = "HP [" + new string('|', Health) + new string('.', MaxHealth - Health) + "]";

        // Esc twice leaves for the hub: the level keeps nothing mid-way, so it asks first.
        if (PlayInput.GetKeyDown(KeyCode.Escape) && !Completed && !PauseMenu.IsOpen && Time.frameCount != PauseMenu.ClosedFrame && Time.frameCount != SettingsPanel.ClosedFrame) {
            if (Time.time - escAt < 2.5f) {
                LeaveToHub();
            } else {
                escAt = Time.time;
                Hud.ShowReceipt((UiFit.Touch ? "Tap Hub again to go back to the hub." : "Esc again to go back to the hub."), 2.5f);
            }
        }
    }
}
