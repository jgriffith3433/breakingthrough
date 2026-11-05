using System.Collections.Generic;
using UnityEngine;

// Campaign-only moves for Wallit: the combo's three distinct hits and the dodge (X) with its short window of
// invulnerability. Arcade keeps today's controls and balance: every number here reads as "no effect" unless a chapter
// of an open case file is running (CampaignProgress.Active), and Enabled is the flag that switches it all off.
public static class CampaignMoves {
    public static bool Enabled = true;

    public const KeyCode DodgeKey = KeyCode.X;
    public const float DodgeDistance = 6f;
    public const float DodgeSeconds = 0.28f;
    public const float DodgeInvulnerable = 0.4f;
    public const float DodgeCooldown = 0.9f;

    // The combo's three hits. Swing 3 stays the finisher at its Tuning numbers.
    public struct ComboHit {
        public string Name;
        public float Damage;
        public float Knock;
        public float Pitch;
        public float HitStop;
    }

    public static readonly ComboHit[] Combo = {
        new ComboHit { Name = "DEPOSIT", Damage = 0.85f, Knock = 0.8f, Pitch = 1.12f, HitStop = 0.03f },
        new ComboHit { Name = "WITHDRAWAL", Damage = 1.15f, Knock = 1.5f, Pitch = 0.96f, HitStop = 0.04f },
        new ComboHit { Name = "OVERDRAFT!", Damage = 1f, Knock = 1f, Pitch = 0.9f, HitStop = 0.07f },
    };

    // What the playtest reads: combo hits, dodges and the hits a dodge took.
    public static readonly List<string> Log = new List<string>();
    public static int Dodges { get; private set; }
    public static int Avoided { get; private set; }
    public static int ComboHits { get; private set; }

    static float dodgeUntil;
    static float invulnerableUntil;
    static float readyAt;

    public static bool Active {
        get { return Enabled && CampaignProgress.Active; }
    }

    public static bool Invulnerable {
        get { return Active && (Time.time < invulnerableUntil || CampaignProgress.ShieldUp); }
    }

    public static bool Dodging {
        get { return Active && Time.time < dodgeUntil; }
    }

    public static bool DodgeReady {
        get { return Active && Time.time >= readyAt && !Dodging; }
    }

    public static float DodgeWait {
        get { return Mathf.Max(0f, readyAt - Time.time); }
    }

    public static void Begin() {
        Log.Clear();
        Dodges = 0;
        Avoided = 0;
        ComboHits = 0;
        dodgeUntil = 0f;
        invulnerableUntil = 0f;
        readyAt = 0f;
    }

    // step is 1 or 2; the finisher is not changed.
    public static ComboHit Hit(int step) {
        return Combo[Mathf.Clamp(step, 1, 3) - 1];
    }

    public static void NoteCombo(int step, int bots) {
        if (bots <= 0) {
            return;
        }
        ComboHit hit = Hit(step);
        ComboHits++;
        Log.Add("combo|" + step + "|" + hit.Name + "|" + bots + " hit|damage x" + (step == 3 ? Tuning.BatDamageFinisher : Tuning.BatDamage * hit.Damage).ToString("0.00"));
        UpgradeLook.Say(hit.Name, 0.45f, -40f);
    }

    // The dodge: a quick dash along the stick (or back, with none), untouchable for a moment. True when it went off.
    public static bool TryDodge(Vector3 from, Vector3 direction) {
        if (!DodgeReady) {
            return false;
        }
        Dodges++;
        dodgeUntil = Time.time + DodgeSeconds;
        invulnerableUntil = Time.time + DodgeInvulnerable;
        readyAt = Time.time + DodgeCooldown;
        Log.Add("dodge|" + Dodges + "|window " + DodgeInvulnerable.ToString("0.00") + " s|" + direction.ToString("0.0"));
        Fx.Ring(from + Vector3.up * 0.2f, 3.2f, new Color(0.75f, 0.9f, 1f, 0.8f), 0.3f);
        Fx.Emit(FxKind.Dust, from + Vector3.up * 0.2f, 8, new Color(0.62f, 0.58f, 0.5f, 0.5f), 0.7f, 2f, 1f);
        GameAudio.Play("batswing", 0.4f, 0.7f);
        return true;
    }

    // A blow that came in during the window and found nothing.
    public static void NoteAvoided(Enemy by) {
        Avoided++;
        Log.Add("avoided|" + (by != null ? BotRoster.Slug(by.Kind) : "stamp") + "|hit " + Avoided);
        UpgradeLook.Say(CampaignProgress.ShieldUp ? "HELD" : "MISSED ME", 0.8f, 120f);
    }
}

// Rides on the ChapterRun object: carries Wallit through the dodge.
public class CampaignDodge : MonoBehaviour {
    Vector3 heading;
    CharacterController body;

    public void Go(Vector3 direction) {
        heading = direction;
    }

    void Update() {
        if (!CampaignMoves.Dodging || PauseMenu.IsOpen || Time.timeScale == 0f || PlayerGrowth.Instance == null) {
            return;
        }
        if (body == null) {
            body = PlayerGrowth.Instance.GetComponent<CharacterController>();
        }
        if (body != null && body.enabled) {
            body.Move(heading * (CampaignMoves.DodgeDistance / CampaignMoves.DodgeSeconds) * Time.deltaTime);
        }
    }

    // The way the stick points, flat on the ground by the camera; backwards from where he faces when it rests.
    public static Vector3 Heading(Transform player) {
        Vector2 input = Vector2.ClampMagnitude(new Vector2(PlayInput.GetAxis("Horizontal"), PlayInput.GetAxis("Vertical")), 1f);
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 fwd = cam != null ? cam.forward : player.forward;
        Vector3 right = cam != null ? cam.right : player.right;
        fwd.y = 0f;
        right.y = 0f;
        Vector3 dir = fwd.normalized * input.y + right.normalized * input.x;
        if (input.sqrMagnitude < 0.01f || dir.sqrMagnitude < 0.01f) {
            dir = -player.forward;
            dir.y = 0f;
        }
        return dir.normalized;
    }
}
