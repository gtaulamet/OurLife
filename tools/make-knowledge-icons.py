"""Draws the icons of the knowledge tree: the three branches, the four states of a knowledge and the
research mark of the HUD. 16 x 16 pixel PNGs, no smoothing, written to game/assets/icons/knowledge/<id>.png.

Run from the repository root (python tools/make-knowledge-icons.py) and import the project
afterwards (godot --headless --path game --import).
"""

import os

from PIL import Image

OUT = "game/assets/icons/knowledge"
EDGE = (58, 51, 45, 255)

# Each icon: a palette (character to colour) and 16 rows of 16 characters. "." is transparent.
ICONS = {
    # Branch of crafts: a hammer.
    "branch_crafts": ({"e": EDGE, "h": (150, 150, 158, 255), "l": (200, 200, 208, 255), "w": (160, 100, 56, 255), "d": (116, 70, 38, 255)}, [
        "................",
        "...eeeeeeeee....",
        "..ellllllhhhe...",
        "..elllllhhhhe...",
        "..ehhhhhhhhhe...",
        "...eeeewdeee....",
        "......ewde......",
        "......ewde......",
        "......ewde......",
        "......ewde......",
        "......ewde......",
        "......ewde......",
        "......ewde......",
        "......eeee......",
        "................",
        "................",
    ]),
    # Branch of the field: an ear of wheat.
    "branch_field": ({"e": EDGE, "y": (232, 190, 70, 255), "o": (188, 140, 40, 255), "g": (96, 140, 60, 255)}, [
        ".......ee.......",
        "......eyye......",
        "...ee.eyoe.ee...",
        "..eyyeeyoeeyye..",
        "..eyoe.eoe.eoye.",
        "...ee.eyye.ee...",
        "..eyyeeyoeeyye..",
        "..eyoe.eoe.eoye.",
        "...ee.eyye.ee...",
        "......eyoe......",
        ".......eg.......",
        ".......eg.......",
        ".......eg.......",
        ".......eg.......",
        ".......ee.......",
        "................",
    ]),
    # Branch of fire and metal: a flame over an ingot.
    "branch_fire_metal": ({"e": EDGE, "r": (210, 70, 40, 255), "o": (240, 150, 50, 255), "y": (250, 220, 100, 255), "h": (150, 150, 158, 255), "l": (205, 205, 212, 255)}, [
        ".......e........",
        "......ere.......",
        "......ero.......",
        ".....erroe......",
        "....errooe.ee...",
        "....erooyoeore..",
        "...erooyyoeoore.",
        "...erooyyooooye.",
        "...eroyyyyooye..",
        "....eooyyyoe....",
        ".....eeooeee....",
        "..eeeeeeeeeeee..",
        ".ellllllllllhhe.",
        ".ehhhhhhhhhhhhe.",
        "..eeeeeeeeeeee..",
        "................",
    ]),
    # Learned: a green check on a dark disc.
    "state_learned": ({"e": EDGE, "g": (92, 170, 80, 255), "w": (250, 246, 236, 255)}, [
        "................",
        "....eeeeeeee....",
        "...eggggggggge..",
        "..eggggggggwge..",
        ".egggggggggwgge.",
        ".eggggggggwggge.",
        ".eggwggggwgggge.",
        ".egggwggwggggge.",
        ".eggggwwgggggge.",
        ".egggggwgggggge.",
        "..egggggggggge..",
        "...eggggggggge..",
        "....eeeeeeee....",
        "................",
        "................",
        "................",
    ]),
    # Available: an open circle with a plus, ready to be queued.
    "state_available": ({"e": EDGE, "w": (250, 246, 236, 255), "b": (70, 130, 200, 255)}, [
        "................",
        "....eeeeeeee....",
        "...ewwwwwwwwe...",
        "..ewwwwbbwwwwe..",
        ".ewwwwwbbwwwwwe.",
        ".ewwwwwbbwwwwwe.",
        ".ewbbbbbbbbbbwe.",
        ".ewbbbbbbbbbbwe.",
        ".ewwwwwbbwwwwwe.",
        ".ewwwwwbbwwwwwe.",
        "..ewwwwbbwwwwe..",
        "...ewwwwwwwwe...",
        "....eeeeeeee....",
        "................",
        "................",
        "................",
    ]),
    # Queued: an hourglass.
    "state_queued": ({"e": EDGE, "w": (250, 246, 236, 255), "s": (232, 190, 70, 255), "b": (160, 100, 56, 255)}, [
        "................",
        "..bbbbbbbbbbbb..",
        "...ewwwwwwwwe...",
        "....ewssssse....",
        ".....ewsssse....",
        "......ewsse.....",
        ".......ese......",
        ".......ese......",
        "......ewwse.....",
        ".....ewwwsse....",
        "....ewwsssse....",
        "...ewssssssse...",
        "..bbbbbbbbbbbb..",
        "................",
        "................",
        "................",
    ]),
    # Blocked: a padlock.
    "state_blocked": ({"e": EDGE, "h": (150, 150, 158, 255), "y": (200, 150, 60, 255), "d": (140, 98, 36, 255)}, [
        "................",
        "......eeee......",
        ".....ehhhhe.....",
        "....ehe..ehe....",
        "....ehe..ehe....",
        "....ehe..ehe....",
        "..eeeeeeeeeeee..",
        "..eyyyyyyyyyye..",
        "..eyyyyddyyyye..",
        "..eyyyyddyyyye..",
        "..eyyyyyddyyye..",
        "..eyyyyyyyyyye..",
        "..eeeeeeeeeeee..",
        "................",
        "................",
        "................",
    ]),
    # Research of the HUD: an open book with a spark.
    "research": ({"e": EDGE, "w": (250, 246, 236, 255), "b": (160, 100, 56, 255), "g": (150, 140, 128, 255), "y": (250, 210, 80, 255)}, [
        ".............y..",
        "............yyy.",
        "...............y",
        "................",
        "..eeeeee.eeeeee.",
        ".ewwwwwebewwwwwe",
        ".ewggggebeggggwe",
        ".ewwwwwebewwwwwe",
        ".ewggggebeggggwe",
        ".ewwwwwebewwwwwe",
        ".ewggggebeggggwe",
        ".ewwwwwebewwwwwe",
        ".eeeeeeebeeeeeee",
        "................",
        "................",
        "................",
    ]),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (palette, rows) in ICONS.items():
        assert len(rows) == 16, name
        image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            assert len(row) == 16, (name, y, len(row))
            for x, cell in enumerate(row):
                if cell in palette:
                    image.putpixel((x, y), palette[cell])
        path = os.path.join(OUT, f"{name}.png")
        image.save(path)
        print(path)


if __name__ == "__main__":
    main()
