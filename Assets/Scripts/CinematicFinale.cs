using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Chapter 6 ADDRESS FILED payoff: the player holds swing to carry the stamp to Slice's ring while batting
// three raining notices. On release ADDRESS prints, lamps light, Slice's closing line plays. Existing lines only.
public partial class CinematicDirector {
    const int FinaleNoticeNeed = 3;
    const int FinaleLampNeed = 4;
    const float FinaleMinSeconds = 20f;
    const float FinaleMaxSeconds = 40f;
    const float FinaleHoldRate = 0.055f;       // ~18 s of clean hold to fill (notices stretch it)
    const float FinaleKnockLoss = 0.08f;
    const float FinaleNoticeLead = 2.4f;
    const float FinaleWindow = 0.95f;

    bool carryArmed;
    bool carrySettling;
    bool carryResolving;
    bool carryHeldOnce;
    bool carryWasHeld;
    bool carryNeedTap;
    float carryStart = -1f;
    float carrySettleUntil;
    float carryNextNoticeAt;
    float carryWindowUntil;
    float carryKnockUntil;
    float carryProgress;
    int carryBats;
    int carryMisses;
    int carryNoticesSpawned;
    Vector3 carryFrom;
    Vector3 carryTo;
    GameObject carryStamp;
    Transform carryStampHead;
    readonly List<GameObject> carryNotices = new List<GameObject>();
    readonly List<GameObject> carryLamps = new List<GameObject>();
    readonly List<Light> carryLampLights = new List<Light>();
    GameObject carryHud;
    RectTransform carryHoldRt;
    Image carryHoldFill;
    Image carryTimingIcon;
    RectTransform carryTimingRt;
    Text carryHint;
    Button carryTimingButton;
    bool carryTimingPressed;
    bool carrySliceReacted;
    readonly List<Canvas> carryHiddenHud = new List<Canvas>();

