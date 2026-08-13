#!/usr/bin/env python3
"""Позы борьбы: по одной паре на каждую позицию графа.

Главное решение здесь — **парные позы ставятся в одной сцене**. Позиция
партера это не «поза верхнего» плюс «поза нижнего», а их взаимное
зацепление: колено давит в бедро, рука подмышкой, голова с одной стороны.
Если авторить роли по отдельности, они гарантированно разъедутся.

Из этого следует и то, как клипы попадут в Unity: **оба бойца стоят в
одной точке мира, а всё взаимное расположение лежит в самой анимации**.
Так делают все парные захваты — иначе смещение применяется дважды и
бойцы расходятся тем сильнее, чем длиннее серия переходов.

Углы задаются в локальных осях кости, в градусах:
    X — сгиб (колено, локоть), основная ось
    Y — скрутка вдоль кости
    Z — отведение в сторону
Локальная ось Y кости всегда направлена от головы к хвосту, поэтому
запись не зависит от того, куда кость смотрит в позе покоя.
"""

import math

import mathutils

# Роли. TOP — тот, кто сверху (доминирует), BOT — снизу.
TOP = "Top"
BOT = "Bottom"

# Высота таза в позе покоя. Нужна, чтобы `root` задавал положение таза в
# мире, а не смещение от позы покоя: смещение — это тот самый способ
# посадить бойца в воздух на 0.95 м и не заметить.
HIPS_REST_Z = 0.95

# В позе покоя болванка смотрит в −Y (носки направлены туда же), поэтому
# поворот на 180° по Z разворачивает бойца лицом в +Y.
FACING = -1


def _p(**kwargs):
    """Поза: имя кости -> (X, Y, Z) в градусах.

    Ключ root — положение **таза в мире** и поворот всей фигуры:
    (x, y, z, rot_x, rot_y, rot_z). Именно таза, а не начала координат
    объекта: у лежащего бойца поворот на −88° уводит начало координат
    далеко в сторону, и задавать его вручную бессмысленно."""
    return kwargs


# ---------------------------------------------------------------- стойка

STANDING_TOP = _p(
    root=(0.0, 0.62, 0.95, 0, 0, 0),
    Spine=(-8, 0, 0),
    Chest=(-6, 0, 0),
    Head=(6, 0, 0),
    # Руки вперёд в захвате: из T-позы опускаем и выносим.
    LeftUpperArm=(0, 0, -62), LeftLowerArm=(-72, 0, -18),
    RightUpperArm=(0, 0, 62), RightLowerArm=(-72, 0, 18),
    LeftUpperLeg=(-18, 0, 4), LeftLowerLeg=(26, 0, 0),
    RightUpperLeg=(-12, 0, -4), RightLowerLeg=(20, 0, 0),
)

STANDING_BOT = _p(
    root=(0.0, -0.62, 0.95, 0, 0, 180),
    Spine=(-8, 0, 0),
    Chest=(-6, 0, 0),
    Head=(6, 0, 0),
    LeftUpperArm=(0, 0, -58), LeftLowerArm=(-68, 0, -16),
    RightUpperArm=(0, 0, 58), RightLowerArm=(-68, 0, 16),
    LeftUpperLeg=(-14, 0, 4), LeftLowerLeg=(22, 0, 0),
    RightUpperLeg=(-18, 0, -4), RightLowerLeg=(26, 0, 0),
)

# ------------------------------------------------------- закрытая гвардия

# Нижний на спине, ноги сомкнуты за поясницей верхнего. Верхний на
# коленях между ног. Высота таза нижнего — 0.13: он лежит, а не сидит.
CLOSED_GUARD_BOT = _p(
    root=(0.0, 0.0, 0.13, -84, 0, 0),
    Spine=(12, 0, 0),
    Chest=(10, 0, 0),
    Head=(-26, 0, 0),
    # Ноги обхватывают: сильный сгиб в бедре и колене, лёгкое сведение.
    LeftUpperLeg=(-96, 0, 16), LeftLowerLeg=(104, 0, 0),
    RightUpperLeg=(-96, 0, -16), RightLowerLeg=(104, 0, 0),
    # Руки тянут за отвороты вверх-вперёд.
    LeftUpperArm=(-28, 0, -74), LeftLowerArm=(-84, 0, -10),
    RightUpperArm=(-28, 0, 74), RightLowerArm=(-84, 0, 10),
)

CLOSED_GUARD_TOP = _p(
    root=(0.0, -0.42, 0.42, 0, 0, 180),
    # На коленях: бёдра подогнуты почти полностью.
    LeftUpperLeg=(-88, 0, 8), LeftLowerLeg=(128, 0, 0),
    RightUpperLeg=(-88, 0, -8), RightLowerLeg=(128, 0, 0),
    Spine=(-14, 0, 0),
    Chest=(-8, 0, 0),
    Head=(16, 0, 0),
    # Руки упираются в грудь нижнего — постуральный контроль.
    LeftUpperArm=(-16, 0, -70), LeftLowerArm=(-38, 0, -12),
    RightUpperArm=(-16, 0, 70), RightLowerArm=(-38, 0, 12),
)

