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

# В режиме сборной сцены (превью деревни) отдельные ассеты не должны ни
# очищать сцену за собой, ни писать свои FBX: они собираются один за
# другим в общий кадр.
SCENE_MODE = False


def reset():
    if SCENE_MODE:
        return
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


def add_cyl(loc, radius, height, verts=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius,
                                        depth=height, location=loc)
    obj = bpy.context.active_object
    obj.rotation_euler = rot
    return obj


def add_cone(loc, r1, r2, height, verts=16, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=height, location=loc)
    obj = bpy.context.active_object
    obj.rotation_euler = rot
    return obj


def add_torus(loc, major, minor, rot=(0, 0, 0), mseg=20, nseg=8):
    bpy.ops.mesh.primitive_torus_add(location=loc, major_radius=major,
                                     minor_radius=minor, major_segments=mseg,
                                     minor_segments=nseg)
    obj = bpy.context.active_object
    obj.rotation_euler = rot
    return obj


def bar_along(loc, half_len, thick, dx, dz, depth=None):
    """Брусок, лежащий вдоль направления (dx, dz) в плоскости XZ.

    Поворот вокруг Y переводит локальную ось X в (cos t, 0, -sin t),
    поэтому нужный угол — atan2(-dz, dx). Считать его руками каждый раз
    значит рано или поздно повернуть деталь на 90 градусов не туда: так
    у крыльев мельницы планки полотна легли ВДОЛЬ лонжерона.
    """
    d = depth if depth is not None else thick
    obj = add_cube(loc, (half_len, d, thick), (0, math.atan2(-dz, dx), 0))
    return obj


def gable_roof(parts, mats, eave, half_w, depth, rise, overhang=0.3, rows=5):
    """Двускатная крыша с черепицей и фронтонами.

    Скат — это доска, повёрнутая вокруг оси Y так, чтобы её локальная ось X
    легла точно на линию «конёк — карниз». Поворот на atan2(rise, span) с
    знаком стороны: при +1 локальный X уходит вправо-вниз, при -1 —
    влево-вниз, и оба конца садятся ровно на углы.
    """
    span = half_w + overhang
    length = math.hypot(span, rise)
    ang = math.atan2(rise, span)

    for sx in (-1, 1):
        rot = (0, sx * ang, 0)
        cx = sx * span * 0.5
        cz = eave + rise * 0.5
        slope = add_cube((cx, 0, cz), (length * 0.5, depth + overhang, 0.09), rot)
        shade(slope, mats["roof"], False)
        parts.append(slope)

        # Ряды черепицы поверх ската, вдоль его нормали.
        nx, nz = sx * rise / length, span / length
        for r in range(rows):
            t = (r + 0.5) / rows            # 0 — у конька, 1 — у карниза
            px = sx * span * t + nx * 0.07
            pz = eave + rise * (1.0 - t) + nz * 0.07
            row = add_cube((px, 0, pz),
                           (length * 0.5 / rows, depth + overhang + 0.02, 0.05), rot)
            shade(row, mats["roofDark"] if r % 2 else mats["roof"], False)
            parts.append(row)

    # Фронтоны: треугольник, набранный сужающимися дощечками.
    for sy in (-1, 1):
        for r in range(6):
            t = (r + 0.5) / 6.0
            slab = add_cube((0, sy * (depth + 0.02), eave + rise * t),
                            (half_w * (1.0 - t), 0.06, rise / 12.0))
            shade(slab, mats["plaster"], False)
            parts.append(slab)

    ridge = add_cube((0, 0, eave + rise + 0.05), (0.11, depth + overhang, 0.09))
    shade(ridge, mats["beam"], False)
    parts.append(ridge)


