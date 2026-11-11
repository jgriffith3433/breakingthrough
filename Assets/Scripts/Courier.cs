using System.Collections.Generic;
using UnityEngine;

// The Courier, Chapter 1's Alpha Yo Bot (Docs/CampaignBible.md): a Yo Bot with a clipboard and a notice of striking.
// It walks to NoticeRange of whoever the notice is for, stops, and raises the notice. A red mark fills on the ground under
// them: that is the telegraph, with a ring pulsing over it. When it fills the notice lands, with two answers: break the wind-up
// (hit it for InterruptShare of its health before the mark fills and it drops the notice and stands dazed), or take the notice
// yourself by standing in the mark (Wallit takes the stamp and Slice is spared). Under half health it winds up faster, and
// calls two Yo Bots with every notice. Enemy.IsCourier keeps it from swiping and Enemy.Hold keeps it still.
// Two homes. In the Hollow (ChapterRun) the notice is for Slice at the circle (Musician) and lands for NoticeDamage. In
// Chapter 1's level (LevelYard, no circle) it is for Slice walking with Wallit (Companion) while she stands, and lands by
// knocking her down; while she is down or not there it is for Wallit himself, and he steps out of it or breaks it.
public class Courier : MonoBehaviour {
    public const float NoticeRange = 7.5f;
    public const float MarkRadius = 3.4f;
    public const int NoticeDamage = 12;
    public const float InterruptShare = 0.1f;
    const float WindFirst = 2.2f;
    const float WindOpening = 3f;    // a level's first notice reads slower, so a thumb on a phone has time to answer it
    const float WindAngry = 1.5f;
    const float RecoverSeconds = 3.2f;
    const float DazeSeconds = 2.6f;

    // What the fight did, for the playtest: "wind|<n>|<seconds>|<for>", "land|<n>|slice", "land|<n>|wallit",
    // "dodged|<n>", "interrupt|<n>", "phase2", "call".
    public static readonly List<string> Events = new List<string>();
    public static int Winds;
    public static int Landed;
    public static int Interrupted;
    public static int Blocked;
    public static int Dodged;
    public static bool MarkUp { get; private set; }
    public static bool Angry { get; private set; }

    enum Step { Approach, Wind, Recover }
    enum Aim { Circle, Slice, Wallit }   // Slice at the circle (Musician); Slice walking with him (Companion); Wallit

    Enemy enemy;
    Step step = Step.Approach;
    Aim aim;
    Vector3 markAt;
    float clock;
    float windTime;
    float healthAtWind;
    float nextRing;
    GameObject mark;
    Material markMaterial;
    GameObject fill;
    Material fillMaterial;
    Transform clipboard;
    Vector3 clipRest;
    bool toldTip;
    bool toldTaken;
    bool toldBroken;

    // A campaign level: no circle, so the notice is for the people walking it.
    static bool InLevel { get { return Musician.Instance == null && LevelScene.Instance != null; } }

    void Awake() {
        enemy = GetComponent<Enemy>();
        Events.Clear();
        Winds = 0;
        Landed = 0;
        Interrupted = 0;
        Blocked = 0;
        Dodged = 0;
        Angry = false;
        MarkUp = false;
        BuildClipboard();
    }

    void OnDestroy() {
        DropMark();
    }

    // The notice: a pale board on a dark clip held in front of the chest. Built in code, no collider.
    void BuildClipboard() {
        GameObject holder = CharacterKit.Prop("clipboard");
        holder.name = "Clipboard";
        holder.transform.SetParent(transform, false);
        clipRest = new Vector3(0.32f, 0.5f, 0.36f);
        holder.transform.localScale = Vector3.one * 0.8f;
        holder.transform.localPosition = clipRest;
        holder.transform.localRotation = Quaternion.Euler(-18f, 0f, 6f);
        clipboard = holder.transform;
    }

    // The level puts it back at the drop, asleep, after Wallit goes down: no notice up, the next one starts from the walk.
    public void Rest() {
        DropMark();
        if (enemy != null) {
            enemy.EndTell(false);
        }
        step = Step.Approach;
        clock = 0f;
    }

