# Polish pass 2 (stricter pass): first part, static audit

**Read this first.** This part was done in a sandbox with no Unity editor, no browser and no GPU. There are **no new photos or footage** in it, and nothing here was compiled. Every finding below comes from reading code and doing layout arithmetic. Evidence is marked `code` (read) or `math` (checked by hand). Anything that needs eyes is listed as **Open** and has to be judged from a beta playtest. Criteria 2 and 4 (after photos, footage of every chapter) are **not met yet**.

`Docs/PolishPass1.md` is not in the repo. Pass 1 (#129) left only its commit, so the regression check below is made from that diff (touch prompts, touch-only Chapter 1, photo fps, KitMesh crest gem, boss marks and coin).

## Findings

| # | Area | Where | Finding | Severity | Fix | Status |
|---|---|---|---|---|---|---|
| 1 | Camera at walls | `ZeldaStyleCamera.CompensateForWalls` | Its linecast only reacts to a collider tagged `wall`, and then clears `HitWall` anyway. Level walls (gate hall, factory floor, office, Chapter 1 buildings) have no tag, so in the levels the camera passes through walls and shows the void or the back of the facade. Pass 1 never found it because the playtest camera sits in open lanes. (code) | Medium | `levelMode` (set in `ConfigureForLevel`) casts from Wallit's head to the camera and pulls the camera in front of the first solid, ignoring Wallit, bots and the companion, 0.8 m minimum. Arcade and the collider-free chapter maps do not use it. | Fixed, uncompiled |
| 2 | Small props that are boxes | `Musician.BuildKnife` | Slice's knife was two scaled cubes, and the knife is in her hand in the hub and every chapter. (code) | Medium | A diamond-section blade with a point and guard, and a round grip, as `BotMesh` meshes. | Fixed, uncompiled |
| 3 | Small props that are boxes | `Pickups.Build` (chest fallback), `Fort.cs` pieces, `BoonEffects.MakeBlade` fallback | Cubes, but only when `ArtLibrary` has no model, or in Arcade build mode. The campaign never reaches them. (code) | Low | None: Arcade is out of scope. | Not fixed |
| 4 | Character consistency | `ChapterHub.BuildPeople`, `Companion.Build` | Capsule stand-ins if the Wallit or Slice prefab is missing. Both load the same prefab and skins as the chapters, so the look matches while the prefabs exist. (code) | Low | None needed. | Not fixed |
| 5 | Touch placement | `TouchControls.Layout` (landscape) | Every pad pair was checked for overlap in the Wide layout: Swing/Jump, Swing/Lock, Lock/Jump, Interact/Lock, Dodge/Jump, Revive/Dodge, Revive/Lock, the three ability pads against Interact, Shake, Lock and Journal. The least clearance is Interact against Lock (160 against 145 units), with no overlap. (math) | Info | None. | Verified |
| 6 | Thumb reach | `TouchControls.Layout` | The ability pads sit 430 units up and the Revive pad 250 up, 440 in. The Swing thumb can reach them only by lifting off Swing mid-fight. This is the weakest spot on a small phone. (math) | Low | Left. Moving them needs a photo on a 16:9 and a 21:9 phone to avoid new overlaps. | Open |
| 7 | Sound and music gaps | `MusicDirector.Play`, `Resources/Music`, `Resources/Voice` | A missing clip makes `Play` fade to silence with no warning. `Assets/Resources/Music` and the voice mp3s are not in this checkout, so this audit cannot say whether any chapter plays silent. (code) | Medium (unverified) | Check beta's `score` playtest for `track|` events against `Music.Changes`. No code change: a new warning would be counted as a console error. | Open |
| 8 | Load hitches, lowest fps | `PlaytestLevels` `photos-fps` | The metric already logs a mean and a worst per scenario. This pass could not run it, so there is no new number. (code) | Open | Re-run on beta and compare with pass 1. | Open |
| 9 | Pacing | `ChapterPacing.cs` | Opening gate (30 m walked and the Receipt), at most 3 encounters, 10 s of quiet between them. This matches the sparse-fighting rule on paper. Time between story beats could not be measured. (code) | Info | None. | Open |
| 10 | Lighting continuity | `ChapterMap.Load`, `ChapterHub` | The hub redresses `OfficeMap` while the chapters apply their own lighting a few frames after `NightAtmosphere`. I could not compare the first frames after a transition. (code) | Low (unverified) | Compare the hub and the first chapter photo for a lighting pop. | Open |
| 11 | Dead space, repeated props, scale, clipping at doors and stairs | `OwnMap*.cs`, `LevelDress.cs`, `OwnMapOffice.cs` | Needs photos. I did not call these fine. | Unknown | None. | Open |
| 12 | Characters' personality in play and writing | `Story.cs`, `SliceVoice`, `WallitVoice` | Not re-read this pass. | Unknown | None. | Open |

Findings 1 and 2 are the only two medium findings that can be fixed from code alone. They are fixed.

## Pass 1 regression check (from the #129 diff)

- Touch prompts: `Interact` pad and `Tap:` prompts are still wired in `TouchControls.Layout`. (code)
- Pass 1's KitMesh crest gem, boss marks and coin were not touched. (code)
- Touch-only Chapter 1 and `photos-fps` live in `PlaytestTouch.cs` and `PlaytestLevels.cs`. I did not edit either, and did not run them. (code)

## Before and after

Not recorded. There is no editor or browser here. Beta's playtest is the first real before and after. Reading the findings this way: for #1, stand in the gate hall or factory floor with your back to a wall and turn the camera. For #2, look at Slice's knife in the hub.

## Left for the next part

Phone landscape photos at two aspect ratios, desktop photos, footage of every chapter and the hub, the touch-only full chapter plus Slice by tapping, the Arcade scenarios, and the checks for #6, #7, #8, #10, #11 and #12. Compile once. No GPU work was needed, so there is no estimate to give.

## Second attempt (beta build repair)

Beta's red build (run r_866907a2325c) was `playtest.run needs 9 and 1 is left`: the studio was out of credits. It was not a compile error. I re-read the C# from #132 (`Musician.BuildKnife`, `ZeldaStyleCamera.CompensateForWalls`) against `BotMesh` (`Box`, `Rod`, `ToMesh(string)`) and `using System.Collections.Generic`: every call exists and resolves. Unity compile not run here.

No browser or editor in this sandbox either, so there are still no new photos or footage and no before/after numbers. Findings 6, 7, 8, 10, 11 and 12 stay **Open** until beta's `campaign-touch` run (touch-only Chapter 1 to the hub, Slice by tapping, phone landscape, `touch-layout`, `photos-fps`) is read. Criteria 2 and 4 remain unmet from this checkout; say so rather than claim it.