def join(objs, name):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    # Склейка наследует трансформ ПЕРВОЙ детали, а у неё и сдвиг, и поворот,
    # и масштаб почти всегда неединичные: остальные меши пересчитываются в
    # её систему координат. Пока объект не трогают, это незаметно, но стоит
    # задать ему свой трансформ — и модель разъезжается. Так дом вытягивался
    # в башню (масштаб), крест крыльев уходил в другую плоскость (поворот
    # втулки на 90 градусов), а башня мельницы проваливалась на 4.6 м под
    # землю (сдвиг первого яруса на z=4).
    #
    # Запекаем всё в вершины: у объекта остаётся единичный трансформ, а меш
    # лежит ровно там, где его построили — основанием в нуле.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def export(name):
    if SCENE_MODE:
        return
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


# ---------- деревня ----------

# Общая палитра построек, чтобы деревня выглядела одним поселением,
# а не набором случайных предметов.
def village_palette():
    return {
        "plaster": material("plaster", (0.88, 0.82, 0.70), 0.9),
        "beam": material("beam", (0.30, 0.19, 0.12), 0.85),
        "stone": material("stone", (0.52, 0.50, 0.47), 0.9),
        "roof": material("roof", (0.46, 0.20, 0.17), 0.8),
        "roofDark": material("roofDark", (0.36, 0.15, 0.13), 0.8),
        "wood": material("wood", (0.55, 0.38, 0.22), 0.85),
        "woodDark": material("woodDark", (0.38, 0.25, 0.14), 0.85),
        "glass": material("glass", (1.0, 0.86, 0.52), 0.25),
        "iron": material("iron", (0.22, 0.22, 0.25), 0.35, 0.8),
        "leaf": material("leaf", (0.28, 0.55, 0.26), 0.9),
        "petal": material("petal", (0.90, 0.36, 0.42), 0.8),
    }


