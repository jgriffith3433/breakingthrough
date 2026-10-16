# Agent guide

Unity 6.3 LTS (`6000.3.7f1`), built-in render pipeline, old Input Manager (`activeInputHandler` 0). `README.md` explains how the game plays. This file explains how to change it without breaking it.

The player is J. Prefer editing the scripts directly. Do not commit unless asked.

## Do not change

- The equipped bow's local pose in `BowEquipment.cs`: position `(-0.009, -0.066, 0.035)`, Euler `(13, 350, 358)`, scale `0.353789777`.
- `.meta` files, except by letting Unity create them. Never delete a `.meta` without its asset.
- `Library/`, `Temp/`, `obj/`.
- `Packages/manifest.json` must not gain `com.unity.purchasing`.
- Existing files in `Assets/Resources/Sfx/`. Do not regenerate them.
- The piano pirate and the bow textures, unless the task is about them.
- Retired `UpgradeId` slots 2, 3, 5, 7 and 8. Never renumber or reuse them, because saved ranks are packed by index.
- J's standing art rules:
  - No camera shake.
  - No tiny spark, glitter, ember or firefly particles. Effects stay soft: flashes, smoke, a few feathers, rings.
  - Keep the playfield (within 150 m of the piano) free of anything solid. Birds walk straight lines and do not pathfind.
- `KeyCode.Plus` does not exist. Wave skip uses `Equals`, `KeypadPlus`, `Minus` and `KeypadMinus`.

## Where code lives

Runtime gameplay is in `Assets/Scripts/`. Editor wiring is in `Assets/Editor/`. Do not put gameplay in editor scripts.

| To change… | Touch |
|---|---|
| A balance number | `Tuning.cs` first: wave-power waves, level curve, feather values, elite odds, golden gobbler, boss health, events, streaks, coins, bird health growth |
| Wave flow, spawning, events, bosses, coins per wave | `WaveDirector.cs`. Bird counts are `TurkeyCount`, `WaveQuota` and `ScreenCaps`. Events are `WaveEvent` plus `RollEvent` |
| A bird species | `FowlRoster.cs` (`ForTier`, `UnlockOf`). Spawning picks it up through `ForSpawn`, which is the only place spawn size is applied |
| Bird behavior, health, status, elites, golden gobbler, bosses | `Enemy.cs` (`ApplyFowl`, `MakeElite`, `MakeGolden`, `MakeBoss`, `TickStatus`) |
| Anything that damages a bird | `Enemy.Damage(amount, DamageKind, crit)`. Arrows go through `Combat.ArrowHit`, so crits, marks, fire, frost and lightning apply. `Enemy.Hit` is one plain blow at arrow strength, used by spikes, towers and stomps. Deaths go through `Enemy.Die` → `Combat.OnKill` (feathers, chests, bombs, streak, run log) |
| An in-run boon | `BoonId` and a row in `RunBoons.All`, **in enum order**, because the array is indexed by the enum. Read the boon's rank where it acts: `Combat` for hits and kills, `ArrowFlight` (in `BowEquipment.cs`) for arrow behavior, `BoonEffects` for things that tick on their own. An evolution uses `E(...)` and names two parents that have max ranks |
| Level-up cards | `LevelUpScreen.cs` |
| XP bar, streak counter, boss bar, boon list | `RunHud.cs` |
| Streak and hit-stop | `Juice.cs` (`Combo`, `HitStop`) |
| Feathers and chests | `Pickups.cs` |
| Size, jump-landing, foot stomp, shake-off | `PlayerGrowth.cs` |
| A shop upgrade | `UpgradeId` in `MetaProgress.cs` with a **new number at the end**, a `Catalog` row, a real caller, and a visible mark in `UpgradeLook.cs`. 10 is Four-Leaf Clover, 11 Opening Act, 12 Stage Hands. Rank costs come from `Cost` |
| A fort piece | `FortKind`, `FortStock`, `DefencePiece` and `BuildMode` in `Fort.cs` |
| A combat or build tip | `ControlsHelp.cs` |
| The wave-power list on the left | `AbilityList.cs` |
| The bird list on the right | `WaveRoster.cs`, which reads `WaveDirector.CopyRoster` |
| A sound | `Assets/Resources/Sfx/Name.mp3`, played with `GameAudio.Play("Name", volume, pitch)` |
| A body animation | A Mixamo humanoid FBX under `Assets/Art/`, listed in `SetupWarrok.Clips`, then a State id and `AddAction` |
| Bird models | `Tools/Blender/bt_birds.py`, then `export("<Kind>")` in Blender. Output goes to `Assets/Resources/Models/Birds/` |
| Map scenery models | `Tools/Blender/bt_arena.py`, then `export_arena()`. Output goes to `Assets/Art/Arena/` |
| The map | `Assets/Editor/BuildArena.cs`, then **Tools → Arena → Rebuild Arena** |
| Night lighting, lanterns, mist, notes | `NightAtmosphere.cs` |
| Bloom and color grade | `NightGrade.cs` with `Resources/Shaders/BTPost.shader` |
| A particle effect | `Fx.cs`: add an `FxKind` and a `Make(...)` line, or reuse a recipe. Never spawn primitive spheres for effects. Status (burning, frozen, golden) is a tint on the bird in `Enemy.TickFlash`, not particles |
| UI panels and bars | `UiSkin.Round` and `UiSkin.Bar` |

## Rules the code depends on

