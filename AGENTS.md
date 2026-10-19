# Agent guide

Unity 6.3 LTS (`6000.3.7f1`), built-in render pipeline, old Input Manager (`activeInputHandler` 0). `README.md` explains how the game plays. This file explains how to change it without breaking it.

The player is J. Prefer editing the scripts directly. Do not commit unless asked.

## Do not change

- `.meta` files, except by letting Unity create them. Never delete a `.meta` without its asset.
- `Library/`, `Temp/`, `obj/`.
- `Packages/manifest.json` must not gain `com.unity.purchasing`.
- Existing files in `Assets/Resources/Sfx/`. Do not regenerate them.
- The piano pirate and the bow textures, unless the task is about them.
- Retired `UpgradeId` slots 2, 3, 5, 7 and 8. Never renumber or reuse them, because saved ranks are packed by index.
- J's standing art rules:
  - No camera shake, except the bat's small contact jolt (`ZeldaStyleCamera.Shake`, called only from `BatEquipment`), which J asked for with the bat on 2026-10-19. Keep it that small and don't add others.
  - No tiny spark, glitter, ember or firefly particles. Effects stay soft: flashes, smoke, a few feathers or chunky bot plates, rings.
  - Keep the playfield (within 150 m of the piano) free of anything solid. Bots walk straight lines and do not pathfind.
- `KeyCode.Plus` does not exist. Wave skip uses `Equals`, `KeypadPlus`, `Minus` and `KeypadMinus`.

## Where code lives

Runtime gameplay is in `Assets/Scripts/`. Editor wiring is in `Assets/Editor/`. Do not put gameplay in editor scripts.

