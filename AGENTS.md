# Agent guide

Unity 6.3 LTS (`6000.3.7f1`), built-in pipeline, old Input Manager (`activeInputHandler` 0). Design detail is in `README.md`. This file is how to change the project without breaking it.

The player is J. Prefer editing the scripts directly. Do not commit unless asked.

## Where code lives

Runtime gameplay is `Assets/Scripts/`. Editor wiring is `Assets/Editor/`. Do not put gameplay in editor scripts.

| If you are adding… | Touch |
|---|---|
| A bird | `FowlRoster.cs` (`ForTier`, `UnlockOf`), then spawn already picks it up through `ForSpawn` |
| Wave rules, pauses, skip keys | `WaveDirector.cs` |
| Damage, size, jump-land, foot stomp, shake | `PlayerGrowth.cs` |
| Climbing or getting hit | `Enemy.cs` |
| A shop upgrade | `UpgradeId` in `MetaProgress.cs` with a **new number at the end**, a `Catalog` row, a real caller, and a visible mark in `UpgradeLook.cs`. The shop is five upgrades, three ranks each. Slots 2, 3, 5, 7, and 8 are retired. Do not renumber or reuse them. `Blast` is rockets from the bow: the bow pose stays, and the nocked shot becomes a rocket |
| A fort piece | `FortKind`, `FortStock`, `DefencePiece`, and `BuildMode` in `Fort.cs` |
| A combat or build tip | `ControlsHelp.cs`. A tip holds for a band of waves, then the next one replaces it and flashes |
| A wave-unlocked ability on the left | `AbilityList.cs`. Jump land is wave 20, auto shoot is `AutoWave`, stomp is `StompWave`, shake off is `ClimbWave` |
| The flock list on the right | `WaveRoster.cs`. It reads `WaveDirector.CopyRoster`. Each row is one bird in the wave, the number still to fall, and a bar that fills as that type dies |
| A body animation | A Mixamo humanoid FBX under `Assets/Art/`, listed in `SetupWarrok.Clips`, then a State id and `AddAction` |
| A sound | `Assets/Resources/Sfx/Name.mp3`, played with `GameAudio.Play("Name")`. Current names: `shot`, `hit`, `death`, `stomp`, `launch`, `coin`, `shake` |

Bird counts and the on-screen mix are `TurkeyCount` and `ScreenCaps` in `WaveDirector`. Turkeys match the wave number until wave 20, then pull ahead, and from wave 50 they swarm. Every unlocked size gets at least one on-screen slot. Extra slots favor the smaller birds, and turkeys fill what is left. Health is authored on the `FowlDef`. Spawn size is applied only in `ForSpawn`. Jump-landing uses `DebutWave`: turkeys are 0, everyone else is their unlock wave, and they can be landed on when `wave - DebutWave >= 20`. From `ClimbWave`, a bird climbs the player instead of the musician when `wave - DebutWave >= 5`.

## Editor scripts

`SetupWarrok`, `SetupMainMenu`, `FixMixamoImport`, and `BlenderMcpSetup` use `[InitializeOnLoad]`.

- Reschedule on `EditorApplication.delayCall` while compiling, updating, or playing.
- Return immediately unless the open scene is `scene2` when the script mutates that scene.
- Cap retries. `FixMixamoImport` stops after 8 passes.
- `SetupWarrok` reimports clips, rewrites `Hero.controller`, and saves the scene. New states must be applied **after** the loop that sets `canTransitionToSelf` false, or the next editor load clears them.
- Do not retarget `Assets/hero/ANIM_HERO_*` kick clips onto Warrok. They are generic Bip01.

While Unity is playing, script and controller edits are not live. Leave play mode before expecting a compile. `SetupWarrok` logging `InvalidOperationException` on `MarkSceneDirty` during play is expected.

Prefer an editor script over hand-editing `scene2.unity` while the editor is open. Unity will overwrite the YAML.

## Do not change

- The equipped bow local pose in `BowEquipment.cs`: position `(-0.009, -0.066, 0.035)`, Euler `(13, 350, 358)`, scale `0.353789777`.
- `.meta` files, except by letting Unity create them. Never delete a `.meta` without the asset.
- `Library/`, `Temp/`, `obj/`.
- `Packages/manifest.json` must not gain `com.unity.purchasing`.
- The night knoll, cottages, piano pirate, and bow textures, unless the task is specifically about them.
- Existing files in `Assets/Resources/Sfx/`. Do not regenerate them.
- `KeyCode.Plus` does not exist. Wave skip uses `Equals`, `KeypadPlus`, `Minus`, and `KeypadMinus`.

## Scripts and encoding

Match the surrounding C# style. No new namespaces.

Write `.cs` files as UTF-8. A BOM is accepted (`CharacterMovement.cs` has one). UTF-16 LE breaks the compiler (`CS1056`). `ZeldaStyleCamera.cs` is UTF-8, but some readers show it as one garbled line. If a search-and-replace fails, edit it with Python and keep LF.

The ground ray is Ground only (`1 << 3`) and ignores triggers. `MovementMotor` must keep applying gravity while `FallVelocity > 0.5`, or the player rockets upward. Jump only when grounded, cooled down, and not rising.

`CharacterController` is not scaled by transform. `PlayerGrowth` scales height, radius, and center from the scene baseline (height 2, radius 0.5). Do not save a grown capsule back into the scene.

## Animator

`applyRootMotion` is false. Gameplay calls `SetInteger("State", n)` and `CrossFade`. Upper-body shots call `SetTrigger("Fire")` and must not set base State to 2.

While `PlayerGrowth.Rooted` is set, or `Enemy.Climbers > 0`, `CharacterMovement` moves nowhere useful for actions: no jump, and `AnimationMove` must not overwrite State 5 or 7. Shooting on the upper layer can continue.

## MCP servers

Project config is `.cursor/mcp.json`. Both servers are started with `uvx` from WinGet (`C:/Users/jgrif/AppData/Local/Microsoft/WinGet/Links/uvx.exe`).

| Server | What it is | How it starts |
|---|---|---|
| `unityMCP` | [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | `uvx --from mcpforunityserver mcp-for-unity --transport stdio` |
| `blender` | MCP for Blender | `uvx mcp-for-blender` |

Unity also has the package `com.coplaydev.unity-mcp` (`MCPForUnity` on `main`). The editor and the MCP process have to be running together. This session's `user-unityMCP` bridge has been in an error state. If a Unity MCP call fails auth or connection, say so and edit the scripts. Do not keep retrying a dead bridge, and do not drive the editor by focusing its window.

Blender MCP is a socket in a Blender GUI session, port **9876**, not a Unity package. `Assets/Editor/BlenderMcpSetup.cs` probes that port when the editor loads and from **Tools → Blender MCP → Ensure Server**. It enables the `blender_mcp` add-on and starts the server. Blender has to be a GUI session. Background mode cannot host it.

Cursor's own MCP namespaces (browser, Origin, and so on) are separate from `.cursor/mcp.json`.

## This machine

Shell commands need to run outside the sandbox (`required_permissions: ["all"]`). The workspace sandbox cannot isolate the filesystem here.

`Editor.log` is `C:\Users\jgrif\AppData\Local\Unity\Editor\Editor.log`. Trust only lines from after your edit. Older `error CS` lines stay in that file.

There is a parent `C:\Projects\AGENTS.md` about a Docker mount at `/home/node/workspace/`. This game is edited on Windows at `C:\Projects\breakingthrough`. Follow this file when the two disagree.
