#!/usr/bin/env python3
"""Writes every campaign reel (Assets/Resources/Cinematics: ch1-6 open/close, boss intros, ending, credits) and Tools/Cinematics/gen_manifest.json
from the shot lists in Docs/CampaignBible.md, and every shot's keyframe (Art/<reel>_<n>.jpg) from Tools/Cinematics/keyframes.json. Voices for non-Wallit/Slice speakers: Resources/Voice/cinema/<reel>_<shot>.mp3."""
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

# ---- Part 2: chapters 4-6, the four boss intros, the ending and the credits (same tuple format) ----
R.update({
"ch4_open": ("ch4_score", [
 ("N","A causeway of pale slabs runs out over a pit of mist. Six lamps stand along it, paired like folded legs.",5.6,[-0.6,0,1.1],[0.6,0,1.2],0.6,"a long pale causeway over a pit of mist at dusk, paired lamp stilts like folded spider legs",None,"Three held notes, string harmonics","hollow_back"),
 ("Y","Spiders en route! Chirp!",3.0,[0.3,0,1.4],[0,0,1.1],0.8,"far down the causeway a Spider Bot lifts a foreleg and tilts its glass toward us","Yo Bot, chirpy, claws up","A single bright chirp, then silence","crest_back"),
 ("S","Mind the legs.",2.4,[0,0,1.3],[0.1,0,1.15],0.5,"the mist pit, lamp light on her face","Slice in profile, eyes on the bot","Harmonics hold","slice_back"),
 ("W","They're not bigger Yo Bots. They're... deliveries.",3.8,[0,0,1.2],[0,0,1.1],0.7,"the causeway slabs, legs walking in the distance","Wallit hefting the bat, watching the legs","Low hum underneath","hollow_back"),
 ("S","Process servers. They stand and wait while it's read.",4.4,[0,0,1.05],[0,0,1.4],1.0,"a Spider Bot stops, folds one leg and offers a paper on its claw tip","a paper on a claw","A polite minor figure on harp, nothing else","stamp_back")]),
"ch4_close": ("ch4_score", [
 ("V","Delivery... undelivered... chirp?",3.6,[0,-0.3,1.1],[0,0.4,1.1],0.7,"the Server in pieces on a lamp stand, its paper fluttering toward the pit","the Server, split, one leg folded","Harp figure stops mid-phrase","crest_back"),
 ("W","Return to sender.",2.6,[0.2,0,1.25],[0,0,1.2],0.4,"the paper vanishing into the mist","Wallit over the shoulder, hold","Silence, then one low bell","hollow_back"),
 ("S","Clean.",2.2,[0,0.1,1.1],[0,0,1.4],0.6,"a lowered blade, the next lamp lit down the causeway","Slice, blade lowered","Harp resumes, a half step higher","slice_back"),
 ("N","",3.2,[0,0,1.3],[0,0,1.05],1.0,"the next lamp, six legs of light, glowing on the far side",None,"Harmonics, fade","hollow_back")]),
"ch5_open": ("ch5_score", [
 ("N","A bare round hall. The circle is drawn on its floor at the Hollow's scale, and every wall is lined with identical forms.",6.0,[0,0,1.0],[0,0,1.3],0.6,"a bare round hall, a circle drawn on the floor, walls lined with identical forms, lamp-gold",None,"Held choir pad, no words","ring_back"),
 ("M","Order, read aloud: STRIKE. THIS. ADDRESS.",4.2,[0,0.3,1.2],[0,-0.1,1.1],0.8,"a lectern in a round hall, forms behind it","Mega Yo Bot at a lectern, reading evenly","Choir pad swells on each word","crest_back"),
 ("S","It's reading it out.",2.6,[0,0,1.3],[0.2,0,1.15],0.5,"the circle's edge on the floor","Slice, hand on the circle's edge","Pad holds","slice_back"),
 ("W","Then I'm reading mine. Pay up.",3.2,[0,0,1.4],[0,0,1.15],0.6,"the RETURN TO SENDER stamp held up to the lamplight","Wallit holding the stamp up","Pad drops; one dry strike of a wooden block","stamp_back"),
 ("W","It's not signed.",2.4,[0,0,1.5],[0,0,1.2],0.4,"a form with a blank signature line",None,"Silence","stamp_back")]),
"ch5_close": ("ch5_score", [
 ("R","Register... closed... uploading... nothing?",3.8,[0,0,1.05],[0,0,1.2],0.6,"the Registrar, gold trim cracked, ledger open on its knees, every page blank","the Registrar","Pad drops to a single held note","crest_back"),
 ("W","Struck from the record.",3.0,[0,0,1.5],[0,0,1.3],0.5,"the stamp pressed on the final page, DENIED appearing in ink",None,"Wooden block, one strike","stamp_back"),
 ("S","Clean.",2.2,[-0.4,0,1.1],[0.4,0,1.1],0.5,"the white walls, then the door now open","Slice reading the white walls","Pad warms by a third","slice_back"),
 ("W","Then we answer it. Pay up.",3.4,[0,0,1.4],[0,0,1.05],0.9,"Wallit and Slice side by side at the open white door","Wallit with the bat on his shoulder","Brass stab, then silence","wallet_front")]),
"ch6_open": ("ch6_score", [
 ("N","The Moonlit Hollow, changed. The wall of the Department is a row of broken stumps, and a path of pale slabs runs out to nothing.",6.0,[0,0.5,1.1],[0,-0.3,1.3],0.6,"the Hollow at dawn-blue, the Department wall a row of broken stumps, a pale slab path to nothing",None,"Cello pedal from Chapter 1, now with a third above it","hollow_back"),
 ("S","The wall is gone. The circle is brighter.",3.4,[0.3,0,1.15],[-0.2,0,1.3],0.5,"the ring glowing white at dawn","Slice at the ring, looking to where the wall was","Pedal holds","ring_back"),
 ("W","Then let's file it. Pay up.",3.0,[0,0,1.3],[0,0,1.1],0.8,"low angle, dawn sky","Wallit swinging the bat once, grinning","Pulse enters, bright","hollow_back"),
 ("N","",3.5,[0,0,1.05],[0,0,1.4],1.0,"a great gold seal rising at the far end of the pale path",None,"Brass note, held","stamp_back")]),
"ch6_close": ("ch6_score", [
 ("N","The Spider Bot comes apart. The order drops.",4.0,[0,-0.3,1.1],[0,0.4,1.2],0.4,"the Order-Bearer collapsing at the white doorframe, a page floating down in dawn light","the Order-Bearer, split","All silent; a held breath","hollow_back"),
 ("W","'Strike this address.' Stamped DENIED. Ha.",4.0,[0.2,0,1.3],[0,0,1.4],0.6,"the order in his hands, the stamp falling on it, DENIED","Wallit over the shoulder","Wooden block, then a bell","stamp_back"),
 ("S","Filed. Properly, this time.",3.4,[0,0,1.3],[0,0,1.05],0.8,"top-down, a scalpel writing a door number across the ring","Slice's hand and scalpel","Strings reply, full and warm","ring_back"),
 ("W","So we exist.",2.6,[0,0,1.4],[0,0,1.2],0.9,"the number and the ring glowing under his feet","Wallit looking down","Chord rises","ring_back"),
 ("S","Clean. Good swing, Wallit.",3.4,[0,0,1.6],[0,0,1.0],1.0,"two small figures in a gold ring at dawn, title card ADDRESS FILED",None,"Resolve; ring the bell once","ring_back"),
 ("W","Pay up.",2.4,[0,0,1.0],[0,0,1.0],0.3,"black, a faint gold edge",None,"Silence; then fade into the credits theme","stamp_back")]),
"boss_courier": ("ch1_score", [
 ("Y","Special delivery! Notice of striking!",3.4,[0,0.2,1.15],[0,0,1.35],0.9,"the Courier on the Hollow's rim, clipboard raised, red seal","the Courier, clipboard up","Cello pedal, a bright rising figure","hollow_back"),
 ("S","The Courier. It reads the order out, then it lands.",4.2,[0.2,0,1.3],[-0.1,0,1.1],0.6,"the circle, a shadow on the grass","Slice, knife low","Pedal holds","slice_back"),
 ("W","Then it can read me the bill.",3.0,[0,0,1.2],[0,0,1.35],0.8,"the clipboard shadow crossing the ring","Wallit, bat on shoulder","Bright pulse enters","wallet_front")]),
"boss_foreman": ("ch3_score", [
 ("F","Order, read aloud: STRIKE. THIS. ADDRESS.",4.0,[0,0.2,1.1],[0,-0.1,1.3],0.9,"the Foreman rising over the belt, lamps red behind it","the Foreman, order sheet on its chest","Pulse doubles, brass under it","crest_back"),
 ("S","The Foreman. It reads the order to the Crew. Don't let it finish.",4.6,[0.1,0,1.2],[-0.2,0,1.1],0.6,"rows of hooked chassis behind her","Slice, eyes on the belt","Drone, pulse low","slice_back"),
 ("W","Then I'll read it the invoice.",3.0,[0,0,1.2],[0,0,1.4],0.8,"the belt lamps","Wallit hefting the bat","Brass hit, cut","wallet_front")]),
"boss_server": ("ch4_score", [
 ("Y","Order delivery! Signature required!",3.2,[0,0,1.2],[0,0,1.4],0.8,"the Servers' Dais at the end of the causeway, a Spider Bot standing very still","the Server, paper held out","A polite minor figure on harp","crest_back"),
 ("S","The Server. It will not strike until it has offered the paper three times and been refused three times.",6.0,[0.2,0,1.25],[-0.1,0,1.1],0.5,"mist and lamp stilts","Slice in profile","Harp holds, harmonics","slice_back"),
 ("W","Refused. I can do refused. Free of charge.",3.6,[0,0,1.2],[0,0,1.4],0.8,"a paper hanging in the air","Wallit grinning, bat up","Harp stops; a low bell","wallet_front")]),
"boss_order": ("ch6_score", [
 ("Y","Order delivery! Signature required!",3.2,[0,0.3,1.1],[0,0,1.4],0.9,"the white doorframe on its dais at dawn, a Spider Bot carrying a scroll with a red seal","the Order-Bearer, scroll across its back","Brass note, held under the cello pedal","crest_back"),
 ("S","Spider. It carries the order.",2.8,[0,0,1.3],[0.2,0,1.1],0.6,"the ring at dawn","Slice, knife out","Pedal holds","slice_back"),
 ("O","Order, in force: STRIKE. THIS. ADDRESS.",4.0,[0,0,1.1],[0,0,1.4],0.9,"the red seal glowing on the scroll","the Order-Bearer","Brass swell, cut","stamp_back"),
 ("W","Then it carries my bill.",2.8,[0,0,1.25],[0,0,1.4],0.9,"Wallit squaring up","Wallit, bat on his shoulder","Pulse enters, full","wallet_front")]),
"ending_received": ("ch6_score", [
 ("N","Wallit sets six things down in the ring: a postcard, a key, a hat, a paper, a leaf, a stamp.",5.6,[0,0,1.5],[0,0,1.05],0.6,"top-down on the filed circle at dawn, six small things in a row inside it",None,"Cello pedal; the harp figure from Chapter 4, now resolved","ring_back"),
 ("S","No address.",2.4,[-0.3,0,1.2],[0.3,0,1.15],0.4,"the six things, the ring","Slice looking at the six things, the ring and Wallit","Pedal holds","slice_back"),
 ("W","Then give it one. You've got the stamp.",3.6,[0,0,1.4],[0,0,1.2],0.6,"close on the RETURN TO SENDER stamp lifted","Wallit lifting the stamp","Pedal lifts a step","stamp_back"),
 ("N","The stamp prints ADDRESS. Nothing strikes it. Every lost thing in the Hollow gets a door.",5.6,[0,0,1.3],[0,0,1.0],0.8,"the stamp coming down on the ring, ADDRESS, doors rising one by one",None,"Bells in a line, no pulse","ring_back"),
 ("Y","Delivery! ... Oh. It's for us?",3.4,[0,0,1.3],[0,0,1.1],0.7,"a new door glowing, soft focus","a small Yo Bot with a paper, puzzled","A single, kind chirp","crest_back"),
 ("S","Not this time. Received.",3.0,[0,0,1.1],[0,0,1.3],0.9,"Wallit and Slice in gold light, the stamp reading RECEIVED","Slice, a small smile only in her eyes","Full strings resolve","slice_back"),
 ("W","Received. In full. Pay up.",3.4,[0,0,1.25],[0,0,1.1],1.0,"the stamped ring","Wallit grinning, bat on his shoulder","Final bell; fade to the credits","wallet_front")]),
"credits": ("ch6_score", [
 ("N","WALLIT: one wallet, one bat. Pay up.",3.4,[0,0,1.05],[0,0,1.15],0.7,"the filed circle at dawn, wide",None,"Cello pedal, warm","ring_back"),
 ("N","SLICE: guards the circle. Clean.",3.2,[0,0,1.1],[0,0,1.2],0.7,"the circle, a knife standing in the grass",None,"Harp figure","slice_back"),
 ("N","YO BOTS: uploading victory. SPIDER BOTS: by order of nobody.",4.0,[0,0,1.1],[0,0,1.2],0.7,"a Yo Bot and a Spider Bot standing in a field of doors",None,"Strings lift","crest_back"),
 ("N","The circle stands. Slice and Wallit are on the books. Thanks for playing.",4.6,[0,0,1.2],[0,0,1.0],0.8,"wide dawn Hollow, a gold ring, two small figures",None,"Resolve, last bell","hollow_back")]),
})
SPEAKS = "KPHFYVROM"
# One generated keyframe per shot (Tools/Cinematics/keyframes.json: scene, place, cast; Resources/Cinematics/Art/<reel>_<n>.jpg).
# The keyframe is the whole picture, characters painted in from their Hawkes Gen sheets, so a shot has no front layer and
# no stand-in alt: the placeholders in Art are only for sample.json.
K = json.load(open(os.path.join(root, "Tools/Cinematics/keyframes.json")))
KEYS = dict((k["file"], k) for k in K["shots"])
def keyframe_prompt(k):
    scene = k["scene"][0].upper() + k["scene"][1:]
    text = scene + ". Setting: " + K["places"][k["place"]] + ". "
    if k["cast"]:
        refs = ["image %d is %s's character sheet (%s)" % (n + 1, K["sheets"][c]["name"], K["sheets"][c]["look"]) for n, c in enumerate(k["cast"])]
        text += "; ".join(refs) + ". " + K["castClause"] + " "
    return text + K["style"]
