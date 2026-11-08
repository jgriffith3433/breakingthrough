#!/usr/bin/env python3
"""Voice/sound pass 2: writes Tools/Audio/pass2/<cast>.json table-read batches (same shape as batches/) for the lines the
game names but has no file for yet. Run from the repo root. Lodger lines are added once their voices (cast.json) exist."""
import json, re, os
def slug(t):
    out, gap = '', False
    for ch in t.lower():
        if ch.isalnum():
            if gap and out: out += '_'
            gap = False; out += ch
        else: gap = True
    return out
cast = {m['id']: m for m in json.load(open('Assets/Resources/Cast/cast.json'))['members']}
L = {
 'wallit': ["You're not afraid of the circle?", "You've got no head. How do you hear?", "You were in the hub.",
            "Zero delivered. Six returned. Somebody's cooking the ledger."],
 'slice': ["Always. Don't thank me.", "Behind you.", "Blank is a fresh account.", "Clean. Stand as long as you like.", "Closer.",
   "Counted. Four would be rude.", "Door's yours. Stay clear of the lamp.", "Every slip on the wall was pinned. The Floor sends fewer now.",
   "Fine. Keep swinging.", "Good. Keep swinging.", "I'm armed.", "Last time you signed for the notice. Two names, one pen. Do it again.",
   "Let it. Ring is full.", "Look west. The trail. Lost things, lying where they landed.", "Out. North-east.", "Paid. In kind. Stay.",
   "Quiet is rent. Take it.", "Sender unknown. Billing void.", "Take them.", "The door never shut. It will stay open.",
   "The Hollow knows our door. Somebody will knock.", "The Registrar reads one name aloud. Last time it was yours alone.",
   "The Server kept two names last time. It will ask for two.", "They can't hear you.", "Two doors. Wallit's is next to mine. Knock on it.",
   "Two numbers, side by side. Same pen. Same line.", "Wallit.", "You did. Don't boast.",
   "You filed the ledger alone. The Foreman reads it back differently now.", "Yours. Keep the receipt."],
 'narrator': ["A gummed label on Brim's hook reads BRIM, in Brim's own slow hand.",
   "Brim hangs on the wall by the door number, band on, left of the desk. It has an address.",
   "Chime rings once for the right address. It is the quietest bell in the Hollow.",
   "Spare chirps once, properly, and the Hollow answers.",
   "Spare is in the doorway of the ring, lid on straight, cheering at nothing in particular.",
   "Tick is wound. Nobody had to be the Department to do it.", "Tick keeps the hour. Every pin where it should be.",
   "Twelve slips on the office wall, every one pinned. The Department files itself."],
 'yobot': ["Lids on! The door opens for us this time!", "The ledger knows us! Uploading... a door!"],
 'courier': ["Second notice! This address is still unknown.", "Special delivery! Notice of striking!"],
}
# lodger lines: every Story.Lodgers line and every "L|" line in HubLife.cs (needs voices first)
src = open('Assets/Scripts/Story.cs', encoding='utf-8-sig').read()
lod = {}
for m in re.finditer(r'new Lodger \{ Id = "(\w+)".*?Lines = new\[\] \{(.*?)\} \}', src, re.S):
    lod[m.group(1)] = re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(2))
hub = open('Assets/Scripts/HubLife.cs', encoding='utf-8-sig').read()
for m in re.finditer(r'Lodger = (\d)|Id = "(\w+)"', hub): pass
json.dump(lod, open('Tools/Audio/pass2/lodger_story_lines.json', 'w'), indent=1)
for who, lines in L.items():
    m = cast[who]
    json.dump({'cast': who, 'voice': m['voiceId'], 'provider': m['provider'], 'ids': [who + '_' + slug(t) for t in lines],
               'lines': [{'who': who, 'text': t, 'direction': m['direction']} for t in lines]},
              open('Tools/Audio/pass2/%s.json' % who, 'w'), indent=0)
    print(who, len(lines), sum(map(len, lines)))
print({k: len(v) for k, v in lod.items()})
