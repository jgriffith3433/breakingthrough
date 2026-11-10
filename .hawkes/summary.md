# Camera feel + explore detector

## What was wrong
Chapter kit maps were dressed without colliders, so `ZeldaStyleCamera.CompensateForWalls` (even the levelMode raycast) had nothing to hit and the camera clipped through walls. Arcade/tag-`wall` path was dead. Idle recentre and lock ease fields existed but did nothing useful. The `explore` scenario only walked Arcade and had no camera-feel numbers.

## What changed
- **`CamBlock` layer** (TagManager layer 7) + `CamBlock.cs`: kit walls/buildings get a BoxCollider on that layer via `KitCatalog.Make`; movers ignore it (`Physics.IgnoreLayerCollision`). Proxy boxes on Chapter 1 stay on Default and still block both Wallit and the camera.
- **`ZeldaStyleCamera`**: sphere-cast pull-in (radius ≥ near clip) with fast pull-in / slow ease-out, follow damping fixed to a real seconds constant, FOV clamped 60–75 (default 65; hub camera 60), near clip ≥ 0.15, idle recentre behind Wallit after 2 s, lock-on and release ease on `orbitYaw`.
- **`CameraFeel`**: per-frame detector (inside solid, within 0.15 m of walls, snaps > 30°, jitter); metrics `feel.cameraInsideFrames.<map>` etc.
- **`explore`**: walks Arcade, chapters 1–6 and the hub; reports explore + camera-feel metrics; probes corner pull-in, idle recentre, lock ease. Scenario budget 1200 s.

## Files
`CamBlock.cs`, `CameraFeel.cs`, `ZeldaStyleCamera.cs`, `PlaytestExplore.cs`, `Playtest.cs`, `KitCatalog.cs`, `LevelDress.cs`, `OwnMap.cs`, `ChapterHub.cs`, `ProjectSettings/TagManager.asset`

## Checks
- Read C# for CS0102/CS0111 duplicates and call sites; Unity compile **not run here** (no editor in this sandbox).
- Fun-reviewer: **thin** (camera polish helps immersion; does not fill living-world gaps — out of scope for this task).
- Reviewer first pass: **revise** — fixed idle probe window (4.5 s), explore budget (shorter rings + 1200 s), Ground MeshCollider false positives, lock **release** ease. Re-review: plumbing matches criteria; live `explore` still needed on beta to prove `feel.cameraInsideFrames.* = 0`.
- Conflicts: none.

## Left for beta
Run `explore` and confirm every map’s `feel.cameraInsideFrames.<map>` is 0, corner/idle/lock events green, and a lock-on clip shows no snap. GPU budget: none used / none needed.

## Scenario
`explore` (see `.hawkes/scenario.txt`)
