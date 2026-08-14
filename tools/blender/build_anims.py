#!/usr/bin/env python3
"""Сборка анимаций и экспорт в FBX для Unity.

    python3 tools/blender/build_anims.py

Кладёт `unity/Assets/Resources/Models/fighters/Fighter.fbx` — одна модель,
внутри все клипы обеих ролей.

## Почему одна модель на обе роли

Роль — это не отдельный персонаж, а то, какой клип боец сейчас играет.
Оба бойца в игре берут одну и ту же модель (свою из пака Quaternius), и
позицию задаёт пара клипов: `Top_Mount` у одного, `Bottom_Mount` у другого.
Клипы сняты в одной сцене друг против друга, поэтому совпадают.

## Оба бойца стоят в одной точке

Взаимное расположение целиком лежит в анимации, а корневые объекты обоих
бойцов в Unity стоят в центре схватки. Иначе смещение применилось бы
дважды: раз из `Staging`, раз из клипа. После подключения этих клипов
раскладка в `Staging.cs` нужна только капсулам серого бокса и камере.

## Клипы

- `Top_<Позиция>` / `Bottom_<Позиция>` — удержание позиции, зациклено.
  Не статичная поза: дыхание и микродвижение, иначе картинка мертвеет.
- `Top_<Приём>` / `Bottom_<Приём>` — переход между позициями, один проход.
  Длительность берётся из таблицы приёмов в Transitions.cs.
"""

import math
import os
import sys

try:
    import bpy
except ImportError as exc:
    # Показываем настоящую причину. Пакет может стоять и всё равно не
    # грузиться: bpy — нативный модуль, и ему нужны системные библиотеки.
    # Совет «поставьте пакет» в таком случае только сбивает с толку.
    sys.exit("Не удалось загрузить bpy (%s).\n"
             "Установка: python3 -m pip install --break-system-packages bpy" % exc)

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import poses  # noqa: E402
import rig  # noqa: E402
import texture  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Models", "fighters")
OUT_FBX = os.path.join(OUT_DIR, "Fighter.fbx")

FPS = 30
HOLD_FRAMES = 60      # две секунды на цикл удержания


# Переходы: (имя клипа, откуда, куда, секунд). Длительности совпадают с
# полем Time в Transitions.cs — клип должен кончаться тогда же, когда
# правила отдают результат, иначе приём «срабатывает» до того, как виден.
MOVES = [
    ("Pass_Closed",      "ClosedGuard", "HalfGuard",   1.3),
    ("Pass_Half",        "HalfGuard",   "SideControl", 1.2),
    ("Mount_Transition", "SideControl", "Mount",       1.0),
    ("TakeBack",         "TurtleDown",  "BackControl", 1.0),
    ("TakeBack_Mount",   "Mount",       "BackControl", 1.1),
    ("Sweep_Scissor",    "ClosedGuard", "Mount",       1.1),
    ("Recover_Guard",    "HalfGuard",   "ClosedGuard", 1.0),
    ("Escape_Side",      "SideControl", "HalfGuard",   1.2),
    ("Escape_Mount",     "Mount",       "HalfGuard",   1.4),
    ("Escape_Back",      "BackControl", "TurtleDown",  1.3),
    ("StandUp",          "TurtleDown",  "Standing",    1.0),
    ("PullGuard",        "Standing",    "ClosedGuard", 0.8),
    ("Takedown_Double",  "Standing",    "SideControl", 1.1),
]


def key_pose(arm, pose, frame, breathe=0.0):
    """Ставит позу и записывает ключи на кадр.

    `breathe` — небольшая добавка к сгибу груди: удержание позиции без
    неё выглядит фотографией, а не живым человеком.
    """
    poses.apply(arm, pose)

    if breathe:
        chest = arm.pose.bones.get("Chest")
        if chest:
            e = chest.rotation_euler
            chest.rotation_euler = (e[0] + math.radians(breathe), e[1], e[2])

    arm.keyframe_insert(data_path="location", frame=frame)
    arm.keyframe_insert(data_path="rotation_euler", frame=frame)
    for bone in arm.pose.bones:
        bone.keyframe_insert(data_path="rotation_euler", frame=frame)


