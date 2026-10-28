"""Procedural low-poly model helpers: boxes and cylinders with named flat materials, exported as .glb.

Conventions (see docs/art.md): Y up, origin at the centre of the base on Y = 0, front towards +Z.
"""
import math
import os

import numpy as np
import trimesh
from trimesh import creation, transformations as tf
from trimesh.visual.material import PBRMaterial

# Flat colours by material name. The game can recolour a material by its name.
PALETTE = {
    "wood": "#9A6B3F",
    "wood_dark": "#5E4029",
    "wood_light": "#C9A06A",
    "metal": "#8C929B",
    "metal_dark": "#4A4F57",
    "stone": "#9A9A92",
    "stone_dark": "#6E6E68",
    "cloth": "#D9CFB8",
    "cloth_red": "#9C4A3C",
    "parchment": "#E9DDB0",
    "ink": "#23212B",
    "ember": "#E8501E",
    "straw": "#CFAE55",
    "soot": "#2B2724",
    "leaf": "#5E9A3A",
    "leaf_light": "#9CCB5A",
    "leaf_dark": "#3F7A2E",
    "pea": "#6FAE3C",
    "crust": "#B87A3D",
    "crumb": "#E3B96F",
    "clay": "#A5623A",
    "clay_light": "#D2B48C",
    "stew": "#C98F3C",
    "brine": "#B7B24C",
    "rope": "#8A6A3D",
    "wicker": "#B58A4A",
    "apple": "#B8362E",
    "flour": "#EFE8D6",
    "stone_light": "#B4B4AA",
    "rust": "#A8502A",
    "iron": "#6C7A8C",
    "iron_dark": "#46515F",
    "charcoal": "#1F1C1B",
    "wheat_ear": "#E0B043",
    "wheat_green": "#86B845",
    "pod": "#B5E060",
}


def _rgba(hexcolor):
    h = hexcolor.lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)] + [1.0]


class Model:
    def __init__(self, name):
        self.name = name
        self.parts = []  # (material name, mesh)

    def _add(self, mat, mesh, at, rot, T):
        if any(rot):
            mesh.apply_transform(tf.euler_matrix(*[math.radians(a) for a in rot], "sxyz"))
        mesh.apply_translation(at)
        if T is not None:
            mesh.apply_transform(T)
        self.parts.append((mat, mesh))

    def box(self, mat, size, at, rot=(0, 0, 0), T=None):
        """Box of `size` centred on `at`; `rot` is in degrees, applied about the box centre."""
        self._add(mat, creation.box(extents=size), at, rot, T)

    def cyl(self, mat, radius, height, at, axis="y", sections=12, rot=(0, 0, 0), T=None):
        mesh = creation.cylinder(radius=radius, height=height, sections=sections)
        if axis == "y":
            mesh.apply_transform(tf.euler_matrix(math.radians(-90), 0, 0, "sxyz"))
        elif axis == "x":
            mesh.apply_transform(tf.euler_matrix(0, math.radians(90), 0, "sxyz"))
        self._add(mat, mesh, at, rot, T)

    def sphere(self, mat, radius, at, scale=(1, 1, 1), T=None, count=(8, 12)):
        mesh = creation.uv_sphere(radius=radius, count=list(count))
        mesh.apply_transform(np.diag([*scale, 1.0]))
        self._add(mat, mesh, at, (0, 0, 0), T)

    def to_scene(self):
        merged = {}
        for mat, mesh in self.parts:
            merged.setdefault(mat, []).append(mesh)
        scene = trimesh.Scene()
        for mat, meshes in merged.items():
            mesh = trimesh.util.concatenate(meshes)
            material = PBRMaterial(
                name=mat,
                baseColorFactor=_rgba(PALETTE[mat]),
                metallicFactor=0.0,
                roughnessFactor=1.0,
            )
            mesh.visual = trimesh.visual.TextureVisuals(material=material)
            scene.add_geometry(mesh, node_name=mat, geom_name=mat)
        return scene

    def ground(self):
        """Lower the model so its lowest point rests on Y = 0."""
        low = min(m.bounds[0][1] for _, m in self.parts)
        for _, mesh in self.parts:
            mesh.apply_translation((0, -low, 0))

    def report(self):
        scene = self.to_scene()
        lo, hi = scene.bounds
        tris = sum(len(g.faces) for g in scene.geometry.values())
        fits = hi[0] - lo[0] <= 1.0 and hi[2] - lo[2] <= 1.0
        print(f"{self.name}: size={np.round(hi - lo, 2)} min_y={lo[1]:.3f} "
              f"centre_xz=({(hi[0] + lo[0]) / 2:.2f},{(hi[2] + lo[2]) / 2:.2f}) tris={tris} "
              f"fits_cell={fits} materials={list(scene.geometry)}")
        return fits and abs(lo[1]) < 1e-3

    def export(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        ok = self.report()
        self.to_scene().export(path)
        return ok


def rot_x_about(pivot, degrees):
    """Rotation about the X axis through `pivot` (to tilt a group of parts)."""
    p = np.array(pivot, dtype=float)
    r = tf.rotation_matrix(math.radians(degrees), [1, 0, 0], p)
    return r


def preview(model, path, extent=0.6):
    """Render a quick 4-view preview (matplotlib) so the shape can be reviewed without Godot."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection

    scene = model.to_scene()
    light = np.array([0.4, 0.8, 0.5])
    light /= np.linalg.norm(light)
    fig = plt.figure(figsize=(12, 3.2), dpi=100)
    for i, azim in enumerate((-45, 45, 135, 225)):
        ax = fig.add_subplot(1, 4, i + 1, projection="3d")
        for mat, mesh in scene.geometry.items():
            base = np.array(_rgba(PALETTE[mat])[:3])
            tris = mesh.vertices[mesh.faces]
            # glTF is Y-up; matplotlib is Z-up, so map (x, y, z) -> (x, -z, y).
            pts = tris[:, :, [0, 2, 1]] * np.array([1, -1, 1])
            shade = 0.45 + 0.55 * np.clip(mesh.face_normals @ light, 0, 1)
            cols = [(*(base * s), 1) for s in shade]
            ax.add_collection3d(Poly3DCollection(pts, facecolors=cols, edgecolors="none"))
        ax.set_xlim(-extent, extent); ax.set_ylim(-extent, extent); ax.set_zlim(0, 2 * extent)
        ax.set_box_aspect((1, 1, 1.0))
        ax.view_init(elev=28, azim=azim)
        ax.set_axis_off()
    fig.suptitle(model.name, fontsize=9)
    fig.tight_layout()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    fig.savefig(path)
    plt.close(fig)
