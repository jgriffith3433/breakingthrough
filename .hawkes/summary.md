# Sound variants: the files #213/#214 left out

**Wrong:** #213/#214 added the variant code but no sound files, because Gen was called as `theplacedepartment` ("No access"). With `org_eb0d5c82e8da8c2d.theplacedepartment` `sound_effect` works (about $0.006-0.018 a clip, about $0.35 in all with retries).

**Changed:** 30 new mp3 files in `Assets/Resources/Sfx/`, no code change. Every name already matches what `GameAudio` / `PanelFx` load.
- Combat, 3 each (the existing file is variant 1): `hit_2/_3`, `batcrack_2/_3`, `batswing_2/_3`, `coin_2/_3`, `stomp_2/_3`, `stamp_2/_3`, `death_2/_3`. `botstep`, `botstep_2`, `botstep_3` (there was no file, only a synth).
- Home run: `homerun_whoosh`, `homerun_clang` (the names `BatEquipment` plays).
- UI: `cardflip1-3`, `stamp1-3`, `panelslide1-3`, `buildup`.
- Each clip is mono, 44.1 kHz, 64 kbps. Peak loudness (200 ms window) is matched to the existing `hit` sample, so all of them sit within about 2 dB of it. The six takes that were nearly silent were regenerated. Peaks are capped at -1.5 dBFS.
- Added size is 248 KB, under the 1.5 MB limit.

**Checked:** md5 shows no duplicate files, and loudness was measured with ffmpeg. Unity was not run here, so I did not see the `audio` playtest. It is the scenario named in `.hawkes/scenario.txt` and already logs `audio-variants` and the repeat counts. Unity will create the `.meta` files on import.
**Look at:** the `/disk` checkout is sparse, so I could not compare against the existing mp3s directly. I read their loudness from git. Whether `cardflip`/`stamp` get logged on a phone-sized level-up is for the beta playtest to confirm, since `PanelFx` falls back to coin/shot only when the files are missing.
**GPU estimate:** none used.
