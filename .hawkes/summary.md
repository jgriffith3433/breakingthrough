# Land camera tip 20738af (PR #204 work) on beta

## What
J said merge anyway. I merged `origin/agent/merge-camera-023e16bfbe99` (tip 20738af) into this task branch, which sits on beta 703f56e. No camera code was rewritten or touched by me.
It carries: sphere-cast pull-in, `CamBlock` on kit walls, yaw/view turn-rate caps (`LimitViewTurn`), idle recentre, eased lock-on/release, `CameraFeel` detector, and the `explore` scenario (`.hawkes/scenario.txt` = `explore`).

## Conflicts
Only `.hawkes/{scenario.txt,summary.md,try.md,learned.md}`; resolved with the camera branch's scenario (`explore`) and refreshed notes. No source conflicts (Playtest.cs auto-merged).

## Checked
Unity compile and playtests not run here (no editor). Studio's build compiles; beta's explore run judges criteria 1-4 (Arcade metrics feel.cameraInsideFrames/Near/Snap/Jitter, idle recentre, lock ease).
Ch1 explore stall is out of scope (k_634c3a07019d). No GPU used; estimate: none.
