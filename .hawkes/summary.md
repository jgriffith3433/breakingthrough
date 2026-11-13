# Bot sound identity: Gen files baked in

The code from the earlier commit (`BotSound.cs`: family movement loops, footfalls, wind-up tells, deaths, variant layers, a 6-loop cap nearest first) now plays real Hawkes Gen sounds. Before, it played code-synthesised stand-ins.

**Made (audio.sfx, 14 batches of 3, about 0.06 USD of the 0.29 budget):** 41 files in `Assets/Resources/Sfx/bot_<name>[_2|_3].mp3`, named so `GameAudio.VariantsOf` finds them without any code change.
- Movement loops: `bot_move_yo`, `bot_move_spider`, `bot_move_mega`.
- Wind-up tells: `bot_tell_yo` (rising whine), `bot_tell_spider` (hiss), `bot_tell_boss` (ratchet, also used by Mega).
- Deaths: `bot_death_yo` (pop and fizz), `bot_death_spider` (crumple and scattering legs), `bot_death_boss` (long collapse).
- Footfalls: `bot_step_yo`, `bot_step_mega`.
- Chapter variant layers: `bot_layer_paper`, `bot_layer_stamp`, `bot_layer_ticket`.

**Judged and processed** (ffmpeg, my own edit; the raw takes are not committed):
- Tells: Gen made 4 s clips, so each is a 0.65–0.76 s window of the rising part, with fades (Yo whines are sped up 2x). They land inside the 0.4–0.8 s lead (wind-ups are 0.45 s Yo, 0.50 s Spider).
- Loops: the tail is crossfaded into the head so the seam is continuous. Gen's own loop check had failed all three Spider takes.
- Deaths trimmed with fade-outs; all clips levelled per family.

**Rejected / not made:**
- `death_boss` take 3 was 0.08 s long. Boss death has 2 files, and `GameAudio` plays the same take at a shifted rate as the third variant, so the "3 variants, never the same twice" rule still holds.
- Yo death take 3 is only the pop (0.4 s); the other two carry the fizz.
- `bot_step_spider` was never queued. The spider's footfall stays the synthesised clip, and its identity comes from the new skitter loop.

**Checks:** nothing was compiled or played (no Unity in this sandbox, and no builds or playtests are allowed). No code changed in this pass. The `bot-sound` scenario (touch, 390x844, 15+ bots; loop cap, tell lead, death variants) is named in `.hawkes/scenario.txt`.

**Conflicts:** none. The only shared file touched earlier was `docs/world-bible.md`, with a small addition. Unity creates the new `.meta` files on import.
