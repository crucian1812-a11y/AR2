#!/usr/bin/env python3
"""Рендер поз в картинки — единственный способ проверить борьбу глазами.

В коде поза выглядит как список углов, и по нему не видно ни того, что
рука прошла сквозь грудь соперника, ни того, что боец висит в воздухе.
В медвежьем проекте эта проверка сразу нашла разъехавшуюся крышу; здесь
цена ошибки выше: позиции партера — это сплошные контакты.

Рендер идёт на Cycles/CPU: EEVEE требует GPU-контекст, которого в
контейнере нет.

    python3 tools/blender/preview.py            # все позиции
    python3 tools/blender/preview.py Mount      # одну
"""

import os
import sys

try:
    import bpy
    import mathutils
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview")


def setup_render(width=720, height=480, samples=24):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.05, 0.055, 0.07, 1.0)
        bg.inputs[1].default_value = 1.0


def add_ground():
    """Пол обязателен: без него не видно, лежит боец на татами или парит."""
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    plane.name = "Tatami"
    mat = bpy.data.materials.new("Tatami")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.14, 0.3, 0.46, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.9
    plane.data.materials.append(mat)
    return plane


def add_light():
    bpy.ops.object.light_add(type="AREA", location=(2.4, -2.6, 3.4))
    key = bpy.context.active_object
    key.data.energy = 900
    key.data.size = 3.0
    key.rotation_euler = (0.75, 0.0, 0.75)

    bpy.ops.object.light_add(type="AREA", location=(-2.8, -1.6, 2.0))
    fill = bpy.context.active_object
    fill.data.energy = 260
    fill.data.size = 3.5
    fill.rotation_euler = (1.1, 0.0, -1.0)


def add_camera(location, look_at=(0, 0, 0.5), lens=45):
    bpy.ops.object.camera_add(location=location)
    cam = bpy.context.active_object
    cam.data.lens = lens

    direction = mathutils.Vector(look_at) - mathutils.Vector(location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam
    return cam


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("  ->", os.path.relpath(path, os.getcwd()))


def shot(name, cam_loc, look_at=(0, 0, 0.5), lens=45):
    add_camera(cam_loc, look_at, lens)
    render(os.path.join(OUT_DIR, name + ".png"))


def render_position(name, top_pose, bot_pose):
    """Пара бойцов в одной позиции, три ракурса."""
    import rig
    import poses

    rig.reset_scene()
    setup_render()
    add_ground()
    add_light()

    # Синий — верхний, красный — нижний: те же цвета, что и в игре.
    top_arm, top_body = rig.build_fighter("Top", (0.16, 0.3, 0.68))
    bot_arm, bot_body = rig.build_fighter("Bottom", (0.66, 0.15, 0.17))

    poses.apply(top_arm, top_pose)
    poses.apply(bot_arm, bot_pose)
    bpy.context.view_layer.update()

    # Провал под татами — самая частая ошибка позы, и на картинке под
    # нужным ракурсом её можно не заметить. Меряем.
    for label, obj in (("верхний", top_body), ("нижний", bot_body)):
        _, _, bz = rig.bounds(obj)
        flag = "  <-- под татами!" if bz[0] < -0.02 else ""
        print("  %s: Z %.2f..%.2f%s" % (label, bz[0], bz[1], flag))

    shot(name + "_side", (2.9, -1.5, 1.5), (0, 0, 0.45), lens=42)
    shot(name + "_front", (0.0, -3.0, 1.3), (0, 0, 0.45), lens=42)
    shot(name + "_top", (0.05, -0.9, 3.1), (0, 0, 0.3), lens=40)


if __name__ == "__main__":
    import poses

    wanted = sys.argv[1:]
    names = wanted if wanted else list(poses.POSITIONS.keys())

    for name in names:
        if name not in poses.POSITIONS:
            print("Нет такой позиции:", name)
            print("Есть:", ", ".join(poses.POSITIONS))
            continue
        print(name)
        top_pose, bot_pose = poses.POSITIONS[name]
        render_position(name, top_pose, bot_pose)
