"""Draws the balloons over colonists who interact: one 16 x 16 pixel icon per interaction.

A white balloon with a tail and a symbol in it, written to game/assets/icons/interactions/<id>.png.
Run from the repository root (python tools/make-interaction-icons.py) and import the project
afterwards (godot --headless --path game --import).
"""

import os

from PIL import Image

OUT = "game/assets/icons/interactions"
INK = (58, 51, 45, 255)
WHITE = (250, 246, 236, 255)
EDGE = (90, 80, 70, 255)

# The balloon: 1 is the edge, 2 its inside; the last rows are its tail.
BALLOON = [
    "..111111111111..",
    ".12222222222221.",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    "1222222222222221",
    ".12222222222221.",
    "..111112211111..",
    "......1221......",
    ".......11.......",
    "................",
]

# Each symbol is drawn over the inside of the balloon, from its top left corner (row 2, column 3).
SYMBOLS = {
    "chat": ({"#": INK}, [
        "..........",
        "..........",
        "..........",
        "##..##..##",
        "##..##..##",
    ]),
    "joke": ({"#": INK}, [
        "..........",
        "..#....#..",
        "..#....#..",
        "..........",
        ".#......#.",
        "..#....#..",
        "...####...",
    ]),
    "teach": ({"#": (120, 72, 40, 255), "o": WHITE, "-": (150, 140, 128, 255)}, [
        "..........",
        "##......##",
        "#o##..##o#",
        "#o--##--o#",
        "#o--oo--o#",
        "#o--oo--o#",
        "##########",
    ]),
    "argue": ({"#": (200, 60, 40, 255)}, [
        ".....##...",
        "....##....",
        "...##.....",
        "..######..",
        ".....##...",
        "....##....",
        "...##.....",
    ]),
    "comfort": ({"#": (232, 140, 160, 255)}, [
        "..........",
        "..##..##..",
        ".########.",
        ".########.",
        "..######..",
        "...####...",
        "....##....",
    ]),
    "flirt": ({"#": (200, 40, 60, 255)}, [
        ".##....##.",
        "####..####",
        "##########",
        "##########",
        ".########.",
        "..######..",
        "....##....",
    ]),
    "play": ({"#": (210, 60, 50, 255), "y": (240, 200, 70, 255)}, [
        "...####...",
        "..#yy###..",
        ".#yyy####.",
        ".##yy####.",
        ".#####yy#.",
        "..####yy..",
        "...####...",
    ]),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (colours, rows) in SYMBOLS.items():
        image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        for y, row in enumerate(BALLOON):
            for x, cell in enumerate(row):
                if cell == "1":
                    image.putpixel((x, y), EDGE)
                elif cell == "2":
                    image.putpixel((x, y), WHITE)
        for y, row in enumerate(rows):
            for x, cell in enumerate(row):
                if cell in colours:
                    image.putpixel((3 + x, 2 + y), colours[cell])
        path = os.path.join(OUT, f"{name}.png")
        image.save(path)
        print(path)


if __name__ == "__main__":
    main()
