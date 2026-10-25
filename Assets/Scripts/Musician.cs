using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Slice, guarding the circle. The bots are here for her. The run ends when she does. She stands in the wary idle with
// her scalpel out: a bot in reach gets a stab or backhand slash. There is no piano; the clip slots keep their old
// numbers because the Animator and analytics use them. The player is still the one defending her.
public class Musician : MonoBehaviour {
    public const int BaseHealth = 100;
    public const string Name = "Slice";
    public const float StabReach = 1.8f;
    public const float StabCooldown = 2.5f;
    const float SitAfter = 1.2f;
    const float DefeatDuration = 1.65f;

    public const int CLIP_PIANO = 0;
    public const int CLIP_STAND_UP = 1;
    public const int CLIP_STAB = 2;
    public const int CLIP_SLASH = 3;
    public const int CLIP_SIT_DOWN = 4;
    public const int CLIP_IDLE = 5;
    public const int CLIP_HURT = 6;
    public const int CLIP_DEFEAT = 7;

    static readonly string[] ClipNames = {
        "piano", "standUp", "stab", "slash", "sitDown", "idle", "hurt", "defeat"
    };

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
    CanvasScaler barScaler;
    float barFill = 554f;
    int barStamp = -1;

    // The knife. Stand is 0 seated at the keys, 1 up on her feet.
    public int Stabs { get; private set; }
    public int FlinchCuts { get; private set; }
    public bool Standing { get { return stand >= 0.9f; } }
    public bool Seated { get { return stand <= 0f; } }
    public float QuietFor { get { return quietFor; } }
    // Playtest look: keeps her up on her feet in the wary idle with the knife out, with no bot near.
    public bool HoldKnifeUp { get; set; }
    public float LastStabAt { get; private set; } = -99f;
    public Enemy LastStabbed { get; private set; }
    public float LastStabDamage { get; private set; }
    public bool LastStabBig { get; private set; }
    public float LastStabHealthBefore { get; private set; }
    public float LastStabHealthAfter { get; private set; }
    public string LastStabClip { get; private set; } = "";
    public string Title { get { return title != null ? title.text : ""; } }

    // Animation state & diagnostics for playtest verification.
    readonly int[] clipCounts = new int[8];
    public int CurrentClip { get; private set; } = CLIP_IDLE;
    public string CurrentClipName {
        get {
            return CurrentClip >= 0 && CurrentClip < ClipNames.Length ? ClipNames[CurrentClip] : "unknown";
        }
    }
    public int StandUpClipsPlayed { get { return clipCounts[CLIP_STAND_UP]; } }
    public int SitDownClipsPlayed { get { return clipCounts[CLIP_SIT_DOWN]; } }
    public int StabClipsPlayed { get { return clipCounts[CLIP_STAB]; } }
    public int SlashClipsPlayed { get { return clipCounts[CLIP_SLASH]; } }
    public int HurtFlinches { get; private set; }
    public int Defeats { get; private set; }
    public int KnifeHitFrames { get; private set; }
    public bool Defeating { get { return defeating; } }
    public bool DefeatPlayedBeforeGameOver { get; private set; }
    public bool HasSliceRig { get { return hasSliceClips && slice != null && pianoHands != null; } }
    public bool MusicPlaying {
        get {
            AudioSource src = GetComponent<AudioSource>();
            return src != null && src.clip != null && src.isPlaying;
        }
    }
    public bool HandsNearKeys {
        get {
            if (handL == null || handR == null) {
                return slice != null;
            }
            Vector3 l = transform.InverseTransformPoint(handL.position);
            Vector3 r = transform.InverseTransformPoint(handR.position);
            return l.y > 0.5f && l.y < 0.95f && l.z > 0.05f && l.z < 0.55f
                && r.y > 0.5f && r.y < 0.95f && r.z > 0.05f && r.z < 0.55f;
        }
    }

