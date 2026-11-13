# Materials: the painted PBR sets

Eight tiling sets painted by Hawkes Gen (`image.paint` with `seamless`, then `texture.maps` for the normal and roughness), 512 px, in the
game's painted storybook style. Code: `PbrKit.cs` (loading, noise, masks, hub overrides), `HollowGround.cs`, shader
`Resources/Shaders/PbrSplat.shader`, import `Assets/Editor/PbrImportRules.cs`, playtest `PlaytestMaterials.cs` (`materials`).

| Set | Where | Files |
|---|---|---|
| grass (moonlit lawn), dirt (worn path), cobble | Moonlit Hollow ground: grass everywhere, the four dirt lanes the flock walks in on (they lead to Slice's circle), a cobble apron round the circle that the lanes turn into for their last 24 m | `Resources/Pbr/hollow/<set>_a.png`, `_n.png` (first download) |
| brick, wood, linoleum, carpet, rust | Hub office: brick for the plaster walls, painted boards, rusted metal cabinets and doors, carpet rug (all through `KitTextures.Get("office", …)`); the floor is a splat of boards (aisle), linoleum (along both walls) and carpet (in front of the six doors) | `Resources/Pbr/hub/<set>_a.png`, `_n.png` (on demand, group `core`) |

`_a.png` is the albedo with **roughness in alpha**; `_n.png` is an OpenGL (+Y) normal map read as plain RGB. AO is not a map: `PbrSplat` derives a
cheap cavity term from the albedo (the kit's vertex colour is the baked AO on props).

## How the ground is built
- `Knoll/PbrSplat` blends three layers by a mask texture read in world space (R: layer 1, G: layer 2). `HollowGround` builds the mask at load by
  repeating `BuildArena`'s lane maths (same seed, same wobble), so the paths lie where the scene's old dirt colour did. The ground mesh, its collider
  and its vertex colours stay (the colours still shade the rim and the rock). Missing files keep the old ground.
- **Texel density** is one number: every layer tiles at 3 m per 512 px (170 px/m).
- **No visible repeat:** a warp of the sample position, a macro brightness/hue field at about 75 m, a second sample of each layer at another scale and
  angle (full tier only), and ragged blend edges from the noise. A detail normal at 0.6 m adds grain up close.
- **Phone tier** (`UiFit.Touch`): `_Lite` skips the second samples and the detail normal, `MobileQuality` drops the top mip and anisotropy.

## Memory (limit 12 MB)
Hollow: six textures, ASTC on web and phones (6x6 albedo, 5x5 normal), about 0.15 MB each with mips; plus the noise (0.35 MB) and a 384 px mask
(0.6 MB). Hub: fetched as PNG and rebuilt with mips at runtime (albedo 512, normal 256: about 8.75 MB for the five sets); the hub and the Hollow are
never loaded together. `PbrKit.Bytes()` reports the sum; the `materials` playtest logs it (`pbrTextureMb`).