# ------------------------------------------------------- удержание сбоку

# Верхний лежит поперёк груди нижнего, головой к его левому плечу.
SIDE_CONTROL_BOT = _p(
    root=(0.0, 0.0, 0.13, -88, 0, 0),
    Spine=(6, 0, 0),
    Chest=(4, 0, 0),
    Head=(-20, 0, 25),
    LeftUpperLeg=(-14, 0, 12), LeftLowerLeg=(22, 0, 0),
    RightUpperLeg=(-10, 0, -12), RightLowerLeg=(16, 0, 0),
    # Руки упёрты в бедро и шею верхнего — рамка защиты.
    LeftUpperArm=(-40, 0, -46), LeftLowerArm=(-96, 0, -20),
    RightUpperArm=(-30, 0, 52), RightLowerArm=(-88, 0, 16),
)

SIDE_CONTROL_TOP = _p(
    root=(0.45, 0.42, 0.32, -74, 0, 90),
    Spine=(-6, 0, 0),
    Chest=(-4, 0, 0),
    Head=(30, 0, 0),
    # Ноги разведены и вытянуты назад — вес идёт на грудь нижнего.
    LeftUpperLeg=(-16, 0, 26), LeftLowerLeg=(30, 0, 0),
    RightUpperLeg=(-8, 0, -30), RightLowerLeg=(46, 0, 0),
    # Одна рука под шеей, другая под дальним бедром: это и есть удержание.
    LeftUpperArm=(-52, 0, -58), LeftLowerArm=(-104, 0, -26),
    RightUpperArm=(-46, 0, 60), RightLowerArm=(-100, 0, 24),
)

# -------------------------------------------------------------- верхом

MOUNT_BOT = _p(
    root=(0.0, 0.0, 0.13, -88, 0, 0),
    Spine=(4, 0, 0),
    Chest=(2, 0, 0),
    Head=(-18, 0, 0),
    LeftUpperLeg=(-10, 0, 10), LeftLowerLeg=(14, 0, 0),
    RightUpperLeg=(-10, 0, -10), RightLowerLeg=(14, 0, 0),
    # Руки подняты, отталкивают — типичная защита из-под верхом.
    LeftUpperArm=(-58, 0, -60), LeftLowerArm=(-92, 0, -18),
    RightUpperArm=(-58, 0, 60), RightLowerArm=(-92, 0, 18),
)

MOUNT_TOP = _p(
    root=(0.0, 0.30, 0.46, -8, 0, 180),
    Spine=(-10, 0, 0),
    Chest=(-6, 0, 0),
    Head=(14, 0, 0),
    # Колени прижаты к бокам нижнего, голени назад.
    LeftUpperLeg=(-74, 0, 22), LeftLowerLeg=(122, 0, 0),
    RightUpperLeg=(-74, 0, -22), RightLowerLeg=(122, 0, 0),
    LeftUpperArm=(-30, 0, -66), LeftLowerArm=(-56, 0, -14),
    RightUpperArm=(-30, 0, 66), RightLowerArm=(-56, 0, 14),
)

# --------------------------------------------------------------- спина

# Оба сидят, верхний позади, ноги-крюки внутри бёдер нижнего.
BACK_CONTROL_BOT = _p(
    root=(0.0, 0.0, 0.32, -18, 0, 0),
    Spine=(10, 0, 0),
    Chest=(8, 0, 0),
    Head=(-14, 0, 0),
    LeftUpperLeg=(-72, 0, 16), LeftLowerLeg=(64, 0, 0),
    RightUpperLeg=(-72, 0, -16), RightLowerLeg=(64, 0, 0),
    # Руки тянут чужое предплечье от шеи — защита от удушения.
    LeftUpperArm=(-56, 0, -40), LeftLowerArm=(-104, 0, -24),
    RightUpperArm=(-56, 0, 40), RightLowerArm=(-104, 0, 24),
)

BACK_CONTROL_TOP = _p(
    root=(0.0, 0.34, 0.30, -22, 0, 0),
    Spine=(8, 0, 0),
    Chest=(6, 0, 0),
    Head=(-10, 0, 18),
    # Крюки: бёдра разведены и обхватывают нижнего.
    LeftUpperLeg=(-84, 0, 34), LeftLowerLeg=(72, 0, 0),
    RightUpperLeg=(-84, 0, -34), RightLowerLeg=(72, 0, 0),
    # Рука через шею — заготовка удушения сзади.
    LeftUpperArm=(-72, 0, -52), LeftLowerArm=(-118, 0, -34),
    RightUpperArm=(-64, 0, 48), RightLowerArm=(-112, 0, 30),
)

