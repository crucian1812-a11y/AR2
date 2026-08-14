#!/usr/bin/env python3
"""Скелет и болванка бойца для авторинга анимаций.

Зачем нужна своя болванка, когда модели берутся у Quaternius: анимации
делаются здесь, в контейнере без дисплея, а модели лежат у пользователя.
Связывает их **Humanoid** — Unity отображает кости любого гуманоидного
скелета на свой эталонный и переносит клип с одной модели на другую.
Поэтому клип, снятый на этой болванке, проиграется на бойце из пака.

Плата за это — расхождение пропорций: точка, где рука касалась тела
соперника, при переносе уезжает на несколько сантиметров. Лечится IK
поверх клипа (см. §4 в docs/bjj/PLAN.md), а не более точной болванкой.

Имена костей выбраны так, чтобы Unity распознал их сам, без ручной
разметки аватара: LeftUpperArm, LeftLowerArm, LeftHand и так далее.
Поза покоя — T-поза, как того требует Humanoid.
"""

import math
import sys

try:
    import bpy
    import mathutils
except ImportError as exc:
    # Показываем настоящую причину. Пакет может стоять и всё равно не
    # грузиться: bpy — нативный модуль, и ему нужны системные библиотеки.
    # Совет «поставьте пакет» в таком случае только сбивает с толку.
    sys.exit("Не удалось загрузить bpy (%s).\n"
             "Установка: python3 -m pip install --break-system-packages bpy" % exc)


# Пропорции взрослого мужчины ростом 1.75 м, в метрах от пола.
# Это не «примерно»: Humanoid нормирует клип по росту, но соотношение
# длин звеньев влияет на то, куда попадёт рука после переноса.
H = {
    "hips": 0.95,
    "spine": 1.10,
    "chest": 1.25,
    "neck": 1.45,
    "head": 1.58,
    "head_top": 1.75,
    "shoulder": 1.42,
    "elbow": 1.15,
    "wrist": 0.88,
    "knee": 0.50,
    "ankle": 0.09,
    "toe": 0.02,
}

SHOULDER_X = 0.18
HAND_X = 0.72        # T-поза: руки в стороны
HIP_X = 0.10


# Кость: (имя, родитель, голова, хвост). Хвост важен не меньше головы —
# он задаёт ось кости, а значит и то, вокруг чего она вращается.
BONES = [
    ("Hips",          None,        (0, 0, H["hips"]),            (0, 0, H["spine"])),
    ("Spine",         "Hips",      (0, 0, H["spine"]),           (0, 0, H["chest"])),
    ("Chest",         "Spine",     (0, 0, H["chest"]),           (0, 0, H["neck"])),
    ("Neck",          "Chest",     (0, 0, H["neck"]),            (0, 0, H["head"])),
    ("Head",          "Neck",      (0, 0, H["head"]),            (0, 0, H["head_top"])),

    ("LeftShoulder",  "Chest",     (0.04, 0, H["shoulder"]),     (SHOULDER_X, 0, H["shoulder"])),
    ("LeftUpperArm",  "LeftShoulder", (SHOULDER_X, 0, H["shoulder"]), (0.45, 0, H["elbow"] + 0.27)),
    ("LeftLowerArm",  "LeftUpperArm", (0.45, 0, H["elbow"] + 0.27), (0.62, 0, H["elbow"] + 0.27)),
    ("LeftHand",      "LeftLowerArm", (0.62, 0, H["elbow"] + 0.27), (HAND_X, 0, H["elbow"] + 0.27)),

    ("RightShoulder", "Chest",     (-0.04, 0, H["shoulder"]),    (-SHOULDER_X, 0, H["shoulder"])),
    ("RightUpperArm", "RightShoulder", (-SHOULDER_X, 0, H["shoulder"]), (-0.45, 0, H["elbow"] + 0.27)),
    ("RightLowerArm", "RightUpperArm", (-0.45, 0, H["elbow"] + 0.27), (-0.62, 0, H["elbow"] + 0.27)),
    ("RightHand",     "RightLowerArm", (-0.62, 0, H["elbow"] + 0.27), (-HAND_X, 0, H["elbow"] + 0.27)),

    ("LeftUpperLeg",  "Hips",      (HIP_X, 0, H["hips"]),        (HIP_X, 0, H["knee"])),
    ("LeftLowerLeg",  "LeftUpperLeg", (HIP_X, 0, H["knee"]),     (HIP_X, 0, H["ankle"])),
    ("LeftFoot",      "LeftLowerLeg", (HIP_X, 0, H["ankle"]),    (HIP_X, -0.16, H["toe"])),
    ("LeftToes",      "LeftFoot",  (HIP_X, -0.16, H["toe"]),     (HIP_X, -0.24, H["toe"])),

    ("RightUpperLeg", "Hips",      (-HIP_X, 0, H["hips"]),       (-HIP_X, 0, H["knee"])),
    ("RightLowerLeg", "RightUpperLeg", (-HIP_X, 0, H["knee"]),   (-HIP_X, 0, H["ankle"])),
    ("RightFoot",     "RightLowerLeg", (-HIP_X, 0, H["ankle"]),  (-HIP_X, -0.16, H["toe"])),
    ("RightToes",     "RightFoot", (-HIP_X, -0.16, H["toe"]),    (-HIP_X, -0.24, H["toe"])),
]


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"


