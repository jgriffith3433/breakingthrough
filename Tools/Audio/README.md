# Voice, score and set-piece sounds

- `collect_lines.py` reads every spoken line (Story.cs, Choices.cs, Companion.cs, ChapterRun.cs, Journal.cs, the reels) into `lines.json` (`id` = the `Resources/Voice` file name: `<castid>_<slug>`, slug as `SliceVoice.LineId`).
- `make_batches.py` splits the lines Gen has not voiced into table reads (`batches/<cast>_<n>.json`, stems come back in line order: `ids[i]` is the file).
- Voices are in `Resources/Cast/cast.json` (`voiceId`, `voiceName`, `direction`, `provider`): Wallit keeps his ElevenLabs voice (`audio.dialogue`), everyone else is a Gen house voice (`audio.dialogue-open`).
- Music is `StreamingAssets/Music/<track>.mp3` (names in `MusicDirector.cs`; fetched by URL when played, so it stays out of the WebGL data file); set-piece sounds are `Resources/Sfx/<name>.mp3` fired from `Resources/Cinematics/sfx.json` and `ChapterRun`. Keep everything mp3 (voices ~64 kbps mono, music ~96 kbps, 48 s loops) for WebGL. Voice and sfx are re-encoded at import by `Assets/Editor/AudioImportRules.cs`.
- Playtest: `score`.
