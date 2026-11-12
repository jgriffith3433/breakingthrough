# Conflict pass: merge origin/beta into boss-barks

## What happened
Beta moved with #258 (hub conversations as acted two-shots). Merged `origin/beta` into this branch and kept both sides' work.

## Conflicts and how each was resolved

| File | Resolution |
|---|---|
| `.hawkes/scenario.txt` | **Conflict.** Kept this change's playtest pointer: `boss-barks` + phone JSON `{"mobile":true,"width":390,"height":844}`. Beta's `hub-two-shot` stays registered and playable in `Playtest.cs` (Studio only reads line 1 of scenario.txt for this change's criteria). |
| `Assets/Scripts/Playtest.cs` | **Auto-merged clean.** Both `boss-barks` / `BossBarksRun` and `hub-two-shot` / `PlaytestTwoShot.Run` stay registered and dispatched. |
| `docs/world-bible.md` | **Auto-merged clean.** Kept this change's six boss Characters rows (distinct eleven_v4 voice ids + BossVoice moments) and beta's ConversationStage talk animations, Office two-shot interactions, and story beat 14. |

No unsafe binary / scene / prefab conflicts. Did not abort.

## Also pulled from beta (no conflict)
`ConversationStage.cs`, `PlaytestTwoShot.cs`, and CastVoice/ChapterHub/HubAmbience mouth-amplitude wiring for acted hub talks.

## Checks re-run
- Six campaign boss `voiceId`s in `cast.json` still distinct (Josh/Adam/Arnold/Charlotte/Harry/James); provider elevenlabs
- `cast.json`, `audio.json`, `Tools/Audio/batches/boss_barks.json` parse
- No conflict markers in Playtest.cs / world-bible / scenario.txt
- Brace balance on Playtest.cs matches both parents (string-stripped)
- Unity compile not run here (no editor in sandbox)

## Other agents
`c_0e22a5366cf1` (finale Slice address restore) has no files yet — no overlap.

## Still blocked (unchanged from prior pass)
Gen credits too low to record the 18 bark stems; subtitles + distinct voice ids + wiring are in. Stems still need a credits/budget pass.
