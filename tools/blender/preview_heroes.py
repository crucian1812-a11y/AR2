#!/usr/bin/env python3
"""Пятеро силачей в одном кадре — проверка силуэтов до сборки APK.

Собрать кубического персонажа мало: понять, узнаётся ли он, можно
только увидев всех рядом и в рост. Скрипт строит те же тела, что и
build_heroes, ставит их в ряд и снимает спереди и с трёх четвертей.

    python3 tools/blender/preview_heroes.py
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_hero as BH  # noqa: E402
from build_heroes import HEROES  # noqa: E402
from build_assets import material, add_cube  # noqa: E402

OUT = os.path.join(BH.ROOT, "tools", "blender", "preview")
STEP = 1.55


def build_row():
    BH.reset()

    # Пол и линии по блокам: рост в блоках виден без линейки.
    floor = add_cube((0, 0, -0.04), (6.0, 2.0, 0.04))
    floor.data.materials.append(material("floor", (0.30, 0.32, 0.35), 0.9))
    grid = material("grid", (0.52, 0.54, 0.58), 0.9)
    for b in range(1, 3):
        bar = add_cube((0, -1.6, b * 0.5), (6.0, 0.015, 0.005))
        bar.data.materials.append(grid)

    for i, (name, fn) in enumerate(HEROES):
        body = BH.merge(fn(), name + "Body")
        body.location.x = (i - (len(HEROES) - 1) * 0.5) * STEP


def stage(loc):
    """Камера смотрит в центр ряда. Наводим ограничителем, а не углами:
    считать эйлеровы углы для трёх ракурсов вручную — верный способ
    получить кадр мимо."""
    for o in [o for o in bpy.context.scene.objects
              if o.type in ("CAMERA", "LIGHT", "EMPTY")]:
        bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.empty_add(location=(0, 0, 1.0))
    target = bpy.context.active_object

    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    cam.data.lens = 50
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(3, -8, 9))
    sun = bpy.context.active_object
    sun.data.energy = 3.4
    sun.rotation_euler = (math.radians(50), 0, math.radians(24))

    bpy.ops.object.light_add(type="AREA", location=(-4.5, -6.0, 3.0))
    fill = bpy.context.active_object
    fill.data.energy = 900
    fill.data.size = 8

    world = bpy.data.worlds.new("heroesWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.16, 0.21, 1.0)
        bg.inputs[1].default_value = 0.8

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 44
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1400
    sc.render.resolution_y = 620


def main():
    os.makedirs(OUT, exist_ok=True)
    build_row()
    # Спина обязательна: в платформере камера почти всегда за героем,
    # и плащ — то, что игрок видит чаще всего. Спереди он честно
    # прячется за руками, как и в самом Minecraft.
    shots = (
        ((0, -10.5, 1.4), "front"),
        ((5.4, -9.2, 2.6), "three"),
        ((-4.2, 9.6, 2.6), "back"),
    )
    for loc, tag in shots:
        stage(loc)
        path = os.path.join(OUT, "heroes_" + tag + ".png")
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("превью:", os.path.relpath(path, BH.ROOT))
    print("порядок:", ", ".join(n for n, _ in HEROES))


if __name__ == "__main__":
    main()
