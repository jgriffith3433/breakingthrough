using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 5: the hub office (Docs/Levels4to6AndHub.md, sections 2 and 9). Walls in bays with wainscot and cornice, window
// bays, six themed door bays with their swinging leaves, the chapter board, the four lodgers, the hanging lamps and the small things
// the office is dressed with. Every piece is authored from KitMesh like the rest; none is a primitive or a recoloured copy.
// Wall pieces face +Z (the office places them turned to face the room). The door leaves hinge at x = 0 and carry their trim on -Z.
public static partial class KitCatalog {
    static void AddLate() {
        AddOfficeWalls();
        AddOfficeDoors();
        AddOfficeThings();
        AddOfficeLodgers();
        AddWalkLate();
        AddReadingLate();
        AddFiledLate();
        AddArenaLate();
    }

    // Wainscot panels, dado rail, skirting, picture rail and cornice on the +Z face of a wall of width w and height h.
    static void WallTrim(KitMesh m, float w, float h, int sub, bool panels = true) {
        m.Sub = sub;
        float x0 = -w * .5f + .03f, x1 = w * .5f - .03f;
        int n = Mathf.Max(1, Mathf.RoundToInt(w / .75f));
        float pw = (x1 - x0) / n;
        if (panels) {
            for (int i = 0; i < n; i++) {
                B(m, x0 + i * pw + .04f, .14f, .125f, x0 + (i + 1) * pw - .04f, .92f, .15f, .008f);
            }
        }
        B(m, x0, .92f, .125f, x1, 1.0f, .18f, .008f);
        B(m, x0, 0f, .125f, x1, .12f, .17f, .008f);
        B(m, x0, h - .75f, .125f, x1, h - .69f, .16f, .006f);
        B(m, -w * .5f - .02f, h - .22f, -.18f, w * .5f + .02f, h, .2f, .012f);
        m.Sub = 0;
    }

    // A door bay wall 4.6 m wide: the opening, and a dark "beyond" behind it.
    static void DoorWall(KitMesh m, KitMesh.Opening hole) {
        m.Wall(4.6f, 5.2f, .3f, new[] { hole });
        B(m, hole.X0, 0f, -.24f, hole.X1, hole.Y1, -.19f, .004f);
    }