def build_cottage():
    """Деревенский дом с двускатной крышей.

    В игре дом был коробкой с конусом сверху. Здесь настоящий силуэт:
    каменный цоколь, фахверк с раскосами, черепица рядами, слуховое окно,
    крыльцо с навесом и труба с колпаком.
    """
    reset()
    m = village_palette()
    parts = []

    W, D, H = 2.8, 2.4, 2.6   # половина ширины, половина глубины, высота стен

    # Каменный цоколь чуть шире стен — дом «стоит», а не висит.
    base = add_cube((0, 0, 0.28), (W + 0.16, D + 0.16, 0.28))
    shade(base, m["stone"], False)
    parts.append(base)

    walls = add_cube((0, 0, 0.56 + H * 0.5), (W, D, H * 0.5))
    shade(walls, m["plaster"], False)
    parts.append(walls)

    # Фахверк: стойки по углам, поясок и раскосы на длинных стенах.
    for sx in (-1, 1):
        for sy in (-1, 1):
            p = add_cube((W * sx, D * sy, 0.56 + H * 0.5), (0.16, 0.16, H * 0.5))
            shade(p, m["beam"], False)
            parts.append(p)
    for sy in (-1, 1):
        belt = add_cube((0, D * sy, 0.56 + H * 0.62), (W, 0.09, 0.12))
        shade(belt, m["beam"], False)
        parts.append(belt)
        for sx in (-1, 1):
            br = add_cube((1.5 * sx, D * sy, 0.56 + H * 0.28),
                          (0.09, 0.08, H * 0.30), (0, 0.62 * sx, 0))
            shade(br, m["beam"], False)
            parts.append(br)

    eave = 0.56 + H
    ridge = 1.6
    gable_roof(parts, m, eave, W, D, ridge, 0.34, 6)

    # Крыльцо: две стойки и навес над дверью.
    for sx in (-1, 1):
        post = add_cube((0.85 * sx, D + 0.62, 0.56 + 0.95), (0.10, 0.10, 0.95))
        shade(post, m["woodDark"], False)
        parts.append(post)
    canopy = add_cube((0, D + 0.42, 0.56 + 2.02), (1.05, 0.72, 0.08), (0.34, 0, 0))
    shade(canopy, m["roofDark"], False)
    parts.append(canopy)

    door = add_cube((0, D + 0.03, 0.56 + 0.85), (0.52, 0.08, 0.85))
    shade(door, m["woodDark"], False)
    parts.append(door)
    frame = add_cube((0, D + 0.06, 0.56 + 0.92), (0.62, 0.06, 0.94))
    shade(frame, m["beam"], False)
    parts.append(frame)
    knob = add_sphere((0.34, D + 0.12, 0.56 + 0.85), (0.07, 0.07, 0.07), 8, 6)
    shade(knob, m["iron"])
    parts.append(knob)

    # Окна. Рама — четыре бруска ВОКРУГ стекла: сплошной короб, каким она
    # была раньше, просто закрывал стекло собой, и окно читалось глухой
    # коричневой заплатой.
    HW, HH, T = 0.5, 0.5, 0.07     # полуширина, полувысота, толщина стекла
    for (px, py, side) in ((-1.75, D + 0.02, False), (1.75, D + 0.02, False),
                           (-W - 0.02, -0.7, True), (W + 0.02, -0.7, True)):
        z = 0.56 + 1.55
        # ax — ось «вширь» окна: X для фасада, Y для боковой стены.
        def wsize(u, v, w):
            return (v, u, w) if side else (u, v, w)

        gl = add_cube((px, py, z), wsize(HW, T, HH))
        shade(gl, m["glass"], False)
        parts.append(gl)

        for s in (-1, 1):
            bx = px + (0 if side else HW * s * 1.06)
            by = py + (HW * s * 1.06 if side else 0)
            v = add_cube((bx, by, z), wsize(0.07, T + 0.05, HH + 0.09))
            shade(v, m["beam"], False)
            parts.append(v)
            h = add_cube((px, py, z + HH * s * 1.06),
                         wsize(HW + 0.09, T + 0.05, 0.07))
            shade(h, m["beam"], False)
            parts.append(h)

        # Переплёт крест-накрест поверх стекла
        mv = add_cube((px, py, z), wsize(0.045, T + 0.03, HH))
        shade(mv, m["beam"], False)
        parts.append(mv)
        mh = add_cube((px, py, z), wsize(HW, T + 0.03, 0.045))
        shade(mh, m["beam"], False)
        parts.append(mh)

        # Ящик с цветами под окном
        ox = 0.14 if side else 0.0
        oy = 0.0 if side else 0.14
        box = add_cube((px + ox * (1 if px > 0 else -1), py + oy, z - 0.66),
                       wsize(HW + 0.06, T + 0.14, 0.12))
        shade(box, m["woodDark"], False)
        parts.append(box)
        for k in (-1, 0, 1):
            fx2 = px + ox * (1 if px > 0 else -1) + (0 if side else 0.26 * k)
            fy2 = py + oy + (0.26 * k if side else 0)
            fl = add_sphere((fx2, fy2, z - 0.5), (0.1, 0.1, 0.1), 8, 6)
            shade(fl, m["petal"] if k else m["leaf"])
            parts.append(fl)

    # Слуховое окно на скате
    dorm = add_cube((-0.9, 0, eave + 0.62), (0.42, 0.36, 0.34))
    shade(dorm, m["plaster"], False)
    parts.append(dorm)
    dormr = add_cube((-0.9, 0, eave + 1.0), (0.5, 0.44, 0.07), (0, 0.5, 0))
    shade(dormr, m["roofDark"], False)
    parts.append(dormr)
    dormg = add_cube((-0.9, 0.37, eave + 0.62), (0.24, 0.05, 0.22))
    shade(dormg, m["glass"], False)
    parts.append(dormg)

    # Труба с колпаком
    ch = add_cube((1.5, -1.1, eave + 1.25), (0.32, 0.32, 1.0))
    shade(ch, m["stone"], False)
    parts.append(ch)
    cap = add_cube((1.5, -1.1, eave + 2.3), (0.44, 0.44, 0.09))
    shade(cap, m["stone"], False)
    parts.append(cap)

    join(parts, "Cottage")
    export("cottage")


