"""Phase 4 models: the three crops in three stages. Run from tools/models: python fase4.py

Each model fills one crop cell with many plants. The plants of a crop sit at the same spots in
its three stages, so the field reads as the same plants growing. Final size in world units.
"""
import math
import os
import random

from lib import Model, preview

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def grid(n, span, jitter, seed):
    """n x n plant spots over `span`, nudged by up to `jitter`, with a turn each."""
    rng = random.Random(seed)
    step = span / (n - 1)
    spots = []
    for i in range(n):
        for j in range(n):
            x = -span / 2 + i * step + rng.uniform(-jitter, jitter)
            z = -span / 2 + j * step + rng.uniform(-jitter, jitter)
            spots.append((x, z, rng.uniform(0, 360)))
    return spots


# ---- Wheat -------------------------------------------------------------------------------

WHEAT = grid(4, 0.72, 0.04, 11)


def wheat(stage):
    m = Model({"sprout": "crop_wheat_sprout", "growing": "crop_wheat_growing", "ripe": "crop_wheat"}[stage])
    height = {"sprout": 0.1, "growing": 0.34, "ripe": 0.5}[stage]
    for x, z, turn in WHEAT:
        for k in range(4 if stage != "sprout" else 3):
            a = turn + k * 90
            r = 0.025 if stage != "sprout" else 0.015
            px, pz = x + r * math.sin(math.radians(a)), z + r * math.cos(math.radians(a))
            h = height * (0.85 + 0.15 * ((k * 7 + int(x * 100)) % 3) / 2)
            tilt = 1.5 + 2 * (k % 2)
            if stage == "sprout":
                m.box("wheat_green", (0.012, h, 0.012), (px, h / 2, pz), rot=(tilt, a, 0))
            elif stage == "growing":
                m.box("wheat_green", (0.014, h, 0.014), (px, h / 2, pz), rot=(tilt, a, 0))
                m.box("leaf", (0.05, 0.012, 0.012), (px, h * 0.55, pz), rot=(0, a, -25))   # flag leaf
            else:
                m.box("straw", (0.014, h, 0.014), (px, h / 2, pz), rot=(tilt, a, 0))
                m.box("wheat_ear", (0.026, 0.1, 0.026), (px, h + 0.03, pz), rot=(tilt, a, 0))
    return m


# ---- Peas --------------------------------------------------------------------------------

STAKES = [(x, z) for x in (-0.3, 0.0, 0.3) for z in (-0.28, 0.28)]


def peas(stage):
    m = Model({"sprout": "crop_peas_sprout", "growing": "crop_peas_growing", "ripe": "crop_peas"}[stage])
    rng = random.Random(23)
    if stage == "sprout":
        for x, z in STAKES:
            for dx in (-0.05, 0.05):
                m.box("wheat_green", (0.012, 0.09, 0.012), (x + dx, 0.045, z), rot=(0, 0, dx * 200))
                m.box("leaf_light", (0.05, 0.012, 0.035), (x + dx, 0.09, z), rot=(0, rng.uniform(0, 180), 0))
            m.box("leaf_light", (0.012, 0.012, 0.05), (x, 0.06, z + 0.03), rot=(0, 0, 0))      # tendril
        return m
    height = 0.38 if stage == "growing" else 0.52
    for x, z in STAKES:
        m.box("wood", (0.022, height + 0.04, 0.022), (x, (height + 0.04) / 2, z), rot=(0, 0, 0))
    for y in (0.5 * height, height):     # string between the stakes
        for z in (-0.28, 0.28):
            m.box("rope", (0.62, 0.008, 0.008), (0, y, z))
    for x, z in STAKES:
        steps = 5 if stage == "growing" else 7
        for k in range(steps):
            y = 0.04 + (height - 0.04) * k / (steps - 1)
            off = 0.035 * (1 if k % 2 else -1)
            m.box("leaf", (0.07, 0.014, 0.05), (x + off, y, z + (0.02 if k % 3 else -0.02)), rot=(0, rng.uniform(0, 180), 12 * (1 if k % 2 else -1)))
            if k % 2 == 0:
                m.box("leaf_dark", (0.014, 0.05, 0.014), (x + off * 0.5, y + 0.025, z), rot=(0, 0, 0))   # curling tendril
        if stage == "ripe":
            for k, y in enumerate((0.16, 0.26, 0.36, 0.44)):
                side = 1 if k % 2 else -1
                m.box("pod", (0.022, 0.085, 0.034), (x + side * 0.05, y, z + 0.04), rot=(0, 0, side * 8))
                m.box("leaf_dark", (0.012, 0.018, 0.012), (x + side * 0.05, y + 0.05, z + 0.04))   # pod stalk
    return m


# ---- Cabbage -----------------------------------------------------------------------------

CABBAGE = grid(3, 0.6, 0.03, 5)


def cabbage(stage):
    m = Model({"sprout": "crop_cabbage_sprout", "growing": "crop_cabbage_growing", "ripe": "crop_cabbage"}[stage])
    for x, z, turn in CABBAGE:
        if stage == "sprout":
            for k in range(3):
                a = turn + k * 120
                m.box("leaf_light", (0.045, 0.01, 0.06), (x + 0.025 * math.sin(math.radians(a)), 0.03, z + 0.025 * math.cos(math.radians(a))), rot=(25, a, 0))
            m.box("wheat_green", (0.012, 0.03, 0.012), (x, 0.015, z))
        elif stage == "growing":
            for k in range(6):
                a = turn + k * 60
                r = 0.07
                m.box("leaf" if k % 2 else "leaf_dark", (0.1, 0.012, 0.13),
                      (x + r * math.sin(math.radians(a)), 0.08, z + r * math.cos(math.radians(a))), rot=(-35, a, 0))
            m.sphere("leaf_light", 0.045, (x, 0.07, z), count=(6, 8))
        else:
            m.sphere("leaf_light", 0.11, (x, 0.12, z), scale=(1, 0.9, 1), count=(6, 8))
            for k in range(5):
                a = turn + k * 72
                m.box("leaf" if k % 2 else "leaf_dark", (0.12, 0.012, 0.12),
                      (x + 0.1 * math.sin(math.radians(a)), 0.09, z + 0.1 * math.cos(math.radians(a))), rot=(-45, a, 0))
    return m


MODELS = [("cultivos", build(stage)) for build in (wheat, peas, cabbage) for stage in ("sprout", "growing", "ripe")]

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"), extent=0.5)
    print("OK" if ok else "REVISAR")
