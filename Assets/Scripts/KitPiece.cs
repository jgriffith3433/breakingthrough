using UnityEngine;

// Marks a kit prefab (Docs/ModelKit.md): which piece it is, its palette, and its footprint in cells of KitCatalog.Grid (0.5 m).
// Placement code multiplies cells by the grid and turns pieces in 90 degree steps so they snap without gaps.
public class KitPiece : MonoBehaviour {
    public string Id;
    public string Palette;
    public Vector2Int Footprint;
}