def build_windmill():
    """Мельница — главный ориентир деревни.

    Код уже ссылался на неё («тренировочная полоса за мельницей»), но
    самой мельницы в мире не было. Крылья вынесены отдельным ассетом,
    чтобы игра вращала их компонентом Spinner.
    """
    reset()
    m = village_palette()
    parts = []

    # Один сплошной конус вместо стопки цилиндров: ярусы читались как
    # этажи маяка, а не как сужающаяся башня мельницы.
    tower = add_cone((0, 0, 4.0), 2.3, 1.25, 8.0, 24)
    shade(tower, m["plaster"])
    parts.append(tower)

    # Каменный цоколь и два лёгких пояска
    plinth = add_cone((0, 0, 0.28), 2.45, 2.34, 0.56, 24)
    shade(plinth, m["stone"])
    parts.append(plinth)
    for z, r in ((3.1, 1.93), (6.2, 1.58)):
        band = add_torus((0, 0, z), r, 0.09)
        shade(band, m["woodDark"])
        parts.append(band)

    # Опоясывающий балкон с перилами
    bal = add_cyl((0, 0, 4.6), 2.05, 0.14, 24)
    shade(bal, m["woodDark"])
    parts.append(bal)
    for i in range(18):
        a = i * math.pi / 9.0
        p = add_cube((math.cos(a) * 1.92, math.sin(a) * 1.92, 5.05),
                     (0.06, 0.06, 0.38), (0, 0, a))
        shade(p, m["woodDark"], False)
        parts.append(p)
    rail = add_torus((0, 0, 5.42), 1.92, 0.07)
    shade(rail, m["woodDark"])
    parts.append(rail)

    # Купольный колпак: полусфера с гребнем, а не острый конус
    cap = add_sphere((0, 0, 8.0), (1.42, 1.42, 1.15), 24, 12)
    shade(cap, m["roof"])
    parts.append(cap)
    crest = add_cube((0, 0, 9.05), (0.14, 1.3, 0.12))
    shade(crest, m["woodDark"], False)
    parts.append(crest)
    # Хвостовое бревно, которым мельницу разворачивают по ветру
    tail = add_cube((0, -2.1, 7.2), (0.11, 1.1, 0.11), (0.55, 0, 0))
    shade(tail, m["woodDark"], False)
    parts.append(tail)

    # Вал под крылья — сквозной. Односторонний торчал с той стороны
    # колпака, где крыльев нет, и не доставал до втулки.
    shaft = add_cyl((0, 0, 7.9), 0.20, 4.6, 12, (math.pi / 2, 0, 0))
    shade(shaft, m["iron"])
    parts.append(shaft)
    for sy in (-1, 1):
        collar = add_cyl((0, 2.0 * sy, 7.9), 0.30, 0.28, 12, (math.pi / 2, 0, 0))
        shade(collar, m["woodDark"])
        parts.append(collar)

    # Дверь с косяком
    door = add_cube((0, 2.18, 1.15), (0.55, 0.12, 1.0))
    shade(door, m["woodDark"], False)
    parts.append(door)
    dframe = add_cube((0, 2.12, 1.22), (0.66, 0.08, 1.1))
    shade(dframe, m["beam"], False)
    parts.append(dframe)

    # Окошки вразнобой по высоте
    for a, z, r in ((0.0, 2.9, 1.98), (2.1, 5.9, 1.55), (4.2, 3.6, 1.87)):
        w = add_cube((math.cos(a) * r, math.sin(a) * r, z),
                     (0.34, 0.34, 0.38), (0, 0, a))
        shade(w, m["glass"], False)
        parts.append(w)
        fr = add_cube((math.cos(a) * (r - 0.04), math.sin(a) * (r - 0.04), z),
                      (0.42, 0.42, 0.46), (0, 0, a))
        shade(fr, m["beam"], False)
        parts.append(fr)

    join(parts, "Windmill")
    export("windmill")


