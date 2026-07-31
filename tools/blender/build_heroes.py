#!/usr/bin/env python3
"""Пять кубических героев-силачей: Бэтмен, Супермен, Халк, Тор, Росомаха.

Геометрия здесь целиком своя — коробки, набранные вручную в тех же
пропорциях Стива, что и «Железный человечек»: голова 8x8x8, корпус
8x4x12, руки и ноги 4x4x12, всего 32 «пикселя» от земли. Ничего не
скачано и не срисовано: узнаваемость держится на силуэте и трёх-четырёх
цветах, потому что на телефоне мелкая деталь всё равно не читается.

Скелет, привязка и семь клипов берутся готовыми из build_hero — они у
всех героев одни и те же.

Запуск:
    python3 tools/blender/build_heroes.py

Готовые FBX кладутся в unity/Assets/Resources/Models/monsters/.
"""

import os
import sys

try:
    import bpy  # noqa: F401
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import material  # noqa: E402
from build_hero import (PX, ROOT, assemble, export, glow_material,  # noqa: E402
                        part, reset)


def mat(name, rgb, rough=0.55, metal=0.0):
    return material(name, rgb, rough, metal)


# ---------- общие куски тела ----------

def limbs(parts, skin, thick=2.0):
    """Руки и ноги одним цветом. thick — половина ширины в пикселях."""
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        part(bone, (sx * thick * PX, 0, 6 * PX),
             (thick * PX, thick * PX, 6 * PX), skin, parts)
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        part(bone, (sx * (4 + thick) * PX, 0, 18 * PX),
             (thick * PX, thick * PX, 6 * PX), skin, parts)


def torso(parts, m, half_w=4.0):
    part("Spine", (0, 0, 18 * PX), (half_w * PX, 2 * PX, 6 * PX), m, parts)


def head(parts, m, half=4.0):
    part("Head", (0, 0, 28 * PX), (half * PX, half * PX, half * PX), m, parts)


def eyes(parts, m, y=-4.1, z=29.2, dx=1.7, w=1.0, h=0.55):
    for sx in (-1, 1):
        part("Head", (sx * dx * PX, y * PX, z * PX),
             (w * PX, 0.25 * PX, h * PX), m, parts)


def cape(parts, m, drop=13.0, half_w=5.6):
    """Плащ — плита за спиной. Перёд у всех героев по -Y.

    Ширину и длину пришлось задрать: плащ ровно по корпусу (4.3 на 9)
    не было видно ни спереди, ни с трёх четвертей — он целиком прятался
    за спиной. Плащ обязан выступать за силуэт, иначе его нет.
    """
    # drop — ПОЛНАЯ длина плаща от плеч вниз. Раньше она же шла в
    # половину высоты коробки, и плащ длиной 14 уходил на четыре пикселя
    # под землю, а сверху торчал выше плеч.
    part("Spine", (0, 2.75 * PX, (24 - drop * 0.5) * PX),
         (half_w * PX, 0.4 * PX, drop * 0.5 * PX), m, parts)
    # Оплечье: плащ должен на чём-то держаться.
    part("Spine", (0, 2.3 * PX, 23.2 * PX),
         (half_w * 0.8 * PX, 0.9 * PX, 1.0 * PX), m, parts)


# ---------- Бэтмен ----------

