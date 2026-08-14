#!/usr/bin/env python3
"""Меш бойца: стилизованный low-poly, но с настоящим силуэтом.

Кубическая болванка годилась, чтобы проверить скелет, и никуда не годится
как то, на что игрок смотрит весь матч. Здесь тело строится **лофтом по
сечениям**: каждая часть — труба, протянутая через несколько эллипсов
разного радиуса. Одна функция `loft` даёт и конус бедра, и грудную клетку,
и рукав кимоно, поэтому силуэт правится числами, а не новой геометрией.

Почему не сглаживание подразделением: оно съедает грани, а стилизация
держится именно на читаемой форме. Восьмигранные сечения плюс мягкое
затенение с порогом по углу дают округлость там, где она нужна, и
сохраняют чёткое ребро на плече и колене.

Стыки закрыты «шарнирами» — сферами в суставах, принадлежащими той же
кости, что и верхнее звено. Без них при сгибе локтя между звеньями
открывается дыра, и это первое, что выдаёт дешёвую модель.
"""

import math

import bmesh
import bpy
import mathutils

Vec = mathutils.Vector


# ------------------------------------------------------------ материалы

def material(name, rgb, roughness=0.65, metallic=0.0, sheen=0.0):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat

    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        # Ткань кимоно: без блеска по касательной она читается пластиком.
        if sheen and "Sheen Weight" in bsdf.inputs:
            bsdf.inputs["Sheen Weight"].default_value = sheen
    return mat


def palette(gi_rgb):
    # Суффикс по цвету обязателен. Материалы кешируются по имени, и без
    # него второй боец в сцене переиспользовал бы «Gi» первого — оба
    # выходили синими, а превью парных поз только для того и нужно, чтобы
    # различать роли. В игре имена материалов важнее их цвета: FighterRig
    # опознаёт по ним шейдер, поэтому суффикс идёт после «Gi», а не до.
    tag = "_%02x%02x%02x" % tuple(int(max(0.0, min(1.0, c)) * 255) for c in gi_rgb)
    return {
        "gi": material("Gi" + tag, gi_rgb, 0.92, sheen=0.35),
        "gi_dark": material("GiDark" + tag, [c * 0.72 for c in gi_rgb], 0.94, sheen=0.3),
        "skin": material("Skin" + tag, (0.80, 0.60, 0.47), 0.55),
        "belt": material("Belt" + tag, (0.045, 0.045, 0.055), 0.7),
        "hair": material("Hair" + tag, (0.08, 0.06, 0.05), 0.6),
        "eye": material("Eye" + tag, (0.06, 0.05, 0.05), 0.35),
        # Рот темнее кожи, но не чёрный: чёрная щель читается дырой.
        "mouth": material("Mouth" + tag, (0.28, 0.15, 0.14), 0.55),
    }


# ------------------------------------------------------------- геометрия

def _ring(center, rx, ry, sides, axis, roll=0.0):
    """Кольцо вершин-эллипс вокруг центра, перпендикулярно оси `axis`."""
    axis = Vec(axis).normalized()

    # Опорный вектор для построения базиса. Берём не тот, что почти
    # совпадает с осью, иначе векторное произведение вырождается в ноль и
    # сечение схлопывается в линию.
    ref = Vec((0.0, 0.0, 1.0))
    if abs(axis.dot(ref)) > 0.95:
        ref = Vec((0.0, 1.0, 0.0))

    u = axis.cross(ref).normalized()
    v = axis.cross(u).normalized()

    verts = []
    for i in range(sides):
        a = 2.0 * math.pi * i / sides + roll
        verts.append(Vec(center) + u * (math.cos(a) * rx) + v * (math.sin(a) * ry))
    return verts


def loft(name, sections, sides=8, cap_start=True, cap_end=True, roll=0.0):
    """Труба через сечения. Сечение: (центр, радиус_X, радиус_Y).

    Ось каждого кольца берётся по направлению к следующему сечению, чтобы
    труба не заламывалась на изгибах.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    rings = []
    for i, (center, rx, ry) in enumerate(sections):
        if i < len(sections) - 1:
            axis = Vec(sections[i + 1][0]) - Vec(center)
        else:
            axis = Vec(center) - Vec(sections[i - 1][0])
        if axis.length < 1e-6:
            axis = Vec((0.0, 0.0, 1.0))

        ring = [bm.verts.new(p) for p in _ring(center, rx, ry, sides, axis, roll)]
        rings.append(ring)

    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((a[i], a[j], b[j], b[i]))

    if cap_start:
        bm.faces.new(tuple(reversed(rings[0])))
    if cap_end:
        bm.faces.new(tuple(rings[-1]))

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def limb(name, a, b, ra, rb, sides=8, mid=None):
    """Звено конечности: конус от точки `a` радиуса `ra` к `b` радиуса `rb`.

    `mid` — необязательное утолщение посередине (бицепс, икра): без него
    рука выглядит трубой, а не рукой.
    """
    a, b = Vec(a), Vec(b)
    if mid is None:
        sections = [(a, ra, ra), (b, rb, rb)]
    else:
        c = a.lerp(b, 0.42)
        sections = [(a, ra, ra), (c, mid, mid), (b, rb, rb)]
    return loft(name, sections, sides=sides)


def blob(name, center, radius, scale=(1.0, 1.0, 1.0), segments=10, rings=6):
    """Скруглённый объём: суставы, кисти, голова."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    bmesh.ops.scale(bm, vec=Vec(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vec(center), verts=bm.verts)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def box(name, center, half, rotation=(0.0, 0.0, 0.0)):
    """Параллелепипед — отвороты кимоно, узел пояса, брови."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.scale(bm, vec=Vec(half), verts=bm.verts)

    rot = mathutils.Euler(rotation, "XYZ").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vec(center), verts=bm.verts)

    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def finish(obj, mat, smooth=True):
    """Назначает материал и помечает грани мягкими."""
    obj.data.materials.append(mat)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    return obj


def auto_smooth(obj, angle=40.0):
    """Мягкое затенение с порогом по углу — вызывать один раз на готовом меше.

    Порог не косметика: без него сглаживание размазывает ребро плеча и
    колена, и стилизованная фигура превращается в оплывшую свечу. В
    Blender 4.1+ это делается оператором, флаг use_auto_smooth убрали.
    """
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
    except Exception as exc:
        print("  авто-сглаживание недоступно:", exc)