    static void AddOfficeWalls() {
        Add("of_wall_bay", "office", "plaster", "wood", m => {
            m.Wall(3f, 5f, .25f, new KitMesh.Opening[0]);
            WallTrim(m, 3f, 5f, 1);
            S1(m);
            B(m, -1.2f, 1.7f, .125f, 1.2f, 2.9f, .14f, .004f);       // a hung frame's backing, bare: the office hangs notices here
            B(m, -1.24f, 1.66f, .125f, -1.2f, 2.94f, .17f, .004f);
            B(m, 1.2f, 1.66f, .125f, 1.24f, 2.94f, .17f, .004f);
            B(m, -1.24f, 1.66f, .125f, 1.24f, 1.7f, .17f, .004f);
            B(m, -1.24f, 2.9f, .125f, 1.24f, 2.94f, .17f, .004f);
        });
        Add("of_wall_window", "office", "plaster", "lamp", m => {
            var win = new KitMesh.Opening(-.72f, .72f, 1.1f, 3.9f);
            m.Wall(3f, 5f, .25f, new[] { win });
            m.Frame(win, .25f, .11f, .06f, .14f);
            m.Mullions(win, 2, 3, .05f);
            B(m, -.9f, 3.98f, .125f, .9f, 4.12f, .22f, .01f);          // lintel hood
            B(m, -.82f, 4.12f, .125f, .82f, 4.2f, .2f, .008f);
            B(m, -.85f, .92f, .125f, .85f, 1.02f, .3f, .01f);          // window seat
            WallTrim(m, 3f, 5f, 0, false);
            for (int i = 0; i < 3; i++) {
                float x0 = i == 0 ? -1.45f : .8f + (i - 1) * .35f;
                float x1 = i == 0 ? -.82f : x0 + .3f;
                B(m, x0, .14f, .125f, x1, .9f, .15f, .008f);
            }
            m.Pane(win);
        });
        Add("of_wall_corner", "office", "plaster", "wood", m => {
            m.Wall(1.4f, 5f, .25f, new KitMesh.Opening[0]);
            WallTrim(m, 1.4f, 5f, 1);
            B(m, -.6f, .1f, .125f, .6f, 4.4f, .2f, .02f);
            B(m, -.66f, 4.4f, .125f, .66f, 4.6f, .24f, .012f);
        });
        Add("of_pilaster", "office", "plaster", "brass", m => {
            B(m, -.32f, 0f, -.1f, .32f, .28f, .3f, .02f);
            B(m, -.24f, .28f, -.04f, .24f, 4.55f, .22f, .015f);
            B(m, -.3f, 4.55f, -.1f, .3f, 4.75f, .28f, .015f);
            B(m, -.36f, 4.75f, -.12f, .36f, 5f, .3f, .015f);
            S1(m);
            B(m, -.26f, 1.0f, -.04f, .26f, 1.08f, .24f, .006f);
            B(m, -.26f, 4.4f, -.04f, .26f, 4.48f, .24f, .006f);
            for (int i = 0; i < 3; i++) {
                B(m, -.2f + i * .14f, 1.4f, .22f, -.12f + i * .14f, 4.0f, .24f, .004f);
            }
        });
        Add("of_skyline_view", "office", "brick", "lamp", m => {
            float[] w = { 5f, 4f, 6f, 3.5f, 5.5f, 4.5f, 6.5f, 4f };
            float[] h = { 11f, 17f, 9f, 24f, 13f, 19f, 10f, 15f };
            float x = -22f;
            for (int i = 0; i < w.Length; i++) {
                B(m, x, 0f, 0f, x + w[i], h[i], 1.6f, 0f);
                if (i % 2 == 0) {
                    m.At(V(x + w[i] * .5f, h[i], 0f), 0f);
                    m.ExtrudeZ(P(-w[i] * .55f, 0f, w[i] * .55f, 0f, 0f, w[i] * .38f), 0f, 1.6f);
                    Rst(m);
                } else {
                    B(m, x - .1f, h[i], -.1f, x + w[i] + .1f, h[i] + .5f, 1.7f, 0f);
                }
                x += w[i];
            }
            S1(m);
            x = -22f;
            for (int i = 0; i < w.Length; i++) {
                int cols = Mathf.Max(1, Mathf.FloorToInt(w[i] / 1.6f));
                int rows = Mathf.Max(1, Mathf.FloorToInt((h[i] - 2f) / 2.4f));
                for (int c = 0; c < cols; c++) {
                    for (int r = 0; r < rows; r++) {
                        if (((i * 7 + c * 3 + r * 5) % 3) == 0) {
                            continue;
                        }
                        float wx = x + .5f + c * 1.5f;
                        B(m, wx, 1.6f + r * 2.3f, 1.6f, wx + .6f, 2.4f + r * 2.3f, 1.66f, 0f);
                    }
                }
                x += w[i];
            }
        });
    }

