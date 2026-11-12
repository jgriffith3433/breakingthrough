using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Chapters 4-6 openings and the ADDRESS FILED / RECEIVED endings: paper offer, ADDRESS stamp, six doors around the ring,
// and credits that scroll over the live scene (Hades-style payoff). Existing voice lines and score cues only; no new Gen.
public partial class CinematicDirector {
    // Live credits over the filed Hollow: Wallit and Slice stay in the ring, the score swells, tap jumps to the end card.
    public static void PlayCreditsLive(Action onDone, bool secretBlock) {
        if (Instance == null) {
            Instance = new GameObject("Cinematic Director").AddComponent<CinematicDirector>();
        }
        Instance.BeginCreditsLive(onDone, secretBlock);
    }

    void BeginCreditsLive(Action onDone, bool secretBlock) {
        if (open) {
            End(true);
        }
        creditsOnly = true;
        CreditsLive = true;
        done = onDone;
        creditsStart = Time.unscaledTime;
        CreditsSkipMs = 0f;
        open = false;
        handingOff = false;
        skipRevealed = false;
        viewSet = false;
        chapterHost = true;
        yard = null;
        circle = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        chapterBearing = RouteBearing();
        Events.Add("credits-live|start");
        VoiceDirector.Scene = true;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        MusicDirector.Play("credits");

        PlayerGrowth pg = PlayerGrowth.Instance;
        wallit = pg != null ? pg.transform : null;
        BindWallit();
        Camera main = Camera.main;
        if (main != null) {
            cam = main;
            zelda = main.GetComponent<ZeldaStyleCamera>();
            savedFov = main.fieldOfView;
        }
        sliceCompanion = Companion.Instance;
        if (sliceCompanion != null) {
            slice = sliceCompanion.transform;
            sliceCompanion.Held = true;
        } else if (Musician.Instance != null) {
            slice = Musician.Instance.transform;
        }
        BindSliceAnim();

        // Seat Wallit and Slice in the ring for the payoff, facing each other a little.
        if (wallit != null) {
            Vector3 w = PolarPoint(20f, 2.2f, 0.8f);
            w.y = Ground(w.x, w.z) + 0.2f;
            if (wallitBody != null) {
                wallitBody.enabled = false;
            }
            wallit.position = w;
            wallit.rotation = Quaternion.LookRotation((circle - w).normalized);
            if (wallitBody != null) {
                wallitBody.enabled = true;
            }
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_IDLE);
        }
        if (slice != null) {
            Vector3 s = PolarPoint(200f, 2.0f, -0.6f);
            s.y = Ground(s.x, s.z);
            slice.position = s;
            slice.rotation = Quaternion.LookRotation((circle - s).normalized);
        }

        creditsBody = Story.CreditsTitle + "\n\n" + Story.Credits + (secretBlock ? Story.SecretCredits : "");
        EnsureUi();
        EnsureCreditsScroll();
        uiRoot.SetActive(true);
        if (caption != null) {
            caption.text = "";
        }
        if (namePlate != null) {
            namePlate.text = "";
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ApplyCreditsCamera(0f);
    }

    void TickCreditsOnly() {
        if (PauseMenu.IsOpen) {
            return;
        }
        float u = Mathf.Clamp01((Time.unscaledTime - creditsStart) / 22f);
        ApplyCreditsCamera(u);
        TickCreditsScroll(u);
        TickSkipInput();
        EaseBars(true);
        LayoutUi();
        LayoutCredits();
        // Auto-finish after a full scroll (~22 s) so a silent player still reaches the card.
        if (u >= 1f) {
            EndCreditsLive(false);
        }
    }

    void ApplyCreditsCamera(float u) {
        if (cam == null) {
            return;
        }
        float ang = Mathf.Lerp(-18f, 22f, u);
        Vector3 pivot = circle + Vector3.up * 1.5f;
        Vector3 offset = Quaternion.Euler(12f, ang, 0f) * new Vector3(0f, 0f, -7.5f);
        viewPos = pivot + offset;
        if (Phone()) {
            viewPos = pivot + offset.normalized * 9.2f + Vector3.up * 0.6f;
            cam.fieldOfView = 62f;
        } else {
            cam.fieldOfView = 52f;
        }
        Vector3 dir = pivot - viewPos;
        if (dir.sqrMagnitude < 0.01f) {
            dir = Vector3.forward;
        }
        viewRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        viewSet = true;
    }