def new_action(arm, name):
    action = bpy.data.actions.new(name)
    # Метка «ложного пользователя» обязательна: без неё Blender выбрасывает
    # действие при переключении, и до экспорта доживает только последнее.
    action.use_fake_user = True
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = action
    return action


def build_hold(arm, role, position, pose):
    """Цикл удержания позиции: вдох-выдох, первый и последний кадр совпадают."""
    new_action(arm, role + "_" + position)
    key_pose(arm, pose, 1, breathe=0.0)
    key_pose(arm, pose, 1 + HOLD_FRAMES // 2, breathe=2.5)
    key_pose(arm, pose, 1 + HOLD_FRAMES, breathe=0.0)


def build_move(arm, role, move_name, pose_from, pose_to, seconds):
    """Переход между позициями. Ключи только по краям плюс средний кадр.

    Средний кадр не для красоты: без него интерполяция ведёт таз по прямой
    сквозь татами, когда боец встаёт. Приподнятая середина задаёт дугу.
    """
    frames = max(2, int(round(seconds * FPS)))
    new_action(arm, role + "_" + move_name)

    key_pose(arm, pose_from, 1)

    mid = {}
    mid.update(pose_to)
    r_from = pose_from.get("root")
    r_to = pose_to.get("root")
    if r_from and r_to:
        mid["root"] = tuple(
            (a + b) * 0.5 + (0.06 if i == 2 else 0.0)
            for i, (a, b) in enumerate(zip(r_from, r_to))
        )
    key_pose(arm, mid, 1 + frames // 2)

    key_pose(arm, pose_to, 1 + frames)


def build_all():
    rig.reset_scene()
    arm, body = rig.build_fighter("Fighter")

    scene = bpy.context.scene
    scene.render.fps = FPS

    made = []

    for position, (top_pose, bot_pose) in sorted(poses.POSITIONS.items()):
        build_hold(arm, poses.TOP, position, top_pose)
        made.append(poses.TOP + "_" + position)
        build_hold(arm, poses.BOT, position, bot_pose)
        made.append(poses.BOT + "_" + position)

    for move_name, src, dst, seconds in MOVES:
        if src not in poses.POSITIONS or dst not in poses.POSITIONS:
            print("  пропуск", move_name, "— нет позиции", src, "или", dst)
            continue
        src_top, src_bot = poses.POSITIONS[src]
        dst_top, dst_bot = poses.POSITIONS[dst]
        build_move(arm, poses.TOP, move_name, src_top, dst_top, seconds)
        made.append(poses.TOP + "_" + move_name)
        build_move(arm, poses.BOT, move_name, src_bot, dst_bot, seconds)
        made.append(poses.BOT + "_" + move_name)

    return arm, body, made


def export(arm, body):
    os.makedirs(OUT_DIR, exist_ok=True)

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm

    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX,
        use_selection=True,
        add_leaf_bones=False,
        # Запекание системы координат ломает позы оснащённых моделей —
        # проверено на медвежьем проекте, где от него разъезжался скелет.
        bake_space_transform=False,
        object_types={"ARMATURE", "MESH"},
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        # Прореживание ключей. С нулём экспортёр пишет каждый кадр каждой
        # кости: 42 клипа давали 6.9 МБ, а файл лежит в LFS и каждая
        # пересборка съедает квоту заново. Unity всё равно прореживает
        # клипы на импорте (KeyframeReduction), так что терять нечего.
        bake_anim_simplify_factor=1.0,
        axis_forward="-Z",
        axis_up="Y",
    )


if __name__ == "__main__":
    arm, body, made = build_all()

    # Текстуры рисуются здесь же, по раскладке атласа той самой модели,
    # которая сейчас экспортируется. Разносить это по разным запускам
    # нельзя: раскладка зависит от состава деталей, и текстуры от другой
    # сборки легли бы мимо.
    maps = texture.build(rig.ATLAS)
    print("Атласов:", ", ".join(maps))

    export(arm, body)

    size = os.path.getsize(OUT_FBX) / 1024.0
    print("Клипов:", len(made))
    print("FBX:", os.path.relpath(OUT_FBX, ROOT), "%.0f КБ" % size)
