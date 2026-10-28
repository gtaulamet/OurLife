"""Phase 1 models: the four workstations and the cradle. Run from tools/models: python fase1.py"""
import math
import os

from lib import Model, preview, rot_x_about

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def legs(m, mat, xs, zs, height, size=0.08):
    for x in xs:
        for z in zs:
            m.box(mat, (size, height, size), (x, height / 2, z))


def carpenter_bench():
    m = Model("furniture_carpenter_bench")
    legs(m, "wood_dark", (-0.4, 0.4), (-0.23, 0.23), 0.64)
    m.box("wood", (0.94, 0.08, 0.56), (0, 0.68, 0))          # top
    m.box("wood_dark", (0.8, 0.04, 0.44), (0, 0.22, 0))      # lower shelf
    m.box("wood", (0.74, 0.03, 0.2), (0, 0.255, 0.04), rot=(0, 0, 0))  # plank on the shelf
    m.box("wood_light", (0.7, 0.03, 0.18), (0.02, 0.285, 0.06), rot=(0, 4, 0))
    # Vice on the front-left corner: jaw, screw and handle.
    m.box("metal_dark", (0.1, 0.12, 0.14), (-0.39, 0.78, 0.2))
    m.box("wood_dark", (0.06, 0.1, 0.1), (-0.39, 0.77, 0.3))
    m.cyl("metal", 0.015, 0.14, (-0.39, 0.78, 0.37), axis="z", sections=8)
    m.cyl("metal", 0.012, 0.14, (-0.39, 0.78, 0.44), axis="x", sections=8)
    # Saw lying on the bench: blade and handle.
    m.box("metal", (0.46, 0.09, 0.012), (0.1, 0.746, -0.1), rot=(0, 0, 0))
    m.box("wood_dark", (0.1, 0.08, 0.03), (-0.2, 0.75, -0.1))
    # Board being worked, with shavings around.
    m.box("wood_light", (0.4, 0.04, 0.16), (0.15, 0.74, 0.12), rot=(0, 12, 0))
    for x, z, r in ((-0.1, 0.02, 20), (0.0, 0.2, 40), (-0.32, 0.0, 10), (0.33, -0.12, 30)):
        m.box("wood_light", (0.07, 0.012, 0.035), (x, 0.726, z), rot=(0, r, 0))
    for x, z, r in ((-0.45, 0.42, 25), (0.1, 0.4, 70), (0.52, -0.3, 10)):
        m.box("wood_light", (0.07, 0.012, 0.035), (x * 0.9, 0.006, z * 0.9), rot=(0, r, 0))
    return m


def mason_bench():
    m = Model("furniture_mason_bench")
    # Two trestles: splayed legs joined by a crossbar.
    for x in (-0.32, 0.32):
        for z, rz in ((-0.17, 0), (0.17, 0)):
            m.box("wood_dark", (0.08, 0.56, 0.08), (x, 0.28, z), rot=(0, 0, 0))
        m.box("wood_dark", (0.1, 0.08, 0.5), (x, 0.58, 0))
        m.box("wood_dark", (0.06, 0.05, 0.4), (x, 0.2, 0), rot=(0, 0, 0))
    # Big rough stone slab on top.
    m.box("stone", (0.84, 0.16, 0.46), (0, 0.7, 0))
    m.box("stone_dark", (0.84, 0.03, 0.46), (0, 0.775, 0), rot=(0, 0, 0))
    # Half carved block: rough half and a smooth half with stepped edge.
    m.box("stone_dark", (0.26, 0.2, 0.26), (-0.2, 0.9, 0.0), rot=(0, 8, 0))
    m.box("stone", (0.26, 0.1, 0.26), (-0.2, 1.04, 0.0), rot=(0, 8, 0))
    m.box("stone", (0.16, 0.1, 0.26), (-0.05, 0.86, 0.0), rot=(0, 8, 0))
    # Chisel and mallet.
    m.box("metal", (0.025, 0.025, 0.28), (0.26, 0.815, 0.08), rot=(0, 20, 0))
    m.box("metal_dark", (0.03, 0.03, 0.05), (0.33, 0.815, 0.2), rot=(0, 20, 0))
    m.cyl("wood", 0.012, 0.22, (0.22, 0.81, -0.1), axis="x", sections=8, rot=(0, -25, 0))
    m.cyl("wood_dark", 0.045, 0.1, (0.32, 0.83, -0.15), axis="x", sections=10, rot=(0, -25, 0))
    # Stone chips.
    for x, z in ((0.1, 0.15), (0.0, -0.12), (0.4, 0.28), (-0.4, -0.3)):
        y = 0.79 if abs(x) < 0.4 else 0.0
        m.box("stone", (0.05, 0.03, 0.04), (x, y + 0.015, z), rot=(0, x * 100, 0))
    return m