    void EnsureCreditsScroll() {
        if (creditsScroll != null) {
            creditsScroll.text = creditsBody;
            return;
        }
        var go = new GameObject("Credits Scroll");
        go.transform.SetParent(uiRoot.transform, false);
        creditsScroll = go.AddComponent<Text>();
        creditsScroll.font = UiSkin.Font(UiSkin.FontRole.Body);
        // Tall phone: ≥24 canvas units → ≥14 CSS px (22 ≈ 13 px per UiFit notes).
        creditsScroll.fontSize = Phone() ? 28 : 32;
        creditsScroll.alignment = TextAnchor.UpperCenter;
        creditsScroll.color = new Color(0.96f, 0.93f, 0.82f, 0.96f);
        creditsScroll.horizontalOverflow = HorizontalWrapMode.Wrap;
        creditsScroll.verticalOverflow = VerticalWrapMode.Overflow;
        creditsScroll.text = creditsBody;
        creditsRt = go.GetComponent<RectTransform>();
        ReportCreditsPx();
    }

    void ReportCreditsPx() {
        if (creditsScroll == null || scaler == null) {
            return;
        }
        // CSS px ≈ fontSize * (Screen.height / referenceResolution.y) for a height-driven Tall canvas.
        float refY = Mathf.Max(1f, scaler.referenceResolution.y);
        CreditsTextPx = creditsScroll.fontSize * (Screen.height / refY);
        // Tall mode also floors the short side at 660: use the larger reading so 390x844 never under-reports.
        if (Phone()) {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float tallFloor = creditsScroll.fontSize * (shortSide / 660f);
            CreditsTextPx = Mathf.Max(CreditsTextPx, tallFloor);
        }
        Events.Add("credits-px|" + CreditsTextPx.ToString("0.0"));
    }

    void LayoutCredits() {
        if (creditsRt == null) {
            return;
        }
        float bottom = Phone() ? 0.18f : 0.14f;
        float top = Phone() ? 0.86f : 0.88f;
        creditsRt.anchorMin = new Vector2(0.1f, bottom);
        creditsRt.anchorMax = new Vector2(0.9f, top);
        creditsRt.offsetMin = Vector2.zero;
        creditsRt.offsetMax = Vector2.zero;
        ReportCreditsPx();
    }

    void TickCreditsScroll(float u) {
        if (creditsScroll == null) {
            return;
        }
        // Soft fade-up then a slow rise of the block (Hades-style over the live frame).
        Color c = creditsScroll.color;
        c.a = Mathf.Clamp01(u * 4f) * 0.96f;
        creditsScroll.color = c;
        if (creditsRt != null) {
            float rise = Mathf.Lerp(-40f, 80f, u);
            creditsRt.anchoredPosition = new Vector2(0f, rise);
        }
    }

    void TickCreditsShot(Shot s, float u) {
        if (!s.credits || shotCredits || u < 0.05f) {
            return;
        }
        shotCredits = true;
        creditsBody = Story.CreditsTitle + "\n\n" + Story.Credits;
        EnsureCreditsScroll();
        creditsStart = Time.unscaledTime;
        CreditsLive = true;
        MusicDirector.Play("credits");
        Events.Add("credits-shot|" + s.id);
    }

    void EndCreditsLive(bool skipped) {
        if (!creditsOnly) {
            return;
        }
        float ms = (Time.unscaledTime - creditsStart) * 1000f;
        if (skipped) {
            CreditsSkipMs = ms;
            Events.Add("credits-skip|" + CreditsSkipMs.ToString("0"));
            Skipped++;
        } else {
            Events.Add("credits-done");
            Finished++;
        }
        creditsOnly = false;
        CreditsLive = false;
        VoiceDirector.Scene = false;
        WallitVoice.Silence();
        SliceVoice.Silence();
        CastVoice.Silence();
        if (creditsScroll != null) {
            creditsScroll.text = "";
        }
        if (uiRoot != null) {
            uiRoot.SetActive(false);
        }
        if (sliceCompanion != null) {
            sliceCompanion.Held = false;
            sliceCompanion = null;
        }
        ClearEndingProps(false);
        if (cam != null) {
            cam.fieldOfView = savedFov;
        }
        Action cb = done;
        done = null;
        // No follow-camera handoff: the end card freezes time next.
        handingOff = false;
        cb?.Invoke();
    }

