#!/usr/bin/env python3
"""Превью кубических врагов: проверка пропорций до сборки APK.

Числа здесь повторяют BlockMob.cs пиксель в пиксель — это его зеркало, а
не самостоятельная модель. Смысл в том, что коробочного врага в Unity
никак не разглядеть до сборки, а ошибка в пропорциях кубического моба
видна только глазом: крипер с длинными ногами перестаёт быть крипером,
хотя все размеры «примерно правильные».

Соответствие осей: Unity Y вверх, Blender Z вверх, поэтому
(x, y, z) Unity -> (x, z, y) Blender.

    python3 tools/blender/preview_mobs.py
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import material, add_cube, ROOT  # noqa: E402

OUT = os.path.join(ROOT, "tools", "blender", "preview")

# Тот же пиксель, что и в BlockMob.P
P = 0.06

MATS = {}


def mat(name, rgb):
    if name not in MATS:
        MATS[name] = material(name, rgb, 0.9)
    return MATS[name]


def box(ox, cx, cy, cz, sx, sy, sz, m):
    """Коробка по центру и размеру в пикселях, в координатах Unity."""
    obj = add_cube(((cx + ox) * P, cz * P, cy * P),
                   (sx * P * 0.5, sz * P * 0.5, sy * P * 0.5))
    obj.data.materials.append(m)
    return obj


def limb(ox, px, py, pz, sx, sy, sz, m, rx=0.0, rz=0.0):
    """Конечность на шарнире — как BlockMob.Limb.

    Пустышка стоит в плече, коробка висит под ней на (0, -sy/2, 0), и
    поворот считается вокруг шарнира. Формулы поворота — Unity'евские:
    вокруг X  y' = y cos a - z sin a,  z' = y sin a + z cos a
    вокруг Z  x' = x cos c - y sin c,  y' = x sin c + y cos c
    """
    a = math.radians(rx)
    c = math.radians(rz)
    lx, ly, lz = 0.0, -sy * 0.5, 0.0
    # сначала X, затем Z — тот же порядок, что у Quaternion.Euler
    ly, lz = ly * math.cos(a) - lz * math.sin(a), ly * math.sin(a) + lz * math.cos(a)
    lx, ly = lx * math.cos(c) - ly * math.sin(c), lx * math.sin(c) + ly * math.cos(c)

    obj = add_cube(((px + lx + ox) * P, (pz + lz) * P, (py + ly) * P),
                   (sx * P * 0.5, sz * P * 0.5, sy * P * 0.5))
    # Оси при переносе меняются местами: Unity Z (вперёд) — это Blender Y.
    # Поворот ноги паука вокруг Unity Z разводит её в стороны, а записанный
    # в Blender Z он всего лишь разворачивал коробку в плане, и ноги
    # торчали вертикальными плитами.
    obj.rotation_euler = (a, -c, 0.0)
    obj.data.materials.append(m)
    return obj


# ---------- мобы ----------

def creeper(ox):
    skin = mat("cr_skin", (0.29, 0.62, 0.24))
    dark = mat("cr_dark", (0.20, 0.45, 0.17))
    black = mat("cr_black", (0.03, 0.05, 0.03))

    box(ox, 0, 12, 0, 8, 12, 4, skin)
    box(ox, 0, 22, 0, 8, 8, 8, skin)
    box(ox, -2.6, 14, 2.1, 2, 5, 0.4, dark)
    box(ox, 2.2, 9, 2.1, 3, 3, 0.4, dark)
    box(ox, 0.5, 16, -2.1, 3, 4, 0.4, dark)

    box(ox, -2, 23, 4.2, 2, 2, 0.5, black)
    box(ox, 2, 23, 4.2, 2, 2, 0.5, black)
    box(ox, 0, 20.5, 4.2, 2, 3, 0.5, black)
    box(ox, -2, 19.5, 4.2, 2, 2, 0.5, black)
    box(ox, 2, 19.5, 4.2, 2, 2, 0.5, black)

    for lx in (-2, 2):
        for lz in (-2, 2):
            limb(ox, lx, 6, lz, 4, 6, 4, skin)


def humanoid(ox, bones):
    skin = mat("h_skin_%s" % bones,
               (0.79, 0.79, 0.75) if bones else (0.21, 0.44, 0.28))
    shirt = skin if bones else mat("h_shirt", (0.24, 0.37, 0.55))
    pants = skin if bones else mat("h_pants", (0.22, 0.23, 0.40))
    black = mat("h_black", (0.03, 0.03, 0.04))

    limb_w = 2 if bones else 4
    arm_x = 5 if bones else 6

    box(ox, 0, 18, 0, 8, 12, 4, shirt)
    box(ox, 0, 28, 0, 8, 8, 8, skin)
    box(ox, -2, 29, 4.2, 2, 2, 0.5, black)
    box(ox, 2, 29, 4.2, 2, 2, 0.5, black)
    if bones:
        rib = mat("h_rib", (0.62, 0.62, 0.58))
        for i in range(3):
            box(ox, 0, 15 + i * 3, 2.1, 7, 1, 0.4, rib)

    limb(ox, -2, 12, 0, limb_w, 12, limb_w, pants)
    limb(ox, 2, 12, 0, limb_w, 12, limb_w, pants)
    limb(ox, -arm_x, 23, 0, limb_w, 12, limb_w, skin, rx=-80)
    limb(ox, arm_x, 23, 0, limb_w, 12, limb_w, skin, rx=-80)


def spider(ox):
    body = mat("sp_body", (0.19, 0.13, 0.11))
    hair = mat("sp_hair", (0.11, 0.08, 0.07))
    eye = mat("sp_eye", (1.0, 0.15, 0.1))

    box(ox, 0, 6, -6, 10, 8, 12, body)
    box(ox, 0, 6, 4, 8, 8, 8, body)
    box(ox, 0, 9, -6, 6, 1, 8, hair)

    for i in range(2):
        for s in (-1, 1):
            box(ox, s * 1.4, 7.5 - i * 2, 8.2, 1.2, 1.2, 0.5, eye)
            box(ox, s * 3.2, 7.5 - i * 2, 8.2, 1.2, 1.2, 0.5, eye)

    # Нога из двух колен — как SpiderLeg в BlockMob.
    for i in range(4):
        z = -7 + i * 4.5
        for s in (-1, 1):
            limb(ox, s * 5, 9, z, 1.6, 9, 1.6, hair, rz=s * 70)
            # Колено — конец бедра; голень падает от него вертикально.
            kx = s * 5 + 9 * math.sin(math.radians(s * 70))
            ky = 9 - 9 * math.cos(math.radians(s * 70))
            limb(ox, kx, ky, z, 1.4, 6, 1.4, hair)


def enderman(ox):
    skin = mat("en_skin", (0.055, 0.055, 0.075))
    eye = mat("en_eye", (0.87, 0.45, 1.0))

    box(ox, 0, 36, 0, 8, 12, 4, skin)
    box(ox, 0, 46, 0, 8, 8, 8, skin)
    box(ox, -2.2, 46.5, 4.2, 3, 1.6, 0.5, eye)
    box(ox, 2.2, 46.5, 4.2, 3, 1.6, 0.5, eye)

    limb(ox, -2, 30, 0, 2, 30, 2, skin)
    limb(ox, 2, 30, 0, 2, 30, 2, skin)
    limb(ox, -5, 41, 0, 2, 30, 2, skin)
    limb(ox, 5, 41, 0, 2, 30, 2, skin)


def cube_slime(ox):
    gel = mat("sl_gel", (0.45, 0.78, 0.35))
    black = mat("sl_black", (0.05, 0.09, 0.04))
    box(ox, 0, 8, 0, 16, 16, 16, gel)
    box(ox, -3, 10, 8.2, 2.4, 2.4, 0.5, black)
    box(ox, 3, 10, 8.2, 2.4, 2.4, 0.5, black)
    box(ox, 0, 6, 8.2, 2.4, 1.6, 0.5, black)


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    # Пол и мерная сетка: рост в блоках видно без линейки.
    floor = add_cube((0, 0, -0.05), (9, 3, 0.05))
    floor.data.materials.append(mat("floor", (0.32, 0.34, 0.36)))
    grid = mat("grid", (0.55, 0.57, 0.6))
    for b in range(1, 4):
        bar = add_cube((0, -2.4, b * 0.96), (9, 0.02, 0.006))
        bar.data.materials.append(grid)

    step = 38  # в пикселях, между мобами
    creeper(-2.5 * step)
    humanoid(-1.5 * step, False)
    humanoid(-0.5 * step, True)
    spider(0.6 * step)
    enderman(1.6 * step)
    cube_slime(2.5 * step)


def render():
    # Камера сильно отодвинута и повёрнута на три четверти. В упор и в лоб
    # крипер со слизью не влезали в кадр вовсе, а вытянутые вперёд руки
    # зомби смотрели точно в объектив и читались обрубками.
    bpy.ops.object.empty_add(location=(0.0, 0.0, 1.1))
    target = bpy.context.active_object

    # Камера стоит со стороны +Y Blender, то есть +Z Unity. Это «перёд»:
    # именно туда Enemy.HostStep разворачивает моба (Atan2(x, z)), и
    # именно там у всех лица. С другой стороны кадра были одни затылки.
    bpy.ops.object.camera_add(location=(-5.5, 15.5, 3.4))
    cam = bpy.context.active_object
    cam.data.lens = 38
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(-4, -8, 10))
    sun = bpy.context.active_object
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(52), 0, math.radians(-28))

    world = bpy.data.worlds.new("mobs")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.42, 0.55, 0.68, 1.0)
        bg.inputs[1].default_value = 0.6

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 40
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1400
    sc.render.resolution_y = 620

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "mobs.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("превью:", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    build()
    render()
