#!/usr/bin/env python3
"""Собственный герой игры: «Железный человечек».

Кубический человечек в пропорциях Стива из Minecraft, но в броне Железного
человека. Отличается от остальных ассетов тем, что это ИГРОВОЙ персонаж:
ему нужен скелет и набор клипов, которые игра ищет по именам —
Idle, Walk, Run, Jump, Bite_InPlace, HitRecieve, Death.

Запуск:
    python3 tools/blender/build_hero.py

Готовый FBX кладётся в unity/Assets/Resources/Models/monsters/.
ModelImportSettings даст ему Legacy-риг и обрежет префикс «Armature|»
у имён клипов, поэтому в коде они ищутся просто по «Idle».
"""

import math
import os
import sys

try:
    import bpy
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_assets import material, shade, add_cube  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Models", "monsters")

FPS = 24
PX = 0.0625        # «пиксель» Minecraft: 1/16 блока

NAME = "IronSteve"


# ---------- палитра ----------

def palette():
    return {
        "red": material("armorRed", (0.52, 0.045, 0.05), 0.32, 0.55),
        "redDark": material("armorRedDark", (0.30, 0.025, 0.03), 0.38, 0.5),
        "gold": material("armorGold", (0.86, 0.62, 0.11), 0.22, 0.9),
        "goldDark": material("armorGoldDark", (0.62, 0.42, 0.07), 0.3, 0.85),
        "steel": material("armorSteel", (0.34, 0.35, 0.39), 0.35, 0.8),
        # Реактор и глаза: очень светлый холодный цвет плюс собственное
        # свечение материала — в игре к нему добавляется ореол из кода.
        "arc": glow_material("arcReactor", (0.55, 0.95, 1.0), 3.0),
        "eye": glow_material("visorEye", (0.62, 0.94, 1.0), 2.2),
    }


