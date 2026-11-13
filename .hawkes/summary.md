Beta's build failure (r_33654e4420e4) was "Hawkes Gen /files/a_519ead563fdcb392: HTTP 502": the build server could not fetch a file from Gen. No file in this repo names that asset id, and there is no compile error to fix (no .hawkes/beta-build.md). I read the already-merged code and found it complete:
- `Tools/Web/hawkes-playtest-bridge.js` patches the stand-in `window.hawkesPlaytest` to list every id in `scenarios.json`, forwards `start` to Unity's bridge, and presses "Play in portrait anyway" by itself.
- `WebGlBuild.InstallPlaytestBridge` writes `scenarios.json` from the `PlaytestScenario` rows, installs the bridge and tags the page, on both deploy paths (lines 216 and 494).
- Change in this pass: `.hawkes/scenario.txt` now holds `finale-carry` and `{"mobile":true}` (item 3 of the task).
Not run: Unity compile and playtests (no editor here, and I may not start builds). Retrying the build is the next step; a repeat 502 is a Gen/server problem, not this code.
