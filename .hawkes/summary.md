# Slice as partner (callouts + act guard) — voices wired

## What was wrong
Arcade Slice was mostly an escort HP bar. Prior commit `22977c7` added `SliceCallouts` (markers, parry/stamp, playtest) and Wallit reply clips, but Slice callout stems and the parry clink were still waiting on Gen.

## What changed (this pass)
- **Judged & baked Gen audio** (`j_504e2676518d` dialogue-open / hv_heroine, ~$0.019): 13 Slice stems → mono 32 kHz 56 kbps `Assets/Resources/Voice/slice_{left,right,behind,ahead,spider,mine,close,flank,clear,yo,down,blocked,stamp}.mp3` (+ `slice_hold`); asset ids stamped in `audio.json`. Durations 0.65–0.86 s, mean ~−17 dB — pass.
- **Parry SFX** (`j_2f0c83f1a634` a_93ee534b9e1f2db5): Gen verify said −70 LUFS (false for this short take); local probe was loud. Baked mono 64 kbps `Assets/Resources/Sfx/sliceparry.mp3`. Removed `SfxSynth` stand-in so `GameAudio` loads the file.
- **Wallit replies** restored into the sparse tree (`wallit_on_it` / `billable` / `on_it_billable` / `covered`).
- **Reviewer fixes:** `callout-reply` is VoiceDirector **Normal**; replies wait the cast gap (`GapReady`) instead of dropping; patience 10 s; parry knock ~2 m (`Knock` ×10); act unlock shows block/stamp pose + shock; `slice-partner` forces **390×844** touch and asserts `Voice/slice_left` + `Sfx/sliceparry` clips exist.

## Story / bible
Already in `Docs/Story.md` (Slice as partner) and `docs/world-bible.md` (Characters + beat 13). No canon conflict.

## How checked
- ffprobe / volumedetect on every baked clip; audio.json parses; LineId ↔ file names match.
- Read-through for CS0102/CS0111 on `GapReady`, `SayCallout`, `BeginParry` / `BeginStamp`.
- **Unity compile not run here** (no editor). Studio cloud build + `slice-partner` playtest should prove AC 1–4.
- Conflicts: none with c_7b31e5719065 (Hollow landmark); we did not touch arena dress.

## GPU
This task’s Gen: dialogue-open ~$0.019 + sfx ~$0.001 (+ earlier Wallit `audio.voice` cents). Under $0.38 budget. No jobs left waiting.

## Reviewer verdicts
- **studio:reviewer (after fixes):** earlier revise notes addressed (voices committed, sliceparry + synth removal together, callout-reply Normal + gap hold, 390×844 + clip asserts, knock tuned). Ready to merge pending cloud playtest.
- **studio:fun-reviewer:** **thin** (not demo). Gaps left as follow-ups: Last Light stamp in `slice-partner`, stronger call-and-response when swinging a marked bot. Acceptance only requires Act 2 parry + 3 callouts.

## Scenario
`slice-partner` (`.hawkes/scenario.txt`).