def kitchen():
    m = Model("furniture_kitchen")
    m.box("stone_dark", (0.94, 0.1, 0.8), (0, 0.05, 0))          # base slab
    m.box("stone", (0.94, 0.4, 0.14), (0, 0.3, -0.33))           # back wall
    m.box("stone", (0.16, 0.4, 0.66), (-0.39, 0.3, 0.07))        # side walls
    m.box("stone", (0.16, 0.4, 0.66), (0.39, 0.3, 0.07))
    m.box("stone_dark", (0.94, 0.05, 0.14), (0, 0.525, -0.33))   # coping
    m.box("stone_dark", (0.16, 0.05, 0.66), (-0.39, 0.525, 0.07))
    m.box("stone_dark", (0.16, 0.05, 0.66), (0.39, 0.525, 0.07))
    # Embers and ashes.
    m.box("soot", (0.6, 0.04, 0.5), (0, 0.12, 0.05))
    for x, z, r in ((-0.12, 0.0, 15), (0.1, 0.12, 40), (0.0, -0.1, 70), (0.2, -0.05, 25)):
        m.box("ember", (0.14, 0.07, 0.1), (x, 0.17, z), rot=(0, r, 8))
    # Grill: bars across the fire.
    for z in (-0.18, -0.06, 0.06, 0.18, 0.3):
        m.box("metal_dark", (0.64, 0.025, 0.025), (0, 0.575, z))
    # Hanging rig: two posts on the side walls, crossbar, chain and pot.
    for x in (-0.39, 0.39):
        m.box("wood_dark", (0.06, 0.45, 0.06), (x, 0.775, 0.07))
    m.box("wood_dark", (0.84, 0.06, 0.06), (0, 1.0, 0.07))
    m.cyl("metal", 0.008, 0.2, (0, 0.9, 0.07), sections=6)
    m.cyl("metal_dark", 0.15, 0.2, (0, 0.7, 0.07), sections=12)          # pot body
    m.cyl("metal", 0.165, 0.03, (0, 0.8, 0.07), sections=12)             # rim
    m.cyl("soot", 0.13, 0.01, (0, 0.795, 0.07), sections=12)             # dark stew surface
    return m


