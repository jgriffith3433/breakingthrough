#!/usr/bin/env python3
"""Writes the chapter 1-3 reels (Assets/Resources/Cinematics/ch{1,2,3}_{open,close}.json) and Tools/Cinematics/gen_manifest.json
from the shot lists in Docs/CampaignBible.md. Art names are final (Art/<reel>_<n>_back|front.png): until Hawkes Gen makes them
each shot shows its `alt` placeholder. Voices for non-Wallit/Slice speakers: Resources/Voice/cinema/<reel>_<shot>.mp3."""
import json, os
root = os.path.join(os.path.dirname(__file__), "..", "..")
# (speaker, line, seconds, from, to, level, back prompt, front prompt or None, music cue, alt)
R = {
"ch1_open": ("ch1_score", [
 ("N","Out of the dark comes a stamp: RETURN TO SENDER. Its ink is the only bright thing in the Hollow.",5.5,[0,-0.6,1.1],[0,0.5,1.25],0.6,"black sky over the Moonlit Hollow, a hand-cut RETURN TO SENDER rubber stamp turning high above, red ink glowing","stamp","Low cello pedal, one cold bell","stamp_back"),
 ("W","Ow! That's extra!",3.5,[0.8,0.2,1.3],[-0.4,0,1.1],0.9,"moonlit grass at ground level, ink flash on brown leather","Wallit the wallet, just landed, startled","Pedal breaks into a short run of strings","hollow_back"),
 ("N","",3.5,[0,0,1.4],[0,0,1.08],1.0,"top-down view of nine standing stones lighting a pale gold ring one by one",None,"Held chord, bell on each stone","ring_back"),
 ("S","You're inside. Inside, you exist.",3.8,[-0.5,0.6,1.08],[0.2,-0.2,1.3],0.5,"the circle at dusk, shadow over her face","Slice the goth guard, knife down, low angle on her eyes","Strings drop out; room tone","slice_back"),
 ("W","Does existing come with a receipt?",3.2,[0.2,-0.2,1.3],[0.6,0,1.2],0.4,"over Slice's shoulder, the circle","Wallit looking up, bills falling from his pocket","Silence, one dry plucked note","slice_back"),
 ("Y","Hello! Uploading victory! Striking this address now!",4.2,[0.6,0,1.3],[-0.2,0,1.08],1.0,"wide Hollow, a Yo Bot crests the hill, Wallit and Slice small in the ring","Yo Bot, chirpy, claws up","Pulse enters: bright, thin, unfriendly","hollow_back")]),
"ch1_close": ("ch1_score", [
 ("K","Notice... undelivered... uploading... defeat?",4.2,[0,0,1.05],[0,0,1.1],0.8,"the circle, locked wide, a clipboard flat on the grass","the Courier split and on one knee","Pulse cuts, a single dying chirp","hollow_back"),
 ("W","Return to sender.",3.2,[0.3,0,1.1],[-0.3,0,1.2],0.8,"the notice drifting down beside him","Wallit, bat on his shoulder, over the clipboard","Cello pedal returns, warmer","hollow_back"),
 ("S","Clean.",2.8,[0,0.3,1.4],[0,0,1.1],0.6,"a lowered then re-raised knife blade","Slice's hand and knife","Bell, one note","slice_back"),
 ("W","Bolts. Somebody's paying me.",3.6,[0,0,1.3],[0,0,1.15],0.7,"close on his hands and a coin slot","Wallit gathering three bolts","Held string, rising a half step","wallet_front"),
 ("S","Not somebody. Something.",4.0,[0.3,0,1.3],[-0.4,0,1.1],0.5,"the far dark past the Hollow's wall","Slice in profile","Pedal falls to a low drone","slice_back")]),
"ch2_open": ("ch2_score", [
 ("N","Past the last lantern the Hollow runs out. Far off, a gate stands in the dark with nothing written on it.",5.5,[0,0,1.3],[0,0,1.05],0.6,"a guttering lantern, blue dark beyond, a thin blank gate","","Low drone, distant wind chime","hollow_back"),
 ("W","There's a door. Doors have senders.",3.4,[0.2,0.1,1.2],[0,0,1.1],0.7,"the Hollow's edge toward the gate","Wallit, hands on hips, bat on shoulder","Drone holds, hopeful two-note figure","hollow_back"),
 ("S","Doors have locks. Come back.",3.2,[0,0,1.3],[0.1,0,1.1],0.5,"the ring's last standing stone","Slice in profile, one hand on the stone","Figure ends, held low note","slice_back"),
 ("W","Locks have keys. Keys have owners. Owners get paid.",4.4,[-0.3,-0.2,1.3],[0.3,-0.2,1.15],0.8,"low chalk road, boots stepping over the ring's edge",None,"Soft low drum, 70 bpm","hollow_back"),
 ("S","Ring's staked. I'm coming.",3.4,[0,0.2,1.3],[0,-0.3,1.1],1.0,"a knife driven into the ground, the ring's light jumping to a second ring","Slice's blade","Drum doubles, strings on the beat","ring_back")]),
"ch2_close": ("ch2_score", [
 ("P","Plate... voided... uploading... nothing?",4.0,[0,0,1.05],[0,0,1.2],0.7,"cracked plate, INVALID split into IN and VALID","the Plated Alpha","Drum stops, clipped metallic ring","stamp_back"),
 ("W","Void your own stamp.",2.8,[0.2,0,1.2],[0,0,1.2],0.5,"a boot kicking a plate flat","Wallit over the shoulder","Silence","stamp_back"),
 ("S","You have an address. The circle.",3.8,[0,0,1.15],[0,0,1.05],0.8,"the staked knife pulled, two rings folding into one",None,"Warm bell, one note","ring_back"),
 ("W","Leased. By you.",3.0,[0,0,1.4],[0,0,1.2],0.6,"close on the stone chip of the torn half-postmark","Wallit's hand holding it","Strings reply under him","stamp_back"),
 ("S","Then pay with swings.",4.0,[0,0,1.1],[0,0,1.3],0.9,"Wallit and Slice shoulder to shoulder at the ring, the dark gate behind","","Resolving chord, fade on the drone","hollow_back")]),
"ch3_open": ("ch3_score", [
 ("N","Past the broken gate the ground drops away into a yard lit by lamps. Rows of Yo Bot chassis hang on hooks.",5.8,[0,0.5,1.1],[0,-0.4,1.25],0.7,"the lip of a sunken amber yard, hooks in rows like a ribcage",None,"Slow industrial pulse, steam breath","hollow_back"),
 ("H","Hello! I am... not finished! Uploading victory?",4.0,[0.4,0,1.3],[0,0,1.1],0.8,"hooks and chassis, one eye lit","the Half-Built Yo Bot twitching on a hook","Pulse skips a beat, thin chirp","crest_back"),
 ("W","They're all the same unit.",3.0,[-0.5,0,1.1],[0.5,0,1.1],0.7,"a pan across matching rows of chassis","Wallit studying them","Pulse steadies","hollow_back"),
 ("S","Spare parts. Spare orders.",3.0,[0,0,1.08],[0,0,1.1],0.5,"two-shot, still, lamps behind","Slice at his shoulder reading the lamps","Low drone under the pulse","slice_back"),
 ("N","",3.5,[0,-0.4,1.4],[0,0.2,1.05],1.0,"wide of the yard, the belt glowing far off",None,"Brass swell, then cut","hollow_back")]),
"ch3_close": ("ch3_score", [
 ("F","Order... unread...",3.4,[0,0.2,1.2],[0,0,1.1],0.6,"the order sheet fluttering off its chest","the Foreman on its knees","Pulse stops, a last factory sigh","crest_back"),
 ("W","Paid in full.",3.0,[0,0,1.4],[0,0,1.2],0.5,"a bolt tumbling into his palm","Wallit, bat on shoulder","Quiet bell","wallet_front"),
 ("S","Clean.",2.8,[0,0.1,1.3],[0,0,1.1],0.4,"the dead belt, red lamp dark, its last glow","Slice's eyes","Drone only","slice_back"),
 ("N","",3.5,[0,0,1.4],[0,0,1.08],1.0,"the red lamp lens turning a degree toward the Hollow, blinking",None,"One cold bell, cut to silence","hollow_back")]),
}
manifest = {"sheets": [], "stills": [], "clips": [], "voices": [], "beds": {}, "sfx": ["stamp_land", "lantern_gutter", "belt_stop", "red_lamp_blink"]}
for rid, (bed, shots) in R.items():
    out = {"id": rid, "bed": bed, "shots": []}
    manifest["beds"].setdefault(bed, []).append(rid)
    for i, (sp, line, sec, fr, to, lv, bp, fp, cue, alt) in enumerate(shots, 1):
        sid = "s%d" % i
        sh = {"id": sid, "back": "%s_%d_back" % (rid, i), "speaker": sp, "seconds": sec, "fade": 0.0 if i == 1 and rid.endswith("open") is False else 0.7, "level": lv, "from": fr, "to": to, "alt": alt}
        if fp:
            sh["front"] = "%s_%d_front" % (rid, i)
            manifest["stills"].append({"file": sh["front"], "prompt": fp, "from_sheet": True})
        if line:
            sh["line"] = line
        if sp in "KPHFY":
            manifest["voices"].append({"file": "Voice/cinema/%s_%s.mp3" % (rid, sid), "speaker": sp, "line": line})
        out["shots"].append(sh)
        manifest["stills"].append({"file": sh["back"], "prompt": bp, "music": cue})
        if i in (1, len(shots)):
            manifest["clips"].append({"from": sh["back"], "seconds": 5, "file": "StreamingAssets/Cinematics/%s_%d.mp4" % (rid, i)})
    json.dump(out, open(os.path.join(root, "Assets/Resources/Cinematics", rid + ".json"), "w"), indent=2)
manifest["sheets"] = ["Wallit", "Slice", "The Courier", "The Foreman", "The Plated Alpha", "Yo Bot", "The Half-Built"]
json.dump(manifest, open(os.path.join(root, "Tools/Cinematics/gen_manifest.json"), "w"), indent=2)
print(len(manifest["stills"]), "stills", len(manifest["clips"]), "clips", len(manifest["voices"]), "voices")