def glow_material(name, rgb, strength):
    mat = material(name, rgb, 0.1, 0.0)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        elif "Emission" in bsdf.inputs:
            bsdf.inputs["Emission"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = strength
    return mat


# ---------- сборка тела ----------

def part(bone, loc, half, mat, parts, smooth=False):
    """Кубическая деталь, целиком привязанная к одной кости."""
    obj = add_cube(loc, half)
    shade(obj, mat, smooth)
    parts.append((obj, bone))
    return obj


def build_body(m):
    """Тело в пропорциях Стива: голова 8x8x8, корпус 8x4x12, руки и ноги 4x4x12.

    Стороны меряются в «пикселях» Minecraft, поэтому силуэт узнаваем, а
    броня — уже своя: наплечники, золотая лицевая панель, реактор, дюзы.
    """
    parts = []

    # --- ноги (12 px), от земли до 0.75 ---
    for sx, bone in ((1, "Leg.L"), (-1, "Leg.R")):
        x = sx * 2 * PX
        part(bone, (x, 0, 6 * PX), (2 * PX, 2 * PX, 6 * PX), m["red"], parts)
        # Золотой ботинок и накладка на голени
        part(bone, (x, 0, 1.2 * PX), (2.15 * PX, 2.3 * PX, 1.2 * PX), m["gold"], parts)
        part(bone, (x, -2.1 * PX, 5 * PX), (1.4 * PX, 0.3 * PX, 3 * PX), m["goldDark"], parts)
        # Дюза в подошве — человечек всё-таки летает
        part(bone, (x, 0, 0.12 * PX), (1.1 * PX, 1.1 * PX, 0.2 * PX), m["arc"], parts)

    # --- корпус (12 px), 12 .. 24 по вертикали ---
    # Пропорции Стива считаются от земли: ноги 0..12, корпус 12..24,
    # голова 24..32. Центр корпуса — 18, а не 12, иначе он налезает на
    # ноги, а голова с руками повисают над дырой.
    part("Spine", (0, 0, 18 * PX), (4 * PX, 2 * PX, 6 * PX), m["red"], parts)
    # Золотая грудная пластина и пояс
    part("Spine", (0, -2.05 * PX, 20.5 * PX), (3.2 * PX, 0.3 * PX, 2.6 * PX), m["gold"], parts)
    part("Spine", (0, 0, 12.6 * PX), (4.1 * PX, 2.1 * PX, 0.8 * PX), m["goldDark"], parts)
    # Боковые панели брони
    for sx in (-1, 1):
        part("Spine", (sx * 4.05 * PX, 0, 18 * PX),
             (0.3 * PX, 2.05 * PX, 4.5 * PX), m["steel"], parts)

    # Дуговой реактор: оправа и светящееся ядро
    part("Spine", (0, -2.25 * PX, 21 * PX), (1.9 * PX, 0.25 * PX, 1.9 * PX),
         m["steel"], parts)
    part("Spine", (0, -2.45 * PX, 21 * PX), (1.25 * PX, 0.2 * PX, 1.25 * PX),
         m["arc"], parts)

    # --- руки (12 px), 12 .. 24, по бокам корпуса ---
    for sx, bone in ((1, "Arm.L"), (-1, "Arm.R")):
        x = sx * 6 * PX
        part(bone, (x, 0, 18 * PX), (2 * PX, 2 * PX, 6 * PX), m["red"], parts)
        # Наплечник
        part(bone, (x, 0, 23.4 * PX), (2.4 * PX, 2.4 * PX, 1.4 * PX), m["gold"], parts)
        # Перчатка и репульсор на ладони
        part(bone, (x, 0, 13 * PX), (2.15 * PX, 2.15 * PX, 1.7 * PX), m["gold"], parts)
        part(bone, (x, -0.1 * PX, 11.3 * PX), (1.2 * PX, 1.2 * PX, 0.25 * PX),
             m["arc"], parts)
        # Полоса на предплечье
        part(bone, (x + sx * 2.05 * PX, 0, 16.5 * PX),
             (0.25 * PX, 2.05 * PX, 3 * PX), m["steel"], parts)

    # --- голова (8 px), 1.5 .. 2.0 ---
    part("Head", (0, 0, 28 * PX), (4 * PX, 4 * PX, 4 * PX), m["red"], parts)
    # Золотая лицевая панель
    part("Head", (0, -4.1 * PX, 27.6 * PX), (3.3 * PX, 0.35 * PX, 3.2 * PX),
         m["gold"], parts)
    # Прорези глаз
    for sx in (-1, 1):
        part("Head", (sx * 1.7 * PX, -4.4 * PX, 29 * PX),
             (1.1 * PX, 0.25 * PX, 0.5 * PX), m["eye"], parts)
    # Рот-решётка и «уши»-блоки шлема
    part("Head", (0, -4.35 * PX, 25.6 * PX), (1.6 * PX, 0.2 * PX, 0.35 * PX),
         m["steel"], parts)
    for sx in (-1, 1):
        part("Head", (sx * 4.1 * PX, 0.4 * PX, 28 * PX),
             (0.35 * PX, 2.2 * PX, 2.4 * PX), m["goldDark"], parts)
    # Гребень шлема
    part("Head", (0, 0.5 * PX, 32.3 * PX), (1.1 * PX, 3.4 * PX, 0.5 * PX),
         m["gold"], parts)

    return parts


def merge(parts, name="IronSteveBody"):
    """Склеить детали в один меш, дав каждой свою весовую группу."""
    for obj, bone in parts:
        vg = obj.vertex_groups.new(name=bone)
        vg.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")

    objs = [o for o, _ in parts]
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    body = bpy.context.active_object
    body.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return body


# ---------- скелет ----------

BONES = [
    # имя, голова, хвост, родитель
    ("Hips", (0, 0, 12 * PX), (0, 0, 14 * PX), None),
    ("Spine", (0, 0, 12 * PX), (0, 0, 24 * PX), "Hips"),
    ("Head", (0, 0, 24 * PX), (0, 0, 32 * PX), "Spine"),
    ("Arm.L", (6 * PX, 0, 23 * PX), (6 * PX, 0, 11 * PX), "Spine"),
    ("Arm.R", (-6 * PX, 0, 23 * PX), (-6 * PX, 0, 11 * PX), "Spine"),
    ("Leg.L", (2 * PX, 0, 12 * PX), (2 * PX, 0, 0), "Hips"),
    ("Leg.R", (-2 * PX, 0, 12 * PX), (-2 * PX, 0, 0), "Hips"),
]


def build_armature():
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm = bpy.context.active_object
    arm.name = "Armature"

    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    for b in list(eb):
        eb.remove(b)

    made = {}
    for name, head, tail, parent in BONES:
        b = eb.new(name)
        b.head = head
        b.tail = tail
        # Руки и ноги смотрят вниз: без выравнивания roll Blender крутит их
        # вокруг своей оси, и сгибание вперёд превращается в вращение вбок.
        b.roll = 0.0
        if parent:
            b.parent = made[parent]
        made[name] = b

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def bind(body, arm):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    # ARMATURE_NAME берёт готовые весовые группы, а не считает автовеса:
    # деталь целиком принадлежит своей кости, кубы не должны «плыть».
    bpy.ops.object.parent_set(type="ARMATURE_NAME")


# ---------- анимации ----------

def key(arm, bone, frame, rot=(0, 0, 0), loc=None):
    pb = arm.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    pb.rotation_euler = (math.radians(rot[0]), math.radians(rot[1]), math.radians(rot[2]))
    pb.keyframe_insert("rotation_euler", frame=frame)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert("location", frame=frame)


def new_action(arm, name):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data.action = act
    return act


def rest(arm, frame):
    for name, _, _, _ in BONES:
        key(arm, name, frame)


def build_actions(arm):
    if arm.animation_data is None:
        arm.animation_data_create()

    # --- Idle: дыхание и лёгкое покачивание рук ---
    new_action(arm, "Idle")
    for f, a in ((1, 0), (24, 4), (48, 0)):
        key(arm, "Spine", f, (a * 0.5, 0, 0))
        key(arm, "Head", f, (-a * 0.4, 0, 0))
        key(arm, "Arm.L", f, (a, 0, -a * 0.5))
        key(arm, "Arm.R", f, (a, 0, a * 0.5))
        key(arm, "Hips", f, (0, 0, 0), (0, 0, a * 0.004))
        key(arm, "Leg.L", f)
        key(arm, "Leg.R", f)

    # --- Walk: руки и ноги маятником в противофазе ---
    new_action(arm, "Walk")
    for f, s in ((1, 1), (12, -1), (24, 1)):
        key(arm, "Arm.L", f, (-28 * s, 0, 0))
        key(arm, "Arm.R", f, (28 * s, 0, 0))
        key(arm, "Leg.L", f, (26 * s, 0, 0))
        key(arm, "Leg.R", f, (-26 * s, 0, 0))
        key(arm, "Spine", f, (2, 3 * s, 0))
        key(arm, "Head", f, (-2, -2 * s, 0))
        key(arm, "Hips", f, (0, 0, 0), (0, 0, 0.012 if f == 12 else 0))

    # --- Run: тот же маятник, но шире и с наклоном вперёд ---
    new_action(arm, "Run")
    for f, s in ((1, 1), (8, -1), (16, 1)):
        key(arm, "Arm.L", f, (-52 * s, 0, 0))
        key(arm, "Arm.R", f, (52 * s, 0, 0))
        key(arm, "Leg.L", f, (46 * s, 0, 0))
        key(arm, "Leg.R", f, (-46 * s, 0, 0))
        key(arm, "Spine", f, (9, 5 * s, 0))
        key(arm, "Head", f, (-7, -3 * s, 0))
        key(arm, "Hips", f, (0, 0, 0), (0, 0, 0.02 if f == 8 else 0))

    # --- Jump: подобрать ноги, руки вверх, потом раскрыться ---
    new_action(arm, "Jump")
    rest(arm, 1)
    for f, arm_a, leg_a, spine in ((6, -120, 55, -12), (13, -140, 20, -6), (20, -110, 8, 0)):
        key(arm, "Arm.L", f, (arm_a, 0, -12))
        key(arm, "Arm.R", f, (arm_a, 0, 12))
        key(arm, "Leg.L", f, (leg_a, 0, 0))
        key(arm, "Leg.R", f, (leg_a * 0.6, 0, 0))
        key(arm, "Spine", f, (spine, 0, 0))
        key(arm, "Head", f, (-spine * 0.5, 0, 0))
        key(arm, "Hips", f)

    # --- Bite_InPlace: удар репульсором с правой ---
    new_action(arm, "Bite_InPlace")
    rest(arm, 1)
    key(arm, "Arm.R", 4, (-30, 0, 25))
    key(arm, "Spine", 4, (0, 14, 0))
    key(arm, "Arm.R", 9, (-104, 0, -8))
    key(arm, "Spine", 9, (4, -20, 0))
    key(arm, "Head", 9, (4, -8, 0))
    key(arm, "Arm.L", 9, (18, 0, 6))
    key(arm, "Arm.R", 18, (0, 0, 0))
    key(arm, "Spine", 18, (0, 0, 0))
    key(arm, "Head", 18, (0, 0, 0))
    key(arm, "Arm.L", 18, (0, 0, 0))

    # --- HitRecieve: отброс назад ---
    new_action(arm, "HitRecieve")
    rest(arm, 1)
    key(arm, "Spine", 5, (-22, 0, 0))
    key(arm, "Head", 5, (-16, 0, 0))
    key(arm, "Arm.L", 5, (34, 0, -26))
    key(arm, "Arm.R", 5, (34, 0, 26))
    key(arm, "Leg.L", 5, (-12, 0, 0))
    key(arm, "Leg.R", 5, (-8, 0, 0))
    rest(arm, 16)

    # --- Death: падение навзничь ---
    new_action(arm, "Death")
    rest(arm, 1)
    key(arm, "Spine", 8, (-26, 0, 0))
    key(arm, "Arm.L", 8, (52, 0, -30))
    key(arm, "Arm.R", 8, (52, 0, 30))
    key(arm, "Head", 8, (-18, 0, 0))
    key(arm, "Hips", 20, (-84, 0, 0), (0, 0, -0.28))
    key(arm, "Spine", 20, (-10, 0, 0))
    key(arm, "Head", 20, (12, 0, 0))
    key(arm, "Arm.L", 20, (16, 0, -46))
    key(arm, "Arm.R", 20, (16, 0, 46))
    key(arm, "Leg.L", 20, (10, 0, 0))
    key(arm, "Leg.R", 20, (14, 0, 0))
    key(arm, "Hips", 30, (-88, 0, 0), (0, 0, -0.3))

    arm.animation_data.action = bpy.data.actions["Idle"]


# ---------- экспорт ----------

def export(name=None):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, (name or NAME) + ".fbx")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        # Для оснащённых моделей запекание системы координат ломает позы,
        # поэтому здесь оно выключено — в отличие от статичного реквизита.
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("готово:", os.path.relpath(path, ROOT))


def assemble(name, parts):
    """Общая сборка игрового героя: склеить, оснастить, оживить.

    Вынесено отдельно, потому что скелет, привязка и все семь клипов у
    всех героев одни и те же — различается только геометрия. Так новый
    персонаж это одна функция с коробками, а не копия всего файла.
    """
    body = merge(parts, name + "Body")
    arm = build_armature()
    bind(body, arm)
    build_actions(arm)
    return arm


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS


def build():
    reset()
    return assemble(NAME, build_body(palette()))


def main():
    build()
    export()


if __name__ == "__main__":
    main()
