#!/usr/bin/env python3
"""Настоящие деревья: ствол с корнями и ветвями, крона слоями.

Прежние деревья брались из пака Kenney — низкополигональные, с прямым
как труба стволом и кроной из одного-двух шаров. Издалека сходило, вблизи
сразу видно расставленные копии одной модели.

Здесь дерево строится по правилам, а не набирается коробками:
ствол сегментами с сужением и уводом в сторону, от него отходят ветви,
крона — несколько сплюснутых шаров со сбитыми вершинами. Неровность
даётся смещением самих вершин: гладкий шар кроной не выглядит никогда,
сколько его ни крась.

    python3 tools/blender/build_trees.py

Готовые FBX кладутся в unity/Assets/Resources/Models/custom/.
"""

import math
import os
import random
import sys

try:
    import bpy
    import mathutils
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import material, shade, export, reset, ROOT  # noqa: E402


def jitter(obj, amount, seed):
    """Сбить вершины случайно — этим гладкий примитив и превращается в
    живую форму. Смещение по нормали, иначе меш выворачивает наизнанку."""
    rnd = random.Random(seed)
    for v in obj.data.vertices:
        n = v.normal
        k = 1.0 + rnd.uniform(-amount, amount)
        v.co = (v.co[0] * k, v.co[1] * k, v.co[2] * k)
        v.co = (v.co[0] + n[0] * rnd.uniform(-amount, amount) * 0.5,
                v.co[1] + n[1] * rnd.uniform(-amount, amount) * 0.5,
                v.co[2] + n[2] * rnd.uniform(-amount, amount) * 0.5)


def cone_seg(loc, r1, r2, height, rot, verts=8):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=height, location=loc)
    o = bpy.context.active_object
    o.rotation_euler = rot
    return o


def between(p0, p1, r1, r2, verts=8, overlap=1.12):
    """Конус ОТ точки ДО точки.

    Первая версия ставила сегменты по заданной высоте и наклоняла их
    вокруг собственного центра. При наклоне вертикальный размер сегмента
    становится height*cos(угол), то есть меньше шага по высоте, — и ствол
    распадался на висящие в воздухе цилиндры с просветами между ними.
    Здесь длина и поворот считаются из самого отрезка, а небольшой
    перехлёст прячет стык.
    """
    d = mathutils.Vector((p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]))
    length = d.length
    if length < 1e-5:
        return None
    mid = ((p0[0] + p1[0]) * 0.5, (p0[1] + p1[1]) * 0.5, (p0[2] + p1[2]) * 0.5)
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=length * overlap, location=mid)
    o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return o


