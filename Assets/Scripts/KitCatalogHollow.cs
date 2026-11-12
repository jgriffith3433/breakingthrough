using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 6: the Dead Letter Office ruin and the Hollow's edge dressing (Docs/ModelKit.md, "Arcade's Hollow").
// Authored from KitMesh like every other piece, at the size they are used (the office pieces are made to be placed at scale 2):
//   landmark: the arched front wall, the broken upper storey (tilted when placed), the handless clock face, sorting shelves
//   edge:     hedge run, picket run (one submesh each: they are drawn instanced around the rim), parcels, paper drift
public static partial class KitCatalog {
    static void AddHollow() {
        Add("hl_office_front", "yard", "brick", "stone", m => {
            var door = new KitMesh.Opening(-1.4f, 1.4f, 0f, 4.2f, true);
            var winL = new KitMesh.Opening(-6.2f, -3.8f, 2.0f, 5.2f, true);
            var winR = new KitMesh.Opening(3.8f, 6.2f, 2.0f, 5.2f, true);
            m.Wall(16f, 7f, 0.8f, new[] { door, winL, winR });
            m.Frame(door, 0.8f, 0.3f, 0.12f, 0f);
            m.Frame(winL, 0.8f, 0.22f, 0.1f, 0.15f);
            m.Frame(winR, 0.8f, 0.22f, 0.1f, 0.15f);
            S1(m);
            B(m, -8.2f, 6.7f, -0.55f, 8.2f, 7.1f, 0.55f, 0.06f);          // cornice
            B(m, -2.2f, 0f, 0.4f, 2.2f, 0.25f, 1.6f, 0.04f);              // doorstep
            B(m, -2.0f, 0.25f, 0.9f, 2.0f, 0.45f, 1.7f, 0.04f);
            S0(m);
            B(m, -7.6f, 0f, -3.4f, 7.6f, 6.2f, -3.2f, 0.03f);              // the back wall, so the doorway shows a room
            B(m, -7.6f, 0f, -3.4f, -7.4f, 5.4f, 0f, 0.03f);
            B(m, 7.4f, 0f, -3.4f, 7.6f, 3.6f, 0f, 0.03f);
        });
        Add("hl_office_upper", "yard", "brick", "stone", m => {
            m.Wall(16f, 4.6f, 0.8f, new KitMesh.Opening[0]);   // blank: the clock hangs on it
            float[] hs = { 0.9f, 0.3f, 1.4f, 0.5f, 0.1f, 1.1f, 0.6f, 1.6f, 0.2f, 0.8f };
            for (int i = 0; i < hs.Length; i++) {
                float x = -8f + i * 1.6f;
                B(m, x + 0.05f, 4.6f, -0.4f, x + 1.55f, 4.6f + hs[i], 0.4f, 0.04f);   // broken parapet teeth
            }
        });
        Add("hl_clock_face", "yard", "stone", "lamp", m => {
            m.Orient(V(0, 2.4f, 0), V(90f, 0f, 0f));
            m.Lathe(P(0, 0, 2.3f, 0, 2.3f, .3f, 0, .3f), 20, false);
            m.Reset();
            S1(m);
            m.Orient(V(0, 2.4f, 0), V(90f, 0f, 0f));
            m.Lathe(P(0, .36f, 1.95f, .36f, 1.95f, .3f), 20, false);   // the face: lit, and no hands
            m.Reset();
            S0(m);
            for (int k = 0; k < 12; k++) {
                float a = k * 30f;
                float s = Mathf.Sin(a * Mathf.Deg2Rad), c = Mathf.Cos(a * Mathf.Deg2Rad);
                BR(m, V(s * 1.55f, 2.4f + c * 1.55f, .4f), V(0, 0, -a), V(.12f, k % 3 == 0 ? .42f : .26f, .06f), .008f);
            }
            B(m, -.18f, 2.22f, .3f, .18f, 2.58f, .5f, .02f);   // the pin the hands came off
        });
        Add("hl_shelf", "yard", "wood", "paper", m => {
            B(m, -2f, 0f, -.4f, -1.9f, 3f, .4f, .01f);
            B(m, 1.9f, 0f, -.4f, 2f, 3f, .4f, .01f);
            for (int i = 0; i <= 4; i++) {
                B(m, -2f, .05f + i * .72f, -.4f, 2f, .1f + i * .72f, .4f, .008f);
            }
            for (int c = 1; c < 5; c++) {
                B(m, -2f + c * .8f - .03f, .1f, -.4f, -2f + c * .8f + .03f, 2.95f, .4f, .006f);
            }
            B(m, -2f, 0f, -.42f, 2f, 3f, -.38f, .006f);
            S1(m);
            for (int i = 0; i < 9; i++) {
                float x = -1.7f + (i % 5) * .8f;
                float y = .1f + (i % 4) * .72f;
                BR(m, V(x, y + .12f, .3f + (i % 3) * .08f), V(0, 0, (i * 37) % 30 - 15), V(.4f, .26f, .05f), .004f);
            }
            for (int i = 0; i < 4; i++) {   // spilled onto the floor
                BR(m, V(-1.2f + i * .8f, .03f, .9f + i * .15f), V(0, i * 53f, 0), V(.4f, .05f, .26f), .004f);
            }
        });
        Add("hl_hedge", "yard", "grass", "grass", m => {
            B(m, -2f, 0f, -.5f, 2f, .35f, .5f, .05f);
            for (int i = 0; i < 4; i++) {
                float x = -1.5f + i;
                m.At(V(x, 0, 0), i * 41f);
                m.Lathe(P(0, .1f, .62f, .15f, .72f, .6f, .6f, 1.0f, .3f, 1.28f, 0, 1.34f), 8, true);
                m.Reset();
            }
        });
        Add("hl_picket", "yard", "wood", "wood", m => {
            for (int i = 0; i < 3; i++) {
                float x = -1.9f + i * 1.9f;
                B(m, x - .06f, 0f, -.06f, x + .06f, 1.1f, .06f, .008f);
            }
            B(m, -2f, .25f, .06f, 2f, .33f, .1f, .005f);
            B(m, -2f, .72f, .06f, 2f, .8f, .1f, .005f);
            for (int i = 0; i < 16; i++) {
                if (i == 6 || i == 11) {
                    continue;   // missing boards
                }
                float x = -1.85f + i * .245f;
                float lean = (i % 3 - 1) * .015f;
                var pick = new List<Vector2> { new Vector2(x - .055f, .05f), new Vector2(x + .055f, .05f), new Vector2(x + .055f + lean, .95f), new Vector2(x + lean, 1.03f), new Vector2(x - .055f + lean, .95f) };
                m.ExtrudeZ(pick, .1f, .13f);
            }
        });
        Add("hl_parcel", "yard", "paper", "cloth", m => {
            B(m, -.5f, 0f, -.35f, .5f, .55f, .35f, .03f);
            B(m, -.52f, 0f, -.04f, .52f, .56f, .04f, .01f);
            S1(m);
            B(m, -.02f, .55f, -.37f, .02f, .58f, .37f, .004f);
            S0(m);
            BR(m, V(.9f, .24f, .2f), V(0, 24f, 0), V(.7f, .46f, .5f), .03f);
            BR(m, V(.05f, .78f, .05f), V(0, -14f, 0), V(.55f, .4f, .4f), .03f);
        });
        Add("hl_paper_drift", "yard", "paper", "paper", m => {
            for (int i = 0; i < 9; i++) {
                float a = i * 47f;
                float r = .25f + (i % 4) * .28f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * r, .045f + (i % 3) * .012f, Mathf.Sin(a * Mathf.Deg2Rad) * r), V((i % 3 - 1) * 4f, a * 1.3f, (i % 2) * 5f - 2f), V(.42f, .014f, .3f), .003f);
            }
        });
    }
}
