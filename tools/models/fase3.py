"""Phase 3 models: stone, iron, charcoal and the four iron tools. Run from tools/models: python fase3.py

Items are modelled at their final size in world units, so the catalog scale is 1. Tools lie flat
on the ground so they read well from the tilted camera. The iron parts use the `iron` material
so the catalog can recolour them; the handles use `wood`.
"""
import os

from lib import Model, preview

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def stone_block():
    m = Model("item_stone_block")
    m.box("stone", (0.34, 0.17, 0.2), (0, 0.085, 0))
    m.box("stone_light", (0.34, 0.025, 0.2), (0, 0.18, 0))                 # dressed top face
    m.box("stone_dark", (0.34, 0.012, 0.012), (0, 0.1, 0.103))             # chisel line on the front
    m.box("stone_dark", (0.012, 0.012, 0.2), (0.172, 0.1, 0))
    m.box("stone_dark", (0.05, 0.03, 0.02), (-0.16, 0.185, 0.07), rot=(0, 20, 0))   # tool marks
    m.box("stone_dark", (0.05, 0.03, 0.02), (-0.1, 0.185, 0.0), rot=(0, 20, 0))
    return m


def iron_ore():
    m = Model("item_iron_ore")
    m.sphere("stone_dark", 0.14, (0, 0.1, 0), scale=(1.1, 0.8, 1))
    m.sphere("stone", 0.1, (0.1, 0.07, 0.06), scale=(1, 0.8, 1))
    m.sphere("stone_dark", 0.08, (-0.1, 0.06, 0.07))
    # Rusty veins and nuggets on the faces.
    for x, y, z, r in ((0.0, 0.17, 0.0, 20), (0.1, 0.12, 0.1, -30), (-0.12, 0.1, 0.1, 50), (0.04, 0.1, 0.14, 10)):
        m.box("rust", (0.09, 0.025, 0.035), (x, y, z), rot=(15, r, 10))
    m.sphere("rust", 0.03, (0.1, 0.14, -0.05))
    m.sphere("rust", 0.025, (-0.06, 0.15, 0.05))
    return m


def iron_ingot():
    m = Model("item_iron_ingot")
    # Trapezoidal bar: wide base, narrower top, rounded by a smaller second layer.
    m.box("iron", (0.3, 0.05, 0.11), (0, 0.025, 0))
    m.box("iron", (0.26, 0.035, 0.08), (0, 0.0675, 0))
    m.box("iron_dark", (0.3, 0.012, 0.11), (0, 0.006, 0))                    # shadowed foot
    m.box("iron_dark", (0.012, 0.03, 0.06), (0.1, 0.075, 0))                  # stamp
    return m


def charcoal():
    m = Model("item_charcoal")
    chunks = (
        ((0.12, 0.09, 0.1), (0.0, 0.045, 0.0), (0, 20, 12)),
        ((0.1, 0.07, 0.09), (0.12, 0.035, 0.05), (10, -30, 0)),
        ((0.09, 0.08, 0.08), (-0.11, 0.04, 0.06), (0, 55, 15)),
        ((0.08, 0.06, 0.1), (0.03, 0.03, -0.12), (-8, 10, 0)),
        ((0.07, 0.05, 0.06), (-0.05, 0.1, 0.0), (20, 40, 25)),
        ((0.06, 0.05, 0.07), (0.07, 0.1, 0.02), (0, -15, 20)),
    )
    for size, at, rot in chunks:
        m.box("charcoal", size, at, rot=rot)
    for at in ((0.0, 0.1, 0.0), (0.14, 0.075, 0.04)):
        m.box("stone_dark", (0.03, 0.012, 0.025), at, rot=(0, 30, 0))       # grey ash edge
    return m


def _handle(m, length, radius=0.018, z0=0.0):
    m.cyl("wood", radius, length, (0, radius, z0), axis="z", sections=8)


def iron_axe():
    m = Model("item_iron_axe")
    _handle(m, 0.44, 0.02)
    m.box("iron_dark", (0.07, 0.05, 0.07), (0, 0.025, 0.2))                 # eye
    m.box("iron", (0.045, 0.045, 0.06), (-0.05, 0.0225, 0.2))               # poll
    m.box("iron", (0.07, 0.03, 0.1), (0.075, 0.015, 0.2))                   # cheek
    m.box("iron", (0.05, 0.028, 0.15), (0.13, 0.014, 0.2))                  # flared blade
    m.box("iron_dark", (0.012, 0.03, 0.17), (0.16, 0.015, 0.2))             # sharp edge
    m.box("wood", (0.04, 0.04, 0.04), (0, 0.02, -0.22))                     # knob at the butt
    return m


def iron_pickaxe():
    m = Model("item_iron_pickaxe")
    _handle(m, 0.46, 0.02)
    m.box("iron_dark", (0.07, 0.055, 0.06), (0, 0.0275, 0.2))               # eye
    for s in (-1, 1):
        m.box("iron", (0.1, 0.04, 0.045), (s * 0.075, 0.02, 0.2), rot=(0, 0, 0))
        m.box("iron", (0.08, 0.03, 0.03), (s * 0.15, 0.015, 0.215), rot=(0, s * -22, 0))
        m.box("iron_dark", (0.05, 0.022, 0.02), (s * 0.205, 0.011, 0.235), rot=(0, s * -35, 0))   # point
    return m


def iron_hoe():
    m = Model("item_iron_hoe")
    _handle(m, 0.5, 0.02)
    m.box("iron_dark", (0.05, 0.05, 0.07), (0, 0.025, 0.22))                # socket
    m.box("iron", (0.04, 0.03, 0.09), (0.04, 0.03, 0.26), rot=(0, 25, 20))   # neck bent towards the blade
    m.box("iron", (0.13, 0.02, 0.1), (0.1, 0.05, 0.31), rot=(0, 0, 28))      # flat blade, angled
    m.box("iron_dark", (0.13, 0.022, 0.012), (0.1, 0.05, 0.365), rot=(0, 0, 28))   # edge
    return m


def iron_hammer():
    m = Model("item_iron_hammer")
    _handle(m, 0.3, 0.02, z0=-0.02)
    m.box("iron", (0.17, 0.08, 0.075), (0, 0.04, 0.13))                      # head across the handle
    for s in (-1, 1):
        m.box("iron_dark", (0.02, 0.09, 0.085), (s * 0.095, 0.045, 0.13))    # striking faces
    m.box("wood", (0.035, 0.035, 0.035), (0, 0.0175, -0.18))
    return m


MODELS = (
    ("objetos/materiales", stone_block), ("objetos/materiales", iron_ore),
    ("objetos/materiales", iron_ingot), ("objetos/materiales", charcoal),
    ("objetos/herramientas", iron_axe), ("objetos/herramientas", iron_pickaxe),
    ("objetos/herramientas", iron_hoe), ("objetos/herramientas", iron_hammer),
)

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build()
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"), extent=0.3)
    print("OK" if ok else "REVISAR")
