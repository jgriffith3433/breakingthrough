# Usage: python3 tame.py <Skin.png> [out.png]   (see Tools/Bots/README.md)
# Removes the white silhouettes and glow bleed from a bot skin in place: near-white texels (the Spider Bot's glass
# highlight and Tron frame) become pale cyan, and glow on them is capped, because Knoll/BotSkin multiplies albedo by
# glow and by _Lift, which turns white into a flat bright blob that bleeds past the mesh edge under mip filtering.
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import png
CAP = 205          # brightest allowed channel on a glowing or highlighted texel
GLOW_ON_PALE = 90  # glow ceiling for texels that were near white
def tame(px, bpp):
    changed = 0
    for i in range(0, len(px), bpp):
        r, g, b = px[i], px[i+1], px[i+2]
        lo = min(r, g, b)
        if lo > CAP - 20:
            # pull toward cyan: keep blue, drop red hardest
            px[i] = min(r, 120 + (r - 120) // 4)
            px[i+1] = min(g, CAP)
            px[i+2] = min(b, CAP + 25)
            if bpp == 4 and px[i+3] > GLOW_ON_PALE:
                px[i+3] = GLOW_ON_PALE
            changed += 1
    return changed
if __name__ == '__main__':
    src = sys.argv[1]; dst = sys.argv[2] if len(sys.argv) > 2 else src
    w, h, bpp, px = png.load(src)
    px = bytearray(px)
    n = tame(px, bpp)
    png.save(dst, w, h, bpp, px)
    print('tamed', n, 'texels in', dst)