    void Update() {
        if (enemy == null || !enemy.Alive) {
            DropMark();
            if (clipboard != null) {
                clipboard.gameObject.SetActive(false);
            }
            return;
        }
        if (PauseMenu.IsOpen || Cutscene.IsOpen || LevelUpScreen.IsOpen) {
            return;
        }
        float dt = Time.deltaTime;
        if (enemy.Dormant) {
            Lower(dt);
            return;
        }
        Musician slice = Musician.Instance;
        if (InLevel ? PlayerGrowth.Instance == null : (slice == null || !slice.Alive)) {
            return;
        }
        if (!Angry && enemy.Health01 <= 0.5f) {
            Angry = true;
            enemy.EnterPhase2(new Color(1.45f, 0.75f, 0.3f));
            enemy.Stagger(1.2f);   // phase two arrives on the body (after the tell, if one is running)
            Events.Add("phase2");
            BossPhases.Announce(enemy, "faster notices, it calls the crew");
            Fx.Ring(transform.position, 7f, new Color(1f, 0.3f, 0.22f, 0.9f), 0.6f);
            GameAudio.PlayAt("stamp_heavy", transform.position, 0.7f, 1.1f);
            Line(Story.CourierSecond, "courier-second");
        }
        switch (step) {
            case Step.Approach:
                Approach(slice);
                break;
            case Step.Wind:
                Wind(slice, dt);
                break;
            case Step.Recover:
                clock -= dt;
                Lower(dt);
                if (clock <= 0f) {
                    step = Step.Approach;
                }
                break;
        }
    }

    // In the Hollow: Slice at the circle once she is in range. In a level: Slice while she stands near enough to be read to
    // (Wallit is usually reached first, so she counts a few metres further out), else Wallit once he is in range.
    void Approach(Musician slice) {
        Lower(Time.deltaTime);
        if (!InLevel) {
            if (Flat(slice.transform.position - transform.position).magnitude <= NoticeRange) {
                BeginWind(Aim.Circle, Ground(slice.transform.position));
            }
            return;
        }
        PlayerGrowth player = PlayerGrowth.Instance;
        Companion c = Companion.Instance;
        float toWallit = Flat(player.transform.position - transform.position).magnitude;
        float toSlice = c != null && c.Alive ? Flat(c.transform.position - transform.position).magnitude : float.MaxValue;
        if (toSlice <= NoticeRange || (toWallit <= NoticeRange && toSlice <= NoticeRange + 4f)) {
            BeginWind(Aim.Slice, Ground(c.transform.position));
        } else if (toWallit <= NoticeRange) {
            BeginWind(Aim.Wallit, Ground(player.transform.position));
        }
    }

    void BeginWind(Aim who, Vector3 at) {
        step = Step.Wind;
        aim = who;
        markAt = at;
        clock = 0f;
        windTime = Angry ? WindAngry : (InLevel && Winds == 0 ? WindOpening : WindFirst);
        healthAtWind = enemy.Health01;
        Winds++;
        Events.Add("wind|" + Winds + "|" + windTime.ToString("0.0") + "|" + (who == Aim.Wallit ? "wallit" : "slice"));
        MakeMark(at);
        enemy.Hold(windTime + 0.3f);
        enemy.BeginTell();   // the clipboard goes up and the body rears back until the notice lands
        nextRing = 0f;
        GameAudio.PlayAt("clipboard_raise", transform.position, 0.8f);
        GameAudio.PlayAt("stomp", transform.position, 0.7f, 1.4f);
        GameAudio.PlayAt("paper", transform.position, 0.6f, 1.1f);
        if (!toldTip) {
            toldTip = true;
            // Slice names it (a banked clip); the screen only names the verbs.
            if (!InLevel || (Companion.Instance != null && Companion.Instance.Alive)) {
                Line(Story.CourierTip, "courier-tip");
            }
            UpgradeLook.Say(who == Aim.Wallit ? "Break it, or step out" : "Break it, or stand in it", 3f, 160f);
        }
        if (Angry) {
            if (WaveDirector.Instance != null) {
                WaveDirector.Instance.BossCall(transform.position, 2);
                Events.Add("call");
            } else if (LevelScene.Instance != null && LevelScene.Instance.Yard != null && LevelScene.Instance.Yard.CourierCall(transform.position, 2) > 0) {
                Events.Add("call");
            }
        }
    }

