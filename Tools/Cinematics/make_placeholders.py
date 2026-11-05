#!/usr/bin/env python3
"""Placeholder stills for the sample cutscene (no GPU, no PIL: plain zlib PNGs).
Writes Assets/Resources/Cinematics/Art/*.png. Replace them with Hawkes Gen art of the same names."""
import math, os, struct, sys, zlib

W, H = 960, 540
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Cinematics", "Art")
os.makedirs(out, exist_ok=True)

def png(path, px):
    raw = b"".join(b"\x00" + bytes(px[y * W * 4:(y + 1) * W * 4]) for y in range(H))
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))

def sky(top, bottom, alpha=255):
    px = bytearray(W * H * 4)
    for y in range(H):
        t = y / (H - 1)
        c = [int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)]
        row = bytes(c + [alpha]) * W
        px[y * W * 4:(y + 1) * W * 4] = row
    return px

def clear():
    return bytearray(W * H * 4)

def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        i = (y * W + x) * 4
        px[i:i + 4] = bytes(c)

def disc(px, cx, cy, r, c, ring=0):
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r), int(cx + r) + 1):
            d = math.hypot(x - cx, y - cy)
            if (ring == 0 and d <= r) or (ring and r - ring <= d <= r):
                put(px, x, y, c)

def rect(px, x0, y0, x1, y1, c):
    for y in range(int(y0), int(y1)):
        for x in range(int(x0), int(x1)):
            put(px, x, y, c)

def hill(px, base, amp, phase, c):
    for x in range(W):
        top = int(base + amp * math.sin(x / 150.0 + phase))
        for y in range(top, H):
            put(px, x, y, c)

INK = (150, 24, 30, 255)
GOLD = (222, 184, 96, 255)
LEATHER = (96, 58, 34, 255)
SHADOW = (14, 12, 20, 255)

# 1. The stamp in the dark (back), its hand (front, drifts faster)
p = sky((6, 6, 12), (24, 18, 30)); disc(p, 480, 200, 40, (30, 24, 40, 255)); png(f"{out}/stamp_back.png", p)
p = clear(); rect(p, 450, 40, 510, 150, (70, 52, 40, 255)); rect(p, 420, 150, 540, 200, INK); rect(p, 430, 205, 530, 215, INK); png(f"{out}/stamp_front.png", p)
# 2. Brown leather and the Hollow's grass
p = sky((20, 28, 30), (40, 56, 40)); hill(p, 330, 24, 0.0, (34, 52, 36, 255)); png(f"{out}/hollow_back.png", p)
p = clear(); rect(p, 380, 330, 580, 410, LEATHER); rect(p, 380, 330, 580, 340, (140, 96, 60, 255)); rect(p, 520, 300, 540, 336, (240, 230, 200, 255)); png(f"{out}/wallet_front.png", p)
# 3. The ring, top down, one stone at a time
p = sky((10, 14, 20), (18, 24, 28)); disc(p, 480, 270, 190, GOLD, 6)
for k in range(12):
    a = k / 12 * 2 * math.pi; disc(p, 480 + 190 * math.cos(a), 270 + 190 * math.sin(a), 11, GOLD)
png(f"{out}/ring_back.png", p)
# 4. Slice in the ring, face in shadow
p = sky((8, 8, 14), (22, 20, 28)); disc(p, 480, 270, 230, (28, 26, 36, 255), 4); png(f"{out}/slice_back.png", p)
p = clear(); rect(p, 410, 170, 550, 540, SHADOW); disc(p, 480, 150, 62, SHADOW); rect(p, 560, 330, 575, 470, (200, 200, 214, 255)); png(f"{out}/slice_front.png", p)
# 5. Wide: a Yo Bot crests the hill
p = sky((22, 26, 44), (60, 54, 70)); hill(p, 360, 30, 1.2, (28, 40, 34, 255)); hill(p, 430, 20, 0.2, (20, 30, 26, 255)); png(f"{out}/crest_back.png", p)
p = clear(); rect(p, 700, 250, 780, 330, (170, 190, 210, 255)); disc(p, 740, 235, 30, (170, 190, 210, 255)); disc(p, 728, 232, 6, (255, 220, 120, 255)); disc(p, 752, 232, 6, (255, 220, 120, 255)); png(f"{out}/crest_front.png", p)
print("wrote", out)
