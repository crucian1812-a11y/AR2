#!/usr/bin/env python3
"""Боец целиком: тело, кимоно, пояс, голова — и привязка к скелету.

Каждая деталь целиком принадлежит одной кости (весовая группа с весом 1).
Автовесами такую модель привязывать нельзя: рукав кимоно налезает на
корпус, и Blender размажет его веса между грудью и плечом — при первом же
сгибе руки рукав потянет за собой грудь.

Жёсткая привязка оставляет щели в суставах. Их закрывают «шарниры» —
сферы в плечах, локтях, бёдрах и коленях, принадлежащие верхнему звену.
Приём старый и в стилизации работает лучше мягкой развесовки: силуэт
остаётся чётким, а сустав не «мнётся».
"""

import math

import bpy
import mathutils

import body as B

Vec = mathutils.Vector

# Высоты берутся из rig.H, чтобы меш и скелет не разъезжались: правка
# пропорций в одном месте.
import rig  # noqa: E402

H = rig.H
SHOULDER_X = rig.SHOULDER_X
HIP_X = rig.HIP_X

ELBOW_Z = H["elbow"] + 0.27      # в T-позе руки идут горизонтально
ARM_Z = ELBOW_Z


def _parts():
    """Список (объект, кость). Порядок только для читаемости."""
    return []


