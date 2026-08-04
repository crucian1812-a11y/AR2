#!/usr/bin/env python3
"""Сборка играбельного рыцаря из пака KayKit Adventurers (CC0).

ЗАЧЕМ ЭТОТ СКРИПТ ВООБЩЕ НУЖЕН. В паке персонажи и анимации лежат
РАЗДЕЛЬНО: Knight.fbx содержит меши и скелет, но ноль клипов, а клипы
лежат отдельными файлами на общем риге Rig_Medium. Для Unity это обычное
дело — там их сводят ретаргетингом на Humanoid. Но наш проект читает
анимацию через Legacy (см. ModelImportSettings), а Legacy требует, чтобы
клипы лежали В ТОМ ЖЕ файле, что и модель. Поэтому сводим здесь.

Скелеты совпадают буква в букву — 23 кости, общий корень root, — так что
действия переносятся на рыцаря напрямую, без ретаргетинга.

ЧЕГО В ПАКЕ НЕТ. В бесплатном тире нет ни одного клипа атаки: есть
ходьба, бег, прыжки, простой, попадание, смерть, подбор, бросок. Удар
мечом дописывается здесь кейфреймами по четырём костям — это несколько
строк, в отличие от лепки самой модели.

Имена клипов переводятся в те, что ищет игра (CharacterModel.Pick):
Idle, Walk, Run, Jump, Bite_InPlace (удар), HitRecieve, Death.

Запуск:
    blender -b --python tools/blender/build_kaykit.py -- <папка распакованного пака>

Кладёт FBX в unity/Assets/Resources/Models/monsters/ и текстуру в
unity/Assets/Resources/Textures/monsters/ по конвенции <Id>_Texture.png.
"""

import math
import os
import shutil
import sys

try:
    import bpy
except ImportError:
    sys.exit("Скрипт запускается внутри Blender: blender -b --python ...")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODEL_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Models", "monsters")
TEX_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Textures", "monsters")

FPS = 24

# Кого собираем: id в игре → файл персонажа и его текстура.
# Рыцарь — герой, остальные пойдут на средневековых воинов-врагов.
CHARACTERS = [
    ("Knight", "Knight.fbx", "knight_texture.png"),
    ("Rogue", "Rogue.fbx", "rogue_texture.png"),
    ("Barbarian", "Barbarian.fbx", "barbarian_texture.png"),
    ("Ranger", "Ranger.fbx", "ranger_texture.png"),
]

# Какой клип пака становится каким клипом игры. Берём вариант «A»
# везде, где пак даёт несколько: они основные, B/C — вариации на
# разнообразие, которого нам пока некуда девать.
CLIP_MAP = {
    "Idle_A": "Idle",
    "Walking_A": "Walk",
    "Running_A": "Run",
    "Jump_Full_Short": "Jump",
    "Hit_A": "HitRecieve",
    "Death_A": "Death",
    "PickUp": "Interact",
}

# Клипы, которые в игру не идут: варианты, позы и служебное. Без чистки
# FBX распухает вдвое, а Legacy тащит в память каждый.
DROP = ("T-Pose", "_Pose", "Idle_B", "Walking_B", "Walking_C", "Running_B",
        "Hit_B", "Death_B", "Jump_Full_Long", "Jump_Idle", "Jump_Land",
        "Jump_Start", "Spawn_Air", "Spawn_Ground", "Throw", "Use_Item",
        "Interact")


def strip(name):
    """Имя действия без префикса рига: «Rig_Medium|Idle_A» → «Idle_A»."""
    return name.split("|")[-1]


def import_actions(path):
    """Забрать действия из файла анимаций и выкинуть его геометрию."""
    before = set(bpy.data.actions.keys())
    known = set(o.name for o in bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)

    for o in list(bpy.data.objects):
        if o.name not in known:
            bpy.data.objects.remove(o, do_unlink=True)

    return [bpy.data.actions[n] for n in bpy.data.actions.keys() if n not in before]