def batman():
    m = {
        "suit": mat("bmSuit", (0.115, 0.125, 0.155), 0.6),
        "cowl": mat("bmCowl", (0.05, 0.055, 0.075), 0.55),
        "belt": mat("bmBelt", (0.85, 0.68, 0.12), 0.4, 0.3),
        "cape": mat("bmCape", (0.08, 0.085, 0.11), 0.7),
        "eye": glow_material("bmEye", (0.85, 0.93, 1.0), 1.6),
    }
    parts = []
    limbs(parts, m["suit"])
    torso(parts, m["suit"])
    cape(parts, m["cape"], 15.0, 6.4)

    # Пояс и эмблема нетопыря: три коробки — тело и два крыла.
    part("Spine", (0, 0, 12.8 * PX), (4.1 * PX, 2.1 * PX, 0.9 * PX), m["belt"], parts)
    part("Spine", (0, -2.1 * PX, 20.4 * PX), (0.6 * PX, 0.3 * PX, 1.1 * PX), m["belt"], parts)
    for sx in (-1, 1):
        part("Spine", (sx * 1.5 * PX, -2.1 * PX, 20.7 * PX),
             (1.1 * PX, 0.3 * PX, 0.45 * PX), m["belt"], parts)
        part("Spine", (sx * 2.5 * PX, -2.1 * PX, 20.1 * PX),
             (0.6 * PX, 0.3 * PX, 0.35 * PX), m["belt"], parts)

    # Перчатки и сапоги
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        part(bone, (sx * 6 * PX, 0, 13 * PX),
             (2.15 * PX, 2.15 * PX, 1.8 * PX), m["cowl"], parts)
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        part(bone, (sx * 2 * PX, 0, 1.6 * PX),
             (2.15 * PX, 2.2 * PX, 1.6 * PX), m["cowl"], parts)

    head(parts, m["cowl"])
    # Открытый подбородок — иначе шлем читается сплошным кубом.
    part("Head", (0, -4.05 * PX, 25.2 * PX), (2.2 * PX, 0.3 * PX, 1.2 * PX),
         mat("bmChin", (0.72, 0.56, 0.44), 0.7), parts)
    eyes(parts, m["eye"], -4.3, 29.2, 1.8, 1.1, 0.45)
    # Уши — то, по чему силуэт узнаётся с любого расстояния.
    for sx in (-1, 1):
        part("Head", (sx * 2.6 * PX, 0.6 * PX, 33.4 * PX),
             (0.55 * PX, 0.55 * PX, 2.0 * PX), m["cowl"], parts)
    return parts


# ---------- Супермен ----------

def superman():
    m = {
        "suit": mat("smSuit", (0.09, 0.22, 0.62), 0.5),
        "red": mat("smRed", (0.66, 0.07, 0.09), 0.5),
        "gold": mat("smGold", (0.90, 0.74, 0.16), 0.35, 0.2),
        "skin": mat("smSkin", (0.86, 0.66, 0.52), 0.7),
        "hair": mat("smHair", (0.09, 0.08, 0.10), 0.6),
        "eye": mat("smEye", (0.12, 0.16, 0.26), 0.4),
    }
    parts = []
    limbs(parts, m["suit"])
    torso(parts, m["suit"])
    cape(parts, m["red"], 16.0, 6.6)

    # Щит на груди: жёлтая пластина и красная засечка внутри.
    part("Spine", (0, -2.1 * PX, 20.4 * PX), (2.1 * PX, 0.3 * PX, 2.1 * PX),
         m["gold"], parts)
    part("Spine", (0, -2.3 * PX, 20.4 * PX), (1.0 * PX, 0.2 * PX, 1.3 * PX),
         m["red"], parts)
    # Пояс и красные сапоги
    part("Spine", (0, 0, 12.8 * PX), (4.1 * PX, 2.1 * PX, 1.0 * PX), m["red"], parts)
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        part(bone, (sx * 2 * PX, 0, 3.0 * PX),
             (2.15 * PX, 2.2 * PX, 3.0 * PX), m["red"], parts)

    head(parts, m["skin"])
    part("Head", (0, 0.2 * PX, 31.4 * PX), (4.1 * PX, 4.1 * PX, 1.4 * PX),
         m["hair"], parts)
    # Знаменитый завиток на лбу
    part("Head", (0.8 * PX, -4.05 * PX, 30.4 * PX),
         (1.0 * PX, 0.3 * PX, 0.8 * PX), m["hair"], parts)
    eyes(parts, m["eye"], -4.1, 29.0, 1.7, 0.9, 0.45)
    return parts


# ---------- Халк ----------

