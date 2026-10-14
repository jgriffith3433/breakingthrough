using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum WaveEvent {
    None,
    Stampede,
    AirRaid,
    Feast,
    GoldenHour,
    EliteSurge,
    BloodMoon
}

// Infinite turkey waves. You lose when the musician falls.
public class WaveDirector : MonoBehaviour {
    public static int Wave { get; private set; } = 1;
    public static bool IsBuilding { get; private set; }
    public static bool IsLost { get; private set; }
    public static WaveEvent Event { get; private set; }
    // Shown on the menu after a run ends. Lives only until the game closes.
    public static string LastRun { get; private set; }

    public static string EventName {
        get {
            switch (Event) {
                case WaveEvent.Stampede: return "Stampede: everything runs faster";
                case WaveEvent.AirRaid: return "Air Raid: they fly more";
                case WaveEvent.Feast: return "Feast: twice the turkeys, half the health";
                case WaveEvent.GoldenHour: return "Golden Hour: double feathers";
                case WaveEvent.EliteSurge: return "Elite Surge: crested birds everywhere";
                case WaveEvent.BloodMoon: return "Blood Moon: tougher birds, more feathers";
                default: return "";
            }
        }
    }

    public static float SpeedScale {
        get { return Event == WaveEvent.Stampede ? 1.35f : Event == WaveEvent.BloodMoon ? 1.1f : 1f; }
    }

    public static float FlightScale {
        get { return Event == WaveEvent.AirRaid ? 2f : 1f; }
    }

    public static float FeatherScale {
        get { return Event == WaveEvent.GoldenHour ? 2f : Event == WaveEvent.BloodMoon ? 1.6f : Event == WaveEvent.Feast ? 0.8f : 1f; }
    }

    static float HealthMult {
        get { return Event == WaveEvent.Feast ? 0.5f : Event == WaveEvent.BloodMoon ? 1.35f : 1f; }
    }

    enum Phase {
        Intro,
        Combat,
        Break,
        Build,
        Lost
    }

    Phase phase = Phase.Intro;
    float phaseLeft = 1.6f;
    float spawnIn;
    float spawnAngle;
    readonly List<FowlKind> pending = new List<FowlKind>();
    int cleared;
    int spawnSerial;
    bool debutAnnounced;
    bool toldLoadout;
    bool pausedGame;
    bool record;
    float returnReadyAt;
    bool bossPending;
    float goldenIn = -1f;

    Text timerText;
    Text waveText;
    Text detailText;
    Text coinText;
    Image purseFill;
    GameObject lostPanel;
    Text lostBody;

    void Start() {
        Instance = this;
        Enemy[] already = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in already) {
            Destroy(enemy.gameObject);
        }