    Transform slice;
    Animator pianoHands;
    Transform hips;
    Transform handL;
    Transform handR;
    Transform upperLegL;
    Transform upperLegR;
    Transform lowerLegL;
    Transform lowerLegR;
    Transform footL;
    Transform footR;
    Transform knife;
    LineRenderer slash;
    Vector3 sliceBase;
    Quaternion sliceTurn;
    Vector3 threatDir;
    float stand = 1f;
    float quietFor;
    float stabIn;
    float lunge;
    float slashLeft;
    Vector3 slashCenter;
    Vector3 slashDir;

    bool hasSliceClips;
    bool standingUp;
    bool sittingDown;
    bool nextIsSlash;
    int strikeClip = CLIP_STAB;
    Enemy strikeTarget;
    float strikeLeft;
    float strikeHitIn;
    bool strikeHitDone;
    float hurtLeft;
    bool defeating;
    float defeatLeft;

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
        GameObject recital = GameObject.Find("PianoRecital");
        if (recital == null) {
            GameObject oldPirate = GameObject.Find("Pirate");
            if (oldPirate != null) {
                recital = oldPirate.transform.parent != null ? oldPirate.transform.parent.gameObject : oldPirate;
                recital.name = "PianoRecital";
            }
        }
        if (recital == null) {
            GameObject prefab = Resources.Load<GameObject>("Slice/PianoRecital");
            if (prefab != null) {
                recital = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                recital.name = "PianoRecital";
            }
        }
        if (recital == null) {
            return;
        }
        EnsureSliceOn(recital);
        if (recital.GetComponent<Musician>() == null) {
            recital.AddComponent<Musician>();
        }
    }

    static void EnsureSliceOn(GameObject recital) {
        if (recital == null) {
            return;
        }
        Transform oldPirate = recital.transform.Find("Pirate");
        Transform sliceChild = recital.transform.Find("Slice");
        if (sliceChild == null) {
            GameObject slicePrefab = Resources.Load<GameObject>("Slice/Slice");
            if (slicePrefab != null) {
                GameObject instance = Instantiate(slicePrefab, recital.transform);
                instance.name = "Slice";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                sliceChild = instance.transform;
            }
        }
        if (oldPirate != null && sliceChild != null) {
            Destroy(oldPirate.gameObject);
        }
        // No piano: the bench and the keys are switched off, and the old piano loop never plays.
        Transform bench = recital.transform.Find("PianoBench");
        if (bench != null) {
            bench.gameObject.SetActive(false);
        }
        Transform piano = recital.transform.Find("Piano");
        if (piano != null) {
            piano.gameObject.SetActive(false);
        }
        Transform lamp = recital.transform.Find("PianoLight");
        if (lamp != null) {
            lamp.gameObject.SetActive(false);
        }
        AudioSource audio = recital.GetComponent<AudioSource>();
        if (audio != null) {
            audio.Stop();
            audio.playOnAwake = false;
            audio.enabled = false;
        }
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
        EnsureSliceOn(gameObject);
        BuildBar();
        FindSlice();
        SliceVoice.Ensure(this);
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // by: the bot that struck her, for analytics (the cause when she falls).
    public void Hurt(int amount, Enemy by = null) {
        if (!Alive || amount <= 0) {
            return;
        }
        Health = Mathf.Max(0, Health - amount);
        DamageTaken += amount;
        RunAnalytics.NoteHurt(by, amount);
        HitFlash = 1f;
        if (flinch <= 0f) {
            basePos = transform.localPosition;
        }
        flinch = 1f;
        GameAudio.Play("hit", 0.45f);
        RefreshBar();
        if (Alive) {
            OnHurtFlinch();
        } else {
            BeginDefeat();
        }
    }

    void OnHurtFlinch() {
        HurtFlinches++;
        hurtLeft = 0.58f;
        if (strikeLeft <= 0f && !standingUp && !sittingDown) {
            PlayClip(CLIP_HURT, true);
        }
    }

    void BeginDefeat() {
        if (defeating) {
            return;
        }
        defeating = true;
        defeatLeft = DefeatDuration;
        Defeats++;
        strikeLeft = 0f;
        strikeTarget = null;
        standingUp = false;
        sittingDown = false;
        hurtLeft = 0f;
        if (knife != null) {
            knife.gameObject.SetActive(false);
        }
        AudioSource music = GetComponent<AudioSource>();
        if (music != null && music.isPlaying) {
            music.Stop();
        }
        if (pianoHands != null) {
            pianoHands.updateMode = AnimatorUpdateMode.UnscaledTime;
            pianoHands.speed = 1f;
        }
        PlayClip(CLIP_DEFEAT, true);
    }

    public void ReviveForWave() {
        defeating = false;
        defeatLeft = 0f;
        if (Health <= 0) {
            Health = MaxHealth;
        }
        if (pianoHands != null) {
            pianoHands.updateMode = AnimatorUpdateMode.Normal;
            pianoHands.speed = 1f;
        }
        stand = 1f;
        standingUp = false;
        sittingDown = false;
        strikeLeft = 0f;
        strikeTarget = null;
        PlayClip(CLIP_IDLE, true);
        RefreshBar();
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
        if (defeating && !WaveDirector.IsLost) {
            defeatLeft -= Time.unscaledDeltaTime;
            if (defeatLeft <= 0f) {
                DefeatPlayedBeforeGameOver = true;
                WaveDirector director = FindAnyObjectByType<WaveDirector>();
                if (director != null) {
                    director.MusicianDown();
                }
            }
        }
        TickKnife();
        if (barScaler != null && barStamp != UiFit.Stamp) {
            LayoutBar();
        }
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
        barScaler = canvasGo.AddComponent<CanvasScaler>();
        barScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        barScaler.referenceResolution = new Vector2(1920, 1080);

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
        title = MakeText(canvasGo.transform, font, Name, 26, new Vector2(0f, -8f), new Vector2(640f, 32f));
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
        LayoutBar();
    }

    // Top middle everywhere. Held sideways, a phone narrows it to fit between the coin line and the bird list.
    void LayoutBar() {
        barStamp = UiFit.Stamp;
        UiFit.Apply(barScaler);
        float width = 560f;
        float titleWidth = 640f;
        int titleSize = 26;
        if (UiFit.Mode == UiMode.Wide) {
            width = Mathf.Clamp(UiFit.Size.x - 880f, 280f, 560f);
            titleWidth = width;
            titleSize = 22;
        }
        title.fontSize = titleSize;
        title.rectTransform.sizeDelta = new Vector2(titleWidth, 32f);
        back.rectTransform.sizeDelta = new Vector2(width, 26f);
        barFill = width - 6f;
        RefreshBar();
    }

    void RefreshBar() {
        if (fill == null) {
            return;
        }
        float ratio = MaxHealth <= 0 ? 0f : Health / (float)MaxHealth;
        fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, barFill * ratio), -6f);
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

    // ---------------------------------------------------------------- Slice & her knife

    void FindSlice() {
        Transform found = transform.Find("Slice");
        if (found == null) {
            found = transform.Find("Pirate");
        }
        pianoHands = found != null ? found.GetComponent<Animator>() : GetComponentInChildren<Animator>();
        if (found == null && pianoHands != null) {
            found = pianoHands.transform;
        }
        if (found == null) {
            return;
        }
        slice = found;
        ApplySliceSkins(slice.gameObject);
        sliceBase = slice.localPosition;
        sliceTurn = slice.localRotation;
        if (pianoHands != null) {
            pianoHands.applyRootMotion = false;
            pianoHands.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (pianoHands.GetComponent<SliceAnimRelay>() == null) {
                pianoHands.gameObject.AddComponent<SliceAnimRelay>();
            }
            hasSliceClips = HasClipParameter(pianoHands);
            EnsureClipEvents(pianoHands);
            if (pianoHands.isHuman) {
                hips = pianoHands.GetBoneTransform(HumanBodyBones.Hips);
                handL = pianoHands.GetBoneTransform(HumanBodyBones.LeftHand);
                handR = pianoHands.GetBoneTransform(HumanBodyBones.RightHand);
                upperLegL = pianoHands.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                upperLegR = pianoHands.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                lowerLegL = pianoHands.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                lowerLegR = pianoHands.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                footL = pianoHands.GetBoneTransform(HumanBodyBones.LeftFoot);
                footR = pianoHands.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }
        knife = FindDeepChild(slice, "Knife");
        if (knife == null && handR != null) {
            BuildKnife(handR);
        }
        if (knife != null) {
            knife.gameObject.SetActive(false);
        }
        BuildSlash();
        PlayClip(CLIP_IDLE, true);
    }

    static bool HasClipParameter(Animator animator) {
        if (animator == null || animator.runtimeAnimatorController == null) {
            return false;
        }
        foreach (AnimatorControllerParameter param in animator.parameters) {
            if (param.name == "Clip" && param.type == AnimatorControllerParameterType.Int) {
                return true;
            }
        }
        return false;
    }

    static void EnsureClipEvents(Animator animator) {
        if (animator == null || animator.runtimeAnimatorController == null) {
            return;
        }
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null) {
            return;
        }
        for (int i = 0; i < clips.Length; i++) {
            AnimationClip clip = clips[i];
            if (clip == null) {
                continue;
            }
            float hitNormalized = clip.name == "stab" ? 0.35f : clip.name == "slash" ? 0.45f : -1f;
            if (hitNormalized < 0f) {
                continue;
            }
            bool exists = false;
            AnimationEvent[] existing = clip.events;
            for (int e = 0; e < existing.Length; e++) {
                if (existing[e].functionName == "OnSliceKnifeHit") {
                    exists = true;
                    break;
                }
            }
            if (!exists) {
                var ev = new AnimationEvent {
                    functionName = "OnSliceKnifeHit",
                    time = Mathf.Clamp(clip.length * hitNormalized, 0.05f, Mathf.Max(0.05f, clip.length - 0.05f))
                };
                clip.AddEvent(ev);
            }
        }
    }

    static Transform FindDeepChild(Transform parent, string childName) {
        if (parent == null) {
            return null;
        }
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name == childName) {
                return child;
            }
            Transform found = FindDeepChild(child, childName);
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    void PlayClip(int clip, bool restart = false) {
        if (clip < 0 || clip >= ClipNames.Length) {
            return;
        }
        bool changed = CurrentClip != clip;
        CurrentClip = clip;
        if (changed || restart) {
            clipCounts[clip]++;
        }
        if (pianoHands == null) {
            return;
        }
        if (hasSliceClips) {
            pianoHands.speed = 1f;
            pianoHands.SetInteger("Clip", clip);
            if (changed || restart) {
                float fade = clip == CLIP_STAB || clip == CLIP_SLASH || clip == CLIP_HURT || clip == CLIP_DEFEAT ? 0.06f : 0.12f;
                pianoHands.CrossFadeInFixedTime(ClipNames[clip], fade, 0, 0f);
            }
        }
    }

    static Material sliceBodyMat;
    static Material sliceKnifeMat;

    // Binds the SliceSkin and SliceKnifeSkin textures (the Gen bake, taken out of Slice.fbx) on Knoll/BotSkin with
    // _Glow 0, so Slice and her scalpel never fall back to the FBX importer's white-silhouette material in WebGL.
    public static void ApplySliceSkins(GameObject model) {
        if (model == null) {
            return;
        }
        Shader shader = SafeShader.Find("Knoll/BotSkin");
        if (shader == null) {
            return;
        }
        if (sliceBodyMat == null) {
            sliceBodyMat = SkinMaterial(shader, "Slice/SliceSkin", 1.3f, 0.32f, 0.35f);
        }
        if (sliceKnifeMat == null) {
            sliceKnifeMat = SkinMaterial(shader, "Slice/SliceKnifeSkin", 1f, 0.3f, 0.3f);
        }
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) {
            if (r == null) {
                continue;
            }
            string n = r.gameObject.name.ToLowerInvariant();
            if (n == "blade" || n == "grip") {
                continue;
            }
            Material mat = n.Contains("knife") || n.Contains("scalpel") ? sliceKnifeMat : sliceBodyMat;
            if (mat != null) {
                r.sharedMaterial = mat;
            }
        }
    }

    static Material SkinMaterial(Shader shader, string resource, float lift, float ambient, float rim) {
        Texture2D skin = Resources.Load<Texture2D>(resource);
        if (skin == null) {
            return null;
        }
        var mat = new Material(shader);
        mat.name = skin.name;
        mat.mainTexture = skin;
        mat.color = Color.white;
        mat.SetFloat("_Lift", lift);
        mat.SetFloat("_AmbientBoost", ambient);
        mat.SetFloat("_RimStrength", rim);
        mat.SetFloat("_Glow", 0f);
        return mat;
    }

    // Fallback stand-in knife if the model has no Knife child.
    void BuildKnife(Transform hand) {
        if (hand == null) {
            return;
        }
        var holder = new GameObject("Knife").transform;
        holder.SetParent(hand, false);
        holder.localPosition = new Vector3(0f, 0.08f, 0.02f);
        var mat = new Material(SafeShader.Find("Unlit/Color"));
        mat.color = new Color(0.82f, 0.86f, 0.9f);
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(blade.GetComponent<Collider>());
        blade.name = "Blade";
        blade.transform.SetParent(holder, false);
        blade.transform.localPosition = new Vector3(0f, 0.11f, 0f);
        blade.transform.localScale = new Vector3(0.025f, 0.16f, 0.012f);
        var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(grip.GetComponent<Collider>());
        grip.name = "Grip";
        grip.transform.SetParent(holder, false);
        grip.transform.localScale = new Vector3(0.03f, 0.08f, 0.024f);
        foreach (Renderer part in holder.GetComponentsInChildren<Renderer>()) {
            part.sharedMaterial = mat;
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        grip.GetComponent<Renderer>().material.color = new Color(0.2f, 0.16f, 0.14f);
        knife = holder;
        knife.gameObject.SetActive(false);
    }

    // The slash: one pale arc that sweeps across the stab and fades in a fifth of a second.
    void BuildSlash() {
        var go = new GameObject("Slice Slash");
        go.transform.SetParent(transform, false);
        slash = go.AddComponent<LineRenderer>();
        slash.useWorldSpace = true;
        slash.positionCount = 0;
        slash.numCapVertices = 3;
        slash.material = new Material(SafeShader.Find("Sprites/Default"));
        slash.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0.12f), new Keyframe(1f, 0f));
        slash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        slash.receiveShadows = false;
    }

    void TickKnife() {
        if (slice == null) {
            return;
        }
        if (defeating) {
            if (knife != null) {
                knife.gameObject.SetActive(false);
            }
            lunge = Mathf.MoveTowards(lunge, 0f, Time.unscaledDeltaTime * 5f);
            TickSlash(Time.unscaledDeltaTime);
            return;
        }
        if (Time.deltaTime <= 0f) {
            return;
        }
        float dt = Time.deltaTime;
        Enemy near = Alive && !WaveDirector.IsLost ? NearestInReach() : null;
        if (near != null) {
            quietFor = 0f;
            Vector3 to = near.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) {
                threatDir = to.normalized;
            }
        } else {
            quietFor += dt;
        }

        // She never sits: guarding the circle, she is always up.
        stand = 1f;
        standingUp = false;
        sittingDown = false;

        stabIn -= dt;
        if (strikeLeft > 0f) {
            strikeLeft -= dt;
            strikeHitIn -= dt;
            if (!strikeHitDone && strikeHitIn <= 0f) {
                ExecuteStrikeHit();
            }
            if (strikeLeft <= 0f) {
                strikeTarget = null;
            }
        } else if (near != null && Standing && !sittingDown && stabIn <= 0f) {
            BeginStrike(near);
        } else if (hurtLeft > 0f) {
            hurtLeft -= dt;
            if (hurtLeft <= 0f) {
                if (Standing && !sittingDown) {
                    PlayClip(CLIP_IDLE, true);
                }
            }
        } else if (Standing && !sittingDown && CurrentClip != CLIP_IDLE) {
            PlayClip(CLIP_IDLE);
        }

        lunge = Mathf.MoveTowards(lunge, 0f, dt * 5f);
        if (pianoHands != null && !hasSliceClips) {
            pianoHands.speed = stand > 0.02f ? 0f : 1f;
        }
        if (knife != null) {
            knife.gameObject.SetActive(Alive && (stand > 0.15f || standingUp || strikeLeft > 0f));
        }
        TickSlash(dt);
    }

    Enemy NearestInReach() {
        Enemy best = null;
        float bestGap = float.MaxValue;
        Vector3 at = transform.position;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy enemy = Enemy.Roster[i];
            if (enemy == null || !enemy.Alive || enemy.Climbing) {
                continue;
            }
            Vector3 to = enemy.transform.position - at;
            float height = to.y;
            to.y = 0f;
            float gap = to.magnitude - enemy.BodyScale * 0.22f;
            if (gap <= StabReach && height < 2.5f && gap < bestGap) {
                bestGap = gap;
                best = enemy;
            }
        }
        return best;
    }

    void BeginStrike(Enemy foe) {
        bool useSlash = nextIsSlash;
        nextIsSlash = !nextIsSlash;
        strikeClip = useSlash ? CLIP_SLASH : CLIP_STAB;
        strikeTarget = foe;
        strikeHitDone = false;
        strikeHitIn = useSlash ? 0.36f : 0.34f;
        strikeLeft = useSlash ? 0.78f : 0.90f;
        stabIn = StabCooldown + strikeHitIn;
        hurtLeft = 0f;
        if (foe != null) {
            Vector3 to = foe.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) {
                threatDir = to.normalized;
            }
        }
        PlayClip(strikeClip, true);
        GameAudio.Play("batswing", 0.4f, useSlash ? 1.55f : 1.7f);
    }

    // Fired by the AnimationEvent on Slice's stab and slash clips (via SliceAnimRelay).
    public void OnSliceKnifeHit() {
        KnifeHitFrames++;
        if (!strikeHitDone && (strikeLeft > 0f || strikeTarget != null)) {
            ExecuteStrikeHit();
        }
    }

    // Small bots take a real stab; the big ones (Spider Bots, heavies, the Alpha) only get a flinch-cut and a shove.
    // Stage Hands sharpens it a little.
    void ExecuteStrikeHit() {
        if (strikeHitDone) {
            return;
        }
        strikeHitDone = true;
        stabIn = StabCooldown;
        Enemy foe = strikeTarget != null && strikeTarget.Alive ? strikeTarget : NearestInReach();
        if (foe == null || !foe.Alive) {
            return;
        }
        bool big = foe.Boss || foe.IsHeavy || BotRoster.IsSpider(foe.Kind);
        float amount = Mathf.Max(1f, Combat.ArrowBase() * 0.6f) * (1f + 0.15f * MetaProgress.Rank(UpgradeId.StageHands));
        if (big) {
            amount *= 0.25f;
        }
        float before = foe.Health01;
        lunge = 1f;
        Vector3 to = foe.transform.position - transform.position;
        to.y = 0f;
        Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : threatDir;
        threatDir = dir;
        slashCenter = transform.position + dir * 0.9f + Vector3.up * 1f;
        slashDir = dir;
        slashLeft = 1f;
        foe.Damage(amount, DamageKind.Blade, false);
        if (big) {
            foe.Knock(dir * 3.5f);
            FlinchCuts++;
        } else {
            GameAudio.Play("hit", 0.4f, 1.35f);
        }
        Stabs++;
        LastStabAt = Time.time;
        LastStabbed = foe;
        LastStabDamage = amount;
        LastStabBig = big;
        LastStabHealthBefore = before;
        LastStabHealthAfter = foe.Alive ? foe.Health01 : 0f;
        LastStabClip = strikeClip == CLIP_SLASH ? "slash" : "stab";
    }

    void TickSlash(float dt) {
        if (slash == null) {
            return;
        }
        if (slashLeft <= 0f) {
            slash.positionCount = 0;
            return;
        }
        slashLeft = Mathf.MoveTowards(slashLeft, 0f, dt * 5.5f);
        float sweep = 1f - slashLeft;
        const int points = 10;
        slash.positionCount = points;
        Vector3 side = Vector3.Cross(Vector3.up, slashDir);
        for (int i = 0; i < points; i++) {
            float t = i / (float)(points - 1);
            float angle = Mathf.Lerp(-65f, Mathf.Lerp(-65f, 65f, Mathf.Min(1f, sweep * 2.2f)), t) * Mathf.Deg2Rad;
            Vector3 p = slashCenter + side * Mathf.Sin(angle) * 0.75f + slashDir * (Mathf.Cos(angle) - 1f) * 0.35f + Vector3.up * Mathf.Sin(angle) * 0.25f;
            slash.SetPosition(i, p);
        }
        Color steel = new Color(0.86f, 0.95f, 1f, 0.85f * slashLeft);
        slash.startColor = new Color(steel.r, steel.g, steel.b, 0f);
        slash.endColor = steel;
    }

    // After the Animator poses her each frame: when up on her feet, turn to the bot, step clear of the bench, and lunge on the hit.
    void LateUpdate() {
        if (slice == null) {
            return;
        }
        if (defeating) {
            Vector3 backStep = Vector3.back * 0.42f;
            slice.localPosition = sliceBase + backStep;
            return;
        }
        if (stand <= 0f) {
            slice.localPosition = sliceBase;
            slice.localRotation = sliceTurn;
            return;
        }
        float w = Mathf.SmoothStep(0f, 1f, stand);
        Vector3 local = threatDir.sqrMagnitude > 0.01f ? transform.InverseTransformDirection(threatDir) : Vector3.back;
        local.y = 0f;
        // The piano is in front of her: step out to the side or back, never into it.
        Vector3 step = local;
        if (step.z > 0f) {
            step.z = 0f;
        }
        step = step.sqrMagnitude > 0.01f ? step.normalized : Vector3.back;
        Quaternion face = local.sqrMagnitude > 0.01f ? Quaternion.LookRotation(local.normalized) : sliceTurn;
        slice.localRotation = Quaternion.Slerp(sliceTurn, face, w);
        slice.localPosition = sliceBase + step * (0.45f * w) + local.normalized * (0.22f * lunge);
        if (SliceVoice.Instance != null) {
            SliceVoice.Instance.ApplyPose(slice);
        }
        if (hasSliceClips) {
            return;
        }
        if (hips == null || footL == null || footR == null) {
            slice.localPosition += Vector3.up * 0.35f * w;
            return;
        }
        float feetBefore = Mathf.Min(footL.position.y, footR.position.y);
        Straighten(upperLegL, lowerLegL, w);
        Straighten(upperLegR, lowerLegR, w);
        Straighten(lowerLegL, footL, w);
        Straighten(lowerLegR, footR, w);
        float feetAfter = Mathf.Min(footL.position.y, footR.position.y);
        slice.position += Vector3.up * (feetBefore - feetAfter);
    }

    // Turn a leg bone so the next joint hangs straight below it (fallback when no stand/sit clips exist).
    static void Straighten(Transform bone, Transform next, float w) {
        if (bone == null || next == null) {
            return;
        }
        Vector3 along = next.position - bone.position;
        if (along.sqrMagnitude < 0.0001f) {
            return;
        }
        Quaternion turn = Quaternion.FromToRotation(along, Vector3.down);
        bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * bone.rotation;
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
