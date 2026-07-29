#!/usr/bin/env python3
"""Превью героя: несколько кадров из его анимаций одним листом.

Персонажа мало собрать — надо увидеть, что он не разъезжается в позах.
Скрипт собирает героя тем же кодом, что и сборка, ставит его в кадр из
каждого клипа и склеивает результат.

    python3 tools/blender/preview_hero.py
"""

import math
import os
import sys

try:
    import bpy
    import mathutils
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_hero as BH  # noqa: E402

OUT = os.path.join(BH.ROOT, "tools", "blender", "preview")

# клип, кадр, подпись под позой
SHOTS = [
    ("Idle", 24, "idle"),
    ("Walk", 6, "walk"),
    ("Run", 4, "run"),
    ("Jump", 13, "jump"),
    ("Bite_InPlace", 9, "attack"),
    ("Death", 20, "death"),
]


def setup_stage(front):
    """Камера и свет. front=True — вид спереди, иначе три четверти."""
    for o in [o for o in bpy.context.scene.objects if o.type in ("CAMERA", "LIGHT")]:
        bpy.data.objects.remove(o, do_unlink=True)

    if front:
        loc, rot = (0, -5.2, 1.05), (math.radians(90), 0, 0)
    else:
        loc, rot = (3.3, -4.0, 1.9), (math.radians(80), 0, math.radians(39))
    bpy.ops.object.camera_add(location=loc)
    cam = bpy.context.active_object
    cam.rotation_euler = rot
    cam.data.lens = 60
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(3, -6, 8))
    bpy.context.active_object.data.energy = 3.2
    bpy.context.active_object.rotation_euler = (math.radians(52), 0, math.radians(28))

    bpy.ops.object.light_add(type="AREA", location=(-3.5, -3.5, 2.4))
    fill = bpy.context.active_object
    fill.data.energy = 420
    fill.data.size = 5

    world = bpy.data.worlds.new("heroWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.13, 0.15, 0.2, 1.0)

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 460
    sc.render.resolution_y = 640


def shoot(arm, clip, frame, path, front=False):
    act = bpy.data.actions.get(clip)
    if act is None:
        print("нет клипа:", clip)
        return False
    arm.animation_data.action = act
    bpy.context.scene.frame_set(frame)
    setup_stage(front)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("превью:", os.path.relpath(path, BH.ROOT))
    return True


def main():
    os.makedirs(OUT, exist_ok=True)
    arm = BH.build()

    made = []
    face = os.path.join(OUT, "hero_face.png")
    if shoot(arm, "Idle", 1, face, front=True):
        made.append((face, "front"))

    for clip, frame, label in SHOTS:
        p = os.path.join(OUT, "hero_" + clip + ".png")
        if shoot(arm, clip, frame, p):
            made.append((p, label))

    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return
    imgs = [(Image.open(p), t) for p, t in made if os.path.exists(p)]
    if not imgs:
        return
    w, h = imgs[0][0].size
    cols = min(4, len(imgs))
    rows = (len(imgs) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * w, rows * h), (20, 23, 30))
    draw = ImageDraw.Draw(sheet)
    for i, (im, label) in enumerate(imgs):
        x, y = (i % cols) * w, (i // cols) * h
        sheet.paste(im, (x, y))
        draw.text((x + 12, y + h - 24), label, fill=(210, 220, 235))
    out = os.path.join(OUT, "hero_sheet.png")
    sheet.save(out)
    print("лист:", os.path.relpath(out, BH.ROOT))


if __name__ == "__main__":
    main()
