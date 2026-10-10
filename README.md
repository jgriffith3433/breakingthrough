# Breaking Through

A night-knoll bow game in Unity 6.3 LTS (`6000.3.7f1`), built-in render pipeline. You protect a pirate playing piano. Birds come in waves. You grow, your arrows hit harder, and every tenth wave brings a new bird that is about your size.

The project folder name is `breakingthrough`. The Unity product name is still `Controller`.

## How a run works

A build starts at `Assets/MainMenu.unity`. Pressing Play on the open `scene2` scene skips the menu until you die and press **M**.

Waves count up from 1. Wave N spawns N turkeys, plus every species you have already met. A species that first appears at wave U spawns `N - (U - 1)` of itself once that number is positive. Wave 10 is 10 turkeys and 1 hen. Wave 11 is 11 turkeys and 2 hens. After wave 100 the same birds repeat. They only get larger.

You lose when the musician's health reaches 0. There is no clock. The musician starts at 100. Each peck is 1. The bar flashes once when they are hit. Turkeys do not spawn inside the fort ring around the piano.

Between waves there is a 5 second break. Before waves 5, 15, 25, and so on, there is a 30 second build window.

## Controls

The mouse orbits the camera. The cursor locks and hides during a wave, and unlocks while you are building.

| Input | What it does |
|---|---|
| **W A S D** | Move relative to the camera |
| **Mouse** | Orbit |
| **Hold left click** | Shoot. One shot per draw |
| **Space** | Jump. One second between jumps |
| **Space before level 20** | You must release it before the next jump |
| **Space from level 20** | Hold it to keep hopping. Each hop plays the jump |
| **F** | Shake climbers off, from level 50 |
| **Tab** | During a build, cycle Wall, Spikes, Tower |
| **Q / E** | During a build, turn the piece 45 degrees |
| **Click** | During a build, place the selected piece between the rings |
| **= or numpad +** | Testing: next wave |
| **- or numpad -** | Testing: previous wave, not below 1 |
| **R** | After a loss, reload the hill |
| **M** | After a loss, open the shop |

Aim prefers the largest bird within 36 meters. You can shoot while jumping. The bow stays on the upper body while the legs jump.

Tips at the bottom of the screen show the new rule for that wave, then later waves replace them. That replacement is intentional.

## Growth and arrows

Size is `1 + progress * growth per wave`. Progress is `(wave - 1)` plus a little for each kill this wave. Base growth is `0.11` per wave. Big Appetite adds `0.02` per rank.

Arrow damage is `floor(1 + (wave - 1) * 0.5)` plus Keen Arrows, plus a tiny bonus from kills this wave. Turkeys have 1 health, so they always die to one arrow.

| Wave | Damage, before upgrades |
|---|---|
| 1 | 1 |
| 5 | 3 |
| 10 | 5 |
| 19 | 10 |
| 20 | 10 |
| 30 | 15 |
| 40 | 20 |
| 70 | 35 |
| 100 | 50 |

From level 70, an arrow explodes when it hits a bird. Heavy Arrows pulls that earlier (6 waves per rank, not before wave 45) and widens the splash. Splash hits use the same arrow damage.

## Birds

Authored size is multiplied again at spawn by `1.18 + (wave - 1) * 0.012`. After wave 100 there is another `2.5%` per wave. Health below is the authored value, before that size bump. Arrows listed are at the debut wave with no Keen Arrows.

| Wave | Bird | Health | Arrows at debut |
|---|---|---|---|
| 1 | Turkey | 1 | 1 |
| 10 | Hen | 20 | 4 |
| 20 | Rooster | 90 | 9 |
| 30 | Goose | 160 | 11 |
| 40 | Swan | 220 | 11 |
| 50 | Emu | 280 | 12 |
| 60 | Ostrich | 330 | 11 |
| 70 | Cassowary | 420 | 12 |
| 80 | Terror | 500 | 13 |
| 90 | Roc | 600 | 13 |
| 100 | Thunderbird | 900 | 18 |

The goose and everything after it are about twice the size of the earlier plan. The Thunderbird is larger than you. Decade birds are meant to feel like bosses: about your height, and they take a lot of arrows. Hens get easier as your damage climbs: 3 arrows by level 13, 2 by level 19.

Small turkeys spawn 26–36 meters out, with a wide random angle and at least 7 meters between them. Larger birds spawn 17–24 meters out.

## Jumping, stomping, climbing

