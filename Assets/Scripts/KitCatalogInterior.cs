using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 2: the office (hub) and Chapter 1's yard, Chapter 2's gate hall. See KitCatalog.cs.
public static partial class KitCatalog {
    // ---- OFFICE -----------------------------------------------------------------------------------------------------

    static void AddOffice() {
        Add("of_desk", "office", "wood", "brass", m => {
            B(m, -.85f, .72f, -.42f, .85f, .77f, .42f, .012f);
            for (int s = -1; s <= 1; s += 2) {
                float x0 = s < 0 ? -.8f : .38f, x1 = s < 0 ? -.38f : .8f;
                B(m, x0, .08f, -.36f, x1, .72f, .36f, .01f);
                m.Sub = 0;
                Drawers(m, x0, x1, .12f, .7f, .36f, 3);
            }
            B(m, -.38f, .08f, -.3f, .38f, .66f, -.26f, .006f);
            B(m, -.8f, 0f, -.36f, .8f, .08f, .36f, .01f);
            B(m, -.38f, .62f, -.34f, .38f, .72f, .3f, .006f);
            B(m, -.9f, .77f, -.46f, .9f, .79f, .46f, .01f);
        });
        Add("of_chair", "office", "wood", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, .42f, .22f, .42f, .24f, .45f, .24f, .5f, .2f, .52f, 0, .52f), 12);
            m.At(V(0, 0, 0));
            m.Lathe(P(.23f, .55f, .23f, .86f, .21f, .86f, .21f, .55f, .23f, .55f), 8, true, 120f, 210f);
            m.Reset();
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(0, .1f, .05f, .1f, .035f, .14f, .03f, .42f, 0, .42f), 8);
            for (int k = 0; k < 5; k++) {
                m.At(V(0, .08f, 0), k * 72f);
                m.Box(V(0f, 0f, -.025f), V(.3f, .045f, .025f), .006f);
                m.Reset();
                S1(m);
                Vector3 p = V(Mathf.Cos(k * 72f * Mathf.Deg2Rad) * .29f, 0f, -Mathf.Sin(k * 72f * Mathf.Deg2Rad) * .29f);
                Lat(m, p, 0f, P(0, 0, .03f, 0, .03f, .07f, 0, .07f), 6);
            }
        });
        Add("of_filing_cabinet", "office", "metal", "brass", m => {
            B(m, -.24f, .04f, -.34f, .24f, 1.3f, .34f, .014f);
            B(m, -.26f, 0f, -.36f, .26f, .04f, .36f, .006f);
            B(m, -.26f, 1.3f, -.36f, .26f, 1.33f, .36f, .01f);
            for (int i = 0; i < 4; i++) {
                float y0 = .08f + i * .3f;
                m.Sub = 0;
                B(m, -.22f, y0, .34f, .22f, y0 + .27f, .365f, .008f);
                S1(m);
                B(m, -.1f, y0 + .17f, .365f, .1f, y0 + .24f, .372f, .003f);   // label frame
                B(m, -.07f, y0 + .06f, .365f, .07f, y0 + .085f, .395f, .006f); // pull
            }
        });
        Add("of_stamp_press", "office", "iron", "brass", m => {
            B(m, -.2f, 0f, -.15f, .2f, .06f, .15f, .012f);
            Lat(m, V(-.12f, .06f, 0), 0f, P(0, 0, .05f, 0, .04f, .05f, .035f, .5f, .05f, .52f, .05f, .56f, 0, .56f), 8);
            BR(m, V(.05f, .58f, 0), V(0, 0, 8f), V(.46f, .05f, .07f), .006f);
            BR(m, V(.26f, .45f, 0), V(0, 0, 0), V(.04f, .26f, .04f), .004f);
            Lat(m, V(.27f, .22f, 0), 0f, P(0, 0, .07f, 0, .07f, .03f, .045f, .05f, .03f, .1f, 0, .1f), 8);
            S1(m);
            B(m, .12f, .06f, -.09f, .19f, .075f, .09f, .004f);   // ink pad
            Lat(m, V(-.12f, .56f, 0), 0f, P(0, 0, .035f, 0, .035f, .06f, 0, .07f), 8);
            B(m, -.18f, .075f, -.14f, -.14f, .1f, -.1f, .004f);
            Lat(m, V(.27f, .12f, 0), 0f, P(0, 0, .06f, 0, .06f, .02f, 0, .02f), 8);
        });
        Add("of_pneumatic_tube", "office", "brass", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .14f, 0, .14f, .06f, .09f, .1f, .07f, .12f, .07f, .98f, .1f, 1.0f, .1f, 1.06f, .07f, 1.08f, .07f, 2.18f, .1f, 2.2f, .1f, 2.26f, .07f, 2.28f, .07f, 2.52f, 0, 2.52f), 12);
            Lat(m, V(0, 2.62f, 0), 0f, P(0, -.1f, .06f, -.1f, .09f, -.02f, .09f, .06f, .06f, .12f, 0, .12f), 10);
            m.Orient(V(0, 2.62f, 0), V(0, 0, -90f));
            m.Lathe(P(0, 0, .07f, 0, .07f, .08f, .1f, .1f, .1f, .16f, .07f, .18f, .07f, .7f, 0, .7f), 10);
            m.Reset();
            S1(m);
            B(m, -.14f, 1.4f, -.06f, .14f, 1.46f, -.0f, .006f);
            B(m, -.12f, .5f, -.14f, .12f, .55f, 0f, .006f);
            Lat(m, V(0, 0, 0), 0f, P(0, .02f, .06f, .02f, .06f, .2f, .04f, .26f, 0, .27f), 8);
        });
        Add("of_mail_slot_wall", "office", "wood", "brass", m => {
            var holes = new List<KitMesh.Opening>();
            for (int i = 0; i < 4; i++) {
                float x = -1.05f + i * .7f;
                holes.Add(new KitMesh.Opening(x, x + .4f, 1.5f, 1.58f));
            }
            m.Wall(3f, 3f, .25f, holes);
            for (int i = 0; i < 4; i++) {
                float x = -1.05f + i * .7f;
                S1(m);
                B(m, x - .06f, 1.44f, .125f, x + .46f, 1.64f, .15f, .008f);
                BR(m, V(x + .2f, 1.585f, .17f), V(25f, 0, 0), V(.38f, .02f, .09f), .004f);
                S0(m);
            }
            for (int i = 0; i < 5; i++) {
                float x = -1.3f + i * .6f;
                B(m, x, .1f, .125f, x + .5f, .95f, .15f, .01f);
            }
            B(m, -1.5f, .95f, .125f, 1.5f, 1.03f, .18f, .01f);
            B(m, -1.5f, 0f, .125f, 1.5f, .1f, .17f, .008f);
            B(m, -1.55f, 2.88f, -.18f, 1.55f, 3.04f, .2f, .015f);
        });
        Add("of_pigeonholes", "office", "wood", "paper", m => {
            B(m, -.6f, .06f, -.15f, .6f, 1.2f, -.12f, .004f);
            B(m, -.62f, .06f, -.15f, -.58f, 1.2f, .15f, .006f);
            B(m, .58f, .06f, -.15f, .62f, 1.2f, .15f, .006f);
            for (int i = 0; i < 5; i++) {
                float y = .06f + i * .28f;
                B(m, -.6f, y, -.15f, .6f, y + .03f, .15f, .004f);
            }
            for (int i = 1; i < 4; i++) {
                float x = -.6f + i * .3f;
                B(m, x - .012f, .06f, -.15f, x + .012f, 1.2f, .15f, .003f);
            }
            B(m, -.66f, 1.2f, -.19f, .66f, 1.26f, .2f, .01f);
            B(m, -.64f, 1.26f, -.17f, .64f, 1.3f, .17f, .008f);
            B(m, -.64f, 0f, -.17f, .64f, .06f, .17f, .008f);
            S1(m);
            for (int i = 0; i < 6; i++) {
                float x = -.55f + (i % 4) * .3f + .03f;
                float y = .09f + (i / 4 * 2 + 1) * .28f;
                B(m, x, y, -.1f, x + .12f, y + .16f, .08f, .004f);
            }
        });
        Add("of_rug", "office", "cloth", "brass", m => {
            B(m, -1f, 0f, -.7f, 1f, .03f, .7f, .008f);
            B(m, -.85f, .03f, -.55f, .85f, .045f, .55f, .006f);
            S1(m);
            B(m, -.78f, .045f, -.48f, .78f, .052f, -.43f, .002f);
            B(m, -.78f, .045f, .43f, .78f, .052f, .48f, .002f);
            m.Cyl(.26f, .045f, .06f, 12, .008f);
            for (int i = 0; i < 10; i++) {
                float x = -.9f + i * .2f;
                B(m, x, 0f, .7f, x + .1f, .02f, .8f, .002f);
                B(m, x, 0f, -.8f, x + .1f, .02f, -.7f, .002f);
            }
        });
        Add("of_desk_lamp", "office", "brass", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .09f, 0, .09f, .025f, .06f, .045f, .02f, .06f, 0, .06f), 10);
            BR(m, V(0, .17f, 0), V(0, 0, 0), V(.02f, .22f, .02f), .002f);
            BR(m, V(.09f, .34f, 0), V(0, 0, 55f), V(.02f, .22f, .02f), .002f);
            Lat(m, V(.17f, .38f, 0), 0f, P(0, .02f, .05f, 0, .12f, .0f, .13f, -.14f, 0, -.14f), 10);
            S1(m);
            Lat(m, V(.17f, .27f, 0), 0f, P(0, 0, .04f, 0, .045f, .05f, .03f, .09f, 0, .1f), 8);
        });
        Add("of_coat_stand", "office", "wood", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, .12f, .04f, .12f, .04f, .5f, .055f, .54f, .035f, .6f, .035f, 1.5f, .06f, 1.55f, .06f, 1.62f, .035f, 1.66f, 0, 1.68f), 8);
            for (int k = 0; k < 3; k++) {
                BR(m, V(Mathf.Cos(k * 120f * Mathf.Deg2Rad) * .17f, .1f, Mathf.Sin(k * 120f * Mathf.Deg2Rad) * .17f), V(0, -k * 120f, 14f), V(.34f, .04f, .05f), .006f);
            }
            S1(m);
            for (int k = 0; k < 4; k++) {
                float a = k * 90f + 45f;
                Vector3 d = V(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
                BR(m, V(0, 1.55f, 0) + d * .11f, V(0, -a, 35f), V(.22f, .025f, .025f), .003f);
                Lat(m, V(0, 1.6f, 0) + d * .2f, 0f, P(0, 0, .02f, 0, .02f, .04f, 0, .05f), 6);
            }
        });
        Add("of_wainscot_door", "office", "plaster", "wood", m => {
            var door = new KitMesh.Opening(-.45f, .5f, 0f, 2.2f);
            m.Wall(3f, 3f, .25f, new[] { door });
            S1(m);
            m.Frame(door, .25f, .1f, .05f, 0f);
            m.DoorLeaf(door, -.02f);
            for (int i = 0; i < 3; i++) {
                float x0 = i == 0 ? -1.45f : .65f + (i - 1) * .43f;
                float x1 = i == 0 ? -.6f : x0 + .38f;
                B(m, x0, .12f, .125f, x1, .92f, .155f, .01f);
            }
            B(m, -1.5f, .93f, .125f, 1.5f, 1.01f, .18f, .008f);       // dado rail
            B(m, -1.5f, 0f, .125f, 1.5f, .1f, .17f, .008f);           // skirting
            B(m, -1.55f, 2.9f, -.18f, 1.55f, 3.04f, .2f, .012f);      // cornice
            B(m, -.62f, 2.2f, .125f, .67f, 2.32f, .2f, .01f);         // architrave head
        });
    }

    // ---- YARD (Chapter 1) -------------------------------------------------------------------------------------------

    static void AddYard() {
        Add("yd_standing_stone", "yard", "stone", "concrete", m => {
            var poly = P(-.55f, 0, .6f, 0, .5f, 1.2f, .35f, 2.1f, 0, 2.45f, -.4f, 2f, -.5f, 1.1f);
            var inner = new List<Vector2>();
            foreach (Vector2 p in poly) {
                inner.Add(new Vector2(p.x * .86f, 1.1f + (p.y - 1.1f) * .96f));
            }
            m.Orient(V(0, 0, 0), V(3f, 5f, 2f));
            m.ExtrudeZ(poly, -.22f, .22f);
            m.ExtrudeZ(inner, -.3f, .3f);
            m.Reset();
            S1(m);
            B(m, -.7f, 0f, -.4f, .7f, .08f, .4f, .02f);
        });
        Add("yd_ring_segment", "yard", "stone", "brass", m => {
            m.At(V(-3.1f, 0, 0));
            m.Lathe(P(3.25f, 0, 3.25f, .16f, 3.17f, .2f, 3.03f, .2f, 2.95f, .16f, 2.95f, 0, 3.25f, 0), 7, false, 36f, -18f);
            S1(m);
            m.Lathe(P(3.14f, .2f, 3.14f, .214f, 3.06f, .214f, 3.06f, .2f, 3.14f, .2f), 7, false, 36f, -18f);
            m.Reset();
            S0(m);
            B(m, -.1f, .16f, 2.9f - 2.9f + .02f, .12f, .22f, .2f, .02f);
        });
        Add("yd_fallen_mailbox", "yard", "metal", "iron", m => {
            var d = new List<Vector2> { new Vector2(0, 0), new Vector2(.18f, 0) };
            d.AddRange(KitMesh.Arc(.18f, .24f, .24f, -90f, 90f, 8));
            d.Add(new Vector2(0, .48f));
            m.Orient(V(-.3f, 0, 0), V(0, 12f, 0));
            m.ExtrudeZ(d, -.5f, .5f);
            B(m, -.02f, -0.0f, -.5f, .02f, .48f, .5f, .004f);
            m.Reset();
            S1(m);
            BR(m, V(.15f, .24f, .62f), V(0, 55f, 0), V(.5f, .4f, .03f), .006f);   // flap hanging open
            B(m, -.45f, .06f, -.95f, -.35f, .12f, .15f, .01f);
            BR(m, V(.9f, .1f, -.4f), V(0, 70f, 4f), V(.14f, .14f, 1.5f), .012f);   // snapped post
            Lat(m, V(-.2f, .5f, -.3f), 0f, P(0, 0, .035f, 0, .035f, .08f, 0, .1f), 6);
        });
        Add("yd_post_box", "yard", "metal", "brass", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .32f, 0, .32f, .1f, .27f, .14f, .27f, .28f, .29f, .3f, .29f, 1.2f, .33f, 1.24f, .33f, 1.3f, .26f, 1.4f, .15f, 1.46f, 0, 1.5f), 14);
            S1(m);
            B(m, -.14f, .88f, .26f, .14f, 1.04f, .34f, .008f);
            Lat(m, V(0, .5f, 0), 0f, P(.295f, 0, .31f, 0, .31f, .05f, .295f, .05f, .295f, 0), 14);
            B(m, -.1f, .94f, .3f, .1f, .98f, .36f, .004f);
            B(m, -.09f, .5f, .28f, .09f, .68f, .33f, .006f);
        });
        Add("yd_fence", "yard", "wood", "iron", m => {
            for (int i = 0; i < 3; i++) {
                float x = -1f + i;
                B(m, x - .06f, 0f, -.06f, x + .06f, 1.15f, .06f, .008f);
                m.Face(Vector3.up, new[] { V(x - .06f, 1.15f, -.06f), V(x + .06f, 1.15f, -.06f), V(x, 1.22f, 0) });
                S1(m);
                m.Face(Vector3.up, new[] { V(x - .06f, 1.15f, .06f), V(x + .06f, 1.15f, .06f), V(x, 1.22f, 0) });
                S0(m);
            }
            B(m, -1f, .25f, .06f, 1f, .33f, .1f, .005f);
            B(m, -1f, .75f, .06f, 1f, .83f, .1f, .005f);
            for (int i = 0; i < 9; i++) {
                float x = -.9f + i * .225f;
                if (Mathf.Abs(x + 1f) < .09f || Mathf.Abs(x) < .09f || Mathf.Abs(x - 1f) < .09f) {
                    continue;
                }
                var pick = new List<Vector2> { new Vector2(x - .05f, .1f), new Vector2(x + .05f, .1f), new Vector2(x + .05f, .95f), new Vector2(x, 1.03f), new Vector2(x - .05f, .95f) };
                m.ExtrudeZ(pick, .1f, .13f);
            }
        });
        Add("yd_sorting_trestle", "yard", "wood", "paper", m => {
            B(m, -1.4f, .82f, -.36f, 1.4f, .88f, .36f, .01f);
            for (int s = -1; s <= 1; s += 2) {
                for (int z = -1; z <= 1; z += 2) {
                    BR(m, V(s * 1.0f, .41f, z * .28f), V(z * 12f, 0, 0), V(.07f, .86f, .07f), .006f);
                }
                B(m, s * 1.0f - .05f, .28f, -.3f, s * 1.0f + .05f, .32f, .3f, .005f);
            }
            B(m, -1.4f, .88f, -.36f, 1.4f, 1.3f, -.32f, .006f);
            for (int i = 0; i < 7; i++) {
                float x = -1.4f + i * .4667f;
                B(m, x - .015f, .88f, -.36f, x + .015f, 1.3f, -.12f, .003f);
            }
            S1(m);
            for (int i = 0; i < 6; i++) {
                B(m, -1.3f + i * .46f, .88f, -.3f, -1.1f + i * .46f, .88f + .12f + (i % 3) * .07f, -.14f, .004f);
            }
            B(m, -.3f, .88f, .1f, .3f, .92f, .3f, .004f);
        });
        Add("yd_sack_pile", "yard", "cloth", "iron", m => {
            var sack = P(0, 0, .28f, 0, .34f, .1f, .38f, .3f, .32f, .5f, .17f, .58f, .08f, .64f, .15f, .72f, 0, .74f);
            Matrix4x4 keep = m.M;
            m.Orient(V(-.3f, 0, .0f), V(4f, 0, -6f));
            m.Lathe(sack, 9, true);
            m.Orient(V(.35f, 0, .1f), V(-3f, 0, 5f));
            m.Lathe(sack, 9, true);
            m.Orient(V(0, .5f, -.3f), V(10f, 0, 4f));
            m.Lathe(sack, 9, true);
            m.Reset();
            S1(m);
            Lat(m, V(-.3f, .62f, 0), 0f, P(.085f, 0, .095f, 0, .095f, .03f, .085f, .03f, .085f, 0), 8);
            Lat(m, V(.35f, .62f, .1f), 0f, P(.085f, 0, .095f, 0, .095f, .03f, .085f, .03f, .085f, 0), 8);
            B(m, -.1f, .0f, .35f, .3f, .05f, .5f, .01f);
        });
        Add("yd_gravel_slab", "yard", "concrete", "stone", m => {
            B(m, -.5f, 0f, -.5f, .5f, .12f, .5f, .04f);
            S1(m);
            B(m, -.3f, .12f, -.3f, .1f, .135f, .3f, .01f);
            B(m, .12f, .12f, -.3f, .3f, .135f, .0f, .01f);
            for (int sx = -1; sx <= 1; sx += 2) {
                for (int sz = -1; sz <= 1; sz += 2) {
                    B(m, sx * .42f - .03f, .12f, sz * .42f - .03f, sx * .42f + .03f, .145f, sz * .42f + .03f, .008f);
                }
            }
        });
        Add("yd_signpost", "yard", "wood", "iron", m => {
            B(m, -.06f, 0f, -.06f, .06f, 1.9f, .06f, .008f);
            Lat(m, V(0, 1.9f, 0), 0f, P(0, 0, .09f, 0, .08f, .04f, .04f, .08f, 0, .1f), 8);
            for (int i = 0; i < 2; i++) {
                var arrow = new List<Vector2> { new Vector2(-.5f, -.09f), new Vector2(.4f, -.09f), new Vector2(.62f, 0f), new Vector2(.4f, .09f), new Vector2(-.5f, .09f) };
                m.At(V(0, 1.55f - i * .3f, .07f), i == 0 ? 8f : -14f);
                m.ExtrudeZ(arrow, 0f, .035f);
                m.Reset();
            }
            S1(m);
            B(m, -.08f, 1.12f, -.08f, .08f, 1.16f, .08f, .004f);
            B(m, -.1f, 0f, -.1f, .1f, .08f, .1f, .01f);
        });
        Add("yd_stump", "yard", "wood", "stone", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .55f, 0, .5f, .08f, .36f, .22f, .32f, .5f, .34f, .75f, .3f, .8f, .26f, .78f, .22f, .8f, .18f, .78f, .12f, .8f, .06f, .78f, 0, .8f), 10, false);
            for (int k = 0; k < 4; k++) {
                float a = k * 90f + 20f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * .42f, .08f, Mathf.Sin(a * Mathf.Deg2Rad) * .42f), V(0, -a, 16f), V(.5f, .12f, .16f), .01f);
            }
            S1(m);
            for (int k = 0; k < 3; k++) {
                Lat(m, V(Mathf.Cos(k * 2.1f) * .5f, .0f, Mathf.Sin(k * 2.1f) * .5f), 0f, P(0, 0, .08f, 0, .07f, .05f, 0, .06f), 6);
            }
        });
    }

    // ---- GATE (Chapter 2) -------------------------------------------------------------------------------------------

    static void AddGate() {
        Add("gt_turnstile", "gate", "iron", "brass", m => {
            B(m, -.9f, 0f, -.5f, .9f, .06f, .5f, .012f);
            B(m, -.2f, .06f, -.2f, .2f, 1.05f, .2f, .02f);
            Lat(m, V(0, 1.05f, 0), 0f, P(0, 0, .22f, 0, .22f, .04f, .08f, .1f, 0, .1f), 10);
            for (int k = 0; k < 3; k++) {
                m.At(V(0, 1.12f, 0), k * 120f);
                m.Box(V(0f, -.025f, -.025f), V(.75f, .025f, .025f), .004f);
                m.Cyl(.035f, -.1f, .1f, 8, .005f, V(.75f, 0, 0));
                m.Reset();
            }
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .82f - .05f, .06f, -.05f, s * .82f + .05f, 1.2f, .05f, .008f);
                B(m, s * .82f - .08f, 1.2f, -.08f, s * .82f + .08f, 1.26f, .08f, .01f);
            }
            S1(m);
            Lat(m, V(0, 1.2f, 0), 0f, P(0, 0, .05f, 0, .05f, .04f, 0, .05f), 8);
            B(m, -.12f, .7f, .2f, .12f, .86f, .23f, .006f);
        });
        Add("gt_gate_arch", "gate", "stone", "brass", m => {
            var open = new KitMesh.Opening(-1f, 1f, 0f, 3f, true);
            m.Wall(4f, 4f, .6f, new[] { open });
            float spring = 2f, r = 1f;
            for (int i = 0; i < 9; i++) {
                float a = 10f + i * 20f;
                float rad = a * Mathf.Deg2Rad;
                bool key = i == 4;
                BR(m, V(Mathf.Cos(rad) * (r + .17f), spring + Mathf.Sin(rad) * (r + .17f), .04f), V(0, 0, a - 90f), V(key ? .34f : .26f, key ? .46f : .3f, .72f), .02f);
            }
            B(m, -1.25f, 1.95f, -.34f, -.95f, 2.1f, .34f, .015f);
            B(m, .95f, 1.95f, -.34f, 1.25f, 2.1f, .34f, .015f);
            B(m, -2.12f, 3.84f, -.4f, 2.12f, 4.06f, .4f, .02f);
            B(m, -2.06f, 0f, -.36f, 2.06f, .22f, .36f, .02f);
            S1(m);
            B(m, -.1f, 3.28f, .36f, .1f, 3.4f, .4f, .006f);
        });
        Add("gt_ticket_booth", "gate", "plaster", "glass", m => {
            B(m, -.75f, 0f, -.65f, .75f, .9f, .65f, .02f);
            B(m, -.82f, .9f, -.7f, .82f, .96f, .85f, .015f);
            var win = new KitMesh.Opening(-.5f, .5f, .3f, 1.1f);
            m.M = Matrix4x4.TRS(V(0, .96f, .6f), Quaternion.identity, Vector3.one);
            m.Wall(1.5f, 1.45f, .1f, new[] { win });
            m.Frame(win, .1f, .06f, .04f, .06f);
            m.Pane(win);
            m.Reset();
            B(m, -.78f, .96f, -.65f, -.7f, 2.4f, -.55f, .01f);
            B(m, .7f, .96f, -.65f, .78f, 2.4f, -.55f, .01f);
            B(m, -.75f, .96f, -.65f, .75f, 2.4f, -.6f, .01f);
            B(m, -.8f, 2.4f, -.7f, .8f, 2.5f, .7f, .015f);
            S1(m);
            m.At(V(0, 2.5f, 0), 45f);
            m.Lathe(P(0, 0, 1.2f, 0, 1.2f, .05f, 0, .65f), 4, false);
            m.Reset();
            S0(m);
            Lat(m, V(0, 3.1f, 0), 0f, P(0, 0, .05f, 0, .04f, .15f, 0, .2f), 6);
        });
        Add("gt_stamp_press_large", "gate", "iron", "paper", m => {
            B(m, -.8f, 0f, -.6f, .8f, .2f, .6f, .02f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .65f - .1f, .2f, -.1f, s * .65f + .1f, 1.9f, .1f, .015f);
                BR(m, V(s * .45f, .7f, 0), V(0, 0, -s * 38f), V(.07f, .75f, .07f), .006f);
            }
            B(m, -.8f, 1.9f, -.18f, .8f, 2.15f, .18f, .02f);
            Lat(m, V(0, 1.1f, 0), 0f, P(0, 0, .1f, 0, .1f, .8f, .13f, .85f, .13f, .9f, 0, .9f), 10);
            B(m, -.4f, .6f, -.3f, .4f, .85f, .3f, .03f);
            Lat(m, V(0, 2.15f, 0), 0f, P(0, 0, .09f, 0, .09f, .05f, .06f, .1f, .06f, .35f, .2f, .38f, .2f, .45f, 0, .45f), 10);
            S1(m);
            B(m, -.35f, .2f, -.25f, .35f, .24f, .25f, .008f);
            B(m, -.38f, .55f, -.28f, .38f, .6f, .28f, .01f);
            Lat(m, V(.55f, 1.0f, .3f), 0f, P(0, 0, .13f, 0, .13f, .03f, 0, .03f), 12);
        });
        Add("gt_barrier_rail", "gate", "iron", "brass", m => {
            for (int s = -1; s <= 1; s += 2) {
                Lat(m, V(s * 1f, 0, 0), 0f, P(0, 0, .14f, 0, .14f, .05f, .05f, .09f, .045f, .9f, .07f, .94f, .07f, 1.0f, .04f, 1.05f, 0, 1.08f), 8);
            }
            m.Orient(V(0, .98f, 0), V(0, 0, 90f));
            m.Cyl(.028f, -1f, 1f, 8, .004f);
            m.Orient(V(0, .5f, 0), V(0, 0, 90f));
            m.Cyl(.022f, -1f, 1f, 8, .004f);
            m.Reset();
            S1(m);
            for (int i = 0; i < 3; i++) {
                Lat(m, V(-.5f + i * .5f, .98f, 0), 0f, P(0, -.04f, .045f, -.04f, .045f, .04f, 0, .04f), 8);
            }
        });
        Add("gt_bollard", "gate", "iron", "brass", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .2f, 0, .2f, .08f, .15f, .14f, .14f, .7f, .17f, .74f, .17f, .84f, .12f, .92f, .05f, .96f, 0, .97f), 10);
            S1(m);
            Lat(m, V(0, .74f, 0), 0f, P(.165f, 0, .18f, 0, .18f, .035f, .165f, .035f, .165f, 0), 10);
            Lat(m, V(0, .2f, 0), 0f, P(.145f, 0, .16f, 0, .16f, .03f, .145f, .03f, .145f, 0), 10);
        });
        Add("gt_invalid_board", "gate", "wood", "iron", m => {
            B(m, -.65f, .75f, -.04f, .65f, 1.55f, .04f, .02f);
            B(m, -.72f, .7f, -.07f, .72f, .78f, .07f, .01f);
            B(m, -.72f, 1.52f, -.07f, .72f, 1.6f, .07f, .01f);
            B(m, -.72f, .78f, -.07f, -.64f, 1.52f, .07f, .01f);
            B(m, .64f, .78f, -.07f, .72f, 1.52f, .07f, .01f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .5f - .04f, 0f, -.04f, s * .5f + .04f, .75f, .04f, .006f);
            }
            S1(m);
            BR(m, V(0, 1.15f, .06f), V(0, 0, 38f), V(1.1f, .1f, .02f), .004f);
            BR(m, V(0, 1.15f, .06f), V(0, 0, -38f), V(1.1f, .1f, .02f), .004f);
            B(m, -.55f, .72f, -.12f, .55f, .76f, .0f, .004f);
        });
        Add("gt_chalk_lamp", "gate", "iron", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .2f, 0, .2f, .1f, .12f, .16f, .09f, .2f, .09f, 2.5f, .12f, 2.55f, .12f, 2.62f, 0, 2.62f), 10);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * .22f, 2.7f, 0), V(0, 0, -s * 40f), V(.5f, .05f, .05f), .004f);
                BR(m, V(s * .52f, 2.78f, 0), V(0, 0, 0), V(.42f, .04f, .04f), .004f);
                Lat(m, V(s * .72f, 2.78f, 0), 0f, P(0, 0, .13f, 0, .1f, .08f, 0, .08f), 8);
            }
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                Lat(m, V(s * .72f, 2.84f, 0), 0f, P(0, 0, .15f, .02f, .2f, .15f, .18f, .28f, .1f, .36f, 0, .38f), 10);
            }
        });
        Add("gt_gate_pillar", "gate", "stone", "iron", m => {
            B(m, -.45f, 0f, -.45f, .45f, .4f, .45f, .04f);
            m.At(V(0, .4f, 0), 45f);
            m.Lathe(P(0, 0, .5f, 0, .38f, 2.0f, 0, 2.0f), 4, false);
            m.Reset();
            B(m, -.42f, 2.4f, -.42f, .42f, 2.52f, .42f, .02f);
            B(m, -.48f, 2.52f, -.48f, .48f, 2.66f, .48f, .025f);
            B(m, -.36f, 2.66f, -.36f, .36f, 2.74f, .36f, .015f);
            S1(m);
            Lat(m, V(0, 2.74f, 0), 0f, P(0, 0, .18f, 0, .26f, .12f, .27f, .26f, .2f, .4f, .08f, .5f, 0, .52f), 10);
        });
    }
}
