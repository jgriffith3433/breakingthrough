using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 6: chapters 4 to 6 and their three boss arenas (Docs/Levels4to6AndHub.md, sections 3 to 9). All authored from
// KitMesh at the size they are used (the hall wall is 33 m wide, the spires 60 m tall): none is a primitive or a scaled copy.
//   walk:    pale spires (three silhouettes), mist banks, the paper stand and its sheet, a pier stub, a fallen slab
//   reading: hall wall bay, hall column, ribbed vault, chandelier, form racks (full and blank), circle inlay, a page, the white door
//   filed:   wall stumps and rubble, a freestanding doorframe, circle marks, the gold seal, a grass tuft
//   arena:   the Servers' dais and its pillar, the Registrar's gallery, the Threshold's dais, the Order-Bearer's stamp mark
public static partial class KitCatalog {
    // The same as BR and Lat, but the submesh stays what it was (their Reset puts it back on the main one).
    static void BRk(KitMesh m, Vector3 c, Vector3 euler, Vector3 size, float ch = 0f) {
        int s = m.Sub;
        m.Orient(c, euler);
        m.BoxC(Vector3.zero, size, ch);
        m.Reset();
        m.Sub = s;
    }

    static void Lk(KitMesh m, Vector3 at, float yaw, List<Vector2> prof, int seg = 12, bool smooth = true) {
        int s = m.Sub;
        m.At(at, yaw);
        m.Lathe(prof, seg, smooth);
        m.Reset();
        m.Sub = s;
    }

    static void Rst(KitMesh m) {
        int s = m.Sub;
        m.Reset();
        m.Sub = s;
    }

    // ---- WALK (Chapter 4) -------------------------------------------------------------------------------------------

