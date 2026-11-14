# Film light and the exposure rule

FilmLight (Arcade acts), ArcadeActs (the three looks), SceneMood (yard, hub), ChapterMap (chapters) and BTPost (the post pass) all
feed one finishing pass, `NightGrade` -> `Hidden/BT/Post`. Every place shares one exposure rule, held in `NightGrade`, so a Look
can set its colour, contrast and mood but not blow the frame out.

## The rule

- **Exposure**: effective exposure is `min(Look.Exposure, NightGrade.MaxExposure)` (1.04). Looks ask for 1.0 to 1.24; the level is held.
- **Bloom**: intensity is capped (0.5 desktop, 0.3 phone) and only pixels at or above the floor bloom (threshold >= 1.0 in HDR on
  desktop, >= 0.92 on the phone's 8-bit frame). Mid-tones never glow. One pixel feeds bloom at most 4.0 (2.0 phone), so a crowd of
  additive glows or emissive eyes cannot pile into a wash as bots multiply.
- **Tonemap**: a filmic shoulder, identity below 0.6 (0.66 phone) and an exponential roll-off above, so highlights approach white
  and keep detail instead of clipping. It replaces the old `c / (1 + max(c - 0.85, 0) * 0.6)`, which still clipped.
- **Banding**: a triangular dither (0.75/255 desktop, 1.5/255 phone) after the vignette and the stakes drain.
- **Phone**: no HDR (`allowHDR = !UiFit.Touch`), bloom at a quarter resolution with four levels, one pixel light, no shadows (MobileQuality).
- **Safety kept**: `SafeHDR` on every tap, no normalising a zero vector, finale lamps stay off until lit.

A new Look sets its own numbers freely; do not add per-scene brightness hacks, change the rule in `NightGrade`.

## Act weights

`ArcadeActs` owns the 1.5 s blend and the three weights FilmLight reads (rim, lamps, pools, fog, LUT). They used to step only inside
the music tick, which returns early without the score, so a silent run left the old act's film layer stacked on the new look. They now step
in `Update` every frame.

## Measuring

`exposure` playtest (`PlaytestExposure.cs`; run with `mobile` for 390x844): waves 1, 5, 10, 15, 20, 25, 30, event `exposure` with mean
luma, pure-white and near-black share, and the effective exposure, bloom and threshold. Metrics `exposureLuma_<wave>`,
`exposureWhitePct_<wave>`, `exposureDrift`. `Playtest.ExposureSample(label, ...)` logs the same for a hub, chapter or finale frame.
