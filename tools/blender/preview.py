#!/usr/bin/env python3
"""Превью собранных ассетов: рендерит их в один PNG.

Играть в контейнере нельзя, а посмотреть на модель до того, как она
попадёт в мир, надо. Скрипт собирает ассеты тем же кодом, что и сборка,
ставит камеру со светом и рендерит каждый в свою картинку, а затем
склеивает их в общий лист.

    python3 tools/blender/preview.py cottage windmill well
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_assets as BA  # noqa: E402

OUT = os.path.join(BA.ROOT, "tools", "blender", "preview")


def frame_and_render(name, path):
    """Камера по габаритам сцены, три источника света, мягкий фон."""
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not objs:
        return
    xs, ys, zs = [], [], []
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ __import__("mathutils").Vector(c)
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    cz = (min(zs) + max(zs)) / 2
    size = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs), 0.5)

    # Сцену смотрим с игрового ракурса — низко и близко, как камера за
    # медведем. Отдельный предмет — с трёх четвертей сверху.
    scene_shot = name == "village"
    if scene_shot:
        d = size * 0.62
        bpy.ops.object.camera_add(location=(cx + d * 0.95, cy - d * 1.05, cz + d * 0.42))
        cam = bpy.context.active_object
        cam.rotation_euler = (math.radians(79), 0, math.radians(42))
        cam.data.lens = 38
    else:
        d = size * 2.1
        bpy.ops.object.camera_add(location=(cx + d * 0.8, cy - d * 0.9, cz + d * 0.55))
        cam = bpy.context.active_object
        cam.rotation_euler = (math.radians(72), 0, math.radians(41))
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(cx + 6, cy - 8, cz + 12))
    key = bpy.context.active_object
    key.data.energy = 4.0
    key.rotation_euler = (math.radians(52), 0, math.radians(35))

    bpy.ops.object.light_add(type="AREA", location=(cx - 7, cy - 5, cz + 4))
    fill = bpy.context.active_object
    fill.data.energy = 260 * max(1.0, size)
    fill.data.size = size * 2

    world = bpy.data.worlds.new("prevWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.20, 0.28, 1.0)
        bg.inputs[1].default_value = 1.0

    sc = bpy.context.scene
    # Cycles на CPU: EEVEE требует GPU-контекст, которого в контейнере нет.
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 40
    sc.cycles.use_denoising = True
    wide = name == "village"
    sc.render.resolution_x = 1100 if wide else 640
    sc.render.resolution_y = 620 if wide else 640
    sc.render.film_transparent = False
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("превью:", os.path.relpath(path, BA.ROOT))


def main():
    names = sys.argv[1:] or list(BA.ASSETS)
    os.makedirs(OUT, exist_ok=True)
    made = []
    for n in names:
        fn = BA.ASSETS.get(n)
        if fn is None:
            print("нет такого ассета:", n)
            continue
        fn()                       # собирает и экспортирует, сцена остаётся
        p = os.path.join(OUT, n + ".png")
        frame_and_render(n, p)
        made.append(p)

    # Склейка в один лист, если есть Pillow — так удобнее смотреть сразу все.
    try:
        from PIL import Image
    except ImportError:
        return
    imgs = [Image.open(p) for p in made if os.path.exists(p)]
    if not imgs:
        return
    cols = min(3, len(imgs))
    rows = (len(imgs) + cols - 1) // cols
    w, h = imgs[0].size
    sheet = Image.new("RGB", (cols * w, rows * h), (24, 28, 38))
    for i, im in enumerate(imgs):
        sheet.paste(im, ((i % cols) * w, (i // cols) * h))
    sheet_path = os.path.join(OUT, "sheet.png")
    sheet.save(sheet_path)
    print("лист:", os.path.relpath(sheet_path, BA.ROOT))


if __name__ == "__main__":
    main()