def hulk():
    m = {
        "skin": mat("hkSkin", (0.29, 0.62, 0.22), 0.75),
        "dark": mat("hkDark", (0.18, 0.42, 0.14), 0.75),
        "pants": mat("hkPants", (0.34, 0.20, 0.52), 0.7),
        "eye": mat("hkEye", (0.95, 0.97, 0.9), 0.4),
    }
    parts = []
    # Толще обычного: рост у всех героев игра всё равно приводит к одному,
    # поэтому громадность передаётся шириной, а не высотой.
    limbs(parts, m["skin"], 2.9)
    torso(parts, m["skin"], 5.2)

    # Рваные штаны на бёдрах
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        part(bone, (sx * 2.9 * PX, 0, 9.4 * PX),
             (3.05 * PX, 3.05 * PX, 2.8 * PX), m["pants"], parts)
    part("Spine", (0, 0, 12.6 * PX), (5.3 * PX, 3.0 * PX, 1.2 * PX), m["pants"], parts)

    # Грудные и плечи — тень другого оттенка, иначе торс плоский.
    part("Spine", (0, -2.05 * PX, 21.0 * PX), (4.2 * PX, 0.3 * PX, 1.6 * PX),
         m["dark"], parts)
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        part(bone, (sx * 6.9 * PX, 0, 23.0 * PX),
             (3.2 * PX, 3.2 * PX, 1.6 * PX), m["dark"], parts)

    head(parts, m["skin"], 4.2)
    # Тяжёлый лоб и растрёпанные волосы
    part("Head", (0, -4.15 * PX, 30.2 * PX), (4.3 * PX, 0.4 * PX, 1.2 * PX),
         m["dark"], parts)
    part("Head", (0, 0.3 * PX, 32.6 * PX), (4.3 * PX, 4.3 * PX, 0.9 * PX),
         m["dark"], parts)
    eyes(parts, m["eye"], -4.3, 28.9, 1.9, 1.0, 0.5)
    # Оскал
    part("Head", (0, -4.3 * PX, 26.4 * PX), (2.2 * PX, 0.25 * PX, 0.5 * PX),
         m["eye"], parts)
    return parts


# ---------- Тор ----------

def thor():
    m = {
        "armor": mat("thArmor", (0.26, 0.28, 0.34), 0.35, 0.7),
        "silver": mat("thSilver", (0.72, 0.75, 0.80), 0.25, 0.9),
        "red": mat("thRed", (0.62, 0.09, 0.11), 0.55),
        "skin": mat("thSkin", (0.86, 0.68, 0.54), 0.7),
        "hair": mat("thHair", (0.80, 0.64, 0.26), 0.6),
        "wood": mat("thWood", (0.32, 0.20, 0.11), 0.7),
        "eye": mat("thEye", (0.18, 0.30, 0.42), 0.4),
    }
    parts = []
    limbs(parts, m["armor"])
    torso(parts, m["armor"])
    cape(parts, m["red"], 14.0, 6.2)

    # Круги на нагруднике — приметная деталь доспеха.
    for sx in (-1, 1):
        part("Spine", (sx * 2.0 * PX, -2.1 * PX, 20.4 * PX),
             (1.1 * PX, 0.3 * PX, 1.1 * PX), m["silver"], parts)
    part("Spine", (0, 0, 12.8 * PX), (4.1 * PX, 2.1 * PX, 1.0 * PX), m["silver"], parts)
    # Наплечники
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        part(bone, (sx * 6 * PX, 0, 23.4 * PX),
             (2.4 * PX, 2.4 * PX, 1.3 * PX), m["silver"], parts)

    head(parts, m["skin"])
    # Шлем с крыльями
    part("Head", (0, 0, 31.6 * PX), (4.15 * PX, 4.15 * PX, 1.6 * PX), m["silver"], parts)
    for sx in (-1, 1):
        part("Head", (sx * 5.0 * PX, 0.6 * PX, 33.0 * PX),
             (1.0 * PX, 2.4 * PX, 3.2 * PX), m["silver"], parts)
    # Борода — крупная и золотая: мелкая сливалась с лицом.
    part("Head", (0, -3.6 * PX, 25.2 * PX), (3.2 * PX, 1.2 * PX, 1.8 * PX),
         m["hair"], parts)
    eyes(parts, m["eye"], -4.1, 29.0, 1.7, 0.9, 0.45)

    # Молот в правой руке: рукоять и боёк.
    # Тонкая рукоять пряталась за бойком, и молот выглядел отдельно
    # летящей коробкой — поэтому она заметно толще, а боёк шире, чем
    # глубже: так силуэт читается и сбоку, и в три четверти.
    part("Arm.R", (-6 * PX, -2.4 * PX, 11.8 * PX),
         (0.85 * PX, 2.4 * PX, 0.85 * PX), m["wood"], parts)
    part("Arm.R", (-6 * PX, -5.2 * PX, 11.8 * PX),
         (2.4 * PX, 1.5 * PX, 2.0 * PX), m["silver"], parts)
    part("Arm.R", (-6 * PX, -5.2 * PX, 11.8 * PX),
         (2.5 * PX, 0.6 * PX, 1.2 * PX), m["armor"], parts)
    return parts


