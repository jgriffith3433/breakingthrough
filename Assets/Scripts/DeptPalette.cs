using UnityEngine;

// The painted cards' palette (chapter title cards, load cards): warm cream paper, stamp red, ink and brass. HUD text and the
// 3D grade both read from here so the cards and the world look like one game. No cyan or icy blue in text.
public static class DeptPalette {
    public static readonly Color Cream = new Color(0.96f, 0.91f, 0.78f);      // body text on dark
    public static readonly Color CreamSoft = new Color(0.90f, 0.84f, 0.70f);  // quieter lines
    public static readonly Color Red = new Color(0.82f, 0.22f, 0.17f);        // stamp red: bars, warnings
    public static readonly Color RedLight = new Color(0.96f, 0.45f, 0.36f);   // red text on dark
    public static readonly Color Brass = new Color(0.95f, 0.78f, 0.36f);      // coins, Wallit
    public static readonly Color Ink = new Color(0.07f, 0.06f, 0.07f, 0.88f); // bar backs

    // Grade: shadows lean plum-ink, not blue; highlights lean cream. Used as the base for the night looks.
    public static readonly Color GradeShadows = new Color(0.97f, 0.9f, 0.96f);
    public static readonly Color GradeHighlights = new Color(1.12f, 1.03f, 0.88f);
    public static readonly Color GradeLift = new Color(0.016f, 0.008f, 0.012f);
}
