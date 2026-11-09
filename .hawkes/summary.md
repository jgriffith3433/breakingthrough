# Chapter 1 playtest bots: finish the fight (attempt 2)

**Not run: there is no Unity editor in this sandbox, so campaign-beats and campaign-touch were not played here.** The fix is the commit e1bc427, already on this branch. Beta has not moved past a55847d, so there was nothing to re-merge and no conflicts. I made no code change this pass, as the PM said. I re-read the diff and checked that the members it calls exist (`MovementMotor.Stop`, `Enemy.Dormant`, `TouchControls.Locked`, `PlayInput.Tap`, `LevelYard.H`, `PlayerGrowth.Rooted`, `CharacterMovement.ZeldaCam`, `PlaytestTouch.TapPad`/`Rest`). Unity compile not run here; the build server will do it. No GPU, nothing generated.

I never saw the raw event logs of s_27bc35ad5310 and s_f682562a701b, so the stall causes below are readings of the code, not confirmed.

## Can a real player move after LevelScene.Resume?
By the code, yes. Nothing in a level sets `PlayerGrowth.Rooted`, and bats and climbers don't block walking. So I found no game bug that freezes Wallit. I still hardened `Resume`: it seats him on the drawn ground (`LevelYard.H`), calls `MovementMotor.Stop()` and drops the old lock-on.

## Changes (4 files)
- `LevelScene.cs`: `Resume` as above.
- `ZeldaStyleCamera.AcquireTarget`: sleeping (`Dormant`) bots can't be locked. This is the likely cause of touch's "Locked" with no crew in view. Lock used to latch onto a sleeper and the bot's lock-on steering then held still.
- `PlaytestBeats.cs`: the walk back to the sleeping Courier now goes all the way in to 3 m. It was stopping at 6 m, and the Courier only wakes inside 14 m of a different measure. It logs `walk-back-stalled` with the position, axes, controller, rooted, lock, timeScale, pause and scene state if Wallit hasn't moved for 3 s, then taps Space. If beats stalls again, that event names the cause.
- `PlaytestTouch.cs`: when nothing is awake and Lock is held, the finger taps Lock again (`touch-unlock`). It then walks to the nearest sleeping bot, so the crew wakes by proximity, and fights it with the lock-on stick axes.

All existing events and metrics are kept.

## What to look for when the build plays them
- campaign-beats: `checkpoint-resume`, then `boss-wake`. `walk-back-stalled` should be absent.
- campaign-touch: `touch-unlock`, `touch-lock`, then the `touch-fight` cleared event.

## Attempt 3: rebuild as is
No code changed this pass. The fix is still e1bc427 (`LevelScene.cs`, `PlaytestBeats.cs`, `PlaytestTouch.cs`, `ZeldaStyleCamera.cs`). This commit only touches the summary so Studio rebuilds the branch and runs campaign-beats and campaign-touch. Unity is not available here, so neither scenario was played and the compile was not run. No GPU, nothing generated. To read on the build: `checkpoint-resume` and `boss-wake` in beats (`walk-back-stalled` absent), `touch-unlock`, `touch-lock` and `touch-fight` cleared in touch. Only a compile error in the four files above is this change's to fix; a Gen 5xx, timeout or restart is infrastructure.


---

# Also on beta (merged in): Gen models for bosses, lodgers and landmark props (#184)

## Summary (other change)

Nothing new was generated this pass: all the art from the earlier passes is already committed on this branch. The last "failed" verdict was Studio's own "no build settings (products.json build)" error, which the PM said is a Studio config problem and not this change. Nothing is pending, and `.hawkes/pending-gpu.txt` does not exist.

Unity compile was not run here. CharacterValidation, KitValidation and the 150k/64 MB budgets were not run either (no Unity in this sandbox). They are judged by beta's `characters` and `levels-1-3` playtests.

## What is baked (Gen concept image -> mesh.textured -> `Tools/Bake/glb_bake.py` -> `Assets/Resources/Baked/<id>.mesh.txt`)

