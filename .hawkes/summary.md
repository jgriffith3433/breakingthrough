# Finale carry beat: beta build

**What was wrong:** the failed beta build (run r_9ea1fbb31570) was not a code error. Studio's message was "out of credits: unity.build needs 50 and 1 is left". The only compile error since then, CS0019 in `ConversationStage.cs`, was already fixed on beta by a007a51 (#272), which this branch starts from.

**What I checked:** `Assets/Scripts/CinematicFinale.cs` (the hold-to-carry beat, 3 notices, lamps, RECEIVED path) and the `finale-carry` scenario in `Playtest.cs` are both present and unchanged. I read through them and found nothing to fix. Unity compile was not run here (no editor on this machine).

**What I changed:** nothing in code. `.hawkes/scenario.txt` names `finale-carry`.

**For J:** the build needs credits topped up in Settings → Plan and credits before beta can build. No GPU work in this change, so no estimate.