    void BeginFinaleCarry(Shot s) {
        carryArmed = true;
        FinaleCarryActive = true;
        FinaleCarryReady = false;
        FinaleTimingOpen = false;
        carrySettling = true;
        carryResolving = false;
        carryHeldOnce = false;
        carryWasHeld = false;
        carryNeedTap = false;
        carryStart = Time.unscaledTime;
        carrySettleUntil = Time.unscaledTime + 1.1f;
        carryNextNoticeAt = Time.unscaledTime + 3.2f;
        carryWindowUntil = -1f;
        carryKnockUntil = -1f;
        carryProgress = 0f;
        carryBats = 0;
        carryMisses = 0;
        carryNoticesSpawned = 0;
        FinaleCarryProgress = 0f;
        FinaleCarryBats = 0;
        FinaleCarryMisses = 0;
        FinaleLampsLit = 0;
        FinaleCarrySeconds = 0f;
        FinaleHoldPromptWidthPx = 0f;
        FinaleHoldPromptCenterY01 = 0f;
        carryTimingPressed = false;
        carrySliceReacted = false;

        // Seat Wallit a few metres from the ring, facing Slice; camera holds the shot's look.
        carryTo = circle + Vector3.up * 0.05f;
        carryFrom = PolarPoint(20f, 5.5f, 0.8f);
        carryFrom.y = Ground(carryFrom.x, carryFrom.z) + 0.2f;
        if (wallit != null) {
            if (wallitBody != null) {
                wallitBody.enabled = false;
            }
            wallit.position = carryFrom;
            Vector3 face = circle - wallit.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) {
                wallit.rotation = Quaternion.LookRotation(face.normalized);
            }
            if (wallitBody != null) {
                wallitBody.enabled = true;
            }
            if (wallitMotor != null) {
                wallitMotor.Stop();
            }
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
        }
        if (slice != null) {
            Vector3 sp = PolarPoint(200f, 2.0f, -0.5f);
            sp.y = Ground(sp.x, sp.z);
            slice.position = sp;
            Vector3 face = circle - slice.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) {
                slice.rotation = Quaternion.LookRotation(face.normalized);
            }
        }
        ApplyCamera(s, 0f);
        SpawnCarryStamp();
        PlaceCarryLamps();
        EnsureCarryHud();
        ShowCarryHud(true);
        HideOtherHud(true);
        if (skipPill != null) {
            skipPill.SetActive(false);
        }
        skipRevealed = false;
        // Chapter 6 score under the hold; ending swells on the print.
        MusicDirector.Play("ch6_both", "ch6_score");
        Events.Add("finale-carry|start");
        if (caption != null) {
            caption.text = "";
        }
        if (namePlate != null) {
            namePlate.text = "";
        }
    }

    void HideOtherHud(bool hide) {
        if (!hide) {
            for (int i = 0; i < carryHiddenHud.Count; i++) {
                if (carryHiddenHud[i] != null) {
                    carryHiddenHud[i].enabled = true;
                }
            }
            carryHiddenHud.Clear();
            return;
        }
        carryHiddenHud.Clear();
        RunHud hud = Object.FindAnyObjectByType<RunHud>();
        if (hud != null) {
            Canvas c = hud.GetComponent<Canvas>();
            if (c != null && c.enabled) {
                c.enabled = false;
                carryHiddenHud.Add(c);
            }
        }
        WaveRoster roster = Object.FindAnyObjectByType<WaveRoster>();
        if (roster != null) {
            Canvas c = roster.GetComponentInParent<Canvas>();
            if (c != null && c.enabled && (hud == null || c != hud.GetComponent<Canvas>())) {
                c.enabled = false;
                carryHiddenHud.Add(c);
            }
        }
        AbilityList abl = Object.FindAnyObjectByType<AbilityList>();
        if (abl != null) {
            Canvas c = abl.GetComponentInParent<Canvas>();
            if (c != null && c.enabled && !carryHiddenHud.Contains(c)) {
                c.enabled = false;
                carryHiddenHud.Add(c);
            }
        }
    }

    void TickFinaleCarry() {
        if (!carryArmed || carryResolving) {
            return;
        }
        float now = Time.unscaledTime;
        FinaleCarrySeconds = now - carryStart;
        // Esc still ends the beat cleanly (chapter6 Enter-spam, pause Skip scene).
        if (PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter)) {
            FinishFinaleCarry(true);
            return;
        }
        if (carrySettling) {
            ApplyCamera(reel.shots[shotIndex], 0f);
            LayoutCarryHud();
            if (now >= carrySettleUntil) {
                carrySettling = false;
                Events.Add("finale-hold|ready");
            }
            return;
        }

        bool held = CarryHoldDown();
        if (held) {
            carryHeldOnce = true;
        }
        // Timing window: need a fresh press (release then press, Space, pad A, or the timing icon).
        if (FinaleTimingOpen) {
            bool tap = CarryBatPressed(held);
            if (tap) {
                ResolveNotice(true);
            } else if (now >= carryWindowUntil) {
                ResolveNotice(false);
            }
        } else if (held && carryBats < FinaleNoticeNeed && !FinaleTimingOpen && now >= carryNextNoticeAt && carryKnockUntil < now) {
            SpawnCarryNotice();
        }

        if (held && now >= carryKnockUntil && !FinaleTimingOpen) {
            carryProgress = Mathf.Min(1f, carryProgress + Time.unscaledDeltaTime * FinaleHoldRate);
        }
        FinaleCarryProgress = carryProgress;
        // Soft ring pulse as the stamp nears; Slice claims the print spot mid-carry.
        if (held && Mathf.FloorToInt(carryProgress * 8f) != Mathf.FloorToInt((carryProgress - Time.unscaledDeltaTime * FinaleHoldRate) * 8f)) {
            Fx.Ring(circle + Vector3.up * 0.25f, 3f + carryProgress * 6f, new Color(0.95f, 0.82f, 0.4f, 0.25f + carryProgress * 0.35f), 0.45f);
        }
        if (!carrySliceReacted && carryProgress >= 0.45f && slice != null) {
            carrySliceReacted = true;
            // Step clear of the print and claim it — she needs him to finish the walk.
            Vector3 clear = PolarPoint(210f, 2.6f, -0.2f);
            clear.y = Ground(clear.x, clear.z);
            slice.position = clear;
            Vector3 face = circle - slice.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) {
                slice.rotation = Quaternion.LookRotation(face.normalized);
            }
            if (caption != null && namePlate != null && string.IsNullOrEmpty(caption.text)) {
                namePlate.text = "Slice";
                namePlate.color = new Color(0.75f, 0.7f, 0.95f);
                caption.text = "Mine.";
            }
            if (SliceVoice.Instance != null) {
                SliceVoice.Cutscene("Mine.");
            }
            Events.Add("finale-slice|Mine.");
            VoiceLines++;
        }
        UpdateCarryStampPose();
        UpdateCarryHoldFill();
        LayoutCarryHud();

        bool batsDone = carryBats >= FinaleNoticeNeed;
        bool filled = carryProgress >= 0.999f;
        bool minTime = FinaleCarrySeconds >= FinaleMinSeconds;
        // Ready once the stamp is at the ring and all three notices are handled (or max time).
        bool force = FinaleCarrySeconds >= FinaleMaxSeconds;
        FinaleCarryReady = (filled && batsDone && minTime) || (force && carryHeldOnce);
        if (FinaleCarryReady && carryHeldOnce && !held) {
            FinishFinaleCarry(false);
            return;
        }
        // Failsafe: past max while holding, print so a stuck hold cannot soft-lock. No hold → Esc/Skip only.
        if (FinaleCarrySeconds >= FinaleMaxSeconds + 2f && carryHeldOnce) {
            FinishFinaleCarry(false);
        }
        carryWasHeld = held;
    }

    bool CarryHoldDown() {
        if (UiFit.Touch) {
            // Phone: the big hold pad (or the Swing pad under it) through Mouse0.
            return PlayInput.GetMouseButton(0);
        }
        return PlayInput.GetMouseButton(0)
            || PlayInput.GetKey(KeyCode.JoystickButton0)
            || PlayInput.GetKey(KeyCode.JoystickButton5);
    }

    bool CarryBatPressed(bool heldNow) {
        if (carryTimingPressed) {
            carryTimingPressed = false;
            return true;
        }
        if (PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.JoystickButton0)
            || PlayInput.GetKeyDown(KeyCode.JoystickButton1)) {
            return true;
        }
        // Fresh Mouse0 edge after a release during the window.
        if (heldNow && !carryWasHeld) {
            return true;
        }
        return false;
    }

    void SpawnCarryStamp() {
        if (carryStamp != null) {
            Destroy(carryStamp);
        }
        carryStamp = new GameObject("Finale Carry Stamp");
        DontDestroyOnLoad(carryStamp);
        Vector3 forward = wallit != null ? wallit.forward : Vector3.forward;
        Vector3 start = carryFrom + Vector3.up * 1.6f + forward * 0.6f;
        carryStamp.transform.position = start;
        GameObject head = LevelDress.Put(carryStamp.transform, "gt_press_head", start, 0f, 0.35f);
        if (head != null) {
            head.name = "StampHead";
            carryStampHead = head.transform;
        }
        addressStampGo = carryStamp;
        addressStampHead = carryStampHead;
    }

    void UpdateCarryStampPose() {
        if (carryStamp == null) {
            return;
        }
        Vector3 hand = carryFrom + Vector3.up * 1.55f;
        if (wallit != null) {
            hand = wallit.position + Vector3.up * 1.55f + wallit.forward * 0.55f;
            // Walk Wallit toward the ring as the stamp fills.
            Vector3 want = Vector3.Lerp(carryFrom, PolarPoint(20f, 2.4f, 0.3f), carryProgress);
            want.y = Ground(want.x, want.z) + 0.2f;
            if (wallitBody != null) {
                wallitBody.enabled = false;
            }
            wallit.position = want;
            Vector3 face = circle - wallit.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) {
                wallit.rotation = Quaternion.LookRotation(face.normalized);
            }
            if (wallitBody != null) {
                wallitBody.enabled = true;
            }
            if (CarryHoldDown() && carryKnockUntil < Time.unscaledTime) {
                CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_RUN);
            } else if (Time.unscaledTime < carryKnockUntil) {
                CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.35f);
            } else {
                CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
            }
            hand = wallit.position + Vector3.up * 1.55f + wallit.forward * 0.55f;
        }
        Vector3 land = carryTo + Vector3.up * 0.55f;
        Vector3 pos = Vector3.Lerp(hand, land, carryProgress);
        if (carryStampHead != null) {
            carryStampHead.position = pos;
        } else {
            carryStamp.transform.position = pos;
        }
    }

    void PlaceCarryLamps() {
        ClearFinaleLamps();
        Transform parent = new GameObject("Finale Ring Lamps").transform;
        DontDestroyOnLoad(parent.gameObject);
        carryLamps.Add(parent.gameObject);
        for (int i = 0; i < FinaleLampNeed + 2; i++) {
            float bearing = i * (360f / (FinaleLampNeed + 2));
            Vector3 p = PolarPoint(bearing, 4.2f, 0f);
            p.y = Ground(p.x, p.z);
            GameObject lamp = LevelDress.Put(parent, "fd_lantern", p, bearing + 180f, 0.85f);
            lamp.name = "Finale Lamp " + (i + 1);
            // Start dark: dim the lamp material and keep its light off until the print.
            LevelDress.Tint(lamp, 1, new Color(0.12f, 0.12f, 0.16f, 1f));
            Light lit = LevelDress.PointLight(lamp.transform, new Vector3(0f, 1.4f, 0f), new Color(1f, 0.85f, 0.55f), 5f, 0f);
            carryLamps.Add(lamp);
            carryLampLights.Add(lit);
        }
    }

    void SpawnCarryNotice() {
        carryNoticesSpawned++;
        Vector3 from = circle + Vector3.up * 7.5f + Random.insideUnitSphere * 1.2f;
        from.y = Mathf.Max(from.y, circle.y + 6f);
        Vector3 aim = wallit != null ? wallit.position + Vector3.up * 1.2f : circle + Vector3.up * 1.2f;
        GameObject notice = new GameObject("Finale Notice " + carryNoticesSpawned);
        DontDestroyOnLoad(notice);
        notice.transform.position = from;
        // Sealed Order page: pale paper, red wax disc — last notices off the fallen scroll.
        GameObject sheet = LevelKit.Decal(notice.transform, "Paper", Vector3.zero, new Vector2(0.7f, 0.9f), new Color(0.95f, 0.92f, 0.82f, 0.95f));
        if (sheet != null) {
            sheet.transform.localRotation = Quaternion.Euler(70f, Random.Range(-20f, 20f), Random.Range(-10f, 10f));
        }
        GameObject seal = LevelKit.Decal(notice.transform, "Seal", new Vector3(0.12f, 0.06f, 0.02f), new Vector2(0.22f, 0.22f), new Color(0.72f, 0.12f, 0.1f, 0.95f));
        if (seal != null) {
            seal.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        }
        TextMesh words = LevelKit.Text(notice.transform, "STRIKE", new Vector3(0f, 0.05f, 0f), 0.04f, new Color(0.7f, 0.15f, 0.12f, 0.95f), true);
        if (words != null) {
            words.name = "Strike";
        }
        carryNotices.Add(notice);
        StartCoroutine(FallNotice(notice, from, aim, FinaleNoticeLead));
        OpenTimingWindow();
        Events.Add("finale-notice|" + carryNoticesSpawned);
        GameAudio.Play("paper_flutter", 0.7f, 1.05f);
        GameAudio.Play("seal_crack", 0.35f, 1.2f);
    }

    IEnumerator FallNotice(GameObject notice, Vector3 from, Vector3 to, float seconds) {
        float t0 = Time.unscaledTime;
        while (notice != null && Time.unscaledTime - t0 < seconds) {
            float u = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
            float e = u * u;
            notice.transform.position = Vector3.Lerp(from, to, e);
            notice.transform.Rotate(0f, 90f * Time.unscaledDeltaTime, 40f * Time.unscaledDeltaTime, Space.World);
            yield return null;
        }
    }

    void OpenTimingWindow() {
        FinaleTimingOpen = true;
        carryNeedTap = true;
        // Sweet spot opens as the paper nears Wallit.
        float openAt = Time.unscaledTime + FinaleNoticeLead * 0.55f;
        carryWindowUntil = openAt + FinaleWindow;
        StartCoroutine(RevealTiming(openAt));
        Events.Add("finale-timing|open|" + carryNoticesSpawned);
    }

    IEnumerator RevealTiming(float at) {
        while (Time.unscaledTime < at && FinaleTimingOpen) {
            if (carryTimingIcon != null) {
                carryTimingIcon.gameObject.SetActive(false);
            }
            yield return null;
        }
        if (!FinaleTimingOpen) {
            yield break;
        }
        if (carryTimingIcon != null) {
            carryTimingIcon.gameObject.SetActive(true);
            Color c = carryTimingIcon.color;
            c.a = 1f;
            carryTimingIcon.color = c;
        }
        if (carryTimingButton != null) {
            carryTimingButton.gameObject.SetActive(true);
        }
    }

    void ResolveNotice(bool hit) {
        FinaleTimingOpen = false;
        carryNeedTap = false;
        if (carryTimingIcon != null) {
            carryTimingIcon.gameObject.SetActive(false);
        }
        if (carryTimingButton != null) {
            carryTimingButton.gameObject.SetActive(false);
        }
        // Clear the oldest live notice visual.
        if (carryNotices.Count > 0) {
            GameObject n = carryNotices[0];
            carryNotices.RemoveAt(0);
            if (n != null) {
                if (hit) {
                    Fx.Impact(n.transform.position, 1f, false, Vector3.up);
                    Destroy(n);
                } else {
                    StartCoroutine(MissNotice(n));
                }
            }
        }
        if (hit) {
            carryBats++;
            FinaleCarryBats = carryBats;
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_SWING_1, 0.45f);
            GameAudio.Play("batcrack", 0.9f, 1f);
            GameAudio.Play("batswing", 0.55f, 1.1f);
            Events.Add("finale-bat|" + carryBats);
            carryNextNoticeAt = Time.unscaledTime + 2.6f;
        } else {
            carryMisses++;
            FinaleCarryMisses = carryMisses;
            carryProgress = Mathf.Max(0f, carryProgress - FinaleKnockLoss);
            FinaleCarryProgress = carryProgress;
            carryKnockUntil = Time.unscaledTime + 0.55f;
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.45f);
            GameAudio.Play("hit", 0.7f, 0.9f);
            if (wallit != null) {
                Vector3 away = wallit.position - circle;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) {
                    away = -wallit.forward;
                }
                Vector3 knock = wallit.position + away.normalized * 1.1f;
                knock.y = Ground(knock.x, knock.z) + 0.2f;
                if (wallitBody != null) {
                    wallitBody.enabled = false;
                }
                wallit.position = knock;
                if (wallitBody != null) {
                    wallitBody.enabled = true;
                }
            }
            Events.Add("finale-miss|" + carryMisses);
            carryNextNoticeAt = Time.unscaledTime + 1.8f;
            // Miss does not count as a bat; another notice will fall. Max-time failsafe ends the beat.
        }
    }

    IEnumerator MissNotice(GameObject n) {
        if (n == null) {
            yield break;
        }
        Vector3 from = n.transform.position;
        Vector3 to = from + Vector3.up * 0.4f + (wallit != null ? wallit.forward : Vector3.forward) * 2.5f;
        float t0 = Time.unscaledTime;
        while (n != null && Time.unscaledTime - t0 < 0.4f) {
            float u = Mathf.Clamp01((Time.unscaledTime - t0) / 0.4f);
            n.transform.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        if (n != null) {
            Destroy(n);
        }
    }

    void FinishFinaleCarry(bool skipped) {
        if (carryResolving || !carryArmed) {
            return;
        }
        carryResolving = true;
        FinaleTimingOpen = false;
        carryProgress = 1f;
        FinaleCarryProgress = 1f;
        FinaleCarrySeconds = Mathf.Max(FinaleCarrySeconds, Time.unscaledTime - carryStart);
        // Skip may short-circuit remaining bats; a normal finish keeps the real bat count.
        if (skipped && carryBats < FinaleNoticeNeed) {
            carryBats = FinaleNoticeNeed;
            FinaleCarryBats = carryBats;
        }
        ShowCarryHud(false);
        HideOtherHud(false);
        StartCoroutine(ResolveCarryFinale(skipped));
    }

    IEnumerator ResolveCarryFinale(bool skipped) {
        // Slam the stamp onto the ring and print ADDRESS.
        Vector3 land = carryTo;
        DropAddressStampAt(land, "ADDRESS");
        AddressStampSeen = true;
        shotAddressStamp = true;
        GameAudio.Play("stamp", 1f, 0.7f);
        GameAudio.Play("received", 0.75f);
        Fx.Ring(land + Vector3.up * 0.3f, 10f, new Color(0.95f, 0.82f, 0.35f, 0.65f), 0.7f);
        Events.Add("finale-stamp|ADDRESS");
        Events.Add("address-stamp|finale-carry");
        MusicDirector.Play("ending_both", "ending");
        // Sync Filed Hollow's Door Number set piece with the stamp (ring unrolls into writing + number).
        if (ChapterMap.Current != null && ChapterMap.Current.Own != null) {
            ChapterMap.Current.Own.Trigger();
            Events.Add("finale-door-number");
        }

        // Lamps light one by one around the ring.
        int lit = 0;
        for (int i = 0; i < carryLampLights.Count; i++) {
            Light L = carryLampLights[i];
            if (L == null) {
                continue;
            }
            L.intensity = 1.6f;
            if (i + 1 < carryLamps.Count && carryLamps[i + 1] != null) {
                LevelDress.Tint(carryLamps[i + 1], 1, new Color(1f, 0.88f, 0.55f, 1f));
            }
            lit++;
            FinaleLampsLit = lit;
            Events.Add("finale-lamp|" + lit);
            GameAudio.Play("stamp", 0.35f, 1.3f + lit * 0.05f);
            if (lit >= FinaleLampNeed) {
                // Keep lighting the rest for dress, but AC only needs ≥4.
            }
            yield return new WaitForSecondsRealtime(0.38f);
        }
        FinaleLampsLit = Mathf.Max(FinaleLampsLit, lit);

        // Slice's existing closing line in her voice.
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        string line = "Clean. Good swing, Wallit.";
        if (namePlate != null) {
            namePlate.text = "Slice";
            namePlate.color = new Color(0.75f, 0.7f, 0.95f);
        }
        if (caption != null) {
            caption.text = line;
        }
        float len = SliceVoice.Instance != null ? SliceVoice.Cutscene(line) : 2.2f;
        VoiceLines++;
        Events.Add("voice|S|" + line);
        Events.Add("finale-line|" + line);
        yield return new WaitForSecondsRealtime(Mathf.Max(1.6f, len + 0.2f));

        // Clear leftover notices.
        for (int i = 0; i < carryNotices.Count; i++) {
            if (carryNotices[i] != null) {
                Destroy(carryNotices[i]);
            }
        }
        carryNotices.Clear();

        FinaleCarryActive = false;
        FinaleCarryReady = false;
        carryArmed = false;
        carryResolving = false;
        Events.Add("finale-carry|done|seconds=" + FinaleCarrySeconds.ToString("0.0")
            + "|bats=" + FinaleCarryBats + "|misses=" + FinaleCarryMisses + "|lamps=" + FinaleLampsLit
            + (skipped ? "|skipped" : ""));
        // Advance the reel: the next shot (Pay up) still plays, then the ending card.
        shotStart = Time.unscaledTime - shotLength;
        NextShot();
    }

    void EnsureCarryHud() {
        if (carryHud != null) {
            return;
        }
        EnsureUi();
        carryHud = new GameObject("Finale Carry HUD");
        carryHud.transform.SetParent(uiRoot.transform, false);
        var rt = carryHud.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Large hold target in the lower third (phone AC: ≥88 CSS px, lower third).
        var holdGo = new GameObject("Hold");
        holdGo.transform.SetParent(carryHud.transform, false);
        carryHoldRt = holdGo.AddComponent<RectTransform>();
        Image holdBg = holdGo.AddComponent<Image>();
        holdBg.color = new Color(0.12f, 0.14f, 0.18f, 0.82f);
        holdBg.raycastTarget = true;
        var holdBtn = holdGo.AddComponent<Button>();
        holdBtn.targetGraphic = holdBg;
        // Hold pad drives Mouse0 through EventSystem press (touch + mouse).
        var holdTrig = holdGo.AddComponent<CarryHoldPad>();
        holdTrig.owner = this;

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(holdGo.transform, false);
        var fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(8f, 8f);
        fillRt.offsetMax = new Vector2(-8f, -8f);
        carryHoldFill = fillGo.AddComponent<Image>();
        carryHoldFill.color = new Color(0.92f, 0.78f, 0.35f, 0.85f);
        carryHoldFill.type = Image.Type.Filled;
        carryHoldFill.fillMethod = Image.FillMethod.Horizontal;
        carryHoldFill.fillAmount = 0f;
        carryHoldFill.raycastTarget = false;

        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(holdGo.transform, false);
        carryHint = hintGo.AddComponent<Text>();
        carryHint.font = UiSkin.Font(UiSkin.FontRole.Body);
        carryHint.fontSize = Phone() ? 28 : 26;
        carryHint.alignment = TextAnchor.MiddleCenter;
        carryHint.color = new Color(0.96f, 0.93f, 0.82f, 0.95f);
        carryHint.raycastTarget = false;
        var hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = Vector2.zero;
        hintRt.anchorMax = Vector2.one;
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;

        // Timing icon (bat disc) — icons not words on phone.
        var timingGo = new GameObject("Timing");
        timingGo.transform.SetParent(carryHud.transform, false);
        carryTimingRt = timingGo.AddComponent<RectTransform>();
        carryTimingIcon = timingGo.AddComponent<Image>();
        carryTimingIcon.color = new Color(0.95f, 0.55f, 0.25f, 1f);
        carryTimingButton = timingGo.AddComponent<Button>();
        carryTimingButton.targetGraphic = carryTimingIcon;
        carryTimingButton.onClick.AddListener(() => { carryTimingPressed = true; });
        MenuButtonFx.Add(carryTimingButton);
        timingGo.SetActive(false);

        // Bat glyph: a short thick bar (icon), no words.
        var batGo = new GameObject("BatIcon");
        batGo.transform.SetParent(timingGo.transform, false);
        var batImg = batGo.AddComponent<Image>();
        batImg.color = new Color(0.2f, 0.12f, 0.08f, 1f);
        batImg.raycastTarget = false;
        var batRt = batGo.GetComponent<RectTransform>();
        batRt.anchorMin = new Vector2(0.2f, 0.35f);
        batRt.anchorMax = new Vector2(0.8f, 0.65f);
        batRt.offsetMin = Vector2.zero;
        batRt.offsetMax = Vector2.zero;

        carryHud.SetActive(false);
    }

    void ShowCarryHud(bool on) {
        if (carryHud == null) {
            return;
        }
        carryHud.SetActive(on);
        if (on) {
            LayoutCarryHud();
            if (carryHint != null) {
                // Phone: icon-only hold (no words). Desktop keeps a one-line hint.
                carryHint.text = Phone() ? "" : (FinaleCarryReady ? "Release" : "Hold swing");
            }
        }
    }

    void UpdateCarryHoldFill() {
        if (carryHoldFill != null) {
            carryHoldFill.fillAmount = carryProgress;
        }
        if (carryHint != null && !Phone()) {
            carryHint.text = FinaleCarryReady ? "Release" : (FinaleTimingOpen ? "Tap!" : "Hold swing");
        }
        if (carryHoldRt != null) {
            Image bg = carryHoldRt.GetComponent<Image>();
            if (bg != null) {
                bool held = CarryHoldDown();
                bg.color = held
                    ? new Color(0.22f, 0.28f, 0.18f, 0.9f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.82f);
            }
        }
    }

    void LayoutCarryHud() {
        if (carryHoldRt == null) {
            return;
        }
        bool phone = Phone();
        // Lower third, large thumb target. Canvas units → CSS px via short side / 660 on Tall.
        if (phone) {
            carryHoldRt.anchorMin = new Vector2(0.18f, 0.06f);
            carryHoldRt.anchorMax = new Vector2(0.82f, 0.22f);
        } else {
            carryHoldRt.anchorMin = new Vector2(0.35f, 0.06f);
            carryHoldRt.anchorMax = new Vector2(0.65f, 0.16f);
        }
        carryHoldRt.offsetMin = Vector2.zero;
        carryHoldRt.offsetMax = Vector2.zero;

        if (carryTimingRt != null) {
            carryTimingRt.anchorMin = new Vector2(0.42f, 0.42f);
            carryTimingRt.anchorMax = new Vector2(0.58f, 0.58f);
            if (phone) {
                carryTimingRt.anchorMin = new Vector2(0.36f, 0.40f);
                carryTimingRt.anchorMax = new Vector2(0.64f, 0.56f);
            }
            carryTimingRt.offsetMin = Vector2.zero;
            carryTimingRt.offsetMax = Vector2.zero;
        }

        ReportHoldPromptPx();
    }

    void ReportHoldPromptPx() {
        if (carryHoldRt == null || scaler == null) {
            return;
        }
        Vector3[] corners = new Vector3[4];
        carryHoldRt.GetWorldCorners(corners);
        float widthPx = Mathf.Abs(corners[2].x - corners[0].x) / Mathf.Max(0.01f, UiFit.PixelRatio);
        float centerY = (corners[0].y + corners[2].y) * 0.5f;
        float y01 = centerY / Mathf.Max(1f, Screen.height);
        // Log once when the reading changes (not every frame).
        if (Mathf.Abs(widthPx - FinaleHoldPromptWidthPx) > 0.5f || Mathf.Abs(y01 - FinaleHoldPromptCenterY01) > 0.01f) {
            Events.Add("finale-hold-prompt|w=" + widthPx.ToString("0") + "|y01=" + y01.ToString("0.00"));
        }
        FinaleHoldPromptWidthPx = widthPx;
        FinaleHoldPromptCenterY01 = y01;
    }

    void ClearFinaleLamps() {
        for (int i = 0; i < carryLamps.Count; i++) {
            if (carryLamps[i] != null) {
                Destroy(carryLamps[i]);
            }
        }
        carryLamps.Clear();
        carryLampLights.Clear();
    }

    void ClearFinaleCarryProps() {
        FinaleCarryActive = false;
        FinaleCarryReady = false;
        FinaleTimingOpen = false;
        carryArmed = false;
        carryResolving = false;
        for (int i = 0; i < carryNotices.Count; i++) {
            if (carryNotices[i] != null) {
                Destroy(carryNotices[i]);
            }
        }
        carryNotices.Clear();
        ClearFinaleLamps();
        if (carryStamp != null && carryStamp != addressStampGo) {
            Destroy(carryStamp);
        }
        carryStamp = null;
        carryStampHead = null;
        if (carryHud != null) {
            carryHud.SetActive(false);
        }
        HideOtherHud(false);
    }

    // Touch/mouse hold pad: keeps Mouse0 held while the finger stays on the lower-third target.
    class CarryHoldPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler {
        public CinematicDirector owner;
        bool down;

        public void OnPointerDown(PointerEventData eventData) {
            down = true;
            PlayInput.PadHold(KeyCode.Mouse0, true);
        }

        public void OnPointerUp(PointerEventData eventData) {
            if (!down) {
                return;
            }
            down = false;
            PlayInput.PadHold(KeyCode.Mouse0, false);
        }

        public void OnPointerExit(PointerEventData eventData) {
            // Keep holding if the finger slides; only Up clears it.
        }

        void OnDisable() {
            if (down) {
                down = false;
                PlayInput.PadHold(KeyCode.Mouse0, false);
            }
        }
    }
}
