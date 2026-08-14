#!/usr/bin/env python3
"""Рисовка текстур бойца: альбедо, карта нормалей и ORM.

Почему текстуры, а не шум в шейдере, как было раньше. Шум считается
каждый кадр — около двенадцати выборок на пиксель, — и умеет только
однородную зернистость. Он не нарисует брови, губы, щетину, вены на
предплечьях, потёртость именно на колене и полоски именно на поясе.
А в текстуре всё это рисуется намеренно, стоит один раз при сборке и в
кадре бесплатно.

Работает это только потому, что развёртка сделана осознанно
(см. body.py и fighter.pack_atlas): у каждой детали своя клетка атласа, и
рисунок знает, где на картинке лицо, а где колено. С автоматической
развёрткой такое невозможно — там острова ложатся как попало.

Карты:
  albedo — цвет
  normal — рельеф (поры, переплетение ткани, складки)
  orm    — R: затенение, G: шероховатость, B: металличность

Файлы кладутся в unity/Assets/Resources/Textures/fighters/ и собираются
в CI, как и FBX; в репозиторий не коммитятся.
"""

import math
import os
import sys

import numpy as np

try:
    import bpy
except ImportError as exc:
    sys.exit("Не удалось загрузить bpy (%s)" % exc)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "unity", "Assets", "Resources", "Textures", "fighters")

SIZE = 1024


# --------------------------------------------------------------- шум

def _value_noise(shape, freq, seed):
    """Значение шума на решётке с гладкой интерполяцией."""
    rng = np.random.default_rng(seed)
    gw = int(freq) + 1
    grid = rng.random((gw + 1, gw + 1))

    ys = np.linspace(0, freq, shape[0], endpoint=False)
    xs = np.linspace(0, freq, shape[1], endpoint=False)
    gy, gx = np.meshgrid(ys, xs, indexing="ij")

    y0 = gy.astype(int)
    x0 = gx.astype(int)
    fy = gy - y0
    fx = gx - x0
    # Сглаживающая кривая: линейная интерполяция даёт видимую решётку.
    fy = fy * fy * (3 - 2 * fy)
    fx = fx * fx * (3 - 2 * fx)

    n00 = grid[y0, x0]
    n10 = grid[y0 + 1, x0]
    n01 = grid[y0, x0 + 1]
    n11 = grid[y0 + 1, x0 + 1]

    return (n00 * (1 - fy) * (1 - fx) + n10 * fy * (1 - fx) +
            n01 * (1 - fy) * fx + n11 * fy * fx)


def fbm(shape, freq=4, octaves=5, seed=0, gain=0.5):
    """Сумма октав шума."""
    total = np.zeros(shape)
    amp = 1.0
    norm = 0.0
    for i in range(octaves):
        total += _value_noise(shape, freq * (2 ** i), seed + i * 101) * amp
        norm += amp
        amp *= gain
    return total / norm


def height_to_normal(height, strength=1.0):
    """Карта нормалей из карты высот.

    Считается градиентом: наклон поверхности в точке и есть то, что карта
    нормалей кодирует. Отдельно рисовать её не нужно и вредно — рельеф и
    его нормаль обязаны совпадать, иначе блик ложится не на бугорок.
    """
    gy, gx = np.gradient(height)
    nx = -gx * strength * 40.0
    ny = -gy * strength * 40.0
    nz = np.ones_like(height)

    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx /= length
    ny /= length
    nz /= length

    return np.stack([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5], axis=-1)


def _grad(shape, axis=0):
    """Линейный градиент 0..1 вдоль оси — для затемнения к краям."""
    if axis == 0:
        return np.linspace(0, 1, shape[0])[:, None] * np.ones((1, shape[1]))
    return np.ones((shape[0], 1)) * np.linspace(0, 1, shape[1])[None, :]


# ------------------------------------------------------------ холст

class Canvas:
    """Три карты сразу: рисовать их порознь — значит рассинхронизировать."""

    def __init__(self, size=SIZE):
        self.size = size
        self.albedo = np.zeros((size, size, 3))
        self.height = np.zeros((size, size))
        self.rough = np.full((size, size), 0.6)
        self.ao = np.ones((size, size))

    def cell(self, rect):
        """Границы клетки в пикселях: (y0, y1, x0, x1)."""
        x, y, w, h = rect
        return (int(y * self.size), int((y + h) * self.size),
                int(x * self.size), int((x + w) * self.size))

    def shape(self, rect):
        y0, y1, x0, x1 = self.cell(rect)
        return (y1 - y0, x1 - x0)

    def paint(self, rect, albedo=None, height=None, rough=None, ao=None):
        y0, y1, x0, x1 = self.cell(rect)
        if albedo is not None:
            self.albedo[y0:y1, x0:x1] = albedo
        if height is not None:
            self.height[y0:y1, x0:x1] = height
        if rough is not None:
            self.rough[y0:y1, x0:x1] = rough
        if ao is not None:
            self.ao[y0:y1, x0:x1] = ao

    def save(self, prefix):
        os.makedirs(OUT_DIR, exist_ok=True)
        _save_rgb(prefix + "_albedo", self.albedo)
        _save_rgb(prefix + "_normal", height_to_normal(self.height))
        orm = np.stack([self.ao, self.rough, np.zeros_like(self.rough)], axis=-1)
        _save_rgb(prefix + "_orm", orm)


