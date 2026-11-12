# Boss eleven_v4 voices + in-fight barks (part 1)

## What was wrong
Campaign bosses shared house/open voices and did not talk back mid-fight, so they read as health bars.

## What changed
- **Bark lines + story:** three paperwork barks each for Courier, Plated Alpha, Foreman, Server, Registrar, Order-Bearer (`Docs/Story.md` "Boss barks"; banks in `BossVoice.cs`).
- **Wiring:** `BossVoice.cs` plays through `VoiceDirector` (`boss-phase` Key, `boss-homerun` Normal, `boss-win` Key). Hooks from `BossPhases`, `Courier`, `BossSetPiece`, `BatEquipment` (home run on a boss), `LevelScene.Defeated`, `WaveDirector.MusicianDown`. Yo-family bosses get a light `AudioHighPassFilter` on the mixer source (not baked into files). Captions use `UiFit.PlaceCaption` (upper third on phone).
- **Distinct eleven_v4 voice ids** in `cast.json` + `audio.json` (six premade library ids; none shared). 18 bark rows registered in `audio.json`; batch list in `Tools/Audio/batches/boss_barks.json`.
- **World bible:** full Characters entries for all six bosses (Want/Arc/Moment/Personality/Voice/Animations).
- **Playtest:** `boss-barks` scenario (`PlaytestBossBarks.cs`); `.hawkes/scenario.txt` is phone 390x844.

## Blocked (Gen credits)
Tested one `audio.voice` line first: Gen has **1 credit left** (needs 8). See `.hawkes/stuck.md`. Custom voice-design estimate alone ~$1.11 vs $0.86 budget. Stems are pending (`.hawkes/pending-gpu.txt`); subtitles still show.

## Checks
- JSON parse of cast/audio/batch: ok
- Six voice ids unique: ok
- Brace balance on new C#: ok
- Unity compile not run here (no editor in sandbox)
- Fun review (self, subagent usage capped): **thin** — wiring and tone are living-world; silent stems until credits
- Code review (self): **revise for stems** before criteria 2–4 can pass on beta audio; code path and distinct ids are ready

## Conflicts
None with `c_ccaa518c99cf` (dialogue two-shots); did not touch cutscene/portrait presentation.

## Left for next pass (credits + optional budget bump)
1. Record 18 bark stems (+ 11 existing boss line re-records) with `audio.dialogue` / `audio.voice`, ffmpeg mono 32 kHz 56 kbps, `studio_gen_import`.
2. Optional: replace premade ids with custom `audio.voice-design` (~$1.11+) if J wants designed voices over library ones.