    static void AddOfficeDoors() {
        // 1 Return to Sender: the post-office door, brick quoins, a brass letter slot and a RETURN plate.
        Add("of_door_frame_1", "office", "brick", "brass", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 3.25f);
            DoorWall(m, hole);
            m.Frame(hole, .3f, .14f, .07f, 0f);
            for (int j = 0; j < 8; j++) {
                float w = j % 2 == 0 ? .4f : .28f;
                B(m, 1.3f, j * .45f, .15f, 1.3f + w, j * .45f + .4f, .24f, .01f);
                B(m, -1.3f - w, j * .45f, .15f, -1.3f, j * .45f + .4f, .24f, .01f);
            }
            S1(m);
            B(m, -.6f, 3.55f, .22f, .6f, 3.7f, .27f, .008f);
            B(m, -.95f, 3.95f, .2f, .95f, 4.4f, .25f, .01f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .85f - .03f, 4.1f, .25f, s * .85f + .03f, 4.24f, .28f, .004f);
            }
        });
        // 2 No Fixed Address: a civic arch, pale pilasters, a pediment and the crossed INVALID bar over the lintel.
        Add("of_door_frame_2", "office", "stone", "iron", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 3.25f);
            DoorWall(m, hole);
            m.Frame(hole, .3f, .12f, .08f, 0f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.55f - .2f, 0f, .15f, s * 1.55f + .2f, 3.4f, .4f, .02f);
                B(m, s * 1.55f - .28f, 3.4f, .15f, s * 1.55f + .28f, 3.6f, .46f, .015f);
            }
            m.At(V(0f, 3.6f, 0f), 0f);
            m.ExtrudeZ(P(-1.9f, 0f, 1.9f, 0f, 0f, .85f), .14f, .3f);
            Rst(m);
            S1(m);
            BRk(m, V(0f, 3.95f, .34f), V(0f, 0f, 24f), V(3.1f, .13f, .05f), .005f);
            BRk(m, V(0f, 3.95f, .34f), V(0f, 0f, -24f), V(3.1f, .13f, .05f), .005f);
            B(m, -.12f, 3.83f, .34f, .12f, 4.07f, .4f, .006f);
        });
        // 3 The Clearing Floor: a riveted steel hatch frame with a caged lamp.
        Add("of_door_frame_3", "office", "metal", "lamp", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 3.25f);
            DoorWall(m, hole);
            m.Frame(hole, .3f, .18f, .09f, 0f);
            for (int i = 0; i < 8; i++) {
                float y = .3f + i * .4f;
                B(m, -1.32f, y, .22f, -1.24f, y + .08f, .27f, .004f);
                B(m, 1.24f, y, .22f, 1.32f, y + .08f, .27f, .004f);
            }
            for (int i = 0; i < 6; i++) {
                float x = -.9f + i * .36f;
                B(m, x, 3.5f, .2f, x + .08f, 3.58f, .25f, .004f);
            }
            for (int k = 0; k < 6; k++) {
                float a = k * 60f * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(a) * .2f, 4.05f, .34f + Mathf.Sin(a) * .0f), V(0, 0, 0), V(.025f, .5f, .025f), .004f);
            }
            B(m, -.3f, 3.62f, .15f, .3f, 3.7f, .45f, .01f);
            B(m, -.3f, 4.4f, .15f, .3f, 4.48f, .45f, .01f);
            S1(m);
            Lk(m, V(0f, 3.78f, .3f), 0f, P(0, 0, .16f, .02f, .2f, .2f, .14f, .4f, 0, .44f), 8);
        });
        // 4 The Process Servers: a tall narrow iron frame crowned with a folded-leg lamp head.
        Add("of_door_frame_4", "office", "iron", "lamp", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 3.25f);
            DoorWall(m, hole);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.17f - .08f, 0f, .15f, s * 1.17f + .08f, 4.6f, .35f, .01f);
                Lk(m, V(s * 1.17f, 4.6f, .25f), 0f, P(0, 0, .12f, 0, .08f, .1f, .03f, .5f, 0, .6f), 6);
            }
            B(m, -1.25f, 3.25f, .15f, 1.25f, 3.4f, .3f, .01f);
            for (int i = 0; i < 5; i++) {
                float x = -.8f + i * .4f;
                B(m, x, 3.4f, .18f, x + .03f, 4.3f, .22f, .003f);
            }
            for (int k = 0; k < 3; k++) {
                float a = k * 120f * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(a) * .2f, 4.7f, .3f + Mathf.Sin(a) * .2f), V(Mathf.Sin(a) * 14f, 0, -Mathf.Cos(a) * 14f), V(.04f, .5f, .04f), .004f);
            }
            S1(m);
            Lk(m, V(0f, 4.3f, .3f), 0f, P(0, 0, .16f, .02f, .22f, .2f, .16f, .42f, 0, .5f), 8);
        });
        // 5 Strike This Address: the paper door, forms pinned all round it.
        Add("of_door_frame_5", "office", "paper", "wood", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 3.25f);
            DoorWall(m, hole);
            for (int s = -1; s <= 1; s += 2) {
                for (int r = 0; r < 3; r++) {
                    float x0 = s < 0 ? -2.05f : 1.2f;
                    B(m, x0 + (r % 2) * .05f, .4f + r * 1.15f, .15f, x0 + .8f + (r % 2) * .05f, 1.35f + r * 1.15f, .17f, .004f);
                }
            }
            for (int c = 0; c < 4; c++) {
                B(m, -1.0f + c * .52f, 3.5f, .15f, -.55f + c * .52f, 4.4f, .17f, .004f);
            }
            S1(m);
            B(m, -1.17f, 0f, .15f, -1.05f, 3.45f, .3f, .008f);
            B(m, 1.05f, 0f, .15f, 1.17f, 3.45f, .3f, .008f);
            B(m, -1.17f, 3.25f, .15f, 1.17f, 3.45f, .3f, .008f);
        });
        // 6 Address Filed: a pale round arch with a gold keystone and rolled moulding.
        Add("of_door_frame_6", "office", "stone", "brass", m => {
            var hole = new KitMesh.Opening(-1.05f, 1.05f, 0f, 4.1f, true);
            DoorWall(m, hole);
            m.Orient(V(0f, 3.05f, .16f), V(90f, 0f, 0f));
            m.Lathe(P(1.22f, -.08f, 1.22f, .08f, 1.08f, .08f, 1.08f, -.08f, 1.22f, -.08f), 14, false, 180f, 180f);
            Rst(m);
            for (int s = -1; s <= 1; s += 2) {
                Lk(m, V(s * 1.15f, 0f, .26f), 0f, P(0, 0, .2f, 0, .16f, .1f, .13f, .2f, .12f, 2.9f, .16f, 3.0f, .2f, 3.06f, 0, 3.06f), 8);
            }
            S1(m);
            B(m, -.14f, 4.1f, .14f, .14f, 4.4f, .26f, .01f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.15f - .2f, 3.06f, .2f, s * 1.15f + .2f, 3.12f, .32f, .004f);
            }
        });

        // The leaves: hinge edge at x = 0, 1.9 m wide, 3.1 m tall, trim on -Z (the room side).
        Add("of_door_leaf_1", "office", "wood", "brass", m => {
            B(m, 0f, 0f, -.05f, 1.9f, 3.1f, .05f, .01f);
            for (int i = 0; i < 9; i++) {
                B(m, .05f + i * .2f, .06f, -.09f, .22f + i * .2f, 3.05f, -.05f, .008f);
            }
            B(m, .05f, 1.0f, -.1f, 1.85f, 1.1f, -.05f, .006f);
            S1(m);
            B(m, .5f, 1.9f, -.11f, 1.4f, 2.05f, -.06f, .006f);
            Lk(m, V(1.7f, 1.5f, -.1f), 0f, P(0, 0, .05f, 0, .05f, .04f, 0, .06f), 8);
        });
        Add("of_door_leaf_2", "office", "iron", "stone", m => {
            B(m, 0f, 0f, -.05f, 1.9f, 3.1f, .05f, .01f);
            for (int i = 0; i < 7; i++) {
                B(m, .08f + i * .26f, .08f, -.1f, .13f + i * .26f, 3.0f, -.05f, .004f);
            }
            for (int j = 0; j < 4; j++) {
                B(m, .04f, .3f + j * .8f, -.12f, 1.86f, .4f + j * .8f, -.05f, .006f);
            }
            S1(m);
            BRk(m, V(.95f, 1.55f, -.14f), V(0, 0, 38f), V(1.9f, .1f, .03f), .004f);
        });
        Add("of_door_leaf_3", "office", "metal", "iron", m => {
            B(m, 0f, 0f, -.06f, 1.9f, 3.1f, .06f, .012f);
            B(m, .12f, .12f, -.1f, 1.78f, 1.4f, -.06f, .01f);
            B(m, .12f, 1.6f, -.1f, 1.78f, 2.98f, -.06f, .01f);
            for (int i = 0; i < 5; i++) {
                B(m, .2f + i * .36f, .2f, -.12f, .26f + i * .36f, .26f, -.1f, .003f);
                B(m, .2f + i * .36f, 2.8f, -.12f, .26f + i * .36f, 2.86f, -.1f, .003f);
            }
            S1(m);
            B(m, .3f, 2.3f, -.18f, 1.6f, 2.36f, -.1f, .006f);
            for (int h = 0; h < 3; h++) {
                B(m, .5f + h * .5f, 2.0f, -.16f, .54f + h * .5f, 2.3f, -.12f, .004f);
            }
            B(m, 1.6f, 1.45f, -.16f, 1.8f, 1.55f, -.1f, .004f);
        });
        Add("of_door_leaf_4", "office", "iron", "lamp", m => {
            B(m, 0f, 0f, -.05f, 1.9f, 3.1f, .05f, .01f);
            for (int j = 0; j < 9; j++) {
                B(m, .06f, .1f + j * .32f, -.09f, 1.84f, .3f + j * .32f, -.05f, .005f);
            }
            S1(m);
            B(m, .55f, 1.3f, -.08f, 1.35f, 2.5f, -.04f, .004f);
        });
        Add("of_door_leaf_5", "office", "paper", "wood", m => {
            B(m, 0f, 0f, -.04f, 1.9f, 3.1f, .04f, .006f);
            for (int r = 0; r < 4; r++) {
                B(m, .15f, .15f + r * .75f, -.07f, 1.75f, .75f + r * .75f, -.04f, .004f);
            }
            S1(m);
            B(m, .06f, 0f, -.09f, .12f, 3.1f, -.04f, .003f);
            B(m, 1.78f, 0f, -.09f, 1.84f, 3.1f, -.04f, .003f);
            B(m, 1.5f, 1.45f, -.12f, 1.62f, 1.55f, -.06f, .003f);
        });
        Add("of_door_leaf_6", "office", "plaster", "brass", m => {
            B(m, 0f, 0f, -.05f, 1.9f, 3.1f, .05f, .012f);
            B(m, .12f, .14f, -.08f, 1.78f, 1.5f, -.05f, .008f);
            B(m, .12f, 1.64f, -.08f, 1.78f, 2.96f, -.05f, .008f);
            S1(m);
            B(m, .65f, 2.2f, -.1f, 1.25f, 2.7f, -.08f, .006f);
            Lk(m, V(1.65f, 1.5f, -.08f), 0f, P(0, 0, .05f, 0, .05f, .04f, 0, .06f), 8);
        });
    }

    static void AddOfficeThings() {
        Add("of_chapter_board", "office", "wood", "paper", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 2.3f - .12f, 0f, -.12f, s * 2.3f + .12f, 3.0f, .12f, .015f);
            }
            B(m, -2.45f, .8f, -.08f, 2.45f, 3.5f, .08f, .015f);
            B(m, -2.5f, 3.5f, -.12f, 2.5f, 3.62f, .14f, .012f);
            BRk(m, V(0f, 3.76f, .06f), V(-14f, 0f, 0f), V(5.4f, .1f, .9f), .012f);
            B(m, -2.5f, .74f, -.12f, 2.5f, .86f, .16f, .012f);
            S1(m);
            for (int i = 0; i < 6; i++) {
                float x = -1.9f + (i % 3) * 1.3f;
                float y = 2.2f - (i / 3) * 1.15f;
                B(m, x, y, .08f, x + 1.0f, y + .86f, .1f, .004f);
            }
        });
        Add("of_lost_thing", "office", "brass", "lamp", m => {
            m.At(V(0f, .3f, 0f), 0f);
            m.ExtrudeZ(P(-.25f, .05f, .25f, .05f, .3f, .5f, 0f, .8f, -.3f, .5f), -.035f, .035f);
            Rst(m);
            Lk(m, V(0f, 1.18f, 0f), 0f, P(0, 0, .1f, 0, .08f, .06f, 0, .08f), 6);
            S1(m);
            m.Orient(V(0f, .85f, .036f), V(90f, 0f, 0f));
            m.Lathe(P(0, 0, .09f, 0, .09f, .02f, 0, .02f), 10);
            Rst(m);
            B(m, -.16f, .45f, .036f, .16f, .52f, .05f, .004f);
        });
        Add("of_notice_slip", "office", "paper", "brass", m => {
            B(m, -.35f, -.45f, 0f, .35f, .45f, .02f, .005f);
            B(m, -.28f, .28f, .02f, .2f, .32f, .024f, .002f);
            B(m, -.28f, .16f, .02f, .12f, .2f, .024f, .002f);
            B(m, -.28f, .04f, .02f, .24f, .08f, .024f, .002f);
            S1(m);
            B(m, -.18f, -.34f, .02f, .24f, -.14f, .03f, .004f);
            m.Orient(V(.0f, .39f, .02f), V(90f, 0f, 0f));
            m.Lathe(P(0, 0, .05f, 0, .05f, .03f, 0, .045f), 8);
            Rst(m);
        });
        Add("of_hanging_lamp", "office", "brass", "lamp", m => {
            B(m, -.015f, .8f, -.015f, .015f, 2.3f, .015f, 0f);
            B(m, -.04f, 1.6f, -.04f, .04f, 1.66f, .04f, .005f);
            Lk(m, V(0f, 2.3f, 0f), 0f, P(0, 0, .2f, 0, .16f, .08f, .04f, .14f, 0, .16f), 8);
            Lk(m, V(0f, 0f, 0f), 0f, P(.6f, .3f, .52f, .34f, .32f, .62f, .1f, .8f, 0, .82f), 12);
            S1(m);
            Lk(m, V(0f, 0f, 0f), 0f, P(0, .26f, .16f, .3f, .2f, .42f, .12f, .55f, 0, .6f), 10);
        });
        Add("of_wall_lamp", "office", "brass", "lamp", m => {
            B(m, -.1f, -.26f, 0f, .1f, .26f, .04f, .01f);
            B(m, -.03f, -.04f, .04f, .03f, .04f, .22f, .005f);
            Lk(m, V(0f, -.24f, .22f), 0f, P(0, 0, .09f, 0, .06f, .05f, 0, .06f), 8);
            S1(m);
            Lk(m, V(0f, -.2f, .22f), 0f, P(0, 0, .1f, .01f, .12f, .12f, .1f, .26f, 0, .32f), 8);
        });
        Add("of_waiting_bench", "office", "wood", "iron", m => {
            for (int i = 0; i < 5; i++) {
                B(m, -1.1f, .46f, -.24f + i * .1f, 1.1f, .5f, -.16f + i * .1f, .006f);
            }
            for (int i = 0; i < 4; i++) {
                B(m, -1.1f, .72f + i * .1f, -.26f, 1.1f, .78f + i * .1f, -.22f, .006f);
            }
            B(m, -1.1f, .46f, -.26f, 1.1f, 1.1f, -.24f, 0f);
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.0f - .04f, 0f, -.24f, s * 1.0f + .04f, .46f, -.18f, .005f);
                B(m, s * 1.0f - .04f, 0f, .12f, s * 1.0f + .04f, .46f, .18f, .005f);
                B(m, s * 1.0f - .04f, .4f, -.24f, s * 1.0f + .04f, .46f, .18f, .005f);
                B(m, s * 1.0f - .04f, .46f, -.28f, s * 1.0f + .04f, 1.1f, -.22f, .005f);
            }
        });
        Add("of_plant", "office", "plaster", "cloth", m => {
            Lk(m, V(0f, 0f, 0f), 0f, P(0, 0, .2f, 0, .28f, .06f, .3f, .4f, .26f, .45f, 0, .45f), 10);
            S1(m);
            for (int i = 0; i < 10; i++) {
                float a = i * 36f;
                float lean = 18f + (i % 3) * 14f;
                float len = .8f + (i % 4) * .18f;
                BRk(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * .14f, .45f + len * .45f, Mathf.Sin(a * Mathf.Deg2Rad) * .14f), V(0f, -a, -lean), V(.05f, len, .17f), .004f);
            }
        });
        Add("of_ring_inlay", "office", "brass", "lamp", m => {
            Lk(m, V(0f, 0f, 0f), 0f, P(4.3f, 0, 4.3f, .04f, 3.9f, .04f, 3.9f, 0, 4.3f, 0), 44, false);
            Ring(m, V(0f, 0f, 0f), 4.1f, 12, V(.5f, .06f, .14f), .005f);
            S1(m);
            Lk(m, V(0f, 0f, 0f), 0f, P(3.7f, 0, 3.7f, .03f, 3.62f, .03f, 3.62f, 0, 3.7f, 0), 44, false);
        });
        Add("of_number_plate", "office", "brass", "iron", m => {
            B(m, -1.0f, -.7f, -.08f, 1.0f, .7f, .08f, .03f);
            S1(m);
            B(m, -1.1f, -.8f, -.1f, 1.1f, -.7f, .12f, .01f);
            B(m, -1.1f, .7f, -.1f, 1.1f, .8f, .12f, .01f);
            B(m, -1.1f, -.7f, -.1f, -1.0f, .7f, .12f, .01f);
            B(m, 1.0f, -.7f, -.1f, 1.1f, .7f, .12f, .01f);
        });
        Add("of_stamp_seal", "office", "brass", "iron", m => {
            m.Orient(V(0f, 0f, 0f), V(90f, 0f, 0f));
            m.Lathe(P(0, 0, .42f, 0, .42f, .05f, .36f, .08f, 0, .08f), 16);
            Rst(m);
            S1(m);
            m.Orient(V(0f, 0f, .08f), V(90f, 0f, 0f));
            m.Lathe(P(.3f, 0, .3f, .03f, .25f, .03f, .25f, 0, .3f, 0), 16, false);
            Rst(m);
            B(m, -.2f, -.04f, .08f, .2f, .04f, .12f, .004f);
            B(m, -.04f, -.2f, .08f, .04f, .2f, .12f, .004f);
        });
        Add("of_paper_stack", "office", "paper", "wood", m => {
            for (int i = 0; i < 7; i++) {
                float j = (i % 3 - 1) * .03f;
                BRk(m, V(j, .03f + i * .055f, -j), V(0f, i * 11f, 0f), V(.6f, .05f, .8f), .004f);
            }
            S1(m);
            B(m, -.34f, 0f, -.44f, .34f, .02f, .44f, .004f);
        });
        Add("of_ceiling_beam", "office", "wood", "brass", m => {
            B(m, -15f, 0f, -.22f, 15f, .45f, .22f, .02f);
            B(m, -15f, -.1f, -.3f, 15f, 0f, .3f, .01f);
            S1(m);
            for (int i = 0; i < 7; i++) {
                float x = -13.5f + i * 4.5f;
                B(m, x - .2f, -.08f, -.34f, x + .2f, .4f, .34f, .01f);
            }
        });
    }

    static void AddOfficeLodgers() {
        // Tick: a wall clock on little legs, brass face, hands, a pendulum and a hat of dust on top.
        Add("of_lodger_tick", "office", "wood", "brass", m => {
            B(m, -.45f, .4f, -.22f, .45f, 1.85f, .22f, .03f);
            B(m, -.52f, 1.85f, -.26f, .52f, 1.95f, .26f, .02f);
            B(m, -.38f, 1.95f, -.2f, .38f, 2.1f, .2f, .02f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .24f - .06f, 0f, -.06f, s * .24f + .06f, .42f, .06f, .01f);
                B(m, s * .24f - .12f, 0f, -.12f, s * .24f + .12f, .08f, .16f, .01f);
                B(m, s * .5f - .04f, 1.0f, -.05f, s * .5f + .04f, 1.4f, .05f, .008f);
            }
            S1(m);
            m.Orient(V(0f, 1.35f, .22f), V(90f, 0f, 0f));
            m.Lathe(P(0, 0, .35f, 0, .35f, .03f, 0, .03f), 18);
            Rst(m);
            B(m, -.03f, 1.35f, .25f, .03f, 1.64f, .28f, .004f);
            B(m, .0f, 1.32f, .25f, .22f, 1.38f, .28f, .004f);
            for (int i = 0; i < 4; i++) {
                float a = i * 90f * Mathf.Deg2Rad;
                B(m, Mathf.Cos(a) * .3f - .02f, 1.35f + Mathf.Sin(a) * .3f - .02f, .25f, Mathf.Cos(a) * .3f + .02f, 1.35f + Mathf.Sin(a) * .3f + .02f, .27f, .002f);
            }
            B(m, -.04f, .55f, .21f, .04f, 1.0f, .24f, .004f);
            Lk(m, V(0f, .5f, .22f), 0f, P(0, 0, .1f, .02f, .12f, .05f, .08f, .1f, 0, .1f), 8);
        });
        // Brim: a bowler hat with a ribbon band and a pair of round eyes, no head to put it on.
        Add("of_lodger_brim", "office", "cloth", "brass", m => {
            Lk(m, V(0f, .75f, 0f), 0f, P(0, 0, .78f, 0, .8f, .06f, .4f, .1f, .38f, .2f, .35f, .55f, .22f, .76f, 0, .84f), 16);
            S1(m);
            Lk(m, V(0f, .75f, 0f), 0f, P(.37f, .14f, .41f, .14f, .4f, .3f, .36f, .3f, .37f, .14f), 16, false);
            B(m, .38f, .8f, -.1f, .52f, .86f, .1f, .006f);
            B(m, .45f, .7f, -.06f, .6f, .78f, .06f, .006f);
            for (int s = -1; s <= 1; s += 2) {
                m.Orient(V(s * .13f, 1.12f, .33f), V(90f, 0f, 0f));
                m.Lathe(P(0, 0, .07f, 0, .07f, .03f, .04f, .06f, 0, .07f), 8);
                Rst(m);
                S1(m);
            }
        });
        // Spare: half a Yo Bot with an open chassis, one claw, one leg, a loose lid leaning beside it.
        Add("of_lodger_spare", "office", "metal", "lamp", m => {
            B(m, -.4f, .55f, -.26f, .4f, 1.25f, .26f, .04f);
            for (int i = 0; i < 4; i++) {
                B(m, -.34f + i * .22f, .6f, .22f, -.3f + i * .22f, 1.2f, .27f, .004f);
            }
            Lk(m, V(0f, 1.25f, 0f), 0f, P(0, 0, .34f, 0, .38f, .08f, .32f, .26f, .16f, .38f, 0, .42f), 12);
            B(m, .38f, .75f, -.06f, .76f, .85f, .06f, .01f);
            B(m, .72f, .55f, -.08f, .84f, .85f, .08f, .008f);
            BRk(m, V(.8f, .5f, -.1f), V(0f, 0f, 24f), V(.07f, .24f, .06f), .004f);
            BRk(m, V(.8f, .5f, .1f), V(0f, 0f, 24f), V(.07f, .24f, .06f), .004f);
            B(m, -.5f, .85f, -.05f, -.4f, .93f, .05f, .006f);
            B(m, -.28f, 0f, -.1f, -.12f, .56f, .1f, .01f);
            B(m, -.34f, 0f, -.14f, -.08f, .08f, .18f, .01f);
            B(m, .12f, 0f, -.08f, .22f, .26f, .08f, .008f);
            m.Orient(V(-.85f, .24f, .1f), V(0f, 0f, 62f));
            m.Lathe(P(0, 0, .32f, 0, .32f, .05f, 0, .05f), 12);
            Rst(m);
            S1(m);
            B(m, -.24f, 1.36f, .3f, .24f, 1.46f, .36f, .006f);
        });
        // Chime: a small bell on a loop, a flared lip, a clapper below it and two round eyes.
        Add("of_lodger_chime", "office", "brass", "iron", m => {
            Lk(m, V(0f, .3f, 0f), 0f, P(.55f, 0, .5f, .12f, .4f, .38f, .3f, .66f, .18f, .86f, .08f, .96f, 0, .99f), 14);
            Lk(m, V(0f, .22f, 0f), 0f, P(0, 0, .07f, 0, .1f, .1f, .05f, .16f, 0, .18f), 8);
            S1(m);
            Lk(m, V(0f, .3f, 0f), 0f, P(.52f, 0, .62f, 0, .62f, .1f, .52f, .1f, .52f, 0), 14, false);
            Lk(m, V(0f, 1.29f, 0f), 0f, P(.1f, 0, .14f, 0, .14f, .05f, .1f, .05f, .1f, 0), 8, false);
            B(m, -.04f, 1.26f, -.04f, .04f, 1.4f, .04f, .004f);
            for (int s = -1; s <= 1; s += 2) {
                m.Orient(V(s * .16f, .85f, .33f), V(90f, 0f, 0f));
                m.Lathe(P(0, 0, .06f, 0, .06f, .03f, 0, .05f), 8);
                Rst(m);
                S1(m);
            }
        });
    }
}
