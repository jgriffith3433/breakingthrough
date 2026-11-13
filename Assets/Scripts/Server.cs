using System.Collections.Generic;
using UnityEngine;

// The Server, Chapter 4's Alpha (Docs/CampaignBible.md): the first full Spider Bot. It does not strike. It walks to OfferRange
// of Slice, folds a front leg and holds out a paper on the tip of its claw, politely, and waits. The answer is to refuse it:
// land a blow on the Server (or the paper) while it is offered. It backs off, comes again, and offers again. Only when the
// paper has been refused three times does it strike: a long wind-up on the circle's mark, then the stab on Slice, and from there
// it is a plain Alpha (Enemy.Unleash). Until then its health cannot drop below Enemy.MinHealth01, so it cannot be killed
// before it has been refused. Enemy.IsCourier keeps it from swiping and summoning, Enemy.Hold keeps it still.
public class Server : MonoBehaviour {
    public const float OfferRange = 8f;
    public const int DefaultRefusals = 3;
    // Three refusals, or two when Wallit took the paper and read it (choice 4.c1): it has been offered the order once already.
    public static int RefusalsNeeded { get { return Choices.Is("4.c1", "take") ? 2 : DefaultRefusals; } }
    public const int StrikeDamage = 14;
    const float StrikeWind = 2.0f;
    const float BackOffSeconds = 2.4f;

    // What the fight did, for the playtest: "offer|<n>", "refuse|<n>", "strike-wind", "strike|slice", "unleashed".
    public static readonly List<string> Events = new List<string>();
    public static int Offers;
    public static int Refusals;
    public static bool Struck;
    public static bool Offering { get; private set; }

    enum Step { Approach, Offer, BackOff, Wind, Free }

    Enemy enemy;
    Step step = Step.Approach;
    float clock;
    int blowsAtOffer;
    Transform paper;
    Vector3 paperRest;
    float nextRing;

    void Awake() {
        enemy = GetComponent<Enemy>();
        Events.Clear();
        Offers = 0;
        Refusals = 0;
        Struck = false;
        Offering = false;
        BuildPaper();
    }

    void OnDestroy() {
        Offering = false;
    }

    // The paper: a pale sheet with two ruled lines, held on a thin arm in front of the body. Built in code, no collider.
    void BuildPaper() {
        GameObject holder = CharacterKit.Prop("paper");
        holder.name = "Paper";
        holder.transform.SetParent(transform, false);
        paperRest = new Vector3(0.25f, 0.55f, 0.5f);
        holder.transform.localPosition = paperRest;
        holder.transform.localRotation = Quaternion.Euler(-30f, 0f, 8f);
        paper = holder.transform;
    }

    void Update() {
        if (enemy == null || !enemy.Alive) {
            Offering = false;
            if (paper != null) {
                paper.gameObject.SetActive(false);
            }
            return;
        }
        Musician slice = Musician.Instance;
        if (slice == null || !slice.Alive || PauseMenu.IsOpen || Cutscene.IsOpen || LevelUpScreen.IsOpen) {
            return;
        }
        float dt = Time.deltaTime;
        Vector3 to = slice.transform.position - transform.position;
        to.y = 0f;
        switch (step) {
            case Step.Approach:
                Offering = false;
                Lower(dt);
                if (to.magnitude <= OfferRange) {
                    BeginOffer();
                }
                break;
            case Step.Offer:
                // It waits. It does not strike. A refusal is any blow while the paper is out.
                enemy.Hold(0.5f);
                Face(to, dt);
                Raise(dt, 1f);
                if (enemy.BlowsTaken > blowsAtOffer) {
                    Refuse();
                }
                break;
            case Step.BackOff:
                clock -= dt;
                Lower(dt);
                if (clock <= 0f) {
                    step = Refusals >= RefusalsNeeded ? Step.Wind : Step.Approach;
                    if (step == Step.Wind) {
                        BeginWind();
                    }
                }
                break;
            case Step.Wind:
                clock += dt;
                enemy.Hold(StrikeWind - clock + 0.3f);
                Face(to, dt);
                Raise(dt, 1.6f);
                if (Time.time >= nextRing) {
                    nextRing = Time.time + 0.5f;
                    Fx.Ring(slice.transform.position, 6f * (0.5f + 0.5f * clock / StrikeWind), new Color(1f, 0.25f, 0.18f, 0.6f), 0.5f);
                }
                if (clock >= StrikeWind) {
                    Strike(slice);
                }
                break;
        }
    }