def build_windmill_blades():
    """Крылья мельницы: решётчатый крест, вращается в игре отдельно."""
    reset()
    m = village_palette()
    parts = []

    hub = add_cyl((0, 0, 0), 0.42, 0.5, 14, (math.pi / 2, 0, 0))
    shade(hub, m["woodDark"])
    parts.append(hub)
    cap = add_sphere((0, -0.32, 0), (0.3, 0.3, 0.3), 12, 8)
    shade(cap, m["iron"])
    parts.append(cap)

    # Размах маха сопоставим с высотой башни: с радиусом 2.3 крылья едва
    # вылезали за колпак и читались флюгером, а не мельницей.
    R = 4.4
    for i in range(4):
        a = i * math.pi / 2.0
        ca, sa = math.cos(a), math.sin(a)
        # Лонжерон — вдоль луча, полотно — поперёк него.
        sp = bar_along((ca * R * 0.5, 0, sa * R * 0.5), R * 0.5, 0.11, ca, sa, 0.16)
        shade(sp, m["woodDark"], False)
        parts.append(sp)

        for k in range(9):
            d = 0.9 + k * 0.42
            slat = bar_along((ca * d, 0, sa * d), 0.62, 0.05, -sa, ca)
            shade(slat, m["wood"], False)
            parts.append(slat)

        # Дальний край полотна — маху нужен внятный контур
        edge = bar_along((ca * R * 0.55 - sa * 0.6, 0, sa * R * 0.55 + ca * 0.6),
                         R * 0.42, 0.05, ca, sa)
        shade(edge, m["wood"], False)
        parts.append(edge)

    join(parts, "WindmillBlades")
    export("windmill_blades")


def build_well():
    """Колодец — центр деревенской площади."""
    reset()
    m = village_palette()
    parts = []

    # Кольцо из тёсаных камней вместо гладкого цилиндра
    for i in range(12):
        a = i * math.pi / 6.0
        s = add_cube((math.cos(a) * 0.92, math.sin(a) * 0.92, 0.34),
                     (0.28, 0.20, 0.34), (0, 0, a))
        shade(s, m["stone"], False)
        parts.append(s)
    rim = add_torus((0, 0, 0.7), 0.95, 0.10)
    shade(rim, m["stone"])
    parts.append(rim)
    water = add_cyl((0, 0, 0.42), 0.78, 0.05, 16)
    shade(water, material("wellWater", (0.16, 0.34, 0.45), 0.15))
    parts.append(water)

    for sx in (-1, 1):
        post = add_cube((0.85 * sx, 0, 1.5), (0.11, 0.11, 0.85))
        shade(post, m["woodDark"], False)
        parts.append(post)

    gable_roof(parts, m, 2.35, 0.95, 0.85, 0.55, 0.25, 4)

    # Ворот с ручкой, верёвка и ведро
    drum = add_cyl((0, 0, 2.1), 0.14, 1.5, 12, (math.pi / 2, 0, 0))
    shade(drum, m["wood"])
    parts.append(drum)
    handle = add_cube((0.95, 0.3, 2.1), (0.05, 0.3, 0.05))
    shade(handle, m["iron"], False)
    parts.append(handle)
    rope = add_cyl((0, 0, 1.62), 0.03, 0.95, 8)
    shade(rope, m["wood"])
    parts.append(rope)
    bucket = add_cone((0, 0, 1.03), 0.20, 0.24, 0.34, 12)
    shade(bucket, m["woodDark"])
    parts.append(bucket)
    hoop = add_torus((0, 0, 1.18), 0.23, 0.025)
    shade(hoop, m["iron"])
    parts.append(hoop)

    join(parts, "Well")
    export("well")