# ------------------------------------------------------------ черепаха

TURTLE_BOT = _p(
    root=(0.0, 0.0, 0.45, -62, 0, 180),
    Spine=(14, 0, 0),
    Chest=(10, 0, 0),
    Head=(-30, 0, 0),
    LeftUpperLeg=(-88, 0, 12), LeftLowerLeg=(118, 0, 0),
    RightUpperLeg=(-88, 0, -12), RightLowerLeg=(118, 0, 0),
    # Локти прижаты к коленям — смысл черепахи в том, чтобы не дать щелей.
    LeftUpperArm=(-40, 0, -80), LeftLowerArm=(-128, 0, -8),
    RightUpperArm=(-40, 0, 80), RightLowerArm=(-128, 0, 8),
)

TURTLE_TOP = _p(
    root=(0.40, -0.10, 0.58, -30, 0, 90),
    Spine=(-8, 0, 0),
    Chest=(-6, 0, 0),
    Head=(20, 0, 0),
    LeftUpperLeg=(-70, 0, 24), LeftLowerLeg=(104, 0, 0),
    RightUpperLeg=(-64, 0, -26), RightLowerLeg=(96, 0, 0),
    LeftUpperArm=(-48, 0, -62), LeftLowerArm=(-108, 0, -22),
    RightUpperArm=(-44, 0, 64), RightLowerArm=(-104, 0, 20),
)


# Полугвардия — промежуточная: нижний зажал одну ногу верхнего.
HALF_GUARD_BOT = _p(
    root=(0.0, 0.0, 0.13, -84, 0, 10),
    Spine=(8, 0, 0),
    Chest=(6, 0, 0),
    Head=(-22, 0, 12),
    LeftUpperLeg=(-84, 0, 22), LeftLowerLeg=(96, 0, 0),
    RightUpperLeg=(-40, 0, -10), RightLowerLeg=(52, 0, 0),
    LeftUpperArm=(-44, 0, -54), LeftLowerArm=(-98, 0, -18),
    RightUpperArm=(-32, 0, 58), RightLowerArm=(-90, 0, 18),
)

HALF_GUARD_TOP = _p(
    root=(0.14, -0.10, 0.42, -45, 0, 170),
    Spine=(-8, 0, 0),
    Chest=(-6, 0, 0),
    Head=(24, 0, 0),
    LeftUpperLeg=(-58, 0, 28), LeftLowerLeg=(88, 0, 0),
    RightUpperLeg=(-30, 0, -20), RightLowerLeg=(64, 0, 0),
    LeftUpperArm=(-50, 0, -56), LeftLowerArm=(-100, 0, -22),
    RightUpperArm=(-44, 0, 58), RightLowerArm=(-96, 0, 22),
)


# Позиции графа -> (поза верхнего, поза нижнего). Имена совпадают со
# значениями enum Pos в unity/Assets/Scripts/Game/Positions.cs — это и
# есть связь между анимацией и правилами.
POSITIONS = {
    "Standing":    (STANDING_TOP, STANDING_BOT),
    "ClosedGuard": (CLOSED_GUARD_TOP, CLOSED_GUARD_BOT),
    "OpenGuard":   (CLOSED_GUARD_TOP, CLOSED_GUARD_BOT),
    "HalfGuard":   (HALF_GUARD_TOP, HALF_GUARD_BOT),
    "SideControl": (SIDE_CONTROL_TOP, SIDE_CONTROL_BOT),
    "Mount":       (MOUNT_TOP, MOUNT_BOT),
    "BackControl": (BACK_CONTROL_TOP, BACK_CONTROL_BOT),
    "TurtleDown":  (TURTLE_TOP, TURTLE_BOT),
}


def apply(arm_obj, pose):
    """Ставит арматуру в позу. Углы — в локальных осях кости."""
    root = pose.get("root")
    if root:
        rot = mathutils.Euler((math.radians(root[3]),
                               math.radians(root[4]),
                               math.radians(root[5])), "XYZ")
        arm_obj.rotation_euler = rot

        # Начало координат объекта считаем из требуемого положения таза:
        # location = таз − R·(таз в позе покоя).
        hips_rest = mathutils.Vector((0.0, 0.0, HIPS_REST_Z))
        want = mathutils.Vector((root[0], root[1], root[2]))
        arm_obj.location = want - (rot.to_matrix() @ hips_rest)

    for bone in arm_obj.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)

    for name, angles in pose.items():
        if name == "root":
            continue
        bone = arm_obj.pose.bones.get(name)
        if bone is None:
            raise KeyError("В скелете нет кости " + name)
        bone.rotation_mode = "XYZ"
        # Порядок осей: X — сгиб, Y — скрутка вдоль кости, Z — отведение.
        bone.rotation_euler = (math.radians(angles[0]),
                               math.radians(angles[1]),
                               math.radians(angles[2]))
