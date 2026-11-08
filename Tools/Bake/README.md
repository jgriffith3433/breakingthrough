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
