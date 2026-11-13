#!/usr/bin/env python3
"""Lists every spoken line with no audio. Run from the repo root: python3 Tools/Story/check_voice.py [--json out.json] [--quiet]
Walks Assets/Resources/Story/campaign_script.json (scenes, choice answers, opening/closing shots) and every
Assets/Resources/Cinematics/*.json reel. A line is voiced when the file the game would load exists:
  Resources/Voice/<castid>_<slug>.mp3   (slug = SliceVoice.LineId; Wallit, Slice, cast voices, scenes and in-world)
  Resources/Voice/cinema/<reel>_<shot>.mp3   (a reel shot from a speaker other than Wallit or Slice; CutsceneShots.PlayCastVoice)
Reel shots by Wallit (W) or Slice (S) go through WallitVoice/SliceVoice, so they need the slug file. Exit code 1 when any is missing.
The same rule is mirrored in Playtest.cs (scenario "bible", metric bibleSilentLines)."""
import json, pathlib, sys
root = pathlib.Path(__file__).resolve().parents[2]
res = root / "Assets/Resources"
def slug(t):
    out, gap = "", False
    for ch in t.lower():
        if ch.isalnum():
            if gap and out: out += "_"
            gap = False; out += ch
        else: gap = True
    return out
cast = {m["code"]: m["id"] for m in json.loads((res / "Cast/cast.json").read_text(encoding="utf-8"))["members"]}
SPEAKERS = set(cast)
def stem(code, text): return "%s_%s" % (cast.get(code, "narrator"), slug(text))
import subprocess
# Audio may be absent from a sparse or partial checkout: the tracked file list counts as present too.
tracked = set(subprocess.run(["git", "ls-files", "Assets/Resources/Voice"], cwd=root, capture_output=True, text=True).stdout.split("\n"))
def has(p): return (res / p).exists() or ("Assets/Resources/" + p) in tracked
missing, total = [], 0
def want(where, code, text, extra=None):
    """extra: a second acceptable file (the reel's own cinema clip)."""
    global total
    if not text or code not in SPEAKERS: return
    total += 1
    files = ["Voice/%s.mp3" % stem(code, text)]
    if extra: files.append(extra)
    if not any(has(f) for f in files):
        missing.append({"where": where, "speaker": code, "cast": cast[code], "text": text, "stem": stem(code, text),
                        "cinema": extra})
def spoken(line):
    c, _, t = line.partition("|")
    return c, t
d = json.loads((res / "Story/campaign_script.json").read_text(encoding="utf-8"))
blocks = [("ch%d" % c["n"], c) for c in d["chapters"]] + [("ending", d["ending"])]
for label, c in blocks:
    for s in c["scenes"]:
        for l in s["lines"]:
            code, t = spoken(l); want("%s scene %s" % (label, s["id"]), code, t)
    for ch in c["choices"]:
        for op in ch["options"]:
            for part in op["now"].split("\n"):   # an answer can be two spoken lines
                code, t = spoken(part); want("%s choice %s/%s" % (label, ch["id"], op["key"]), code, t)
    for k in ("opening", "closing"):
        for i, s in enumerate(c[k], 1):
            want("%s %s shot %d" % (label, k, i), s["speaker"], s["line"])
reels = {}
for f in sorted((res / "Cinematics").glob("*.json")):
    r = json.loads(f.read_text(encoding="utf-8"))
    if "shots" not in r: continue
    reels[r["id"]] = r
    for s in r["shots"]:
        own = None if s["speaker"] in ("W", "S") else "Voice/cinema/%s_%s.mp3" % (r["id"], s["id"])
        want("reel %s %s" % (r["id"], s["id"]), s["speaker"], s.get("line", ""), own)
if "--json" in sys.argv:
    pathlib.Path(sys.argv[sys.argv.index("--json") + 1]).write_text(json.dumps(missing, indent=1), encoding="utf-8")
if "--quiet" not in sys.argv:
    for m in missing: print("%-28s %-12s %s" % (m["where"], m["cast"], m["text"]))
print("spoken lines %d, silent %d (reels %d)" % (total, len(missing), len(reels)))
sys.exit(1 if missing else 0)