    void TickPaper(Shot s, float u) {
        if (!s.paper || shotPaper || u < 0.12f) {
            return;
        }
        shotPaper = true;
        if (spider != null) {
            spider.BeginTell();
            Transform paper = spider.transform.Find("Paper");
            if (paper == null) {
                foreach (Transform t in spider.GetComponentsInChildren<Transform>()) {
                    if (t.name == "Paper") {
                        paper = t;
                        break;
                    }
                }
            }
            if (paper != null) {
                // Folded out toward the circle: polite offer, not a strike.
                paper.localPosition = new Vector3(0.35f, 0.75f, 0.85f);
                paper.localRotation = Quaternion.Euler(-50f, 0f, 10f);
                paper.gameObject.SetActive(true);
            }
        }
        GameAudio.Play("paper_flutter", 0.85f);
        Events.Add("paper|" + s.id);
    }

    void TickAddressStamp(Shot s, float u) {
        if (!s.addressStamp || shotAddressStamp || u < 0.1f) {
            return;
        }
        shotAddressStamp = true;
        AddressStampSeen = true;
        Vector3 at = circle + Vector3.up * 0.05f;
        DropAddressStampAt(at, s.doors ? "RECEIVED" : "ADDRESS");
        GameAudio.Play("stamp", 1f, 0.7f);
        GameAudio.Play("received", 0.7f);
        Fx.Ring(at + Vector3.up * 0.3f, 10f, new Color(0.95f, 0.82f, 0.35f, 0.65f), 0.7f);
        Events.Add("address-stamp|" + s.id);
    }

    void DropAddressStampAt(Vector3 land, string word) {
        if (addressStampGo != null) {
            Destroy(addressStampGo);
        }
        addressStampGo = new GameObject("Cinematic Address Stamp");
        addressStampGo.transform.position = land + Vector3.up * 7f;
        addressStampGo.transform.rotation = Quaternion.identity;
        // Ink disc on the grass: readable upright, never LookAt / never negative scale.
        GameObject ink = LevelKit.Decal(addressStampGo.transform, "Ink", new Vector3(0f, 0.04f, 0f), new Vector2(3.2f, 3.2f), new Color(0.78f, 0.18f, 0.16f, 0.55f));
        if (ink != null) {
            ink.transform.SetParent(null, true);
            ink.transform.position = land + Vector3.up * 0.04f;
            ink.transform.SetParent(addressStampGo.transform, true);
        }
        // flat=true lays the word on the grass readable from above (never LookAt / never negative scale).
        TextMesh words = LevelKit.Text(addressStampGo.transform, word, new Vector3(0f, 0.12f, 0f), 0.09f, new Color(1f, 0.92f, 0.75f, 0.95f), true);
        words.name = "Words";
        GameObject head = LevelDress.Put(addressStampGo.transform, "gt_press_head", land + Vector3.up * 7f, 0f, 0.5f);
        head.name = "StampHead";
        addressStampHead = head.transform;
        StartCoroutine(SlamAddressHead(land + Vector3.up * 0.55f));
        if (wallit != null) {
            CharacterMovement.PlayWallitClip(CharacterMovement.CLIP_HIT_REACT, 0.35f);
        }
    }

    IEnumerator SlamAddressHead(Vector3 land) {
        if (addressStampHead == null) {
            yield break;
        }
        Vector3 from = addressStampHead.position;
        float t0 = Time.unscaledTime;
        while (addressStampHead != null && Time.unscaledTime - t0 < 0.5f) {
            float k = Mathf.Clamp01((Time.unscaledTime - t0) / 0.5f);
            float e = k * k;
            addressStampHead.position = Vector3.Lerp(from, land, e);
            yield return null;
        }
        if (addressStampHead != null) {
            addressStampHead.position = land;
        }
    }

    void TickDoors(Shot s, float u) {
        if (!s.doors || shotDoors || u < 0.08f) {
            return;
        }
        shotDoors = true;
        RaiseDoors(6);
        GameAudio.Play("slot_shutter", 0.75f);
        Events.Add("doors|" + s.id + "|count=" + DoorsShown);
    }

