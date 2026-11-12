# Fix beta build: PlaytestOpening CS0120 (transitions already shipped)

## What was wrong
Beta build web 122 failed after the stamped-transitions change merged: `Assets/Scripts/PlaytestOpening.cs(120,36): error CS0120` — `CinematicDirector.ReelId` is an instance property, but the opening playtest called it as static. Latent since the cinematic-director PR; a clean beta rebuild surfaced it. The transitions feature itself (ScreenTransition, LoadScreen, seven LoadCards, `transitions` playtest) was already on beta.

## What changed
- `Assets/Scripts/PlaytestOpening.cs`: read `CinematicDirector.Instance.ReelId` (null-safe), same pattern as `ShotIndex` two lines later.
- `.hawkes/scenario.txt`: `transitions` (was stale `slice-partner` from an older merge note in HEAD).

No GPU spend. No new art. Conflicts: none.

## Checks
- Grep: only that one bad static `ReelId` call; nested types like `BossResult` are fine.
- `transitions` still registered in `Playtest.cs`; `PlaytestTransitions.cs` present; seven `LoadCards/card_*.png` and `paper_slide` / `stamp_heavy` tracked.
- Animation timing still 0.66 s (0.26+0.08+0.06+0.26).
- Unity compile not run here (no editor). Studio cloud build should clear CS0120.

## Reviewers
- **fun-reviewer:** thin (not demo). Soul 5; story-as-experience 4. Gaps beyond this AC (Settings/Shop hard cuts; card is watch-only; art-miss fallback) left for later — this pass is the build fix.
- **reviewer:** merge. Compile fix is the real cause; scope tight; feature kept.

## Left for later (not this change)
Settings / Shop / pause / level-up still hard-cut; optional card interaction while the strip fills; richer art-miss fallback than the night plate.
