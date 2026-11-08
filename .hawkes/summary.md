Imported the 7 scene sounds and 7 ambience loops already made in Hawkes Gen (jobs j_9fc7961d0c3a, j_bf0e6a84873b). No new generation, no GPU.
- Assets/Resources/Sfx/: yobot_wake, flag_flutter, slot_shutter, gallery_rise, talk_start, clipboard_raise, paper_flutter (mp3, 96 kbps). shutter_lift left out as asked.
- Assets/StreamingAssets/Music/: amb_hub, amb_ch1..amb_ch6 (mono, 96 kbps, about 30 s each).
- Every file was re-encoded with ffmpeg. Each starts with ID3 and none holds a git-lfs pointer. `git diff --cached --numstat` lists all 14 as binary.
- Loops: Gen had already crossfaded the ends. amb_ch2 had been flagged with a 7.4 dB level jump at the loop point, so I re-crossfaded its tail into its head over 4 s; it is now 26 s long. The others are untouched.
- Not done: I never ran Unity or listened to the files. I did not write `.meta` files; Unity makes them on first open. The amb_ch2 fix is unheard.
- Merge conflicts: none. GPU estimate: none needed.
