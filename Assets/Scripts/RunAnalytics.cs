using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Hawkes;
using UnityEngine;

// Hawkes Analytics (Gen's docs/analytics.md) for The Place Department: what players do, where they stop and why.
// Gen's Unity bridge (com.hawkes.analytics) loads the SDK into the page and sends the events; this class decides
// what to send and when. Gameplay calls one line here at the real moment (a run starts, a wave ends, a card is
// taken), never per frame. Nothing personal: waves, birds, cards and upgrades only. In the Editor nothing is sent.
// AGENTS.md ("Analytics") lists every event and where it comes from: keep them true when the game changes.
public static class RunAnalytics {
    public const string Product = "theplacedepartment";
    public const string AskId = "game-over";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void HawkesAnalyticsPageSetBuild(string json);

    [DllImport("__Internal")]
    static extern int HawkesAnalyticsPageDebug();

    [DllImport("__Internal")]
    static extern void HawkesAnalyticsPageEnableDebug();

    [DllImport("__Internal")]
    static extern void HawkesAnalyticsPageAsk(string json);

    [DllImport("__Internal")]
    static extern void HawkesAnalyticsPageCloseAsk();
#else
    static void HawkesAnalyticsPageSetBuild(string json) { }
    static int HawkesAnalyticsPageDebug() { return 0; }
    static void HawkesAnalyticsPageEnableDebug() { }
    static void HawkesAnalyticsPageAsk(string json) { }
    static void HawkesAnalyticsPageCloseAsk() { }
#endif

    static bool readySent;
    static int runsThisSession;

    // The run.
    static bool runOpen;
    static int runStartWave;
    static float runStartedAt;
    static int cleared;
    static int cardsTaken;
    static int rerolls;
    static readonly Dictionary<string, int> hurtBy = new Dictionary<string, int>();
    static string lastHurtBy;
    static bool lastHurtBoss;
    static bool lastHurtElite;

    // The wave.
    static int openWave;
    static int waveShots;
    static int waveHits;
    static int waveKills;
    static int waveDamage;
    static int waveHpStart;