def scriptorium():
    m = Model("furniture_scriptorium")
    tilt = 18
    legs(m, "wood_dark", (-0.38, 0.38), (0.26,), 0.68)
    legs(m, "wood_dark", (-0.38, 0.38), (-0.26,), 0.9)
    m.box("wood_dark", (0.8, 0.04, 0.5), (0, 0.18, 0))                # lower shelf
    m.box("wood", (0.8, 0.06, 0.06), (0, 0.5, 0.26))                  # front rail
    pivot = (0, 0.8, 0)
    T = rot_x_about(pivot, tilt)

    def on_desk(kind, *args, **kw):
        getattr(m, kind)(*args, T=T, **kw)

    # Local desk coordinates: surface at y = 0.8 + 0.03.
    top = 0.8
    on_desk("box", "wood", (0.94, 0.06, 0.62), (0, top, 0))
    on_desk("box", "wood_dark", (0.94, 0.05, 0.05), (0, top + 0.04, 0.3))   # ledge for the pen
    s = top + 0.03
    on_desk("box", "parchment", (0.42, 0.012, 0.34), (-0.1, s + 0.006, 0.0), rot=(0, 3, 0))
    on_desk("box", "ink", (0.24, 0.004, 0.012), (-0.1, s + 0.014, -0.08), rot=(0, 3, 0))
    on_desk("box", "ink", (0.2, 0.004, 0.012), (-0.1, s + 0.014, 0.0), rot=(0, 3, 0))
    on_desk("box", "ink", (0.22, 0.004, 0.012), (-0.1, s + 0.014, 0.08), rot=(0, 3, 0))
    # Open book on the right: cover plus two page blocks.
    on_desk("box", "cloth_red", (0.3, 0.02, 0.38), (0.33, s + 0.01, -0.05))
    on_desk("box", "parchment", (0.13, 0.03, 0.34), (0.26, s + 0.035, -0.05), rot=(0, 0, -6))
    on_desk("box", "parchment", (0.13, 0.03, 0.34), (0.4, s + 0.035, -0.05), rot=(0, 0, 6))
    # Inkwell and feather.
    on_desk("cyl", "ink", 0.04, 0.07, (-0.38, s + 0.035, -0.2), sections=10)
    on_desk("box", "cloth", (0.015, 0.2, 0.015), (-0.34, s + 0.15, -0.2), rot=(0, 0, -25))
    on_desk("box", "cloth", (0.05, 0.12, 0.012), (-0.3, s + 0.2, -0.2), rot=(0, 0, -25))
    return m


def cradle():
    m = Model("furniture_cradle")
    # Rockers: two curved bars made of short segments, longer axis along Z.
    n = 8
    half = 0.32
    for x in (-0.2, 0.2):
        pts = []
        for i in range(n + 1):
            z = -half + 2 * half * i / n
            pts.append((z, 0.03 + 0.07 * (z / half) ** 2))
        for (z0, y0), (z1, y1) in zip(pts, pts[1:]):
            ang = math.degrees(math.atan2(y1 - y0, z1 - z0))
            length = math.hypot(z1 - z0, y1 - y0) + 0.01
            m.box("wood_dark", (0.04, 0.045, length), (x, (y0 + y1) / 2 + 0.02, (z0 + z1) / 2), rot=(-ang, 0, 0))
    # Posts joining rockers to the body.
    for x in (-0.2, 0.2):
        for z in (-0.26, 0.26):
            y_base = 0.05 + 0.07 * (z / half) ** 2
            m.box("wood_dark", (0.04, 0.2 - y_base + 0.05, 0.04), (x, (y_base + 0.2 + 0.05) / 2 - 0.0, z))
    m.box("wood", (0.4, 0.04, 0.56), (0, 0.25, 0))                      # floor of the cradle
    # Sides leaning outwards, head end taller than foot end.
    for s in (-1, 1):
        m.box("wood", (0.035, 0.22, 0.58), (s * 0.215, 0.36, 0), rot=(0, 0, s * -10))
    m.box("wood", (0.46, 0.3, 0.035), (0, 0.4, -0.3))                   # head board
    m.box("wood", (0.46, 0.2, 0.035), (0, 0.35, 0.3))                   # foot board
    m.box("wood_dark", (0.5, 0.04, 0.045), (0, 0.56, -0.3))             # head board cap
    # Blanket.
    m.box("cloth", (0.34, 0.06, 0.46), (0, 0.3, 0.02))
    m.box("cloth_red", (0.34, 0.03, 0.2), (0, 0.345, 0.12))
    return m


MODELS = (
    ("puestos-trabajo", carpenter_bench),
    ("puestos-trabajo", mason_bench),
    ("puestos-trabajo", kitchen),
    ("puestos-trabajo", scriptorium),
    ("muebles", cradle),
)

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build()
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"))
    print("OK" if ok else "REVISAR: algun modelo no apoya en Y=0")