def build_lamp_post():
    """Фонарь: расставленные по площади, они задают вечерний свет."""
    reset()
    m = village_palette()
    parts = []

    base = add_cyl((0, 0, 0.1), 0.24, 0.2, 12)
    shade(base, m["stone"])
    parts.append(base)
    foot = add_cone((0, 0, 0.36), 0.19, 0.11, 0.34, 12)
    shade(foot, m["iron"])
    parts.append(foot)
    post = add_cyl((0, 0, 1.5), 0.075, 2.3, 10)
    shade(post, m["iron"])
    parts.append(post)
    for z in (0.9, 1.75):
        ring = add_torus((0, 0, z), 0.10, 0.028)
        shade(ring, m["iron"])
        parts.append(ring)

    # Фонарь: стеклянный короб с крышкой и завитком
    cage = add_cube((0, 0, 2.95), (0.20, 0.20, 0.28))
    shade(cage, m["glass"], False)
    parts.append(cage)
    for sx in (-1, 1):
        for sy in (-1, 1):
            edge = add_cube((0.20 * sx, 0.20 * sy, 2.95), (0.035, 0.035, 0.29))
            shade(edge, m["iron"], False)
            parts.append(edge)
    top = add_cone((0, 0, 3.36), 0.30, 0.05, 0.24, 12)
    shade(top, m["iron"])
    parts.append(top)
    knob = add_sphere((0, 0, 3.54), (0.06, 0.06, 0.08), 8, 6)
    shade(knob, m["iron"])
    parts.append(knob)
    tray = add_cube((0, 0, 2.64), (0.24, 0.24, 0.05))
    shade(tray, m["iron"], False)
    parts.append(tray)

    join(parts, "LampPost")
    export("lamp_post")


def build_fence():
    """Звено штакетника — из них собираются палисадники у домов."""
    reset()
    m = village_palette()
    parts = []

    for sx in (-1, 1):
        post = add_cube((1.9 * sx, 0, 0.55), (0.11, 0.11, 0.55))
        shade(post, m["woodDark"], False)
        parts.append(post)
        cap = add_cone((1.9 * sx, 0, 1.16), 0.15, 0.02, 0.16, 8)
        shade(cap, m["woodDark"])
        parts.append(cap)

    for z in (0.35, 0.82):
        rail = add_cube((0, 0, z), (1.9, 0.05, 0.07))
        shade(rail, m["wood"], False)
        parts.append(rail)

    for i in range(9):
        x = -1.6 + i * 0.4
        pk = add_cube((x, 0, 0.6), (0.07, 0.035, 0.6))
        shade(pk, m["wood"], False)
        parts.append(pk)
        tip = add_cone((x, 0, 1.28), 0.10, 0.01, 0.16, 6)
        shade(tip, m["wood"])
        parts.append(tip)

    join(parts, "Fence")
    export("fence")


def build_barrel():
    """Бочка — мелкий реквизит, которым обживается площадь."""
    reset()
    m = village_palette()
    parts = []

    body = add_cyl((0, 0, 0.5), 0.42, 1.0, 16)
    shade(body, m["wood"])
    parts.append(body)
    belly = add_cyl((0, 0, 0.5), 0.46, 0.55, 16)
    shade(belly, m["wood"])
    parts.append(belly)
    for z in (0.2, 0.5, 0.8):
        r = 0.465 if z == 0.5 else 0.435
        hoop = add_torus((0, 0, z), r, 0.035)
        shade(hoop, m["iron"])
        parts.append(hoop)
    lid = add_cyl((0, 0, 1.01), 0.40, 0.05, 16)
    shade(lid, m["woodDark"])
    parts.append(lid)

    join(parts, "Barrel")
    export("barrel")


