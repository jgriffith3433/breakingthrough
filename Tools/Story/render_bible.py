#!/usr/bin/env python3
"""Renders Docs/CampaignBible.md from Assets/Resources/Story/campaign_script.json.
Usage: python3 Tools/Story/render_bible.py   (run from the repo root; edit the JSON, never the generated sections)"""
import json, pathlib
root = pathlib.Path(__file__).resolve().parents[2]
d = json.loads((root / "Assets/Resources/Story/campaign_script.json").read_text(encoding="utf-8"))
head = (root / "Tools/Story/bible_head.md").read_text(encoding="utf-8")
cast = {m["code"]: m["name"] or "NARRATION" for m in json.loads((root / "Assets/Resources/Cast/cast.json").read_text(encoding="utf-8"))["members"]}
def spk(line):
    c, _, t = line.partition("|")
    return "**%s:** %s" % (cast.get(c, c), t.replace("\n", " / "))
o = [head.rstrip(), ""]
o.append("## Character arcs\n")
for a in d["arcs"]:
    o.append("### %s\n\n- Start: %s\n- Turns:" % (a["who"], a["start"]))
    o.extend(["  - " + t for t in a["turns"]])
    o.append("- End: %s\n" % a["end"])
o.append("### The Department (summary)\n\n%s %s\n" % (d["department"]["arc"], d["department"]["unseen"]))
o.append("## Choice flags\n\n| Flag | Set by | Read by |\n|---|---|---|")
for f in d["flags"]:
    o.append("| `%s` | %s | %s |" % (f["id"], f["set"], f["reads"]))
o.append("")
def block(title, c, is_ch):
    o.append("## %s\n" % title)
    o.append("Status: %s. Emotion: %s\n" % (c["status"], c["emotion"]))
    o.append("### Script\n")
    for s in c["scenes"]:
        o.append("**Scene %s: %s** (%s) [%s]  \n*%s*\n" % (s["id"], s["name"], s["where"], s["status"], s["beat"]))
        o.extend(["> " + spk(l) + "  " for l in s["lines"]])
        o.append("")
    sp = c.get("scriptedSetPiece") or c.get("setPiece")
    o.append("### Scripted set piece: %s%s\n\n%s\n" % (sp["name"], " [%s]" % sp["status"] if "status" in sp else "", sp["text"]))
    o.append("### Choices\n")
    for ch in c["choices"]:
        o.append("**%s** at %s: %s" % (ch["id"], ch["at"], ch["prompt"]))
        for op in ch["options"]:
            o.append("- *%s* (%s)%s. Now: %s. Later: %s" % (op["key"], op["label"], " sets `%s`" % op["set"] if op["set"] else "", spk(op["now"]), op["later"]))
        o.append("")
    for k, t in (("opening", "Opening cutscene shot list"), ("closing", "Closing cutscene shot list")):
        o.append("### %s\n\n| # | Shot | Camera | Speaker | Voice line | Music |\n|---|---|---|---|---|---|" % t)
        for i, s in enumerate(c[k], 1):
            o.append("| %d | %s | %s | %s | %s | %s |" % (i, s["shot"], s["camera"], cast.get(s["speaker"], s["speaker"]), s["line"] or "(none)", s["music"]))
        o.append("")
    m = c["map"]
    o.append("### Map design sheet: %s\n" % m["id"])
    o.append("- **Setting:** %s\n- **Landmarks:** %s\n- **Route:** %s\n- **Hazards:** %s\n- **Boss arena:** %s\n- **Palette:** %s\n- **Props:** %s\n" % (
        m["setting"], "; ".join(m["landmarks"]), m["route"], "; ".join(m["hazards"]), m["bossArena"], " ".join("`%s`" % p for p in m["palette"]), "; ".join(m["props"])))
for c in d["chapters"]:
    block("Chapter %d: %s (stamp: %s)" % (c["n"], c["title"], c["stamp"]), c, True)
e = d["ending"]
o.append("Gate: %s\n" % e["gate"])
block("Ending: %s" % e["title"], e, False)
o.append("## Open canon questions\n\nEach carries the answer this bible assumes, so work can go on unless J overrules.\n")
for q in d["openQuestions"]:
    o.append("%s. **%s** Assumed: %s" % (q["id"], q["q"], q["assumed"]))
o.append("\n## What's next (not done here)\n\n- Wire the text into `Story.Chapters` (the lines are already in `Story.cs` format), and add a choice page to `Cutscene.cs` for the choices (open question Q8).\n- Store the choice flags in `CampaignSave.Slot` (a flag word; save version bump with a `Migrate` step).\n- GPU work (cutscene stills, backdrops, models, music): see the estimate in the summary. Nothing was generated.\n")
(root / "Docs/CampaignBible.md").write_text("\n".join(o), encoding="utf-8")
print("rendered", len("\n".join(o)), "chars")