        BuildHud();
        IsLost = false;
        Event = WaveEvent.None;
        MetaProgress.BeginRun();
        FortStock.BeginRun();
        Combo.BeginRun();
        Combat.BeginRun();
        RunLog.BeginRun();
        RunBoons.BeginRun();
        Wave = MetaProgress.TakeStartWave();
        MetaProgress.NoteReached(Wave);
        BeginIntro();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (pausedGame) {
            Time.timeScale = 1f;
        }
    }

    void Update() {
        if (phase == Phase.Lost) {
            if (Time.unscaledTime >= returnReadyAt && Input.anyKeyDown) {
                Time.timeScale = 1f;
                pausedGame = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                string scene = Application.CanStreamedLevelBeLoaded(MetaProgress.MenuScene)
                    ? MetaProgress.MenuScene
                    : MetaProgress.GameScene;
                SceneManager.LoadScene(scene);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) {
            GoToWave(Wave + 1);
            return;
        }
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus)) {
            GoToWave(Mathf.Max(1, Wave - 1));
            return;
        }

        if (phase == Phase.Intro) {
            phaseLeft -= Time.deltaTime;
            if (phaseLeft <= 0f) {
                BeginCombat();
            }
        } else if (phase == Phase.Break || phase == Phase.Build) {
            phaseLeft -= Time.deltaTime;
            IsBuilding = phase == Phase.Build;
            if (!LevelUpScreen.IsOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) {
                phaseLeft = 0f;
            }
            if (phaseLeft <= 0f) {
                IsBuilding = false;
                BeginCombat();
            }
        } else {
            TickCombat();
        }

        MetaProgress.TickToast();
        RefreshHud();
    }


    void BeginIntro() {
        phase = Phase.Intro;
        phaseLeft = Wave == 1 ? 1.8f : 1.35f;
        pending.Clear();
        IsBuilding = false;
        spawnAngle = Random.Range(0f, 360f);
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.SyncToWave(Wave);
        }
    }

    void BeginCombat() {
        phase = Phase.Combat;
        RollEvent();
        FillPending(Wave);
        spawnIn = 0.2f;
        spawnSerial = 0;
        debutAnnounced = false;
        IsBuilding = false;
        bossPending = Wave % 10 == 0;
        goldenIn = Wave >= Tuning.GoldenFirstWave && (Wave - Tuning.GoldenFirstWave) % Tuning.GoldenEvery == 0
            ? Random.Range(4f, 10f)
            : -1f;
        RunLog.BeginWave();
        if (Event != WaveEvent.None) {
            UpgradeLook.Say(EventName, 2.6f, 230f);
            GameAudio.Play("launch", 0.4f, 0.7f);
        }
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.SyncToWave(Wave);
        }
        if (!toldLoadout) {
            toldLoadout = true;
            UpgradeLook.AnnounceLoadout();
        }
    }

    void RollEvent() {
        Event = WaveEvent.None;
        if (Wave < Tuning.EventFirstWave || Wave % 10 == 0 || IsFortWave(Wave) || Random.value > Tuning.EventChance) {
            return;
        }
        var options = new List<WaveEvent> { WaveEvent.Stampede, WaveEvent.Feast, WaveEvent.GoldenHour, WaveEvent.EliteSurge };
        if (Wave >= 8) {
            options.Add(WaveEvent.AirRaid);
        }
        if (Wave >= 15) {
            options.Add(WaveEvent.BloodMoon);
        }
        Event = options[Random.Range(0, options.Count)];
    }

    // A boss calls for help: a few turkeys burst out around it.
    public void BossCall(Vector3 at, int count) {
        if (phase != Phase.Combat) {
            return;
        }
        for (int i = 0; i < count; i++) {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 spot = at + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Random.Range(3f, 6f);
            spot.y = GroundNear(spot, at.y) + 0.5f;
            Spawn(FowlRoster.ForSpawn(FowlKind.Turkey, Wave), spot, false);
        }
    }

    void SpawnGolden() {
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 spot = origin + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Random.Range(22f, 32f);
        spot.y = GroundNear(spot, origin.y) + 0.5f;
        Enemy golden = Spawn(FowlRoster.ForSpawn(FowlKind.Turkey, Wave), spot, false);
        golden.MakeGolden();
        golden.transform.localScale *= 1.25f;
        UpgradeLook.Say("A Golden Gobbler! Catch it for treasure", 2.6f, 120f);
        GameAudio.Play("coin", 0.8f, 1.5f);
    }

    Enemy Spawn(FowlDef def, Vector3 spot, bool rollElite) {
        var go = new GameObject(def.Name);
        go.tag = "enemy";
        go.transform.position = spot;
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        Vector3 look = origin - spot;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) {
            go.transform.rotation = Quaternion.LookRotation(look);
        }
        Enemy enemy = go.AddComponent<Enemy>();
        enemy.ApplyFowl(def);
        enemy.HitsToDrop = Mathf.Max(1, Mathf.RoundToInt(enemy.HitsToDrop * HealthMult));
        if (rollElite && def.Kind == FowlKind.Turkey) {
            float chance = Tuning.EliteChance(Wave) * (Event == WaveEvent.EliteSurge ? 3f : 1f);
            if (Random.value < chance) {
                enemy.MakeElite(PickTrait());
            }
        }
        return enemy;
    }

    TurkeyTrait PickTrait() {
        var traits = new List<TurkeyTrait> { TurkeyTrait.Sprinter, TurkeyTrait.Bruiser };
        if (Wave >= 4) traits.Add(TurkeyTrait.Glider);
        if (Wave >= 6) traits.Add(TurkeyTrait.Splitter);
        if (Wave >= 8) traits.Add(TurkeyTrait.Charger);
        if (Wave >= 10) traits.Add(TurkeyTrait.Blinker);
        if (Wave >= 12) traits.Add(TurkeyTrait.Swarmer);
        if (Wave >= 15) traits.Add(TurkeyTrait.Titan);
        return traits[Random.Range(0, traits.Count)];
    }

    void TickCombat() {
        if (goldenIn > 0f) {
            goldenIn -= Time.deltaTime;
            if (goldenIn <= 0f) {
                goldenIn = -1f;
                SpawnGolden();
            }
        }

        if (pending.Count > 0) {
            int[] alive = new int[KindCount];
            int[] caps = new int[KindCount];
            CountAlive(alive);
            ScreenCaps(Wave, caps);
            int pick = PickSpawn(alive, caps);
            if (pick >= 0) {
                spawnIn -= Time.deltaTime;
                if (spawnIn <= 0f) {
                    spawnIn = SpawnOne(pick);
                }
            }
        }

        if (pending.Count == 0 && goldenIn < 0f && !AnyAlive()) {
            cleared = Wave;
            UpgradeLook.WaveCleared(cleared);
            RunLog.EndWave(Wave, "cleared");
            MetaProgress.AddCoins(Tuning.CoinsForWave(Wave), Vector3.zero);
            Pickups.Vacuum();
            if (Musician.Instance != null) {
                Musician.Instance.Heal(6 * RunBoons.Rank(BoonId.Ovation));
            }
            Event = WaveEvent.None;
            record = Wave > MetaProgress.BestWave;
            if (record) {
                MetaProgress.SetBest(Wave);
            }
            Wave++;
            MetaProgress.NoteReached(Wave);
            if (IsFortWave(Wave)) {
                phase = Phase.Build;
                phaseLeft = Tuning.BuildSeconds;
                IsBuilding = true;
            } else {
                phase = Phase.Break;
                phaseLeft = Tuning.BreakSeconds;
                IsBuilding = false;
            }
            if (PlayerGrowth.Instance != null) {
                PlayerGrowth.Instance.SyncToWave(Wave);
            }
            return;
        }
    }

    public void DevSkipBreak() {
        if (phase == Phase.Break || phase == Phase.Build) {
            phaseLeft = Mathf.Min(phaseLeft, 0.6f);
        }
    }

    public void MusicianDown() {
        if (phase == Phase.Lost) {
            return;
        }
        Lose();
    }

    void GoToWave(int wave) {
        Time.timeScale = 1f;
        pausedGame = false;
        IsLost = false;
        if (lostPanel != null) {
            lostPanel.SetActive(false);
        }
        Wave = Mathf.Max(1, wave);
        MetaProgress.NoteReached(Wave);
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null) {
                Destroy(enemy.gameObject);
            }
        }
        if (IsFortWave(Wave)) {
            phase = Phase.Build;
            phaseLeft = Tuning.BuildSeconds;
            pending.Clear();
            IsBuilding = true;
            if (PlayerGrowth.Instance != null) {
                PlayerGrowth.Instance.SyncToWave(Wave);
            }
            return;
        }
        BeginCombat();
    }

    public static bool IsFortWave(int wave) {
        return wave >= 5 && (wave - 5) % 10 == 0;
    }

    void Lose() {
        RunLog.EndWave(Wave, "lost");
        phase = Phase.Lost;
        IsLost = true;
        IsBuilding = false;
        pausedGame = true;
        Time.timeScale = 0f;
        lostPanel.SetActive(true);
        returnReadyAt = Time.unscaledTime + 0.4f;
        string waves = cleared <= 0
            ? "No waves cleared."
            : cleared == 1 ? "Cleared 1 wave." : "Cleared " + cleared + " waves.";
        string bestLine = record
            ? "New best: " + MetaProgress.BestWave + "."
            : MetaProgress.BestWave > 0 ? "Best cleared: " + MetaProgress.BestWave + "." : "No best yet.";
        string earned = MetaProgress.CoinsThisRun == 0
            ? "No new coins."
            : "+" + MetaProgress.CoinsThisRun + (MetaProgress.CoinsThisRun == 1 ? " coin this run." : " coins this run.");
        string purse = MetaProgress.Coins == 1 ? "1 coin to spend." : MetaProgress.Coins + " coins to spend.";
        string run = "Level " + RunBoons.Level + "   ·   Best streak " + Combo.Best;
        string boons = RunBoons.Summary();
        LastRun = "Fell on wave " + Wave + ", level " + RunBoons.Level + ", streak " + Combo.Best + ", +" + MetaProgress.CoinsThisRun + " coins.\n" + boons;
        lostBody.text = waves + "\n" + bestLine + "\n" + run + "\n\n" + earned + "\n" + purse
            + (string.IsNullOrEmpty(boons) ? "" : "\n\n<size=22>" + boons + "</size>") + "\n\nPress any key";
    }

    float SpawnOne(int index) {
        Transform player = null;
        if (Musician.Instance != null) {
            player = Musician.Instance.transform;
        } else {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) {
                player = playerGo.transform;
            }
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        spawnSerial++;
        if (index < 0 || index >= pending.Count) {
            return 1.8f;
        }
        FowlKind kind = pending[index];
        pending.RemoveAt(index);
        FowlDef def = FowlRoster.ForSpawn(kind, Wave);
        bool makeBoss = bossPending && kind == BossKind(Wave);
        bool turkey = kind == FowlKind.Turkey;
        float waveT = Mathf.Max(0, Wave - 1);
        float reach = waveT * 0.48f;
        float band = (turkey ? 18f : 16f) + waveT * 0.22f;
        float inner = (turkey ? 30f : 24f) + reach + Mathf.Max(0f, def.Scale - 1f) * 0.35f;
        float outer = Mathf.Min(inner + band, 150f);
        inner = Mathf.Min(inner, outer - 10f);
        float jitter = turkey ? 110f : 80f;
        float gap = turkey ? 8f + waveT * 0.08f : Mathf.Max(7f, def.Scale * 0.85f);
        Vector3 spot = origin;
        float bestClear = -1f;
        for (int attempt = 0; attempt < 18; attempt++) {
            float spread = (spawnAngle + spawnSerial * 137.5f + Random.Range(-jitter, jitter)) * Mathf.Deg2Rad;
            float radius = Random.Range(inner, outer);
            Vector3 trySpot = origin + new Vector3(Mathf.Sin(spread), 0f, Mathf.Cos(spread)) * radius;
            float clear = FlockClearance(trySpot);
            if (clear > bestClear) {
                bestClear = clear;
                spot = trySpot;
            }
            if (clear >= gap) {
                break;
            }
        }
        spot.y = GroundNear(spot, origin.y) + 0.55f + def.Scale * 0.08f;

        Enemy enemy = Spawn(def, spot, true);
        if (makeBoss) {
            bossPending = false;
            enemy.MakeBoss();
            UpgradeLook.Say(enemy.Title + " approaches!", 2.8f, 160f);
            GameAudio.Play("stomp", 1f, 0.6f);
        } else if (!debutAnnounced && def.UnlockWave == Wave && Wave % 10 == 0) {
            debutAnnounced = true;
            UpgradeLook.Arrive(def.Name);
        }
        return PaceAfter(kind);
    }

    static FowlKind BossKind(int wave) {
        int tier = wave <= 100 ? wave : ((wave / 10 - 1) % 10 + 1) * 10;
        return FowlRoster.ForTier(tier).Kind;
    }

    static float PaceAfter(FowlKind kind) {
        int heft = kind == FowlKind.Turkey ? 0 : (int)kind;
        return 0.32f + heft * 0.04f + Random.Range(-0.04f, 0.06f);
    }

    static float FlockClearance(Vector3 spot) {
        List<Enemy> enemies = Enemy.Roster;
        float best = 999f;
        foreach (Enemy enemy in enemies) {
            if (enemy == null) {
                continue;
            }
            Vector3 flat = enemy.transform.position - spot;
            flat.y = 0f;
            if (flat.magnitude < best) {
                best = flat.magnitude;
            }
        }
        return best;
    }

    public void CopyRoster(int[] total, int[] left) {
        for (int i = 0; i < total.Length; i++) {
            total[i] = 0;
            left[i] = 0;
        }
        if (phase == Phase.Lost) {
            return;
        }
        WaveQuota(Wave, total);
        if (phase != Phase.Combat) {
            for (int i = 0; i < total.Length; i++) {
                left[i] = total[i];
            }
            return;
        }
        for (int i = 0; i < pending.Count; i++) {
            int index = (int)pending[i];
            if (index >= 0 && index < left.Length) {
                left[index]++;
            }
        }
        List<Enemy> enemies = Enemy.Roster;
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            int index = (int)enemy.Kind;
            if (index >= 0 && index < left.Length) {
                left[index]++;
            }
        }
    }

    static void WaveQuota(int wave, int[] counts) {
        for (int i = 0; i < counts.Length; i++) {
            counts[i] = 0;
        }
        int[] caps = new int[KindCount];
        ScreenCaps(wave, caps);
        for (int tier = 10; tier <= 100; tier += 10) {
            if (tier > wave) {
                break;
            }
            FowlKind kind = FowlRoster.ForTier(tier).Kind;
            int slots = caps[(int)kind];
            if (slots <= 0) {
                continue;
            }
            // A new bird debuts as a pair (one is the Alpha), then ramps up over its decade.
            int ramp = 2 + (wave - tier) * 2;
            counts[(int)kind] = Mathf.Min(Mathf.Max(2, slots * 2), Mathf.Max(2, ramp));
        }
        if (counts.Length > (int)FowlKind.Turkey) {
            counts[(int)FowlKind.Turkey] = TurkeyCount(wave) * (Event == WaveEvent.Feast ? 2 : 1);
        }
    }

    void FillPending(int wave) {
        pending.Clear();
        int[] quota = new int[KindCount];
        WaveQuota(wave, quota);
        for (int tier = 10; tier <= 100; tier += 10) {
            if (tier > wave) {
                break;
            }
            FowlKind kind = FowlRoster.ForTier(tier).Kind;
            int count = quota[(int)kind];
            for (int i = 0; i < count; i++) {
                pending.Add(kind);
            }
        }
        int turkeys = quota[(int)FowlKind.Turkey];
        for (int i = 0; i < turkeys; i++) {
            pending.Add(FowlKind.Turkey);
        }
        for (int i = pending.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            FowlKind swap = pending[i];
            pending[i] = pending[j];
            pending[j] = swap;
        }
    }

    public const int KindCount = 11;
    public static WaveDirector Instance { get; private set; }

    static int AliveCap(int wave) {
        int cap = Mathf.Clamp(16 + wave, 16, 56);
        if (wave >= 50) {
            cap = Mathf.Clamp(72 + (wave - 50), 72, 120);
        }
        return cap;
    }

    // Every unlocked size gets at least one spot. Extra spots pile onto the smaller birds.
    static void ScreenCaps(int wave, int[] maxAlive) {
        for (int i = 0; i < maxAlive.Length; i++) {
            maxAlive[i] = 0;
        }
        int cap = AliveCap(wave);
        var kinds = new List<FowlKind>();
        kinds.Add(FowlKind.Turkey);
        for (int tier = 10; tier <= 100; tier += 10) {
            if (tier > wave) {
                break;
            }
            kinds.Add(FowlRoster.ForTier(tier).Kind);
        }

        int count = kinds.Count;
        for (int i = 0; i < count; i++) {
            maxAlive[(int)kinds[i]] = 1;
        }
        int left = Mathf.Max(0, cap - count);
        int[] weights = new int[count];
        int weightSum = 0;
        for (int i = 0; i < count; i++) {
            int weight = 1;
            for (int step = 0; step < count - 1 - i; step++) {
                weight *= 3;
            }
            weights[i] = weight;
            weightSum += weight;
        }
        int given = 0;
        for (int i = count - 1; i >= 1; i--) {
            int share = left * weights[i] / Mathf.Max(1, weightSum);
            maxAlive[(int)kinds[i]] += share;
            given += share;
        }
        maxAlive[(int)FowlKind.Turkey] += Mathf.Max(0, left - given);
    }

    static void CountAlive(int[] alive) {
        for (int i = 0; i < alive.Length; i++) {
            alive[i] = 0;
        }
        List<Enemy> enemies = Enemy.Roster;
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            int index = (int)enemy.Kind;
            if (index >= 0 && index < alive.Length) {
                alive[index]++;
            }
        }
    }

    int PickSpawn(int[] alive, int[] caps) {
        int best = -1;
        int bestScore = -1;
        for (int i = 0; i < pending.Count; i++) {
            FowlKind kind = pending[i];
            int index = (int)kind;
            if (index < 0 || index >= caps.Length || caps[index] <= 0 || alive[index] >= caps[index]) {
                continue;
            }
            int gap = Wave - FowlRoster.DebutOf(kind);
            int score = alive[index] == 0
                ? 100000 - gap
                : (caps[index] - alive[index]) * 1000 / caps[index];
            if (score > bestScore) {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    static int TurkeyCount(int wave) {
        if (wave < 20) {
            return Mathf.RoundToInt(2f + wave * 1.5f);
        }
        int count = 32 + (wave - 20) * 3;
        if (wave >= 50) {
            count += (wave - 50) * 6;
        }
        return count;
    }

    static string Flavor(int wave) {
        if (wave <= 1) {
            return "";
        }
        string line = FowlRoster.Intro(wave);
        int auto = MetaProgress.AutoWave();
        if (wave == auto) {
            line += " Your bow fires on its own now.";
        } else if (wave > auto) {
            line += "  Auto-shoot is on.";
        }
        return line;
    }

    bool AnyAlive() {
        List<Enemy> enemies = Enemy.Roster;
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
        timerText.text = "";

        if (phase == Phase.Intro) {
            waveText.text = WaveLine();
            detailText.text = Flavor(Wave);
        } else if (phase == Phase.Build) {
            waveText.text = "Build  ·  " + Mathf.CeilToInt(Mathf.Max(phaseLeft, 0f)) + "s   ·   Enter to start";
            detailText.text = Wave <= 5
                ? "It's level 5, prepare."
                : "It's level " + Wave + ", time to prepare.";
        } else if (phase == Phase.Break) {
            waveText.text = "Next wave in " + Mathf.CeilToInt(Mathf.Max(phaseLeft, 0f)) + "   ·   Enter to start";
            detailText.text = Flavor(Wave);
        } else if (phase == Phase.Lost) {
            waveText.text = "The musician fell";
            detailText.text = "";
        } else if (!string.IsNullOrEmpty(PlayerGrowth.Banner)) {
            waveText.text = WaveLine();
            detailText.text = PlayerGrowth.Banner;
        } else {
            waveText.text = WaveLine();
            detailText.text = Flavor(Wave);
        }

        detailText.color = new Color(1f, 0.96f, 0.88f, 0.92f);
        if (coinText != null) {
            coinText.text = MetaProgress.StatusLine();
            float pulse = MetaProgress.PurseFlash;
            coinText.color = pulse > 0.05f
                ? Color.Lerp(new Color(1f, 0.86f, 0.4f, 0.95f), Color.white, pulse)
                : new Color(1f, 0.86f, 0.4f, 0.95f);
        }
        if (purseFill != null && MetaProgress.NextCoinAt > 0) {
            float ratio = Mathf.Clamp01(MetaProgress.HensToward / (float)MetaProgress.NextCoinAt);
            purseFill.rectTransform.sizeDelta = new Vector2(220f * ratio, 8f);
            purseFill.color = MetaProgress.PurseFlash > 0.05f
                ? new Color(1f, 0.95f, 0.6f, 1f)
                : new Color(1f, 0.78f, 0.22f, 0.95f);
        }
    }

    string WaveLine() {
        string size = "";
        if (PlayerGrowth.Instance != null) {
            string shot = MetaProgress.ShootsRockets() ? "Rocket" : "Arrow";
            size = "   ·   Size " + PlayerGrowth.Instance.Scale.ToString("0.00") + "   ·   " + shot + " " + PlayerGrowth.Instance.ArrowDamage;
        }
        if (phase == Phase.Intro) {
            return "Wave " + Wave + size;
        }
        int left = AliveCount() + pending.Count;
        return "Wave " + Wave + "   ·   " + left + " left" + size;
    }

    int AliveCount() {
        int count = 0;
        List<Enemy> enemies = Enemy.Roster;
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
        waveText = MakeText(canvasGo.transform, font, "Wave 1", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(1500f, 46f));
        detailText = MakeText(canvasGo.transform, font, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(1400f, 72f));
        coinText = MakeText(canvasGo.transform, font, "", 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(860f, 40f));
        coinText.rectTransform.pivot = new Vector2(0f, 1f);

        var purseBackGo = new GameObject("Purse");
        purseBackGo.transform.SetParent(canvasGo.transform, false);
        var purseBack = purseBackGo.AddComponent<Image>();
        UiSkin.Bar(purseBack);
        purseBack.color = new Color(0.12f, 0.08f, 0.04f, 0.85f);
        purseBack.raycastTarget = false;
        var purseRect = purseBack.rectTransform;
        purseRect.anchorMin = new Vector2(0f, 1f);
        purseRect.anchorMax = new Vector2(0f, 1f);
        purseRect.pivot = new Vector2(0f, 1f);
        purseRect.anchoredPosition = new Vector2(36f, -68f);
        purseRect.sizeDelta = new Vector2(220f, 10f);
        var purseFillGo = new GameObject("Fill");
        purseFillGo.transform.SetParent(purseBackGo.transform, false);
        purseFill = purseFillGo.AddComponent<Image>();
        UiSkin.Bar(purseFill);
        purseFill.color = new Color(1f, 0.78f, 0.22f, 0.95f);
        purseFill.raycastTarget = false;
        var fillRect = purseFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(0f, 0f);
        fillRect.sizeDelta = new Vector2(0f, 8f);

        lostPanel = new GameObject("Lost");
        lostPanel.transform.SetParent(canvasGo.transform, false);
        var panel = lostPanel.AddComponent<Image>();
        UiSkin.Round(panel);
        panel.color = new Color(0.05f, 0.04f, 0.04f, 0.82f);
        panel.raycastTarget = false;
        var panelRect = lostPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(900f, 700f);

        var title = MakeText(lostPanel.transform, font, "The music stopped", 58, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(680f, 90f));
        title.color = new Color(1f, 0.45f, 0.35f);
        lostBody = MakeText(lostPanel.transform, font, "", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(820f, 520f));
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
