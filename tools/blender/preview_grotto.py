#!/usr/bin/env python3
"""Превью «грота»: сцена по мотивам референса.

Смысл не в красивой картинке, а в проверке замысла до Unity: сходятся ли
вместе мшистый камень, лианы, папоротники, факелы, кристаллы и бирюзовая
вода — или по отдельности всё хорошо, а рядом мешается.

Материалы здесь повторяют то, что делает Bear/MossyStone в игре: камень с
кладкой, мох на верхних гранях, тёплый свет факелов против холодной воды.
Cycles считает это иначе, чем URP, поэтому картинка — не скриншот игры, а
проверка композиции и палитры.

    python3 tools/blender/preview_grotto.py
"""

import math
import os
import random
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import (material, shade, add_cube, add_cyl, add_cone,  # noqa: E402
                          add_sphere, ROOT)

OUT = os.path.join(ROOT, "tools", "blender", "preview")
random.seed(4242)


# ---------- материалы ----------

def mats():
    m = {
        "stone": material("stone", (0.33, 0.30, 0.27), 0.88),
        "stoneDark": material("stoneDark", (0.13, 0.13, 0.13), 0.92),
        "moss": material("moss", (0.11, 0.34, 0.09), 0.95),
        "mossLit": material("mossLit", (0.26, 0.60, 0.15), 0.9),
        "leaf": material("leaf", (0.08, 0.30, 0.10), 0.95),
        "leafLit": material("leafLit", (0.22, 0.56, 0.16), 0.92),
        "bark": material("bark", (0.18, 0.11, 0.07), 0.9),
        "wood": material("wood", (0.30, 0.19, 0.10), 0.88),
        "iron": material("iron", (0.16, 0.15, 0.16), 0.4, 0.8),
    }
    m["water"] = glass("water", (0.02, 0.38, 0.46))
    m["flame"] = emit("flame", (1.0, 0.52, 0.16), 46.0)
    m["gemC"] = emit("gemCyan", (0.35, 0.85, 1.0), 12.0)
    m["gemV"] = emit("gemViolet", (0.66, 0.36, 1.0), 10.0)
    m["coin"] = material("coin", (1.0, 0.78, 0.16), 0.25, 1.0)
    return m


def emit(name, rgb, strength):
    mat = material(name, rgb, 0.2)
    b = mat.node_tree.nodes.get("Principled BSDF")
    if b:
        for k in ("Emission Color", "Emission"):
            if k in b.inputs:
                b.inputs[k].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
                break
        if "Emission Strength" in b.inputs:
            b.inputs["Emission Strength"].default_value = strength
    return mat


