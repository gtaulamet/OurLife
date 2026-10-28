"""Phase 5 models: the basic furniture (table, bed, chair, chest). Run from tools/models: python fase5.py

Modelled at their final size in world units, so the catalog scale is 1. Front towards +Z; the
bed runs along Z with the headboard at -Z; the chair faces +Z with its back at -Z.
"""
import os

from lib import Model, preview

# The generated .glb go to MODELS/ (not versioned); the ones the game uses are copied to game/assets/models/ourlife.
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "MODELS"))
PREVIEWS = os.path.join(os.path.dirname(__file__), "previews")


def table():
    m = Model("furniture_table")
    for x in (-0.28, 0.28):
        for z in (-0.28, 0.28):
            m.box("wood_dark", (0.07, 0.5, 0.07), (x, 0.25, z))
    for x, w in ((-0.2, 0.21), (0.0, 0.21), (0.2, 0.21)):                 # three boards
        m.box("wood", (w - 0.01, 0.06, 0.7), (x, 0.53, 0))
    for z in (-0.2, 0.2):                                                    # battens under the top
        m.box("wood_dark", (0.6, 0.04, 0.05), (0, 0.48, z))
    m.box("wood_dark", (0.5, 0.035, 0.035), (0, 0.15, 0.0))                  # stretcher
    return m


def bed():
    m = Model("furniture_bed")
    for x in (-0.23, 0.23):
        for z in (-0.45, 0.45):
            m.box("wood_dark", (0.06, 0.12, 0.06), (x, 0.06, z))
    m.box("wood", (0.52, 0.06, 0.98), (0, 0.13, 0))                          # frame
    m.box("wood_dark", (0.06, 0.1, 0.98), (-0.23, 0.18, 0))                  # side rails
    m.box("wood_dark", (0.06, 0.1, 0.98), (0.23, 0.18, 0))
    m.box("wood", (0.52, 0.34, 0.05), (0, 0.2, -0.465))                      # headboard
    m.box("wood_dark", (0.56, 0.04, 0.07), (0, 0.39, -0.465))
    m.box("wood", (0.52, 0.16, 0.05), (0, 0.13, 0.465))                      # footboard
    m.box("straw", (0.4, 0.09, 0.86), (0, 0.19, 0.01))                       # straw mattress
    m.box("straw", (0.1, 0.02, 0.1), (0.14, 0.24, 0.3), rot=(0, 30, 0))       # loose straw
    m.box("cloth", (0.3, 0.06, 0.2), (0, 0.255, -0.32))                      # pillow
    m.box("cloth_red", (0.42, 0.04, 0.48), (0, 0.235, 0.14))                 # blanket
    return m


def chair():
    m = Model("furniture_chair")
    for x in (-0.15, 0.15):
        for z in (-0.15, 0.15):
            m.box("wood_dark", (0.055, 0.3, 0.055), (x, 0.15, z))
    m.box("wood", (0.4, 0.05, 0.4), (0, 0.325, 0))                           # seat
    for x in (-0.15, 0.15):                                                    # back posts
        m.box("wood_dark", (0.055, 0.36, 0.055), (x, 0.48, -0.15))
    for y in (0.5, 0.62):                                                      # back slats
        m.box("wood", (0.32, 0.07, 0.03), (0, y, -0.15))
    m.box("wood_dark", (0.3, 0.03, 0.03), (0, 0.12, 0.15))                    # front stretcher
    return m


def chest():
    m = Model("furniture_chest")
    m.box("wood", (0.7, 0.3, 0.45), (0, 0.15, 0))                             # body
    m.box("wood", (0.7, 0.07, 0.43), (0, 0.335, 0))                           # lid, built as a low vault
    m.box("wood", (0.7, 0.07, 0.36), (0, 0.38, 0), rot=(0, 0, 0))
    m.box("wood", (0.7, 0.05, 0.24), (0, 0.42, 0))
    for x in (-0.22, 0.22):                                                    # iron bands
        m.box("metal_dark", (0.07, 0.31, 0.47), (x, 0.155, 0))
        m.box("metal_dark", (0.07, 0.08, 0.45), (x, 0.36, 0))
        m.box("metal_dark", (0.07, 0.07, 0.3), (x, 0.405, 0))
    m.box("metal", (0.1, 0.1, 0.03), (0, 0.3, 0.235))                          # lock plate
    m.box("ink", (0.025, 0.04, 0.035), (0, 0.29, 0.245))                       # keyhole
    for x, z in ((-0.33, -0.2), (0.33, -0.2), (-0.33, 0.2), (0.33, 0.2)):      # corner studs
        m.box("metal_dark", (0.04, 0.04, 0.04), (x, 0.03, z))
    return m


MODELS = (("muebles", table), ("muebles", bed), ("muebles", chair), ("muebles", chest))

if __name__ == "__main__":
    ok = True
    for folder, build in MODELS:
        m = build()
        m.ground()
        ok &= m.export(os.path.join(ROOT, folder, m.name + ".glb"))
        preview(m, os.path.join(PREVIEWS, m.name + ".png"), extent=0.5)
    print("OK" if ok else "REVISAR")
