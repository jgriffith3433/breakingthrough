using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The pirate at the piano. Turkeys are here for him. The run ends when he does.
public class Musician : MonoBehaviour {
    public const int BaseHealth = 100;

    public static Musician Instance { get; private set; }
    public int MaxHealth { get; private set; } = BaseHealth;
    public int Health { get; private set; } = BaseHealth;
    public int DamageTaken { get; private set; }
    public float HitFlash { get; private set; }
    public bool Alive { get { return Health > 0; } }

    Light flare;
    Image fill;
    Image back;
    Image veil;
    Text number;
    Text title;
    Vector3 basePos;
    float flinch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<Musician>() != null) {
            return;
        }
        GameObject recital = GameObject.Find("PianoRecital");
        if (recital == null) {
            recital = GameObject.Find("Pirate");
        }
        if (recital == null) {
            return;
        }
        recital.AddComponent<Musician>();
    }

    void Awake() {
        Instance = this;
        MaxHealth = BaseHealth + MetaProgress.StageHealth();
        Health = MaxHealth;
        flare = gameObject.AddComponent<Light>();
        flare.type = LightType.Point;
        flare.color = new Color(1f, 0.25f, 0.18f);
        flare.range = 4.5f;
        flare.intensity = 0f;
        flare.shadows = LightShadows.None;
    }

    void Start() {
        BuildBar();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    public void Hurt(int amount) {
        if (!Alive || amount <= 0) {
            return;
        }
        Health = Mathf.Max(0, Health - amount);
        DamageTaken += amount;
        HitFlash = 1f;
        if (flinch <= 0f) {
            basePos = transform.localPosition;
        }
        flinch = 1f;
        GameAudio.Play("hit", 0.45f);
        RefreshBar();
        if (!Alive) {
            WaveDirector director = FindAnyObjectByType<WaveDirector>();
            if (director != null) {
                director.MusicianDown();
            }
        }
    }

    public void Heal(int amount) {
        if (!Alive || amount <= 0 || Health >= MaxHealth) {
            return;
        }
        int before = Health;
        Health = Mathf.Min(MaxHealth, Health + amount);
        UpgradeLook.Number(transform.position + Vector3.up * 2.4f, Health - before, false, DamageKind.Blade);
        RefreshBar();
    }

    public void RaiseMax(int amount) {
        MaxHealth += amount;
        Health += amount;
        RefreshBar();
    }

    void Update() {
        if (HitFlash > 0f) {
            HitFlash = Mathf.MoveTowards(HitFlash, 0f, Time.deltaTime * 3.2f);
        }
        bool low = Alive && Health <= 30;
        if (HitFlash > 0f || low) {
            RefreshBar();
        }
        if (flinch > 0f) {
            flinch = Mathf.MoveTowards(flinch, 0f, Time.deltaTime * 3.5f);
            transform.localPosition = basePos + new Vector3(Mathf.Sin(flinch * 28f) * 0.05f, 0f, 0f);
            if (flinch <= 0f) {
                transform.localPosition = basePos;
            }
        }
        if (flare != null) {
            float hurt = HitFlash * 7f;
            float warn = low ? 1.2f + Mathf.Sin(Time.time * 5f) * 0.8f : 0f;
            flare.intensity = Mathf.Max(hurt, warn);
        }
        if (veil != null) {
            float pulse = low ? 0.12f + 0.14f * (0.5f + 0.5f * Mathf.Sin(Time.time * 5f)) : 0f;
            veil.color = new Color(0.65f, 0.04f, 0.03f, pulse);
        }
    }

    void BuildBar() {
        var canvasGo = new GameObject("Musician Health");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var veilGo = new GameObject("Veil");
        veilGo.transform.SetParent(canvasGo.transform, false);
        veil = veilGo.AddComponent<Image>();
        veil.color = new Color(0.65f, 0.04f, 0.03f, 0f);
        veil.raycastTarget = false;
        var veilRect = veil.rectTransform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = Vector2.zero;
        veilRect.offsetMax = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title = MakeText(canvasGo.transform, font, "Musician's Health", 26, new Vector2(0f, -8f), new Vector2(640f, 32f));
        title.alignment = TextAnchor.MiddleCenter;

        var backGo = new GameObject("Back");
        backGo.transform.SetParent(canvasGo.transform, false);
        back = backGo.AddComponent<Image>();
        UiSkin.Bar(back);
        back.color = new Color(0.12f, 0.08f, 0.08f, 0.92f);
        back.raycastTarget = false;
        var backRect = back.rectTransform;
        backRect.anchorMin = new Vector2(0.5f, 1f);
        backRect.anchorMax = new Vector2(0.5f, 1f);
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.anchoredPosition = new Vector2(0f, -42f);
        backRect.sizeDelta = new Vector2(560f, 26f);

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(backGo.transform, false);
        fill = fillGo.AddComponent<Image>();
        UiSkin.Bar(fill);
        fill.raycastTarget = false;
        var fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(3f, 0f);
        fillRect.sizeDelta = new Vector2(554f, -6f);

        number = MakeText(backGo.transform, font, "100", 20, Vector2.zero, new Vector2(560f, 26f));
        number.alignment = TextAnchor.MiddleCenter;
        number.rectTransform.anchorMin = Vector2.zero;
        number.rectTransform.anchorMax = Vector2.one;
        number.rectTransform.offsetMin = Vector2.zero;
        number.rectTransform.offsetMax = Vector2.zero;
        RefreshBar();
    }

    void RefreshBar() {
        if (fill == null) {
            return;
        }
        float ratio = MaxHealth <= 0 ? 0f : Health / (float)MaxHealth;
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, 554f * ratio), -6f);
        Color healthy = Color.Lerp(new Color(0.75f, 0.18f, 0.14f), new Color(0.45f, 0.82f, 0.38f), ratio);
        if (ratio < 0.45f) {
            healthy = Color.Lerp(new Color(0.85f, 0.16f, 0.12f), new Color(0.92f, 0.72f, 0.22f), ratio / 0.45f);
        }
        bool flash = HitFlash > 0.35f;
        bool low = Alive && Health <= 30;
        if (!flash && low) {
            float throb = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
            healthy = Color.Lerp(new Color(0.45f, 0.05f, 0.04f), new Color(1f, 0.28f, 0.18f), throb);
        }
        fill.color = flash ? Color.white : healthy;
        if (back != null) {
            back.color = flash ? new Color(1f, 0.92f, 0.85f, 0.95f) : new Color(0.12f, 0.08f, 0.08f, 0.92f);
        }
        if (number != null) {
            number.text = Health.ToString();
            number.color = flash ? new Color(0.25f, 0.08f, 0.06f) : new Color(1f, 0.97f, 0.92f);
        }
        if (title != null) {
            title.color = flash ? Color.white : new Color(1f, 0.94f, 0.84f, 0.96f);
        }
    }

    static Text MakeText(Transform parent, Font font, string value, int size, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(1f, 0.96f, 0.88f, 0.96f);
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleCenter;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }
}
