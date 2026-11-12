# UI art

Every generated piece of UI art, with its file. All files are optional `Assets/Resources/UiArt/<name>.png`, loaded by `UiArt.cs`;
each caller keeps its code-drawn version as the fallback, so the game is complete with none of them. Made through Hawkes Gen
(`image.generate`, project `org_eb0d5c82e8da8c2d.theplacedepartment`), matched to `Resources/UI/MenuArt.png`: moonlit midnight teal and navy,
warm brass-gold accents, soft glow, painted low-poly feel, no text, no sparks or glitter (J's art rules). Ids are lower-cased enum names
(`BoonId`, `UpgradeId`), never renamed, so a file never drifts from the id analytics reports.

## Budget and phones

- Icons are generated at 512 px and stored at **128 x 128** (shown at 96 on a level-up card, 52 in a shop row): 42 icons are about 0.9 MB of
  texture memory at 128 px RGBA (no mipmaps), under 3 MB on disk with PNG; the frame is 256 x 256 and the logo 1024 x 384 (`TextureSizeRules` caps WebGL at 1024).
- Icons are square on a flat very dark navy ground that sits on the dark card face (no alpha needed).
- Phone layouts (`UiFit` Wide and Tall): level-up cards have no room for an icon, so they show none (`LevelUpScreen`, Desktop only);
  shop rows (`MainMenu`) give the icon 64 units and shrink the text by that, so the 44 px tap targets (Buy, tabs) and the no-overlap checks are unchanged.
  The panel frame's border is about 16 units, never over a tap target.

## Frames and logo

| Piece | File | Size | Used by |
|---|---|---|---|
| Title logo "BREAKING THROUGH" in gold leaf | `UiArt/logo.png` | 1024 x 384 | `MainMenu.BuildHeader` replaces the gold text (the text stays hidden as fallback) |
| Panel frame, grey metal (tinted by the caller), flat dark middle, 41 px nine-slice border | `UiArt/panel_frame.png` | 256 x 256 | `UiSkin.Framed` (falls back to `UiSkin.Round`, the code-drawn pill from `UiSkin.Make`): main-menu case file, pause panel, level-up cards |

Not replaced on purpose: bars (`UiSkin.Bar`/`Disc`), buttons and halos stay code-drawn, because they scale to any size and tint freely.

## Boon icons (`BoonId`, in `RunBoons.All`)

Shown on the level-up cards (Desktop). Not yet on the HUD boon list (text).

| Boon | File |
|---|---|
| Corked Bat | `UiArt/boon_heavy.png` |
| Quick Hands | `UiArt/boon_quickdraw.png` |
| Wide Arc | `UiArt/boon_splitshot.png` |
| Follow Through | `UiArt/boon_pierce.png` |
| Sweet Spot | `UiArt/boon_eagleeye.png` |
| Line Drive | `UiArt/boon_ricochet.png` |
| Pine Tar | `UiArt/boon_firebrand.png` |
| Ice Bat | `UiArt/boon_frost.png` |
| Live Wire | `UiArt/boon_chain.png` |
| Powder Keg | `UiArt/boon_birdbomb.png` |
| Whirling Blades | `UiArt/boon_orbit.png` |
| Encore | `UiArt/boon_encore.png` |
| Standing Ovation | `UiArt/boon_ovation.png` |
| Lodestone | `UiArt/boon_magnet.png` |
| Fleet Foot | `UiArt/boon_fleet.png` |
| Bounty Hunter | `UiArt/boon_bounty.png` |
| Called Shot | `UiArt/boon_mark.png` |
| Grand Slam | `UiArt/boon_volley.png` |
| Executioner | `UiArt/boon_executioner.png` |
| Long Ball | `UiArt/boon_homing.png` |
| Tower Song | `UiArt/boon_turret.png` |
| Second Wind | `UiArt/boon_secondwind.png` |
| Thunder Bat (evolution) | `UiArt/boon_stormquiver.png` |
| Phoenix Bat (evolution) | `UiArt/boon_phoenix.png` |
| Grand Finale (evolution) | `UiArt/boon_grandfinale.png` |
| Blizzard Bat (evolution) | `UiArt/boon_hailstorm.png` |

## Shop icons (`UpgradeId`)

Shown on the main menu's upgrade forms. Retired slots 2, 3, 5, 7 and 8 have none.

| Upgrade | File |
|---|---|
| Corked Core | `UiArt/upgrade_keen.png` |
| Stone Pile | `UiArt/upgrade_walls.png` |
| Aluminum! Not wood! | `UiArt/upgrade_blast.png` |
| Long Stride | `UiArt/upgrade_stride.png` |
| Watch Kit | `UiArt/upgrade_towers.png` |
| Four-Leaf Clover | `UiArt/upgrade_clover.png` |
| Opening Act | `UiArt/upgrade_openingact.png` |
| Stage Hands | `UiArt/upgrade_stagehands.png` |

## Ability icons (`CampaignProgress.Abilities`)

Drawn in the campaign HUD as a row of 44 unit icons under the XP line (`ChapterRun`, Desktop only, dimmed while a slot waits or is locked; phones keep the text line and the touch pads).

| Ability | File |
|---|---|
| Spare Change | `UiArt/ability_spare_change.png` |
| Refund | `UiArt/ability_refund.png` |
| Receipt | `UiArt/ability_receipt.png` |
| Chargeback | `UiArt/ability_chargeback.png` |
| Coin Toss | `UiArt/ability_coin_toss.png` |
| Stamp Out | `UiArt/ability_stamp_out.png` |
| Escrow | `UiArt/ability_escrow.png` |

## What is left

- Ability icons in the hub's level-up list and on phones.
- Boon icons in the in-run HUD list and on phone cards (a 56 unit icon needs the Wide layout re-measured).
- Panel frame for the settings panel, chapter cards and the hub board (they use `UiSkin.Round` directly).
- `polish` logs `ui-art` per screen: which of these pieces are drawing.

## The paper HUD (HudPaper)

`Assets/Scripts/HudPaper.cs` draws Arcade's run HUD from one atlas, `Resources/UiArt/hud_atlas.png` (painted Department style via
Hawkes Gen, cut out and packed 1024x1024; the pixel rects are constants in `HudPaper`). Slice's address card (four stages, tears at 75/50/25%
health, flutters on a hit, tape patch on a heal), the coin ledger (odometer digits, `coin` twice on a gain), the wave postmark
(one per act, re-stamped each wave, red seal on boss waves) and the level ink stamp. With no atlas `HudPaper.Active` is false and the old
text HUD (RunHud, Musician bar, WaveDirector coin/wave lines) shows. `hud-paper` playtest (`PlaytestHudPaper.cs`).
