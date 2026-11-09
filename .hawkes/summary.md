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