# ---------- Росомаха ----------

def wolverine():
    m = {
        "yellow": mat("wvYellow", (0.86, 0.72, 0.10), 0.5),
        "blue": mat("wvBlue", (0.11, 0.17, 0.48), 0.5),
        "black": mat("wvBlack", (0.07, 0.07, 0.09), 0.6),
        "skin": mat("wvSkin", (0.84, 0.64, 0.50), 0.7),
        "claw": mat("wvClaw", (0.84, 0.87, 0.92), 0.15, 0.95),
        "eye": mat("wvEye", (0.95, 0.96, 0.98), 0.4),
    }
    parts = []
    limbs(parts, m["blue"])
    torso(parts, m["yellow"])

    # Жёлтые вставки на плечах и синий пояс
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        part(bone, (sx * 6 * PX, 0, 21.6 * PX),
             (2.15 * PX, 2.15 * PX, 2.6 * PX), m["yellow"], parts)
    part("Spine", (0, 0, 12.8 * PX), (4.1 * PX, 2.1 * PX, 1.1 * PX), m["blue"], parts)
    for sx in (-1, 1):
        part("Spine", (sx * 3.3 * PX, -2.1 * PX, 18.0 * PX),
             (0.55 * PX, 0.3 * PX, 5.5 * PX), m["blue"], parts)

    head(parts, m["yellow"])
    part("Head", (0, -4.05 * PX, 25.6 * PX), (2.4 * PX, 0.3 * PX, 1.6 * PX),
         m["skin"], parts)
    part("Head", (0, -4.2 * PX, 29.4 * PX), (4.15 * PX, 0.3 * PX, 1.5 * PX),
         m["black"], parts)
    eyes(parts, m["eye"], -4.45, 29.4, 1.8, 1.0, 0.5)
    # Острые углы маски — второй узнаваемый силуэт после когтей.
    # Углы маски: широкие и разведённые в стороны. Высокие и узкие
    # читались заячьими ушами, а не шлемом.
    for sx in (-1, 1):
        part("Head", (sx * 4.4 * PX, 0.6 * PX, 31.8 * PX),
             (1.9 * PX, 1.5 * PX, 1.1 * PX), m["yellow"], parts)

    # Когти: по три лезвия вперёд из каждого кулака.
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        x = sx * 6 * PX
        part(bone, (x, 0, 13 * PX), (2.15 * PX, 2.15 * PX, 1.8 * PX), m["blue"], parts)
        for i in (-1, 0, 1):
            part(bone, (x + i * 1.25 * PX, -4.2 * PX, 12.6 * PX),
                 (0.26 * PX, 2.4 * PX, 0.26 * PX), m["claw"], parts)
    return parts


HEROES = [
    ("Batman", batman),
    ("Superman", superman),
    ("Hulk", hulk),
    ("Thor", thor),
    ("Wolverine", wolverine),
]


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    for name, fn in HEROES:
        if only and only.lower() != name.lower():
            continue
        # Сцена сбрасывается на каждого: иначе материалы и меши прошлого
        # героя уезжают в следующий FBX.
        reset()
        assemble(name, fn())
        export(name)
    print("папка:", os.path.relpath(
        os.path.join(ROOT, "unity", "Assets", "Resources", "Models", "monsters"), ROOT))


if __name__ == "__main__":
    main()