    // The page gets the build stamp and the SDK before the first scene, so even the loading-to-menu time counts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() {
        readySent = false;
        runsThisSession = 0;
        runOpen = false;
        openWave = 0;
        try {
            TextAsset build = Resources.Load<TextAsset>("HawkesBuild");
            if (build != null) {
                HawkesAnalyticsPageSetBuild(build.text);
            }
        } catch (System.Exception) {
        }
        var settings = ScriptableObject.CreateInstance<HawkesAnalyticsSettings>();
        settings.product = Product;
        settings.debug = HawkesAnalyticsPageDebug() == 1;
        HawkesAnalytics.Init(settings);
    }

    // A playtest started by window.hawkesPlaytest.start() on a page that wasn't opened in debug mode.
    public static void EnableDebug() {
        HawkesAnalyticsPageEnableDebug();
    }

    // ---------------------------------------------------------------- menus

    // The main menu (the shop) is up: the game is playable. game_ready goes once per page load.
    public static void Menu() {
        if (!readySent) {
            readySent = true;
            HawkesAnalytics.Ready();
        }
        HawkesAnalytics.SetContext(new Dictionary<string, object> {
            { "wave", null }, { "level", null }, { "upgrades", null }, { "player_level", null },
            { "shop_ranks", MetaProgress.TotalRanks() }, { "best_wave", MetaProgress.BestWave }
        });
        HawkesAnalytics.Screen("menu");
    }

    public static void Screen(string name) {
        HawkesAnalytics.Screen(name);
    }

    // A shop upgrade bought between runs.
    public static void ShopBuy(UpgradeInfo info, int cost) {
        HawkesAnalytics.Feature("shop_buy", new Dictionary<string, object> {
            { "item", info.Id.ToString() }, { "rank", MetaProgress.Rank(info.Id) }, { "max_rank", info.MaxRank },
            { "cost", cost }, { "coins_left", MetaProgress.Coins }
        });
        HawkesAnalytics.SetContext("shop_ranks", MetaProgress.TotalRanks());
    }

    // Settings closed: what the player set (sound off is a "why" worth knowing).
    public static void SettingsClosed(string from) {
        HawkesAnalytics.Feature("settings", new Dictionary<string, object> {
            { "from", from }, { "sound", SoundSettings.On }, { "music", Mathf.RoundToInt(SoundSettings.Music * 100f) },
            { "sfx", Mathf.RoundToInt(SoundSettings.Sfx * 100f) }
        });
    }

    // ---------------------------------------------------------------- the run

    public static void RunStart(int startWave) {
        HawkesAnalyticsPageCloseAsk();
        runOpen = true;
        runsThisSession++;
        runStartWave = startWave;
        runStartedAt = Time.realtimeSinceStartup;
        cleared = 0;
        cardsTaken = 0;
        rerolls = 0;
        hurtBy.Clear();
        lastHurtBy = null;
        lastHurtBoss = false;
        lastHurtElite = false;
        openWave = 0;
        HawkesAnalytics.SetContext(new Dictionary<string, object> {
            { "mode", "waves" }, { "wave", startWave }, { "upgrades", "" }, { "player_level", RunBoons.Level },
            { "shop_ranks", MetaProgress.TotalRanks() }, { "best_wave", MetaProgress.BestWave }
        });
        HawkesAnalytics.Track("run_start", new Dictionary<string, object> {
            { "run", runsThisSession }, { "mode", "waves" }, { "start_wave", startWave },
            { "best_wave", MetaProgress.BestWave }, { "shop_ranks", MetaProgress.TotalRanks() },
            { "shop", MetaProgress.ShopLine() }, { "coins", MetaProgress.Coins }, { "touch", UiFit.Touch }
        });
        HawkesAnalytics.Screen("wave");
    }

    // A wave's fighting begins (after its intro or the break before it).
    public static void WaveStart(int wave, WaveEvent waveEvent, int birds) {
        if (!runOpen) {
            return;
        }
        if (openWave != 0) {
            WaveEnd(openWave, "restart");
        }
        openWave = wave;
        waveShots = RunLog.RunShots;
        waveHits = RunLog.RunHits;
        waveKills = RunLog.RunKills;
        waveDamage = Musician.Instance != null ? Musician.Instance.DamageTaken : 0;
        waveHpStart = Musician.Instance != null ? Musician.Instance.Health : 0;
        HawkesAnalytics.SetContext("wave", wave);
        var props = new Dictionary<string, object> {
            { "wave", wave }, { "birds", birds }, { "musician_hp", waveHpStart }, { "player_level", RunBoons.Level }
        };
        if (waveEvent != WaveEvent.None) {
            props["event"] = waveEvent.ToString();
        }
        if (wave % 10 == 0) {
            props["boss"] = true;
        }
        if (WaveDirector.IsFortWave(wave)) {
            props["fort"] = true;
        }
        HawkesAnalytics.LevelStart(wave.ToString(), props);
    }

    // result: complete (cleared), death (the musician fell), quit (left mid-wave), restart (a wave key jumped).
    public static void WaveEnd(int wave, string result) {
        if (openWave == 0) {
            return;
        }
        openWave = 0;
        if (result == "complete") {
            cleared++;
        }
        int shots = RunLog.RunShots - waveShots;
        int hits = RunLog.RunHits - waveHits;
        var props = new Dictionary<string, object> {
            { "wave", wave }, { "kills", RunLog.RunKills - waveKills },
            { "damage_taken", (Musician.Instance != null ? Musician.Instance.DamageTaken : 0) - waveDamage },
            { "musician_hp", Musician.Instance != null ? Musician.Instance.Health : 0 },
            { "arrows", shots }, { "hits", hits }, { "hit_rate", shots > 0 ? Mathf.Round(100f * hits / shots) / 100f : 0f },
            { "best_streak", Combo.Best }, { "player_level", RunBoons.Level }
        };
        HawkesAnalytics.LevelEnd(wave.ToString(), result, props);
    }

    // A bot hit the musician (Musician.Hurt). The last one to land a blow is the cause when he falls.
    public static void NoteHurt(Enemy by, int amount) {
        if (by == null) {
            return;
        }
        string kind = Cause(by.Kind);
        hurtBy.TryGetValue(kind, out int sum);
        hurtBy[kind] = sum + amount;
        lastHurtBy = kind;
        lastHurtBoss = by.Boss;
        lastHurtElite = by.Elite;
    }

    // The musician fell: the death (which bot), the wave's end, the run's end and the game-over screen.
    public static void MusicianDown(int wave) {
        if (!runOpen) {
            return;
        }
        string cause = lastHurtBy ?? "unknown";
        var props = new Dictionary<string, object> { { "wave", wave }, { "worst", WorstBird() } };
        if (lastHurtBoss) {
            props["boss"] = true;
        }
        if (lastHurtElite) {
            props["elite"] = true;
        }
        HawkesAnalytics.Death(cause, props);
        WaveEnd(wave, "death");
        RunEnd(wave, "lose", "musician_down");
        HawkesAnalytics.Screen("game_over");
        Ask(wave);
    }

    // The game scene went away with the run still going (the page went to the menu some other way).
    public static void RunAbandoned(int wave) {
        if (!runOpen) {
            return;
        }
        WaveEnd(wave, "quit");
        RunEnd(wave, "quit", "left");
    }

    static void RunEnd(int wave, string result, string cause) {
        runOpen = false;
        int shots = RunLog.RunShots;
        var props = new Dictionary<string, object> {
            { "result", result }, { "duration", Mathf.Round(Time.realtimeSinceStartup - runStartedAt) },
            { "score", wave }, { "wave", wave }, { "start_wave", runStartWave }, { "waves_cleared", cleared },
            { "cause", cause }, { "killed_by", lastHurtBy ?? "" }, { "worst", WorstBird() },
            { "kills", RunLog.RunKills }, { "arrows", shots },
            { "hit_rate", shots > 0 ? Mathf.Round(100f * RunLog.RunHits / shots) / 100f : 0f },
            { "damage_taken", Musician.Instance != null ? Musician.Instance.DamageTaken : 0 },
            { "musician_hp", Musician.Instance != null ? Musician.Instance.Health : 0 },
            { "player_level", RunBoons.Level }, { "cards", cardsTaken }, { "rerolls", rerolls },
            { "best_streak", Combo.Best }, { "coins", MetaProgress.CoinsThisRun },
            { "best_wave", MetaProgress.BestWave },
            { "upgrades", Upgrades(300) }
        };
        HawkesAnalytics.Track("run_end", props);
        HawkesAnalytics.Flush();
    }

    // "How was that run?" on the game-over screen: stars and a comment, in the game's night colours.
    static void Ask(int wave) {
        string json = "{\"id\":\"" + AskId + "\",\"question\":\"How was that run?\",\"context\":\"wave " + wave + "\","
            + "\"kind\":\"stars\",\"comment\":true,\"thanks\":\"Thanks! The pianist plays on.\","
            + "\"theme\":{\"background\":\"#141019\",\"color\":\"#f4ecdc\",\"accent\":\"#ffd14d\","
            + "\"font\":\"Arial, Helvetica, sans-serif\",\"radius\":\"14px\"}}";
        HawkesAnalyticsPageAsk(json);
    }

    // ---------------------------------------------------------------- cards

    public static void CardsOpen(bool chest) {
        HawkesAnalytics.Screen(chest ? "chest" : "level_up");
    }

    public static void Reroll() {
        rerolls++;
        HawkesAnalytics.Feature("reroll", new Dictionary<string, object> { { "left", RunBoons.Rerolls } });
    }

    // A level-up or chest card taken, with what else was offered.
    public static void CardTaken(bool chest, List<BoonId> offered, int index) {
        if (offered == null || index < 0 || index >= offered.Count) {
            return;
        }
        cardsTaken++;
        BoonId taken = offered[index];
        BoonInfo info = RunBoons.Info(taken);
        var names = new string[offered.Count];
        for (int i = 0; i < offered.Count; i++) {
            names[i] = offered[i].ToString();
        }
        HawkesAnalytics.Choice(chest ? "chest" : "level_up", taken.ToString());
        HawkesAnalytics.Track("card_pick", new Dictionary<string, object> {
            { "taken", taken.ToString() }, { "offered", string.Join(",", names) }, { "slot", index + 1 },
            { "rarity", info.Rarity.ToString() }, { "rank", RunBoons.Rank(taken) }, { "evolution", info.Evolution },
            { "chest", chest }, { "player_level", RunBoons.Level }, { "wave", WaveDirector.Wave }
        });
        HawkesAnalytics.SetContext(new Dictionary<string, object> {
            { "upgrades", Upgrades(100) }, { "player_level", RunBoons.Level }
        });
        HawkesAnalytics.Screen("wave");
    }

    // ---------------------------------------------------------------- helpers

    // yo_bot, plated_yo_bot, spider_bot ... titan_spider_bot.
    static string Cause(BotKind kind) {
        return BotRoster.Slug(kind);
    }

    static string WorstBird() {
        string worst = "";
        int most = 0;
        foreach (var pair in hurtBy) {
            if (pair.Value > most) {
                most = pair.Value;
                worst = pair.Key;
            }
        }
        return worst;
    }

    // The run's boons, short: "Heavy2 SplitShot1 Ovation1".
    static string Upgrades(int max) {
        var text = new StringBuilder();
        foreach (BoonId id in System.Enum.GetValues(typeof(BoonId))) {
            int rank = RunBoons.Rank(id);
            if (rank <= 0) {
                continue;
            }
            string part = id + rank.ToString();
            if (text.Length + part.Length + 1 > max) {
                break;
            }
            if (text.Length > 0) {
                text.Append(' ');
            }
            text.Append(part);
        }
        return text.ToString();
    }
}
