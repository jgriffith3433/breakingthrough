#!/usr/bin/env python3
"""Split Tools/Audio/lines.json into audio.dialogue(-open) table reads (<1800 chars each) per speaker, skipping lines
audio.json already has. Writes Tools/Audio/batches/<cast>_<n>.json = {cast, voice, lines:[{who,text,direction}], ids:[...]}.
Stems come back in line order: ids[i] is the Resources/Voice file for stem i."""
import json, os
cast = {m['id']: m for m in json.load(open('Assets/Resources/Cast/cast.json'))['members']}
have = {l['id'] for l in json.load(open('Assets/Resources/Voice/audio.json'))['lines']}
os.makedirs('Tools/Audio/batches', exist_ok=True)
for f in os.listdir('Tools/Audio/batches'): os.remove('Tools/Audio/batches/' + f)
by = {}
for l in json.load(open('Tools/Audio/lines.json')):
    if l['id'] in have: continue
    by.setdefault(l['cast'], []).append(l)
for cid, rows in by.items():
    m = cast[cid]; n = 0; cur = []; size = 0
    def flush():
        global n, cur, size
        if not cur: return
        n += 1
        json.dump({'cast': cid, 'voice': m['voiceId'], 'ids': [r['id'] for r in cur],
                   'lines': [{'who': cid, 'text': r['text'], 'direction': m['direction']} for r in cur]},
                  open('Tools/Audio/batches/%s_%d.json' % (cid, n), 'w'), indent=0)
        cur = []; size = 0
    for r in rows:
        if size + len(r['text']) > 1700: flush()
        cur.append(r); size += len(r['text'])
    flush()
