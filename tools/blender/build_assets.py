#!/usr/bin/env python3
"""Генератор собственных ассетов игры через Blender.

Blender подключён не как приложение, а как модуль Python (пакет `bpy`),
поэтому дисплей не нужен и всё работает в контейнере и в CI.

Запуск:
    python3 tools/blender/build_assets.py

Скрипт кладёт готовые FBX в unity/Assets/Resources/Models/custom/.
Настройки импорта им задаёт ModelImportSettings, как и остальным моделям:
скелета у них нет, материалы берутся из самого файла.
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Models", "custom")


# ---------- вспомогательные ----------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, rgb, roughness=0.75, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
    return mat


def shade(obj, mat, smooth=True):
    obj.data.materials.append(mat)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True


def add_sphere(loc, scale, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.active_object
    obj.scale = scale
    return obj


def add_cube(loc, scale, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    obj = bpy.context.active_object
    obj.scale = scale
    obj.rotation_euler = rot
    return obj


def join(objs, name):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.location = (0, 0, 0)
    return obj


def export(name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".fbx")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("готово:", os.path.relpath(path, ROOT))


# ---------- ассеты ----------

def build_turtle():
    """Гигантская черепаха: город в Черепахограде стоит на таких панцирях.

    Раньше панцирь собирался из шумовых сфер прямо в игре — форма выходила
    случайной. Здесь она задана осознанно: купол со щитками, голова, лапы
    и хвост.
    """
    reset()
    shell_mat = material("shellDark", (0.16, 0.30, 0.22), 0.85)
    plate_mat = material("shellPlate", (0.32, 0.52, 0.34), 0.8)
    skin_mat = material("turtleSkin", (0.42, 0.55, 0.34), 0.9)
    eye_mat = material("turtleEye", (0.05, 0.05, 0.06), 0.3)

    parts = []

    dome = add_sphere((0, 0, 0.5), (2.0, 1.9, 1.0), 20, 12)
    shade(dome, shell_mat)
    parts.append(dome)

    belly = add_sphere((0, 0, 0.18), (1.9, 1.8, 0.35), 16, 8)
    shade(belly, plate_mat)
    parts.append(belly)

    # Роговые щитки: центральный ряд и кольцо по краю панциря.
    for i in range(6):
        a = i * math.pi / 3.0
        p = add_cube((math.cos(a) * 1.25, math.sin(a) * 1.2, 1.12),
                     (0.42, 0.42, 0.12), (0, 0, a))
        shade(p, plate_mat, smooth=False)
        parts.append(p)
    top = add_cube((0, 0, 1.34), (0.5, 0.5, 0.12))
    shade(top, plate_mat, smooth=False)
    parts.append(top)

    for i in range(10):
        a = i * math.pi / 5.0
        p = add_cube((math.cos(a) * 1.95, math.sin(a) * 1.85, 0.55),
                     (0.34, 0.34, 0.22), (0, 0, a))
        shade(p, plate_mat, smooth=False)
        parts.append(p)

    # Голова на шее
    neck = add_sphere((0, 2.05, 0.42), (0.34, 0.55, 0.34), 12, 8)
    shade(neck, skin_mat)
    parts.append(neck)
    head = add_sphere((0, 2.75, 0.55), (0.52, 0.62, 0.46), 14, 10)
    shade(head, skin_mat)
    parts.append(head)
    for sx in (-1, 1):
        eye = add_sphere((0.26 * sx, 3.1, 0.72), (0.12, 0.1, 0.12), 8, 6)
        shade(eye, eye_mat)
        parts.append(eye)

    # Ласты
    for sx in (-1, 1):
        for sy, ang in ((1, 0.5), (-1, -0.5)):
            flip = add_sphere((1.7 * sx, 1.1 * sy, 0.28),
                              (0.72, 0.36, 0.22), 12, 8)
            flip.rotation_euler = (0, 0, ang * sx)
            shade(flip, skin_mat)
            parts.append(flip)

    tail = add_sphere((0, -2.1, 0.4), (0.28, 0.52, 0.24), 10, 8)
    shade(tail, skin_mat)
    parts.append(tail)

    join(parts, "Turtle")
    export("turtle_giant")


def build_signpost():
    """Указатель на распутье — подсказывает игроку направление."""
    reset()
    wood = material("woodBark", (0.42, 0.29, 0.17), 0.9)
    board = material("woodPlank", (0.63, 0.47, 0.28), 0.85)

    parts = []
    post = add_cube((0, 0, 1.2), (0.09, 0.09, 1.2))
    shade(post, wood, smooth=False)
    parts.append(post)
    base = add_cube((0, 0, 0.08), (0.35, 0.35, 0.08))
    shade(base, wood, smooth=False)
    parts.append(base)

    for i, (h, ang) in enumerate(((1.9, 0.0), (1.5, 2.1), (1.1, 4.1))):
        arm = add_cube((math.sin(ang) * 0.55, math.cos(ang) * 0.55, h),
                       (0.62, 0.06, 0.17), (0, 0, -ang))
        shade(arm, board, smooth=False)
        parts.append(arm)

    join(parts, "Signpost")
    export("signpost")


def build_treasure_chest():
    """Сундук — заметная цель на площадках вместо очередной монеты."""
    reset()
    wood = material("chestWood", (0.45, 0.26, 0.13), 0.85)
    gold = material("chestGold", (0.85, 0.66, 0.22), 0.35, 0.85)

    parts = []
    body = add_cube((0, 0, 0.32), (0.62, 0.42, 0.32))
    shade(body, wood, smooth=False)
    parts.append(body)

    bpy.ops.mesh.primitive_cylinder_add(vertices=14, location=(0, 0, 0.64))
    lid = bpy.context.active_object
    lid.scale = (0.62, 0.62, 0.42)
    lid.rotation_euler = (math.pi / 2, 0, 0)
    shade(lid, wood)
    parts.append(lid)

    for x in (-0.42, 0.0, 0.42):
        band = add_cube((x, 0, 0.42), (0.05, 0.44, 0.44))
        shade(band, gold, smooth=False)
        parts.append(band)
    lock = add_cube((0, -0.44, 0.44), (0.12, 0.04, 0.12))
    shade(lock, gold, smooth=False)
    parts.append(lock)

    join(parts, "Chest")
    export("treasure_chest")


ASSETS = {
    "turtle": build_turtle,
    "signpost": build_signpost,
    "chest": build_treasure_chest,
}


def main():
    names = sys.argv[1:] or list(ASSETS)
    for n in names:
        fn = ASSETS.get(n)
        if fn is None:
            print("нет такого ассета:", n, "— доступны:", ", ".join(ASSETS))
            continue
        fn()


if __name__ == "__main__":
    main()
