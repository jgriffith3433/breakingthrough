# Breaking Through

A night-knoll bow game in Unity 6.3 LTS (`6000.3.7f1`), built-in render pipeline. You protect a pirate playing piano. Birds come in waves. You grow, your arrows hit harder, and every tenth wave brings a new bird that is about your size.

The project folder name is `breakingthrough`. The Unity product name is still `Controller`.

## How a run works

A build starts at `Assets/MainMenu.unity`. Pressing Play on the open `scene2` scene skips the menu until you lose. Any key on the loss screen returns to the shop.

Waves count up from 1. Turkeys are one per wave until wave 20, then they pull ahead. From wave 50 they swarm. Every size you have met gets at least one bird on screen. Extra spots go to the smaller birds, so turkeys still make the swarm. Wave 10 is 10 turkeys and 1 hen. Wave 30 is 50 turkeys. Wave 50 is 110. Wave 100 is 560. After wave 100 the same birds repeat. They only get larger.

The right side lists every bird in the current wave. Each row shows how many of that bird are left, and a bar that fills as they fall.

You lose when the musician's health reaches 0. There is no clock. The musician starts at 100. Each peck is 1. The bar flashes once when they are hit. Turkeys do not spawn inside the fort ring around the piano.

Between waves there is a 5 second break. Before waves 5, 15, 25, and so on, there is a 30 second build window.

## Controls

The mouse orbits the camera. The cursor locks and hides during a wave, and unlocks while you are building.

| Input | What it does |
|---|---|
| **W A S D** | Move relative to the camera |
| **Mouse** | Orbit |
| **Hold left click** | Shoot. One shot per draw |
| **Hold Shift** | Lock the largest bird in range. Release it to aim where you face |
| **Space** | Jump. One second between jumps |
| **Space before level 20** | You must release it before the next jump |
| **Space from level 20** | Hold it to keep hopping. Each hop plays the jump |
| **F** | Shake climbers off, from level 50 |
| **Tab** | During a build, cycle Wall, Spikes, Tower |
| **Q / E** | During a build, turn the piece 45 degrees |
| **Click** | During a build, place the selected piece between the rings |
| **= or numpad +** | Testing: next wave |
| **- or numpad -** | Testing: previous wave, not below 1 |

Hold Shift to lock the largest bird within 36 meters, plus 1.25 meters for each wave after the first, up to 170 meters. Releasing Shift sends the shot to the crosshair, so you can aim at a bird or the ground yourself. You can shoot while jumping. The bow stays on the upper body while the legs jump.

Tips at the bottom of the screen show the new rule for that wave, then later waves replace them. That replacement is intentional.

## Growth and arrows

Size is `1 + progress * growth per wave`. Progress is `(wave - 1)` plus a little for each kill this wave. Growth is `0.11` per wave.

Arrow damage is `floor(1 + (wave - 1) * 0.5)` plus Keen Arrows (+6 per rank), plus a tiny bonus from kills this wave. Turkeys have 3 health: three arrows on wave 1, and one arrow from wave 5.

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

Rockets! Not arrows! keeps the bow and replaces the shot with a rocket. A rocket explodes on a bird or on the ground, then the model is gone. The first rank explodes from wave 1, about 4.2 meters. Rank 2 is about 7.4 meters, and rank 3 is about 10.6. Splash hits use the same shot damage. Reaching wave 70 does not turn rockets on.

## Birds

Authored size is multiplied again at spawn by `1.18 + (wave - 1) * 0.012`. After wave 100 there is another `2.5%` per wave. Health below is the authored value, before that size bump. Arrows listed are at the debut wave with no Keen Arrows.

| Wave | Bird | Health | Arrows at debut |
|---|---|---|---|
| 1 | Turkey | 3 | 3 |
| 10 | Hen | 60 | 12 |
| 20 | Rooster | 270 | 27 |
| 30 | Goose | 480 | 32 |
| 40 | Swan | 660 | 33 |
| 50 | Emu | 840 | 34 |
| 60 | Ostrich | 990 | 33 |
| 70 | Cassowary | 1260 | 36 |
| 80 | Terror | 1500 | 38 |
| 90 | Roc | 1800 | 40 |
| 100 | Thunderbird | 2700 | 54 |

The goose and everything after it are about twice the size of the earlier plan. The Thunderbird is larger than you. Decade birds are meant to feel like bosses: about your height, and they take a lot of arrows. Health is three times the earlier numbers, so a run with no shop upgrades is meant to fall apart before wave 10.

Birds arrive one at a time, mixed together, after a short beat at the start of the wave. Turkeys are about a third of a second apart. Larger birds leave a longer gap, under a second. More of them stay on screen at once: about 16 early, and up to 120 from the late waves. A long queue no longer dumps them faster, and the biggest bird is not saved for the front of the line.

