using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Which way did that come from? When a bot's blow lands on Wallit (PlayerGrowth.OnHit) or Slice (Musician.Hurt), a soft red
// wedge lights on a ring around the middle of the screen, pointing at the attacker (turned by the camera's yaw, so it is
// right whichever way he faces) and fades in 0.7 s. It rides a ring about a third of the short side from the centre: clear of
// the stick and Swing pad in the bottom corners, the top-middle bar and the edges. Soft shapes only (J's art rules): no
// sparks. Slice's wedge is a little dimmer than Wallit's own. Self-made singleton, one pooled Image per slot.
public class HitDirection : MonoBehaviour {
    const int Slots = 4;
    const float Life = 0.7f;

    public static int Shown, ShownWallit, ShownSlice;
    public static int WallitHits;       // every blow Wallit took (shown or not): Shown / this is hurt.directionShown
    public static float Radius = 0.40f; // of the short side, from the ring centre: out near the edge, above the corner pads
    public static float LastAngle;      // degrees clockwise from screen up
    public static string Last = "";

    static HitDirection instance;
    static Sprite wedge;

    class Mark {
        public Image Img;
        public float Age = 99f;
        public float Peak;
    }

    Canvas canvas;
    RectTransform ringRoot;
    readonly List<Mark> marks = new List<Mark>();
    int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        wedge = null;
        Shown = ShownWallit = ShownSlice = WallitHits = 0;
        LastAngle = 0f;
        Last = "";
    }

    // Wallit took a blow: from is the attacker's or the projectile's or hazard's spot; null means unknown, so the arc
    // points behind where he faces (a hit he did not see coming). Always counted, always shown.
    public static void HitWallit(Vector3? from, Transform self) {
        WallitHits++;
        Vector3 at = from.HasValue ? from.Value : self.position - self.forward * 3f;
        Hit(at, true);
    }

    // from: where the blow came from, in the world. Wallit: the player took it; else Slice.
    public static void Hit(Vector3 from, bool wallit) {
        Camera cam = Camera.main;
        if (cam == null) {
            return;
        }
        if (instance == null) {
            instance = new GameObject("Hit Direction").AddComponent<HitDirection>();
        }
        // Where the player's eye is: Wallit, or Slice for her own blow.
        Vector3 centre = wallit && PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform.position
            : Musician.Instance != null ? Musician.Instance.transform.position : cam.transform.position;
        Vector3 d = from - centre;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) {
            return;
        }
        // Into the camera's frame: screen up is the camera's forward on the ground.
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) {
            fwd = Vector3.forward;
        }
        fwd.Normalize();
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        float angle = Mathf.Atan2(Vector3.Dot(d, right), Vector3.Dot(d, fwd)) * Mathf.Rad2Deg;
        instance.Show(angle, wallit);
    }

    void Show(float angle, bool wallit) {
        Build();
        Mark m = marks[next];
        next = (next + 1) % Slots;
        m.Age = 0f;
        m.Peak = wallit ? 0.85f : 0.6f;
        if (ComfortOptions.ReducedMotion) {
            m.Peak *= 0.6f;   // Calm: softer
        }
        m.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        m.Img.gameObject.SetActive(true);
        Shown++;
        if (wallit) {
            ShownWallit++;
        } else {
            ShownSlice++;
        }
        LastAngle = angle < 0f ? angle + 360f : angle;
        Last = (wallit ? "Wallit" : "Slice") + " hit from " + LastAngle.ToString("0") + " deg";
        BatEquipment.Note("hit-direction: " + Last);
    }

    void Update() {
        if (canvas == null) {
            return;
        }
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < marks.Count; i++) {
            Mark m = marks[i];
            if (m.Age >= Life * 1.4f) {
                if (m.Img.gameObject.activeSelf) {
                    m.Img.gameObject.SetActive(false);
                }
                continue;
            }
            m.Age += dt;
            float k = Mathf.Clamp01(m.Age / (ComfortOptions.ReducedMotion ? Life * 1.4f : Life));
            float a = m.Peak * (k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f);
            m.Img.color = new Color(0.92f, 0.12f, 0.1f, Mathf.Clamp01(a));
        }
        // The ring's size follows the canvas: a third of the short side (in canvas units) from the middle.
        var rt = (RectTransform)canvas.transform;
        float unit = Mathf.Min(rt.rect.width, rt.rect.height);
        float radius = unit * Radius;
        for (int i = 0; i < marks.Count; i++) {
            RectTransform r = marks[i].Img.rectTransform;
            r.sizeDelta = new Vector2(Mathf.Max(88f, unit * 0.16f), Mathf.Max(88f, radius * 2f));
        }
        ringRoot.anchoredPosition = Vector2.zero;
    }

    void Build() {
        if (canvas != null) {
            return;
        }
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 117;   // under the edge tell, the pads and the cards
        var scaler = gameObject.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);
        ringRoot = new GameObject("Ring").AddComponent<RectTransform>();
        ringRoot.SetParent(transform, false);
        ringRoot.anchorMin = ringRoot.anchorMax = new Vector2(0.5f, 0.55f);
        if (wedge == null) {
            wedge = MakeWedge();
        }
        for (int i = 0; i < Slots; i++) {
            var go = new GameObject("Wedge " + i);
            go.transform.SetParent(ringRoot, false);
            var img = go.AddComponent<Image>();
            img.sprite = wedge;
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            // The image is as tall as the ring's diameter and rotates about its centre; the wedge is drawn at its top.
            marks.Add(new Mark { Img = img });
        }
    }

    // A soft arc at the top of a tall square-ish image: bright centre, falling away at the sides and toward the middle.
    static Sprite MakeWedge() {
        const int w = 64, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Hit Wedge" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++) {
            float v = y / (float)(h - 1);              // 1 at the top (the rim)
            float along = Mathf.Clamp01((v - 0.82f) / 0.18f);   // only the top 18%
            along = Mathf.Sin(along * Mathf.PI * 0.5f);
            for (int x = 0; x < w; x++) {
                float u = Mathf.Abs(x / (float)(w - 1) - 0.5f) * 2f;
                float across = Mathf.Pow(Mathf.Clamp01(1f - u), 1.5f);
                byte a = (byte)(255f * along * across);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
