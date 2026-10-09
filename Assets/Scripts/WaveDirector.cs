using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Infinite turkey waves. The clock is the only way to lose.
public class WaveDirector : MonoBehaviour {
    public static int Wave { get; private set; } = 1;

    enum Phase {
        Intro,
        Combat,
        Clear,
        Lost
    }

    static int bestCleared;

    Phase phase = Phase.Intro;
    float phaseLeft = 1.6f;
    float timeLeft = 22f;
    float spare;
    float spawnIn;
    float spawnAngle;
    int toSpawn;
    int cleared;
    int spawnSerial;
    bool bossSpawned;
    bool pausedGame;
    bool record;

    Text timerText;
    Text waveText;
    Text detailText;
    GameObject lostPanel;
    Text lostBody;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (FindAnyObjectByType<WaveDirector>() != null) {
            return;
        }

        var go = new GameObject("Wave Director");
        go.AddComponent<WaveDirector>();
    }

    void Start() {
        Enemy[] already = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in already) {
            Destroy(enemy.gameObject);
        }

        BuildHud();
        Wave = 1;
        BeginIntro();
    }

    void OnDestroy() {
        if (pausedGame) {
            Time.timeScale = 1f;
        }
    }

    void Update() {
        if (phase == Phase.Lost) {
            if (Input.GetKeyDown(KeyCode.R)) {
                Time.timeScale = 1f;
                pausedGame = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        if (phase == Phase.Intro) {
            phaseLeft -= Time.deltaTime;
            if (phaseLeft <= 0f) {
                BeginCombat();
            }
        } else if (phase == Phase.Clear) {
            phaseLeft -= Time.deltaTime;
            if (phaseLeft <= 0f) {
                BeginIntro();
            }
        } else {
            TickCombat();
        }

        RefreshHud();
    }

    void BeginIntro() {
        phase = Phase.Intro;
        phaseLeft = Wave == 1 ? 1.8f : 1.35f;
        bossSpawned = false;
        toSpawn = 0;
        timeLeft = TimeFor(Wave, CountFor(Wave));
        spawnAngle = Random.Range(0f, 360f);
    }

    void BeginCombat() {
        phase = Phase.Combat;
        toSpawn = CountFor(Wave);
        spawnIn = 0f;
        spawnSerial = 0;
        bossSpawned = false;
        timeLeft = TimeFor(Wave, toSpawn);
    }

    void TickCombat() {
        if (toSpawn > 0) {
            spawnIn -= Time.deltaTime;
            if (spawnIn <= 0f) {
                SpawnOne();
                toSpawn--;
                spawnIn = 0.42f;
            }
        }

        if (toSpawn == 0 && !AnyAlive()) {
            spare = timeLeft;
            cleared = Wave;
            record = Wave > bestCleared;
            if (record) {
                bestCleared = Wave;
            }
            Wave++;
            phase = Phase.Clear;
            phaseLeft = 1.7f;
            return;
        }

        float drain = GobblerAlive() ? 1.65f : 1f;
        timeLeft -= Time.deltaTime * drain;
        if (timeLeft <= 0f) {
            timeLeft = 0f;
            Lose();
        }
    }

    void Lose() {
        phase = Phase.Lost;
        pausedGame = true;
        Time.timeScale = 0f;
        lostPanel.SetActive(true);
        if (cleared <= 0) {
            lostBody.text = bestCleared > 0
                ? "The first turkey won.\n\nBest cleared: " + bestCleared + "\n\nPress R to try again"
                : "The first turkey won.\n\nPress R to try again";
        } else {
            string waves = cleared == 1 ? "1 wave" : cleared + " waves";
            string bestLine = record ? "New best." : "Best cleared: " + bestCleared;
            lostBody.text = "You cleared " + waves + ".\n\n" + bestLine + "\n\nPress R to try again";
        }
    }

    void SpawnOne() {
        Transform player = null;
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) {
            player = playerGo.transform;
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        float angle = (spawnAngle + spawnSerial * 137.5f) * Mathf.Deg2Rad;
        spawnSerial++;
        float radius = Random.Range(8.2f, 12f);
        Vector3 spot = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        spot.y = GroundNear(spot, origin.y) + 0.55f;

        TurkeyTrait primary;
        TurkeyTrait secondary;
        Roll(Wave, out primary, out secondary);

        var go = new GameObject("Turkey");
        go.tag = "enemy";
        go.transform.position = spot;
        Vector3 look = origin - spot;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) {
            go.transform.rotation = Quaternion.LookRotation(look);
        }

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.Apply(primary, secondary);
    }

    void Roll(int wave, out TurkeyTrait primary, out TurkeyTrait secondary) {
        primary = TurkeyTrait.None;
        secondary = TurkeyTrait.None;
        if (wave % 5 == 0 && !bossSpawned) {
            primary = TurkeyTrait.Gobbler;
            bossSpawned = true;
            return;
        }

        TurkeyTrait newest = Newest(wave);
        if (newest == TurkeyTrait.None) {
            return;
        }

        primary = Random.value < 0.7f ? newest : RandomFrom(wave);
        if (wave >= 8 && Random.value < 0.42f) {
            secondary = RandomFrom(wave);
            if (secondary == primary) {
                secondary = TurkeyTrait.None;
            }
        }
    }

    static TurkeyTrait Newest(int wave) {
        if (wave >= 36) {
            return TurkeyTrait.Titan;
        }
        if (wave >= 26) {
            return TurkeyTrait.Swarmer;
        }
        if (wave >= 18) {
            return TurkeyTrait.Charger;
        }
        if (wave >= 14) {
            return TurkeyTrait.Blinker;
        }
        if (wave >= 11) {
            return TurkeyTrait.Splitter;
        }
        if (wave >= 9) {
            return TurkeyTrait.Glider;
        }
        if (wave >= 7) {
            return TurkeyTrait.Juker;
        }
        if (wave >= 5) {
            return TurkeyTrait.Bruiser;
        }
        if (wave >= 3) {
            return TurkeyTrait.Hopper;
        }
        if (wave >= 2) {
            return TurkeyTrait.Sprinter;
        }
        return TurkeyTrait.None;
    }

    static TurkeyTrait RandomFrom(int wave) {
        var pool = new List<TurkeyTrait> { TurkeyTrait.None };
        if (wave >= 2) {
            pool.Add(TurkeyTrait.Sprinter);
        }
        if (wave >= 3) {
            pool.Add(TurkeyTrait.Hopper);
        }
        if (wave >= 5) {
            pool.Add(TurkeyTrait.Bruiser);
        }
        if (wave >= 7) {
            pool.Add(TurkeyTrait.Juker);
        }
        if (wave >= 9) {
            pool.Add(TurkeyTrait.Glider);
        }
        if (wave >= 11) {
            pool.Add(TurkeyTrait.Splitter);
        }
        if (wave >= 14) {
            pool.Add(TurkeyTrait.Blinker);
        }
        if (wave >= 18) {
            pool.Add(TurkeyTrait.Charger);
        }
        if (wave >= 26) {
            pool.Add(TurkeyTrait.Swarmer);
        }
        if (wave >= 36) {
            pool.Add(TurkeyTrait.Titan);
        }
        return pool[Random.Range(0, pool.Count)];
    }

    static int CountFor(int wave) {
        if (wave <= 8) {
            return wave;
        }
        return Mathf.Min(10, 8 + (wave - 8) / 4);
    }

    static float TimeFor(int wave, int count) {
        if (wave <= 1) {
            return 22f;
        }

        float seconds = 12f + count * 6f;
        float heat = Mathf.Clamp01((wave - 1) / 16f);
        seconds *= Mathf.Lerp(1.05f, 0.75f, heat);
        if (wave % 5 == 0) {
            seconds += 10f;
        }
        return Mathf.Max(14f, seconds);
    }

    static string Flavor(int wave) {
        if (wave <= 1) {
            return "One turkey. The clock is the only way to lose. Each one you drop makes you bigger.";
        }
        if (wave == 5) {
            return "Your bow fires on its own now. Click still shoots.";
        }
        if (wave % 5 == 0) {
            return "A Gobbler. Huge, and it eats the clock.";
        }

        switch (Newest(wave)) {
            case TurkeyTrait.Sprinter:
                return "Sprinters. They close the gap.";
            case TurkeyTrait.Hopper:
                return "Hoppers. They fling themselves, then fall.";
            case TurkeyTrait.Bruiser:
                return "Bruisers. They take more arrows.";
            case TurkeyTrait.Juker:
                return "Jukers. They zig-zag.";
            case TurkeyTrait.Glider:
                return "Gliders. They stay up longer, then drop.";
            case TurkeyTrait.Splitter:
                return "Splitters. Drop one, and two pop out.";
            case TurkeyTrait.Blinker:
                return "Blinkers. They vanish sideways.";
            case TurkeyTrait.Charger:
                return "Chargers. They rush you.";
            case TurkeyTrait.Swarmer:
                return "Swarmers. Small, fast, one arrow.";
            case TurkeyTrait.Titan:
                return "Titans. Slow, and they take a beating.";
            default:
                return "Clear them before the clock runs out.";
        }
    }

    bool AnyAlive() {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive) {
                return true;
            }
        }
        return false;
    }

    bool GobblerAlive() {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive && enemy.IsGobbler) {
                return true;
            }
        }
        return false;
    }

    static float GroundNear(Vector3 spot, float preferY) {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(spot.x, 40f, spot.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = preferY;
        bool found = false;
        foreach (RaycastHit hit in hits) {
            if (hit.collider == null || hit.collider.GetComponentInParent<Enemy>() != null) {
                continue;
            }
            float delta = Mathf.Abs(hit.point.y - preferY);
            if (delta < best) {
                best = delta;
                y = hit.point.y;
                found = true;
            }
        }
        return found ? y : preferY;
    }

    void RefreshHud() {
        int shown = Mathf.CeilToInt(Mathf.Max(timeLeft, 0f));
        timerText.text = shown.ToString();
        if (phase == Phase.Lost || timeLeft <= 5f) {
            timerText.color = new Color(1f, 0.35f, 0.28f);
        } else if (timeLeft <= 10f) {
            timerText.color = new Color(1f, 0.82f, 0.35f);
        } else if (phase == Phase.Clear) {
            timerText.color = new Color(0.55f, 0.95f, 0.55f);
        } else {
            timerText.color = new Color(1f, 0.97f, 0.9f);
        }

        if (phase == Phase.Intro) {
            waveText.text = "Wave " + Wave;
            detailText.text = Flavor(Wave);
        } else if (phase == Phase.Clear) {
            timerText.text = Mathf.CeilToInt(spare).ToString();
            waveText.text = "Wave clear";
            detailText.text = Mathf.CeilToInt(spare) + "s to spare";
        } else if (phase == Phase.Lost) {
            waveText.text = "Time's up";
            detailText.text = "";
        } else if (!string.IsNullOrEmpty(PlayerGrowth.Banner)) {
            waveText.text = "Wave " + Wave + "   ·   " + (AliveCount() + toSpawn) + " left";
            detailText.text = PlayerGrowth.Banner;
        } else if (GobblerAlive()) {
            waveText.text = "Wave " + Wave + "   ·   " + (AliveCount() + toSpawn) + " left";
            detailText.text = "The Gobbler is eating the clock.";
            detailText.color = new Color(1f, 0.45f, 0.32f);
            return;
        } else {
            waveText.text = "Wave " + Wave + "   ·   " + (AliveCount() + toSpawn) + " left";
            detailText.text = Wave >= 5 ? Flavor(Wave) + "  Auto-shoot is on." : Flavor(Wave);
        }

        detailText.color = new Color(1f, 0.96f, 0.88f, 0.92f);
    }

    int AliveCount() {
        int count = 0;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive) {
                count++;
            }
        }
        return count;
    }

    void BuildHud() {
        var canvasGo = new GameObject("Wave Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText = MakeText(canvasGo.transform, font, "22", 84, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 100f));
        waveText = MakeText(canvasGo.transform, font, "Wave 1", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(900f, 46f));
        detailText = MakeText(canvasGo.transform, font, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(1100f, 40f));

        lostPanel = new GameObject("Lost");
        lostPanel.transform.SetParent(canvasGo.transform, false);
        var panel = lostPanel.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.04f, 0.04f, 0.82f);
        panel.raycastTarget = false;
        var panelRect = lostPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 420f);

        var title = MakeText(lostPanel.transform, font, "Time's up", 68, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(640f, 90f));
        title.color = new Color(1f, 0.45f, 0.35f);
        lostBody = MakeText(lostPanel.transform, font, "", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(620f, 220f));
        lostPanel.SetActive(false);
    }

    static Text MakeText(Transform parent, Font font, string value, int size, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorPoint;
        rect.anchorMax = anchorPoint;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }
}