    static void AddWalkLate() {
        // A needle: a six-sided shaft that tapers 62 m to a point, three bands and a violet lamp near the top.
        Add("wk_spire_a", "walk", "stone", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 2.2f, 0, 2.0f, .8f, 1.5f, 12f, 1.1f, 34f, .7f, 52f, 0, 62f), 6, false);
            Lk(m, V(0, 0, 0), 0f, P(1.75f, 10f, 1.75f, 11.2f, 1.4f, 11.2f, 1.4f, 10f, 1.75f, 10f), 6, false);
            Lk(m, V(0, 0, 0), 0f, P(1.45f, 24f, 1.45f, 25f, 1.1f, 25f, 1.1f, 24f, 1.45f, 24f), 6, false);
            Lk(m, V(0, 0, 0), 0f, P(1.12f, 40f, 1.12f, 40.8f, .8f, 40.8f, .8f, 40f, 1.12f, 40f), 6, false);
            S1(m);
            Lk(m, V(0, 47f, 0), 0f, P(0, 0, .5f, .1f, .6f, .6f, .4f, 1.1f, 0, 1.3f), 8);
        });
        // A banded tower: six tiers, each narrower, lit slits and a pyramid cap.
        Add("wk_spire_b", "walk", "concrete", "lamp", m => {
            float[] size = { 3.2f, 2.8f, 2.4f, 2.0f, 1.6f, 1.2f };
            float[] top = { 10f, 18f, 26f, 34f, 40f, 45f };
            float y0 = 0f;
            for (int i = 0; i < size.Length; i++) {
                B(m, -size[i], y0, -size[i], size[i], top[i], size[i], 0f);
                B(m, -size[i] - .25f, top[i] - .35f, -size[i] - .25f, size[i] + .25f, top[i], size[i] + .25f, 0f);
                y0 = top[i];
            }
            Lk(m, V(0, 45f, 0), 45f, P(0, 0, 1.7f, 0, 0, 7f), 4, false);
            S1(m);
            for (int i = 0; i < size.Length; i++) {
                float yb = i == 0 ? 3f : top[i - 1] + 2.5f;
                B(m, -.25f, yb, size[i], .25f, yb + 2.4f, size[i] + .08f, 0f);
                B(m, size[i], yb + .6f, -.25f, size[i] + .08f, yb + 3f, .25f, 0f);
            }
        });
        // A stepped obelisk with a floating ring and a lit orb in it.
        Add("wk_spire_c", "walk", "stone", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 2.4f, 0, 2.4f, 2f, 1.5f, 2.4f, 1.5f, 20f, 1.0f, 20.5f, 1.0f, 34f, 0, 42f), 4, false);
            Lk(m, V(0, 0, 0), 0f, P(3.0f, 46f, 3.0f, 46.6f, 2.5f, 46.6f, 2.5f, 46f, 3.0f, 46f), 24, false);
            Lk(m, V(0, 0, 0), 0f, P(2.0f, 45.5f, 2.0f, 46.1f, 1.7f, 46.1f, 1.7f, 45.5f, 2.0f, 45.5f), 20, false);
            S1(m);
            Lk(m, V(0, 44.8f, 0), 0f, P(0, 0, .6f, .12f, .8f, .5f, .6f, .95f, 0, 1.1f), 8);
        });
        // A soft bank of mist: three overlapping low swells.
        Add("wk_mist_bank", "walk", "plaster", "plaster", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 7f, 0, 8f, .6f, 7.2f, 1.6f, 4.5f, 2.4f, 0, 2.7f), 10);
            Lk(m, V(4.8f, 0, 2f), 0f, P(0, 0, 4.6f, 0, 5.2f, .5f, 4.4f, 1.3f, 2.6f, 1.9f, 0, 2.1f), 9);
            Lk(m, V(-4.4f, 0, -1.8f), 0f, P(0, 0, 4.2f, 0, 4.8f, .5f, 4f, 1.2f, 2.4f, 1.7f, 0, 1.9f), 8);
        });
        // The paper stand: a post, a slanted table with a tag, a lamp bracket. The sheet is its own piece (it lifts).
        Add("wk_paper_stand", "walk", "iron", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, .5f, 0, .5f, .18f, .2f, .3f, .14f, 2.3f, .2f, 2.4f, 0, 2.4f), 10);
            B(m, -1.4f, 2.4f, -.7f, 1.4f, 2.55f, .7f, 0f);
            BRk(m, V(0, 2.66f, .1f), V(-16f, 0, 0), V(2.6f, .06f, 1.1f), .01f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.3f - .06f, 2.55f, -.55f, s * 1.3f + .06f, 3.1f, -.45f, .005f);
            }
            B(m, -.06f, 2.55f, -.45f, .06f, 4.5f, -.35f, .008f);
            S1(m);
            Lk(m, V(0, 4.5f, -.4f), 0f, P(0, 0, .26f, .03f, .3f, .24f, .22f, .5f, 0, .6f), 10);
        });
        Add("wk_paper_sheet", "walk", "paper", "iron", m => {
            B(m, -.8f, -.65f, -.03f, .8f, .65f, .03f, .01f);
            BRk(m, V(.62f, .5f, .02f), V(0, 0, 30f), V(.3f, .3f, .03f), .005f);
            S1(m);
            for (int i = 0; i < 5; i++) {
                B(m, -.6f, -.4f + i * .22f, .03f, .45f - (i % 2) * .3f, -.34f + i * .22f, .04f, .003f);
            }
        });
        // A stub of pier off the road: deck boards, posts and a mooring ring.
        Add("wk_pier_post", "walk", "wood", "iron", m => {
            for (int i = 0; i < 6; i++) {
                B(m, -1.0f, .5f, -2.9f + i * .98f, 1.0f, .62f, -2.0f + i * .98f, .012f);
            }
            for (int s = -1; s <= 1; s += 2) {
                for (int k = 0; k < 3; k++) {
                    Lk(m, V(s * 1.05f, -.5f, -2.6f + k * 2.6f), 0f, P(0, 0, .14f, 0, .12f, 1.4f, .16f, 1.55f, 0, 1.6f), 7);
                }
                B(m, s * 1.0f - .06f, .5f, -2.9f, s * 1.0f + .06f, .56f, 2.9f, .01f);
            }
            S1(m);
            Lk(m, V(0f, .62f, 2.8f), 0f, P(.18f, 0, .22f, 0, .22f, .05f, .18f, .05f, .18f, 0), 8, false);
            B(m, -.05f, .62f, 2.5f, .05f, .7f, 3.1f, .004f);
        });
        // The last slabs, broken: a jagged slab fragment with a crack.
        Add("wk_fallen_slab", "walk", "stone", "concrete", m => {
            m.Orient(V(0, .3f, 0), V(90f, 0, 0));
            m.ExtrudeZ(P(-.9f, -.9f, .9f, -.9f, 1.0f, .2f, .3f, .9f, -.7f, .8f, -1.0f, 0f), -.3f, 0f);
            Rst(m);
            S1(m);
            BRk(m, V(.1f, .31f, 0f), V(0, 25f, 0), V(1.4f, .02f, .05f), .002f);
            B(m, -.5f, .3f, -.5f, -.2f, .34f, -.2f, .004f);
        });
    }

    // ---- READING (Chapter 5) ----------------------------------------------------------------------------------------

    static void AddReadingLate() {
        // A bay of the hall wall, 33 m by 34: three tall arched windows between pilasters, a string course and a cornice.
        Add("rd_hall_wall", "reading", "plaster", "lamp", m => {
            var a = new KitMesh.Opening(-13f, -7f, 15f, 31f, true);
            var b = new KitMesh.Opening(-3f, 3f, 15f, 31f, true);
            var c = new KitMesh.Opening(7f, 13f, 15f, 31f, true);
            m.Wall(33f, 34f, 2f, new[] { a, b, c }, 0f);
            for (int i = 0; i < 4; i++) {
                float x = -16.2f + i * 10.8f;
                B(m, x - .9f, 0f, .9f, x + .9f, 31.5f, 1.6f, 0f);
                B(m, x - 1.2f, 31.5f, .9f, x + 1.2f, 32.3f, 1.9f, 0f);
                B(m, x - 1.2f, 0f, .9f, x + 1.2f, 1.4f, 1.9f, 0f);
            }
            B(m, -16.5f, 0f, .9f, 16.5f, 3.2f, 1.3f, 0f);
            B(m, -16.5f, 13.4f, .9f, 16.5f, 14.4f, 1.4f, 0f);
            B(m, -16.8f, 32f, -1.1f, 16.8f, 34f, 1.8f, 0f);
            for (int i = 0; i < 3; i++) {
                float x = -10f + i * 10f;
                B(m, x - 3.6f, 14.4f, .9f, x + 3.6f, 15f, 1.7f, 0f);
            }
            m.Pane(a);
            m.Pane(b);
            m.Pane(c);
        });
        // A hall column, 30 m: plinth, tapered fluted shaft, brass rings and a squared capital.
        Add("rd_hall_column", "reading", "stone", "brass", m => {
            B(m, -2.4f, 0f, -2.4f, 2.4f, .8f, 2.4f, 0f);
            B(m, -1.9f, .8f, -1.9f, 1.9f, 1.6f, 1.9f, 0f);
            Lk(m, V(0, 1.6f, 0), 0f, P(0, 0, 1.45f, 0, 1.3f, 2f, 1.2f, 14f, 1.28f, 25f, 1.4f, 26.5f, 0, 26.5f), 12, false);
            B(m, -2.1f, 26.5f, -2.1f, 2.1f, 27.6f, 2.1f, 0f);
            B(m, -2.6f, 27.6f, -2.6f, 2.6f, 30f, 2.6f, 0f);
            for (int i = 0; i < 12; i++) {
                float ang = i * 30f * Mathf.Deg2Rad;
                B(m, Mathf.Cos(ang) * 1.3f - .1f, 3f, Mathf.Sin(ang) * 1.3f - .1f, Mathf.Cos(ang) * 1.3f + .1f, 25f, Mathf.Sin(ang) * 1.3f + .1f, 0f);
            }
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(1.55f, 2.4f, 1.55f, 3.2f, 1.2f, 3.2f, 1.2f, 2.4f, 1.55f, 2.4f), 12, false);
            Lk(m, V(0, 0, 0), 0f, P(1.55f, 24.4f, 1.55f, 25.2f, 1.2f, 25.2f, 1.2f, 24.4f, 1.55f, 24.4f), 12, false);
        });
        // The vault: a ribbed dome seen from below, with a glowing eye at the top.
        Add("rd_vault", "reading", "stone", "lamp", m => {
            float[] r = { 0f, 30f, 70f, 105f, 125f, 135f };
            float[] y = { 75f, 74f, 68f, 55f, 44f, 38f };
            var prof = new List<Vector2>();
            for (int i = 0; i < r.Length; i++) {
                prof.Add(new Vector2(r[i], y[i]));
            }
            prof.Add(new Vector2(135f, 35f));
            Lk(m, V(0, 0, 0), 0f, prof, 24, true);
            for (int k = 0; k < 12; k++) {
                float ang = k * 30f;
                float rad = ang * Mathf.Deg2Rad;
                for (int i = 0; i < r.Length - 1; i++) {
                    float dr = r[i + 1] - r[i], dy = y[i + 1] - y[i];
                    float len = Mathf.Sqrt(dr * dr + dy * dy);
                    float mr = (r[i] + r[i + 1]) * .5f, my = (y[i] + y[i + 1]) * .5f;
                    float nr = dy / len, ny = -dr / len;
                    float cr = mr + nr * .8f, cy = my + ny * .8f;
                    BRk(m, V(Mathf.Cos(rad) * cr, cy, Mathf.Sin(rad) * cr), V(0, -ang, Mathf.Atan2(dy, dr) * Mathf.Rad2Deg), V(len + .6f, 1.6f, 1.6f), 0f);
                }
            }
            S1(m);
            Lk(m, V(0, 73.2f, 0), 0f, P(9f, 0, 9f, .6f, 6.5f, .6f, 6.5f, 0, 9f, 0), 20, false);
        });
        // A chandelier, 10 m across: a brass ring with twelve candle cups and four chains running up to the vault.
        Add("rd_hall_chandelier", "reading", "brass", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(5.3f, 0, 5.3f, .6f, 4.8f, .6f, 4.8f, 0, 5.3f, 0), 20, false);
            for (int i = 0; i < 12; i++) {
                float a = i * 30f * Mathf.Deg2Rad;
                Lk(m, V(Mathf.Cos(a) * 5.05f, .6f, Mathf.Sin(a) * 5.05f), 0f, P(0, 0, .35f, 0, .28f, .6f, 0, .6f), 6, false);
            }
            BRk(m, V(2.5f, 8.5f, 0), V(0, 0, 17.2f), V(.2f, 17.8f, .2f), 0f);
            BRk(m, V(-2.5f, 8.5f, 0), V(0, 0, -17.2f), V(.2f, 17.8f, .2f), 0f);
            BRk(m, V(0, 8.5f, 2.5f), V(-17.2f, 0, 0), V(.2f, 17.8f, .2f), 0f);
            BRk(m, V(0, 8.5f, -2.5f), V(17.2f, 0, 0), V(.2f, 17.8f, .2f), 0f);
            Lk(m, V(0, 16.4f, 0), 0f, P(0, 0, .7f, 0, .5f, .5f, 0, .6f), 8, false);
            B(m, -.12f, 16.4f, -.12f, .12f, 52.5f, .12f, 0f);
            S1(m);
            for (int i = 0; i < 12; i++) {
                float a = i * 30f * Mathf.Deg2Rad;
                Lk(m, V(Mathf.Cos(a) * 5.05f, .6f, Mathf.Sin(a) * 5.05f), 0f, P(0, 0, .18f, .05f, .12f, .5f, 0, .9f), 6, false);
            }
        });
        // A wall rack of forms: a frame holding eight identical sheets with a heading bar each (30 m by 13).
        Add("rd_form_rack", "reading", "wood", "paper", m => {
            for (int i = 0; i < 4; i++) {
                float x = -15f + i * 10f;
                B(m, x - .2f, 0f, -.25f, x + .2f, 13.2f, .25f, 0f);
            }
            B(m, -15.2f, 0f, -.3f, 15.2f, .6f, .3f, 0f);
            B(m, -15.2f, 12.8f, -.3f, 15.2f, 13.4f, .3f, 0f);
            B(m, -15.2f, 6.4f, -.3f, 15.2f, 6.9f, .3f, 0f);
            S1(m);
            for (int row = 0; row < 2; row++) {
                for (int i = 0; i < 4; i++) {
                    float x0 = -14.4f + i * 7.3f;
                    float y0 = .9f + row * 6.2f;
                    B(m, x0, y0, .3f, x0 + 6.2f, y0 + 5.1f, .4f, 0f);
                }
            }
            S0(m);
            for (int row = 0; row < 2; row++) {
                for (int i = 0; i < 4; i++) {
                    float x0 = -14.4f + i * 7.3f;
                    float y0 = .9f + row * 6.2f;
                    B(m, x0 + .5f, y0 + 4.1f, .4f, x0 + 5.7f, y0 + 4.5f, .46f, .01f);
                }
            }
        });
        // The same frame once the section has been read: bare, with a scrap of paper left on one pin.
        Add("rd_form_rack_blank", "reading", "wood", "paper", m => {
            for (int i = 0; i < 4; i++) {
                float x = -15f + i * 10f;
                B(m, x - .2f, 0f, -.25f, x + .2f, 13.2f, .25f, 0f);
            }
            B(m, -15.2f, 0f, -.3f, 15.2f, .6f, .3f, 0f);
            B(m, -15.2f, 12.8f, -.3f, 15.2f, 13.4f, .3f, 0f);
            B(m, -15.2f, 6.4f, -.3f, 15.2f, 6.9f, .3f, 0f);
            for (int row = 0; row < 2; row++) {
                for (int i = 0; i < 4; i++) {
                    float x0 = -14.4f + i * 7.3f;
                    float y0 = .9f + row * 6.2f;
                    B(m, x0 + .2f, y0 + 4.9f, .26f, x0 + .45f, y0 + 5.15f, .34f, .01f);
                    B(m, x0 + 5.75f, y0 + 4.9f, .26f, x0 + 6.0f, y0 + 5.15f, .34f, .01f);
                }
            }
            S1(m);
            B(m, -14.4f, .9f, .3f, -13.6f, 2.6f, .34f, .006f);
            B(m, 6.4f, 7.1f, .3f, 7.4f, 8.1f, .34f, .006f);
        });
        // The circle drawn on the hall's floor in brass, 20 m out: a ring, a lit thin ring inside it and thirty-six ticks.
        Add("rd_circle_inlay", "reading", "brass", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(20.4f, 0, 20.4f, .08f, 19.6f, .08f, 19.6f, 0, 20.4f, 0), 72, false);
            Ring(m, V(0, 0, 0), 18.6f, 36, V(.3f, .06f, 1.1f), .004f);
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(17.7f, 0, 17.7f, .05f, 17.5f, .05f, 17.5f, 0, 17.7f, 0), 72, false);
        });
        Add("rd_page", "reading", "paper", "iron", m => {
            B(m, -.7f, -.04f, -.5f, .7f, .04f, .5f, .01f);
            BRk(m, V(.5f, .08f, .3f), V(0, 25f, 10f), V(.4f, .03f, .4f), .004f);
            S1(m);
            for (int i = 0; i < 4; i++) {
                B(m, -.5f, .04f, -.35f + i * .22f, .35f - (i % 2) * .25f, .06f, -.3f + i * .22f, .003f);
            }
        });
        // The white door at the hall's end: a stepped frame, lintel and a tall blank leaf ruled like a form.
        Add("rd_white_door", "reading", "plaster", "paper", m => {
            B(m, -5.8f, 0f, -.5f, 5.8f, .5f, 3f, 0f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 4.6f - .6f, .5f, -.8f, s * 4.6f + .6f, 14f, .8f, 0f);
                B(m, s * 4.6f - .8f, 14f, -.9f, s * 4.6f + .8f, 14.6f, .9f, 0f);
            }
            B(m, -5.4f, 14f, -.9f, 5.4f, 15.4f, .9f, 0f);
            B(m, -5.8f, 15.4f, -.7f, 5.8f, 16f, .7f, 0f);
            S1(m);
            B(m, -4.0f, .5f, -.2f, 4.0f, 14f, .2f, 0f);
            for (int i = 0; i < 9; i++) {
                B(m, -3.4f, 1.6f + i * 1.3f, .2f, 3.4f, 1.72f + i * 1.3f, .26f, .01f);
            }
            S0(m);
            B(m, 2.6f, 6.6f, .26f, 3.1f, 7.6f, .5f, 0f);
        });
    }

    // ---- FILED (Chapter 6) ------------------------------------------------------------------------------------------

    static void AddFiledLate() {
        // A broken stump of the Department wall: a 10 m run with a jagged top and a cornice stub.
        Add("fd_wall_stump", "filed", "stone", "concrete", m => {
            m.ExtrudeZ(P(-5f, 0f, 5f, 0f, 5f, 3f, 3.2f, 3.8f, 1.5f, 2.4f, -.5f, 4.2f, -2.6f, 3.1f, -4.2f, 3.6f, -5f, 2.2f), -.7f, .7f);
            B(m, -5.1f, 0f, -.8f, 5.1f, .5f, .8f, 0f);
            S1(m);
            B(m, -4.2f, 1.2f, .7f, -1.6f, 1.28f, .78f, .006f);
            B(m, .4f, 1.8f, .7f, 3.8f, 1.88f, .78f, .006f);
            B(m, -4.6f, 2.6f, .7f, -3.6f, 2.68f, .76f, .006f);
        });
        Add("fd_rubble", "filed", "stone", "concrete", m => {
            BRk(m, V(0f, .5f, 0f), V(8f, 20f, 12f), V(2.2f, 1.1f, 1.4f), .07f);
            BRk(m, V(1.5f, .35f, .8f), V(-6f, 65f, 4f), V(1.4f, .7f, 1.0f), .06f);
            BRk(m, V(-1.3f, .3f, .9f), V(4f, 110f, -10f), V(1.1f, .6f, .9f), .05f);
            BRk(m, V(.4f, .25f, -1.2f), V(0f, 140f, 6f), V(1.3f, .5f, .8f), .05f);
            Lk(m, V(-.6f, 0, -1.6f), 0f, P(0, 0, .6f, 0, .7f, .3f, .4f, .6f, 0, .66f), 7);
            S1(m);
            B(m, -.6f, 1.05f, -.2f, .2f, 1.1f, .3f, .004f);
        });
        // A bare doorframe standing by itself in the grass, 10 m tall, a little off true.
        Add("fd_free_door", "filed", "plaster", "brass", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 2.4f - .45f, 0f, -.55f, s * 2.4f + .45f, 9.6f, .55f, 0f);
                B(m, s * 2.4f - .65f, 0f, -.75f, s * 2.4f + .65f, .9f, .75f, 0f);
            }
            B(m, -3.3f, 9.6f, -.6f, 3.3f, 10.4f, .6f, 0f);
            B(m, -3.6f, 10.4f, -.45f, 3.6f, 10.8f, .45f, 0f);
            S1(m);
            B(m, -.7f, 9.7f, .6f, .7f, 10.3f, .68f, .01f);
        });
        // One mark of the white circle: a low bar with a gold line down it.
        Add("fd_circle_mark", "filed", "plaster", "brass", m => {
            B(m, -.25f, 0f, -1.7f, .25f, .07f, 1.7f, .02f);
            S1(m);
            B(m, -.05f, .07f, -1.4f, .05f, .09f, 1.4f, .003f);
        });
        // The great gold seal, 14 m across, laid flat here (the map stands it up): a rim, two rings and an eight-armed star.
        Add("fd_gold_seal", "filed", "brass", "stone", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 7f, 0, 7f, .3f, 6.6f, .5f, 0, .5f), 36, false);
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(5.4f, .5f, 5.4f, .62f, 5.1f, .62f, 5.1f, .5f, 5.4f, .5f), 36, false);
            Lk(m, V(0, 0, 0), 0f, P(3.2f, .5f, 3.2f, .62f, 2.9f, .62f, 2.9f, .5f, 3.2f, .5f), 28, false);
            for (int k = 0; k < 8; k++) {
                float ang = k * 45f;
                float rad = ang * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(rad) * 2.7f, .56f, Mathf.Sin(rad) * 2.7f), V(0, -ang, 0), V(3.8f, .12f, .55f), .01f);
            }
        });
        Add("fd_grass_tuft", "filed", "grass", "grass", m => {
            float[] lean = { 12f, 28f, 20f, 36f, 8f, 30f, 18f, 40f, 24f, 14f, 32f };
            float[] len = { .55f, .42f, .6f, .38f, .5f, .45f, .58f, .36f, .52f, .48f, .4f };
            for (int i = 0; i < 11; i++) {
                float a = i * 33f;
                float rad = a * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(rad) * .1f, len[i] * .5f, Mathf.Sin(rad) * .1f), V(0, -a, -lean[i]), V(.05f, len[i], .11f), .004f);
            }
        });
    }

    // ---- ARENAS -----------------------------------------------------------------------------------------------------

    static void AddArenaLate() {
        // The Servers' Dais, 23.5 m out to the lip: two steps, a lit ring inlaid in the floor and eight seams running in to the middle.
        Add("ar_server_dais", "arena", "stone", "lamp", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 23.5f, 0, 23.5f, .32f, 22.6f, .44f, 22.6f, .78f, 21.6f, .9f, 0, .9f), 40, false);
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(16f, .9f, 16f, .97f, 15f, .97f, 15f, .9f, 16f, .9f), 40, false);
            for (int k = 0; k < 8; k++) {
                float ang = k * 45f + 22.5f;
                float rad = ang * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(rad) * 8.6f, .93f, Mathf.Sin(rad) * 8.6f), V(0, -ang, 0), V(13.4f, .06f, .45f), .004f);
            }
        });
        // A dais pillar: a slim eight-sided shaft crowned with a lamp head and three folded legs, like the lamps on the causeway.
        Add("ar_server_pillar", "arena", "stone", "lamp", m => {
            B(m, -.75f, 0f, -.75f, .75f, .5f, .75f, 0f);
            Lk(m, V(0, 0, 0), 0f, P(0, .5f, .55f, .5f, .4f, 1f, .32f, 7f, .36f, 7.4f, 0, 7.4f), 8, false);
            B(m, -.6f, 7.4f, -.6f, .6f, 7.7f, .6f, 0f);
            for (int k = 0; k < 3; k++) {
                float a = k * 120f * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(a) * .3f, 8.3f, Mathf.Sin(a) * .3f), V(Mathf.Sin(a) * 24f, 0, -Mathf.Cos(a) * 24f), V(.08f, .9f, .08f), .006f);
            }
            S1(m);
            Lk(m, V(0, 7.7f, 0), 0f, P(0, 0, .32f, .03f, .4f, .34f, .3f, .65f, 0, .75f), 10);
        });
        // The Registrar's Gallery: three ringed steps round a plinth for the Great Ledger, a brass ring in the top step and a rail with a gap on -Z.
        Add("ar_gallery_dais", "arena", "stone", "brass", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 23.5f, 0, 23.5f, .4f, 21.5f, .4f, 21.5f, .8f, 19.5f, .8f, 19.5f, 1.2f, 17f, 1.2f, 17f, 1.5f, 0, 1.5f), 32, false);
            Lk(m, V(0, 1.5f, 0), 0f, P(0, 0, 3.4f, 0, 3.4f, .35f, 2.8f, .5f, 0, .5f), 16, false);
            for (int k = 0; k < 24; k++) {
                float ang = k * 15f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang, 270f)) < 22f) {
                    continue;
                }
                float rad = ang * Mathf.Deg2Rad;
                B(m, Mathf.Cos(rad) * 23f - .15f, .4f, Mathf.Sin(rad) * 23f - .15f, Mathf.Cos(rad) * 23f + .15f, 1.5f, Mathf.Sin(rad) * 23f + .15f, 0f);
            }
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(15.4f, 1.5f, 15.4f, 1.57f, 14.6f, 1.57f, 14.6f, 1.5f, 15.4f, 1.5f), 32, false);
            for (int k = 0; k < 23; k++) {
                float a0 = k * 15f, a1 = (k + 1) * 15f;
                if (Mathf.Abs(Mathf.DeltaAngle(a0, 270f)) < 22f || Mathf.Abs(Mathf.DeltaAngle(a1, 270f)) < 22f) {
                    continue;
                }
                float am = (a0 + a1) * .5f * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(am) * 22.85f, 1.4f, Mathf.Sin(am) * 22.85f), V(0, -((a0 + a1) * .5f + 90f), 0), V(6.0f, .12f, .12f), 0f);
            }
        });
        // The Threshold's dais: two white steps, a gold ring and eight gold lines running to the rim.
        Add("ar_threshold_dais", "arena", "stone", "brass", m => {
            Lk(m, V(0, 0, 0), 0f, P(0, 0, 23.5f, 0, 23.5f, .4f, 21.5f, .4f, 21.5f, .8f, 0, .8f), 40, false);
            S1(m);
            Lk(m, V(0, 0, 0), 0f, P(16.5f, .8f, 16.5f, .87f, 15.8f, .87f, 15.8f, .8f, 16.5f, .8f), 40, false);
            for (int k = 0; k < 8; k++) {
                float ang = k * 45f;
                float rad = ang * Mathf.Deg2Rad;
                BRk(m, V(Mathf.Cos(rad) * 12f, .83f, Mathf.Sin(rad) * 12f), V(0, -ang, 0), V(8f, .06f, .3f), .004f);
            }
        });
        // One of the Order-Bearer's stamp marks: a pad with a raised crossed block.
        Add("ar_stamp_mark", "arena", "stone", "iron", m => {
            B(m, -2.5f, 0f, -1.7f, 2.5f, .08f, 1.7f, .02f);
            S1(m);
            B(m, -2.2f, .08f, -1.4f, 2.2f, .13f, -1.2f, .006f);
            B(m, -2.2f, .08f, 1.2f, 2.2f, .13f, 1.4f, .006f);
            BRk(m, V(0, .11f, 0), V(0, 28f, 0), V(4.4f, .05f, .5f), .005f);
            BRk(m, V(0, .11f, 0), V(0, -28f, 0), V(4.4f, .05f, .5f), .005f);
        });
    }
}
