# The Place Department: Campaign bible

"Where things that should not exist are given an address."

> **Generated file.** The screenplay lives as data in `Assets/Resources/Story/campaign_script.json` (loaded by
> `CampaignScript.cs`, checked by the `bible` playtest). This page is rendered from it by `python3 Tools/Story/render_bible.py`
> from the repo root: edit the JSON (or `Tools/Story/bible_head.md` for this opening), then re-render. The opening and
> closing lines that exist in `Assets/Scripts/Story.cs` are canon; scenes and choices marked **proposed** await J and are not
> wired into `Story.Chapters` yet.

This is the Campaign told in full: six chapters and the secret ending, in the three case files. It builds on `Docs/Story.md`
(premise, circle, finale) and `Assets/Resources/Cast/cast.json` (speaker codes, names, colours) and changes none of them.
Arcade keeps its own thread; Campaign is the longer telling of the same story.

Line format is `Story.cs`'s: `<code>|<text>`. Codes: `W` Wallit, `S` Slice, `Y` Yo Bot, `M` Mega Yo Bot, `K` the Courier, `P` the Plated
Alpha, `F` the Foreman, `H` the Half-Built, `V` the Server, `R` the Registrar, `O` the Order-Bearer, `N` narration.

## Voices and rules

- **Wallit** talks money and is brash. He never apologizes or admits fear; he converts it to money.
- **Slice** is clipped, a few words, never a joke. She never explains herself fully.
- **Yo Bots** are chirpy and read their orders as cheerful notices. They never understand what they say.
- No piano, no office jokes, no bow, no birds, and the retired bow-era cast does not appear. The Department is a thing that files.
- The Courier, the Stamp and RECEIVED stay. A chapter is one stamp: Chapter 1 RETURN TO SENDER, 2 INVALID, 3 REISSUED, 4 SERVED,
  5 DENIED, 6 ADDRESS FILED, and the ending RECEIVED.
- The creature behind the order is never seen, named or quoted.
- No camera shake in any cutscene. The playfield (150 m round the circle) stays free of solid objects.

## Story spine

**Logline.** A wallet with a bat is stamped "Return to sender", finds no sender, and ends up inside the one filed address in a
hollow where unfiled things are cleared. He defends it for the guard who keeps it, and learns the order against the address
was never about him.

1. Wallit is thrown out with no address. Slice offers him the circle: inside it he exists. [canon]
2. He looks for his sender. There is none; the Hollow ends at a wall the Department built. [canon]
3. The bots are the Department's clearing crew, working to a schedule, stamped out on the Clearing Floor. [canon]
4. The Spider Bots are process servers: they walk to an address and wait while the order is read. The order was aimed at
   Slice's circle before Wallit came. [canon]
5. The order is a form nobody signed. It can be denied. [canon]
6. The Order-Bearer falls; Wallit reads "Strike this address", stamped DENIED; Slice files the circle. ADDRESS FILED. [canon]
7. Ending: with all six lost things, the stamp prints ADDRESS and nothing strikes it. RECEIVED. [canon]

**Where the bots come from.** Yo Bots are the Clearing Crew; the bolts they drop are the Floor's own pay, so Wallit is collecting
what is owed. Plated and Mega Yo Bots are the Floor sending the same unit again, heavier. Spider Bots are process servers.

## Sources read

`Docs/Story.md`, `Assets/Scripts/Story.cs` (chapter data, cutscenes, the finale, the secret ending), `Assets/Resources/Cast/cast.json`,
`ChapterMap.cs` (map looks and layouts), `Courier.cs`, `Server.cs`, `BossPhases.cs`, `AGENTS.md`.