def _save_rgb(name, rgb):
    """Сохраняет карту в PNG средствами Blender: PIL в контейнере нет."""
    h, w = rgb.shape[0], rgb.shape[1]
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)

    rgba = np.ones((h, w, 4))
    rgba[:, :, :3] = np.clip(rgb, 0.0, 1.0)

    # Без переворота. Blender укладывает пиксели начиная с нижней строки,
    # то есть pixels[0] — это v=0; строка 0 массива тоже соответствует
    # v=0. Порядок уже совпадает, и `[::-1]` не компенсировал ничего, а
    # вносил зеркало: вся развёртка отражалась по вертикали, и брови,
    # нарисованные над глазами, оказывались на затылке под волосами.
    img.pixels = rgba.ravel()

    img.filepath_raw = os.path.join(OUT_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    print("  ->", os.path.relpath(img.filepath_raw, ROOT))


# ------------------------------------------------------------ кожа

def paint_skin(canvas, rects, base=(0.76, 0.60, 0.50)):
    for part, rect in rects.items():
        shape = canvas.shape(rect)
        if shape[0] < 4 or shape[1] < 4:
            continue

        # Разнотон: живая кожа нигде не одного цвета.
        # Разнотон. Первая версия брала размах 0.22 и давала камуфляж:
        # у кожи вариации мягкие, их видно только вблизи, и превышать
        # несколько процентов нельзя.
        mottle = fbm(shape, freq=6, octaves=4, seed=hash(part) % 9973) - 0.5
        tone = np.array(base)[None, None, :] * (1.0 + mottle[..., None] * 0.07)
        # Краснота идёт в красный канал сильнее, чем в синий: так работает
        # кровь под кожей.
        tone[..., 0] *= 1.0 + mottle * 0.11
        tone[..., 2] *= 1.0 - mottle * 0.07

        # Поры — мелкая зернистость в рельефе.
        pores = fbm(shape, freq=48, octaves=3, seed=7 + hash(part) % 991)
        height = (pores - 0.5) * 0.06

        rough = 0.52 + (pores - 0.5) * 0.18
        ao = np.ones(shape)

        low = part.lower()

        if low.startswith("skull"):
            _paint_face(tone, height, rough, shape)
        elif "lowerarm" in low:
            _paint_veins(tone, height, shape, part)
        elif "palm" in low or "finger" in low or "thumb" in low:
            # Костяшки и ладонь темнее и краснее: они всё время в упоре.
            tone *= 0.94
            tone[..., 0] *= 1.06
            rough += 0.08
        elif "shin" in low or "heel" in low or "foot" in low:
            tone *= 0.96
            rough += 0.05

        canvas.paint(rect, albedo=tone, height=height, rough=np.clip(rough, 0.05, 1.0), ao=ao)


def _paint_face(tone, height, rough, shape):
    """Лицо: брови, губы, щетина, тень глазниц.

    Развёртка черепа цилиндрическая вокруг вертикальной оси: перед лица
    приходится на u = 0.75, v идёт от темени (0) к подбородку (1). Это и
    позволяет рисовать брови там, где брови, а не «где-то в текстуре».

    Высоты ниже — не на глаз, а по замеру развёртки (tools/blender,
    uvprobe): v нелинейно по высоте, потому что считается вдоль
    поверхности, а свод черепа заваливается. Ключевые отметки:

        линия волос   v ≈ 0.58   (выше — скрыто причёской)
        глазницы      v ≈ 0.67
        рот           v ≈ 0.95
        челюсть       v ≈ 0.80 … 1.00

    Первые две версии рисовались по прикидке «сверху вниз линейно», и
    брови оба раза уходили под волосы.
    """
    h, w = shape
    yy = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
    xx = np.ones((h, 1)) * np.linspace(0, 1, w)[None, :]

    face = np.exp(-((xx - 0.75) ** 2) / (2 * 0.055 ** 2))

    # Брови: две дуги чуть выше середины лица. Широкие и тонкие — первая
    # версия делала их узкими и высокими, и получались чёрные кляксы.
    # Цвет не чёрный, а тёмно-коричневый: чёрные брови на лице читаются
    # дырами.
    brow_rgb = np.array([0.30, 0.20, 0.14])
    # Разнос бровей должен превышать их ширину, иначе они сливаются в
    # одну — что и вышло при сигме 0.042 на расстоянии 0.076.
    # Бровь чуть выше глазницы и заметно ниже линии волос.
    for cx in (0.7175, 0.7825):
        brow = np.exp(-((xx - cx) ** 2) / (2 * 0.019 ** 2)) * \
               np.exp(-((yy - 0.632) ** 2) / (2 * 0.0080 ** 2))
        brow = np.clip(brow, 0, 1) * 0.85
        # Присваивание в срез, а не `tone = ...`: переприсваивание меняет
        # только локальное имя, и правка не доходит до вызывающего кода.
        tone[...] = tone * (1 - brow[..., None]) + brow_rgb[None, None, :] * brow[..., None]

    # Тень в глазницах — мягкое затемнение под бровями.
    for cx in (0.715, 0.785):
        socket = np.exp(-((xx - cx) ** 2) / (2 * 0.030 ** 2)) * \
                 np.exp(-((yy - 0.672) ** 2) / (2 * 0.026 ** 2))
        tone *= (1.0 - 0.30 * socket[..., None])

    # Губы. Не «красные»: у губ тон кожи, только темнее и чуть розовее.
    # Умножение красного канала давало алое пятно, поэтому подмешиваем
    # готовый цвет, а не крутим каналы.
    lip_rgb = np.array([0.62, 0.38, 0.34])
    lips = np.exp(-((xx - 0.75) ** 2) / (2 * 0.038 ** 2)) * \
           np.exp(-((yy - 0.950) ** 2) / (2 * 0.012 ** 2))
    lips = np.clip(lips, 0, 1) * 0.8
    tone[...] = tone * (1 - lips[..., None]) + lip_rgb[None, None, :] * lips[..., None]
    rough -= 0.16 * lips

    # Щетина по нижней части лица. Маску челюсти берём заметно шире
    # маски лица: челюсть заходит на щёки и уходит к ушам, а по узкой
    # маске получалась вертикальная полоса под губами.
    jaw_mask = np.exp(-((xx - 0.75) ** 2) / (2 * 0.115 ** 2))
    jaw = jaw_mask * np.clip((yy - 0.80) / 0.10, 0, 1)
    stubble = fbm(shape, freq=110, octaves=2, seed=451)
    mask = jaw * np.clip((stubble - 0.46) * 6.0, 0, 1)
    tone *= (1.0 - 0.16 * mask[..., None])
    rough += 0.10 * mask

    height += 0.010 * lips - 0.006 * face * np.clip((0.47 - yy) / 0.2, 0, 1)


def _paint_veins(tone, height, shape, part):
    """Вены на предплечье: тонкие ветвящиеся линии под кожей."""
    h, w = shape
    yy = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
    xx = np.ones((h, 1)) * np.linspace(0, 1, w)[None, :]

    warp = fbm(shape, freq=3, octaves=3, seed=hash(part) % 313) - 0.5
    # Извилистость даёт именно искажение прямой линии шумом: ровные
    # полосы читались бы проводами.
    line = np.abs(np.sin((xx + warp * 0.5) * math.pi * 3.0))
    veins = np.clip(1.0 - line * 6.0, 0, 1) * np.clip((yy - 0.15) / 0.5, 0, 1)

    tone[..., 2] *= 1.0 + 0.18 * veins
    tone[..., 0] *= 1.0 - 0.06 * veins
    height += veins * 0.020


# -------------------------------------------------------------- ткань

def paint_cloth(canvas, rects, base=(0.86, 0.87, 0.88), weave_freq=150):
    """Ткань печётся почти белой намеренно.

    Синий и красный боец берут одну и ту же карту, а цвет приходит из
    материала (шейдер умножает текстуру на _Color). Запеки мы цвет в
    текстуру — понадобилось бы по атласу на каждого бойца, и оба весили
    бы одинаково при одинаковом рисунке."""
    for part, rect in rects.items():
        shape = canvas.shape(rect)
        if shape[0] < 4 or shape[1] < 4:
            continue

        h, w = shape
        yy = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
        xx = np.ones((h, 1)) * np.linspace(0, 1, w)[None, :]

        # Переплетение: две перпендикулярные волны. Именно рельеф, а не
        # только цвет — на ткани блик ломается о нити.
        weave = (np.sin(xx * weave_freq) * np.sin(yy * weave_freq)) * 0.5 + 0.5
        height = (weave - 0.5) * 0.10

        # Крупные складки поверх переплетения.
        folds = fbm(shape, freq=5, octaves=4, seed=hash(part) % 7919) - 0.5
        height += folds * 0.12

        tone = np.array(base)[None, None, :] * (1.0 + folds[..., None] * 0.14 +
                                                (weave[..., None] - 0.5) * 0.10)
        rough = np.full(shape, 0.88) - folds * 0.06
        ao = 1.0 - np.clip(-folds, 0, 1) * 0.35

        low = part.lower()

        # Потёртости там, где кимоно трёт о татами.
        if "leg" in low or "knee" in low or "elbow" in low or "arm" in low:
            wear = np.clip(fbm(shape, freq=4, octaves=3, seed=31) * 1.6 - 0.45, 0, 1)
            tone = tone * (1 - wear[..., None] * 0.5) + wear[..., None] * 0.5 * \
                np.array([0.78, 0.80, 0.84])[None, None, :]
            rough += wear * 0.08

        # Строчка по краю отворотов и юбки.
        if "lapel" in low or "skirt" in low:
            stitch = np.exp(-((xx - 0.12) ** 2) / (2 * 0.012 ** 2)) + \
                     np.exp(-((xx - 0.88) ** 2) / (2 * 0.012 ** 2))
            dashes = (np.sin(yy * 220.0) > 0).astype(float)
            stitch = stitch * dashes
            tone *= (1.0 - 0.28 * stitch[..., None])
            height -= stitch * 0.05

        canvas.paint(rect, albedo=tone, height=height,
                     rough=np.clip(rough, 0.05, 1.0), ao=np.clip(ao, 0.2, 1.0))


def paint_belt(canvas, rects, base=(0.05, 0.05, 0.06)):
    """Пояс: плотное плетение и полоски на хвосте.

    Полоски — не украшение. Чёрный пояс с полосками мгновенно читается как
    уровень бойца, и это единственное место, где можно показать его без
    единой подписи на экране.
    """
    for part, rect in rects.items():
        shape = canvas.shape(rect)
        if shape[0] < 4 or shape[1] < 4:
            continue

        h, w = shape
        yy = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
        xx = np.ones((h, 1)) * np.linspace(0, 1, w)[None, :]

        weave = (np.sin(xx * 260) * np.sin(yy * 90)) * 0.5 + 0.5
        height = (weave - 0.5) * 0.16
        tone = np.array(base)[None, None, :] + (weave[..., None] - 0.5) * 0.05
        rough = np.full(shape, 0.72)

        if "tail" in part.lower():
            # Красная полоса и четыре белые нашивки поперёк хвоста.
            red = np.exp(-((yy - 0.30) ** 2) / (2 * 0.055 ** 2))
            tone = tone * (1 - red[..., None]) + \
                np.array([0.42, 0.03, 0.04])[None, None, :] * red[..., None]

            for k in range(4):
                y = 0.44 + k * 0.075
                bar = np.exp(-((yy - y) ** 2) / (2 * 0.013 ** 2)) * \
                      ((xx > 0.18) & (xx < 0.82)).astype(float)
                tone = tone * (1 - bar[..., None]) + \
                    np.array([0.88, 0.88, 0.86])[None, None, :] * bar[..., None]

        canvas.paint(rect, albedo=tone, height=height, rough=rough)


# ------------------------------------------------------------ сборка

def build(layout, gi_rgb=(0.55, 0.58, 0.66)):
    """Рисует все атласы по раскладке из fighter.pack_atlas."""
    made = []
    for mat_name, rects in layout.items():
        low = mat_name.lower()
        canvas = Canvas()

        if low.startswith("skin"):
            paint_skin(canvas, rects)
            prefix = "skin"
        elif low.startswith("belt"):
            paint_belt(canvas, rects)
            prefix = "belt"
        elif low.startswith("gidark"):
            paint_cloth(canvas, rects)
            prefix = "gidark"
        elif low.startswith("gi"):
            paint_cloth(canvas, rects)
            prefix = "gi"
        else:
            # Волосы, глаза, рот — мелочь без собственного рисунка.
            continue

        canvas.save(prefix)
        made.append(prefix)
    return made


# ------------------------------------------------- проверка на модели

def apply_to_blender(layout):
    """Вешает нарисованные карты на материалы Blender.

    Нужно ровно для одного: убедиться глазами, что лицо легло на лицо.
    Всё, что до этого момента, — предположения о том, куда цилиндрическая
    развёртка отправила ту или иную часть черепа, а ошибка тут не видна
    ни в коде, ни в самом атласе.
    """
    for mat_name in layout:
        low = mat_name.lower()
        if low.startswith("skin"):
            prefix = "skin"
        elif low.startswith("belt"):
            prefix = "belt"
        elif low.startswith("gidark"):
            prefix = "gidark"
        elif low.startswith("gi"):
            prefix = "gi"
        else:
            continue

        path = os.path.join(OUT_DIR, prefix + "_albedo.png")
        if not os.path.exists(path):
            continue

        mat = bpy.data.materials.get(mat_name)
        if mat is None or not mat.use_nodes:
            continue

        img = bpy.data.images.load(path, check_existing=True)
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = img
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
