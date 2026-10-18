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


FLOAT = 5126  # glTF's componentType for 32-bit floats.


def head_vertices(data, gltf, binary_start):
    """(offset of its UV in the file, u, v, position, UV accessor) of every vertex of the head mesh, u and v from 0 to 1."""
    head = next(node for node in gltf["nodes"] if node.get("name") == "head-mesh")

    def read(accessor_index, components):
        accessor = gltf["accessors"][accessor_index]
        if accessor["componentType"] != FLOAT or accessor.get("normalized"):
            raise SystemExit(f"accessor {accessor_index}: only plain 32-bit floats are read, found componentType {accessor['componentType']}")
        view = gltf["bufferViews"][accessor["bufferView"]]
        start = binary_start + view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", 4 * components)
        return [(start + index * stride, struct.unpack("<" + "f" * components, data[start + index * stride:start + index * stride + 4 * components]))
                for index in range(accessor["count"])]

    vertices = []
    for primitive in gltf["meshes"][head["mesh"]]["primitives"]:
        uv_accessor = primitive["attributes"]["TEXCOORD_0"]
        uvs = read(uv_accessor, 2)
        positions = read(primitive["attributes"]["POSITION"], 3)
        for (offset, (u, v)), (_, position) in zip(uvs, positions):
            vertices.append((offset, u, v, tuple(round(value, 3) for value in position), uv_accessor))
    return vertices


def write_glb(path, gltf, binary_chunk):
    """A .glb with this JSON and the binary chunk as it was, header included."""
    text = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    text += b" " * (-len(text) % 4)
    body = struct.pack("<II", len(text), 0x4E4F534A) + text + binary_chunk
    open(path, "wb").write(struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body)


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
        points = [position for _, u, v, position, _ in head_vertices(data, gltf, start)
                  if stripe_of(u) == EYES_STRIPE and in_bottom_row(v)]
        if len(points) < MIN_HAIR_VERTICES:
            eyes.update(points)

    for path in files:
        data, gltf, start = read_glb(path)
        out = bytearray(data)
        vertices = head_vertices(data, gltf, start)

        rows = collections.defaultdict(collections.Counter)
        for _, u, v, _, _ in vertices:
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
                total -= sum(1 for _, u, v, position, _ in vertices if in_bottom_row(v) and is_eye(u, position))
            if stripe != skin[0] and total >= MIN_HAIR_VERTICES:
                hair.add(stripe)

        grey_u = (GREY_STRIPE * STRIPE + STRIPE / 2) / TEXTURE
        moved = 0
        for offset, u, v, position, _ in vertices:
            if not in_bottom_row(v) or stripe_of(u) not in hair or is_eye(u, position):
                continue
            out[offset:offset + 4] = struct.pack("<f", grey_u)
            moved += 1

        # The bounds of the UVs that moved, which glTF keeps with them.
        for accessor_index in sorted({accessor for _, _, _, _, accessor in vertices}):
            values = [struct.unpack("<ff", out[offset:offset + 8]) for offset, _, _, _, accessor in vertices if accessor == accessor_index]
            accessor = gltf["accessors"][accessor_index]
            if "min" in accessor or "max" in accessor:
                accessor["min"] = [min(value[i] for value in values) for i in range(2)]
                accessor["max"] = [max(value[i] for value in values) for i in range(2)]

        target = path[:-4] + "-elder.glb"
        json_length = struct.unpack("<I", data[12:16])[0]
        write_glb(target, gltf, bytes(out[20 + json_length:]))
        print(f"{os.path.basename(target)}: hair in stripes {sorted(hair)}, {moved} vertices to grey")


if __name__ == "__main__":
    main()