| To change… | Touch |
|---|---|
| A balance number | `Tuning.cs` first: wave-power waves, level curve, bolt values per kind (`BoltsFor`), elite odds, golden Yo Bot, boss health, events, streaks, coins, bot health growth |
| Wave flow, spawning, events, bosses, coins per wave | `WaveDirector.cs`. Bot counts are `YoBotCount`, `WaveQuota` and `ScreenCaps` (one slot per `BotKind`, `KindCount` 11); the Alpha is `BossKind`. Events are `WaveEvent` plus `RollEvent` |
| A bot kind (name, stats, tint, blurb) | `BotRoster.cs` (`BotKind`, `ForTier`, `ForKind`, `UnlockOf`). Keep `BotKind`'s order: kill counts, the wave list, pacing and the coin tally (index 1, saved as `HensToward`) are packed by it. Yo Bots are the kinds before `SpiderBot` (`IsSpider`). Spawning picks it up through `ForSpawn`, which is the only place spawn size is applied |
| Bot behavior, health, status, elites (`BotTrait`), drones, golden Yo Bot, bosses, the claw swipe and leg stab | `Enemy.cs` (`ApplyBot`, `MakeElite`, `MakeGolden`, `MakeBoss`, `TickStatus`, `AnimateModel`) |
| Anything that damages a bot | `Enemy.Damage(amount, DamageKind, crit)`. The bat goes through `Combat.BatHit` (`DamageKind.Bat`), so crits, marks, executes, fire, frost and lightning apply. `Enemy.Hit` is one plain blow at base strength, used by spikes, towers and stomps. Deaths go through `Enemy.Die` → `Combat.OnKill` (bolts, chests, bombs, streak, run log). Shoving a bot is `Enemy.Knock(velocity, airSeconds)` |
| The bat: combo, ground smash, home run, auto swing, facing, the stand-in bat | `BatEquipment.cs` (on the player; the scene's old `BowEquipment` slot adds it and switches off). Its numbers are the `Bat...`, `Smash...`, `Charge...` and `HomeRun...` values in `Tuning.cs` |
| Lock-on | `ZeldaStyleCamera.AcquireTarget`: hold Shift for the nearest bird in `LockReach()` from `Enemy.Roster`; it sticks to that bird; right click or Tab moves it to the next nearest |
| An in-run boon | `BoonId` and a row in `RunBoons.All`, **in enum order**, because the array is indexed by the enum. Read the boon's rank where it acts: `Combat` for hits and kills, `BatEquipment` for swing behavior, `BoonEffects` for things that tick on their own. An evolution uses `E(...)` and names two parents that have max ranks. The bat boons kept their bow-era `BoonId` names (`Heavy` is Corked Bat, `SplitShot` Wide Arc, `Volley` Grand Slam, `Homing` Long Ball...; the enum comments say which), because analytics reports them by id: rename the display name, never the id. A boon that changes the bat gets a mark in `UpgradeLook.BatMarks` |
| Level-up cards | `LevelUpScreen.cs` |
| XP bar, streak counter, boss bar, boon list | `RunHud.cs` |
| Streak and hit-stop | `Juice.cs` (`Combo`, `HitStop`) |
| Bolts (XP) and chests | `Pickups.cs` (`DropBolts`; the hex bolt is built in code until `Models/Bolt` exists) |
| Size, jump-landing, foot stomp, shake-off | `PlayerGrowth.cs` |
| A shop upgrade | `UpgradeId` in `MetaProgress.cs` with a **new number at the end**, a `Catalog` row, a real caller, and a visible mark in `UpgradeLook.cs`. `Keen` (0) is Corked Core and `Blast` (4) is Aluminum! Not wood! (`MetaProgress.MetalBat`). 10 is Four-Leaf Clover, 11 Opening Act, 12 Stage Hands. Rank costs come from `Cost` |
| A fort piece | `FortKind`, `FortStock`, `DefencePiece` and `BuildMode` in `Fort.cs` |
| A combat or build tip | `ControlsHelp.cs` |
| The wave-power list on the left | `AbilityList.cs` |
| The enemy list on the right | `WaveRoster.cs`, which reads `WaveDirector.CopyRoster` and names rows from `BotRoster.ForKind` |
| A sound | `Assets/Resources/Sfx/Name.mp3`, played with `GameAudio.Play("Name", volume, pitch)`. The bat's are `batswing`, `batcrack`, `batsmash` and `homerun` (Hawkes Gen `audio.sfx`) |
| A body animation | A Mixamo humanoid FBX under `Assets/Art/`, listed in `SetupWarrok.Clips`, then a State id and `AddAction` |
| Bot models | Stand-ins: `BotArt.cs` builds them in code. `Tools/Blender/bt_bots.py` makes the same in Blender (`export("<Kind>")`, `export_all()`) into `Assets/Resources/Models/Bots/`, which `BotArt` loads instead when present |
| Map scenery models | `Tools/Blender/bt_arena.py`, then `export_arena()`. Output goes to `Assets/Art/Arena/` |
| The map | `Assets/Editor/BuildArena.cs`, then **Tools → Arena → Rebuild Arena** |
| Night lighting, lanterns, mist, notes | `NightAtmosphere.cs` |
| Bloom and color grade | `NightGrade.cs` with `Resources/Shaders/BTPost.shader` |
| A particle effect | `Fx.cs`: add an `FxKind` and a `Make(...)` line, or reuse a recipe. Never spawn primitive spheres for effects. Status (burning, frozen, golden) is a tint on the bot in `Enemy.TickFlash`, not particles. Bot deaths are `Fx.Death` (flash, smoke, `Debris` plates); jump-jet hops are `Fx.JetPuff` |
| UI panels and bars | `UiSkin.Round` and `UiSkin.Bar` |
| Touch controls (stick, Swing, Lock, Jump, Shake, Next wave, Turn) | `TouchControls.cs`. They press keys through `PlayInput.Pad...`, so gameplay needs no touch code |
| Phone and tablet layouts | `UiFit.cs` picks the layout (`Mode`: Desktop, Wide, Tall) and the canvas scale (`Apply`). Each screen has a `Layout()` that redoes positions when `UiFit.Stamp` changes |
| Sound on/off, music and effects volume | `SoundSettings.cs` (saved as JSON `{ on, music, sfx }` under `hawkes.sound`: localStorage on the web, PlayerPrefs too). Music is every looping `AudioSource` (the piano); effects are `GameAudio`, which multiplies by `SoundSettings.EffectsLevel` |
| The Settings screen | `SettingsPanel.cs`, opened from the main menu's Settings button and the pause menu |
| Pausing a run | `PauseMenu.cs`: P or Escape, the pause button right of the musician's bar, or the browser freeing a locked mouse. Gameplay that must stop checks `PauseMenu.IsOpen` next to `LevelUpScreen.IsOpen` |
| Slice's knife (stand, stab, slash, sit back down) and her health bar | `Musician.cs` (`TickKnife`, `Stab`, `LateUpdate` straightens the seated pianist's legs as a stand-in until Slice's model lands). Reach, cooldown and the bar's title are its `StabReach`, `StabCooldown` and `Name` |
| The website's "Tap to play" prompt | `Tools/Web/hawkes-sound.js`. `WebGlBuild.InstallSoundPrompt` copies it to `<site>/Game/` and adds its `<script data-hawkes-sound>` to `Game/index.html` on every deploy, so edit it here, never in the website |
| Analytics events, the feedback card, the build stamp | `RunAnalytics.cs`, `Plugins/WebGL/HawkesAnalyticsPage.jslib`, `WebGlBuild.WriteBuildStamp`. See Analytics below |
| The website's What's new page and release notes | `Tools/Web/`: `whatsnew.html`, `releases.json` (the first release), `hawkes-release.js`, and the link blocks `whatsnew-landing.html` (nav, before Play) and `whatsnew-game.html` (the version in the game's corner). `WebGlBuild.InstallWhatsNew` installs them on every deploy. The site's `releases.json` is written only when it has none: after that Hawkes Studio adds a release at each go live, so later notes go there, not here |

## Rules the code depends on

- **`Enemy.Roster`** lists every live bot. Use it instead of `FindObjectsByType<Enemy>` in anything that runs every frame. Copy it before you damage bots in a loop, because a Splitter death adds drones mid-loop.
- **Bot models:**
  - Parts must stay named: Yo Bots `Body`, `Head`, `ClawL`, `ClawR`, `LegL`, `LegR`; Spider Bots `Body`, `Head`, `LegL1`–`LegL4`, `LegR1`–`LegR4` (1 is the front pair, which stabs). Each part pivots where it joins (hip, shoulder, neck).
  - The FBX import mirrors X, so the code reads each claw's and leg's side from its local position.
  - +Z in the game (Blender's −Y) is the bot's front, feet on 0. `BuildModel` turns the model around if the head ends up behind.
  - `BirdArt.Tinted` tints by copying meshes, so the meshes must stay readable. `ModelImportRules.cs` enforces that, together with smooth normals at a 60° crease angle.
- **Shader:** bots use `BirdArt.Shared`, which is `Knoll/VertexColorLit` with `_GlowBand` 0 and a strong `_RimStrength`. Use `BirdArt.Glowing` only where warm vertex colors should emit (the Titan Spider Bot's gold trim, lantern glass). `KnollLit.mat` keeps `_RimStrength` near 0 so scenery does not glow.
- **Hit flash:** it sets a `MaterialPropertyBlock` and clears it when the flash ends, so the bot batches with the rest again. Keep that cleanup.
- **Pausing:** `LevelUpScreen` pauses with `Time.timeScale = 0`. `Juice.HitStop` uses 0.05 briefly and never overrides a real pause. The camera, swinging and build clicks all check `LevelUpScreen.IsOpen`.
- **Jump-landing and climbing:** these use `DebutWave`. Yo Bots count as 0, and every other kind counts as its unlock wave.
- **Map:** the arena is centered on `PianoRecital`. The ground is on the Ground layer (`1 << 3`) with a mesh collider. Scenery has no colliders. The `Boundary` wall is on Ignore Raycast at 205 m. `DressGrassyKnoll` and `PlacePracticeTargets` skip when `Arena` exists, so the old knoll never comes back.
- **Movement:**
  - The ground ray is Ground only and ignores triggers.
  - `MovementMotor` must keep applying gravity while `FallVelocity > 0.5`, or the player rockets upward.
  - Jump only when grounded, cooled down and not rising.
- **Phones and tablets:** every overlay canvas calls `UiFit.Apply(scaler)`. Desktop (`UiMode.Desktop`) is the
  1920x1080 layout exactly as drawn: never change its numbers for phones. Wide (landscape under 500 CSS px tall, or
  a touch tablet) and Tall (portrait) use a canvas at least 660 units across its short side, so 22-unit text is
  about 13 px on a phone and an 88-unit button about 52 px. Keep compact text at 20 units or more and buttons at 88.
  Compact screens keep the bottom corners for the touch stick (left) and buttons (right). The musician's bar keeps
  the top middle. `Screen.width` is in device pixels in the browser: `UiFit.PixelRatio` (from
  `Plugins/WebGL/HawkesScreen.jslib`) converts to CSS pixels.
- **Touch:** `UiFit.Touch` is true on phones and tablets, after the first touch, or when a playtest forces it.
  `TouchControls` then turns off `Input.simulateMouseWithTouches`, so a finger on the stick never counts as a click.
  New gameplay input must go through `PlayInput`, or touch can't press it either.
- **Player size:** `CharacterController` is not scaled by its transform. `PlayerGrowth` scales height, radius and center from the scene baseline (height 2, radius 0.5). Never save a grown capsule back into the scene.

## Animator

- `applyRootMotion` is false. Gameplay calls `SetInteger("State", n)` and `CrossFade`.
- Each bat swing calls `SetTrigger("Fire")` on the upper layer (the old shoot recoil, a stand-in until the new model) and must not set the base State to 2, or the jump is cancelled.
- The upper-layer Shoot state's speed follows the `FireSpeed` float (default 1), which `CharacterMovement` sets every frame.
- While `PlayerGrowth.Rooted` is set or `Enemy.Climbers > 0`, there is no jump, and `AnimationMove` must not overwrite State 5 or 7. Swinging can continue.

## Editor scripts

Many editor scripts run on load through `[InitializeOnLoad]`: `SetupWarrok`, `BuildArena`, `DressGrassyKnoll`, `PlacePianoPlayer`, `PlaceWaveDirector`, `PlaceEnemies`, `PlacePracticeTargets`, `RestoreOldPlayer`, `EquipBow`, `FixMixamoImport`, `SetupMainMenu`, `BlenderMcpSetup`, `ResetTimeScale` and `DisableXboxGdkPrompt`. When writing one:

- Reschedule on `EditorApplication.delayCall` while compiling, updating or playing.
- Return immediately unless the open scene is `scene2`, if the script changes that scene.
- Cap retries. For example, `FixMixamoImport` stops after 8 passes.

Specific scripts:

- **`SetupWarrok`** reimports clips, rewrites `Hero.controller` and saves the scene. Apply new states **after** the loop that sets `canTransitionToSelf` false, or the next editor load clears them. Its `InvalidOperationException` on `MarkSceneDirty` during play is expected.
- **Kick clips:** do not retarget `Assets/hero/ANIM_HERO_*` onto Warrok. They are generic Bip01 clips.
- **`ResetTimeScale`** puts `Time.timeScale` back to 1 when play mode ends. Without it, stopping play while a level-up card is open can save 0 into `TimeManager.asset`, and the next Play starts frozen.

While Unity is playing, script edits do not compile. Leave play mode first. Prefer an editor script over hand-editing `scene2.unity`, because Unity overwrites the YAML while the editor is open.

## Scripts and encoding

- Match the surrounding C# style. No new namespaces.
- Write `.cs` files as UTF-8. A BOM is fine (`CharacterMovement.cs` has one). UTF-16 LE breaks the compiler (`CS1056`).
- `CharacterMovementController.cs` is already UTF-16. It compiles, so leave it alone, and skip it in bulk text edits.
- `ZeldaStyleCamera.cs` is UTF-8, but some readers show it as one garbled line. If a search-and-replace fails, edit it with Python and keep LF.

## Testing and balance

- **Never write to J's save from a test.** Call `MetaProgress.EnterSandbox(freshRanks)` first. `DevAutopilot` (F9) does this itself. `PlayerPrefs` writes from tooling are also blocked by the session's safety rules.
- **Autopilot:** `DevAutopilot` is a perfect batter at the bird nearest the piano (it doesn't walk). Read its `[RunLog]` lines in the console or `runlog.csv`. It is easier than real play, so treat its results as an upper bound.
- **Restore project settings after tests.** Setting `Application.runInBackground` during play writes `runInBackground: 1` into `ProjectSettings.asset`, and quality changes can stick too. Before handing back, check `git diff ProjectSettings/` and restore anything a test changed.
- **The game view only updates while Unity is focused or set to run in the background.** If `Time.time` stops advancing during an MCP session, that is why.

## Playtests

The game implements the Hawkes playtest contract v1 (`C:\Projects\hawkesstudio\docs\playtest.md`) through the
`com.hawkes.playtest` package. Hawkes Studio runs it daily against the WebGL build (`?playtest=<id>` in the URL, or
`window.hawkesPlaytest.start(id)`), records it and has Claude review it.

- **Bot:** `Assets/Scripts/Playtest.cs` registers product `theplacedepartment` at startup. Scenarios: `smoke` (menu,
  Enter, about 30 s of play), `run` (until the musician falls, picking cards; capped at 15 min), `bat` (the bat's
  three moves, each until it is seen: the 3-hit combo on wave 1's Yo Bots with one right-click lock switch, then a
  ground smash on three small Yo Bots dropped in front of the player, then a home run into a line of four Yo Bots
  dropped in front of the player (both drops are named `setup` events); then a minute of play. `bat` timeline
  lines say what each swing did, and the report fails any move it didn't see), `musician` (the bar says Slice; the bot holds fire by the piano until wave 1's Yo Bots reach her and she has stabbed three times, one `stab` event each with damage and the gap since the last; then it clears the wave and waits for `slice-sits`; then it holds fire from wave 2, pressing `=` if she outlasts it, until the run ends), `menus` (shop
  clicks, start, one level-up card with a reroll), `words` (reads the menu, shop, help, build tips, pause, level-up cards and game over; each `old-words` event is 0), `touch` (touch only: menu layout check, tap Play, the stick,
  Swing, Lock and Next wave pressed by the bot's fingers through `TouchControls.BotFinger`, a level-up card tapped,
  until wave 2 is under way; run it with `mobile` for portrait or at 844x390 for landscape), `upgrades` (the bat
  upgrades: shop rows read off the menu (`shop-items`), the sandbox owns Aluminum! Not wood! and Corked Core (setup)
  and `metal-bat` says what the bat looks like; the first level-up's card titles are read off the screen (`cards`,
  a problem if any card says arrow, bow or quiver); setup: the wave key to wave 14 and a dozen bat boons granted
  (`bat-marks` lists each mark); then play until `wide-swing`, `burning`, `slowing`, `chain-sparks`, `spin-swing`
  and `batted-bolt` have each been seen; then Thunder Bat for a lightning spin (`thunder-bat`) and 30 s of play), `site` (the website
  around the game: every page outside `Game/` has "Powered by HAWKES GEN" linking to hawkesgen.com, the landing page
  links to hawkesbiomedical.com. It checks the landing page, every page in the deploy's `site/pages.json` and every
  page they link to, looks for a blog at `/blog/`, `/blog.html`, `/news/` and `/posts/`, and logs `site-pages`,
  `sub-pages` and `blog` events saying what exists. Then it lays each page over the game for 4 s via
  `Plugins/WebGL/HawkesSite.jslib` and plays 20 s). Every touch run reports
  a layout check per screen (`menu`, `hud`, `cards`): text or buttons off screen, text over text or over a button,
  text under 11 px, buttons under 44 px, with `<screen>MinTextPx`, `<screen>MinTapPx` and `<screen>Overlaps`.
  `bots` (wave 1's Yo Bots walking at the musician, event `yo-bots` with their distance closing; 30 s of play, then
  `bolts` with bolts picked up and the XP gained; `enemy-list` with the names read off the list on the right; the
  setup shortcut, the testing wave key `=` to wave 30, then `spider-bot` with the Alpha Spider Bot's height and
  whether the boss bar is up; 30 s of fighting it). `settings` (Settings from the menu: sound off and on, both sliders, Done; then a run, the pause button, Settings
  from pause, Resume, P twice; layout checks `settings` and `pause`). `analytics` (Hawkes Analytics: a run, wave 1
  cleared, then the bot stops swinging from wave 2 so the bots take the musician; the "How was that run?" card must
  show on the game-over screen and the bot rates it 4 stars; metrics `analyticsEvents` and `analyticsSent`). Every
  scenario first presses the page's "Tap to play" prompt (event `tap-prompt` with the audio state, metric
  `audioRunning`); in the editor there is none. Every scenario also logs each analytics event as it goes out
  (`analytics-config` once, with the product, build and env, then one `analytics` event each; see Analytics).
- **Real input:** gameplay reads keys and the mouse through `PlayInput` (`Assets/Scripts/PlayInput.cs`), which is
  `UnityEngine.Input` plus whatever the bot holds. Use `PlayInput.GetKey...` in new gameplay input code, or the bot
  can't press it. The bot walks with WASD to the nearest bird within guard range of the musician, holds Shift
  (lock-on), clicks the left button for one frame per swing (a longer hold winds up a home run), walks back to the
  piano when the birds are gone, presses F for climbers, Enter to skip breaks and 1-3 for cards. Every time the
  lock lands on a bird it checks that it is the nearest (`lock-on` events; a warning if not). It plays in `MetaProgress.EnterSandbox(true)`, so it never
  touches the save.
- **Report:** wave, kills, damage, swings, hits, hitRate, finishers, smashHits, homeRuns, homeRunKnocks,
  knockedBack, lockNearest, lockWrong, level, levelUps, musician HP, coins, run seconds and fps. `bat` lines from
  `BatEquipment.Events` go to the timeline (all of them in `bat`, the first 15 elsewhere). Problems:
  errors and exceptions (from the package), missing-asset warnings, menu art missing, "no kills for 45 s", a wave
  that doesn't end in 240 s, a level-up card that won't close, a game-over screen that doesn't return to the menu.
- **Test:** `Assets/Tests/PlayMode/PlaytestSmokeTest.cs` runs `smoke` through `HawkesPlaytest.StartScenario`.
  With the editor closed:
  `Unity.exe -batchmode -projectPath C:/Projects/breakingthrough -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`
  (about 1.5 min). The report is the `[Playtest] smoke report:` line in the log.

## Analytics

Hawkes Analytics (Gen's `docs/analytics.md`) is the game's only analytics. Product id `theplacedepartment`. Never
add another tracker (`com.unity.analytics` was removed for that reason).

- **Package:** `Packages/com.hawkes.analytics` is an **embedded copy** of Gen's Unity bridge
  (`hawkesgen/clients/unity/com.hawkes.analytics`, same `.meta` GUIDs) with one fix: Gen's `HawkesAnalytics.cs`
  has raw U+2028/U+2029 characters in two char literals, which Roslyn reads as line breaks (CS1010), so the
  `file:` package doesn't compile. Once Gen has `'\u2028'` / `'\u2029'` there, delete the folder and put
  `"com.hawkes.analytics": "file:C:/Projects/hawkesgen/clients/unity/com.hawkes.analytics"` back in the manifest
  (and the lock entry's `version`/`source` to that path and `local`). The bridge loads the SDK into the game page
  itself, so the website repo needs nothing.
- **`RunAnalytics.cs`** decides what is sent; gameplay calls one line of it at the real moment, never per frame.
  `Plugins/WebGL/HawkesAnalyticsPage.jslib` is the page side the bridge lacks: the build stamp, debug mode, the
  themed feedback card and the playtest's view of the SDK's debug log. It starts before the first scene with
  product `theplacedepartment` (no settings asset: it makes one in code).
- **Build:** `WebGlBuild.FullDeploy` writes `Assets/Resources/HawkesBuild.json` (`{ version: "1.0+v<n>", commit,
  date }`) for the length of the build, then deletes it (git-ignored). RunAnalytics sets it as
  `window.HAWKES_BUILD`. The commit is `HAWKES_COMMIT` when the build sets it, else HEAD when nothing tracked
  differs; on the build server (merge base plus uploaded edits) beta builds have no commit, only the version.
- **Context** on every event: `mode` (`waves`), `wave`, `level` (the wave, from levelStart), `upgrades` (the run's
  boons, `Heavy2 SplitShot1`), `player_level`, `shop_ranks`, `best_wave`.
- **Events and where they come from:**

| Event | When | Called from |
|---|---|---|
| `game_ready` | The main menu is up (once per page load) | `MainMenu.Start` → `RunAnalytics.Menu` |
| `screen` | `menu`, `wave`, `level_up`, `chest`, `pause`, `settings`, `game_over` | MainMenu, LevelUpScreen, PauseMenu, SettingsPanel, `MusicianDown` |
| `run_start` | The game scene starts a run: `run`, `start_wave`, `best_wave`, `shop`, `shop_ranks`, `coins`, `touch` | `WaveDirector.Start` |
| `level_start` | A wave's fighting begins: `wave`, `birds`, `musician_hp`, `event`, `boss`, `fort` | `WaveDirector.BeginCombat` |
| `level_end` | `complete` (cleared), `death`, `quit`, `restart` (the wave keys): `kills`, `damage_taken`, `musician_hp`, `swings`, `hits` (birds struck; one swing can hit several), `hit_rate` (share of swings that struck at least one bird), `best_streak` | `TickCombat`, `Lose`, `GoToWave`, `OnDestroy` |
| `death` | The musician falls; `cause` is the bot kind that landed the last blow (`BotRoster.Slug`: `yo_bot`, `plated_yo_bot`, `spider_bot` … `titan_spider_bot`), with `boss`/`elite` and `worst` (the kind that did the most damage this run). Warrok can't fall | `Musician.Hurt(amount, by)` → `WaveDirector.Lose` |
| `run_end` | `result` `lose` (cause `musician_down`) or `quit` (the scene left mid-run): `duration`, `score`/`wave`, `waves_cleared`, `killed_by`, `worst`, kills, `swings`, `hits`, `hit_rate`, damage, level, cards, rerolls, streak, coins, `upgrades` | `RunAnalytics.MusicianDown` / `RunAbandoned` |
| `choice` | A card taken: node `level_up` or `chest`, option the `BoonId` | `LevelUpScreen.Choose` |
| `card_pick` | The same pick with `offered` (all three), `slot`, `rarity`, `rank`, `evolution` | `LevelUpScreen.Choose` |
| `feature` | `reroll`, `shop_buy` (`item`, `rank`, `cost`, `coins_left`), `settings` (sound, music, sfx on close) | LevelUpScreen, MainMenu, SettingsPanel |
| `feedback` | The game-over card ("How was that run?", 1–5 stars and a comment, id `game-over`), sent by the SDK | `RunAnalytics.MusicianDown` → `Ask` |

- **The feedback card** shows only on the game-over screen (never mid-play): the SDK's card in the game's night
  colours, at most once a session and not for 7 days after a close. Its focus is handed back to the game so "press
  any key" still works; a new run removes it.
- **Debug and playtests:** `?hawkes-analytics-debug=1`, a `?playtest=` URL or a webdriver browser turns on the SDK's
  debug log (`window.hawkesAnalytics.debugLog`); `Playtest.AnalyticsTimeline` turns it into timeline events.
- **Privacy:** the Settings panel says "Anonymous gameplay statistics help improve the game. No cookies, no
  personal data, no IP stored.", and `Tools/Web/site-footer.html` says it on every website page.
- **Keep it true:** a new screen calls `RunAnalytics.Screen`; a new way to end a run or a wave goes through
  `RunAnalytics`; anything that hurts the musician passes the bot to `Musician.Hurt`. Nothing personal and no
  per-frame events. Custom event names must match `^[a-z][a-z0-9_]{1,39}$` (Gen keeps 50 per product).

## MCP servers

`.mcp.json` at the project root configures both servers for Claude Code. `.cursor/mcp.json` is the older Cursor config.

| Server | What it is | How it connects |
|---|---|---|
| `unityMCP` | [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), Unity package `com.coplaydev.unity-mcp` | HTTP at `http://127.0.0.1:9090/mcp`. J runs that server alongside the editor |
| `blender` | MCP for Blender | `uvx mcp-for-blender` (WinGet `uvx`). It talks to a Blender GUI session on port **9876** |

Unity MCP tools that have worked well:

- `refresh_unity` with `compile: request` to compile
- `read_console` for errors and `[RunLog]` lines
- `manage_editor` play and stop
- `execute_code` to inspect state or drive a test in play mode
- `manage_camera` screenshots. A positioned view (`view_position` / `view_target`) uses a temporary camera without the post-process grade

Check `Editor.log` for `error CS` too, but only trust lines from after your edit.

If a Unity MCP call fails to connect, say so and edit the scripts instead. Do not keep retrying a dead bridge, and do not drive the editor by focusing its window. `Assets/Editor/BlenderMcpSetup.cs` probes the Blender port on load and from **Tools → Blender MCP → Ensure Server**. Blender must be a GUI session, because background mode cannot host the server.

## WebGL

The game is published into the website repo at `C:\Projects\theplacedepartmentwebsite` (theplacedepartment.com). The deploy window defaults to the folder next to this project. Change it in the window's **Website folder** field, which is saved in EditorPrefs (`bt_webgl_site_root`).

**Tools → Build → WebGL** opens the deploy window. **Run Full Deploy** does six things:

- writes `<site>/Game/vN/<gameName>`, where `<gameName>` is read from the page's `var gameName = "…"` (currently `PlaceDepartment`). Unity names the build files after that folder, so they match what the page loads
- points `<site>/Game/index.html` at the new version through `window.PLACE_BUILD`. `window.GAME_BUILD` also works
- deletes the previous version folder
- stamps the site footer from `Tools/Web/site-footer.html` ("Powered by HAWKES GEN"; on the landing page also
  "A Hawkes Biomedical company") onto every `.html` outside `Game/`, replacing the block between its
  `<!-- hawkes-footer -->` markers, and lists those pages in `<site>/site/pages.json` for the `site` playtest. Change
  the footer there, never in the website repo
- installs What's new from `Tools/Web/` (before the footer, so `whatsnew.html` gets it too): `whatsnew.html`,
  `js/hawkes-release.js`, `releases.json` if the site has none (its first release gets this deploy's build number),
  and a block between `<!-- hawkes-whatsnew -->` markers on the landing page and on `Game/index.html`. Each deploy
  replaces those blocks instead of adding them again
- runs the site's own `auto-start.bat` and opens the port it names (`http://localhost:4173/Game/`)

It does not commit or push the website. Do that in the website repo.

To build from the command line:

`Unity.exe -batchmode -nographics -quit -projectPath C:\Projects\breakingthrough -buildTarget WebGL -executeMethod WebGlBuild.Build`

Notes:

- The player starts with 256 MB of memory and gzip compression, with a decompression fallback so a static host (the site's `python -m http.server`, or GitHub Pages) can serve the `.unityweb` files without a `Content-Encoding` header.
- This project's own `Game/` folder and `auto-start.bat` (Vite on port 8089) are the old local target. The deploy no longer uses them.
- Runtime shaders that no scene references live in `Assets/Resources/Shaders/`, so builds include them.

## This machine

- Windows, project at `C:\Projects\breakingthrough`. The parent `C:\Projects\AGENTS.md` describes a Docker mount. Follow this file when the two disagree.
- `Editor.log` is at `C:\Users\jgrif\AppData\Local\Unity\Editor\Editor.log`. Older `error CS` lines stay in that file.
- Under Cursor, shell commands need to run outside the sandbox (`required_permissions: ["all"]`).

## Generative AI: Hawkes Gen

Images, textures, video, 3D, music, voice, sound and Claude all go through **Hawkes Gen**
(`C:\Projects\hawkesgen`, dashboard http://127.0.0.1:8790; start it with `hawkesgen\auto-start.bat`). It holds
the API and AWS keys and runs the shared GPU fleet. This project never holds those keys.

- **Agents:** the `hawkesgen` MCP server is in this folder's `.mcp.json`. Use `hawkesgen_presets` (project
  `theplacedepartment`) for the art direction, then `hawkesgen_run`.
- **Command line:** `C:\Projects\hawkesgen\hawkesgen.cmd run - --project theplacedepartment --preset <name> --param prompt="..." --out <folder>`
- **Art direction:** `hawkesgen/presets/theplacedepartment.json` (style line and named presets). Edit it there.
- **Unity:** Window > Hawkes Gen (package `com.hawkes.gen` in `Packages/manifest.json`) runs any tool or preset and imports the results into `Assets/HawkesGen/`.
- J's art rules above still apply to anything generated (soft effects, no sparks or fireflies).
- GPU tools cost money while a server runs. Ask J before starting servers or running big batches.
