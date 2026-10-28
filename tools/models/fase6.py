"""Phase 6 models: the stumps left by a felled oak and pine. Run from tools/models: python fase6.py

Modelled at their final size in world units (the trunk of the Nature Kit trees at the catalog
scale), so the catalog scale is 1.
"""
import math
import os

from lib import Model, preview

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def stump_oak():
    m = Model("stump_oak")
    m.cyl("wood_dark", 0.17, 0.2, (0, 0.1, 0), sections=10)                  # trunk
    m.cyl("wood_dark", 0.2, 0.07, (0, 0.035, 0), sections=10)                # flared base
    for i in range(5):                                                         # roots
        a = i * 72 + 15
        m.box("wood_dark", (0.15, 0.05, 0.06),
              (0.17 * math.sin(math.radians(a)), 0.02, 0.17 * math.cos(math.radians(a))), rot=(0, a - 90, 0))
    m.cyl("wood_light", 0.155, 0.012, (0, 0.206, 0), sections=10)             # cut top
    m.cyl("wood", 0.11, 0.004, (0, 0.214, 0), sections=10)                    # growth rings
    m.cyl("wood_light", 0.075, 0.004, (0, 0.218, 0), sections=10)
    m.cyl("wood", 0.035, 0.004, (0, 0.222, 0), sections=8)
    for a, h in ((20, 0.07), (140, 0.05), (250, 0.06)):                        # splinters at the rim
        m.box("wood_light", (0.03, h, 0.02),
              (0.15 * math.sin(math.radians(a)), 0.2 + h / 2, 0.15 * math.cos(math.radians(a))), rot=(0, a, 0))
    return m


def stump_pine():
    m = Model("stump_pine")
    m.cyl("wood_dark", 0.1, 0.15, (0, 0.075, 0), sections=8)                  # trunk
    m.cyl("wood_dark", 0.12, 0.05, (0, 0.025, 0), sections=8)                 # flared base
    for i in range(4):
        a = i * 90 + 30
        m.box("wood_dark", (0.14, 0.035, 0.05),
              (0.13 * math.sin(math.radians(a)), 0.015, 0.13 * math.cos(math.radians(a))), rot=(0, a - 90, 0))
    m.cyl("wood_light", 0.092, 0.012, (0, 0.156, 0), sections=8)              # cut top
    m.cyl("wood", 0.06, 0.004, (0, 0.164, 0), sections=8)
    m.cyl("wood_light", 0.03, 0.004, (0, 0.168, 0), sections=8)
    m.box("wood_light", (0.025, 0.09, 0.018), (0.085, 0.2, 0.0), rot=(0, 0, 8))   # one tall splinter
    return m


MODELS = (("entorno", stump_oak), ("entorno", stump_pine))

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build()
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"), extent=0.3)
    print("OK" if ok else "REVISAR")