def build(gi_rgb=(0.11, 0.21, 0.60), skin_rgb=None):
    """Собирает бойца. Возвращает список пар (меш, имя кости)."""
    pal = B.palette(gi_rgb)
    if skin_rgb:
        pal["skin"] = B.material("Skin", skin_rgb, 0.55)

    parts = []

    def add(obj, bone, mat):
        B.finish(obj, mat)
        parts.append((obj, bone))
        return obj

    # ---------------------------------------------------------- корпус
    # Кимоно надето поверх тела, поэтому корпус сразу строится «в куртке»:
    # отдельный меш тела под ней не виден и только тратил бы полигоны.
    add(B.loft("Hips", [
        ((0, 0, H["hips"] - 0.10), 0.145, 0.105),
        ((0, 0, H["hips"] + 0.02), 0.150, 0.108),
        ((0, 0, H["spine"]),       0.148, 0.104),
    ]), "Hips", pal["gi"])

    add(B.loft("Spine", [
        ((0, 0, H["spine"] - 0.02), 0.146, 0.102),
        ((0, 0, H["chest"] - 0.06), 0.163, 0.110),
    ]), "Spine", pal["gi"])

    # Грудная клетка шире таза и уходит в плечи — это главный вклад в
    # силуэт борца.
    add(B.loft("Chest", [
        ((0, 0, H["chest"] - 0.08), 0.163, 0.116),
        ((0, 0, H["chest"] + 0.07), 0.181, 0.131),
        ((0, 0, H["shoulder"] - 0.02), 0.190, 0.133),
        ((0, 0, H["shoulder"] + 0.03), 0.176, 0.124),
        ((0, 0, H["neck"] + 0.01),  0.116, 0.092),
    ]), "Chest", pal["gi"])

    # Отвороты кимоно: две полосы крест-накрест от плеч к поясу. Без них
    # куртка читается свитером, а захват за отворот — половина игры в ги.
    # Отвороты вынесены на поверхность груди (грудь в этом месте толщиной
    # ~0.115 по Y) и развёрнуты в букву V. Утопленные внутрь груди они
    # просто не видны, а захват за отворот — половина игры в ги.
    for tag, sx in (("L", 1.0), ("R", -1.0)):
        add(B.loft("Lapel" + tag, [
            # Вверху отвороты разведены к плечам, внизу сходятся к узлу
            # пояса. Две вертикальные полосы рядом читались одной планкой.
            ((sx * 0.098, -0.112, H["shoulder"] - 0.02), 0.030, 0.019),
            ((sx * 0.062, -0.128, H["chest"] + 0.02), 0.032, 0.020),
            ((sx * 0.028, -0.126, H["hips"] + 0.04), 0.032, 0.020),
        ]), "Chest", pal["gi_dark"])

    # Юбка куртки ниже пояса — свободный край, а не обтяжка.
    add(B.loft("Skirt", [
        ((0, 0, H["hips"] - 0.06), 0.154, 0.113),
        ((0, 0, H["hips"] - 0.19), 0.163, 0.121),
    ], cap_end=False), "Hips", pal["gi"])

    # ----------------------------------------------------------- пояс
    add(B.loft("Belt", [
        ((0, 0, H["hips"] - 0.010), 0.158, 0.116),
        ((0, 0, H["hips"] - 0.068), 0.159, 0.117),
    ]), "Hips", pal["belt"])
    add(B.box("BeltKnot", (0.0, -0.122, H["hips"] - 0.039),
              (0.042, 0.024, 0.024)), "Hips", pal["belt"])
    # Два хвоста узла: они и отличают завязанный пояс от обруча.
    add(B.box("BeltTailL", (0.040, -0.128, H["hips"] - 0.112),
              (0.022, 0.012, 0.062), rotation=(0, 0, math.radians(5))),
        "Hips", pal["belt"])
    add(B.box("BeltTailR", (-0.040, -0.128, H["hips"] - 0.112),
              (0.022, 0.012, 0.062), rotation=(0, 0, math.radians(-5))),
        "Hips", pal["belt"])

    # ------------------------------------------------------ шея, голова
    add(B.limb("Neck", (0, 0, H["neck"] - 0.04), (0, 0, H["head"] + 0.02),
               0.064, 0.060), "Neck", pal["skin"])

    # Голова слегка сплюснута и вытянута назад — иначе получается шар.
    add(B.blob("Head", (0, 0.008, H["head"] + 0.10), 0.108,
               scale=(0.86, 0.98, 1.02), segments=14, rings=10),
        "Head", pal["skin"])
    add(B.box("Jaw", (0, -0.052, H["head"] + 0.045),
              (0.070, 0.052, 0.045)), "Head", pal["skin"])

    # Волосы шапочкой: отдельный объём сверху и сзади.
    add(B.blob("Hair", (0, 0.020, H["head"] + 0.132), 0.106,
               scale=(0.88, 0.98, 0.80), segments=14, rings=8),
        "Head", pal["hair"])

    # Глаза утоплены в череп: выступающая сфера читается кукольным
    # глазом, а нужна тёмная впадина глазницы.
    add(B.blob("EyeL", (0.040, -0.074, H["head"] + 0.106), 0.019,
               scale=(1.05, 0.42, 0.62), segments=8, rings=6), "Head", pal["eye"])
    add(B.blob("EyeR", (-0.040, -0.074, H["head"] + 0.106), 0.019,
               scale=(1.05, 0.42, 0.62), segments=8, rings=6), "Head", pal["eye"])
    add(B.blob("EarL", (0.092, 0.010, H["head"] + 0.088), 0.030,
               scale=(0.42, 0.85, 1.0), segments=8, rings=6), "Head", pal["skin"])
    add(B.blob("EarR", (-0.092, 0.010, H["head"] + 0.088), 0.030,
               scale=(0.42, 0.85, 1.0), segments=8, rings=6), "Head", pal["skin"])

    # ------------------------------------------------------------ руки
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        sh = (sx * SHOULDER_X, 0, H["shoulder"])
        elbow = (sx * 0.45, 0, ARM_Z)
        wrist = (sx * 0.62, 0, ARM_Z)
        hand = (sx * 0.70, 0, ARM_Z)

        # Плечо-шарнир: закрывает стык руки с корпусом при любом махе.
        add(B.blob(side + "ShoulderCap", (sx * 0.178, 0, H["shoulder"] - 0.008),
                   0.080, scale=(1.0, 1.05, 1.0)),
            side + "Shoulder", pal["gi"])

        # Рукав кимоно доходит до середины предплечья — так его и носят.
        add(B.limb(side + "UpperArm", sh, elbow, 0.074, 0.061, mid=0.079),
            side + "UpperArm", pal["gi"])
        add(B.blob(side + "ElbowCap", elbow, 0.061), side + "UpperArm", pal["gi"])

        # Радиус рукава совпадает с концом плеча, иначе на стыке ступенька.
        sleeve_end = (sx * 0.545, 0, ARM_Z)
        add(B.limb(side + "Sleeve", elbow, sleeve_end, 0.061, 0.056),
            side + "LowerArm", pal["gi"])
        add(B.limb(side + "LowerArm", sleeve_end, wrist, 0.048, 0.038, mid=0.050),
            side + "LowerArm", pal["skin"])

        # Кисть варежкой с большим пальцем: в партере кисти всё время в
        # кадре, и пятипалая кисть тут не нужна, а форма нужна.
        # Ладонь, а не шар: приплюснута по вертикали и вытянута по длине
        # руки. Шар на конце предплечья читается варежкой даже издали.
        add(B.blob(side + "Hand", (sx * 0.665, 0, ARM_Z), 0.044,
                   scale=(1.45, 0.92, 0.52)), side + "Hand", pal["skin"])
        add(B.blob(side + "Thumb", (sx * 0.648, -0.038, ARM_Z), 0.020,
                   scale=(1.0, 1.35, 0.7)), side + "Hand", pal["skin"])

    # ------------------------------------------------------------ ноги
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        hip = (sx * HIP_X, 0, H["hips"] - 0.06)
        knee = (sx * HIP_X, 0, H["knee"])
        ankle = (sx * HIP_X, 0, H["ankle"])

        add(B.blob(side + "HipCap", hip, 0.092, scale=(1.0, 0.95, 0.9)),
            side + "UpperLeg", pal["gi"])

        add(B.limb(side + "UpperLeg", hip, knee, 0.098, 0.076, mid=0.101),
            side + "UpperLeg", pal["gi"])
        add(B.blob(side + "KneeCap", knee, 0.074), side + "UpperLeg", pal["gi"])

        # Штанина до середины голени, дальше голая нога: борются босиком.
        shin_mid = (sx * HIP_X, 0, H["knee"] - (H["knee"] - H["ankle"]) * 0.45)
        add(B.limb(side + "Pant", knee, shin_mid, 0.080, 0.072),
            side + "LowerLeg", pal["gi"])
        add(B.limb(side + "Shin", shin_mid, ankle, 0.058, 0.040, mid=0.062),
            side + "LowerLeg", pal["skin"])

        add(B.blob(side + "Heel", (sx * HIP_X, -0.01, H["ankle"] - 0.005), 0.045,
                   scale=(0.9, 1.0, 0.8)), side + "Foot", pal["skin"])
        add(B.loft(side + "Foot", [
            ((sx * HIP_X, -0.02, H["toe"] + 0.032), 0.048, 0.055),
            ((sx * HIP_X, -0.19, H["toe"] + 0.022), 0.052, 0.045),
        ]), side + "Foot", pal["skin"])

    return parts


def attach(arm_obj, parts, name="Body"):
    """Склеивает детали в один меш и привязывает к скелету по группам."""
    for obj, bone in parts:
        vg = obj.vertex_groups.new(name=bone)
        vg.add(range(len(obj.data.vertices)), 1.0, "REPLACE")

    bpy.ops.object.select_all(action="DESELECT")
    for obj, _ in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0][0]
    bpy.ops.object.join()

    mesh_obj = bpy.context.active_object
    mesh_obj.name = name

    B.auto_smooth(mesh_obj)

    mesh_obj.parent = arm_obj
    mod = mesh_obj.modifiers.new(name="Armature", type="ARMATURE")
    mod.object = arm_obj
    return mesh_obj