def author_attack(arm):
    """Замах мечом: в паке атаки нет, дописываем сами.

    Четыре кости и три позы — этого хватает, чтобы удар читался. Корпус
    доворачивается вместе с рукой: замах одной кистью выглядит вялым, а
    ребёнок должен видеть, что рыцарь вложился в удар.
    """
    act = bpy.data.actions.new("Attack")
    arm.animation_data_create()
    arm.animation_data.action = act

    # кость: (замах, удар, возврат) в градусах по XYZ
    moves = {
        "chest":      [(0, 0, 28), (0, 0, -34), (0, 0, 0)],
        "upperarm.r": [(-58, 0, -22), (74, 0, 16), (0, 0, 0)],
        "lowerarm.r": [(-46, 0, 0), (14, 0, 0), (0, 0, 0)],
        "hand.r":     [(-18, 0, 0), (22, 0, 0), (0, 0, 0)],
    }
    # Замах медленный, удар резкий, возврат средний — иначе движение
    # читается как равномерное помахивание.
    frames = [1, 7, 12, 20]

    for bone, poses in moves.items():
        pb = arm.pose.bones.get(bone)
        if pb is None:
            continue
        pb.rotation_mode = "XYZ"
        seq = [(0, 0, 0)] + poses
        for f, rot in zip(frames, seq):
            pb.rotation_euler = tuple(math.radians(a) for a in rot)
            pb.keyframe_insert("rotation_euler", frame=f)

    arm.animation_data.action = None
    return act


def build(pack_dir, char_id, char_file, tex_file):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS

    chars = os.path.join(pack_dir, "Characters", "fbx")
    anims = os.path.join(pack_dir, "Animations", "fbx", "Rig_Medium")

    bpy.ops.import_scene.fbx(filepath=os.path.join(chars, char_file))
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        sys.exit("в %s нет скелета" % char_file)
    arm = arms[0]

    got = []
    for f in ("Rig_Medium_MovementBasic.fbx", "Rig_Medium_General.fbx"):
        p = os.path.join(anims, f)
        if os.path.exists(p):
            got += import_actions(p)

    # Переименовать нужное, выбросить лишнее.
    kept = []
    for act in got:
        base = strip(act.name)
        if base in CLIP_MAP:
            act.name = CLIP_MAP[base]
            act.use_fake_user = True
            kept.append(act.name)
        else:
            bpy.data.actions.remove(act)

    for act in list(bpy.data.actions):
        if any(d in act.name for d in DROP):
            bpy.data.actions.remove(act)

    # Удар: игра ищет его как Bite_InPlace (так называется клип атаки у
    # моделей Quaternius, и код перебирает имена в этом порядке).
    atk = author_attack(arm)
    atk.name = "Bite_InPlace"
    atk.use_fake_user = True
    kept.append(atk.name)

    print("КЛИПЫ %s: %s" % (char_id, ", ".join(sorted(kept))))

    os.makedirs(MODEL_DIR, exist_ok=True)
    out = os.path.join(MODEL_DIR, char_id + ".fbx")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=out,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
    )

    # Текстура по конвенции проекта: CharacterModel грузит её как
    # Textures/monsters/<Id>_Texture.
    src_tex = os.path.join(pack_dir, "Textures", tex_file)
    if os.path.exists(src_tex):
        os.makedirs(TEX_DIR, exist_ok=True)
        shutil.copyfile(src_tex, os.path.join(TEX_DIR, char_id + "_Texture.png"))

    print("готово:", os.path.relpath(out, ROOT))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        sys.exit("укажите папку распакованного пака KayKit")
    pack = argv[0]
    only = argv[1] if len(argv) > 1 else None
    for cid, cfile, tfile in CHARACTERS:
        if only and cid != only:
            continue
        if not os.path.exists(os.path.join(pack, "Characters", "fbx", cfile)):
            print("пропуск: нет", cfile)
            continue
        build(pack, cid, cfile, tfile)


if __name__ == "__main__":
    main()
