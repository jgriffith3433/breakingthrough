# Content: Interactive ADDRESS FILED finale carry

## What was wrong
Chapter 6’s payoff after the Order-Bearer was a watch-only `ending_filed_world` reel (stamp came down by itself). The story’s beat — Slice’s address was struck before Wallit; she needs him — was not something the player did with their hands.

## What changed
- **Story / bible:** Docs/Story.md, Docs/CampaignBible.md, docs/world-bible.md — Filed is a 20–40 s playable carry on the live Hollow; RECEIVED after ADDRESS FILED unchanged; Slice’s Moment ends on this beat.
- **CinematicDirector:** `Shot.finaleCarry` + metrics (`FinaleCarry*`). Esc/Enter during the carry completes the stamp instead of dumping the whole ending.
- **`CinematicFinale.cs` (new):** hold swing to walk the stamp to Slice’s ring; 3 sealed Order notices with timing taps (Space / icon); miss knocks Wallit back; on release ADDRESS prints, ≥4 lamps light one by one, Door Number set piece triggers, Slice “Clean. Good swing, Wallit.” (and mid-carry “Mine.”), ch6 → ending score. Phone: large lower-third hold pad (≥88 CSS px), timing as icon not words, other HUD hidden.
- **`ending_filed_world.json`:** `s5_stamp` → `s5_carry` with `finaleCarry`.
- **Playtests:** `finale-carry` (named in `.hawkes/scenario.txt`, mobile); `opening-endings` / `chapter6` drive the hold so they don’t stall.

## Merge with `origin/beta` (#259 Bosses with real voices) — conflicts and resolutions

Merged `origin/beta` (`093544f`) into this branch and kept both sides’ work intact:

| File | Resolution |
|---|---|
| `Assets/Scripts/Playtest.cs` | **Auto-merged clean.** Kept both `boss-barks` (`BossBarksRun`) from beta and `finale-carry` (`PlaytestFinaleCarry.Run`) plus the `chapter6` / `opening-endings` carry hold driver from this branch. Verified brace balance and no duplicate definitions. |
| `Docs/Story.md` | **Auto-merged clean.** Kept beta’s `### Boss barks` section and this branch’s interactive *Filed* (`finaleCarry`) beat in Chapter 6 and Endings. |
| `docs/world-bible.md` | **Auto-merged clean.** Kept beta’s six campaign boss entries in `## Characters` (Courier, Plated Alpha, Foreman, Server, Registrar, Order-Bearer with `eleven_v4` voice ids and `BossVoice` moments) and this branch’s Slice Moment update and Story beat 8 (`finaleCarry`). |
| `.hawkes/scenario.txt` | **Conflict resolved.** Kept `finale-carry` + `{"mobile":true}` as the active scenario for this change; `boss-barks` remains registered in `Playtest.cs`. |
| `.hawkes/learned.md` | **Conflict resolved.** Combined both branches’ canon facts (`ADDRESS FILED finale carry` and `Boss in-fight barks`), conventions (`CinematicDirector interactive shots`, `Boss voices`), and phone playtest lessons within the 5-line budget. |
| `.hawkes/try.md` | **Conflict resolved.** Kept the `finale-carry` steps (1–4) and added step 5 covering beta’s `boss-barks` check. |
| `.hawkes/summary.md` | **Conflict resolved.** Preserved this branch’s summary and documented the merge resolutions for all seven files here. |

## How checked
- Acceptance criteria covered by `finale-carry` events/metrics: `finale-hold`, `finale-bat`, `finale-miss`, `finale-stamp`, `finale-lamps`, `finale-hold-prompt`, `received-intact`.
- Verified no conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`) across the repository.
- Validated all JSON files (`ending_filed_world.json`, `cast.json`, `audio.json`, `Tools/Audio/batches/boss_barks.json`) and checked C# syntax/brace balance and world-bible sections.
- Fun reviewer: **thin** → fixed Slice mid-beat, Door Number sync, sealed-notice flavor.
- Studio reviewer: **revise** → fixed phone drive (`PadHold` not BotFinger), honest bat counts (no failsafe inventing bats), deliberate miss, hold prompt CSS px via `UiFit.PixelRatio`.
- Unity compile not run here (no editor on this box). No overlap with `c_3345ac6e4f91` (PBR ground/kit materials).

## For J
Play Chapter 6 to the Order-Bearer (or beta `?playtest=finale-carry&mobile=1`): hold swing, bat three notices, release — her address prints and the lamps light before ADDRESS FILED.
