using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 3: Chapters 3 to 6 and the boss arenas. See KitCatalog.cs.
public static partial class KitCatalog {
    // ---- FLOOR (Chapter 3) ------------------------------------------------------------------------------------------

    static void AddFloor() {
        Add("fl_conveyor", "floor", "metal", "iron", m => {
            B(m, -1.5f, .5f, -.5f, 1.5f, .62f, -.44f, .008f);
            B(m, -1.5f, .5f, .44f, 1.5f, .62f, .5f, .008f);
            B(m, -1.5f, .62f, -.5f, 1.5f, .68f, -.42f, .006f);
            B(m, -1.5f, .62f, .42f, 1.5f, .68f, .5f, .006f);
            for (int i = 0; i < 9; i++) {
                float x = -1.3f + i * .325f;
                m.Orient(V(x, .56f, 0), V(90f, 0, 0));
                m.Cyl(.045f, -.42f, .42f, 8, .006f);
                m.Reset();
            }
            for (int s = -1; s <= 1; s += 2) {
                for (int z = -1; z <= 1; z += 2) {
                    B(m, s * 1.3f - .04f, 0f, z * .47f - .03f, s * 1.3f + .04f, .5f, z * .47f + .03f, .005f);
                }
                B(m, s * 1.3f - .03f, .2f, -.47f, s * 1.3f + .03f, .26f, .47f, .004f);
            }
            S1(m);
            B(m, -1.48f, .63f, -.4f, 1.48f, .66f, .4f, .005f);
            for (int i = 0; i < 12; i++) {
                float x = -1.4f + i * .255f;
                B(m, x, .66f, -.4f, x + .035f, .7f, .4f, .004f);
            }
        });
        Add("fl_crusher", "floor", "iron", "iron", m => {
            B(m, -1f, 0f, -.7f, 1f, .25f, .7f, .025f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .85f - .12f, .25f, -.12f, s * .85f + .12f, 2.6f, .12f, .02f);
                B(m, s * .85f - .16f, 2.6f, -.16f, s * .85f + .16f, 2.7f, .16f, .015f);
            }
            B(m, -1f, 2.7f, -.25f, 1f, 3.0f, .25f, .02f);
            Lat(m, V(0, 1.4f, 0), 0f, P(0, 0, .14f, 0, .14f, 1.3f, .18f, 1.34f, .18f, 1.4f, 0, 1.4f), 10);
            B(m, -.75f, 1.25f, -.4f, .75f, 1.4f, .4f, .03f);
            B(m, -.55f, 0.25f, -.45f, .55f, .4f, .45f, .02f);
            S1(m);
            for (int i = 0; i < 6; i++) {
                float x = -.7f + i * .28f;
                BR(m, V(x, 1.325f, .401f), V(0, 0, 40f), V(.1f, .22f, .01f), .002f);
            }
        });
        Add("fl_hook_rail", "floor", "iron", "metal", m => {
            B(m, -1.5f, 2.6f, -.2f, 1.5f, 2.68f, .2f, .008f);
            B(m, -1.5f, 2.68f, -.03f, 1.5f, 3.0f, .03f, .006f);
            B(m, -1.5f, 3.0f, -.2f, 1.5f, 3.08f, .2f, .008f);
            for (int i = 0; i < 6; i++) {
                float x = -1.25f + i * .5f;
                B(m, x - .1f, 2.52f, -.05f, x + .1f, 2.6f, .05f, .006f);
                m.Orient(V(x, 2.55f, 0), V(0, 0, 90f));
                m.Cyl(.05f, -.12f, .12f, 8, .004f);
                m.Reset();
                B(m, x - .015f, 1.8f, -.015f, x + .015f, 2.52f, .015f, .003f);
                B(m, x - .015f, 1.7f, -.015f, x + .015f, 1.82f, .15f, .003f);
                BR(m, V(x, 1.65f, .15f), V(-30f, 0, 0), V(.03f, .12f, .03f), .003f);
            }
            S1(m);
            for (int i = 0; i < 3; i++) {
                B(m, -1.48f + i * 1.48f, 2.6f, -.22f, -1.4f + i * 1.48f, 3.08f, .22f, .004f);
            }
        });
        Add("fl_chassis_rack", "floor", "metal", "iron", m => {
            for (int sx = -1; sx <= 1; sx += 2) {
                for (int sz = -1; sz <= 1; sz += 2) {
                    B(m, sx * .85f - .04f, 0f, sz * .4f - .04f, sx * .85f + .04f, 2.2f, sz * .4f + .04f, .006f);
                }
            }
            for (int i = 0; i < 3; i++) {
                float y = .15f + i * .9f;
                B(m, -.9f, y, -.44f, .9f, y + .05f, .44f, .008f);
                B(m, -.9f, y, -.44f, .9f, y + .12f, -.4f, .004f);
                B(m, -.9f, y, .4f, .9f, y + .12f, .44f, .004f);
            }
            B(m, -.9f, 2.2f, -.44f, .9f, 2.26f, .44f, .008f);
            S1(m);
            for (int i = 0; i < 2; i++) {
                float y = .2f + i * .9f;
                B(m, -.6f, y, -.2f, -.55f, y + .6f, .2f, .004f);
                B(m, .55f, y, -.2f, .6f, y + .6f, .2f, .004f);
                B(m, -.6f, y + .55f, -.2f, .6f, y + .6f, -.15f, .004f);
                B(m, -.6f, y + .0f, -.2f, .6f, y + .05f, -.15f, .004f);
                BR(m, V(0, y + .3f, 0), V(0, 0, 30f), V(.8f, .03f, .03f), .002f);
            }
        });
        Add("fl_girder", "floor", "metal", "iron", m => {
            B(m, -1.5f, 0f, -.15f, 1.5f, .08f, .15f, .008f);
            B(m, -1.5f, .08f, -.025f, 1.5f, .52f, .025f, .004f);
            B(m, -1.5f, .52f, -.15f, 1.5f, .6f, .15f, .008f);
            for (int i = 0; i < 7; i++) {
                float x = -1.4f + i * .467f;
                B(m, x - .02f, .08f, -.07f, x + .02f, .52f, -.025f, .003f);
                B(m, x - .02f, .08f, .025f, x + .02f, .52f, .07f, .003f);
            }
            S1(m);
            for (int i = 0; i < 6; i++) {
                float x = -1.25f + i * .5f;
                m.Orient(V(x, .6f, .08f), V(0, 0, 0));
                m.Cyl(.02f, 0f, .02f, 6, .004f);
                m.Orient(V(x, .6f, -.08f), V(0, 0, 0));
                m.Cyl(.02f, 0f, .02f, 6, .004f);
                m.Reset();
                S1(m);
            }
        });
        Add("fl_pipe_run", "floor", "metal", "iron", m => {
            m.Orient(V(0, .5f, 0), V(0, 0, 90f));
            m.Lathe(P(0, -1.5f, .12f, -1.5f, .12f, -1.2f, .17f, -1.2f, .17f, -1.15f, .12f, -1.15f, .12f, 1.15f, .17f, 1.15f, .17f, 1.2f, .12f, 1.2f, .12f, 1.5f, 0, 1.5f), 10);
            m.Reset();
            for (int f = 0; f < 2; f++) {
                float x = f == 0 ? -1.175f : 1.175f;
                for (int k = 0; k < 6; k++) {
                    float a = k * 60f * Mathf.Deg2Rad;
                    BR(m, V(x, .5f + Mathf.Cos(a) * .15f, Mathf.Sin(a) * .15f), V(0, 0, 0), V(.07f, .03f, .03f), .004f);
                }
            }
            Lat(m, V(0, .62f, 0), 0f, P(0, 0, .06f, 0, .05f, .06f, .035f, .3f, 0, .3f), 8);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .6f - .04f, 0f, -.04f, s * .6f + .04f, .38f, .04f, .005f);
                B(m, s * .6f - .1f, .34f, -.1f, s * .6f + .1f, .4f, .1f, .005f);
            }
            S1(m);
            m.Orient(V(0, .92f, 0), V(0, 0, 0));
            m.Lathe(P(.0f, 0, .14f, 0, .14f, .03f, .12f, .035f, .0f, .035f), 12);
            m.Reset();
            B(m, -.14f, .92f, -.01f, .14f, .94f, .01f, .002f);
            B(m, -.01f, .92f, -.14f, .01f, .94f, .14f, .002f);
        });
        Add("fl_barrel", "floor", "metal", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .3f, 0, .32f, .04f, .33f, .2f, .35f, .24f, .35f, .3f, .34f, .34f, .35f, .5f, .37f, .54f, .37f, .6f, .35f, .64f, .35f, .84f, .36f, .88f, .33f, .92f, .32f, .96f, 0, .96f), 12);
            S1(m);
            Lat(m, V(0, .96f, 0), 0f, P(0, 0, .06f, 0, .06f, .04f, .04f, .06f, 0, .06f), 8);
            Lat(m, V(0, .3f, 0), 0f, P(.34f, 0, .36f, 0, .36f, .05f, .34f, .05f, .34f, 0), 12);
            Lat(m, V(0, .72f, 0), 0f, P(.355f, 0, .37f, 0, .37f, .05f, .355f, .05f, .355f, 0), 12);
        });
        Add("fl_catwalk", "floor", "metal", "iron", m => {
            B(m, -1.5f, 0f, -.5f, 1.5f, .08f, -.42f, .006f);
            B(m, -1.5f, 0f, .42f, 1.5f, .08f, .5f, .006f);
            for (int i = 0; i < 16; i++) {
                float x = -1.46f + i * .1933f;
                B(m, x, .04f, -.42f, x + .045f, .09f, .42f, .003f);
            }
            for (int i = 0; i < 5; i++) {
                float x = -1.45f + i * .725f;
                B(m, x - .03f, .08f, .44f, x + .03f, 1.1f, .5f, .005f);
            }
            B(m, -1.5f, 1.05f, .43f, 1.5f, 1.11f, .5f, .006f);
            S1(m);
            B(m, -1.5f, .55f, .45f, 1.5f, .59f, .48f, .003f);
            for (int i = 0; i < 3; i++) {
                B(m, -1.5f, .2f + i * 0.0f, -.4f + i * .4f, 1.5f, .24f, -.38f + i * .4f, .002f);
            }
        });
        Add("fl_cage_lamp", "floor", "iron", "lamp", m => {
            B(m, -.12f, .0f, -.04f, .12f, .5f, 0f, .008f);
            BR(m, V(0, .35f, .1f), V(0, 0, 0), V(.04f, .04f, .22f), .004f);
            Lat(m, V(0, .3f, .22f), 0f, P(0, 0, .12f, 0, .12f, .04f, .08f, .08f, 0, .08f), 8);
            for (int k = 0; k < 4; k++) {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                BR(m, V(Mathf.Cos(a) * .12f, .46f, .22f + Mathf.Sin(a) * .12f), V(0, 0, 0), V(.015f, .3f, .015f), .002f);
            }
            Lat(m, V(0, .6f, .22f), 0f, P(0, 0, .13f, 0, .12f, .04f, 0, .1f), 8);
            Lat(m, V(0, .42f, .22f), 0f, P(.13f, 0, .14f, 0, .14f, .02f, .13f, .02f, .13f, 0), 8);
            S1(m);
            Lat(m, V(0, .36f, .22f), 0f, P(0, 0, .09f, .02f, .1f, .13f, .07f, .22f, 0, .24f), 8);
        });
    }

    // ---- WALK (Chapter 4) -------------------------------------------------------------------------------------------

    static void AddWalk() {
        Add("wk_causeway_slab", "walk", "stone", "concrete", m => {
            B(m, -1f, 0f, -1f, 1f, .3f, 1f, .06f);
            B(m, -.82f, .3f, -.82f, .82f, .34f, .82f, .03f);
            S1(m);
            for (int sx = -1; sx <= 1; sx += 2) {
                for (int sz = -1; sz <= 1; sz += 2) {
                    B(m, sx * .9f - .04f, .3f, sz * .9f - .04f, sx * .9f + .04f, .36f, sz * .9f + .04f, .01f);
                }
            }
            B(m, -.02f, .34f, -.8f, .02f, .345f, .8f, .002f);
        });
        Add("wk_lamp_stilt", "walk", "iron", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .2f, 0, .2f, .08f, .13f, .14f, .11f, .6f, .14f, .64f, .14f, .7f, .1f, .74f, .08f, 3.5f, .11f, 3.56f, .11f, 3.66f, .07f, 3.72f, 0, 3.72f), 10);
            for (int k = 0; k < 3; k++) {
                float a = k * 120f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * .12f, 3.82f, Mathf.Sin(a * Mathf.Deg2Rad) * .12f), V(Mathf.Sin(a * Mathf.Deg2Rad) * 10f, 0, -Mathf.Cos(a * Mathf.Deg2Rad) * 10f), V(.03f, .32f, .03f), .003f);
            }
            Lat(m, V(0, 4.0f, 0), 0f, P(0, 0, .22f, 0, .2f, .04f, .06f, .14f, 0, .18f), 10);
            S1(m);
            Lat(m, V(0, 3.72f, 0), 0f, P(0, 0, .1f, .02f, .15f, .14f, .15f, .22f, .1f, .28f, 0, .3f), 10);
        });
        Add("wk_plaza_kerb", "walk", "stone", "concrete", m => {
            m.At(V(-2.5f, 0, 0));
            m.Lathe(P(2.7f, 0, 2.7f, .2f, 2.62f, .26f, 2.5f, .26f, 2.4f, .2f, 2.34f, .1f, 2.3f, 0, 2.7f, 0), 6, false, 30f, -15f);
            m.Reset();
            S1(m);
            m.At(V(-2.5f, 0, 0));
            m.Lathe(P(2.34f, .1f, 2.34f, .12f, 2.28f, .12f, 2.28f, .1f, 2.34f, .1f), 6, false, 30f, -15f);
            m.Reset();
            S0(m);
            B(m, -.1f, .24f, -.1f, .1f, .3f, .1f, .015f);
        });
        Add("wk_railing", "walk", "iron", "concrete", m => {
            for (int i = 0; i < 3; i++) {
                float x = -1f + i;
                Lat(m, V(x, 0, 0), 0f, P(0, 0, .09f, 0, .09f, .1f, .06f, .14f, .055f, .95f, .08f, 1.0f, .08f, 1.06f, .04f, 1.12f, 0, 1.14f), 8);
            }
            B(m, -1f, 1.0f, -.035f, 1f, 1.06f, .035f, .005f);
            B(m, -1f, .3f, -.025f, 1f, .34f, .025f, .004f);
            for (int i = 0; i < 9; i++) {
                float x = -.87f + i * .217f;
                if (Mathf.Abs(x) < .08f) {
                    continue;
                }
                Lat(m, V(x, .34f, 0), 0f, P(0, 0, .02f, 0, .018f, .1f, .028f, .2f, .018f, .3f, .02f, .62f, 0, .66f), 6);
            }
            S1(m);
            B(m, -1.1f, 0f, -.12f, 1.1f, .07f, .12f, .015f);
        });
        Add("wk_mooring_bollard", "walk", "iron", "metal", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .3f, 0, .28f, .1f, .21f, .2f, .17f, .4f, .2f, .52f, .3f, .6f, .3f, .66f, .2f, .72f, 0, .72f), 10);
            S1(m);
            for (int k = 0; k < 8; k++) {
                float a = k * 45f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * .13f, .74f, Mathf.Sin(a * Mathf.Deg2Rad) * .13f + .0f), V(0, -a, 0), V(.07f, .03f, .045f), .004f);
            }
            B(m, -.03f, .72f, -.03f, .03f, .76f, .03f, .004f);
        });
        Add("wk_pylon", "walk", "concrete", "iron", m => {
            B(m, -.55f, 0f, -.55f, .55f, .3f, .55f, .04f);
            m.At(V(0, .3f, 0), 45f);
            m.Lathe(P(0, 0, .75f, 0, .52f, 3.0f, 0, 3.0f), 4, false);
            m.Reset();
            B(m, -.5f, 1.2f, -.5f, .5f, 1.28f, .5f, .02f);
            B(m, -.44f, 2.2f, -.44f, .44f, 2.26f, .44f, .02f);
            B(m, -.55f, 3.3f, -.55f, .55f, 3.5f, .55f, .035f);
            B(m, -.18f, 1.6f, .3f, .18f, 2.0f, .5f, .02f);
            S1(m);
            B(m, -.5f, 3.5f, -.02f, .5f, 3.56f, .02f, .004f);
            B(m, -.2f, 3.5f, -.5f, -.16f, 3.6f, .5f, .004f);
        });
        Add("wk_stone_bench", "walk", "stone", "concrete", m => {
            B(m, -.9f, .38f, -.25f, .9f, .48f, .25f, .03f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .65f - .14f, 0f, -.2f, s * .65f + .14f, .38f, .2f, .03f);
                B(m, s * .65f - .17f, 0f, -.23f, s * .65f + .17f, .06f, .23f, .02f);
            }
            S1(m);
            B(m, -.5f, .48f, -.15f, .5f, .5f, -.13f, .004f);
            B(m, -.5f, .48f, .0f, .5f, .5f, .02f, .004f);
            B(m, -.5f, .48f, .13f, .5f, .5f, .15f, .004f);
        });
        Add("wk_arrow_sign", "walk", "iron", "stone", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .12f, 0, .12f, .06f, .05f, .1f, .04f, 1.7f, .06f, 1.74f, 0, 1.76f), 8);
            var arrow = new List<Vector2> { new Vector2(-.5f, -.1f), new Vector2(.35f, -.1f), new Vector2(.6f, 0f), new Vector2(.35f, .1f), new Vector2(-.5f, .1f) };
            m.At(V(0, 1.45f, .05f), 0f);
            m.ExtrudeZ(arrow, 0f, .04f);
            m.Reset();
            S1(m);
            m.Orient(V(0, 1.18f, .0f), V(90f, 0, 0));
            m.Lathe(P(.1f, -.015f, .13f, -.015f, .13f, .015f, .1f, .015f, .1f, -.015f), 10);
            m.Reset();
        });
        Add("wk_mist_urn", "walk", "stone", "concrete", m => {
            B(m, -.3f, 0f, -.3f, .3f, .14f, .3f, .025f);
            Lat(m, V(0, .14f, 0), 0f, P(0, 0, .2f, 0, .2f, .05f, .15f, .1f, .26f, .3f, .3f, .45f, .22f, .62f, .13f, .7f, .12f, .78f, .2f, .86f, .24f, .9f, .2f, .92f, 0, .92f), 12);
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * .31f, .62f, 0), V(0, 0, s * 20f), V(.05f, .3f, .07f), .006f);
            }
            Lat(m, V(0, .14f, 0), 0f, P(.19f, .4f, .245f, .4f, .245f, .44f, .19f, .44f, .19f, .4f), 12);
        });
    }

    // ---- READING (Chapter 5) ----------------------------------------------------------------------------------------

    static void AddReading() {
        Add("rd_lectern", "reading", "wood", "paper", m => {
            B(m, -.32f, 0f, -.28f, .32f, .1f, .28f, .02f);
            m.At(V(0, .1f, 0), 45f);
            m.Lathe(P(0, 0, .17f, 0, .12f, .85f, 0, .85f), 4, false);
            m.Reset();
            B(m, -.28f, .95f, -.24f, .28f, 1.02f, .24f, .015f);
            BR(m, V(0, 1.12f, 0), V(-24f, 0, 0), V(.62f, .05f, .52f), .012f);
            BR(m, V(0, 1.02f, .24f), V(-24f, 0, 0), V(.62f, .06f, .05f), .01f);
            S1(m);
            BR(m, V(-.14f, 1.16f, 0), V(-24f, 0, -3f), V(.26f, .03f, .38f), .004f);
            BR(m, V(.14f, 1.16f, 0), V(-24f, 0, 3f), V(.26f, .03f, .38f), .004f);
        });
        Add("rd_bookshelf", "reading", "wood", "cloth", m => {
            B(m, -.7f, 0f, -.18f, .7f, .08f, .18f, .01f);
            B(m, -.72f, .08f, -.18f, -.68f, 2.3f, .18f, .01f);
            B(m, .68f, .08f, -.18f, .72f, 2.3f, .18f, .01f);
            B(m, -.7f, .08f, -.18f, .7f, 2.3f, -.15f, .004f);
            B(m, -.76f, 2.3f, -.22f, .76f, 2.38f, .2f, .015f);
            for (int i = 0; i < 5; i++) {
                float y = .08f + i * .5f;
                B(m, -.7f, y, -.15f, .7f, y + .035f, .18f, .005f);
            }
            S1(m);
            for (int row = 0; row < 4; row++) {
                float y = .115f + row * .5f;
                float x = -.66f;
                for (int b = 0; b < 8; b++) {
                    float w = .07f + ((b * 7 + row * 3) % 5) * .012f;
                    float h = .3f + ((b * 5 + row * 2) % 4) * .035f;
                    if (x + w > .66f) {
                        break;
                    }
                    B(m, x, y, -.1f, x + w, y + h, .13f, .004f);
                    x += w + .012f;
                }
            }
        });
        Add("rd_reading_desk", "reading", "wood", "brass", m => {
            B(m, -.95f, .74f, -.5f, .95f, .8f, .5f, .012f);
            B(m, -.98f, .8f, -.5f, .98f, .86f, -.44f, .008f);
            B(m, -.98f, .8f, .44f, .98f, .84f, .5f, .008f);
            for (int s = -1; s <= 1; s += 2) {
                float x0 = s < 0 ? -.9f : .5f, x1 = s < 0 ? -.5f : .9f;
                B(m, x0, 0f, -.44f, x1, .74f, .44f, .012f);
                Drawers(m, x0, x1, .1f, .72f, .44f, 3);
            }
            B(m, -.5f, .1f, -.4f, .5f, .7f, -.36f, .006f);
            B(m, -.5f, .08f, -.4f, .5f, .12f, .4f, .006f);
            S1(m);
            Lat(m, V(.7f, .8f, -.2f), 0f, P(0, 0, .05f, 0, .05f, .04f, .03f, .07f, 0, .07f), 8);
            B(m, -.4f, .8f, -.2f, .0f, .83f, .2f, .005f);
        });
        Add("rd_great_ledger", "reading", "cloth", "paper", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .3f, 0, .3f, .08f, .2f, .14f, .1f, .2f, .1f, .8f, .16f, .86f, .16f, .92f, 0, .92f), 10);
            BR(m, V(0, 1.05f, 0), V(-20f, 0, 0), V(1.5f, .06f, 1.0f), .012f);
            BR(m, V(0, .95f, .46f), V(-20f, 0, 0), V(1.5f, .08f, .06f), .01f);
            S1(m);
            BR(m, V(-.37f, 1.12f, 0), V(-20f, 0, -4f), V(.72f, .06f, .9f), .006f);
            BR(m, V(.37f, 1.12f, 0), V(-20f, 0, 4f), V(.72f, .06f, .9f), .006f);
            S0(m);
            BR(m, V(0, 1.1f, 0), V(-20f, 0, 0), V(.05f, .07f, .94f), .004f);
            BR(m, V(0, 1.075f, 0), V(-20f, 0, 0), V(1.56f, .02f, 1.04f), .006f);
        });
        Add("rd_column", "reading", "stone", "brass", m => {
            B(m, -.5f, 0f, -.5f, .5f, .2f, .5f, .03f);
            Lat(m, V(0, .2f, 0), 0f, P(0, 0, .42f, 0, .42f, .1f, .34f, .16f, .3f, .24f, .28f, 2.9f, .31f, 2.98f, .36f, 3.06f, .36f, 3.12f, 0, 3.12f), 12, false);
            B(m, -.5f, 3.32f, -.5f, .5f, 3.5f, .5f, .03f);
            S1(m);
            Lat(m, V(0, .2f, 0), 0f, P(.43f, 0, .45f, 0, .45f, .04f, .43f, .04f, .43f, 0), 12);
            Lat(m, V(0, 3.1f, 0), 0f, P(.37f, 0, .39f, 0, .39f, .04f, .37f, .04f, .37f, 0), 12);
        });
        Add("rd_arch_window_wall", "reading", "plaster", "glass", m => {
            var win = new KitMesh.Opening(-.6f, .6f, .8f, 2.6f, true);
            m.Wall(3f, 3f, .3f, new[] { win });
            float r = .6f, spring = 2f;
            for (int i = 0; i < 7; i++) {
                float a = 12f + i * 26f;
                float rad = a * Mathf.Deg2Rad;
                bool key = i == 3;
                BR(m, V(Mathf.Cos(rad) * (r + .1f), spring + Mathf.Sin(rad) * (r + .1f), .1f), V(0, 0, a - 90f), V(key ? .2f : .16f, key ? .26f : .18f, .16f), .012f);
            }
            B(m, -.74f, .8f, .1f, -.6f, 2f, .2f, .01f);
            B(m, .6f, .8f, .1f, .74f, 2f, .2f, .01f);
            B(m, -.8f, .68f, .1f, .8f, .8f, .26f, .012f);
            B(m, -1.55f, 2.9f, -.18f, 1.55f, 3.04f, .2f, .012f);
            B(m, -1.55f, 0f, .15f, 1.55f, .12f, .2f, .008f);
            S1(m);
            var pane = new List<Vector2> { new Vector2(-.6f, .8f), new Vector2(.6f, .8f) };
            pane.AddRange(KitMesh.Arc(0f, 2f, .6f, 0f, 180f, 10));
            m.ExtrudeZ(pane, -.01f, .01f);
            S0(m);
            B(m, -.02f, .8f, 0f, .02f, 2.6f, .05f, .003f);
            B(m, -.6f, 1.6f, 0f, .6f, 1.64f, .05f, .003f);
        });
        Add("rd_form_board", "reading", "wood", "paper", m => {
            B(m, -.7f, .9f, -.03f, .7f, 1.9f, .0f, .004f);
            B(m, -.76f, .84f, -.04f, .76f, .92f, .06f, .01f);
            B(m, -.76f, 1.88f, -.04f, .76f, 1.96f, .06f, .01f);
            B(m, -.76f, .92f, -.04f, -.68f, 1.88f, .06f, .01f);
            B(m, .68f, .92f, -.04f, .76f, 1.88f, .06f, .01f);
            S1(m);
            for (int i = 0; i < 6; i++) {
                float x = -.5f + (i % 3) * .4f, y = 1.0f + (i / 3) * .46f;
                BR(m, V(x, y + .18f, .015f), V(0, 0, (i * 7 % 5 - 2) * 2.5f), V(.26f, .36f, .012f), .002f);
                B(m, x - .01f, y + .32f, .02f, x + .01f, y + .35f, .035f, .002f);
            }
        });
        Add("rd_chandelier", "reading", "brass", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .1f, 0, .12f, .08f, .08f, .2f, .12f, .26f, .12f, .34f, .06f, .4f, 0, .42f), 10);
            for (int k = 0; k < 12; k++) {
                float a = k * 30f * Mathf.Deg2Rad;
                BR(m, V(Mathf.Cos(a) * .7f, .2f, Mathf.Sin(a) * .7f), V(0, -k * 30f + 90f, 0), V(.38f, .04f, .04f), .004f);
            }
            for (int k = 0; k < 6; k++) {
                float a = k * 60f;
                float rad = a * Mathf.Deg2Rad;
                BR(m, V(Mathf.Cos(rad) * .35f, .16f, Mathf.Sin(rad) * .35f), V(0, -a, 8f), V(.7f, .03f, .03f), .003f);
                Lat(m, V(Mathf.Cos(rad) * .7f, .22f, Mathf.Sin(rad) * .7f), 0f, P(0, 0, .04f, 0, .04f, .03f, .06f, .05f, .06f, .08f, 0, .08f), 8);
            }
            for (int i = 0; i < 3; i++) {
                B(m, -.015f, .42f + i * .2f, -.04f, .015f, .58f + i * .2f, .04f, .003f);
            }
            S1(m);
            for (int k = 0; k < 6; k++) {
                float rad = k * 60f * Mathf.Deg2Rad;
                Lat(m, V(Mathf.Cos(rad) * .7f, .3f, Mathf.Sin(rad) * .7f), 0f, P(0, 0, .025f, 0, .02f, .08f, 0, .12f), 6);
            }
        });
        Add("rd_runner", "reading", "cloth", "brass", m => {
            B(m, -.6f, 0f, -1.5f, .6f, .03f, 1.5f, .008f);
            B(m, -.5f, .03f, -1.4f, .5f, .045f, 1.4f, .006f);
            S1(m);
            for (int i = 0; i < 6; i++) {
                float z = -1.2f + i * .48f;
                B(m, -.2f, .045f, z, .2f, .052f, z + .22f, .002f);
            }
            for (int i = 0; i < 8; i++) {
                float x = -.56f + i * .15f;
                B(m, x, 0f, 1.5f, x + .07f, .02f, 1.62f, .002f);
                B(m, x, 0f, -1.62f, x + .07f, .02f, -1.5f, .002f);
            }
        });
    }

    // ---- FILED (Chapter 6) ------------------------------------------------------------------------------------------

    static void AddFiled() {
        Add("fd_threshold", "filed", "stone", "brass", m => {
            B(m, -1.6f, 0f, -.9f, 1.6f, .15f, .9f, .03f);
            B(m, -1.4f, .15f, -.75f, 1.4f, .3f, .75f, .03f);
            B(m, -.95f, .3f, -.2f, -.7f, 3.3f, .2f, .02f);
            B(m, .7f, .3f, -.2f, .95f, 3.3f, .2f, .02f);
            B(m, -1.0f, 3.3f, -.24f, 1.0f, 3.6f, .24f, .025f);
            B(m, -1.12f, 3.6f, -.3f, 1.12f, 3.76f, .3f, .02f);
            B(m, -1.2f, 3.76f, -.2f, 1.2f, 3.86f, .2f, .015f);
            B(m, -1.04f, .3f, -.24f, -.95f, .5f, .24f, .012f);
            B(m, .95f, .3f, -.24f, 1.04f, .5f, .24f, .012f);
            S1(m);
            B(m, -.28f, 3.36f, .24f, .28f, 3.54f, .28f, .01f);
            B(m, -.1f, .3f, -.06f, .1f, .34f, .06f, .004f);
            B(m, -.7f, 1.55f, -.03f, -.66f, 1.7f, .1f, .003f);
            B(m, -.7f, 2.45f, -.03f, -.66f, 2.6f, .1f, .003f);
        });
        Add("fd_dais", "filed", "stone", "brass", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, 3f, 0, 3f, .1f, 2.92f, .14f, 2.9f, .24f, 2.4f, .24f, 2.32f, .28f, 2.3f, .38f, 1.8f, .38f, 1.72f, .42f, 1.7f, .52f, 0, .52f), 20, false);
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(1.2f, .52f, 1.2f, .54f, 1.1f, .54f, 1.1f, .52f, 1.2f, .52f), 20, false);
        });
        Add("fd_pillar", "filed", "plaster", "brass", m => {
            B(m, -.5f, 0f, -.5f, .5f, .22f, .5f, .03f);
            Lat(m, V(0, .22f, 0), 0f, P(0, 0, .38f, 0, .36f, .08f, .3f, .14f, .28f, 3.0f, .32f, 3.1f, .4f, 3.2f, .44f, 3.28f, 0, 3.28f), 16);
            B(m, -.52f, 3.5f, -.52f, .52f, 3.64f, .52f, .025f);
            S1(m);
            Lat(m, V(0, .22f, 0), 0f, P(.37f, 0, .39f, 0, .39f, .04f, .37f, .04f, .37f, 0), 16);
            Lat(m, V(0, 3.2f, 0), 0f, P(.41f, 0, .45f, 0, .45f, .05f, .41f, .05f, .41f, 0), 16);
        });
        Add("fd_round_arch", "filed", "stone", "brass", m => {
            var open = new KitMesh.Opening(-1.3f, 1.3f, 0f, 3.6f, true);
            m.Wall(4f, 5.2f, .5f, new[] { open });
            m.Orient(V(0, 2.3f, .2f), V(90f, 0, 0));
            m.Lathe(P(1.5f, -.08f, 1.5f, .08f, 1.38f, .08f, 1.38f, -.08f, 1.5f, -.08f), 12, false, 180f, 180f);
            m.Reset();
            for (int s = -1; s <= 1; s += 2) {
                Lat(m, V(s * 1.46f, 0, .3f), 0f, P(0, 0, .2f, 0, .16f, .1f, .13f, .2f, .12f, 2.2f, .15f, 2.3f, .2f, 2.34f, 0, 2.34f), 8);
            }
            B(m, -2.1f, 5.0f, -.3f, 2.1f, 5.2f, .3f, .02f);
            S1(m);
            B(m, -.12f, 3.7f, .25f, .12f, 3.95f, .3f, .008f);
            B(m, -1.55f, 2.3f, .22f, -1.25f, 2.36f, .34f, .004f);
            B(m, 1.25f, 2.3f, .22f, 1.55f, 2.36f, .34f, .004f);
        });
        Add("fd_pale_slab", "filed", "stone", "brass", m => {
            var hex = new List<Vector2>();
            for (int k = 0; k < 6; k++) {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                hex.Add(new Vector2(Mathf.Cos(a) * .9f, Mathf.Sin(a) * .9f));
            }
            m.Orient(V(0, 0f, 0), V(90f, 0, 0));   // z -.14..0 turns to y 0...14: on the ground, not 14 cm above it
            m.ExtrudeZ(hex, -.14f, 0f);
            m.Reset();
            S1(m);
            var ring = new List<Vector2>();
            for (int k = 0; k < 6; k++) {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                ring.Add(new Vector2(Mathf.Cos(a) * .6f, Mathf.Sin(a) * .6f));
            }
            m.Orient(V(0, .14f, 0), V(90f, 0, 0));   // the brass inset on the slab's top face
            m.ExtrudeZ(ring, -.02f, 0f);
            m.Reset();
        });
        Add("fd_plinth", "filed", "stone", "brass", m => {
            B(m, -.55f, 0f, -.55f, .55f, .18f, .55f, .03f);
            B(m, -.45f, .18f, -.45f, .45f, .8f, .45f, .025f);
            B(m, -.52f, .8f, -.52f, .52f, .9f, .52f, .02f);
            B(m, -.4f, .9f, -.4f, .4f, .96f, .4f, .015f);
            S1(m);
            B(m, -.46f, .44f, -.46f, .46f, .5f, .46f, .004f);
        });
        Add("fd_banner", "filed", "cloth", "brass", m => {
            var tail = new List<Vector2> { new Vector2(-.45f, 0f), new Vector2(.45f, 0f), new Vector2(.45f, -1.3f), new Vector2(.22f, -1.05f), new Vector2(0f, -1.45f), new Vector2(-.22f, -1.05f), new Vector2(-.45f, -1.3f) };
            m.At(V(0, 2.9f, .06f), 0f);
            m.ExtrudeZ(tail, 0f, .025f);
            m.Reset();
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .14f, 0, .14f, .06f, .06f, .1f, .04f, 3.0f, .06f, 3.04f, 0, 3.08f), 8);
            m.Orient(V(0, 2.95f, 0), V(0, 0, 90f));
            m.Cyl(.025f, -.55f, .55f, 8, .004f);
            m.Reset();
            Lat(m, V(-.56f, 2.95f, 0), 0f, P(0, -.04f, .05f, -.04f, .04f, .06f, 0, .08f), 6);
            Lat(m, V(.56f, 2.95f, 0), 0f, P(0, -.04f, .05f, -.04f, .04f, .06f, 0, .08f), 6);
            Lat(m, V(0, 3.04f, 0), 0f, P(0, 0, .06f, 0, .05f, .06f, .03f, .1f, 0, .12f), 8);
        });
        Add("fd_stair", "filed", "stone", "iron", m => {
            for (int i = 0; i < 6; i++) {
                B(m, -.9f, i * .17f, i * .3f, .9f, (i + 1) * .17f, (i + 1) * .3f, .012f);
            }
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * .98f, .62f, .9f), V(-29.5f, 0, 0), V(.1f, .18f, 2.2f), .012f);
            }
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                for (int i = 0; i < 3; i++) {
                    float z = .15f + i * .75f;
                    B(m, s * .98f - .02f, i * .5f * .5f + .0f + .18f, z - .02f, s * .98f + .02f, i * .25f + .18f + .9f, z + .02f, .003f);
                }
                BR(m, V(s * .98f, 1.0f, .9f), V(-29.5f, 0, 0), V(.045f, .04f, 2.0f), .004f);
            }
        });
        Add("fd_lantern", "filed", "brass", "lamp", m => {
            for (int i = 0; i < 4; i++) {
                B(m, -.015f, 1.0f + i * .17f, -.04f, .015f, 1.12f + i * .17f, .04f, .003f);
                B(m, -.04f, 1.05f + i * .17f, -.01f, .04f, 1.17f + i * .17f, .01f, .003f);
            }
            Lat(m, V(0, .85f, 0), 0f, P(0, 0, .22f, 0, .16f, .06f, .06f, .14f, 0, .16f), 10);
            for (int k = 0; k < 6; k++) {
                float a = k * 60f * Mathf.Deg2Rad;
                BR(m, V(Mathf.Cos(a) * .16f, .5f, Mathf.Sin(a) * .16f), V(0, 0, 0), V(.02f, .36f, .02f), .003f);
            }
            Lat(m, V(0, .3f, 0), 0f, P(0, 0, .1f, 0, .2f, .04f, .2f, .08f, .16f, .12f, 0, .12f), 10);
            S1(m);
            Lat(m, V(0, .32f, 0), 0f, P(0, 0, .15f, .02f, .18f, .2f, .15f, .4f, 0, .46f), 10);
        });
    }

    // ---- ARENA (boss rooms) -----------------------------------------------------------------------------------------

    static void AddArena() {
        Add("ar_ring_floor", "arena", "stone", "brass", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, 6f, 0, 6f, .12f, 5.88f, .2f, 5.4f, .2f, 5.3f, .14f, 5.2f, .1f, 3.3f, .1f, 3.2f, .14f, 3.1f, .1f, 1.2f, .1f, 1.1f, .16f, .9f, .16f, 0, .16f), 24, false);
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(3.7f, .1f, 3.7f, .12f, 3.6f, .12f, 3.6f, .1f, 3.7f, .1f), 24, false);
            Lat(m, V(0, 0, 0), 0f, P(5.0f, .1f, 5.0f, .12f, 4.9f, .12f, 4.9f, .1f, 5.0f, .1f), 24, false);
        });
        Add("ar_brazier", "arena", "iron", "lamp", m => {
            for (int k = 0; k < 3; k++) {
                float a = k * 120f;
                float rad = a * Mathf.Deg2Rad;
                BR(m, V(Mathf.Cos(rad) * .22f, .45f, Mathf.Sin(rad) * .22f), V(Mathf.Sin(rad) * 15f, 0, -Mathf.Cos(rad) * 15f), V(.07f, .95f, .07f), .006f);
            }
            Lat(m, V(0, .85f, 0), 0f, P(0, 0, .2f, 0, .4f, .12f, .46f, .3f, .44f, .34f, .38f, .34f, 0, .3f), 12);
            Lat(m, V(0, .5f, 0), 0f, P(.2f, 0, .24f, 0, .24f, .04f, .2f, .04f, .2f, 0), 10);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * .46f, 1.1f, 0), V(0, 0, 0), V(.04f, .09f, .09f), .006f);
            }
            S1(m);
            Lat(m, V(0, 1.12f, 0), 0f, P(0, 0, .36f, 0, .32f, .08f, .22f, .16f, .1f, .24f, 0, .28f), 10);
        });
        Add("ar_boss_gate", "arena", "stone", "iron", m => {
            var open = new KitMesh.Opening(-1.5f, 1.5f, 0f, 3.8f, true);
            m.Wall(8f, 5f, .6f, new[] { open });
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 3.4f - .4f, 0f, -.5f, s * 3.4f + .4f, 5.6f, .5f, .03f);
                B(m, s * 3.4f - .5f, 5.6f, -.6f, s * 3.4f + .5f, 5.8f, .6f, .025f);
                for (int i = 0; i < 3; i++) {
                    B(m, s * 3.4f - .5f + i * .35f, 5.8f, -.5f, s * 3.4f - .25f + i * .35f, 6.1f, .5f, .01f);
                }
                B(m, s * 1.5f - .15f, 3.8f - 3.8f, .3f, s * 1.5f + .15f, 3.0f, .55f, .015f);
            }
            for (int i = 0; i < 8; i++) {
                B(m, -2.5f + i * .65f, 5f, -.3f, -2.2f + i * .65f, 5.35f, .3f, .01f);
            }
            S1(m);
            B(m, -1.5f, 3.0f, .26f, 1.5f, 3.06f, .32f, .006f);
            B(m, -.15f, 3.9f, .3f, .15f, 4.3f, .36f, .01f);
        });
    }
}
