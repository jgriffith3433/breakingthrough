#!/usr/bin/env python3
"""Collect every spoken line in the game (Story.cs, Choices.cs, Companion.cs, ChapterRun.cs, Journal.cs and the
cinematic reels) into Tools/Audio/lines.json: {code, id, text}. id is the Resources/Voice file name, built the same way as
SliceVoice.LineId (<castid>_<slug>). Run from the repo root."""
import re, json, glob, os, sys
cast = {m['code']: m['id'] for m in json.load(open('Assets/Resources/Cast/cast.json'))['members']}
def slug(t):
    out, gap = '', False
    for ch in t.lower():
        if ch.isalnum():
            if gap and out: out += '_'
            gap = False; out += ch
        else: gap = True
    return out
found = {}
def add(code, text):
    text = text.replace('\\n', '\n').replace('\\"', '"').strip()
    if code not in cast or not re.search(r'[A-Za-z]{2}', text) or text == 'text': return
    cid = cast[code]
    key = (cid, slug(text))
    found.setdefault(key, {'code': code, 'cast': cid, 'id': cid + '_' + slug(text), 'text': text})
pat = re.compile(r'"(?:\?\|[^|"]*\|)?([A-Z])\|((?:[^"\\]|\\.)+)"')
for f in ['Story', 'Choices', 'Companion', 'ChapterRun', 'Journal']:
    for m in pat.finditer(open('Assets/Scripts/%s.cs' % f, encoding='utf-8-sig').read()):
        t = m.group(2)
        if m.group(1) in 'CBTQ?': continue
        add(m.group(1), t)
for f in glob.glob('Assets/Resources/Cinematics/*.json'):
    for s in json.load(open(f)).get('shots', []):
        if s.get('line') and s.get('speaker'): add(s['speaker'], s['line'])
rows = sorted(found.values(), key=lambda r: (r['cast'], r['id']))
json.dump(rows, open('Tools/Audio/lines.json', 'w'), indent=1)
from collections import Counter
c = Counter(r['cast'] for r in rows)
print(len(rows), sum(len(r['text']) for r in rows), dict(c))
