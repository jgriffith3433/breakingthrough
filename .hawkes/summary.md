# Campaign files on demand: a phone reaches the menu before the bulk arrives

**First part, written without a compiler or the binary assets (this sandbox has neither: every mp3, png and fbx is a stripped
placeholder). Nothing here has been built or measured. Compile once, then read the new build report.**

## What changed
- **Out of the first download** (`Assets/Editor/OnDemandPack.cs`, called from `WebGlBuild.BuildWebGLTo`): the campaign's `Resources/Voice`
  (everything but `wallit_*` and `slice_*`, which Arcade speaks), `Voice/cinema`, `Cinematics/Art` (JPG, at most 1280 wide), `Portraits`,
  `Skies` and the `Baked` boss, lodger and prop meshes. They are converted, named by content hash, listed in `index.json` with their
  groups, and laid in `<build>/StreamingAssets/OnDemand/`. The originals are only *moved* to `Assets/OnDemandStage~` for the build and
  moved back in a `finally` (and on editor load if the editor died), so git sees no change.
- **Fetched on the player's choice** (`Assets/Scripts/OnDemand.cs`): `CampaignPanel.OpenSlot` fetches `core` ("Pulling the case file"),
  `ChapterScene.BeginChapter` fetches `core` + `ch<n>` ("Filing Chapter n"), `Cutscene.PlayShots` fetches `reel_<id>` for a reel played on
  its own. UnityWebRequest coroutines, six at a time, two tries, no polling, nothing fetched at startup. A voice no chapter names (a
  lodger, a boss bark) is fetched when spoken and plays when it lands, the subtitle running meanwhile. Choosing another chapter drops the
  last chapter's stills and voices.
- **Progress line** (`OnDemandLine.cs`): one line on a night-blue plate, Special Elite, a red bar, bottom middle above the safe inset,
  no raycasts so the screen under it stays alive. It reads "Filing Chapter 1... 41%".
- **Everything reads through `OnDemand.Audio/Texture/Text`**, which look at what was fetched and then at `Resources`, so the editor, a build that
  kept its files, and any path I did not reach keep working (`Need` then calls back at once). Call sites changed: `CastVoice`, `Cast`
  (portraits), `BakedMesh`, `LevelDress` (skies), `CutsceneShots` (stills, reel voices), `LoadScreen`, and three probes in `Playtest.cs`.
- **Build settings** (`WebGlBuild.cs`): Brotli (was Gzip: the brief said "keep Brotli" but the code used Gzip; Studio's hosts serve `.br`
  with its Content-Encoding and the fallback stays on), `dataCaching` on, debug symbols off, `stripEngineCode` set explicitly. After a
  build it logs `[WebGlBuild] Download size` (data, wasm, on-demand MB, the data file by folder, the 25 largest assets, what moved by
  group) and writes `build-report.txt` beside the build.
- **Playtest `first-load`** (`PlaytestLoad.cs`, 390x844 touch through UI pointer events): menu interactive and nothing campaign-sized fetched
  yet; Campaign, case file: progress line checked (words, on screen, inside the safe area, at the bottom); Begin: control within 15 s;
  no failed file, no frame over 1 s while fetching, no missing voice. Named in `.hawkes/scenario.txt` with `{"mobile":true}`.
- `AGENTS.md`: new row, the Brotli note and a "Download size" section.

## Not done, and why
- **Before/after sizes are not in AGENTS.md as numbers for "after".** I could not build or see the assets. The table has the "before" (about
  90 MB, the brief) and says to fill "after" from the first build's `build-report.txt`.
- **Managed stripping and the module list in `Packages/manifest.json` are untouched.** Engine code stripping is already what drops unused
  modules from the player; editing the manifest could not be compiled here and would change nothing once stripping is on.
- **Scenes were not moved to bundles.** `Campaign`, `Map_*` and `Level_1_Yard` are tiny empty scenes filled in code (64 KB of YAML for
  all of `Assets/Maps`); the weight is in audio, stills, meshes and portraits, which are moved.
- **Crunch / mobile max sizes** were already in `TextureSizeRules` (1024, crunch, stills 1280); nothing new there. The stills that leave Resources
  are re-encoded to JPG at 1280 in `OnDemandPack` instead.
- Hub lodgers' and the Server's voice lines are not in a chapter group; they are fetched on first speak. If the report shows `(by key)`
  is big, name them in `AssignGroups`.

## Risks to check first
- Compile: new `OnDemand.cs`, `OnDemandLine.cs`, `PlaytestLoad.cs`, `OnDemandPack.cs`; edits in `WebGlBuild.cs` (try/finally around BuildPlayer) and
  `Playtest.cs`. `BuildReport.packedAssets` may warn as obsolete.
- `OnDemandPack` assumes the reel→chapter map: Courier 1, Foreman 3, Server 4, Order-Bearer 6, endings and credits 6 (AGENTS.md).
- Fetched stills are uncompressed textures (about 3.7 MB each at 1280x720); `Release` frees them when another chapter is chosen.
- Memory/Brotli: if a host serves `.br` without Content-Encoding the loader's JS fallback decompresses it (slower, but works).

## Conflicts (merge of origin/beta)
One file conflicted: `Assets/Scripts/Playtest.cs`, one line, the `LevelScene.ChapterOneIsLevel = ...` list in the scenario setup.
Beta's change added `explore` to it (so Chapter 1 loads Level_1_Yard); mine added `first-load`. I kept both ids and beta's comment.
Nothing else conflicted: `PlaytestExplore.cs` came in from beta unchanged, and the `explore` and `first-load` scenarios are both still
registered. No compiler runs in this sandbox, so the merge has not been compiled; the resolved line is a plain `||` chain.

Files touched that others may edit: `Playtest.cs` (one entry, one dispatch block, one flag list), `WebGlBuild.cs`, `CampaignPanel.cs`,
`ChapterScene.cs`, `CutsceneShots.cs`: each edit is small.

## GPU
No GPU generation was used or needed; nothing to estimate.
