using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Arcade's boss waves (10, 20, 30 ...) as set pieces: a short skippable intro (the lights drop, the camera dollies out to
// the boss and back, Wallit and Slice trade a line through the "cutscene" voice key), a second phase on every boss (the
// red wash and banner; BossPhases for the Plated Alpha and the Foreman, Phases here for the rest) and a payoff on defeat
// (hit-stop, half a second of slow motion, a burst of bolts, Slice's line). Arcade only: chapters never reach it.
public class BossSetPiece : MonoBehaviour {
    public const float IntroSeconds = 4f;
    const float Dim = 0.38f;

    // "intro|<title>|<wave>", "skip|<title>", "intro-end|<title>", "phase2|<title>", "payoff|<title>|<wave>", "order-drop|<wave>", "slice-crack|10".
    public static readonly List<string> Events = new List<string>();
    public static bool Active { get; private set; }
    // True while the defeat payoff coroutine runs: WaveDirector holds the wave clear / act banner until it ends.
    public static bool PayingOff { get; private set; }
    public static int Intros, Skips, Payoffs;
    public static float LastSlowSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Events.Clear();
        Active = false;
        PayingOff = false;
        Intros = Skips = Payoffs = 0;
        LastSlowSeconds = 0f;
    }

    // Reused from existing Voice/ clips: wave beats from Story.md (first hurt / order aloud / order carried).
    static readonly string[][] WallitLines = {
        new[] { "You're bleeding, Slice.", "I'm covering you. Tab's open." }, // wave 10 plating
        new[] { "Then I'm reading mine. Pay up.", "Big bill. Big payday." },  // wave 20 order
        new[] { "Then it carries my bill.", "Last stretch. Pay up." },         // wave 30 The Order
    };
    static readonly string[][] SliceLines = {
        new[] { "Tch. Plated.", "Behind you." },
        new[] { "It's reading it out.", "Closer." },
        new[] { "Take them.", "Closer." },
    };

    Enemy boss;
    Transform cam;
    Vector3 orig;
    Quaternion origRot;
    bool applied;
    float t;
    bool skipped;
    Image dark;
    int line;
    float nextLine;
    string[] wallit, slice;
    int waveNum;
    bool cracked;

    public static void Begin(Enemy boss, int wave) {
        if (boss == null || ChapterRun.Instance != null || Camera.main == null) {
            return;
        }
        // Waves 10, 20, 30: the boss arrives on the CinematicDirector (CinematicBoss.cs). Anything else keeps the dolly below.
        CinematicDirector.BossResult entrance = CinematicDirector.PlayBoss(boss, -1, wave, () => {
            Active = false;
            Events.Add("intro-end|" + boss.Title);
            AfterEntrance(wave);
        });
        if (entrance != CinematicDirector.BossResult.Unavailable) {
            if (entrance == CinematicDirector.BossResult.Played) {
                Intros++;
                Active = true;
                Events.Add("intro|" + boss.Title + "|" + wave);
            }
            if (boss.GetComponent<BossPhases>() == null) {
                boss.gameObject.AddComponent<Phases>();
            }
            EnsureActThree(wave);
            return;
        }
        var go = new GameObject("Boss Intro");
        go.AddComponent<BossSetPiece>().Init(boss, wave);
        if (boss.GetComponent<BossPhases>() == null) {
            boss.gameObject.AddComponent<Phases>();
        }
        // Act three (waves 20-29): Yo Bots keep reading the order aloud after the Foreman arrives.
        EnsureActThree(wave);
    }

    static void EnsureActThree(int wave) {
        if (wave < 20 || wave >= 30 || Object.FindAnyObjectByType<ActThreeChorus>() != null) {
            return;
        }
        new GameObject("Act Three Chorus").AddComponent<ActThreeChorus>();
    }

    void Init(Enemy foe, int wave) {
        boss = foe;
        waveNum = wave;
        cracked = false;
        cam = Camera.main.transform;
        int pick = wave == 30 ? 2 : wave == 20 ? 1 : 0;
        wallit = WallitLines[pick];
        slice = SliceLines[pick];
        Events.Add("intro|" + boss.Title + "|" + wave);
        Intros++;
        Active = true;
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        boss.Hold(IntroSeconds + 0.5f);

        var canvas = new GameObject("Boss Dim").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvas.transform.SetParent(transform, false);
        dark = new GameObject("Dim").AddComponent<Image>();
        dark.transform.SetParent(canvas.transform, false);
        dark.raycastTarget = false;
        dark.color = new Color(0.02f, 0f, 0.05f, 0f);
        dark.rectTransform.anchorMin = Vector2.zero;
        dark.rectTransform.anchorMax = Vector2.one;
        dark.rectTransform.offsetMin = dark.rectTransform.offsetMax = Vector2.zero;
        if (wave == 10) {
            // Covering lines wait until Slice's visible crack (Update at 1.1 s), so the debt beat follows the wound.
            nextLine = 1.35f;
            line = 0;
        } else {
            Say();
        }
        if (wave == 20) {
            // One order bark under the intro so act three is heard as soon as the Foreman lands.
            CastVoice.SayLater("Y", "Order, read aloud: STRIKE. THIS. ADDRESS.", "cutscene");
            GameAudio.Play("stamp", 0.85f, 0.55f);
        }
    }

    void Say() {
        float seconds = line % 2 == 0 ? WallitVoice.Cutscene(wallit[line / 2 % wallit.Length]) : SliceVoice.Cutscene(slice[line / 2 % slice.Length]);
        string text = line % 2 == 0 ? "Wallit: " + wallit[line / 2 % wallit.Length] : "Slice: " + slice[line / 2 % slice.Length];
        UpgradeLook.Say(text, Mathf.Min(seconds, 1.8f), 300f);
        nextLine = t + 1.9f;
        line++;
    }

    // Wallit and Slice answer the boss's taunt once it has been said (the first pair of the wave's lines).
    static void AfterEntrance(int wave) {
        var go = new GameObject("Boss Afterword");
        go.AddComponent<Afterword>().Go(wave == 30 ? 2 : wave == 20 ? 1 : 0);
    }

    class Afterword : MonoBehaviour {
        public void Go(int pick) { StartCoroutine(Run(pick)); }

        IEnumerator Run(int pick) {
            yield return new WaitForSecondsRealtime(0.3f);
            string w = WallitLines[pick][0];
            float seconds = WallitVoice.Cutscene(w);
            UpgradeLook.Say("Wallit: " + w, Mathf.Min(seconds, 1.8f), 300f);
            yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds + 0.3f, 1.2f, 2.2f));
            string s = SliceLines[pick][0];
            seconds = SliceVoice.Cutscene(s);
            UpgradeLook.Say("Slice: " + s, Mathf.Min(seconds, 1.8f), 300f);
            yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds + 0.3f, 1.2f, 2.2f));
            Destroy(gameObject);
        }
    }

    // Story act two: Slice takes the plating's first crack, on camera (wave 10). Shared by the old dolly and the entrance.
    public static void CrackSlice(Enemy boss) {
        if (Musician.Instance == null || !Musician.Instance.Alive) {
            return;
        }
        Musician.Instance.Hurt(1, boss);
        Events.Add("slice-crack|10");
        Fx.Ring(Musician.Instance.transform.position + Vector3.up * 0.4f, 3.5f, new Color(1f, 0.35f, 0.3f, 0.7f), 0.45f);
    }

    static bool SkipPressed() {
        if (Input.anyKeyDown || PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.Mouse0)) {
            return true;
        }
        for (int i = 0; i < Input.touchCount; i++) {
            if (Input.GetTouch(i).phase == TouchPhase.Began) {
                return true;
            }
        }
        return false;
    }

    // Undo last frame's camera pose before the follow camera reads its own position.
    [DefaultExecutionOrder(-1000)]
    class Restore : MonoBehaviour {
        public BossSetPiece owner;
        void Update() { owner.Undo(); }
    }

    void Undo() {
        if (applied && cam != null) {
            cam.position = orig;
            cam.rotation = origRot;
        }
        applied = false;
    }

    void Start() {
        gameObject.AddComponent<Restore>().owner = this;
    }

    void Update() {
        if (PauseMenu.IsOpen) {
            return;
        }
        t += Time.unscaledDeltaTime;
        if (!skipped && t > 0.35f && SkipPressed()) {
            skipped = true;
            Skips++;
            Events.Add("skip|" + (boss != null ? boss.Title : "boss"));
            WallitVoice.Silence();
            SliceVoice.Silence();
            t = Mathf.Max(t, IntroSeconds - 0.5f);
        }
        // Wave 10: Slice takes the plating's first crack on camera (Story act two), then the covering lines play.
        if (!cracked && waveNum == 10 && t >= 1.1f && Musician.Instance != null && Musician.Instance.Alive) {
            cracked = true;
            CrackSlice(boss);
        }
        if (!skipped && t >= nextLine && line < 4 && t < IntroSeconds - 0.8f) {
            Say();
        }
        float w = Weight();
        if (dark != null) {
            dark.color = new Color(0.02f, 0f, 0.05f, Dim * w);
        }
        if (t >= IntroSeconds || boss == null) {
            Finish();
        }
    }

    float Weight() {
        float inW = Mathf.SmoothStep(0f, 1f, t / 0.9f);
        float outW = Mathf.SmoothStep(0f, 1f, (IntroSeconds - t) / 0.9f);
        return Mathf.Min(inW, outW);
    }

    void LateUpdate() {
        float w = Weight();
        if (boss == null || cam == null || w <= 0.001f || Time.timeScale < 0.01f) {
            return;
        }
        orig = cam.position;
        origRot = cam.rotation;
        Vector3 b = boss.transform.position;
        Vector3 toCam = cam.position - b;
        toCam.y = 0f;
        Vector3 dir = toCam.sqrMagnitude > 0.01f ? toCam.normalized : Vector3.back;
        // Orbit slowly while it holds; always a clear height above the open field, never nearer than 9 m.
        dir = Quaternion.Euler(0f, Mathf.Lerp(-25f, 15f, t / IntroSeconds), 0f) * dir;
        float reach = 9f + boss.BodyScale * 3f;
        Vector3 want = b + dir * reach;
        want.y = GroundY(want) + 2.8f + boss.BodyScale;
        Vector3 look = b + Vector3.up * (1.2f * boss.BodyScale);
        Quaternion wantRot = Quaternion.LookRotation(look - want, Vector3.up);
        cam.position = Vector3.Lerp(orig, want, w);
        cam.rotation = Quaternion.Slerp(origRot, wantRot, w);
        applied = true;
    }

    static float GroundY(Vector3 at) {
        RaycastHit hit;
        if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }

    void Finish() {
        Undo();
        Active = false;
        VoiceDirector.Scene = false;
        Events.Add("intro-end|" + (boss != null ? boss.Title : "boss"));
        if (boss != null) {
            boss.Hold(0f);
        }
        Destroy(gameObject);
    }

    void OnDestroy() {
        Undo();
        Active = false;
        VoiceDirector.Scene = false;
    }

    // Second phase for the bosses BossPhases does not run (the Spider Bot Alphas and after): the tell is the shared red wash.
    class Phases : MonoBehaviour {
        Enemy enemy;
        void Start() {
            enemy = GetComponent<Enemy>();
            if (enemy != null && enemy.PhaseAt <= 0f) {
                enemy.PhaseAt = 0.5f;
            }
        }
        void Update() {
            if (enemy == null || !enemy.Alive || enemy.Phase >= 2 || enemy.Health01 > enemy.PhaseAt) {
                return;
            }
            enemy.EnterPhase2(new Color(1.5f, 0.5f, 0.35f));
            BossPhases.Announce(enemy, enemy.Title + " is angry.");
            BossVoice.OnPhase(enemy);
            Events.Add("phase2|" + enemy.Title);
            Fx.Ring(transform.position, enemy.BodyScale * 4.5f, new Color(1f, 0.2f, 0.12f, 0.9f), 0.8f);
            GameAudio.Play("stomp", 1f, 0.5f);
        }
    }

    // The payoff, started from Combat.OnKill's hook (Arcade only).
    public static void Defeated(Enemy foe, int wave) {
        if (foe == null || ChapterRun.Instance != null) {
            return;
        }
        Payoffs++;
        PayingOff = true;
        ArcadeActs.BossTheme = false;   // the act's score comes back up
        MusicDirector.Play("");
        Events.Add("payoff|" + foe.Title + "|" + wave);
        var go = new GameObject("Boss Payoff");
        go.AddComponent<PayoffRun>().Go(foe.transform.position, wave);
    }

    // Waves 20-29: Yo Bots read the order aloud (Story act three) instead of chirping victory.
    class ActThreeChorus : MonoBehaviour {
        float next;
        void Start() { next = Time.time + 5f; }
        void Update() {
            int wave = WaveDirector.Wave;
            if (wave < 20 || wave >= 30 || ChapterRun.Instance != null) {
                Destroy(gameObject);
                return;
            }
            if (PauseMenu.IsOpen || LevelUpScreen.IsOpen || Active || Cutscene.IsOpen || Time.timeScale < 0.05f) {
                return;
            }
            if (Time.time < next) {
                return;
            }
            next = Time.time + 10f + Random.Range(0f, 5f);
            string[] lines = {
                "Order received! Strike the address!",
                "Order, read aloud: STRIKE. THIS. ADDRESS.",
                "Strike the address. Strike the address.",
            };
            CastVoice.SayLater("Y", lines[Random.Range(0, lines.Length)], "bark");
            Events.Add("order-chorus|" + wave);
            GameAudio.Play("stamp", 0.55f, 0.75f);
        }
    }

    class PayoffRun : MonoBehaviour {
        public void Go(Vector3 at, int wave) { StartCoroutine(Run(at, wave)); }

        IEnumerator Run(Vector3 at, int wave) {
            Juice.HitStop(0.12f);
            yield return new WaitForSecondsRealtime(0.14f);
            Pickups.DropBolts(at + Vector3.up * 0.5f, 24);
            Fx.Ring(at + Vector3.up * 0.3f, 9f, new Color(1f, 0.85f, 0.4f, 0.9f), 0.7f);
            Fx.Emit(FxKind.Flash, at + Vector3.up * 1.4f, 1, new Color(1f, 0.9f, 0.6f, 0.8f), 4f, 0f);
            GameAudio.Play("homerun", 0.9f, 0.7f);
            if (wave == 30) {
                // The order drops on the grass; Wallit's DENIED read stays on the finale card (Story.Finale) so it is not said twice.
                GameObject scroll = CharacterKit.Prop("scroll");
                scroll.name = "Fallen Order";
                scroll.transform.position = at + Vector3.up * 0.12f;
                scroll.transform.rotation = Quaternion.Euler(8f, Random.Range(-40f, 40f), 90f);
                scroll.transform.localScale = Vector3.one * 1.1f;
                GameObject blot = KitMesh.Disc("Denied");
                blot.transform.SetParent(scroll.transform, false);
                blot.transform.localPosition = new Vector3(0.02f, 0.02f, 0.04f);
                blot.transform.localRotation = Quaternion.Euler(90f, 18f, 0f);
                blot.transform.localScale = new Vector3(0.32f, 0.02f, 0.32f);
                var shader = SafeShader.Find("Sprites/Default");
                if (shader != null) {
                    var mat = new Material(shader) { color = new Color(0.75f, 0.05f, 0.08f, 0.95f) };
                    var rend = blot.GetComponent<Renderer>();
                    if (rend != null) {
                        rend.sharedMaterial = mat;
                        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                Object.Destroy(scroll, 14f);
                GameAudio.Play("stamp", 1f, 0.5f);
                Events.Add("order-drop|" + wave);
                SliceVoice.Cutscene("Clean.");
                UpgradeLook.Say("Slice: Clean.", 1.8f, 300f);
            } else {
                string[] lines = { "Clean.", "Take them." };
                string said = lines[wave == 20 ? 1 : 0];
                SliceVoice.Cutscene(said);
                UpgradeLook.Say("Slice: " + said, 2.2f, 300f);
            }
            float slow = 0.5f;
            if (Time.timeScale > 0.99f && !PauseMenu.IsOpen && !LevelUpScreen.IsOpen && !StoryScreen.IsOpen) {
                Time.timeScale = 0.3f;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < slow && !StoryScreen.IsOpen) {
                    yield return null;
                }
                if (Time.timeScale < 0.99f && Time.timeScale > 0f && !StoryScreen.IsOpen) {
                    Time.timeScale = 1f;
                }
                LastSlowSeconds = slow;
            }
            PayingOff = false;
            Destroy(gameObject);
        }

        void OnDestroy() {
            PayingOff = false;
            if (Time.timeScale < 0.99f && Time.timeScale > 0.01f && !StoryScreen.IsOpen && !PauseMenu.IsOpen && !LevelUpScreen.IsOpen) {
                Time.timeScale = 1f;
            }
        }
    }
}
