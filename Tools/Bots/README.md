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