def material(name, rgb, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
    return mat


def build_armature(name):
    """Скелет в позе покоя. Возвращает объект арматуры."""
    arm_data = bpy.data.armatures.new(name + "_Armature")
    arm_obj = bpy.data.objects.new("Armature", arm_data)
    bpy.context.collection.objects.link(arm_obj)

    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")

    created = {}
    for bone_name, parent, head, tail in BONES:
        eb = arm_data.edit_bones.new(bone_name)
        eb.head = mathutils.Vector(head)
        eb.tail = mathutils.Vector(tail)
        # Разворот вокруг оси кости. Оставляем нулевым: Unity всё равно
        # пересчитает всё в свою систему при построении аватара, а нам
        # важно, чтобы локальные оси были предсказуемы при позировании.
        eb.roll = 0.0
        if parent:
            eb.parent = created[parent]
            eb.use_connect = False
        created[bone_name] = eb

    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def bone_midpoint(arm_obj, bone_name):
    b = arm_obj.data.bones[bone_name]
    return (b.head_local + b.tail_local) * 0.5


def build_body(arm_obj, gi_color=(0.11, 0.21, 0.60)):
    """Меш бойца из fighter.py, привязанный к скелету.

    Раскладка атласа возвращается наружу: по ней texture.py рисует карты,
    и без неё рисунок не знал бы, где на картинке колено, а где лицо.
    """
    import fighter
    parts = fighter.build(gi_color)
    layout = fighter.pack_atlas(parts)
    mesh = fighter.attach(arm_obj, parts)
    mesh["atlas"] = layout
    return mesh, layout


ATLAS = {}


def build_fighter(name="Fighter", gi_color=(0.11, 0.21, 0.60)):
    """Собирает скелет и модель. Возвращает (арматура, меш)."""
    global ATLAS
    arm = build_armature(name)
    body, layout = build_body(arm, gi_color)
    ATLAS = layout
    return arm, body


def bounds(obj):
    """Габариты в мировых координатах — проверять позы глазами бесполезно."""
    zs, ys, xs = [], [], []
    for corner in obj.bound_box:
        v = obj.matrix_world @ mathutils.Vector(corner)
        xs.append(v.x)
        ys.append(v.y)
        zs.append(v.z)
    return (min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))


if __name__ == "__main__":
    reset_scene()
    arm, body = build_fighter()
    bx, by, bz = bounds(body)
    print("Скелет:", len(arm.data.bones), "костей")
    print("Габариты болванки  X %.2f..%.2f  Y %.2f..%.2f  Z %.2f..%.2f" %
          (bx[0], bx[1], by[0], by[1], bz[0], bz[1]))
