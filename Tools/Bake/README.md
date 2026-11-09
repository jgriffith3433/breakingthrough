# Bake (Gen GLB -> game mesh), no Blender, no Unity

`python3 -I Tools/Bake/glb_bake.py in.glb Assets/Resources/Baked/<id>.mesh.txt --tris 1500 --size X,Y,Z`

- Props and lodgers (id = the kit piece id, e.g. `of_stamp_press`): one part, kit texture from the piece's palette/main set
  (`KitCatalog.MeshOf` uses the baked mesh when the file exists and the code piece otherwise). `--size` is the code piece's
  bounding box so footprints and placement stay the same.
- Bosses (id = `courier`, `foreman`, `server`, `order_bearer`): `--height` plus `--parts spec.json`, a rigid split by region.
  `{"default":"Body","parts":[{"name":"Head","box":[x0,y0,z0,x1,y1,z1],"pivot":[x,y,z],"set":"cloth","color":"#c73"}]}`
  Boxes are fractions of the model's half-width, height and half-depth (x, y, z), pivots are metres in rig space. `CharacterKit.Make`
  uses the baked parts when every required part name exists, else the code body.
- Output is text so no Unity import step can break it; the tool prints triangles per part.
- Settings: `BakedMesh.Enabled = false` forces every code fallback.

## Flags and the specs in `specs/`

- `--yaw <deg>`: a single-picture Gen mesh comes out turned (the concept was a 3/4 view). Try yaws and keep the one with the
  smallest footprint (the Courier is 23.6, the stamp press 28, the clock tower 28, the Order-Bearer 22.8).
- `--exact` with `--size x,y,z`: stretch each axis to the code piece's box (a mild squash) instead of one uniform scale.
- `--set <kit texture set>`: the set of a one-part prop (iron, brick, cloth, wood, metal...).
- `--drop-small 0.01`: cut floating pieces. `--trim-base`: cut the picture's ground-shadow slab off the foot (Gen keeps it as
  geometry; it made the clock tower 6.8 m wide instead of 4.6). `--upright`: shear a leaning tall piece straight.
- Spec parts: `"metres": true` makes the box rig-space metres. `"parent": "Head"` makes a part a child (a cap on a head, the
  scroll on the body) so the parent's turn carries it; its pivot stays in rig space. A part named `_x` only cuts loose bits away.
  The same name may be listed twice (two boxes for one leg). The first matching box wins; `Body` is the default.
- Bosses need no props in the file: the Courier's clipboard, the Server's paper are added in code. The Foreman's baked body
  carries its own `Order Sheet` part (`Enemy.MakeForeman` adds the code sheet only when there is none).
- Mesh source: Gen `image.generate` (one character, plain pose, plain grey ground) -> `mesh.textured` with
  `allowSingleView` (the 4-view `image.viewsheet` check rejects asymmetric props and these characters; `mesh.views3d` hit HTTP
  413) -> `Tools/Bake/in/<id>.glb` -> this tool. Textures are the kit's, tinted per part, never Gen's.
