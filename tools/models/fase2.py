"""Phase 2 models: food and material items. Run from tools/models: python fase2.py

Items are modelled at their final size in world units (0,15 to 0,7), so the catalog scale is 1.
"""
import math
import os

from lib import Model, preview

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def cabbage():
    m = Model("item_cabbage")
    m.sphere("leaf", 0.1, (0, 0.09, 0), scale=(1, 0.9, 1))
    m.sphere("leaf_light", 0.075, (0, 0.12, 0), scale=(1, 0.9, 1))
    for i in range(5):
        a = i * 72
        x, z = 0.09 * math.sin(math.radians(a)), 0.09 * math.cos(math.radians(a))
        m.box("leaf_dark", (0.1, 0.012, 0.07), (x, 0.045, z), rot=(0, a, 0))
    return m


def peas():
    m = Model("item_peas")
    # Three pods lying in a bunch, with round peas showing at the open end.
    for i, (x, z, r) in enumerate(((-0.05, 0.0, 15), (0.0, 0.03, -10), (0.05, -0.02, 35))):
        m.box("leaf", (0.04, 0.035, 0.18), (x, 0.0175 + 0.02 * (i == 1), z), rot=(0, r, 0))
        m.box("leaf_dark", (0.045, 0.012, 0.012), (x + 0.01, 0.05 + 0.02 * (i == 1), z + 0.09), rot=(0, r, 0))
    for x, z in ((-0.1, 0.1), (-0.07, 0.12), (-0.12, 0.13)):
        m.sphere("pea", 0.022, (x, 0.022, z))
    return m


def bread():
    m = Model("item_bread")
    m.sphere("crust", 0.13, (0, 0.04, 0), scale=(1, 0.55, 1))
    m.sphere("crumb", 0.075, (0, 0.07, 0), scale=(1, 0.4, 1))
    for a in (0, 60, 120):
        m.box("crumb", (0.15, 0.012, 0.02), (0, 0.098, 0), rot=(0, a, 0))
    return m


def pottage():
    m = Model("item_pottage")
    m.cyl("clay", 0.13, 0.04, (0, 0.02, 0), sections=12)
    m.sphere("clay", 0.13, (0, 0.11, 0), scale=(1, 0.85, 1))
    m.cyl("clay", 0.1, 0.05, (0, 0.2, 0), sections=12)
    m.cyl("stew", 0.085, 0.012, (0, 0.226, 0), sections=12)
    for x, z in ((-0.03, 0.02), (0.03, -0.03), (0.02, 0.04)):
        m.sphere("leaf", 0.014, (x, 0.235, z))
    for s in (-1, 1):
        m.box("clay", (0.04, 0.03, 0.025), (s * 0.145, 0.15, 0), rot=(0, 0, 0))
    return m


def pickled_cabbage():
    m = Model("item_pickled_cabbage")
    m.cyl("clay_light", 0.1, 0.24, (0, 0.12, 0), sections=12)
    m.cyl("clay_light", 0.085, 0.03, (0, 0.255, 0), sections=12)
    m.cyl("brine", 0.075, 0.012, (0, 0.268, 0), sections=12)
    m.cyl("rope", 0.104, 0.02, (0, 0.18, 0), sections=12)
    m.cyl("rope", 0.104, 0.02, (0, 0.07, 0), sections=12)
    m.cyl("wood_dark", 0.09, 0.02, (0, 0.285, 0), sections=12, rot=(8, 0, 0))   # slightly askew lid
    return m


def wheat():
    m = Model("item_wheat")
    n = 9
    for i in range(n):
        a = i * 360 / n
        tilt = 7 + 5 * (i % 3)
        m.cyl("straw", 0.008, 0.36, (0, 0.18, 0), sections=5, rot=(tilt, a, 0))
    for i in range(n):
        a = i * 360 / n
        r = 0.045 + 0.02 * (i % 3)
        x, z = r * math.sin(math.radians(a)), r * math.cos(math.radians(a))
        m.box("crumb", (0.022, 0.07, 0.022), (x, 0.38, z), rot=(0, a, 0))
    m.cyl("rope", 0.032, 0.03, (0, 0.17, 0), sections=8)
    # Gavilla lying: tilt the whole sheaf by lowering to the ground afterwards.
    return m


def flour():
    m = Model("item_flour")
    m.sphere("cloth", 0.14, (0, 0.13, 0), scale=(1, 1.05, 0.85))
    m.cyl("cloth", 0.065, 0.1, (0, 0.28, 0), sections=8)
    m.cyl("rope", 0.07, 0.025, (0, 0.24, 0), sections=8)
    m.sphere("cloth", 0.06, (0, 0.33, 0), scale=(1, 0.6, 1))
    m.box("flour", (0.09, 0.012, 0.07), (0.0, 0.2, 0.115))        # flour dusted on the front
    return m


def provisions():
    m = Model("item_provisions")
    m.cyl("wicker", 0.2, 0.2, (0, 0.1, 0), sections=14)
    for y in (0.06, 0.13):
        m.cyl("wood_dark", 0.205, 0.012, (0, y, 0), sections=14)
    # Mixed food: loaves, a cabbage, an apple and a wheat sheaf on top.
    m.sphere("crust", 0.09, (-0.07, 0.24, 0.02), scale=(1, 0.6, 0.8))
    m.sphere("leaf", 0.075, (0.07, 0.26, -0.04))
    m.sphere("apple", 0.045, (0.04, 0.25, 0.1))
    m.box("straw", (0.03, 0.03, 0.2), (-0.03, 0.26, -0.09), rot=(0, 25, 0))
    m.sphere("crumb", 0.04, (-0.1, 0.27, -0.1), scale=(1, 0.5, 1))
    return m


def plank():
    m = Model("item_plank")
    m.box("wood_light", (0.7, 0.035, 0.14), (0, 0.0175, 0))
    m.box("wood", (0.7, 0.012, 0.14), (0, 0.041, 0))
    for x in (-0.15, 0.12):
        m.box("wood_dark", (0.012, 0.004, 0.1), (x, 0.049, 0), rot=(0, 0, 0))
    return m


def handle():
    m = Model("item_handle")
    m.cyl("wood_light", 0.022, 0.42, (0, 0.022, 0), axis="z", sections=8)
    m.cyl("wood", 0.03, 0.04, (0, 0.022, 0.2), axis="z", sections=8)      # fatter end
    m.cyl("wood", 0.03, 0.04, (0, 0.022, -0.2), axis="z", sections=8)
    return m


MODELS = (
    ("objetos/comida", cabbage), ("objetos/comida", peas), ("objetos/comida", bread),
    ("objetos/comida", pottage), ("objetos/comida", pickled_cabbage), ("objetos/comida", wheat),
    ("objetos/comida", flour), ("objetos/comida", provisions),
    ("objetos/materiales", plank), ("objetos/materiales", handle),
)

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build()
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"), extent=0.4)
    print("OK" if ok else "REVISAR")
