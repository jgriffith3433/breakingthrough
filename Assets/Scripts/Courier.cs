using System.Collections.Generic;
using UnityEngine;

// The Courier, Chapter 1's Alpha Yo Bot (Docs/CampaignBible.md): a Yo Bot with a clipboard and a notice of striking.
// It walks to NoticeRange of Slice, stops, and raises the notice. A red mark fills on the circle's ground: that is the
// telegraph. When it fills the notice lands on Slice for NoticeDamage, with two answers: break the wind-up (hit it for
// InterruptShare of its health before the mark fills and it drops the notice and stands dazed), or take the notice
// yourself by standing in the mark (Wallit takes the stamp and Slice is spared). Under half health it winds up
// faster, and calls two Yo Bots with every notice. Enemy.IsCourier keeps it from swiping and Enemy.Hold keeps it still.
public class Courier : MonoBehaviour {
    public const float NoticeRange = 7.5f;
    public const float MarkRadius = 3.4f;
    public const int NoticeDamage = 12;
    public const float InterruptShare = 0.1f;
    const float WindFirst = 2.2f;
    const float WindAngry = 1.5f;
    const float RecoverSeconds = 3.2f;
    const float DazeSeconds = 2.6f;

    // What the fight did, for the playtest: "wind|<n>|<seconds>", "land|<n>|slice", "land|<n>|wallit",
    // "interrupt|<n>", "phase2", "call".
    public static readonly List<string> Events = new List<string>();
    public static int Winds;
    public static int Landed;
    public static int Interrupted;
    public static int Blocked;
    public static bool MarkUp { get; private set; }
    public static bool Angry { get; private set; }

    enum Step { Approach, Wind, Recover }

    Enemy enemy;
    Step step = Step.Approach;
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

    void Awake() {
        enemy = GetComponent<Enemy>();
        Events.Clear();
        Winds = 0;
        Landed = 0;
        Interrupted = 0;
        Blocked = 0;
        Angry = false;
        MarkUp = false;
        BuildClipboard();
    }

    void OnDestroy() {
        DropMark();
    }

