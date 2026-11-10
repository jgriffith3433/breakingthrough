# Camera feel, attempt 4: no new code, the last playtest build failed on infrastructure

## Wrong
The latest Studio PM note says the playtest build failed because the build server's in-memory disk was full (2.47 GB of 4 GB, 50 MB short). That is not a code failure. The branch has not been judged on its camera work since the attempt-3 fixes.

## State of the branch (unchanged this pass)
- `.hawkes/scenario.txt` = `explore` (Arcade, chapters 1-6 and the hub). It logs `feel.cameraInsideFrames/Near/Snap/Jitter.<map>` plus the corner, idle-recentre and lock probes (`CameraFeel.cs`, `PlaytestExplore.cs`).
- `ZeldaStyleCamera.cs`:
  - sphere-cast pull-in with ease-out;
  - FOV 60-75;
  - recentre behind Wallit after 2 s idle;
  - lock-on and release eased;
  - yaw clamped to 200 deg/s with dt capped at 0.05 s;
  - rendered view capped at 300 deg/s (`LimitViewTurn`), so a 30-degree one-frame snap is impossible outside a cut.
- `CamBlock.cs`: camera-blocking colliders on kit walls and buildings.

## Checked
Unity compile and explore not run here (no editor in this sandbox). The build server compiles and beta runs explore. No code changed this pass, so there are no new compile risks. Conflicts: none.

## Look at
After merge: `feel.cameraInsideFrames.<map>` (must be 0), `feel.cameraSnapFrames.<map>`, `feel.lockMaxFrameDeg.<map>`, `feel.idleRecentre.<map>`. If the build fails with a full disk again, rerun it after the server is cleared; it is not this change.
No GPU generation used; GPU estimate: none needed.