Turkeys start in a ring about 30–48 meters out. Larger birds start near 24 meters, plus a little for their size. Each later wave pushes that ring out by about half a meter and widens it, out to 150 meters. Each spawn picks the most open spot in that ring, so a flock does not stack on one point.

## Jumping, stomping, climbing

From level 20, landing a jump hurts birds that first appeared at least 20 levels earlier. Turkeys count as level 0, so level 20 hurts them, level 30 also hurts hens, level 40 also hurts roosters, and level 50 also hurts geese. Newer birds are left alone.

From level 40, if a small turkey is at your feet and you are on the ground, you stomp in place. The clip is `Assets/Art/Stomp.fbx` and it loops. Jumping still works. This does not move the jump-landing rule.

From level 50, any bird that first appeared at least 5 waves ago leaves the musician alone and climbs you instead. The newest birds still go for the piano. Jumping and stomping stop until you press **F**. The clip is `Assets/Art/Shaking It Off.fbx`.

Each of those stays listed on the left once the wave that grants it is reached: jump land at 20, auto shoot at 30, stomp at 40, shake off at 50. The newest line stays gold for a few seconds. Skipping backward removes lines for powers that wave no longer has.

## Building

During a build window the mouse is free. Buy pieces with coins, then click in the donut around the piano. The inner red ring is 6.5 meters (too close). The outer gold ring is 12.5 meters. **Q** and **E** rotate. **Tab** switches the piece. The tip keeps **Q** and **E** on the first line for the whole build.

| Piece | Start count | Price | What it does |
|---|---|---|---|
| Wall | 6 | 1 coin | Blocks. Stops 8 turkeys, then breaks |
| Spikes | 3 | 2 coins | Does not block. Kills 4 turkeys, then breaks |
| Tower | 1 | 3 coins | Blocks, shoots nearby birds, kills 6, and falls after 8 reach it |

Stone Pile and Watch Kit raise those starting counts. Spikes stay at the table. Pieces darken as they wear out, then sink and disappear.

## Shop

Coins and upgrades are stored in PlayerPrefs (`bt_coins`, `bt_best`, `bt_hens`, `bt_next`, `bt_ranks`) and survive death and restart. Only hen kills pay. The next coin is a random count between 10 and 20 hens.

There are five upgrades, and each one stops at rank 3. The next rank costs 1, then 2, then 3. Buying one flashes the row, names what you gained, and the hill shows it on the next run. The first wave of a run lists everything you brought.

Once you have reached wave 10, the menu can start the next run later. Each step of 10 waves costs 10 coins, and only waves you have already reached are for sale. Wave 10 costs 10 coins. Wave 100 costs 100. The coins are spent when you press Play. The run after that starts at wave 1 again unless you buy another start.

| Upgrade | Effect |
|---|---|
| Keen Arrows | +6 arrow damage per rank. Rank 3 is +18 |
| Rockets! Not arrows! | The bow shoots rockets from wave 1. Each rank widens the blast, up to about 10.6 meters |
| Long Stride | +25% move speed per rank. Rank 3 is +75% |
| Stone Pile | +5 starting walls and +8 stops per wall. Rank 3 starts 21 walls that each stop 32 |
| Watch Kit | +2 starting towers per rank. They shoot farther and faster, and last longer |

Quick Draw, Big Appetite, Fat Purse, Spike Kit, and Iron Boots were removed. Their save slots stay unused so older ranks do not shift. Coins already spent on them are not refunded. Ranks saved above 3 fold down to 3. Auto-shoot still begins at wave 30 for everyone. Spikes are still a build piece.

## Scenes and the hill

| Scene | Role |
|---|---|
| `Assets/MainMenu.unity` | Shop. Build index 0 |
| `Assets/scene2.unity` | The hill. Build index 1 |

The player is the Warrok model (`Assets/Art/Warrok W Kurniawan.fbx`) on the object tagged `Player`. The visible child is tagged `playerchild`. The animator is `Assets/hero/Hero.controller`. The bow is parented to the left hand. Its local pose is position `(-0.009, -0.066, 0.035)`, rotation `(13, 350, 358)`, scale `0.353789777`. Do not change that pose.

The knoll is the Ground layer and is not flat. Birds stand on Ground. The hill extends about 170 meters from the center. `KnollLit` is double sided. Other materials on that shader stay single sided.

Layers: Default, TransparentFX, Ignore Raycast, Ground, Water, UI, Enemy.

Sounds live in `Assets/Resources/Sfx/`: `shot` (bow), `hit` (arrow), `death`, `stomp`, `launch` (explosion puff), `coin`, `shake`.

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
