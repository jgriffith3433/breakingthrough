#!/usr/bin/env python3
"""Writes the three ADDRESS FILED closing reels (Assets/Resources/Cinematics/ending_<variant>.json).

Which one plays is decided in code by Endings.cs (Docs/Story.md, "Endings and New Game+"). They reuse the stand-in stills
(Tools/Cinematics/make_placeholders.py) through "alt", so they need no GPU art; when generated keyframes named
ending_<variant>_<n>_back / _front exist in Resources/Cinematics/Art they win automatically.
Usage: python3 Tools/Cinematics/make_endings.py
"""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Cinematics")

def shot(rid, n, speaker, seconds, alt, line, front=None, zoom=(1.3, 1.05)):
    s = {"id": "s%d" % n, "back": "%s_%d_back" % (rid, n), "speaker": speaker, "seconds": seconds,
         "fade": 0.0 if n == 1 else 0.7, "level": min(1.0, 0.5 + 0.1 * n),
         "from": [0, 0, zoom[0]], "to": [0, 0, zoom[1]], "alt": alt, "line": line}
    if front:
        s["front"] = front
    return s

REELS = {
    "ending_ledger": [
        ("N", 5.6, "ring_back", "THE LEDGER. One line of writing around the ring, then one door number, in Wallit's own hand.", None),
        ("W", 3.2, "stamp_back", "My stamp. My door.", "stamp_front"),
        ("S", 2.6, "slice_back", "One door.", "slice_front"),
        ("W", 3.4, "hollow_back", "Paid in full. Nobody else's name on it.", "wallet_front"),
        ("N", 5.0, "ring_back", "The ledger closes. The ring keeps the one name on it, and Slice keeps the ring.", None),
        ("S", 2.8, "slice_back", "Clean.", "slice_front"),
    ],
    "ending_doors": [
        ("N", 5.6, "ring_back", "TWO DOORS. Slice writes the number twice, one stroke at a time. The second door sits beside the first.", None),
        ("S", 2.8, "slice_back", "Yours. Next to mine.", "slice_front"),
        ("W", 3.2, "stamp_back", "Two numbers. One bill. Pay up.", "stamp_front"),
        ("N", 5.0, "hollow_back", "Two lamps come on in the Hollow, side by side. The circle has a neighbour.", None),
        ("Y", 3.4, "crest_back", "Two doors! Two deliveries! Uploading... neighbours!", "crest_front"),
        ("S", 3.0, "slice_back", "Keep up, Wallit.", "slice_front"),
    ],
    "ending_open": [
        ("N", 5.8, "hollow_back", "THE OPEN DOOR. The slips on the office wall are pinned, every one. Spare stands in the doorway, lid on straight.", None),
        ("Y", 3.2, "crest_back", "Chirp! Everyone is on the books!", "crest_front"),
        ("N", 5.0, "ring_back", "Brim has an address. Tick keeps the hour. Chime rings once for the right door.", None),
        ("W", 3.4, "wallet_front", "The whole Hollow. Somebody is paying for the lamps. Me.", None),
        ("S", 2.8, "slice_back", "Numbered. All of them.", "slice_front"),
        ("N", 4.4, "hollow_back", "The door stays open. Nothing strikes it.", None),
    ],
}

for rid, rows in REELS.items():
    shots = [shot(rid, i + 1, sp, sec, alt, line, front) for i, (sp, sec, alt, line, front) in enumerate(rows)]
    with open(os.path.join(OUT, rid + ".json"), "w") as f:
        json.dump({"id": rid, "bed": "ch6_score", "shots": shots}, f, indent=2)
        f.write("\n")
    print(rid, len(shots), "shots")