    void TickKeepsakes(Shot s, float u) {
        if (!s.keepsakes || shotKeepsakes || u < 0.06f) {
            return;
        }
        shotKeepsakes = true;
        PlaceKeepsakes();
    }

    // Six lost things in a row inside the ring (postcard, key, hat, paper, leaf, stamp) — seen, not only narrated.
    void PlaceKeepsakes() {
        Transform parent = new GameObject("Cinematic Keepsakes").transform;
        DontDestroyOnLoad(parent.gameObject);
        doorGos.Add(parent.gameObject);
        Color[] tints = {
            new Color(0.92f, 0.78f, 0.55f, 0.95f),
            new Color(0.75f, 0.7f, 0.35f, 0.95f),
            new Color(0.45f, 0.35f, 0.28f, 0.95f),
            new Color(0.95f, 0.95f, 0.9f, 0.95f),
            new Color(0.35f, 0.55f, 0.3f, 0.95f),
            new Color(0.7f, 0.2f, 0.18f, 0.95f),
        };
        Vector2[] sizes = {
            new Vector2(0.55f, 0.35f), new Vector2(0.2f, 0.45f), new Vector2(0.5f, 0.35f),
            new Vector2(0.4f, 0.5f), new Vector2(0.35f, 0.25f), new Vector2(0.35f, 0.35f),
        };
        for (int i = 0; i < 6; i++) {
            float side = (i - 2.5f) * 0.55f;
            Vector3 p = PolarPoint(0f, 0.6f, side);
            p.y = Ground(p.x, p.z) + 0.03f;
            GameObject bit = LevelKit.Decal(parent, "Keep " + (i + 1), Vector3.zero, sizes[i], tints[i]);
            bit.transform.position = p;
            bit.transform.rotation = Quaternion.identity;
        }
        Events.Add("keepsakes|6");
    }

    void RaiseDoors(int count) {
        ClearEndingProps(true);
        count = Mathf.Clamp(count, 1, 8);
        Transform parent = new GameObject("Cinematic Doors").transform;
        DontDestroyOnLoad(parent.gameObject);
        doorGos.Add(parent.gameObject);
        for (int i = 0; i < count; i++) {
            float bearing = i * (360f / count);
            Vector3 p = PolarPoint(bearing, 7.5f, 0f);
            p.y = Ground(p.x, p.z);
            GameObject door = LevelDress.Put(parent, "fd_free_door", p, bearing + 180f, 0.28f);
            door.name = "Lost Door " + (i + 1);
            // Rise out of the grass over half a second.
            Vector3 land = door.transform.position;
            door.transform.position = land + Vector3.down * 3.2f;
            StartCoroutine(RiseDoor(door.transform, land, i * 0.08f));
            doorGos.Add(door);
        }
        DoorsShown = count;
    }

    IEnumerator RiseDoor(Transform door, Vector3 land, float delay) {
        if (door == null) {
            yield break;
        }
        float t0 = Time.unscaledTime + delay;
        while (Time.unscaledTime < t0) {
            yield return null;
        }
        Vector3 from = door.position;
        float start = Time.unscaledTime;
        while (door != null && Time.unscaledTime - start < 0.55f) {
            float k = Mathf.Clamp01((Time.unscaledTime - start) / 0.55f);
            float e = 1f - (1f - k) * (1f - k);
            door.position = Vector3.Lerp(from, land, e);
            yield return null;
        }
        if (door != null) {
            door.position = land;
        }
    }

    // keepDoors: RECEIVED leaves the six doors up under the credits; openings clear everything.
    void ClearEndingProps(bool keepDoors) {
        if (addressStampGo != null) {
            Destroy(addressStampGo);
            addressStampGo = null;
            addressStampHead = null;
        }
        ClearFinaleCarryProps();
        if (!keepDoors) {
            for (int i = 0; i < doorGos.Count; i++) {
                if (doorGos[i] != null) {
                    Destroy(doorGos[i]);
                }
            }
            doorGos.Clear();
            DoorsShown = 0;
        }
        if (postmarkGo != null) {
            Destroy(postmarkGo);
            postmarkGo = null;
            postmarkHead = null;
        }
    }
}