def haze(name, rgb, strength):
    """Аддитивное свечение — то же, что делает Bear/Shaft в игре.

    Ставить эмиссию на Principled и глушить её полем Alpha БЕСПОЛЕЗНО:
    в Cycles альфа гасит только сам BSDF, а эмиссия прибавляется поверх
    неё в полную силу. Первая попытка так и вышла — три белых конуса в
    полкадра при alpha 0.085. Здесь граф собран честно: Transparent BSDF
    пропускает фон, Add Shader прибавляет свет.

    Множитель (1 - Facing) гасит луч на силуэте: у кромки конуса взгляд
    проходит сквозь тонкий слой воздуха, в середине — сквозь всю толщу.
    Ровно эту же зависимость считает шейдер в игре.
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    add = nt.nodes.new("ShaderNodeAddShader")
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    em = nt.nodes.new("ShaderNodeEmission")
    lw = nt.nodes.new("ShaderNodeLayerWeight")
    inv = nt.nodes.new("ShaderNodeMath")
    mul = nt.nodes.new("ShaderNodeMath")

    em.inputs["Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
    lw.inputs["Blend"].default_value = 0.3
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = strength

    nt.links.new(lw.outputs["Facing"], inv.inputs[1])
    nt.links.new(inv.outputs[0], mul.inputs[0])
    nt.links.new(mul.outputs[0], em.inputs["Strength"])
    nt.links.new(clear.outputs[0], add.inputs[0])
    nt.links.new(em.outputs[0], add.inputs[1])
    nt.links.new(add.outputs[0], out.inputs["Surface"])
    return mat


def wet(name, rgb, alpha, rough=0.08):
    """Полупрозрачная вода без свечения.

    Именно без: см. haze — эмиссия игнорирует альфу, поэтому светящаяся
    струя всегда выходит непрозрачной белой полосой.
    """
    mat = material(name, rgb, rough)
    b = mat.node_tree.nodes.get("Principled BSDF")
    if b and "Alpha" in b.inputs:
        b.inputs["Alpha"].default_value = alpha
    return mat


def glass(name, rgb):
    mat = material(name, rgb, 0.05)
    b = mat.node_tree.nodes.get("Principled BSDF")
    if b and "Transmission Weight" in b.inputs:
        b.inputs["Transmission Weight"].default_value = 0.85
    return mat


# ---------- кирпичи сцены ----------

def mossy_slab(m, cx, cy, cz, hx, hy, hz):
    """Каменная плита с мохнатой верхушкой и тёмными стыками.

    Так же устроен и шейдер в игре: камень внизу, мох сверху пятнами,
    кладка — сеткой блоков.
    """
    body = add_cube((cx, cy, cz), (hx, hy, hz))
    shade(body, m["stone"], False)

    # Кладка: ряды блоков с перевязкой, чуть выступающие из плиты.
    step = 0.9
    rows = max(1, int(hz * 2 / 0.55))
    for r in range(rows):
        z = cz - hz + 0.28 + r * 0.55
        offset = (r % 2) * step * 0.5
        n = int(hx * 2 / step) + 1
        for i in range(n):
            x = cx - hx + offset + i * step
            if x > cx + hx - 0.1:
                continue
            b = add_cube((x, cy - hy - 0.03, z), (step * 0.42, 0.04, 0.22))
            shade(b, m["stoneDark"] if (i + r) % 3 == 0 else m["stone"], False)

    # Мох сверху — сплошной ковёр с более светлыми пятнами. Раньше это была
    # только россыпь приплюснутых сфер, и верхушка плиты читалась гроздью
    # винограда; в игре мох тоже не комками, а весом в шейдере по n.y.
    carpet = add_cube((cx, cy, cz + hz - 0.01), (hx * 0.995, hy * 0.995, 0.09))
    shade(carpet, m["moss"], False)
    for i in range(int(hx * hy * 6) + 4):
        px = cx + random.uniform(-hx, hx)
        py = cy + random.uniform(-hy, hy)
        rad = random.uniform(0.3, 0.7)
        blob = add_sphere((px, py, cz + hz + 0.04), (rad, rad, rad * 0.13), 10, 6)
        shade(blob, m["mossLit"] if random.random() < 0.5 else m["moss"])
    for i in range(int(hx * 5) + 3):
        px = cx + random.uniform(-hx, hx)
        s = random.choice((-1, 1))
        drip = add_sphere((px, cy + hy * s, cz + hz - random.uniform(0.1, 0.5)),
                          (0.3, 0.16, 0.34), 10, 6)
        shade(drip, m["moss"])
    return body


def vine(m, x, y, z, length):
    n = max(2, int(length / 0.5))
    yaw = random.uniform(0, 6.28)
    drift = random.uniform(0.05, 0.16)
    for i in range(n):
        t = (i + 0.5) / n
        px = x + math.cos(yaw) * drift * length * t * t
        py = y + math.sin(yaw) * drift * length * t * t
        seg = add_cube((px, py, z - length * t), (0.05, 0.03, length / n * 0.55))
        shade(seg, m["leaf"], False)
        if i % 2:
            lf = add_sphere((px + 0.1, py, z - length * t), (0.16, 0.07, 0.13), 8, 6)
            shade(lf, m["leafLit"])


def fern(m, x, y, z, scale=1.0):
    blades = random.randint(7, 11)
    for i in range(blades):
        a = i / blades * 6.28 + random.uniform(-0.2, 0.2)
        ln = random.uniform(0.6, 1.05) * scale
        # Лист СУЖАЕТСЯ к концу. Раньше это был брусок ровной толщины, и
        # куст читался колючей звездой из палок, а не папоротником.
        blade = add_cone((x + math.cos(a) * ln * 0.42, y + math.sin(a) * ln * 0.42,
                          z + ln * 0.4), 0.15 * scale, 0.008, ln, 6,
                         (math.radians(random.uniform(34, 60)) * math.sin(a),
                          math.radians(random.uniform(34, 60)) * -math.cos(a), 0))
        shade(blade, m["leafLit"] if i % 3 else m["leaf"])
    core = add_sphere((x, y, z + 0.1 * scale), (0.2 * scale, 0.2 * scale, 0.13 * scale), 10, 6)
    shade(core, m["leaf"])


def torch(m, x, y, z, energy=180):
    shade(add_cube((x, y, z), (0.09, 0.09, 0.17)), m["iron"], False)
    sh = add_cyl((x, y - 0.18, z + 0.26), 0.045, 0.44, 10, (math.radians(28), 0, 0))
    shade(sh, m["wood"])
    bowl = add_cyl((x, y - 0.36, z + 0.5), 0.12, 0.08, 12)
    shade(bowl, m["iron"])
    fl = add_sphere((x, y - 0.36, z + 0.62), (0.13, 0.13, 0.22), 12, 8)
    shade(fl, m["flame"])
    bpy.ops.object.light_add(type="POINT", location=(x, y - 0.4, z + 0.66))
    li = bpy.context.active_object
    li.data.energy = energy
    li.data.color = (1.0, 0.63, 0.3)
    li.data.shadow_soft_size = 0.35


def gems(m, x, y, z, violet=False):
    mat = m["gemV"] if violet else m["gemC"]
    for i in range(random.randint(3, 5)):
        a = random.uniform(0, 6.28)
        r = random.uniform(0.05, 0.22)
        c = add_cone((x + math.cos(a) * r, y + math.sin(a) * r, z + 0.2),
                     random.uniform(0.07, 0.13), 0.01, random.uniform(0.3, 0.6), 6,
                     (math.radians(random.uniform(-20, 20)), 0, 0))
        shade(c, mat)
    bpy.ops.object.light_add(type="POINT", location=(x, y, z + 0.35))
    li = bpy.context.active_object
    li.data.energy = 55
    li.data.color = (0.66, 0.36, 1.0) if violet else (0.35, 0.85, 1.0)


def waterfall(m, x, y, ztop, height, width):
    """Водопад в слоях: три полотна на разной глубине, отдельные струи,
    валик на гребне и пенный вал у подошвы.

    Две ошибки, которые здесь исправлены и хорошо видны на рендере. Первая:
    полотна были из glass() с Transmission 0.85 — прозрачное стекло, масса
    воды пропадала целиком, оставались только светящиеся струи. Вторая:
    валик на гребне брал радиус width * 0.55 и лежал диском К КАМЕРЕ, то
    есть был шире самой струи — водопад читался эскимо на палочке.
    """
    tag = "%d_%d" % (int(x * 10), int(y * 10))

    # Порог, с которого падает вода. Без него струя начиналась в пустоте:
    # на первом рендере оба водопада висели столбами в воздухе.
    mossy_slab(m, x, y + 1.6, ztop - 0.5, width * 1.5, 1.5, 0.5)

    # Полотна: дальнее пошире и разреженнее, среднее — основная масса.
    for i, (dy, w, a) in enumerate(((-0.24, 1.08, 0.30),
                                    (0.00, 1.00, 0.54),
                                    (0.22, 0.84, 0.26))):
        sheet = add_cube((x, y + dy, ztop - height * 0.5),
                         (width * 0.5 * w, 0.02, height * 0.5))
        shade(sheet, wet("fall%s_%d" % (tag, i), (0.16, 0.55, 0.68), a))

    # Отдельные струи — узкие и плотнее массы, но не светящиеся.
    for i in range(7):
        sh = height * random.uniform(0.5, 1.0)
        strand = add_cube((x + random.uniform(-width * 0.45, width * 0.45), y - 0.3,
                           ztop - sh * 0.5), (width * 0.05, 0.015, sh * 0.5))
        shade(strand, wet("strand%s_%d" % (tag, i), (0.72, 0.92, 1.0), 0.8))

    # Валик на гребне: цилиндр ВДОЛЬ кромки, по ширине струи.
    crest = add_cyl((x, y, ztop - 0.02), 0.13, width * 1.04, 12,
                    (0, math.radians(90), 0))
    shade(crest, wet("crest%s" % tag, (0.80, 0.95, 1.0), 0.9))

    # Подошва: приплюснутый пенный вал и брызги вокруг.
    pool = add_sphere((x, y, ztop - height + 0.06),
                      (width * 0.85, width * 0.62, 0.2), 16, 8)
    shade(pool, wet("pool%s" % tag, (0.88, 0.97, 1.0), 0.72))
    for i in range(11):
        a = random.uniform(0, 6.28)
        r = random.uniform(0.15, width * 1.1)
        s = random.uniform(0.07, 0.17)
        sp = add_sphere((x + math.cos(a) * r, y + math.sin(a) * r * 0.6,
                         ztop - height + random.uniform(0.1, 0.95)), (s, s, s), 8, 6)
        shade(sp, wet("spray%s_%d" % (tag, i), (0.92, 0.98, 1.0), 0.6))


def light_shaft(m, x, y, ztop, length, spread, rgb, strength=0.45):
    """Луч света: конус аддитивного свечения вершиной в проёме."""
    cone = add_cone((x, y, ztop - length * 0.5), spread, 0.05, length, 18,
                    (math.radians(180), 0, 0))
    shade(cone, haze("shaft%d_%d" % (int(x * 10), int(y * 10)), rgb, strength))


def tree(m, x, y, z, scale=1.0):
    tr = add_cyl((x, y, z + 2.2 * scale), 0.42 * scale, 4.4 * scale, 12)
    shade(tr, m["bark"])
    for i in range(5):
        a = i * 1.26
        rt = add_cyl((x + math.cos(a) * 0.6 * scale, y + math.sin(a) * 0.6 * scale,
                      z + 0.35 * scale), 0.17 * scale, 0.9 * scale, 8,
                     (math.radians(22) * math.sin(a), math.radians(22) * -math.cos(a), 0))
        shade(rt, m["bark"])
    for (dx, dy, dz, r) in ((0, 0, 4.6, 2.3), (1.5, 0.6, 3.9, 1.5),
                            (-1.4, -0.5, 4.0, 1.4), (0.4, -1.3, 5.2, 1.3)):
        c = add_sphere((x + dx * scale, y + dy * scale, z + dz * scale),
                       (r * scale, r * scale, r * 0.78 * scale), 16, 10)
        shade(c, m["leafLit"] if r > 2 else m["leaf"])
    for i in range(6):
        a = random.uniform(0, 6.28)
        vine(m, x + math.cos(a) * 1.9 * scale, y + math.sin(a) * 1.9 * scale,
             z + 4.2 * scale, random.uniform(1.4, 3.2))


# ---------- сцена ----------

def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m = mats()

    # Вода: не только далёкий залив, но и всё дно кадра — в референсе
    # платформы висят над бирюзовой водой. Прошлая плита стояла в y = 26 и
    # уходила за горизонт, поэтому низ кадра был пустой синевой.
    sea = add_cube((3, 12, -1.6), (30, 26, 0.5))
    shade(sea, m["water"])

    # Скальный массив справа, в котором прорублен вход — тёмная рамка кадра.
    mossy_slab(m, 12.5, 2.5, 1.6, 5.0, 4.0, 3.2)
    mossy_slab(m, 13.5, 2.0, 6.4, 4.2, 3.4, 2.0)
    # Арка-проём в скале: опоры и клинчатый свод, как в собранном ассете.
    for sx in (-1, 1):
        for r in range(5):
            pier = add_cube((8.4 + sx * 1.5, -1.2, 0.3 + r * 0.56), (0.34, 1.2, 0.28))
            shade(pier, m["stone"] if r % 2 else m["stoneDark"], False)
    for i in range(9):
        a = math.pi * i / 8.0
        # Клин лежит ВДОЛЬ дуги: угол a - pi/2 (см. build_stone_arch).
        wedge = add_cube((8.4 - math.cos(a) * 1.5, -1.2, 3.1 + math.sin(a) * 1.35),
                         (0.24, 1.2, 0.3), (0, a - math.pi / 2, 0))
        shade(wedge, m["stoneDark"] if i % 3 == 1 else m["stone"], False)
    key = add_cube((8.4, -1.2, 4.58), (0.3, 1.26, 0.3))
    shade(key, m["stone"], False)
    torch(m, 8.0, -2.4, 2.6)
    torch(m, 11.2, -1.6, 5.6, 140)
    gems(m, 10.4, -1.9, 4.9)
    gems(m, 9.2, -2.1, 0.2, True)

    # Главная платформа слева, обжитая зеленью.
    mossy_slab(m, -5.0, 0.0, 2.0, 6.0, 3.0, 0.7)
    for i in range(13):
        fern(m, random.uniform(-10.6, 0.8), random.uniform(-2.8, 2.8), 2.7,
             random.uniform(0.7, 1.2))
    for i in range(6):
        a = random.uniform(0, 6.28)
        vine(m, -5.0 + math.cos(a) * 5.6, 0.0 + math.sin(a) * 2.8, 1.3,
             random.uniform(1.2, 3.0))
    torch(m, -10.2, -2.9, 2.9, 120)

    # Дерево-великан у левого края. В x = -10 оно попадало точно в тень
    # заслонения левой рамки, поэтому сдвинуто внутрь кадра.
    tree(m, -8.0, 3.4, 2.7, 1.25)

    # Парящий островок с монетами.
    mossy_slab(m, 2.6, 3.0, 6.4, 2.2, 1.6, 0.5)
    for i in range(3):
        c = add_cyl((1.7 + i * 0.85, 3.0, 7.7), 0.26, 0.07, 16, (math.radians(90), 0, 0))
        shade(c, m["coin"])
    for i in range(4):
        a = random.uniform(0, 6.28)
        vine(m, 2.6 + math.cos(a) * 2.0, 3.0 + math.sin(a) * 1.4, 5.9,
             random.uniform(1.0, 2.4))
    fern(m, 2.2, 3.0, 6.9, 0.8)

    # Лучи света: из свода над скалой и сквозь листву у дерева. Теперь они
    # честно прозрачные, поэтому могут быть шире — гасит их не размер.
    # Силу пришлось мерить, а не подбирать на глаз. Замер конуса на фоне
    # 0.15 линейных: при strength 0.13 центр давал прибавку 0.25 — вдвое
    # больше номинала, потому что конус не отсекает грани и передняя со
    # задней прибавляют каждая. Итого видимая яркость ≈ 2 * strength, и
    # чтобы луч читался дымкой, а не молоком, прибавка должна быть заметно
    # МЕНЬШЕ фона: отсюда сотые доли.
    light_shaft(m, 8.4, 1.5, 11.0, 8.0, 1.5, (1.0, 0.94, 0.72), 0.05)
    light_shaft(m, -6.4, 2.0, 12.0, 9.0, 1.9, (1.0, 0.96, 0.78), 0.04)
    light_shaft(m, 2.6, 3.0, 11.5, 5.0, 1.1, (0.9, 0.96, 1.0), 0.035)

    # Водопады: один за платформой, один из скалы.
    waterfall(m, -2.0, 7.0, 8.6, 8.0, 1.5)
    waterfall(m, 6.4, 5.4, 5.4, 6.0, 1.0)

    # Верёвочный мост от платформы к скале.
    for i in range(11):
        t = i / 10.0
        x = 1.2 + t * 6.2
        sag = math.sin(t * math.pi) * 0.75
        plank = add_cube((x, -1.0, 2.05 - sag), (0.3, 0.55, 0.05))
        shade(plank, m["wood"], False)
    for s in (-1, 1):
        for i in range(11):
            t = i / 10.0
            x = 1.2 + t * 6.2
            sag = math.sin(t * math.pi) * 0.75
            rope = add_cube((x, -1.0 + 0.55 * s, 2.55 - sag * 0.8), (0.31, 0.03, 0.03))
            shade(rope, m["wood"], False)

    # Тёмная рамка: близкая порода по углам кадра. В референсе именно она
    # держит глубину — светлая даль против почти чёрного переднего плана.
    #
    # Координаты здесь не на глаз: камера стоит в y = -26 с объективом 42 мм,
    # поэтому на плане рамки (y = -14, то есть 12 м от камеры) видно всего
    # x от -4.1 до 6.1 и z от 2.5 до 9.4. Прошлая рамка стояла в x = ±13.5 —
    # целиком за кадром, отчего кадр и остался без тёмных углов.
    # Ширина рамки — это компромисс, а не вкус: близкий блок отбрасывает
    # широкую «тень заслонения» на дальний план. Кромка в x = -2.8 срезала
    # всё левее x = -8.3 у дерева, и дерево пропадало из кадра целиком.
    # Кромки -3.8 и 5.5 оставляют дерево и арку на виду.
    frame = material("frameRock", (0.05, 0.05, 0.06), 0.95)
    for (fx, fz, hx, hz) in ((-6.9, 6.0, 3.1, 4.5), (8.25, 6.2, 2.75, 4.6)):
        blk = add_cube((fx, -14.0, fz), (hx, 1.5, hz))
        shade(blk, frame, False)
    # Валуны по внутренней кромке: ровная вертикаль читалась серой шторой.
    for (ex, sgn) in ((-3.8, -1), (5.5, 1)):
        for i in range(7):
            r = random.uniform(0.5, 1.3)
            blk = add_sphere((ex - sgn * random.uniform(-0.3, 0.9), -14.2,
                              random.uniform(1.8, 10.2)), (r, r * 0.7, r * 0.8), 9, 6)
            shade(blk, frame)
    # Сталактиты свисают из-за верхней кромки кадра (z = 9.4).
    for i in range(9):
        st = add_cone((random.uniform(-4.0, 6.0), -13.5, 10.6),
                      random.uniform(0.35, 0.9), 0.02, random.uniform(2.0, 4.0), 7,
                      (math.radians(180), 0, 0))
        shade(st, frame)

    return m


def render():
    # Камера почти сбоку — как в референсе, платформер смотрит вдоль сцены.
    bpy.ops.object.camera_add(location=(1.0, -26.0, 7.2))
    cam = bpy.context.active_object
    cam.rotation_euler = (math.radians(84), 0, 0)
    cam.data.lens = 42
    bpy.context.scene.camera = cam

    # Солнце сзади-сбоку: подсвечивает зелень на просвет.
    bpy.ops.object.light_add(type="SUN", location=(-8, 14, 22))
    sun = bpy.context.active_object
    sun.data.energy = 6.5
    sun.data.color = (1.0, 0.96, 0.88)
    sun.rotation_euler = (math.radians(48), 0, math.radians(-34))

    world = bpy.data.worlds.new("grotto")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        # Небо яркое, но СВЕТИТ слабо. Сила 1.6 заливала сцену синим
        # заполняющим светом: пропадал контраст, мох становился мятным,
        # камень — белёсым, а огонь факелов не читался вовсе. Ровно эта же
        # ошибка сидит в игре — SetupSky берёт ambient как top * 0.6.
        bg.inputs[1].default_value = 0.32

        # Ровная заливка одним синим давала плоский задник. В референсе даль
        # светлая и мутная у горизонта, синяя вверху — это градиент, и он же
        # стоит в игре в SetupSky (top / horizon / ground).
        nt = world.node_tree
        grad = nt.nodes.new("ShaderNodeValToRGB")
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        mapr = nt.nodes.new("ShaderNodeMapRange")
        mapr.inputs["From Min"].default_value = -0.15
        mapr.inputs["From Max"].default_value = 0.55
        grad.color_ramp.elements[0].color = (0.78, 0.87, 0.90, 1.0)
        grad.color_ramp.elements[1].color = (0.24, 0.52, 0.86, 1.0)
        nt.links.new(geo.outputs["Incoming"], sep.inputs[0])
        nt.links.new(sep.outputs["Z"], mapr.inputs["Value"])
        nt.links.new(mapr.outputs[0], grad.inputs["Fac"])
        nt.links.new(grad.outputs["Color"], bg.inputs[0])

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 56
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1200
    sc.render.resolution_y = 800
    # Контрастная подача: тени плотные, свет факелов уходит в пересвет —
    # тот самый разброс яркостей, на котором держится референс.
    looks = [v.identifier for v in
             sc.view_settings.bl_rna.properties["look"].enum_items]
    for want in ("AgX - Punchy", "Punchy", "AgX - Medium Contrast"):
        if want in looks:
            sc.view_settings.look = want
            break

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "grotto.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("превью:", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    build()
    render()
