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
except ImportError:
    sys.exit("Нужен пакет bpy: python3 -m pip install --break-system-packages bpy")


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


# Части болванки: (кость, половина габарита, смещение от середины кости).
# Каждая деталь принадлежит ровно одной кости с весом 1 — автовесами
# кубическую болванку привязывать нельзя, детали «поплывут».
PARTS = [
    ("Hips",          (0.115, 0.085, 0.09),  (0, 0, 0.01)),
    ("Spine",         (0.125, 0.085, 0.09),  (0, 0, 0)),
    ("Chest",         (0.155, 0.095, 0.11),  (0, 0, 0.01)),
    ("Neck",          (0.045, 0.045, 0.05),  (0, 0, 0)),
    ("Head",          (0.088, 0.095, 0.105), (0, 0.005, 0.02)),

    ("LeftUpperArm",  (0.14, 0.05, 0.05),    (0, 0, 0)),
    ("LeftLowerArm",  (0.085, 0.043, 0.043), (0, 0, 0)),
    ("LeftHand",      (0.05, 0.028, 0.055),  (0, 0, 0)),
    ("RightUpperArm", (0.14, 0.05, 0.05),    (0, 0, 0)),
    ("RightLowerArm", (0.085, 0.043, 0.043), (0, 0, 0)),
    ("RightHand",     (0.05, 0.028, 0.055),  (0, 0, 0)),

    ("LeftUpperLeg",  (0.065, 0.07, 0.225),  (0, 0, 0)),
    ("LeftLowerLeg",  (0.055, 0.06, 0.205),  (0, 0, 0)),
    ("LeftFoot",      (0.05, 0.09, 0.035),   (0, 0, 0)),
    ("RightUpperLeg", (0.065, 0.07, 0.225),  (0, 0, 0)),
    ("RightLowerLeg", (0.055, 0.06, 0.205),  (0, 0, 0)),
    ("RightFoot",     (0.05, 0.09, 0.035),   (0, 0, 0)),
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


def build_body(arm_obj, gi_color=(0.2, 0.32, 0.7)):
    """Кубическая болванка, привязанная к костям по одной детали на кость."""
    gi = material("Gi", gi_color, 0.85)
    skin = material("Skin", (0.82, 0.64, 0.5), 0.62)
    belt = material("Belt", (0.05, 0.05, 0.06), 0.7)

    meshes = []
    for bone_name, half, offset in PARTS:
        center = bone_midpoint(arm_obj, bone_name) + mathutils.Vector(offset)

        bpy.ops.mesh.primitive_cube_add(size=2.0, location=center)
        obj = bpy.context.active_object
        obj.name = "P_" + bone_name
        obj.scale = half

        # Габариты запекаем в вершины: иначе масштаб объекта поедет при
        # привязке к скелету и деталь раздуется вместе с костью.
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

        is_skin = bone_name in ("Head", "Neck", "LeftLowerArm", "RightLowerArm",
                               "LeftHand", "RightHand", "LeftLowerLeg", "RightLowerLeg")
        obj.data.materials.append(skin if is_skin else gi)

        # Весовая группа на одну кость с весом 1.
        vg = obj.vertex_groups.new(name=bone_name)
        vg.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
        meshes.append(obj)

    # Пояс — отдельной деталью на тазе: по нему видно, где «перёд», и
    # в партере это единственный надёжный ориентир.
    center = bone_midpoint(arm_obj, "Hips")
    bpy.ops.mesh.primitive_cube_add(size=2.0, location=center + mathutils.Vector((0, 0, -0.07)))
    obj = bpy.context.active_object
    obj.name = "P_Belt"
    obj.scale = (0.135, 0.10, 0.028)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(belt)
    vg = obj.vertex_groups.new(name="Hips")
    vg.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    meshes.append(obj)

    # Склеиваем в один меш: один объект — один SkinnedMeshRenderer в Unity.
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()

    body = bpy.context.active_object
    body.name = "Body"

    # Привязка к скелету по существующим весовым группам, без автовесов.
    body.parent = arm_obj
    mod = body.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm_obj
    return body


def build_fighter(name="Fighter", gi_color=(0.2, 0.32, 0.7)):
    """Собирает скелет и болванку. Возвращает (арматура, меш)."""
    arm = build_armature(name)
    body = build_body(arm, gi_color)
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