| Piece | Kind | Notes |
|---|---|---|
| courier, foreman, server, order_bearer | bosses | Rigid split by `Tools/Bake/specs/<id>.json` into Body/Head/ClawL/ClawR/LegL/LegR plus child parts (Cap, Pack, Hard Hat, scroll and so on). Bot part names and pivots are kept, so tells, `Enemy.AnimateModel` and the `_Flash` hit flash work as before. 7,000 to 8,000 triangles each, under the 12,000 boss budget. The code bodies are the fallback (`CharacterKit`). |
| of_lodger_tick, brim, chime, spare | lodgers | Baked. |
| gt_stamp_press_large, gt_turnstile, rd_great_ledger, yd_clock_tower | landmark props | Decimated on the existing kit textures. The stamp press is fitted exactly to 1.6x2.6x1.2 m and the clock tower to 4.6x13.3x4.6 m. |
| of_stamp_press, gt_gate_arch, gt_ticket_booth, of_pneumatic_tube, yd_notice_board | landmark props | Baked in earlier passes. |
| rd_lectern | landmark prop | Baked on this branch, per the PM's note. |

The other ~150 kit pieces stay code-built, so the budgets hold.

## Boss extents (m), Gen body vs code body
- courier: 1.12 x 1.30 x 1.15 vs about 1.2 x 1.3 x 0.9. The Gen pack is a bigger mailbox pack than the code jet pack.
- foreman: 1.26 x 1.45 x 0.59 vs about 1.3 x 1.45 x 0.6.
- server: 1.54 x 1.45 x 1.0 vs about 1.8 x 1.4.
- order_bearer: 1.23 x 1.20 x 1.13 vs about 1.3 x 1.1.

## Checks
The `characters` scenario shows the four bosses and four lodgers. Its events report each character's triangles and idle. `levels-1-3` photographs the props and reports `levelPrimitives` and visible triangles. The boss tells and hit flash are played by chapter1, chapter3, chapter4 and chapter6.

## For J to look at
Whether the Courier's bigger mailbox pack reads right in the gallery. I couldn't judge the Registrar and Plated Alpha art direction here; the gallery covers it.

No merge conflicts.

## This pass
No art made, no code edited, no GPU spent (`.hawkes/pending-gpu.txt` does not exist). The earlier branch `task/ce95a565362b-...` was gone from origin, but its tip `1336f88` was still fetchable by sha; I fast-forwarded this branch to it (10 commits over beta `a55847d`, no conflicts). Studio builds it and runs the `characters` playtest (named in `.hawkes/scenario.txt`). Unity compile and the validators were not run here. If the build fails with "no build settings (products.json build)", that is Studio's config, not this change.

## Attempt 2 (rebuild as is)
The worktree was restored on `task/94c4801cbd0d-using-the-method-proven-on-the-registrar` at `60144c1`. `origin/beta` has not moved (0 commits behind, 11 ahead), so there was nothing to merge and no conflicts. I changed no code, made no art and spent no GPU; `.hawkes/pending-gpu.txt` does not exist. Unity compile and the validators were not run here. Studio builds this branch and runs the `characters` playtest (named in `.hawkes/scenario.txt`). Only a compile error in `BakedMesh.cs`, `CharacterKit.cs`, `Enemy.cs` or `PlaytestCharacters.cs` is this change's to fix; a Gen 5xx, timeout or restart loop is infrastructure.

## Attempt 3
Attempt 3: rebuild as is after Gen 502 on the build output. Merged `origin/beta` (`8ed4f6c`, the Chapter 1 softlock fix: Companion, LevelYard, PlaytestBeats) with no conflicts. No code changed, no art made, no GPU spent; `.hawkes/pending-gpu.txt` does not exist. Unity compile and the validators were not run here. `Tools/Bake/in/*.glb` stays as is.

---

## Merge of origin/beta (2148630, #184) into this change
Conflicted file: `.hawkes/summary.md` only (both changes wrote it). Resolved by keeping both: this change's summary (Chapter 1 playtest bots) first, then #184's summary unchanged under its own heading. `.hawkes/learned.md`, `.hawkes/try.md` and `.hawkes/scenario.txt` merged without a conflict, but beta's `scenario.txt` named `characters` (that change's scenario); this change plays `campaign-beats`, so I set `scenario.txt` to that and left `try.md`/`learned.md` with beta's entries plus the entries for this change below. No code files conflicted: this change touches `LevelScene.cs`, `PlaytestBeats.cs`, `PlaytestTouch.cs`, `ZeldaStyleCamera.cs`; #184 touches `BakedMesh.cs`, `CharacterKit.cs`, `Enemy.cs`, `PlaytestCharacters.cs`, docs and `Tools/Bake`; no overlap. Unity is not available here, so nothing was compiled or played; the checks this brief names (campaign-beats, campaign-touch) run on the build.