    void Wind(Musician slice, float dt) {
        clock += dt;
        enemy.Hold(windTime - clock + 0.3f);
        Vector3 to = markAt - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), dt * 6f);
        }
        float t = Mathf.Clamp01(clock / windTime);
        Raise(t);
        PaintMark(t);
        if (Time.time >= nextRing) {
            nextRing = Time.time + 0.55f;
            Fx.Ring(markAt, MarkRadius * 2f * (0.5f + 0.5f * t), new Color(1f, 0.25f, 0.18f, 0.55f + 0.4f * t), 0.5f);
        }
        if (healthAtWind - enemy.Health01 >= InterruptShare) {
            Interrupt();
            return;
        }
        if (clock >= windTime) {
            Land(slice);
        }
    }

    // Hit hard enough during the wind-up: the notice drops and the Courier stands dazed, open to the bat.
    void Interrupt() {
        Interrupted++;
        Events.Add("interrupt|" + Interrupted);
        DropMark();
        step = Step.Recover;
        clock = DazeSeconds;
        enemy.EndTell(false);
        enemy.Stagger(DazeSeconds * 0.6f);
        enemy.Hold(DazeSeconds);
        Fx.Ring(transform.position, 6f, new Color(1f, 1f, 1f, 0.8f), 0.5f);
        Fx.Emit(FxKind.Flash, transform.position + Vector3.up * 1.2f, 1, new Color(1f, 0.95f, 0.8f, 0.7f), 2.2f, 0f);
        GameAudio.PlayAt("batcrack", transform.position, 0.8f, 0.8f);
        GameAudio.PlayAt("paper_slide", transform.position, 0.6f, 0.9f);
        UpgradeLook.Say("Notice dropped!", 1.8f, 160f);
        if (!toldBroken && InLevel) {
            toldBroken = Line(Story.CourierBroken, "courier-broken");
        }
    }

    void Land(Musician slice) {
        GameAudio.PlayAt("paper_flutter", transform.position, 0.8f);
        Vector3 at = markAt;
        bool wallitIn = false;
        PlayerGrowth player = PlayerGrowth.Instance;
        if (player != null) {
            wallitIn = Flat(player.transform.position - at).magnitude <= MarkRadius;
        }
        Fx.Ring(at, MarkRadius * 2.4f, new Color(1f, 0.35f, 0.2f, 0.9f), 0.5f);
        Fx.Emit(FxKind.Dust, at + Vector3.up * 0.3f, 14, new Color(0.6f, 0.55f, 0.48f, 0.6f), 1.4f, 2.4f, 2f);
        GameAudio.PlayAt("stamp", transform.position, 0.9f, 0.9f);
        switch (aim) {
            case Aim.Circle:
                if (wallitIn) {
                    TakenByWallit(player);
                } else {
                    Landed++;
                    Events.Add("land|" + Winds + "|slice");
                    slice.Hurt(NoticeDamage, enemy);
                }
                break;
            case Aim.Slice:
                Companion c = Companion.Instance;
                if (wallitIn) {
                    TakenByWallit(player);
                } else if (c != null && c.Alive && Flat(c.transform.position - at).magnitude <= MarkRadius + 0.5f) {
                    Landed++;
                    Events.Add("land|" + Winds + "|slice");
                    c.KnockDown();   // she goes down with her own line; Wallit revives her with a held E
                } else {
                    Missed();
                }
                break;
            default:
                if (wallitIn) {
                    Landed++;
                    Events.Add("land|" + Winds + "|wallit");
                    player.OnHit(enemy);
                } else {
                    Missed();
                }
                break;
        }
        DropMark();
        enemy.EndTell(true);
        step = Step.Recover;
        clock = Angry ? RecoverSeconds * 0.7f : RecoverSeconds;
        enemy.Hold(clock);
    }

    // Wallit stood in the red: the notice lands on him and Slice is spared.
    void TakenByWallit(PlayerGrowth player) {
        Blocked++;
        Events.Add("land|" + Winds + "|wallit");
        player.OnHit(enemy);
        UpgradeLook.Say("Wallit takes the stamp", 1.8f, 160f);
        if (!toldTaken && InLevel) {
            toldTaken = Line(Story.CourierTaken, "courier-taken");
        }
    }

    void Missed() {
        Dodged++;
        Events.Add("dodged|" + Winds);
        UpgradeLook.Say("Missed", 1.4f, 160f);
    }

    // A line in its speaker's voice. In a level it goes through the bark gate (logged, never over another line, skipped if the
    // floor is busy); in the Hollow it is captioned and queued for the voice.
    static bool Line(string raw, string why) {
        if (string.IsNullOrEmpty(raw) || raw.Length < 3) {
            return false;
        }
        if (LevelScene.Instance != null) {
            return !LevelBarks.Busy && LevelBarks.Say(raw[0], raw.Substring(2), why, true);
        }
        ChapterRun.Say(raw, 3.4f);
        return true;
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    // The ground under a point (the mark lies on it).
    static Vector3 Ground(Vector3 p) {
        if (Physics.Raycast(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, out RaycastHit hit, 20f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            p.y = hit.point.y;
        }
        return p;
    }

    void MakeMark(Vector3 at) {
        DropMark();
        var shader = SafeShader.Find("Sprites/Default");
        mark = new GameObject("Notice Mark");
        mark.transform.position = at;
        fill = null;
        var rim = KitMesh.Disc("Mark");
        rim.transform.SetParent(mark.transform, false);
        rim.transform.localScale = new Vector3(MarkRadius * 2f, 0.02f, MarkRadius * 2f);
        rim.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        var inner = KitMesh.Disc("Mark Fill");
        inner.transform.SetParent(mark.transform, false);
        inner.transform.localScale = new Vector3(0.1f, 0.025f, 0.1f);
        inner.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        fill = inner;
        if (shader != null) {
            markMaterial = new Material(shader) { color = new Color(1f, 0.2f, 0.15f, 0.22f) };
            fillMaterial = new Material(shader) { color = new Color(1f, 0.3f, 0.2f, 0.4f) };
            rim.GetComponent<Renderer>().material = markMaterial;
            inner.GetComponent<Renderer>().material = fillMaterial;
        }
        foreach (Renderer r in mark.GetComponentsInChildren<Renderer>()) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        MarkUp = true;
    }

    // The red fill grows to the rim as the wind-up runs out, and the whole mark deepens.
    void PaintMark(float t) {
        if (mark == null || fill == null) {
            return;
        }
        float r = MarkRadius * 2f * Mathf.Lerp(0.08f, 1f, t);
        fill.transform.localScale = new Vector3(r, 0.025f, r);
        if (markMaterial != null) {
            markMaterial.color = new Color(1f, 0.2f, 0.15f, 0.18f + 0.18f * Mathf.PingPong(Time.time * 3f, 1f));
        }
        if (fillMaterial != null) {
            fillMaterial.color = new Color(1f, 0.3f, 0.2f, 0.3f + 0.35f * t);
        }
    }

    void DropMark() {
        MarkUp = false;
        if (mark != null) {
            Destroy(mark);
            mark = null;
        }
    }

    // The clipboard lifts overhead as the wind-up runs, then comes back down.
    void Raise(float t) {
        if (clipboard == null) {
            return;
        }
        clipboard.localPosition = Vector3.Lerp(clipRest, clipRest + new Vector3(-0.2f, 0.9f, -0.15f), t);
        clipboard.localRotation = Quaternion.Euler(Mathf.Lerp(-18f, -110f, t), 0f, Mathf.Lerp(6f, 0f, t));
    }

    void Lower(float dt) {
        if (clipboard == null) {
            return;
        }
        clipboard.localPosition = Vector3.Lerp(clipboard.localPosition, clipRest, dt * 6f);
        clipboard.localRotation = Quaternion.Slerp(clipboard.localRotation, Quaternion.Euler(-18f, 0f, 6f), dt * 6f);
    }
}
