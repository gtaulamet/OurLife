"""Writes the elder villagers: the Mini Characters of Kenney (CC0) with grey hair.

For each character-<sex>-<letter>.glb of the kit, writes character-<sex>-<letter>-elder.glb next
to it. The head mesh of the kit samples one texture: its hair vertices (those in the bottom row of
stripes that are neither the face's skin nor the eyes, told apart by where they are) move to the warm grey stripe, keeping their
place along it, so the hair keeps its shading. Nothing else changes. Run from the repository root:

    python tools/make-elder-models.py

and import the project afterwards (godot --headless --path game --import).
"""

import collections
import glob
import json
import os
import struct

KIT = "game/assets/models/kenney_mini-characters"
TEXTURE = 512
STRIPE = 32
BOTTOM_ROW = 384  # The stripes of hair, skin and eyes start here, in texture pixels.
GREY_STRIPE = 7  # A warm grey from #BBB4A1 to #757064.
EYES_STRIPE = 1
MIN_HAIR_VERTICES = 30  # Fewer, in a stripe, are a mouth or a detail, not hair.
SKIN = sorted([106, 16, 16, 82])  # Vertices of the face per row of the skin stripe.


def read_glb(path):
    data = open(path, "rb").read()
    length = struct.unpack("<I", data[12:16])[0]
    gltf = json.loads(data[20:20 + length])
    binary_start = 20 + length + 8
    return data, gltf, binary_start


def head_vertices(data, gltf, binary_start):
    """(offset of its UV in the file, u, v, position) of every vertex of the head mesh, u and v from 0 to 1."""
    head = next(node for node in gltf["nodes"] if node.get("name") == "head-mesh")

    def read(accessor_index, components):
        accessor = gltf["accessors"][accessor_index]
        view = gltf["bufferViews"][accessor["bufferView"]]
        start = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", 4 * components)
        return [(start + index * stride, struct.unpack("<" + "f" * components, data[start + index * stride:start + index * stride + 4 * components]))
                for index in range(accessor["count"])]

    vertices = []
    for primitive in gltf["meshes"][head["mesh"]]["primitives"]:
        uvs = read(primitive["attributes"]["TEXCOORD_0"], 2)
        positions = read(primitive["attributes"]["POSITION"], 3)
        for (offset, (u, v)), (_, position) in zip(uvs, positions):
            vertices.append((offset, u, v, tuple(round(value, 3) for value in position)))
    return vertices


def stripe_of(u):
    return int(u * TEXTURE) // STRIPE


def in_bottom_row(v):
    return v * TEXTURE >= BOTTOM_ROW


def main():
    files = sorted(path for path in glob.glob(os.path.join(KIT, "character-*.glb")) if not path.endswith("-elder.glb"))

    # The eyes share their stripe, and even their texture points, with dark hair, but not their
    # place: they are where the vertices of that stripe are in the characters that have few
    # there, and so no hair in it. Every head of the kit has its eyes in the same place.
    eyes = set()
    for path in files:
        data, gltf, start = read_glb(path)
        points = [position for _, u, v, position in head_vertices(data, gltf, start)
                  if stripe_of(u) == EYES_STRIPE and in_bottom_row(v)]
        if len(points) < MIN_HAIR_VERTICES:
            eyes.update(points)

    for path in files:
        data, gltf, start = read_glb(path)
        out = bytearray(data)
        vertices = head_vertices(data, gltf, start)

        rows = collections.defaultdict(collections.Counter)
        for _, u, v, _ in vertices:
            if in_bottom_row(v):
                rows[stripe_of(u)][int(v * TEXTURE) // STRIPE] += 1
        skin = [stripe for stripe, counts in rows.items() if sorted(counts.values()) == SKIN]
        if len(skin) != 1:
            raise SystemExit(f"{path}: cannot tell the skin stripe from {dict(rows)}")

        def is_eye(u, position):
            return stripe_of(u) == EYES_STRIPE and position in eyes

        hair = set()
        for stripe, counts in rows.items():
            total = sum(counts.values())
            if stripe == EYES_STRIPE:
                total -= sum(1 for _, u, v, position in vertices if in_bottom_row(v) and is_eye(u, position))
            if stripe != skin[0] and total >= MIN_HAIR_VERTICES:
                hair.add(stripe)

        grey_u = (GREY_STRIPE * STRIPE + STRIPE / 2) / TEXTURE
        moved = 0
        for offset, u, v, position in vertices:
            if not in_bottom_row(v) or stripe_of(u) not in hair or is_eye(u, position):
                continue
            out[offset:offset + 4] = struct.pack("<f", grey_u)
            moved += 1

        target = path[:-4] + "-elder.glb"
        open(target, "wb").write(bytes(out))
        print(f"{os.path.basename(target)}: hair in stripes {sorted(hair)}, {moved} vertices to grey")


if __name__ == "__main__":
    main()