def blob(loc, scale, seed, subdiv=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    jitter(o, 0.16, seed)
    return o


def trunk(parts, bark, height, base_r, lean, seed, segments=5):
    """Ствол из сегментов: каждый следующий уже и чуть сдвинут в сторону.

    Прямая труба — главное, что выдаёт искусственное дерево. Увод
    накапливается, поэтому верхушка заметно уходит от корня.
    """
    rnd = random.Random(seed)
    x = y = z = 0.0
    r = base_r
    for i in range(segments):
        h = height / segments
        nr = base_r * (1.0 - 0.72 * (i + 1) / segments)
        nx = x + rnd.uniform(-lean, lean)
        ny = y + rnd.uniform(-lean, lean)
        seg = between((x, y, z), (nx, ny, z + h), r, nr)
        if seg is not None:
            shade(seg, bark)
            parts.append(seg)
        x, y, z, r = nx, ny, z + h, nr
    return (x, y, z), r


def roots(parts, bark, base_r, seed, count=5):
    """Корневой наплыв: без него ствол воткнут в землю как палка."""
    rnd = random.Random(seed + 77)
    for i in range(count):
        a = i * (2 * math.pi / count) + rnd.uniform(-0.3, 0.3)
        ln = base_r * rnd.uniform(1.6, 2.4)
        seg = between((0, 0, ln * 0.55),
                      (math.cos(a) * ln, math.sin(a) * ln, 0.02),
                      base_r * 0.5, base_r * 0.14, 6)
        if seg is not None:
            shade(seg, bark)
            parts.append(seg)


def branches(parts, bark, top, height, seed, count=4):
    """Ветви от верхней трети ствола, вверх и в стороны."""
    rnd = random.Random(seed + 31)
    tx, ty, tz = top
    for i in range(count):
        a = i * (2 * math.pi / count) + rnd.uniform(-0.4, 0.4)
        zc = tz - height * rnd.uniform(0.08, 0.34)
        ln = height * rnd.uniform(0.2, 0.33)
        tilt = rnd.uniform(0.55, 0.95)
        seg = between((tx * 0.6, ty * 0.6, zc),
                      (tx * 0.6 + math.cos(a) * ln,
                       ty * 0.6 + math.sin(a) * ln,
                       zc + ln * tilt),
                      height * 0.035, height * 0.012, 6)
        if seg is not None:
            shade(seg, bark)
            parts.append(seg)


def canopy(parts, leaf_mats, top, radius, seed, layers=5, flat=0.74):
    """Крона слоями: один большой шар и несколько поменьше вокруг него,
    все со сбитыми вершинами и в трёх оттенках зелени."""
    rnd = random.Random(seed + 191)
    tx, ty, tz = top
    b = blob((tx, ty, tz + radius * 0.35), (radius, radius, radius * flat), seed)
    shade(b, leaf_mats[0])
    parts.append(b)
    for i in range(layers - 1):
        a = rnd.uniform(0, 6.28)
        d = radius * rnd.uniform(0.45, 0.85)
        rr = radius * rnd.uniform(0.45, 0.72)
        c = blob((tx + math.cos(a) * d, ty + math.sin(a) * d,
                  tz + radius * rnd.uniform(0.0, 0.75)),
                 (rr, rr, rr * flat), seed + 13 * (i + 1))
        shade(c, leaf_mats[(i + 1) % len(leaf_mats)])
        parts.append(c)


def join(parts, name):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


# ---------- деревья ----------

def build_oak():
    reset()
    bark = material("oakBark", (0.24, 0.16, 0.10), 0.9)
    leaves = [material("oakLeafA", (0.16, 0.36, 0.13), 0.9),
              material("oakLeafB", (0.22, 0.46, 0.16), 0.9),
              material("oakLeafC", (0.12, 0.28, 0.11), 0.9)]
    parts = []
    roots(parts, bark, 0.34, 5)
    top, _ = trunk(parts, bark, 3.4, 0.34, 0.12, 5)
    branches(parts, bark, top, 3.4, 5, 4)
    canopy(parts, leaves, top, 1.9, 5, 6, 0.72)
    join(parts, "TreeOak")
    export("tree_oak_natural")


def build_birch():
    reset()
    bark = material("birchBark", (0.80, 0.79, 0.74), 0.85)
    leaves = [material("birchLeafA", (0.34, 0.56, 0.20), 0.9),
              material("birchLeafB", (0.44, 0.66, 0.26), 0.9),
              material("birchLeafC", (0.26, 0.44, 0.17), 0.9)]
    parts = []
    roots(parts, bark, 0.20, 9, 4)
    # Береза выше и тоньше, увод сильнее — отсюда её силуэт.
    top, _ = trunk(parts, bark, 4.6, 0.20, 0.16, 9, 6)
    branches(parts, bark, top, 4.6, 9, 5)
    canopy(parts, leaves, top, 1.45, 9, 5, 0.92)
    join(parts, "TreeBirch")
    export("tree_birch_natural")


def build_pine():
    reset()
    bark = material("pineBark", (0.29, 0.19, 0.12), 0.9)
    green = [material("pineNeedleA", (0.10, 0.26, 0.16), 0.9),
             material("pineNeedleB", (0.14, 0.34, 0.20), 0.9),
             material("pineNeedleC", (0.08, 0.20, 0.13), 0.9)]
    parts = []
    roots(parts, bark, 0.26, 21, 5)
    top, _ = trunk(parts, bark, 5.2, 0.26, 0.07, 21, 6)

    # У хвойного крона не шарами, а ярусами лап — конусами со сбитой
    # вершиной, каждый выше и уже предыдущего.
    rnd = random.Random(21)
    for i in range(5):
        z = 1.5 + i * 0.86
        r = 1.65 * (1.0 - i * 0.16)
        c = cone_seg((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), z),
                     r, r * 0.16, 1.5,
                     (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), 0), 9)
        jitter(c, 0.09, 21 + i)
        shade(c, green[i % 3])
        parts.append(c)
    join(parts, "TreePine")
    export("tree_pine_natural")


def main():
    build_oak()
    build_birch()
    build_pine()


if __name__ == "__main__":
    main()
