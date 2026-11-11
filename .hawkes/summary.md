# Explore Chapter 1 stall (after Arcade)

## What was wrong
On the `explore` scenario, after Arcade the bot waited **60 s** for Chapter 1's level scene and finished **stuck**, with no `chapter-map` / camera-feel metrics and no corner pull-in.

Cause: playtests set `LevelScene.ChapterOneIsLevel = false` unless the scenario id is on an allowlist. `explore` was missing, so Begin started the old `ChapterRun` path while `PlaytestExplore.LoadChapter(0)` waited for `LevelScene` / `Level_*` that never appeared.

## What changed
- `Assets/Scripts/Playtest.cs` — add `explore` to the `ChapterOneIsLevel` allowlist so Begin loads `Level_1_Yard`.
- `Assets/Scripts/PlaytestExplore.cs` — Ch1 load capped at **20 s** with a named `stuck-cell` report (scene, `ChapterOneIsLevel`, LevelScene/ChapterRun); on success logs `chapter-map` / `map-change`, `explore.arcadeToChapter1Seconds`; phone portrait forces touch and steers via `TouchControls.BotFinger` with `first-move` / `explore.touchMoveRegistered`; MapPass still runs corner pull-in and `feel.cameraInsideFrames.<map>` for Chapter 1.
- `.hawkes/scenario.txt` — `explore`

No ZeldaStyleCamera rework. No new features. GPU budget unused ($0).

## Conflicts
None (branch clean vs task start).

## Other agents
`c_71d09c558c12` (on-demand chapter download) had no files yet; this change does not touch streaming/load size.

## Checks
- Hand-read C# for CS issues; brace balance OK.
- Unity compile not run here (no editor in this sandbox).
- Code reviewer: **merge**.
- Fun reviewer: **demo** on living-world scores — expected: this pass only fixes the explore→Ch1 handoff onto the existing Sorting Yard; new yard pressure / fight probes are out of the brief's "no wider scope".

## Acceptance mapping
1. `chapter-map` / `map-change` + `explore.arcadeToChapter1Seconds` (bug if >30)
2. `first-move` / `stuck-cell` / `map-explore` within the walk; load stall reports `stuck-cell` by 20 s
3. `CameraFeel.Report` → `feel.cameraInsideFrames.Level_1_Yard` (or active scene name)
4. Specific stuck report or finished explore — not silent 60 s
5. Phone: ForceTouch + BotFinger; `explore.touchMoveRegistered` / `touch-stick`
