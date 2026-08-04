#!/usr/bin/env python3
"""Герой игры про Кёнигсберг: рыцарь в латах.

Кот-проводник Кёня остаётся спутником на карте, но играть за него в RPG
в духе Diablo 2 нечем — нужен воин. Рыцарь берёт готовый скелет и все семь
клипов из build_hero (Idle, Walk, Run, Jump, Bite_InPlace, HitRecieve,
Death), поэтому здесь только геометрия.

ПОЧЕМУ НЕ КОРОБКИ. Остальные наши персонажи кубические — это осознанный
стиль майнкрафта, и он был уместен в игре про медведя. Здесь он не
подходит: рядом стоят дома Quaternius со скруглённой черепицей и
фасками, и кубический герой рядом с ними выглядит чужим. Поэтому силуэт
собран из усечённых пирамид (броня) и низкополигональных конусов
(конечности) с фасками — то же семейство форм, что у пака.

Перёд у наших моделей — это −Y: там прорези глаз у Железного человечка,
и Unity ждёт того же.

Запуск:
    blender -b --python tools/blender/build_knight.py

Готовый FBX кладётся в unity/Assets/Resources/Models/monsters/Knight.fbx.
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Скрипт запускается внутри Blender: blender -b --python ...")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import material  # noqa: E402
from build_hero import PX, assemble, export, reset  # noqa: E402

NAME = "Knight"


# ---------- палитра ----------

def palette():
    return {
        # Сталь тёмная и слегка синеватая: на солнце Кранца она даст
        # холодный блик против тёплой черепицы — это и держит кадр.
        "steel": material("kSteel", (0.40, 0.43, 0.50), 0.34, 0.90),
        "steelDark": material("kSteelDark", (0.17, 0.19, 0.24), 0.44, 0.85),
        "gold": material("kGold", (0.84, 0.63, 0.19), 0.26, 1.00),
        # Красный плащ — единственный тёплый акцент на герое. По нему
        # рыцарь читается в любой точке кадра, даже когда мелкий.
        "cloth": material("kCloth", (0.56, 0.09, 0.10), 0.68, 0.00),
        "clothDark": material("kClothDark", (0.34, 0.05, 0.06), 0.72, 0.00),
        "leather": material("kLeather", (0.26, 0.17, 0.10), 0.80, 0.00),
        "wood": material("kWood", (0.33, 0.22, 0.13), 0.78, 0.00),
    }


# ---------- формы ----------

def _finish(obj, mat, bone, parts, bevel):
    obj.data.materials.append(mat)
    if bevel > 0:
        m = obj.modifiers.new("bevel", "BEVEL")
        m.width = bevel
        m.segments = 2
        m.limit_method = "ANGLE"
        m.angle_limit = math.radians(40)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=m.name)
    parts.append((obj, bone))
    return obj


def prism(bone, parts, mat, x, y, z0, z1, bot, top, shift=(0.0, 0.0), bevel=0.012):
    """Усечённая пирамида: низ и верх разного размера.

    Именно она отличает броню от коробки — наплечник расширяется кверху,
    юбка книзу, кираса сужается к поясу. Восемь вершин задаются руками,
    без операторов: так форма предсказуема и не зависит от того, что
    Blender считает «активным объектом».
    """
    bx, by = bot
    tx, ty = top
    sx, sy = shift
    v = [
        (x - bx, y - by, z0), (x + bx, y - by, z0),
        (x + bx, y + by, z0), (x - bx, y + by, z0),
        (x - tx + sx, y - ty + sy, z1), (x + tx + sx, y - ty + sy, z1),
        (x + tx + sx, y + ty + sy, z1), (x - tx + sx, y + ty + sy, z1),
    ]
    f = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2),
         (2, 6, 7, 3), (3, 7, 4, 0)]
    me = bpy.data.meshes.new("prism")
    me.from_pydata(v, [], f)
    me.update()
    obj = bpy.data.objects.new("prism", me)
    bpy.context.collection.objects.link(obj)
    return _finish(obj, mat, bone, parts, bevel)


def tube(bone, parts, mat, x, y, z0, z1, r0, r1, seg=10, bevel=0.008):
    """Низкополигональный конус — рука, нога, шея, древко.

    Десять граней достаточно, чтобы конечность читалась круглой, и мало
    настолько, чтобы силуэт остался гранёным, как у моделей пака.
    """
    bpy.ops.mesh.primitive_cone_add(
        vertices=seg, radius1=r0, radius2=r1, depth=(z1 - z0),
        location=(x, y, (z0 + z1) * 0.5))
    obj = bpy.context.active_object
    return _finish(obj, mat, bone, parts, bevel)


def dome(bone, parts, mat, x, y, z, rx, ry, rz, bevel=0.006):
    """Округлый колпак — наплечник, наколенник, навершие.

    Плоская пластина на плече читалась подносом; сфера с малым числом
    сегментов даёт то же гранёное семейство форм, что у пака, но силуэт
    становится плечом, а не полкой.
    """
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, location=(x, y, z))
    obj = bpy.context.active_object
    obj.scale = (rx, ry, rz)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(obj, mat, bone, parts, bevel)


def plate(bone, parts, mat, x, y, z, sx, sy, sz, rot=(0, 0, 0), bevel=0.008):
    """Плоская пластина под углом — забрало, щит, полы плаща."""
    bpy.ops.mesh.primitive_cube_add(location=(x, y, z))
    obj = bpy.context.active_object
    obj.scale = (sx, sy, sz)
    obj.rotation_euler = tuple(math.radians(a) for a in rot)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return _finish(obj, mat, bone, parts, bevel)


# ---------- тело ----------

def build_body(m):
    P = PX
    parts = []

    # --- ноги ---
    # Кость ноги идёт от бедра (12) вниз к земле (0), поэтому вся
    # геометрия ноги висит на ней целиком.
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        x = sx * 2.1 * P
        # Сабатон: шире у земли и вытянут вперёд носком.
        prism(bone, parts, m["steel"], x, -0.5 * P, 0.0, 2.2 * P,
              (2.3 * P, 3.1 * P), (1.9 * P, 2.1 * P), shift=(0, 0.5 * P))
        # Поножи и набедренник — конусы, сужаются к колену и к щиколотке.
        tube(bone, parts, m["steel"], x, 0, 2.2 * P, 6.6 * P, 2.0 * P, 1.55 * P)
        prism(bone, parts, m["steelDark"], x, -0.35 * P, 6.4 * P, 7.6 * P,
              (1.75 * P, 1.9 * P), (1.85 * P, 2.0 * P))
        tube(bone, parts, m["steel"], x, 0, 7.4 * P, 12.2 * P, 2.05 * P, 2.3 * P)

    # --- юбка-фалда: расширяется книзу, прикрывает бёдра ---
    prism("Hips", parts, m["steel"], 0, 0, 9.6 * P, 13.4 * P,
          (4.5 * P, 3.2 * P), (3.7 * P, 2.5 * P))
    prism("Hips", parts, m["leather"], 0, 0, 13.2 * P, 14.2 * P,
          (3.75 * P, 2.55 * P), (3.6 * P, 2.4 * P))
    prism("Hips", parts, m["gold"], 0, -2.35 * P, 13.7 * P, 14.3 * P,
          (0.9 * P, 0.35 * P), (0.8 * P, 0.3 * P))

    # --- кираса: сужена в поясе, широкая в груди ---
    prism("Spine", parts, m["steel"], 0, 0, 13.8 * P, 21.5 * P,
          (2.9 * P, 1.95 * P), (4.5 * P, 2.75 * P))
    # Нагрудный киль — рёбрышко по центру, ловит блик.
    prism("Spine", parts, m["steel"], 0, -2.5 * P, 15.0 * P, 21.0 * P,
          (0.7 * P, 0.5 * P), (0.9 * P, 0.55 * P))
    # Горжет и оплечье.
    prism("Spine", parts, m["steelDark"], 0, 0, 21.3 * P, 23.6 * P,
          (4.7 * P, 2.9 * P), (3.0 * P, 2.2 * P))
    prism("Spine", parts, m["gold"], 0, 0, 23.4 * P, 24.2 * P,
          (2.5 * P, 1.9 * P), (2.2 * P, 1.7 * P))

    # --- плащ: широкий, от плеч до икр, чуть расходится книзу ---
    prism("Spine", parts, m["cloth"], 0, 2.9 * P, 6.0 * P, 22.5 * P,
          (4.6 * P, 0.35 * P), (3.6 * P, 0.3 * P), shift=(0, -0.5 * P))
    prism("Spine", parts, m["clothDark"], 0, 3.15 * P, 6.0 * P, 8.0 * P,
          (4.7 * P, 0.2 * P), (4.6 * P, 0.2 * P))

    # --- руки ---
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        x = sx * 6.0 * P
        # Наплечник сидит на руке, а не на корпусе: тогда он ходит вместе
        # с замахом, и удар читается плечом, а не только кистью.
        dome(bone, parts, m["steel"], x + sx * 0.5 * P, 0, 21.9 * P,
             2.5 * P, 2.3 * P, 2.0 * P)
        prism(bone, parts, m["gold"], x + sx * 0.5 * P, 0, 20.6 * P, 21.2 * P,
              (2.35 * P, 2.15 * P), (2.5 * P, 2.3 * P))
        tube(bone, parts, m["steel"], x, 0, 16.6 * P, 21.0 * P, 1.55 * P, 1.35 * P)
        prism(bone, parts, m["steelDark"], x, 0, 15.9 * P, 16.9 * P,
              (1.6 * P, 1.7 * P), (1.7 * P, 1.8 * P))
        tube(bone, parts, m["steel"], x, 0, 12.4 * P, 16.2 * P, 1.5 * P, 1.25 * P)
        # Латная перчатка.
        prism(bone, parts, m["steelDark"], x, -0.2 * P, 11.0 * P, 12.6 * P,
              (1.5 * P, 1.6 * P), (1.6 * P, 1.7 * P))

    # --- голова: шлем-бацинет ---
    # Голова у кубических героев 8 px в ширину, и от этого силуэт читался
    # почтовым ящиком. Шлем сужен до 4.8 px и вытянут вверх конусом —
    # так он держит те же пропорции, что купола башен пака.
    tube("Head", parts, m["steel"], 0, 0.1 * P, 24.5 * P, 29.4 * P,
         2.45 * P, 2.05 * P, seg=10)
    # Забрало клином: острая грань вперёд, по ней видно направление взгляда.
    prism("Head", parts, m["steelDark"], 0, -1.5 * P, 25.2 * P, 28.9 * P,
          (1.9 * P, 1.35 * P), (1.75 * P, 1.15 * P), shift=(0, -0.45 * P))
    prism("Head", parts, m["steelDark"], 0, -2.75 * P, 26.4 * P, 28.6 * P,
          (1.15 * P, 0.7 * P), (1.0 * P, 0.55 * P), shift=(0, -0.35 * P))
    # Смотровая щель.
    prism("Head", parts, m["gold"], 0, -3.15 * P, 27.5 * P, 27.95 * P,
          (1.5 * P, 0.16 * P), (1.45 * P, 0.16 * P))
    # Купол сходится к навершию.
    tube("Head", parts, m["steel"], 0, 0.1 * P, 29.2 * P, 31.0 * P,
         2.05 * P, 0.55 * P, seg=10)
    # Плюмаж: узкий гребень, отклонён назад.
    prism("Head", parts, m["cloth"], 0, 1.0 * P, 30.4 * P, 33.2 * P,
          (0.34 * P, 1.5 * P), (0.22 * P, 0.9 * P), shift=(0, 1.5 * P))

    # --- меч в правой руке ---
    grip = "Arm.R"
    gx = -6.0 * P
    tube(grip, parts, m["leather"], gx, -1.6 * P, 9.2 * P, 11.4 * P,
         0.42 * P, 0.42 * P, seg=8)
    prism(grip, parts, m["gold"], gx, -1.6 * P, 8.8 * P, 9.3 * P,
          (0.75 * P, 0.75 * P), (0.7 * P, 0.7 * P))
    prism(grip, parts, m["gold"], gx, -1.6 * P, 11.3 * P, 11.9 * P,
          (2.9 * P, 0.5 * P), (2.7 * P, 0.45 * P))
    # Клинок сужается к острию — иначе меч читается палкой.
    prism(grip, parts, m["steel"], gx, -1.6 * P, 11.8 * P, 22.5 * P,
          (0.62 * P, 0.17 * P), (0.30 * P, 0.11 * P))

    # --- щит на левой руке ---
    sh = "Arm.L"
    shx = 6.0 * P + 1.5 * P
    prism(sh, parts, m["wood"], shx, -1.5 * P, 12.0 * P, 19.5 * P,
          (2.6 * P, 0.35 * P), (2.9 * P, 0.4 * P))
    prism(sh, parts, m["wood"], shx, -1.5 * P, 9.4 * P, 12.2 * P,
          (0.6 * P, 0.3 * P), (2.6 * P, 0.35 * P))
    prism(sh, parts, m["cloth"], shx - 0.15 * P, -1.9 * P, 12.4 * P, 19.2 * P,
          (2.2 * P, 0.12 * P), (2.4 * P, 0.14 * P))
    prism(sh, parts, m["gold"], shx - 0.2 * P, -2.0 * P, 15.0 * P, 16.4 * P,
          (0.9 * P, 0.1 * P), (0.8 * P, 0.1 * P))

    return parts


def build():
    reset()
    return assemble(NAME, build_body(palette()))


def main():
    build()
    export(NAME)


if __name__ == "__main__":
    main()
