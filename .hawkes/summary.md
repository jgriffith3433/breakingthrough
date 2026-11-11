# Phone load budget: cut the WebGL data file at the source

## What was wrong
`first-five@local:phone` on build 121 transferred **45.6 MB** before gameplay (limit 20) and took **234.7 s** (limit 20 s). Top asset: `PlaceDepartment.data.unityweb` at 38.5 MB.

Build 121’s `[WebGlBuild] Download size` log (Gen job `j_1ac3edd59beb`) shows OnDemand already moved 26.5 MB of campaign files out. What was still packing the data file:

| Folder / asset | Packed size |
|---|---|
| `Assets/Art/Arena` (mostly `ArenaScatter_*.asset` from `CombineMeshes`) | **41.3 MB** |
| `Assets/Resources/Voice` (all Wallit + Slice lines) | **18.7 MB** |
| Unity splash logo | 2.7 MB |
| `Assets/Art/BowAndArrow` (retired bow still referenced on `BowEquipment`) | 1.7 MB |

Brotli data file on disk: **36.7 MB** (+ wasm 6.4 MB ≈ the playtest’s 45.6 MB).

## What changed
1. **Arena scatter** (`BuildArena.cs`, new `ArenaScatterDraw.cs`): stop baking unique combined scatter meshes; draw shared FBX meshes with `Graphics.DrawMeshInstanced` (same counts/seed). `WebGlBuild.BuildWebGLTo` calls `BuildArena.RebuildForPlayerBuild()` before the player build so scene2 ships the light layout and clears bow model refs.
2. **Voices** (`OnDemandPack`, `WallitVoice`, `SliceVoice`, `OnDemand` comments): move Wallit/Slice lines **outside** the Arcade bark keep-set (`IsArcadeKeep`) to StreamingAssets/OnDemand; keep-set stays in Resources for the first swing. LoadClip uses OnDemand then Resources, with `FetchKey` when missing.
3. **Splash + scenes**: disable Unity splash logo; drop `KitPreview` from the WebGL scene list (editor gallery only).
4. **AGENTS.md**: download-size notes updated for the 20 MB / arena-instance / keep-set rules.

## How checked
- Root cause from build 121 Unity log size report (not guessed).
- Read-through for CS0102/CS0111 duplicates on new members; call sites match.
- **Unity compile not run here** (no editor in this sandbox; binary assets stripped). Studio’s cloud build must run `WebGlBuild.CloudBuild` and read `build-report.txt`: expect `Assets/Art/Arena` far below 41 MB and `Resources/Voice` near the keep-set only; first download (data+wasm+loader) should be ≤ ~20 MB transferred.
- Conflicts: none.

## Reviewer verdict (self-pass)
**Merge** once the cloud build’s size report confirms the cut. Cause matches the finding’s assets; scope stays on first-download weight; living-world scatter density unchanged (instanced, not removed). Residual risk: only a measured build proves ≤20 MB / ≤20 s on Slow 4G.

## Fun / living-world
Same rim forest and field grass/flower counts; drawn via instancing instead of combined mesh assets. Not a demo thinning.
