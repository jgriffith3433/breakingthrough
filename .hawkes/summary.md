# Content: Interactive ADDRESS FILED finale carry

## What was wrong
Chapter 6’s payoff after the Order-Bearer was a watch-only `ending_filed_world` reel (stamp came down by itself). The story’s beat — Slice’s address was struck before Wallit; she needs him — was not something the player did with their hands.

## What changed
- **Story / bible:** Docs/Story.md, Docs/CampaignBible.md, docs/world-bible.md — Filed is a 20–40 s playable carry on the live Hollow; RECEIVED after ADDRESS FILED unchanged; Slice’s Moment ends on this beat.
- **CinematicDirector:** `Shot.finaleCarry` + metrics (`FinaleCarry*`). Esc/Enter during the carry completes the stamp instead of dumping the whole ending.
- **`CinematicFinale.cs` (new):** hold swing to walk the stamp to Slice’s ring; 3 sealed Order notices with timing taps (Space / icon); miss knocks Wallit back; on release ADDRESS prints, ≥4 lamps light one by one, Door Number set piece triggers, Slice “Clean. Good swing, Wallit.” (and mid-carry “Mine.”), ch6 → ending score. Phone: large lower-third hold pad (≥88 CSS px), timing as icon not words, other HUD hidden.
- **`ending_filed_world.json`:** `s5_stamp` → `s5_carry` with `finaleCarry`.
- **Playtests:** `finale-carry` (named in `.hawkes/scenario.txt`, mobile); `opening-endings` / `chapter6` drive the hold so they don’t stall.

## How checked
- Acceptance criteria covered by `finale-carry` events/metrics: `finale-hold`, `finale-bat`, `finale-miss`, `finale-stamp`, `finale-lamps`, `finale-hold-prompt`, `received-intact`.
- Fun reviewer: **thin** → fixed Slice mid-beat, Door Number sync, sealed-notice flavor.
- Studio reviewer: **revise** → fixed phone drive (`PadHold` not BotFinger), honest bat counts (no failsafe inventing bats), deliberate miss, hold prompt CSS px via `UiFit.PixelRatio`.
- Unity compile not run here (no editor on this box). No conflicts with c_53abaafb2d50 (boss voices).

## For J
Play Chapter 6 to the Order-Bearer (or beta `?playtest=finale-carry&mobile=1`): hold swing, bat three notices, release — her address prints and the lamps light before ADDRESS FILED.