- **`Enemy.Roster`** lists every live bird. Use it instead of `FindObjectsByType<Enemy>` in anything that runs every frame. Copy it before you damage birds in a loop, because a Splitter death adds hatchlings mid-loop.
- **Bird models:**
  - Parts must stay named `Body`, `Head`, `WingL`, `WingR`, `LegL` and `LegR`.
  - The FBX import mirrors X, so the code reads each wing's side from its local position.
  - Blender's −Y is the bird's front. `BuildModel` turns the model around if the head ends up behind.
  - `BirdArt` tints by copying meshes, so the meshes must stay readable. `ModelImportRules.cs` enforces that, together with smooth normals at a 60° crease angle.
- **Shader:** birds use `BirdArt.Shared`, which is `Knoll/VertexColorLit` with `_GlowBand` 0 and a strong `_RimStrength`. Use `BirdArt.Glowing` only where warm vertex colors should emit (Thunderbird, lantern glass). `KnollLit.mat` keeps `_RimStrength` near 0 so scenery does not glow.
- **Hit flash:** it sets a `MaterialPropertyBlock` and clears it when the flash ends, so the bird batches with the flock again. Keep that cleanup.
- **Pausing:** `LevelUpScreen` pauses with `Time.timeScale = 0`. `Juice.HitStop` uses 0.05 briefly and never overrides a real pause. The camera, firing and build clicks all check `LevelUpScreen.IsOpen`.
- **Jump-landing and climbing:** these use `DebutWave`. Turkeys count as 0, and every other species counts as its unlock wave.
- **Map:** the arena is centered on `PianoRecital`. The ground is on the Ground layer (`1 << 3`) with a mesh collider. Scenery has no colliders. The `Boundary` wall is on Ignore Raycast at 205 m. `DressGrassyKnoll` and `PlacePracticeTargets` skip when `Arena` exists, so the old knoll never comes back.
- **Movement:**
  - The ground ray is Ground only and ignores triggers.
  - `MovementMotor` must keep applying gravity while `FallVelocity > 0.5`, or the player rockets upward.
  - Jump only when grounded, cooled down and not rising.
- **Player size:** `CharacterController` is not scaled by its transform. `PlayerGrowth` scales height, radius and center from the scene baseline (height 2, radius 0.5). Never save a grown capsule back into the scene.

## Animator

- `applyRootMotion` is false. Gameplay calls `SetInteger("State", n)` and `CrossFade`.
- Shots on the upper layer call `SetTrigger("Fire")` and must not set the base State to 2, or the jump is cancelled.
- The upper-layer Shoot state's speed follows the `FireSpeed` float (default 1), which `CharacterMovement` sets every frame.
- While `PlayerGrowth.Rooted` is set or `Enemy.Climbers > 0`, there is no jump, and `AnimationMove` must not overwrite State 5 or 7. Shooting can continue.

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
- **Autopilot:** `DevAutopilot` is a perfect archer by the piano. Read its `[RunLog]` lines in the console or `runlog.csv`. It is easier than real play, so treat its results as an upper bound.
- **Restore project settings after tests.** Setting `Application.runInBackground` during play writes `runInBackground: 1` into `ProjectSettings.asset`, and quality changes can stick too. Before handing back, check `git diff ProjectSettings/` and restore anything a test changed.
- **The game view only updates while Unity is focused or set to run in the background.** If `Time.time` stops advancing during an MCP session, that is why.

## Playtests

The game implements the Hawkes playtest contract v1 (`C:\Projects\hawkesstudio\docs\playtest.md`) through the
`com.hawkes.playtest` package. Hawkes Studio runs it daily against the WebGL build (`?playtest=<id>` in the URL, or
`window.hawkesPlaytest.start(id)`), records it and has Claude review it.

- **Bot:** `Assets/Scripts/Playtest.cs` registers product `theplacedepartment` at startup. Scenarios: `smoke` (menu,
  Enter, about 30 s of play), `run` (until the musician falls, picking cards; capped at 15 min), `menus` (shop
  clicks, start, one level-up card with a reroll).
- **Real input:** gameplay reads keys and the mouse through `PlayInput` (`Assets/Scripts/PlayInput.cs`), which is
  `UnityEngine.Input` plus whatever the bot holds. Use `PlayInput.GetKey...` in new gameplay input code, or the bot
  can't press it. The bot holds Shift (lock-on) and the left button (shoot), walks back to the piano with WASD,
  presses Enter to skip breaks and 1-3 for cards. It plays in `MetaProgress.EnterSandbox(true)`, so it never
  touches the save.
- **Report:** wave, kills, damage, shots, hits, level, levelUps, musician HP, coins, run seconds and fps. Problems:
  errors and exceptions (from the package), missing-asset warnings, menu art missing, "no kills for 45 s", a wave
  that doesn't end in 240 s, a level-up card that won't close, a game-over screen that doesn't return to the menu.
- **Test:** `Assets/Tests/PlayMode/PlaytestSmokeTest.cs` runs `smoke` through `HawkesPlaytest.StartScenario`.
  With the editor closed:
  `Unity.exe -batchmode -projectPath C:/Projects/breakingthrough -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`
  (about 1.5 min). The report is the `[Playtest] smoke report:` line in the log.

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

**Tools → Build → WebGL** opens the deploy window. **Run Full Deploy** does four things:

- writes `<site>/Game/vN/<gameName>`, where `<gameName>` is read from the page's `var gameName = "…"` (currently `PlaceDepartment`). Unity names the build files after that folder, so they match what the page loads
- points `<site>/Game/index.html` at the new version through `window.PLACE_BUILD`. `window.GAME_BUILD` also works
- deletes the previous version folder
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
