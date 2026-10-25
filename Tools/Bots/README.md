# Bot skins

`Assets/Resources/Models/Bots/YoBotSkin.png` and `SpiderBotSkin.png` are the bots' 1K textures (RGB colour,
alpha = glow mask for `Knoll/BotSkin`). Gen's own bake in `YoBot.fbx` / `SpiderBot.fbx` came out almost black, so
these are painted here by projecting each bot's DoD reference along the mesh's front axis (Blender -Y, the view Gen
built the mesh from). The Spider Bot's glass front with the pilot and consoles is painted on its +X side, which
`BotArt` turns to lead.

    python3 Tools/Bots/paint.py Assets/Resources/Models/Bots/YoBot.fbx ../AllTheCharacters/YoBot/anims/_reference.png Assets/Resources/Models/Bots/YoBotSkin.png yo
    python3 Tools/Bots/paint.py Assets/Resources/Models/Bots/SpiderBot.fbx ../AllTheCharacters/SpiderBot/anims/_side.png Assets/Resources/Models/Bots/SpiderBotSkin.png spider
    python3 Tools/Bots/render.py Assets/Resources/Models/Bots/SpiderBot.fbx Assets/Resources/Models/Bots/SpiderBotSkin.png /tmp/spider.png

Plain Python 3 (no numpy or PIL). Repaint whenever the FBX is replaced: the paint follows its UVs.

## Checking and cleaning (no Unity, Blender or GPU)

    python3 Tools/Bots/validate.py            # FBX part names (what Enemy.BindParts binds), degenerate tris, UV range
                                              # and stretch, feet on the ground, skin size, white blobs, glow bleed
    python3 Tools/Bots/tame.py Assets/Resources/Models/Bots/SpiderBotSkin.png
                                              # turns near-white texels pale cyan and caps their glow; run it after paint.py
    python3 Tools/Bots/render.py <Bot.fbx> <Skin.png> /tmp/bot.png   # front, +X, -X, back and three-quarter views

`validate.py` exits 1 on any FAIL. `Knoll/BotSkin` also caps emission, so a bright texel cannot burn to white.
Part names, the `Front` marker and `BotKind` order are untouched; the validator fails if a part name changes.