    void BeginOffer() {
        step = Step.Offer;
        Offers++;
        blowsAtOffer = enemy.BlowsTaken;
        Offering = true;
        Events.Add("offer|" + Offers);
        enemy.Hold(0.5f);
        GameAudio.PlayAt("stamp", transform.position, 0.5f, 1.3f);
        GameAudio.PlayAt("paper_slide", transform.position, 0.6f);
        Fx.Ring(transform.position, 4f, new Color(0.96f, 0.93f, 0.82f, 0.8f), 0.6f);
        // The Server says it; the screen only names the verb.
        if (Offers == 1) {
            CastVoice.SayLater("V", "Delivery is to the named address.", "server-offer");
        }
        UpgradeLook.Say("Refuse the paper (" + Refusals + "/" + RefusalsNeeded + ")", 2.4f, 160f);
    }

    void Refuse() {
        Refusals++;
        Offering = false;
        Events.Add("refuse|" + Refusals);
        GameAudio.PlayAt("batcrack", transform.position, 0.8f, 0.8f);
        Fx.Ring(transform.position, 6f, new Color(1f, 1f, 1f, 0.8f), 0.5f);
        UpgradeLook.Say("Refused " + Refusals + "/" + RefusalsNeeded, 1.8f, 160f);
        CastVoice.SayLater("W", "Refused.", "server-refuse");
        step = Step.BackOff;
        enemy.Stagger(0.5f);   // a refusal rocks it back
        clock = BackOffSeconds;
        enemy.Hold(BackOffSeconds);
        Vector3 away = transform.position - (Musician.Instance != null ? Musician.Instance.transform.position : transform.position);
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f) {
            enemy.Knock(away.normalized * 9f, 0.2f);
        }
    }

    void BeginWind() {
        step = Step.Wind;
        clock = 0f;
        Events.Add("strike-wind");
        enemy.Hold(StrikeWind + 0.3f);
        enemy.BeginTell();   // the Server rears up on its front legs before it serves
        BotSound.TellIn(enemy, StrikeWind);
        GameAudio.PlayAt("stomp", transform.position, 0.8f, 0.7f);
        UpgradeLook.Say("The Server strikes!", 2.2f, 160f);
        if (RefusalsNeeded == DefaultRefusals) {
            CastVoice.SayLater("V", "Three knocks on record.", "server-strike");
        }
        GameAudio.PlayAt("knock_three", transform.position, 0.8f);
    }

    void Strike(Musician slice) {
        Struck = true;
        BotSound.Hit(enemy);
        enemy.EndTell(true);
        Events.Add("strike|slice");
        Fx.Ring(slice.transform.position, 8f, new Color(1f, 0.35f, 0.2f, 0.9f), 0.5f);
        GameAudio.PlayAt("stamp", transform.position, 0.9f, 0.8f);
        slice.Hurt(StrikeDamage, enemy);
        enemy.Unleash();
        Events.Add("unleashed");
        enemy.gameObject.AddComponent<BossPhases>().Setup(BossPhases.Fight.Server);
        step = Step.Free;
        Offering = false;
        if (paper != null) {
            paper.gameObject.SetActive(false);
        }
        enabled = false;
    }

    void Face(Vector3 to, float dt) {
        if (to.sqrMagnitude > 0.01f) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), dt * 6f);
        }
    }

    // Held out on the claw's tip while offered (and higher still as the strike winds up).
    void Raise(float dt, float amount) {
        if (paper == null) {
            return;
        }
        Vector3 up = paperRest + new Vector3(0f, 0.25f * amount, 0.3f * amount);
        paper.localPosition = Vector3.Lerp(paper.localPosition, up, dt * 6f);
        paper.localRotation = Quaternion.Slerp(paper.localRotation, Quaternion.Euler(-70f * amount, 0f, 0f), dt * 6f);
    }

    void Lower(float dt) {
        if (paper == null) {
            return;
        }
        paper.localPosition = Vector3.Lerp(paper.localPosition, paperRest, dt * 6f);
        paper.localRotation = Quaternion.Slerp(paper.localRotation, Quaternion.Euler(-30f, 0f, 8f), dt * 6f);
    }
}