From level 20, landing a jump hurts birds that first appeared at least 20 levels earlier. Turkeys count as level 0, so level 20 hurts them, level 30 also hurts hens, level 40 also hurts roosters, and level 50 also hurts geese. Newer birds are left alone.

From level 40, if a small turkey is at your feet and you are on the ground, you stomp in place. The clip is `Assets/Art/Stomp.fbx` and it loops. Jumping still works. This does not move the jump-landing rule.

From level 50, small turkeys fly at you and climb. Jumping and stomping stop until you press **F**. The clip is `Assets/Art/Shaking It Off.fbx`.

## Building

During a build window the mouse is free. Buy pieces with coins, then click in the donut around the piano. The inner red ring is 6.5 meters (too close). The outer gold ring is 12.5 meters. **Q** and **E** rotate. **Tab** switches the piece. The tip keeps **Q** and **E** on the first line for the whole build.

| Piece | Start count | Price | What it does |
|---|---|---|---|
| Wall | 6 | 1 coin | Blocks. Stops 8 turkeys, then breaks |
| Spikes | 3 | 2 coins | Does not block. Kills 4 turkeys, then breaks |
| Tower | 1 | 3 coins | Blocks, shoots nearby birds, kills 6, and falls after 8 reach it |

Stone Pile, Spike Kit, and Watch Kit raise the starting counts and how long each piece lasts. Pieces darken as they wear out, then sink and disappear.

## Shop

Coins and upgrades are stored in PlayerPrefs (`bt_coins`, `bt_best`, `bt_hens`, `bt_next`, `bt_ranks`) and survive death and restart. Only hen kills pay. The next coin is a random count between 10 and 20 hens. Fat Purse tightens that range and, from rank 3, pays 2 coins.

Cost of the next rank is the current rank plus 1.

| Upgrade | Effect |
|---|---|
| Keen Arrows | +1 arrow damage per rank, max 6 |
| Stone Pile | +2 starting walls and +2 stops per wall, max rank 5 |
| Quick Draw | Auto-shoot starts 3 waves before 30 per rank, and automatic shots come out faster. Max rank 4, so the earliest auto wave is 18 |
| Big Appetite | Grow faster. Max rank 5 |
| Heavy Arrows | Explosions start sooner than wave 70, and the splash is wider. Max rank 4 |
| Fat Purse | Coins from fewer hens. Max rank 4 |
| Long Stride | +8% move speed per rank, max 5 |
| Spike Kit | +1 starting trap and +1 kill per trap, max rank 5 |
| Watch Kit | +1 starting tower, plus more kills and more stops, max rank 4 |

Iron Boots was removed. Its save slot is still number 7 so older ranks do not shift. Coins already spent on it are not refunded.

## Scenes and the hill

| Scene | Role |
|---|---|
| `Assets/MainMenu.unity` | Shop. Build index 0 |
| `Assets/scene2.unity` | The hill. Build index 1 |

The player is the Warrok model (`Assets/Art/Warrok W Kurniawan.fbx`) on the object tagged `Player`. The visible child is tagged `playerchild`. The animator is `Assets/hero/Hero.controller`. The bow is parented to the left hand. Its local pose is position `(-0.009, -0.066, 0.035)`, rotation `(13, 350, 358)`, scale `0.353789777`. Do not change that pose.

The knoll is the Ground layer and is not flat. Birds stand on Ground. The hill extends about 38 meters from the center. `KnollLit` is double sided. Other materials on that shader stay single sided.

Layers: Default, TransparentFX, Ignore Raycast, Ground, Water, UI, Enemy.

Sounds live in `Assets/Resources/Sfx/`: `shot`, `hit`, `death`, `stomp`, `launch`.

## Animations

`Assets/Editor/SetupWarrok.cs` imports Mixamo clips onto the Warrok avatar and writes `Hero.controller`. Base layer `State`:

| State | Clip |
|---|---|
| 0 | Aim idle |
| 1 | Walk |
| 2 | Full-body shoot, only if the upper-body layer is missing |
| 3 | Jump |
| 4 | Aim walk |
| 5 | Stomp |
| 7 | Shake |

Shooting uses a second layer, Upper Body, masked by `Assets/hero/UpperBody.mask`, driven by the `Fire` trigger. The base layer must stay off State 2 while that layer exists, or the jump is cancelled.

`Assets/hero/ANIM_HERO_UNARMED_KICK_*` are generic Bip01 clips. They do not play on Warrok. New body clips need to be Mixamo humanoid FBX files dropped in `Assets/Art/`, then named in `SetupWarrok`.
