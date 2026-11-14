using UnityEngine;

// What it feels like when a bot lands a blow on Wallit (Campaign levels and chapters, anywhere PlayerGrowth.OnHit
// is reached): he is shoved 1 to 1.5 m away from the hitter in a fifth of a second, his outline strobes white and
// red for the 0.6 s he cannot be hit again, his flinch clip plays (unless the bat is mid-swing: a swing is never
// taken out of his hands), he grunts with his existing hurt line, and a heavy hit (a boss, an elite, a Spider Bot)
// gives the camera one small jolt through CameraFeel. Numbers are the constants below.
public class WallitHurt : MonoBehaviour {
    public const float InvulnerableSeconds = 0.6f;
    public const float KnockLight = 1.0f;
    public const float KnockHeavy = 1.5f;
    public const float KnockSeconds = 0.2f;

    public static int Strikes { get; private set; }
    public static int Heavy { get; private set; }
    public static int Ignored { get; private set; }
    public static float LastKnockMeters { get; private set; }
    public static float LastKnockSeconds { get; private set; }
    public static float FlashPeak { get; private set; }
    public static string Last = "";

    static float until;
    static float flashFrom;

    public static bool Invulnerable {
        get { return Time.time < until; }
    }

    public static void Reset() {
        Strikes = 0;
        Heavy = 0;
        Ignored = 0;
        LastKnockMeters = 0f;
        LastKnockSeconds = 0f;
        FlashPeak = 0f;
        until = 0f;
        Last = "";
    }

    public static bool IsHeavy(Enemy by) {
        return by != null && (by.Boss || by.Elite || BotRoster.IsSpider(by.Kind));
    }

    CharacterController body;
    Vector3 slide;
    float slideLeft;
    float slideTotal;
    float flashClock;
    bool flashing;

    // A blow landed. Returns false when the window was already up and the blow found nothing.
    public static bool Strike(PlayerGrowth growth, Enemy by, Vector3? source = null) {
        if (growth == null) {
            return true;
        }
        if (Invulnerable) {
            Ignored++;
            return false;
        }
        WallitHurt hurt = growth.GetComponent<WallitHurt>();
        if (hurt == null) {
            hurt = growth.gameObject.AddComponent<WallitHurt>();
        }
        hurt.Begin(by, source);
        return true;
    }

    void Begin(Enemy by, Vector3? source) {
        bool heavy = IsHeavy(by);
        Strikes++;
        if (heavy) {
            Heavy++;
        }
        until = Time.time + InvulnerableSeconds;
        flashFrom = Time.time;
        flashing = true;
        flashClock = 0f;

        Vector3 away = by != null ? transform.position - by.transform.position
            : source.HasValue ? transform.position - source.Value : -transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) {
            away = -transform.forward;
            away.y = 0f;
        }
        away.Normalize();
        float meters = heavy ? KnockHeavy : KnockLight;
        if (ComfortOptions.ReducedMotion) {
            meters *= 0.5f;   // Calm: a gentler shove
        }
        if (PlayerGrowth.Instance != null) {
            meters *= Mathf.Clamp(PlayerGrowth.Instance.Scale, 1f, 1.6f);
        }
        slide = away * (meters / KnockSeconds);
        slideLeft = KnockSeconds;
        slideTotal = KnockSeconds;
        LastKnockMeters = meters;
        LastKnockSeconds = KnockSeconds;
        body = GetComponent<CharacterController>();

        if (heavy && !ComfortOptions.ReducedMotion) {   // Calm: no camera jolt
            CameraFeel.HurtJolt();
        }
        Last = (heavy ? "heavy" : "light") + " hit by " + (by != null ? BotRoster.Slug(by.Kind) : "stamp") + ": knock " + meters.ToString("0.0") + " m, window " + InvulnerableSeconds.ToString("0.0") + " s";
        BatEquipment.Note("hurt: " + Last);
    }

    void Update() {
        if (slideLeft > 0f && Time.timeScale > 0f) {
            float dt = Time.deltaTime;
            slideLeft -= dt;
            // Ease out: fastest at once, gone by the end, so the total is the distance asked for.
            float t = Mathf.Clamp01(slideLeft / slideTotal);
            if (body != null && body.enabled && dt > 0f) {
                body.Move(slide * (2f * t) * dt);
            }
        }
        if (flashing) {
            float age = Time.time - flashFrom;
            if (age >= InvulnerableSeconds) {
                flashing = false;
                CharacterMovement.SetWallitFlash(0f, Color.white, Color.clear);
                return;
            }
            // White and red, four swaps in the window; strongest early.
            bool white = Mathf.FloorToInt(age / 0.075f) % 2 == 0;
            float strength = Mathf.Lerp(1f, 0.35f, age / InvulnerableSeconds);
            FlashPeak = Mathf.Max(FlashPeak, strength);
            Color tint = white ? Color.white : new Color(1f, 0.15f, 0.12f);
            CharacterMovement.SetWallitFlash(2f * strength, tint, new Color(tint.r, tint.g, tint.b, 0.4f * strength));
        }
    }

    void OnDisable() {
        if (flashing) {
            flashing = false;
            CharacterMovement.SetWallitFlash(0f, Color.white, Color.clear);
        }
    }
}
