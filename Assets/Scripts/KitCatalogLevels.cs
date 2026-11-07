using System.Collections.Generic;
using UnityEngine;

// Kit pieces, part 4: what Chapters 1 to 3 needed that the first 82 pieces did not have (Docs/Levels1to3.md, Docs/ModelKit.md
// section 6). Every one is authored from KitMesh like the others, at the size it is used (no piece is a scaled primitive):
//   yard:  dead tree, drop box, notice board, Return Slot frame and shutter, mailbox flag, clock tower (the landmark)
//   gate:  press frame and head (the head moves), wall bay and tower, the gate's sealed door
//   floor: press frame and head, smokestack, lever stand with its lamp mast, hung chassis, and the cleared things (hat, clock, bell)
public static partial class KitCatalog {
    static void AddLevels() {
        AddYardMore();
        AddGateMore();
        AddFloorMore();
    }

    static void AddYardMore() {
        Add("yd_dead_tree", "yard", "wood", "stone", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .46f, 0, .5f, .12f, .34f, .5f, .26f, 1.6f, .2f, 2.8f, .13f, 3.9f, .06f, 4.7f, 0, 4.9f), 9, false);
            for (int k = 0; k < 4; k++) {
                float a = k * 90f + 25f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * .42f, .1f, Mathf.Sin(a * Mathf.Deg2Rad) * .42f), V(0, -a, 18f), V(.7f, .16f, .2f), .01f);
            }
            float[] hs = { 1.9f, 2.6f, 3.2f, 3.8f, 4.3f };
            float[] ys = { 20f, 150f, 260f, 80f, 200f };
            float[] ts = { 58f, 52f, 62f, 48f, 40f };
            float[] ls = { 1.9f, 1.6f, 1.5f, 1.2f, .9f };
            for (int i = 0; i < hs.Length; i++) {
                m.Orient(V(0, hs[i], 0), V(0, ys[i], -ts[i]));
                Matrix4x4 baseM = m.M;
                m.Lathe(P(0, 0, .09f, 0, .07f, ls[i] * .5f, .04f, ls[i], 0, ls[i] * 1.05f), 6, false);
                m.M = baseM * Matrix4x4.TRS(V(0, ls[i] * .55f, 0), Quaternion.Euler(0, 90f, -40f), Vector3.one);
                m.Lathe(P(0, 0, .05f, 0, .03f, ls[i] * .35f, 0, ls[i] * .4f), 5, false);
            }
            m.Reset();
            S1(m);
            B(m, -.75f, 0f, -.1f, -.3f, .22f, .35f, .05f);
            B(m, .35f, 0f, .2f, .7f, .16f, .55f, .04f);
        });
        Add("yd_drop_box", "yard", "metal", "brass", m => {
            B(m, -1.2f, 0f, -.9f, 1.2f, .22f, .9f, .03f);
            B(m, -1.1f, .22f, -.8f, 1.1f, 1.5f, .8f, .04f);
            B(m, -1.22f, 1.5f, -.92f, 1.22f, 1.64f, .92f, .035f);
            BR(m, V(0, 1.3f, .95f), V(-32f, 0, 0), V(1.9f, .08f, .55f), .01f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .75f - .05f, .22f, -.84f, s * .75f + .05f, 1.5f, .84f, .008f);
            }
            S1(m);
            B(m, -.75f, 1.0f, .8f, .75f, 1.14f, .86f, .008f);
            B(m, -.5f, .42f, .8f, .5f, .86f, .84f, .006f);
            for (int i = 0; i < 4; i++) {
                float x = -.42f + i * .28f;
                B(m, x - .03f, .22f, .84f, x + .03f, .32f, .88f, .004f);
            }
            B(m, 1.1f, 1.0f, -.4f, 1.18f, 1.4f, .4f, .006f);
        });
        Add("yd_notice_board", "yard", "wood", "paper", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * .95f - .06f, 0f, -.06f, s * .95f + .06f, 2.25f, .06f, .008f);
            }
            B(m, -1.02f, 1.22f, -.07f, 1.02f, 2.12f, .03f, .012f);
            B(m, -1.08f, 2.08f, -.1f, 1.08f, 2.16f, .08f, .01f);
            B(m, -1.08f, 1.18f, -.1f, 1.08f, 1.26f, .08f, .01f);
            BR(m, V(0, 2.3f, .02f), V(-14f, 0, 0), V(2.3f, .06f, .55f), .01f);
            S1(m);
            B(m, -.9f, 1.3f, .03f, .9f, 2.05f, .05f, .005f);
            B(m, -.82f, 1.6f, .05f, -.3f, 1.98f, .065f, .003f);
            B(m, .2f, 1.4f, .05f, .8f, 1.9f, .065f, .003f);
        });
        Add("yd_slot_frame", "yard", "stone", "lamp", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 2.05f - .35f, 0f, -.45f, s * 2.05f + .35f, 4.4f, .45f, .04f);
                B(m, s * 2.05f - .45f, 0f, -.55f, s * 2.05f + .45f, .35f, .55f, .04f);
            }
            B(m, -2.6f, 4.4f, -.5f, 2.6f, 5.1f, .5f, .04f);
            B(m, -2.7f, 5.1f, -.55f, 2.7f, 5.3f, .55f, .03f);
            B(m, -1.7f, 0f, -.6f, 1.7f, .1f, .7f, .02f);
            S1(m);
            B(m, -1.7f, 4.24f, -.1f, 1.7f, 4.4f, .1f, .008f);
            B(m, -1.7f, 0f, -.04f, 1.7f, .05f, .04f, .004f);
        });
        Add("yd_slot_shutter", "yard", "metal", "brass", m => {
            for (int i = 0; i < 12; i++) {
                float y = .02f + i * .35f;
                B(m, -1.95f, y, -.14f, 1.95f, y + .31f, .14f, .016f);
            }
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 1.88f - .07f, 0f, -.18f, s * 1.88f + .07f, 4.2f, .18f, .012f);
            }
            S1(m);
            B(m, -1.0f, 2.0f, .14f, 1.0f, 2.5f, .18f, .008f);
            B(m, -1.2f, 1.9f, .14f, -1.1f, 2.6f, .17f, .004f);
            B(m, 1.1f, 1.9f, .14f, 1.2f, 2.6f, .17f, .004f);
        });
        Add("yd_mail_flag", "yard", "metal", "cloth", m => {
            B(m, -.035f, .1f, -.035f, .035f, 1.5f, .035f, .006f);
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .12f, 0, .12f, .05f, .05f, .1f, .04f, .16f, 0, .18f), 8);
            Lat(m, V(0, 1.5f, 0), 0f, P(0, 0, .06f, 0, .06f, .03f, .03f, .06f, 0, .07f), 8);
            S1(m);
            var pennant = new List<Vector2> { new Vector2(.03f, 1.46f), new Vector2(.8f, 1.38f), new Vector2(.66f, 1.22f), new Vector2(.8f, 1.04f), new Vector2(.03f, 1.1f) };
            m.ExtrudeZ(pennant, -.02f, .02f);
        });
        Add("yd_clock_tower", "yard", "brick", "lamp", m => {
            B(m, -2.3f, 0f, -2.3f, 2.3f, .8f, 2.3f, .05f);
            B(m, -1.8f, .8f, -1.8f, 1.8f, 9f, 1.8f, .04f);
            for (int i = 0; i < 3; i++) {
                float y = 2.6f + i * 2.2f;
                B(m, -1.92f, y, -1.92f, 1.92f, y + .22f, 1.92f, .03f);
            }
            B(m, -2.15f, 9f, -2.15f, 2.15f, 9.4f, 2.15f, .04f);
            B(m, -1.9f, 9.4f, -1.9f, 1.9f, 12f, 1.9f, .04f);
            B(m, -2.15f, 12f, -2.15f, 2.15f, 12.35f, 2.15f, .04f);
            m.At(V(0, 12.35f, 0), 45f);
            m.Lathe(P(0, 0, 2.9f, 0, 0, 4.4f), 4, false);
            m.Reset();
            for (int sx = -1; sx <= 1; sx += 2) {
                for (int sz = -1; sz <= 1; sz += 2) {
                    B(m, sx * 1.95f - .18f, 12.35f, sz * 1.95f - .18f, sx * 1.95f + .18f, 13.3f, sz * 1.95f + .18f, .02f);
                }
            }
            for (int k = 0; k < 4; k++) {
                float yaw = k * 90f;
                BR(m, Quaternion.Euler(0, yaw, 0) * V(0, 10.9f, 1.99f), V(0, yaw, 0), V(.07f, .6f, .03f), .004f);
                BR(m, Quaternion.Euler(0, yaw, 0) * V(.25f, 10.7f, 1.99f), V(0, yaw, -72f), V(.07f, .5f, .03f), .004f);
            }
            S1(m);
            for (int k = 0; k < 4; k++) {
                float yaw = k * 90f;
                m.M = Matrix4x4.TRS(Quaternion.Euler(0, yaw, 0) * V(0, 10.7f, 1.92f), Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90f, 0, 0), Vector3.one);
                m.Lathe(P(0, 0, .95f, 0, .95f, .07f, 0, .07f), 14);
            }
            m.Reset();
        });
    }

    static void AddGateMore() {
        Add("gt_press_frame", "gate", "iron", "paper", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 3.5f - .45f, .2f, -.5f, s * 3.5f + .45f, 9.2f, .5f, .04f);
                B(m, s * 3.5f - .7f, 0f, -.9f, s * 3.5f + .7f, .45f, .9f, .04f);
                BR(m, V(s * 2.7f, 7.8f, 0), V(0, 0, -s * 40f), V(.22f, 1.9f, .22f), .02f);
            }
            B(m, -4.1f, 9.2f, -.7f, 4.1f, 10.2f, .7f, .04f);
            B(m, -3.8f, 0f, -2.6f, 3.8f, .16f, 2.6f, .03f);
            Lat(m, V(0, 10.2f, 0), 0f, P(0, 0, 1.0f, 0, .9f, .15f, .9f, 4.2f, .65f, 4.5f, 0, 4.6f), 12);
            S1(m);
            B(m, -3.0f, .16f, -2.0f, 3.0f, .22f, 2.0f, .01f);
            B(m, -3.4f, 9.4f, .7f, 3.4f, 9.9f, .75f, .008f);
        });
        Add("gt_press_head", "gate", "iron", "paper", m => {
            B(m, -2.8f, 0f, -2.2f, 2.8f, 1.0f, 2.2f, .08f);
            B(m, -2.5f, 1.0f, -1.8f, 2.5f, 1.35f, 1.8f, .05f);
            Lat(m, V(0, 1.35f, 0), 0f, P(0, 0, .4f, 0, .4f, 5.2f, .55f, 5.3f, .55f, 5.6f, 0, 5.6f), 12);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 2.4f - .12f, 1.0f, -.12f, s * 2.4f + .12f, 2.4f, .12f, .02f);
            }
            S1(m);
            B(m, -2.6f, -.06f, -2.0f, 2.6f, 0f, 2.0f, .02f);
        });
        Add("gt_wall_bay", "gate", "stone", "iron", m => {
            B(m, -3f, 0f, -.6f, 3f, 6.9f, .6f, .05f);
            B(m, -3.1f, 0f, -.7f, 3.1f, 1.0f, .8f, .05f);
            B(m, -3.15f, 6.9f, -.75f, 3.15f, 7.35f, .85f, .04f);
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 2.85f - .35f, 1.0f, .6f, s * 2.85f + .35f, 6.9f, 1.1f, .04f);
            }
            for (int i = 0; i < 5; i++) {
                float x = -2.7f + i * 1.35f;
                B(m, x, 7.35f, -.5f, x + .85f, 8.15f, .6f, .05f);
            }
            for (int i = 0; i < 3; i++) {
                float x = -1.7f + i * 1.7f;
                B(m, x - .1f, 3.2f, .6f, x + .1f, 4.9f, .66f, .01f);
            }
            S1(m);
            B(m, -3f, 3.3f, .6f, 3f, 3.5f, .7f, .01f);
            B(m, -3f, 5.4f, .6f, 3f, 5.55f, .68f, .01f);
        });
        Add("gt_wall_tower", "gate", "stone", "lamp", m => {
            B(m, -2.5f, 0f, -2.5f, 2.5f, .9f, 2.5f, .05f);
            B(m, -2.1f, .9f, -2.1f, 2.1f, 9.4f, 2.1f, .04f);
            B(m, -2.35f, 9.4f, -2.35f, 2.35f, 9.9f, 2.35f, .04f);
            m.At(V(0, 9.9f, 0), 45f);
            m.Lathe(P(0, 0, 3.3f, 0, 0, 3.4f), 4, false);
            m.Reset();
            for (int sx = -1; sx <= 1; sx += 2) {
                B(m, sx * 2.1f - .22f, .9f, -2.32f, sx * 2.1f + .22f, 9.4f, 2.32f, .03f);
                B(m, -2.32f, .9f, sx * 2.1f - .22f, 2.32f, 9.4f, sx * 2.1f + .22f, .03f);
            }
            S1(m);
            B(m, -.4f, 6.2f, 2.08f, .4f, 7.8f, 2.18f, .01f);
            B(m, -.4f, 3.2f, 2.08f, .4f, 4.4f, 2.16f, .01f);
        });
        Add("gt_gate_door", "gate", "iron", "brass", m => {
            var slab = new List<Vector2> { new Vector2(-2.45f, 0f), new Vector2(2.45f, 0f) };
            slab.AddRange(KitMesh.Arc(0f, 5f, 2.45f, 0f, 180f, 10));
            m.ExtrudeZ(slab, -.14f, .14f);
            S1(m);
            B(m, -.05f, .05f, .14f, .05f, 5.9f, .2f, .005f);
            for (int i = 0; i < 4; i++) {
                float y = 1.0f + i * 1.2f;
                B(m, -2.4f, y, .14f, 2.4f, y + .16f, .19f, .008f);
            }
            BR(m, V(0, 3.4f, .22f), V(0, 0, 28f), V(3.8f, .22f, .03f), .004f);
            BR(m, V(0, 3.4f, .22f), V(0, 0, -28f), V(3.8f, .22f, .03f), .004f);
        });
    }

    static void AddFloorMore() {
        Add("fl_press_frame", "floor", "iron", "metal", m => {
            for (int s = -1; s <= 1; s += 2) {
                B(m, s * 3.3f - .4f, .2f, -.45f, s * 3.3f + .4f, 8.3f, .45f, .03f);
                B(m, s * 3.3f - .65f, 0f, -.8f, s * 3.3f + .65f, .4f, .8f, .03f);
                B(m, s * 3.3f - .55f, 3.0f, -.55f, s * 3.3f + .55f, 3.2f, .55f, .02f);
            }
            B(m, -3.9f, 8.3f, -.65f, 3.9f, 9.2f, .65f, .035f);
            B(m, -3.5f, 0f, -3.1f, 3.5f, .14f, 3.1f, .025f);
            Lat(m, V(0, 9.2f, 0), 0f, P(0, 0, .95f, 0, .85f, .12f, .85f, 3.6f, .6f, 3.9f, 0, 4.0f), 10);
            S1(m);
            B(m, -3.1f, .14f, -2.7f, 3.1f, .2f, 2.7f, .008f);
            for (int i = 0; i < 6; i++) {
                B(m, -3.7f + i * 1.5f, 8.3f, .65f, -3.5f + i * 1.5f, 9.2f, .7f, .005f);
            }
        });
        Add("fl_press_head", "floor", "iron", "metal", m => {
            B(m, -2.8f, 0f, -2.6f, 2.8f, .9f, 2.6f, .07f);
            B(m, -2.4f, .9f, -2.2f, 2.4f, 1.2f, 2.2f, .04f);
            Lat(m, V(0, 1.2f, 0), 0f, P(0, 0, .38f, 0, .38f, 5.4f, .5f, 5.5f, .5f, 5.7f, 0, 5.8f), 10);
            for (int k = 0; k < 4; k++) {
                float a = k * 90f + 45f;
                BR(m, V(Mathf.Cos(a * Mathf.Deg2Rad) * 2.4f, .2f, Mathf.Sin(a * Mathf.Deg2Rad) * 2.2f), V(0, -a, 0), V(.5f, .5f, .5f), .03f);
            }
            S1(m);
            for (int i = 0; i < 7; i++) {
                B(m, -2.6f + i * .8f, -.05f, -2.3f, -2.3f + i * .8f, 0f, 2.3f, .004f);
            }
        });
        Add("fl_stack", "floor", "metal", "lamp", m => {
            B(m, -4.6f, 0f, -4.6f, 4.6f, 1.6f, 4.6f, .08f);
            Lat(m, V(0, 1.6f, 0), 0f, P(0, 0, 3.6f, 0, 3.4f, 5f, 3.1f, 12f, 2.8f, 22f, 2.6f, 30f, 3.0f, 31.2f, 3.0f, 32f, 0, 32f), 14);
            for (int i = 0; i < 4; i++) {
                float y = 5f + i * 6.4f;
                Lat(m, V(0, y, 0), 0f, P(3.2f - i * .15f, 0, 3.55f - i * .15f, 0, 3.55f - i * .15f, .5f, 3.2f - i * .15f, .5f, 3.2f - i * .15f, 0), 14);
            }
            B(m, 2.8f, 1.6f, -.25f, 3.1f, 20f, .25f, .02f);
            S1(m);
            Lat(m, V(0, 31.4f, 0), 0f, P(0, 0, 2.7f, 0, 2.7f, .4f, 0, .5f), 14);
        });
        Add("fl_lever_stand", "floor", "iron", "lamp", m => {
            B(m, -1.6f, 0f, -1.1f, 1.6f, .4f, 1.1f, .04f);
            B(m, -1.3f, .4f, -.9f, 1.3f, 3.0f, .9f, .05f);
            B(m, -1.4f, 3.0f, -1.0f, 1.4f, 3.2f, 1.0f, .04f);
            B(m, -.3f, 3.2f, -.3f, .3f, 12f, .3f, .03f);
            B(m, -1.1f, 1.2f, .9f, 1.1f, 2.4f, 1.0f, .02f);
            BR(m, V(0, 3.9f, .55f), V(0, 0, 22f), V(.2f, 2.5f, .2f), .02f);
            Lat(m, V(-.5f, 5f, .3f), 0f, P(0, 0, .22f, 0, .22f, .1f, .12f, .26f, 0, .3f), 8);
            Lat(m, V(0, 11.5f, 0), 0f, P(0, 0, 1.0f, 0, 1.0f, .5f, 0, .6f), 8);
            S1(m);
            m.Orient(V(0, 12.3f, 0), V(90f, 0, 0));
            m.Lathe(P(0, 0, 1.1f, 0, 1.1f, .35f, 0, .35f), 14);
            m.Reset();
        });
        Add("fl_hung_chassis", "floor", "metal", "iron", m => {
            B(m, -.42f, .5f, -.28f, .42f, 1.3f, .28f, .06f);
            B(m, -.3f, .1f, -.2f, .3f, .5f, .2f, .04f);
            Lat(m, V(0, 1.3f, 0), 0f, P(0, 0, .3f, 0, .3f, .08f, .22f, .26f, 0, .32f), 10);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * .56f, .95f, 0), V(0, 0, s * 18f), V(.12f, .7f, .12f), .015f);
                B(m, s * .2f - .06f, -.35f, -.06f, s * .2f + .06f, .1f, .06f, .01f);
            }
            S1(m);
            B(m, -.015f, 1.6f, -.015f, .015f, 2.6f, .015f, .003f);
            Lat(m, V(0, 2.55f, 0), 0f, P(0, 0, .05f, 0, .05f, .04f, 0, .04f), 6);
            S1(m);
            B(m, -.2f, .72f, .28f, .2f, .92f, .3f, .005f);
        });
        Add("fl_belt_hat", "floor", "cloth", "metal", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .5f, 0, .55f, .03f, .55f, .06f, .33f, .08f, .3f, .1f, .3f, .3f, .24f, .4f, 0, .42f), 14);
            S1(m);
            Lat(m, V(0, .1f, 0), 0f, P(.3f, 0, .32f, 0, .32f, .1f, .3f, .1f, .3f, 0), 14);
        });
        Add("fl_belt_clock", "floor", "metal", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .42f, 0, .42f, .18f, 0, .18f), 14);
            for (int s = -1; s <= 1; s += 2) {
                Lat(m, V(s * .26f, .18f, 0), 0f, P(0, 0, .12f, 0, .1f, .1f, 0, .14f), 8);
            }
            B(m, -.4f, 0f, -.03f, -.3f, -.0f + .14f, .03f, .006f);
            S1(m);
            m.M = Matrix4x4.TRS(V(0, .19f, 0), Quaternion.identity, Vector3.one);
            m.Lathe(P(0, 0, .34f, 0, .34f, .02f, 0, .02f), 14);
            m.Reset();
        });
        Add("fl_belt_bell", "floor", "brass", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .36f, 0, .33f, .06f, .26f, .2f, .2f, .34f, .12f, .44f, .05f, .5f, 0, .52f), 12);
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .05f, 0, .05f, .08f, 0, .08f), 8);
            S1(m);
            B(m, -.03f, .5f, -.03f, .03f, .62f, .03f, .005f);
        });
    }
}
