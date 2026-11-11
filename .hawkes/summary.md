# Arcade run-end: want to go again

## What was wrong
Losing an Arcade run showed a static "Address struck off" stats dump and any-key returned to the menu. No counting juice, no next unlock, no cast reaction, no one-tap Retry.

## What changed
- **Story / bible:** 12 Wallit/Slice run-end exchanges with conditions in `Docs/Story.md` and `Story.RunEnds`; world-bible beat 12.
- **Goals ladder:** `Assets/Resources/Data/run_goals.json` (12 rungs) + `RunGoals.cs` (career progress, coin/look unlocks, PlayerPrefs).
- **Run-end screen:** `RunEndScreen.cs` replaces the stats body: wave/coin count with `coin` SFX, `stamp_heavy` on new best, goal stamps, one-line cast subtitle, large Retry + Menu. Wired from `WaveDirector.Lose`.
- **Cast talk:** `RunEndTalk.cs` picks by run facts; never repeats an id within 5 runs.
- **Bat looks:** `UpgradeLook` / `BatMarks` show Night Band, Ink Tip, Pale Wake, Foreman Stripe, Order Seal, Long Ball Gold, Crack Band, Ledger Glow when unlocked.
- **Voices:** Hawkes Gen lines for the new exchanges under `Assets/Resources/Voice/` (Wallit ElevenLabs, Slice open `hv_heroine`); `audio.json` updated. Reused existing `coin` / `stamp` / `stamp_heavy` SFX.
- **Playtest:** scenario `run-end` (`PlaytestRunEnd.cs`, `.hawkes/scenario.txt` with 390x844 mobile). Existing `GameOver()` now presses Menu instead of Space.

## Checks
- Manual C# duplicate-member / quote scan on touched scripts: clean.
- Unity compile not run here (no editor in this sandbox).
- Fun reviewer: **thin** (panel over a frozen Hollow; wants in-world strike-off set piece — left for a later pass; this matches the brief's VS/Hades results-screen first slice).
- Reviewer: **revise** notes addressed (Retry gated until ready, overlap probe skips parent/child, real wave clear for best-stamp, look unlock via reaching wave 10, RunEnd fills the lost panel, missing noted/closer/no_refunds restored from git).

## Conflicts
None with other in-flight agents.

## GPU left for J
Optional later set-piece art (4× `image.generate` stamp/ledger stills + 2× `audio.sfx`): hawkesgen_estimate **$0.24** work / **$2.10** with idle tail on 1 server (~8 min). Not started (brief GPU budget: none). Voices already generated under auto-approve (cents).

## What's left
In-world circle snuff / ledger tally / bat dolly on unlock (fun-reviewer gaps 1–3). Campaign chapter loss still uses the simple chapter body + Menu.
