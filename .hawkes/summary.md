# Camera feel, attempt 3: capped turn rates + explore on every map

## Wrong
Review ran smoke, so no `feel.cameraInsideFrames.*` numbers existed; motion sheets showed ~0.2 s view swings. Yaw easing used raw frame time, so a slow frame (map load, hitch) could jump the orbit tens of degrees.

## Changed
- `.hawkes/scenario.txt` = `explore` (Arcade, chapters 1-6, hub: inside/near/snap/jitter per map plus corner, idle-recentre and lock probes).
- `ZeldaStyleCamera.cs`: every eased yaw (idle recentre, lock-on, release) is clamped to 200 deg/s and uses dt capped at 0.05 s; lock-on SmoothDampAngle gets a max speed; a final `LimitViewTurn` caps the rendered view at 300 deg/s (restarts after a >8 m jump of Wallit, i.e. a cut). A 30-degree one-frame snap is now impossible outside cuts.
- Earlier work kept: CamBlock colliders on kit walls, sphere-cast pull-in with ease-out, FOV 60-75, 2 s idle recentre.

## Checked
Read the C# for signature/syntax (SmoothDampAngle 6-arg overload). Unity compile and explore not run here (no editor); the build server compiles, beta runs explore.

## Look at
`feel.cameraInsideFrames.<map>`, `feel.cameraSnapFrames.<map>`, `feel.lockMaxFrameDeg.<map>`, `feel.idleRecentre.<map>`.
No GPU generation used; GPU estimate: none needed. Conflicts: none.
