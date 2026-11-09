# Summary

Nothing new was generated this pass: all the art from the earlier passes is already committed on this branch. The last "failed" verdict was Studio's own "no build settings (products.json build)" error, which the PM said is a Studio config problem and not this change. Nothing is pending, and `.hawkes/pending-gpu.txt` does not exist.

Unity compile was not run here. CharacterValidation, KitValidation and the 150k/64 MB budgets were not run either (no Unity in this sandbox). They are judged by beta's `characters` and `levels-1-3` playtests.

## What is baked (Gen concept image -> mesh.textured -> `Tools/Bake/glb_bake.py` -> `Assets/Resources/Baked/<id>.mesh.txt`)

| Piece | Kind | Notes |
|---|---|---|
| courier, foreman, server, order_bearer | bosses | Rigid split by `Tools/Bake/specs/<id>.json` into Body/Head/ClawL/ClawR/LegL/LegR plus child parts (Cap, Pack, Hard Hat, scroll and so on). Bot part names and pivots are kept, so tells, `Enemy.AnimateModel` and the `_Flash` hit flash work as before. 7,000 to 8,000 triangles each, under the 12,000 boss budget. The code bodies are the fallback (`CharacterKit`). |
| of_lodger_tick, brim, chime, spare | lodgers | Baked. |
| gt_stamp_press_large, gt_turnstile, rd_great_ledger, yd_clock_tower | landmark props | Decimated on the existing kit textures. The stamp press is fitted exactly to 1.6x2.6x1.2 m and the clock tower to 4.6x13.3x4.6 m. |
| of_stamp_press, gt_gate_arch, gt_ticket_booth, of_pneumatic_tube, yd_notice_board | landmark props | Baked in earlier passes. |
| rd_lectern | landmark prop | Baked on this branch, per the PM's note. |

The other ~150 kit pieces stay code-built, so the budgets hold.

## Boss extents (m), Gen body vs code body
- courier: 1.12 x 1.30 x 1.15 vs about 1.2 x 1.3 x 0.9. The Gen pack is a bigger mailbox pack than the code jet pack.
- foreman: 1.26 x 1.45 x 0.59 vs about 1.3 x 1.45 x 0.6.
- server: 1.54 x 1.45 x 1.0 vs about 1.8 x 1.4.
- order_bearer: 1.23 x 1.20 x 1.13 vs about 1.3 x 1.1.

## Checks
The `characters` scenario shows the four bosses and four lodgers. Its events report each character's triangles and idle. `levels-1-3` photographs the props and reports `levelPrimitives` and visible triangles. The boss tells and hit flash are played by chapter1, chapter3, chapter4 and chapter6.

## For J to look at
Whether the Courier's bigger mailbox pack reads right in the gallery. I couldn't judge the Registrar and Plated Alpha art direction here; the gallery covers it.

No merge conflicts.

## This pass
No art made, no code edited, no GPU spent (`.hawkes/pending-gpu.txt` does not exist). The earlier branch `task/ce95a565362b-...` was gone from origin, but its tip `1336f88` was still fetchable by sha; I fast-forwarded this branch to it (10 commits over beta `a55847d`, no conflicts). Studio builds it and runs the `characters` playtest (named in `.hawkes/scenario.txt`). Unity compile and the validators were not run here. If the build fails with "no build settings (products.json build)", that is Studio's config, not this change.

## Attempt 2 (rebuild as is)
The worktree was restored on `task/94c4801cbd0d-using-the-method-proven-on-the-registrar` at `60144c1`. `origin/beta` has not moved (0 commits behind, 11 ahead), so there was nothing to merge and no conflicts. I changed no code, made no art and spent no GPU; `.hawkes/pending-gpu.txt` does not exist. Unity compile and the validators were not run here. Studio builds this branch and runs the `characters` playtest (named in `.hawkes/scenario.txt`). Only a compile error in `BakedMesh.cs`, `CharacterKit.cs`, `Enemy.cs` or `PlaytestCharacters.cs` is this change's to fix; a Gen 5xx, timeout or restart loop is infrastructure.