manifest = {"sheets": {}, "keyframes": [], "clips": [], "voices": [], "beds": {}, "sfx": ["stamp_land", "lantern_gutter", "belt_stop", "red_lamp_blink", "paper_slide", "ledger_close", "seal_crack", "door_open"]}
for rid, (bed, shots) in R.items():
    out = {"id": rid, "bed": bed, "shots": []}
    manifest["beds"].setdefault(bed, []).append(rid)
    for i, (sp, line, sec, fr, to, lv, bp, fp, cue, alt) in enumerate(shots, 1):
        sid = "s%d" % i
        key = KEYS["%s_%d" % (rid, i)]
        sh = {"id": sid, "back": key["file"], "speaker": sp, "seconds": sec, "fade": 0.0 if i == 1 and rid.endswith("open") is False else 0.7, "level": lv, "from": fr, "to": to, "cast": key["cast"]}
        if line:
            sh["line"] = line
        if sp in SPEAKS:
            manifest["voices"].append({"file": "Voice/cinema/%s_%s.mp3" % (rid, sid), "speaker": sp, "line": line})
        out["shots"].append(sh)
        entry = {"file": "Art/" + key["file"] + ".jpg", "tool": "image.paint" if key["cast"] else "image.generate", "cast": key["cast"], "prompt": keyframe_prompt(key), "music": cue}
        for field in ("job", "asset"):
            if key.get(field):
                entry[field] = key[field]
        manifest["keyframes"].append(entry)
        if i in (1, len(shots)):
            manifest["clips"].append({"from": "Art/" + key["file"] + ".jpg", "seconds": 5, "file": "StreamingAssets/Cinematics/%s_%d.mp4" % (rid, i)})
    json.dump(out, open(os.path.join(root, "Assets/Resources/Cinematics", rid + ".json"), "w"), indent=2)
for sid, sheet in K["sheets"].items():
    manifest["sheets"][sid] = dict((f, sheet[f]) for f in ("name", "look", "job", "asset") if sheet.get(f))
json.dump(manifest, open(os.path.join(root, "Tools/Cinematics/gen_manifest.json"), "w"), indent=2)
print(len(manifest["keyframes"]), "keyframes", len(manifest["sheets"]), "sheets", len(manifest["clips"]), "clips", len(manifest["voices"]), "voices")