def build_cart():
    """Телега у лавки — деревня выглядит обжитой, а не декорацией."""
    reset()
    m = village_palette()
    parts = []

    bed = add_cube((0, 0, 0.62), (0.85, 1.35, 0.07))
    shade(bed, m["wood"], False)
    parts.append(bed)
    for sx in (-1, 1):
        side = add_cube((0.85 * sx, 0, 0.85), (0.06, 1.35, 0.26))
        shade(side, m["woodDark"], False)
        parts.append(side)
    for sy in (-1, 1):
        end = add_cube((0, 1.35 * sy, 0.85), (0.85, 0.06, 0.26))
        shade(end, m["woodDark"], False)
        parts.append(end)

    axle = add_cyl((0, -0.3, 0.42), 0.06, 2.0, 10, (0, math.pi / 2, 0))
    shade(axle, m["woodDark"])
    parts.append(axle)

    for sx in (-1, 1):
        wheel = add_torus((0.98 * sx, -0.3, 0.42), 0.40, 0.07,
                          (0, math.pi / 2, 0))
        shade(wheel, m["woodDark"])
        parts.append(wheel)
        hubw = add_cyl((0.98 * sx, -0.3, 0.42), 0.11, 0.16, 10,
                       (0, math.pi / 2, 0))
        shade(hubw, m["wood"])
        parts.append(hubw)
        for k in range(6):
            a = k * math.pi / 3.0
            sp = add_cube((0.98 * sx, -0.3 + math.cos(a) * 0.2, 0.42 + math.sin(a) * 0.2),
                          (0.035, 0.035, 0.2), (a, 0, 0))
            shade(sp, m["wood"], False)
            parts.append(sp)

    # Оглобли
    for sx in (-1, 1):
        shaft = add_cube((0.55 * sx, 1.85, 0.55), (0.06, 0.55, 0.06), (0.12, 0, 0))
        shade(shaft, m["woodDark"], False)
        parts.append(shaft)

    # Груз: пара мешков
    for (mx, my) in ((-0.32, 0.4), (0.3, -0.25)):
        sack = add_sphere((mx, my, 0.86), (0.3, 0.34, 0.26), 10, 8)
        shade(sack, material("sack", (0.66, 0.58, 0.40), 0.95))
        parts.append(sack)

    join(parts, "Cart")
    export("cart")


# Пропорции мельницы — те же, что в WorldBuilder.Windmill.
MILL_H = 10.5      # высота башни в игре
MILL_HUB = 0.862   # доля высоты, на которой сидит вал крыльев
MILL_SPAN = 7.4    # размах крыльев


def build_windmill_assembled():
    """Только для превью: мельница в игровых пропорциях, с землёй.

    В игре это два объекта — иначе крылья не покрутить, — но проверять
    надо именно связку: крылья центрированы вокруг втулки, и стоит
    ошибиться с точкой привязки, как круг махов уходит под землю.
    """
    global SCENE_MODE
    reset()
    SCENE_MODE = True

    ground = add_cyl((0, 0, -0.3), 13, 0.6, 48)
    shade(ground, material("grass", (0.34, 0.52, 0.26), 0.95))

    _place(build_windmill, 0, 0, 0, 0, MILL_H / 9.17)
    # Крылья привязаны ЦЕНТРОМ к валу — как anchorCenter в игре.
    _place(build_windmill_blades, 0, -2.4, MILL_H * MILL_HUB, 0, MILL_SPAN / 8.9)

    # Дом рядом для масштаба: мельница должна быть ориентиром, а не великаном.
    _place(build_cottage, 11, 4, 0, -40, 1.05)

    SCENE_MODE = False


def _place(builder, x, y, z, yaw, scale):
    """Собрать ассет и поставить его копию в сцену деревни."""
    before = set(bpy.context.scene.objects)
    builder()
    made = [o for o in bpy.context.scene.objects if o not in before]
    if not made:
        return
    obj = made[-1]
    obj.rotation_euler = (0, 0, math.radians(yaw))
    obj.scale = (scale, scale, scale)
    obj.location = (x, y, z)


