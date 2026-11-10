# Full campaign review: ending stills confirmed, campaign-full updated

## Ending stills (GPU)
The brief’s “still to make” list is **stale**. All 18 `ending_ledger|doors|open_1..6_back.jpg` (1280×720 JFIF) are already in git from #167/#169 (`Tools/Cinematics/keyframes.json` has job+asset ids). Sparse checkout hid them on disk; after materialising, sizes check out (~110–163 KB each). **No new Gen jobs. $0 of $0.77 spent.** No `.hawkes/pending-gpu.txt`.

Soft spots (same as prior judge): `ending_doors_5_back` reads as one door; `ending_open_1_back` uses a blank sign (regenerated to avoid garbled text); a few stills have soft light motes.

## What changed (playtest)
`campaign-full` was still forcing the **old Chapter 1 chapter-run** (`ChapterOneIsLevel` false), so a “full campaign” video never showed the Sorting Yard. Fixed:

- `LevelScene.ChapterOneIsLevel` on for `campaign-full`
- Chapter 1 via `PlaytestBeats.Run(fromCard: true, finish: false)`, then hub → `ShowCard(1)` → chapters 2–6
- Per-chapter `chapter-verdict`: map/own scene, memory (bug >1024 MB), route/landmark/hazard|setpiece, reused-map bug
- Opening reels: watch ~3 shots, log `cinematics-art`, bug on PLACEHOLDER, then Enter
- Closing/ending reels: same art logging; Filed + ending-variant path no longer assumes page-only finale
- `chapter-cinematics` now includes `ending_ledger`, `ending_doors`, `ending_open`
- Metrics: `fullDistinctMaps`, `fullCinematicKeyframes`, `fullCinematicPlaceholders`, `fullChoicesMade`

Files: `Assets/Scripts/Playtest.cs`, `Assets/Scripts/PlaytestBeats.cs`. Scenario: `campaign-full`.

## Honest per-chapter verdict (repo audit; no browser footage in this sandbox)

| Ch | Map distinct | Cinematic | Choices | Set piece | Notes |
|---|---|---|---|---|---|
| 1 Sorting Yard | **Pass** (`Level_1_Yard`) | **Pass** (talk/barks/Courier; level path, not `ch1_open` reel) | **Pass** (1.c1 keep notice) | **Pass** (Courier notices) | Now played by campaign-full |
| 2 Edge/Gate | **Pass** (`Map_Edge`) | **Pass** (`ch2_open/close` art in repo) | **Pass** (Crossroads/After) | **Pass** (gate / INVALID) | |
| 3 Clearing Floor | **Pass** (`Map_Floor`) | **Pass** | **Pass** | **Pass** (belt / lever) | |
| 4 Long Walk | **Pass** (`Map_Walk`) | **Pass** | **Pass** (Server paper) | **Pass** (lamps / refuse) | |
| 5 Reading Room | **Pass** (`Map_Reading`) | **Pass** | **Pass** | **Pass** (forms blank / Registrar) | |
| 6 Filed + ADDRESS FILED | **Pass** (`Map_Filed`) | **Pass** (`ch6_close` + variant keyframes) | **Pass** (ending notes) | **Pass** (Order / door number) | Variant stills real |

Fun reviewer: **thin** (not demo). Biggest gaps it named are in-world ending staging and causeway/gate ambient beats — out of scope for this stills+scenario pass; not fixed here.

## Checks
- Ending JPGs: real JFIF 1280×720 in git; 6 spot-judged
- C# brace/paren balance vs HEAD after strip: clean
- Unity compile **not run here** (no editor); Studio build compiles
- Conflicts: none
- Arcade scenarios: unchanged
