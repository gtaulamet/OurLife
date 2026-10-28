"""Draws the icons of the rooms layer: the four functions of a room (bedroom, dining room, workshop,
storage) and the five levels of quality, from miserable to luxurious. 16 x 16 pixel PNGs, no
smoothing, written to game/assets/icons/rooms/<id>.png.

The colours of the levels are the same as the floor tints of the layer (RoomPalette.cs): one palette
from a dull grey-brown to gold.

Run from the repository root (python tools/make-room-icons.py) and import the project
afterwards (godot --headless --path game --import).
"""

import os

from PIL import Image

OUT = "game/assets/icons/rooms"
EDGE = (58, 51, 45, 255)

# The five levels of quality, lowest first: id and colour.
LEVELS = [
    ("miserable", (118, 108, 100, 255)),
    ("poor", (150, 128, 96, 255)),
    ("decent", (184, 146, 72, 255)),
    ("comfortable", (214, 170, 58, 255)),
    ("luxurious", (246, 198, 48, 255)),
]

# Each function: a palette (character to colour) and 16 rows of 16 characters. "." is transparent.
ROLES = {
    # Bedroom: a bed seen from the side, with a pillow.
    "role_bedroom": ({"e": EDGE, "w": (160, 100, 56, 255), "d": (116, 70, 38, 255), "p": (240, 236, 224, 255), "b": (96, 134, 190, 255), "c": (70, 104, 156, 255)}, [
        "................",
        "................",
        "................",
        "..ee............",
        "..ewe...........",
        "..ewe.eeeeeeee..",
        "..ewe.eppebbce..",
        "..eweeeppebbbce.",
        "..ewebbbbbbbbce.",
        "..ewebbbbbbbcce.",
        "..eweeeeeeeeeee.",
        "..ewe........we.",
        "..ede........de.",
        "..eee........ee.",
        "................",
        "................",
    ]),
    # Dining room: a table with two chairs.
    "role_dining": ({"e": EDGE, "w": (172, 112, 62, 255), "d": (124, 76, 40, 255), "l": (204, 148, 90, 255)}, [
        "................",
        "................",
        "................",
        "................",
        "..eeeeeeeeeeee..",
        ".ellllllllllllde",
        ".ewwwwwwwwwwwwde",
        ".eddddddddddddee",
        "..eeeeeeeeeeee..",
        "...ewe......ewe.",
        "...ewe......ewe.",
        "...ewe......ewe.",
        "...ede......ede.",
        "...eee......eee.",
        "................",
        "................",
    ]),
    # Workshop: an anvil.
    "role_workshop": ({"e": EDGE, "h": (150, 150, 158, 255), "l": (205, 205, 212, 255), "d": (96, 96, 106, 255)}, [
        "................",
        "................",
        "................",
        "..eeeeeeeeeeee..",
        ".ellllllllllhhe.",
        ".elhhhhhhhhhhee.",
        "..eeeehhhhee....",
        "......ehhde.....",
        "......ehhde.....",
        "......ehhde.....",
        ".....eehhdee....",
        "....ehhhhhhde...",
        "....eddddddde...",
        "....eeeeeeeee...",
        "................",
        "................",
    ]),
    # Storage: a chest with a lock.
    "role_storage": ({"e": EDGE, "w": (172, 112, 62, 255), "d": (124, 76, 40, 255), "l": (204, 148, 90, 255), "y": (236, 200, 90, 255)}, [
        "................",
        "................",
        "................",
        "................",
        "...eeeeeeeeee...",
        "..ellllllllllde.",
        "..ewwwwwwwwwwde.",
        "..eeeeeeyyeeeee.",
        "..ewwwwwyeywwde.",
        "..ewwwwwyyywwde.",
        "..eddddddddddde.",
        "..eeeeeeeeeeeee.",
        "................",
        "................",
        "................",
        "................",
    ]),
}


def draw(palette, rows):
    image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        assert len(row) == 16, row
        for x, char in enumerate(row):
            if char != ".":
                image.putpixel((x, y), palette[char])
    return image


def level_badge(index, color):
    """A round badge in the colour of the level with one white pip more for each step up."""
    image = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    centre = 7.5
    for y in range(16):
        for x in range(16):
            distance = ((x - centre) ** 2 + (y - centre) ** 2) ** 0.5
            if distance <= 6.2:
                image.putpixel((x, y), EDGE)
            if distance <= 5.2:
                shade = 0.8 if y > 9 else 1.0
                image.putpixel((x, y), tuple(int(c * shade) for c in color[:3]) + (255,))

    pip = (252, 248, 236, 255)
    count = index + 1
    # One to five pips in a row across the middle, two rows from the fourth.
    layout = {
        1: [(7, 7)],
        2: [(5, 7), (9, 7)],
        3: [(4, 7), (7, 7), (10, 7)],
        4: [(5, 5), (9, 5), (5, 9), (9, 9)],
        5: [(4, 5), (7, 5), (10, 5), (5, 9), (9, 9)],
    }[count]
    for px, py in layout:
        for dx in (0, 1):
            for dy in (0, 1):
                image.putpixel((px + dx, py + dy), pip)
    return image


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (palette, rows) in ROLES.items():
        draw(palette, rows).save(os.path.join(OUT, f"{name}.png"))
    for index, (name, color) in enumerate(LEVELS):
        level_badge(index, color).save(os.path.join(OUT, f"level_{name}.png"))
    print(f"{len(ROLES) + len(LEVELS)} icons in {OUT}")


if __name__ == "__main__":
    main()