def build_village_scene():
    """Только для превью: деревня в сборе.

    Отдельный ассет в вакууме ничего не говорит о том, как будет выглядеть
    мир. Здесь они расставлены примерно так же, как в HubWorld: площадь,
    дома по кругу, мельница на отшибе, фонари вдоль дорожек.
    """
    global SCENE_MODE
    reset()
    SCENE_MODE = True
    m = village_palette()

    ground = add_cyl((0, 0, -0.3), 62, 0.6, 48)
    shade(ground, material("grass", (0.34, 0.52, 0.26), 0.95))

    plaza = add_cyl((0, 0, 0.06), 17, 0.14, 48)
    shade(plaza, material("pave", (0.66, 0.64, 0.60), 0.9))

    for ang in (0, 90, 180, 270):
        a = math.radians(ang)
        path = add_cube((math.cos(a) * 26, math.sin(a) * 26, 0.06),
                        (16, 2.5, 0.1), (0, 0, a))
        shade(path, material("path", (0.60, 0.52, 0.40), 0.95), False)

    # Фонтан в центре
    basin = add_cyl((0, 0, 0.45), 3.6, 0.9, 32)
    shade(basin, m["stone"])
    water = add_cyl((0, 0, 0.86), 3.1, 0.1, 32)
    shade(water, material("fountainWater", (0.24, 0.48, 0.62), 0.15))
    col = add_cyl((0, 0, 1.6), 0.55, 1.7, 16)
    shade(col, m["stone"])
    orb = add_sphere((0, 0, 2.7), (0.75, 0.75, 0.75), 16, 12)
    shade(orb, m["stone"])

    houses = [(-21, -20, 22, 1.05), (21, -21, -24, 1.15), (-31, 15, 68, 1.0),
              (32, 18, -62, 1.1), (-11, 30, 172, 0.95), (42, -6, -95, 1.2)]
    for (hx, hy, hyaw, hs) in houses:
        _place(build_cottage, hx, hy, 0, hyaw, hs)

    # Те же числа, что в игре: башня 10.5 м, вал на 0.862 её высоты,
    # размах крыльев 7.4 м. Модель башни в блендере ~9.17 м, крыльев ~8.9.
    _place(build_windmill, -48, -36, 0, 0, MILL_H / 9.17)
    _place(build_windmill_blades, -48, -38.4, MILL_H * MILL_HUB, 0, MILL_SPAN / 8.9)

    _place(build_well, -14, 12, 0, 28, 1.15)
    _place(build_cart, 13.5, 3.2, 0, -60, 1.3)
    _place(build_cart, -12, -11, 0, 140, 1.3)

    for (bx, by) in ((-11.4, 6.2), (-12.6, 7.6), (11.6, 9.1), (-12.2, -6.4), (9.2, -10.6)):
        _place(build_barrel, bx, by, 0, 30, 1.1)

    lamps = [(-4.2, 13), (4.2, 13), (-4.2, -13), (4.2, -13),
             (-13, 4.2), (-13, -4.2), (13, 4.2), (13, -4.2),
             (-4.2, 27), (4.2, 27), (-4.2, -27), (4.2, -27)]
    for (lx, ly) in lamps:
        _place(build_lamp_post, lx, ly, 0, 0, 1.15)

    # Палисадники перед двумя ближними домами
    for (cx, cy, base) in ((-21, -20, 22), (21, -21, -24)):
        for i in range(5):
            a = math.radians(base + (i - 2) * 26)
            _place(build_fence, cx + math.sin(a) * 7.5, cy + math.cos(a) * 7.5,
                   0, math.degrees(a), 1.05)

    SCENE_MODE = False


ASSETS = {
    "turtle": build_turtle,
    "mill_full": build_windmill_assembled,
    "village": build_village_scene,
    "signpost": build_signpost,
    "chest": build_treasure_chest,
    "cottage": build_cottage,
    "windmill": build_windmill,
    "blades": build_windmill_blades,
    "well": build_well,
    "lamp": build_lamp_post,
    "fence": build_fence,
    "barrel": build_barrel,
    "cart": build_cart,
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
