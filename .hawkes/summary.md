# Art cohesion on the first screen (cast + compile)

## What was wrong
The critic’s first-five frame (t≈2s) showed a grey web card with an H logo and Google — no Wallit, Slice or bots. MenuStage already puts the live cast behind the Unity menu (#229/#238), but paint-zero is the Tap to play overlay, which was text on a flat night gradient. Separately, beta had hit CS0120 on `CinematicDirector.ReelId`; that call site is already fixed on beta (`PlaytestOpening.cs:120` uses `Instance.ReelId`).

## What changed
- **`Tools/Web/hawkes-sound.js`**: Tap to play now shows Wallit, Slice, Yo Bot and Spider Bot as framed case-file portraits on one night Department palette (gilt frames, warm lamp / cold moon veil). Safe-area padding, overflow hidden, Tap target ≥48 CSS px. `data-hawkes-cast` marks the lineup. Six visible words only (aria-hidden portraits).
- **`Tools/Web/cast/*.webp`**: Existing site cast portraits (no GPU). Deployed next to the Game page.
- **`Assets/Editor/WebGlBuild.cs`**: `InstallSoundPrompt` copies `Tools/Web/cast` into `Game/cast` on every FullDeploy.
- **`PlaytestFirstScreen.cs` / `Playtest.cs`**: `first-screen` requires the MenuStage cast (`first-screen-cast`, `firstScreenCastOk`), logs `first-screen-phone`, and fails controls past the left/right edge (`firstScreenOffscreenX` — sideways scroll). Scenario named in `.hawkes/scenario.txt`.

## Checks
- CS0120: no `CinematicDirector.ReelId` static use left; line 120 is instance-safe.
- `node --check Tools/Web/hawkes-sound.js` passed.
- C# compile not run here (no Unity in this sandbox). Read changed C# for duplicate members / signature traps; none found.
- Fun reviewer: **thin** (cast present; tap card still mute/static vs living MenuStage). Gaps are polish (idle voice, Ken Burns), not blockers for art-cohesion presence.
- Reviewer: **revise** notes fixed (Slice `.s` opacity clash removed; portraits framed as one dossier strip so opaque blue plates don’t fight the night palette). Tap-card HTML isn’t asserted from Unity; MenuStage + first-five footage cover the criterion.
- Conflicts: none.

## For J
Open beta → first paint should be Wallit / Slice / bots in gilt frames on night blue, then Tap to play, then the live MenuStage Hollow. GPU estimate for true alpha cutouts / a single MenuArt still: not run (budget was none); ask if you want that pass.