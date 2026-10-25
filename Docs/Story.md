# The Place Department: the story

"Where things that should not exist are given an address."

This is the story the game tells. `Assets/Scripts/Story.cs` holds every line below as data; if you change a line, change
it in both. Voices: **Wallit** talks money. **Slice** is clipped, a few words, never a joke. **Yo Bots** are chirpy.
Warrok, the bow and the birds are retired and never appear here.

## The premise

The Place Department files things that should not exist. A thing that is filed has an address, and a thing with an
address is allowed to stay. A thing without one is cleared away.

**Wallit** is a wallet with a wooden bat, the kind of thing that should not exist. The Department stamped him
"Return to sender", found no sender, and threw him into the Moonlit Hollow with a bat and no refund.

**Slice** is already there. She is the Department's guard of **the circle**: a ring on the ground, the one address in the
Hollow that is filed. Inside it, things exist. Slice stands in it with her scalpel knife out and does not play the piano
any more.

## Why the circle matters

The circle is the only address in the Hollow. Wallit has none, so while he is inside it he is on the books: he exists,
he can be hurt, he can be paid. If Slice falls, the circle is struck off and everything in it is cleared with her. The
run ends when Slice falls. That is the whole game: Wallit swings, Slice holds the ring.

## Who comes

**Yo Bots** are the Department's chirpy clearing crew ("Uploading victory!"). They come in small, then Plated, then
Mega. **Spider Bots** are the big ones: they walk on six legs and stab. Both carry the same order: strike this address.
The creature behind the order is never seen and never named.

## The thread

Every wave start gets a short beat. A beat is one to three lines, one speaker each, shown for about two seconds.
Boss waves (10, 20 and 30) get a title card and a longer beat.

- **Act one, waves 1 to 9: the arrangement.** Wallit is here for the coins, Slice is here for the circle. The Yo Bots are
  cheerful about erasing them. Wallit works out that the bolts the bots drop are a wage, and Slice admits she needs him.
- **Act two, waves 10 to 19: the plating.** The Plated Yo Bot arrives on wave 10. Slice is hurt for the first time and says
  so. Wallit stops talking about himself and starts talking about her debt ("I'm covering you").
- **Act three, waves 20 to 29: the order.** The Mega Yo Bot arrives on wave 20 and the Yo Bots stop chirping and read the
  order aloud. Wave 30 is the first Spider Bot, the one that carries the order itself.

## The finale: wave 30

Wave 30's boss is the Spider Bot. When the last bot of wave 30 falls, the order drops, and Wallit reads it: "Strike this
address", stamped DENIED. Slice files the circle herself. The ending screen reads **ADDRESS FILED**, and gives the run:
waves, level, streak, coins. Any key goes back to the menu; **Enter keeps going** into overtime, where the waves
continue and the run still ends when Slice falls.

## The intro

Shown once at the start of a run that begins at wave 1, over the frozen Hollow. Five pages, any key or tap for the next,
**Skip** (or Enter) leaves at once.

1. The tagline and the place.
2. Wallit says who he is.
3. Slice says what the circle is.
4. Slice says why it matters.
5. A Yo Bot arrives. Wallit says what to do.

## Wallit talks

Wallit barks while he fights, in `WallitVoice.cs` (banks) and by voice file (`Resources/Voice/wallit_<line>.mp3`, ids in
`Resources/Voice/audio.json`, made with Hawkes Gen `audio.voice` in his Dose Of Destruction voice). He is brash and angry and
everything is money: a kill is "Paid in full." or "Exact change.", a streak is "Cash rain!", a big hit is "Interest due!",
a home run is "Returned to sender!", a hit on him is "Ow! That's extra!", a wave start is "Cash only!", a level-up is
"Profit!", a chest is "Finders keepers!", and when Slice runs low he answers "Slice! Hold on!". No jokes about work, and
the wave beats' Wallit lines above are spoken too. One voice at a time: he waits for Slice to finish and never repeats a
line back to back. His mouth is a part on the painted face (`WallitMouth.cs`) that opens and closes with the loudness of
what he says.

## Canon established here

- The circle is the one filed address in the Moonlit Hollow; whatever is inside it exists.
- Wallit was stamped "Return to sender" and thrown in. He has no address of his own.
- Yo Bots and Spider Bots are sent to strike the circle's address. Wave 30's Spider Bot carries the order.
- The Place Department is the thing that files and clears. The creature behind the order is never named.
