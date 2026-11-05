using System.Collections.Generic;
using UnityEngine;

// The rest of the cast (Docs/CharacterModels.md): Yo Bot, Spider Bot, the four bosses and the four lodgers. Each has its own
// silhouette and its own props; only the small helpers (Tube, Blob, Turn) are shared.
public static partial class CharacterKit {
    static void BotClaws(Transform model, float x, float y, Color paint, Color dark, float size) {
        for (int s = -1; s <= 1; s += 2) {
            string side = s < 0 ? "L" : "R";
            Part(model, "Claw" + side, new Vector3(s * x, y, 0f), "metal", paint, m => {
                Blob(m, Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f) * size, 8, 4);
                Tube(m, Vector3.zero, new Vector3(s * 0.02f, -0.16f, 0.04f) * size, 0.036f * size, 0.032f * size);
                Blob(m, new Vector3(s * 0.02f, -0.16f, 0.04f) * size, new Vector3(0.04f, 0.04f, 0.04f) * size, 8, 4);
                Tube(m, new Vector3(s * 0.02f, -0.16f, 0.04f) * size, new Vector3(s * 0.02f, -0.23f, 0.15f) * size, 0.032f * size, 0.028f * size);
                Blob(m, new Vector3(s * 0.02f, -0.23f, 0.15f) * size, new Vector3(0.05f, 0.05f, 0.05f) * size, 8, 4);
                for (int f = -1; f <= 1; f++) {
                    Tube(m, new Vector3(s * 0.02f, -0.23f, 0.15f) * size, new Vector3(s * 0.02f + f * 0.035f, -0.25f, 0.25f) * size, 0.018f * size, 0.01f * size, 6);
                }
            });
        }
    }

    // ---- Yo Bot -----------------------------------------------------------------------------------------------------
    // About 0.9 m: a mailbox on two feet, a wide flat lid for a head, an antenna ball, a jet pack, three-fingered claws.
    static GameObject BuildYoBot() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color paint = Hex(0xe3b43a), dark = Hex(0x2b2f3a), trim = Hex(0xf1ede0), glow = Hex(0x7fe8ff);
        Transform body = Part(model, "Body", new Vector3(0f, 0.5f, 0f), "metal", paint, m => {
            Plate(m, RoundRect(0.38f, 0.3f, 0.07f, 4), 0.28f, Vector3.zero, Vector3.zero);
            BoxAt(m, new Vector3(0f, -0.17f, 0f), new Vector3(0.26f, 0.06f, 0.18f), Vector3.zero, 0.015f);
        });
        Part(body, "Chest", Vector3.zero, "metal", trim, m => {
            BoxAt(m, new Vector3(0f, 0.0f, 0.145f), new Vector3(0.24f, 0.17f, 0.02f), Vector3.zero, 0.008f);
        });
        Part(body, "Core", Vector3.zero, "glass", glow, m => {
            Blob(m, new Vector3(0f, 0.0f, 0.16f), new Vector3(0.035f, 0.035f, 0.02f), 8, 4);
        }, 0.7f, glow * 0.8f);
        Part(body, "JetPack", new Vector3(0f, 0.02f, -0.19f), "iron", dark, m => {
            BoxAt(m, Vector3.zero, new Vector3(0.24f, 0.22f, 0.1f), Vector3.zero, 0.02f);
            Turn(m, new[] { V(0f, -0.2f), V(0.035f, -0.2f), V(0.045f, -0.1f), V(0.04f, 0f), V(0f, 0f) }, new Vector3(-0.06f, -0.05f, 0f), Vector3.zero, 8);
            Turn(m, new[] { V(0f, -0.2f), V(0.035f, -0.2f), V(0.045f, -0.1f), V(0.04f, 0f), V(0f, 0f) }, new Vector3(0.06f, -0.05f, 0f), Vector3.zero, 8);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 0.66f, 0.01f), "metal", paint, m => {
            Tube(m, Vector3.zero, new Vector3(0f, 0.05f, 0f), 0.045f, 0.045f, 8);
            Plate(m, RoundRect(0.32f, 0.17f, 0.06f, 4), 0.24f, new Vector3(0f, 0.12f, 0f), Vector3.zero);
        });
        Part(head, "Visor", Vector3.zero, "glass", glow, m => {
            BoxAt(m, new Vector3(0f, 0.13f, 0.122f), new Vector3(0.24f, 0.05f, 0.012f), Vector3.zero, 0.006f);
        }, 0.8f, glow);
        Part(head, "Antenna", new Vector3(0.09f, 0.2f, -0.03f), "iron", dark, m => {
            Tube(m, Vector3.zero, new Vector3(0.02f, 0.1f, 0f), 0.012f, 0.01f, 6);
            Blob(m, new Vector3(0.02f, 0.115f, 0f), new Vector3(0.03f, 0.03f, 0.03f), 8, 4);
        });
        BotClaws(model, 0.24f, 0.6f, paint, dark, 1f);
        for (int s = -1; s <= 1; s += 2) {
            Part(model, "Leg" + (s < 0 ? "L" : "R"), new Vector3(s * 0.1f, 0.34f, 0f), "iron", dark, m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.2f, 0f), 0.055f, 0.045f, 8);
                Blob(m, new Vector3(0f, -0.3f, 0.04f), new Vector3(0.075f, 0.04f, 0.12f), 10, 5);
            });
        }
        return root;
    }

    // Eight jointed legs around a body: front pair (1) stabs. Shared by the Spider Bot and the Server only as a routine;
    // each passes its own reach and colours, and the Server's front-left leg is replaced by the paper arm.
    static void SpiderLegs(Transform model, float reach, float yBody, Color shell, Color spike, bool paperArm) {
        float[] zs = { 0.22f, 0.07f, -0.08f };
        for (int s = -1; s <= 1; s += 2) {
            for (int i = 0; i < 3; i++) {
                string name = "Leg" + (s < 0 ? "L" : "R") + (i + 1);
                float z = zs[i] * reach;
                float fan = (i == 0 ? 0.5f : i == 1 ? 0.12f : -0.35f);
                bool paper = paperArm && s < 0 && i == 0;
                Part(model, name, new Vector3(s * 0.17f * reach, yBody, z), "iron", shell, m => {
                    Vector3 knee = new Vector3(s * 0.3f * reach, 0.2f * reach, fan * 0.3f * reach);
                    Vector3 foot = new Vector3(s * 0.52f * reach, -yBody + 0.02f, fan * 0.6f * reach + z * 0.4f);
                    if (paper) {
                        knee = new Vector3(s * 0.18f * reach, 0.16f * reach, 0.28f * reach);
                        foot = new Vector3(s * 0.12f * reach, 0.02f * reach, 0.62f * reach);
                    }
                    Blob(m, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f) * reach, 8, 4);
                    Tube(m, Vector3.zero, knee, 0.04f * reach, 0.032f * reach);
                    Blob(m, knee, new Vector3(0.055f, 0.055f, 0.055f) * reach, 8, 4);
                    Tube(m, knee, foot, 0.03f * reach, 0.012f * reach, 6);
                });
            }
        }
    }

    // ---- Spider Bot -------------------------------------------------------------------------------------------------
    static GameObject BuildSpiderBot() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color shell = Hex(0x4a505e), plate = Hex(0x646c7c), glow = Hex(0xff8a2a);
        Transform body = Part(model, "Body", new Vector3(0f, 0.42f, 0f), "stone", shell, m => {
            Blob(m, new Vector3(0f, 0.04f, -0.18f), new Vector3(0.3f, 0.19f, 0.34f), 14, 7);    // wide low abdomen
            Blob(m, new Vector3(0f, 0.0f, 0.1f), new Vector3(0.18f, 0.12f, 0.16f), 12, 6);      // thorax
        }, 0.3f);
        Part(body, "Plate", Vector3.zero, "metal", plate, m => {
            BoxAt(m, new Vector3(0f, 0.2f, -0.2f), new Vector3(0.26f, 0.03f, 0.3f), new Vector3(-6f, 0f, 0f), 0.012f);
            BoxAt(m, new Vector3(0f, 0.19f, -0.05f), new Vector3(0.2f, 0.025f, 0.05f), Vector3.zero, 0.008f);
        });
        Part(body, "Jets", new Vector3(0f, 0.05f, -0.5f), "iron", Hex(0x23262e), m => {
            Turn(m, new[] { V(0f, 0f), V(0.05f, 0f), V(0.07f, 0.1f), V(0f, 0.1f) }, new Vector3(-0.1f, 0f, 0f), new Vector3(-90f, 0f, 0f), 8);
            Turn(m, new[] { V(0f, 0f), V(0.05f, 0f), V(0.07f, 0.1f), V(0f, 0.1f) }, new Vector3(0.1f, 0f, 0f), new Vector3(-90f, 0f, 0f), 8);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 0.44f, 0.26f), "stone", shell, m => {
            Blob(m, new Vector3(0f, 0f, 0.05f), new Vector3(0.13f, 0.1f, 0.12f), 12, 6);
        }, 0.3f);
        Part(head, "Eyes", Vector3.zero, "glass", glow, m => {
            Blob(m, new Vector3(-0.05f, 0.03f, 0.15f), new Vector3(0.035f, 0.035f, 0.025f), 8, 4);
            Blob(m, new Vector3(0.05f, 0.03f, 0.15f), new Vector3(0.035f, 0.035f, 0.025f), 8, 4);
            Blob(m, new Vector3(-0.09f, 0.06f, 0.1f), new Vector3(0.02f, 0.02f, 0.02f), 6, 3);
            Blob(m, new Vector3(0.09f, 0.06f, 0.1f), new Vector3(0.02f, 0.02f, 0.02f), 6, 3);
        }, 0.8f, glow);
        SpiderLegs(model, 1f, 0.42f, Hex(0x2f333c), Hex(0xc4ccd6), false);
        return root;
    }

    // ---- The Courier ------------------------------------------------------------------------------------------------
    // A round-bellied Yo Bot postman: peaked cap, clipboard held up like a shield, a sealed notice under the other arm.
    static GameObject BuildCourier() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color blue = Hex(0x2f4d86), cream = Hex(0xf0e8cf), red = Hex(0xb3262a), dark = Hex(0x20242e), brass = Hex(0xd9b24a);
        Transform body = Part(model, "Body", new Vector3(0f, 0.8f, 0f), "cloth", blue, m => {
            Turn(m, new[] { V(0f, -0.35f), V(0.2f, -0.35f), V(0.34f, -0.15f), V(0.36f, 0.05f), V(0.28f, 0.27f), V(0.14f, 0.34f), V(0f, 0.34f) }, Vector3.zero, Vector3.zero, 14);
        });
        Part(body, "Satchel", Vector3.zero, "cloth", Hex(0x7a5232), m => {
            BoxAt(m, new Vector3(0f, 0.02f, 0.0f), new Vector3(0.05f, 0.9f, 0.04f), new Vector3(0f, 0f, 38f), 0.01f);   // the strap across the chest
            BoxAt(m, new Vector3(-0.3f, -0.18f, 0.0f), new Vector3(0.14f, 0.2f, 0.26f), Vector3.zero, 0.02f);
        });
        Part(body, "Badge", new Vector3(0.12f, 0.12f, 0.32f), "brass", brass, m => {
            Turn(m, new[] { V(0f, 0f), V(0.05f, 0f), V(0.055f, 0.012f), V(0f, 0.015f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 10);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 1.16f, 0.02f), "metal", Hex(0xcfd6e2), m => {
            Plate(m, RoundRect(0.34f, 0.24f, 0.07f, 4), 0.28f, new Vector3(0f, 0.12f, 0f), Vector3.zero);
        });
        Part(head, "Cap", Vector3.zero, "cloth", blue, m => {
            Turn(m, new[] { V(0f, 0.2f), V(0.19f, 0.2f), V(0.2f, 0.27f), V(0.14f, 0.34f), V(0f, 0.35f) }, Vector3.zero, Vector3.zero, 12);
            BoxAt(m, new Vector3(0f, 0.22f, 0.2f), new Vector3(0.3f, 0.025f, 0.14f), new Vector3(-8f, 0f, 0f), 0.01f);   // the brim
        });
        Part(head, "Visor", Vector3.zero, "glass", Hex(0x9cefff), m => {
            BoxAt(m, new Vector3(0f, 0.12f, 0.142f), new Vector3(0.25f, 0.05f, 0.012f), Vector3.zero, 0.006f);
        }, 0.8f, Hex(0x9cefff));
        BotClaws(model, 0.34f, 1.0f, blue, dark, 1.2f);
        Transform claw = model.Find("ClawL");
        Part(claw, "Prop", new Vector3(0.0f, -0.3f, 0.3f), "wood", Hex(0xb68a52), m => {
            // The clipboard: held up in front, a board, a clip and paper.
            BoxAt(m, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.4f, 0.025f), new Vector3(-12f, 0f, 0f), 0.01f);
            BoxAt(m, new Vector3(0f, 0.3f, 0.02f), new Vector3(0.1f, 0.05f, 0.03f), new Vector3(-12f, 0f, 0f), 0.008f);
        });
        Part(claw.Find("Prop"), "Paper", Vector3.zero, "paper", cream, m => {
            BoxAt(m, new Vector3(0f, 0.11f, 0.02f), new Vector3(0.26f, 0.32f, 0.006f), new Vector3(-12f, 0f, 0f), 0.002f);
        });
        Part(model.Find("ClawR"), "Prop", new Vector3(0.0f, -0.25f, 0.2f), "paper", cream, m => {
            Turn(m, new[] { V(0f, -0.2f), V(0.05f, -0.2f), V(0.05f, 0.2f), V(0f, 0.2f) }, Vector3.zero, new Vector3(80f, 0f, 20f), 10);
            Turn(m, new[] { V(0f, -0.04f), V(0.058f, -0.04f), V(0.058f, 0.04f), V(0f, 0.04f) }, new Vector3(0f, 0f, 0f), new Vector3(80f, 0f, 20f), 10);
        });
        Part(model.Find("ClawR").Find("Prop"), "Seal", Vector3.zero, "cloth", red, m => {
            Blob(m, new Vector3(0f, 0.02f, 0.05f), new Vector3(0.05f, 0.03f, 0.05f), 8, 4);
        });
        for (int s = -1; s <= 1; s += 2) {
            Part(model, "Leg" + (s < 0 ? "L" : "R"), new Vector3(s * 0.14f, 0.46f, 0f), "iron", dark, m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.3f, 0f), 0.08f, 0.065f, 8);
                Blob(m, new Vector3(0f, -0.42f, 0.05f), new Vector3(0.1f, 0.05f, 0.16f), 10, 5);
            });
        }
        return root;
    }

    // ---- The Foreman ------------------------------------------------------------------------------------------------
    // A Mega Yo Bot: a barrel with a hard hat, an order board strapped on the chest, a hydraulic hook arm.
    static GameObject BuildForeman() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color orange = Hex(0xd9701c), hat = Hex(0xe6b422), steel = Hex(0x4c5463), paper = Hex(0xf1ecdc);
        Transform body = Part(model, "Body", new Vector3(0f, 1.0f, 0f), "metal", orange, m => {
            Turn(m, new[] { V(0f, -0.5f), V(0.4f, -0.5f), V(0.5f, -0.3f), V(0.54f, 0f), V(0.48f, 0.3f), V(0.3f, 0.45f), V(0f, 0.45f) }, Vector3.zero, Vector3.zero, 16);
            for (int i = 0; i < 3; i++) {
                Turn(m, new[] { V(0.5f, -0.2f + i * 0.2f), V(0.56f, -0.2f + i * 0.2f), V(0.56f, -0.17f + i * 0.2f), V(0.5f, -0.17f + i * 0.2f) }, Vector3.zero, Vector3.zero, 16);   // barrel hoops
            }
        });
        Part(body, "Board", Vector3.zero, "wood", Hex(0x8a6a44), m => {
            BoxAt(m, new Vector3(0f, 0.02f, 0.52f), new Vector3(0.6f, 0.5f, 0.05f), Vector3.zero, 0.015f);
            BoxAt(m, new Vector3(0f, 0.3f, 0.55f), new Vector3(0.16f, 0.06f, 0.05f), Vector3.zero, 0.01f);
        });
        Part(body, "Sheet", Vector3.zero, "paper", paper, m => {
            BoxAt(m, new Vector3(0f, 0.02f, 0.555f), new Vector3(0.52f, 0.42f, 0.006f), Vector3.zero, 0.002f);
            for (int i = 0; i < 4; i++) {
                BoxAt(m, new Vector3(0f, 0.12f - i * 0.08f, 0.562f), new Vector3(0.4f - i * 0.04f, 0.02f, 0.004f), Vector3.zero, 0.001f);
            }
        });
        Part(body, "Straps", Vector3.zero, "cloth", Hex(0x2a2a30), m => {
            BoxAt(m, new Vector3(-0.2f, 0.28f, 0.2f), new Vector3(0.06f, 0.7f, 0.5f), new Vector3(0f, 0f, 0f), 0.01f);
            BoxAt(m, new Vector3(0.2f, 0.28f, 0.2f), new Vector3(0.06f, 0.7f, 0.5f), new Vector3(0f, 0f, 0f), 0.01f);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 1.46f, 0.02f), "metal", Hex(0xcfd6e2), m => {
            Plate(m, RoundRect(0.46f, 0.26f, 0.08f, 4), 0.34f, new Vector3(0f, 0.12f, 0f), Vector3.zero);
        });
        Part(head, "Hat", Vector3.zero, "metal", hat, m => {
            Turn(m, new[] { V(0f, 0.2f), V(0.28f, 0.2f), V(0.3f, 0.23f), V(0.24f, 0.36f), V(0.12f, 0.44f), V(0f, 0.45f) }, Vector3.zero, Vector3.zero, 14);
            BoxAt(m, new Vector3(0f, 0.28f, 0.0f), new Vector3(0.05f, 0.1f, 0.5f), Vector3.zero, 0.01f);   // crest ridge
        });
        Part(head, "Lamp", Vector3.zero, "glass", Hex(0xfff0b0), m => {
            Blob(m, new Vector3(0f, 0.3f, 0.26f), new Vector3(0.06f, 0.05f, 0.04f), 8, 4);
        }, 0.8f, Hex(0xffe9a0) * 0.9f);
        Part(head, "Visor", Vector3.zero, "glass", Hex(0xffd27a), m => {
            BoxAt(m, new Vector3(0f, 0.13f, 0.172f), new Vector3(0.36f, 0.06f, 0.012f), Vector3.zero, 0.006f);
        }, 0.8f, Hex(0xffa840));
        // Left arm: the clipboard he checks. Right arm: the hydraulic hook.
        Part(model, "ClawL", new Vector3(-0.62f, 1.28f, 0f), "iron", steel, m => {
            Tube(m, Vector3.zero, new Vector3(-0.04f, -0.3f, 0.12f), 0.1f, 0.08f, 8);
            Blob(m, new Vector3(-0.04f, -0.34f, 0.14f), new Vector3(0.1f, 0.1f, 0.1f), 8, 4);
        });
        Part(model.Find("ClawL"), "Prop", new Vector3(-0.04f, -0.36f, 0.2f), "wood", Hex(0xb68a52), m => {
            BoxAt(m, new Vector3(0f, 0.08f, 0.0f), new Vector3(0.22f, 0.3f, 0.025f), new Vector3(-20f, 0f, 0f), 0.008f);
        });
        Part(model, "ClawR", new Vector3(0.62f, 1.28f, 0f), "iron", steel, m => {
            Tube(m, Vector3.zero, new Vector3(0.06f, -0.3f, 0.1f), 0.1f, 0.075f, 8);
            Tube(m, new Vector3(0.06f, -0.3f, 0.1f), new Vector3(0.06f, -0.6f, 0.28f), 0.075f, 0.055f, 8);
            Blob(m, new Vector3(0.06f, -0.3f, 0.1f), new Vector3(0.1f, 0.1f, 0.1f), 8, 4);
            Plate(m, new List<Vector2>(KitMesh.Arc(0f, 0f, 0.2f, -60f, 200f, 8)).GetRange(0, 9), 0.08f, new Vector3(0.06f, -0.74f, 0.3f), new Vector3(0f, 90f, 0f));
        });
        for (int s = -1; s <= 1; s += 2) {
            Part(model, "Leg" + (s < 0 ? "L" : "R"), new Vector3(s * 0.26f, 0.52f, 0f), "iron", steel, m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.34f, 0f), 0.13f, 0.1f, 8);
                Blob(m, new Vector3(0f, -0.5f, 0.06f), new Vector3(0.16f, 0.07f, 0.24f), 10, 5);
            });
        }
        return root;
    }

    // ---- The Server -------------------------------------------------------------------------------------------------
    // A pale Spider Bot with a bow-tie plate and one long front-left leg folded out, a paper held on its flat claw.
    static GameObject BuildServer() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color shell = Hex(0xc9ccd2), dark = Hex(0x1c1d22), paper = Hex(0xf3ecd6), wax = Hex(0xb04040);
        Transform body = Part(model, "Body", new Vector3(0f, 0.7f, 0f), "plaster", shell, m => {
            Blob(m, new Vector3(0f, 0.0f, -0.3f), new Vector3(0.36f, 0.26f, 0.5f), 14, 7);
            Blob(m, new Vector3(0f, 0.0f, 0.18f), new Vector3(0.22f, 0.16f, 0.22f), 12, 6);
        }, 0.35f);
        Part(body, "BowTie", new Vector3(0f, -0.04f, 0.42f), "cloth", dark, m => {
            Plate(m, new[] { V(-0.12f, 0.06f), V(0f, 0.012f), V(0.12f, 0.06f), V(0.12f, -0.06f), V(0f, -0.012f), V(-0.12f, -0.06f) }, 0.03f, Vector3.zero, Vector3.zero);
            Blob(m, Vector3.zero, new Vector3(0.03f, 0.03f, 0.025f), 6, 3);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 0.78f, 0.42f), "plaster", shell, m => {
            Blob(m, new Vector3(0f, 0f, 0.04f), new Vector3(0.15f, 0.12f, 0.13f), 12, 6);
        }, 0.35f);
        Part(head, "Eyes", Vector3.zero, "glass", Hex(0xffffff), m => {
            Blob(m, new Vector3(-0.06f, 0.02f, 0.15f), new Vector3(0.035f, 0.04f, 0.025f), 8, 4);
            Blob(m, new Vector3(0.06f, 0.02f, 0.15f), new Vector3(0.035f, 0.04f, 0.025f), 8, 4);
        }, 0.9f, Hex(0xcfe8ff) * 0.6f);
        SpiderLegs(model, 1.6f, 0.7f, Hex(0xe4e6ea), Hex(0x3a3d46), true);
        Part(model.Find("LegL1"), "Prop", new Vector3(-0.19f, 0.03f, 0.99f), "paper", paper, m => {
            BoxAt(m, Vector3.zero, new Vector3(0.34f, 0.012f, 0.42f), new Vector3(8f, 0f, 0f), 0.004f);
            Blob(m, new Vector3(0f, 0.012f, 0.12f), new Vector3(0.04f, 0.015f, 0.04f), 8, 3);
        });
        Part(model.Find("LegL1").Find("Prop"), "Seal", Vector3.zero, "cloth", wax, m => {
            Blob(m, new Vector3(0f, 0.016f, 0.12f), new Vector3(0.035f, 0.012f, 0.035f), 8, 3);
        });
        return root;
    }

    // ---- The Order-Bearer -------------------------------------------------------------------------------------------
    // A faceless herald in a bell of paper plates, a seal-red slit, a huge scroll across its back and a stamp-hammer arm.
    static GameObject BuildOrderBearer() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color paper = Hex(0xece6d2), red = Hex(0xc71f1f), ink = Hex(0x1c1a22), plate = Hex(0xd6cfba);
        Transform body = Part(model, "Body", new Vector3(0f, 1.2f, 0f), "paper", paper, m => {
            Turn(m, new[] { V(0f, -1.2f), V(0.62f, -1.2f), V(0.5f, -0.7f), V(0.36f, -0.2f), V(0.28f, 0.2f), V(0.3f, 0.48f), V(0.2f, 0.66f), V(0f, 0.68f) }, Vector3.zero, Vector3.zero, 16);
        }, 0.15f);
        Part(body, "Bands", Vector3.zero, "paper", plate, m => {
            for (int i = 0; i < 4; i++) {
                float y = -0.9f + i * 0.34f, r = Mathf.Lerp(0.6f, 0.32f, i / 3f);
                Turn(m, new[] { V(r, y), V(r + 0.04f, y), V(r + 0.04f, y + 0.07f), V(r, y + 0.07f) }, Vector3.zero, Vector3.zero, 16);
            }
        });
        Part(body, "Seal", new Vector3(0f, 0.5f, 0.28f), "cloth", red, m => {
            Turn(m, new[] { V(0f, 0f), V(0.09f, 0f), V(0.1f, 0.02f), V(0.09f, 0.04f), V(0f, 0.04f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 12);
        }, 0.3f, red * 0.5f);
        Transform scroll = Part(body, "Scroll", new Vector3(0f, 0.1f, -0.34f), "paper", Hex(0xf3eddb), m => {
            Turn(m, new[] { V(0f, -1.05f), V(0.12f, -1.05f), V(0.12f, 1.05f), V(0f, 1.05f) }, Vector3.zero, new Vector3(0f, 0f, 38f), 12);
            Turn(m, new[] { V(0.12f, -0.9f), V(0.15f, -0.9f), V(0.15f, -0.8f), V(0.12f, -0.8f) }, Vector3.zero, new Vector3(0f, 0f, 38f), 12);
        });
        Part(scroll, "Ribbon", Vector3.zero, "cloth", red, m => {
            Turn(m, new[] { V(0.12f, -0.1f), V(0.135f, -0.1f), V(0.135f, 0.04f), V(0.12f, 0.04f) }, Vector3.zero, new Vector3(0f, 0f, 38f), 12);
        });
        Transform head = Part(model, "Head", new Vector3(0f, 1.9f, 0f), "plaster", Hex(0xf3efe2), m => {
            Tube(m, new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.04f, 0f), 0.08f, 0.07f, 8);
            Blob(m, new Vector3(0f, 0.2f, 0.02f), new Vector3(0.17f, 0.22f, 0.16f), 14, 7);   // blank, smooth, no features
        }, 0.3f);
        Part(head, "Slit", Vector3.zero, "glass", red, m => {
            BoxAt(m, new Vector3(0f, 0.2f, 0.175f), new Vector3(0.2f, 0.018f, 0.012f), Vector3.zero, 0.003f);
        }, 0.7f, red * 0.9f);
        Part(model, "ClawL", new Vector3(-0.34f, 1.7f, 0f), "paper", paper, m => {
            Tube(m, Vector3.zero, new Vector3(-0.08f, -0.4f, 0.1f), 0.07f, 0.05f, 8);
            Blob(m, new Vector3(-0.08f, -0.44f, 0.11f), new Vector3(0.065f, 0.075f, 0.065f), 8, 4);
        });
        Transform arm = Part(model, "ClawR", new Vector3(0.34f, 1.7f, 0f), "paper", paper, m => {
            Tube(m, Vector3.zero, new Vector3(0.1f, -0.38f, 0.14f), 0.07f, 0.05f, 8);
            Blob(m, new Vector3(0.1f, -0.42f, 0.15f), new Vector3(0.065f, 0.075f, 0.065f), 8, 4);
        });
        Part(arm, "Prop", new Vector3(0.1f, -0.42f, 0.15f), "iron", ink, m => {
            Tube(m, Vector3.zero, new Vector3(0f, -0.04f, 0.3f), 0.025f, 0.025f, 8);
            Turn(m, new[] { V(0f, 0f), V(0.1f, 0f), V(0.1f, 0.12f), V(0.07f, 0.14f), V(0f, 0.14f) }, new Vector3(0f, -0.04f, 0.32f), new Vector3(90f, 0f, 0f), 12);
        });
        Part(model, "LegL", new Vector3(-0.16f, 0.5f, 0f), "paper", plate, m => { Foot(m, new Vector3(0f, -0.4f, 0.08f), 0.14f, 0.24f); Tube(m, Vector3.zero, new Vector3(0f, -0.4f, 0.03f), 0.1f, 0.08f); });
        Part(model, "LegR", new Vector3(0.16f, 0.5f, 0f), "paper", plate, m => { Foot(m, new Vector3(0f, -0.4f, 0.08f), 0.14f, 0.24f); Tube(m, Vector3.zero, new Vector3(0f, -0.4f, 0.03f), 0.1f, 0.08f); });
        return root;
    }

    // ---- Lodgers ----------------------------------------------------------------------------------------------------

    // Tick: a wall clock with legs: brass rim, dark wood case, hands, a key at the back and a pendulum tail.
    static GameObject BuildTick() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color brass = Hex(0xd1a846), wood = Hex(0x5a3820), face = Hex(0xf3ead2);
        Transform body = Part(model, "Body", new Vector3(0f, 0.62f, 0f), "wood", wood, m => {
            Turn(m, new[] { V(0f, -0.1f), V(0.27f, -0.1f), V(0.3f, 0f), V(0.27f, 0.1f), V(0f, 0.1f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 18);
            Plate(m, new[] { V(-0.12f, -0.26f), V(0.12f, -0.26f), V(0.2f, -0.08f), V(-0.2f, -0.08f) }, 0.18f, new Vector3(0f, 0f, 0f), Vector3.zero);
        });
        Part(body, "Rim", Vector3.zero, "brass", brass, m => {
            Turn(m, new[] { V(0.27f, 0.09f), V(0.31f, 0.09f), V(0.31f, 0.13f), V(0.27f, 0.13f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 18);
        });
        Part(body, "Face", Vector3.zero, "paper", face, m => {
            Turn(m, new[] { V(0f, 0.1f), V(0.26f, 0.1f), V(0.26f, 0.115f), V(0f, 0.115f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 18);
            for (int i = 0; i < 12; i++) {
                float a = i * 30f * Mathf.Deg2Rad;
                BoxAt(m, new Vector3(Mathf.Sin(a) * 0.22f, Mathf.Cos(a) * 0.22f, 0.125f), new Vector3(0.016f, 0.04f, 0.01f), new Vector3(0f, 0f, -i * 30f), 0.002f);
            }
        });
        Part(body, "Hands", Vector3.zero, "iron", Hex(0x15151a), m => {
            BoxAt(m, new Vector3(0.03f, 0.05f, 0.14f), new Vector3(0.02f, 0.16f, 0.01f), new Vector3(0f, 0f, -25f), 0.003f);
            BoxAt(m, new Vector3(-0.05f, 0.02f, 0.15f), new Vector3(0.016f, 0.2f, 0.01f), new Vector3(0f, 0f, 70f), 0.003f);
            Blob(m, new Vector3(0f, 0f, 0.14f), new Vector3(0.025f, 0.025f, 0.015f), 8, 3);
        });
        Part(body, "Key", new Vector3(0f, 0.04f, -0.12f), "brass", brass, m => {
            Tube(m, Vector3.zero, new Vector3(0f, 0f, -0.14f), 0.02f, 0.02f, 8);
            BoxAt(m, new Vector3(0f, 0f, -0.16f), new Vector3(0.2f, 0.035f, 0.025f), Vector3.zero, 0.008f);
        });
        Part(body, "Tail", new Vector3(0f, -0.1f, -0.1f), "brass", brass, m => {
            Tube(m, Vector3.zero, new Vector3(0f, -0.28f, -0.04f), 0.012f, 0.012f, 6);
            Blob(m, new Vector3(0f, -0.3f, -0.04f), new Vector3(0.06f, 0.06f, 0.025f), 10, 5);
        });
        for (int s = -1; s <= 1; s += 2) {
            Part(model, "Leg" + (s < 0 ? "L" : "R"), new Vector3(s * 0.1f, 0.36f, 0f), "iron", Hex(0x1e1e24), m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.24f, 0f), 0.028f, 0.022f, 6);
                Blob(m, new Vector3(0f, -0.27f, 0.035f), new Vector3(0.05f, 0.025f, 0.08f), 8, 4);
            });
        }
        Part(model, "ArmR", new Vector3(0.3f, 0.64f, 0f), "iron", Hex(0x1e1e24), m => { Tube(m, Vector3.zero, new Vector3(0.1f, -0.06f, 0.06f), 0.022f, 0.018f, 6); });
        return root;
    }

    // Brim: a floating hat, wide brim, dented crown, blue band, a bow: it tips and turns, it has no head.
    static GameObject BuildBrim() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color felt = Hex(0x7d8088), band = Hex(0x3a62a8);
        Transform body = Part(model, "Body", new Vector3(0f, 0.7f, 0f), "cloth", felt, m => {
            Turn(m, new[] { V(0f, 0.0f), V(0.42f, -0.02f), V(0.48f, 0.04f), V(0.4f, 0.06f), V(0.22f, 0.07f), V(0.2f, 0.2f), V(0.18f, 0.3f), V(0.1f, 0.32f), V(0f, 0.28f) }, Vector3.zero, Vector3.zero, 18);
        }, 0.05f);
        Part(body, "Band", Vector3.zero, "cloth", band, m => {
            Turn(m, new[] { V(0.205f, 0.07f), V(0.225f, 0.07f), V(0.215f, 0.14f), V(0.2f, 0.14f) }, Vector3.zero, Vector3.zero, 18);
            Plate(m, new[] { V(0f, 0f), V(0.09f, 0.05f), V(0.09f, -0.05f), V(0f, 0f), V(-0.09f, 0.05f), V(-0.09f, -0.05f) }, 0.03f, new Vector3(0.2f, 0.1f, 0.12f), new Vector3(0f, 40f, 0f));
        });
        Part(body, "Feather", new Vector3(-0.2f, 0.12f, 0.02f), "paper", Hex(0xe8e0d0), m => {
            Tube(m, Vector3.zero, new Vector3(-0.05f, 0.22f, -0.05f), 0.012f, 0.004f, 6);
            Blob(m, new Vector3(-0.03f, 0.12f, -0.03f), new Vector3(0.02f, 0.1f, 0.012f), 6, 4);
        });
        Part(body, "Hover", new Vector3(0f, -0.28f, 0f), "cloth", Hex(0x1c1c22), m => {
            Blob(m, Vector3.zero, new Vector3(0.18f, 0.01f, 0.18f), 12, 3);
        }, 0.0f);
        return root;
    }

    // Spare: half a Yo Bot: one arm, one leg and a stub, a lid that sits crooked, rust paint.
    static GameObject BuildSpare() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color rust = Hex(0xb5622c), dark = Hex(0x2b2f3a), glow = Hex(0x9fe8ff);
        Transform body = Part(model, "Body", new Vector3(0f, 0.46f, 0f), "metal", rust, m => {
            Plate(m, RoundRect(0.32f, 0.28f, 0.06f, 4), 0.24f, Vector3.zero, new Vector3(0f, 0f, 4f));
            BoxAt(m, new Vector3(-0.17f, 0f, 0f), new Vector3(0.04f, 0.2f, 0.04f), Vector3.zero, 0.008f);   // the missing arm's bare socket
        });
        Part(body, "Core", Vector3.zero, "glass", glow, m => { Blob(m, new Vector3(0.02f, 0f, 0.125f), new Vector3(0.03f, 0.03f, 0.02f), 8, 4); }, 0.7f, glow * 0.7f);
        Transform head = Part(model, "Head", new Vector3(0.01f, 0.6f, 0.01f), "metal", rust, m => {
            Tube(m, Vector3.zero, new Vector3(0f, 0.04f, 0f), 0.04f, 0.04f, 8);
            Plate(m, RoundRect(0.28f, 0.15f, 0.05f, 4), 0.2f, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0f, -10f));   // the crooked lid
        });
        Part(head, "Visor", Vector3.zero, "glass", glow, m => {
            BoxAt(m, new Vector3(0f, 0.115f, 0.105f), new Vector3(0.2f, 0.04f, 0.012f), new Vector3(0f, 0f, -10f), 0.005f);
        }, 0.8f, glow);
        Part(model, "ClawR", new Vector3(0.2f, 0.54f, 0f), "metal", rust, m => {
            Tube(m, Vector3.zero, new Vector3(0.02f, -0.2f, 0.06f), 0.04f, 0.034f, 8);
            Blob(m, new Vector3(0.02f, -0.22f, 0.07f), new Vector3(0.05f, 0.05f, 0.05f), 8, 4);
        });
        Part(model.Find("ClawR"), "Prop", new Vector3(0.02f, -0.24f, 0.08f), "wood", Hex(0x8a6a44), m => {
            Tube(m, Vector3.zero, new Vector3(0f, -0.5f, 0.12f), 0.015f, 0.015f, 6);   // broom handle
            BoxAt(m, new Vector3(0f, -0.54f, 0.14f), new Vector3(0.2f, 0.06f, 0.05f), new Vector3(-12f, 0f, 0f), 0.01f);
        });
        Part(model, "ClawL", new Vector3(-0.2f, 0.54f, 0f), "metal", rust, m => {
            Tube(m, Vector3.zero, new Vector3(-0.02f, -0.07f, 0.01f), 0.04f, 0.04f, 8);   // a stub
            Blob(m, new Vector3(-0.02f, -0.08f, 0.01f), new Vector3(0.04f, 0.025f, 0.04f), 8, 3);
        });
        Part(model, "LegR", new Vector3(0.09f, 0.32f, 0f), "iron", dark, m => {
            Tube(m, Vector3.zero, new Vector3(0f, -0.18f, 0f), 0.05f, 0.04f, 8);
            Blob(m, new Vector3(0f, -0.28f, 0.04f), new Vector3(0.07f, 0.04f, 0.12f), 10, 5);
        });
        Part(model, "LegL", new Vector3(-0.09f, 0.32f, 0f), "iron", dark, m => {
            Tube(m, Vector3.zero, new Vector3(0f, -0.12f, 0f), 0.05f, 0.045f, 8);   // a short stub that never got its foot
        });
        return root;
    }

    // Chime: a small brass bell with a hanging loop, a clapper and two bright dot eyes.
    static GameObject BuildChime() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color brass = Hex(0xe0b64e);
        Transform body = Part(model, "Body", new Vector3(0f, 0.18f, 0f), "brass", brass, m => {
            Turn(m, new[] { V(0f, 0.34f), V(0.07f, 0.34f), V(0.14f, 0.3f), V(0.17f, 0.18f), V(0.2f, 0.06f), V(0.27f, -0.02f), V(0.28f, -0.06f), V(0.24f, -0.06f), V(0.2f, -0.02f), V(0f, -0.02f) }, Vector3.zero, Vector3.zero, 16);
        }, 0.6f);
        Part(body, "Loop", new Vector3(0f, 0.34f, 0f), "brass", brass, m => {
            Turn(m, new[] { V(0.05f, 0f), V(0.065f, 0f), V(0.065f, 0.12f), V(0.05f, 0.12f) }, new Vector3(0f, 0.06f, 0f), new Vector3(90f, 0f, 0f), 10);
        }, 0.6f);
        Part(body, "Clapper", new Vector3(0f, -0.02f, 0f), "iron", Hex(0x3a3228), m => {
            Tube(m, new Vector3(0f, 0.14f, 0f), new Vector3(0f, -0.04f, 0f), 0.012f, 0.012f, 6);
            Blob(m, new Vector3(0f, -0.07f, 0f), new Vector3(0.055f, 0.055f, 0.055f), 8, 4);
        });
        Part(body, "Eyes", Vector3.zero, "glass", Hex(0x1a1410), m => {
            Blob(m, new Vector3(-0.055f, 0.09f, 0.168f), new Vector3(0.022f, 0.03f, 0.012f), 8, 4);
            Blob(m, new Vector3(0.055f, 0.09f, 0.168f), new Vector3(0.022f, 0.03f, 0.012f), 8, 4);
        });
        Part(model, "Base", Vector3.zero, "cloth", Hex(0x3a62a8), m => {
            Blob(m, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.04f, 0.2f), 12, 4);
        });
        return root;
    }
}