    // The notice: a pale board on a dark clip held in front of the chest. Built in code, no collider.
    void BuildClipboard() {
        var shader = SafeShader.Solid();
        var holder = new GameObject("Clipboard");
        holder.transform.SetParent(transform, false);
        clipRest = new Vector3(0.42f, 0.55f, 0.55f);
        holder.transform.localPosition = clipRest;
        holder.transform.localRotation = Quaternion.Euler(-18f, 0f, 6f);
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        Destroy(board.GetComponent<Collider>());
        board.transform.SetParent(holder.transform, false);
        board.transform.localScale = new Vector3(0.46f, 0.62f, 0.04f);
        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Notice";
        Destroy(paper.GetComponent<Collider>());
        paper.transform.SetParent(holder.transform, false);
        paper.transform.localPosition = new Vector3(0f, -0.02f, 0.03f);
        paper.transform.localScale = new Vector3(0.38f, 0.5f, 0.02f);
        if (shader != null) {
            board.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.18f, 0.16f, 0.15f, 1f) };
            paper.GetComponent<Renderer>().material = new Material(shader) { color = new Color(0.96f, 0.93f, 0.82f, 1f) };
        } else {
            UpgradeLook.Paint(board, new Color(0.18f, 0.16f, 0.15f));
            UpgradeLook.Paint(paper, new Color(0.96f, 0.93f, 0.82f));
        }
        foreach (Renderer r in holder.GetComponentsInChildren<Renderer>()) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        clipboard = holder.transform;
    }

    void Update() {
        if (enemy == null || !enemy.Alive) {
            DropMark();
            if (clipboard != null) {
                clipboard.gameObject.SetActive(false);
            }
            return;
        }
        Musician slice = Musician.Instance;
        if (slice == null || !slice.Alive || PauseMenu.IsOpen || Cutscene.IsOpen || LevelUpScreen.IsOpen) {
            return;
        }
        float dt = Time.deltaTime;
        if (!Angry && enemy.Health01 <= 0.5f) {
            Angry = true;
            enemy.EnterPhase2(new Color(1.45f, 0.75f, 0.3f));
            Events.Add("phase2");
            BossPhases.Announce(enemy, "The Courier speeds up and calls Yo Bots with every notice");
            Fx.Ring(transform.position, 7f, new Color(1f, 0.3f, 0.22f, 0.9f), 0.6f);
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

    void Approach(Musician slice) {
        Lower(Time.deltaTime);
        Vector3 to = slice.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude <= NoticeRange) {
            BeginWind(slice);
        }
    }

    void BeginWind(Musician slice) {
        step = Step.Wind;
        clock = 0f;
        windTime = Angry ? WindAngry : WindFirst;
        healthAtWind = enemy.Health01;
        Winds++;
        Events.Add("wind|" + Winds + "|" + windTime.ToString("0.0"));
        MakeMark(slice.transform.position);
        enemy.Hold(windTime + 0.3f);
        nextRing = 0f;
        GameAudio.Play("stomp", 0.7f, 1.4f);
        if (!toldTip) {
            toldTip = true;
            UpgradeLook.Say("Slice: \"Break the wind-up. Or stand in it.\"", 3.4f, 230f);
        }
        if (Angry && WaveDirector.Instance != null) {
            WaveDirector.Instance.BossCall(transform.position, 2);
            Events.Add("call");
        }
    }

    void Wind(Musician slice, float dt) {
        clock += dt;
        enemy.Hold(windTime - clock + 0.3f);
        Vector3 to = slice.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), dt * 6f);
        }
        float t = Mathf.Clamp01(clock / windTime);
        Raise(t);
        PaintMark(t);
        if (Time.time >= nextRing) {
            nextRing = Time.time + 0.55f;
            Fx.Ring(MarkAt(slice), MarkRadius * 2f * (0.5f + 0.5f * t), new Color(1f, 0.25f, 0.18f, 0.55f + 0.4f * t), 0.5f);
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
        enemy.Hold(DazeSeconds);
        Fx.Ring(transform.position, 6f, new Color(1f, 1f, 1f, 0.8f), 0.5f);
        Fx.Emit(FxKind.Flash, transform.position + Vector3.up * 1.2f, 1, new Color(1f, 0.95f, 0.8f, 0.7f), 2.2f, 0f);
        GameAudio.Play("batcrack", 0.8f, 0.8f);
        UpgradeLook.Say("Notice dropped!", 1.8f, 160f);
    }

    void Land(Musician slice) {
        Vector3 at = MarkAt(slice);
        bool wallitIn = false;
        PlayerGrowth player = PlayerGrowth.Instance;
        if (player != null) {
            Vector3 d = player.transform.position - at;
            d.y = 0f;
            wallitIn = d.magnitude <= MarkRadius;
        }
        Fx.Ring(at, MarkRadius * 2.4f, new Color(1f, 0.35f, 0.2f, 0.9f), 0.5f);
        Fx.Emit(FxKind.Dust, at + Vector3.up * 0.3f, 14, new Color(0.6f, 0.55f, 0.48f, 0.6f), 1.4f, 2.4f, 2f);
        GameAudio.Play("stamp", 0.9f, 0.9f);
        if (wallitIn) {
            Blocked++;
            Events.Add("land|" + Winds + "|wallit");
            player.OnHit(enemy);
            UpgradeLook.Say("Wallit takes the stamp", 1.8f, 160f);
        } else {
            Landed++;
            Events.Add("land|" + Winds + "|slice");
            slice.Hurt(NoticeDamage, enemy);
        }
        DropMark();
        step = Step.Recover;
        clock = Angry ? RecoverSeconds * 0.7f : RecoverSeconds;
        enemy.Hold(clock);
    }

    // The mark sits on the circle's ground under Slice.
    static Vector3 MarkAt(Musician slice) {
        Vector3 p = slice.transform.position;
        if (Physics.Raycast(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, out RaycastHit hit, 20f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            p.y = hit.point.y;
        }
        return p;
    }

    void MakeMark(Vector3 sliceAt) {
        DropMark();
        var shader = SafeShader.Find("Sprites/Default");
        mark = new GameObject("Notice Mark");
        Vector3 at = sliceAt;
        if (Musician.Instance != null) {
            at = MarkAt(Musician.Instance);
        }
        mark.transform.position = at;
        fill = null;
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Mark";
        Destroy(rim.GetComponent<Collider>());
        rim.transform.SetParent(mark.transform, false);
        rim.transform.localScale = new Vector3(MarkRadius * 2f, 0.02f, MarkRadius * 2f);
        rim.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        inner.name = "Mark Fill";
        Destroy(inner.GetComponent<Collider>());
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
