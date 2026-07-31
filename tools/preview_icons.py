#!/usr/bin/env python3
"""Отрисовка значков лавки в PNG — проверка до сборки APK.

Значки в Icons.cs заданы строками по символу на пиксель. Прочитать
такой рисунок глазами в исходнике можно, но понять, читается ли он на
витрине, — нет: первая версия булыжника выглядела в коде разумно, а на
картинке оказалась решёткой.

Скрипт берёт и рисунки, и цвета прямо из Icons.cs, поэтому проверяет
именно то, что попадёт в игру. Заодно следит, что все картинки строго
шестнадцать на шестнадцать.

    python3 tools/preview_icons.py
"""

import io
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "unity", "Assets", "Scripts", "UI", "Icons.cs")
OUT = os.path.join(ROOT, "tools", "blender", "preview", "icons.png")

SIZE = 16
SCALE = 6
GAP = 4
# Порядок показа и поле, из которого берутся цвета.
ORDER = ["_heart", "_coin", "_stone", "_iron", "_crystal", "_wood"]


def main():
    src = io.open(SRC, encoding="utf-8").read()

    def rows(name):
        m = re.search(r"string\[\] " + name + r"\s*=\s*\{(.*?)\};", src, re.S)
        if m is None:
            sys.exit("не нашёл рисунок " + name)
        return re.findall(r'"([^"]*)"', m.group(1))

    def palette(field):
        m = re.search(field + r"\s*=\s*Make\((\w+),(.*?)\);", src, re.S)
        if m is None:
            sys.exit("не нашёл цвета для " + field)
        cols = re.findall(r"new Color\(([\d.]+)f?,\s*([\d.]+)f?,\s*([\d.]+)f?\)",
                          m.group(2))
        return m.group(1), [tuple(int(float(v) * 255) for v in c) for c in cols]

    width = (SIZE * SCALE + GAP) * len(ORDER) + GAP
    height = SIZE * SCALE + GAP * 2
    img = [[(28, 32, 44) for _ in range(width)] for _ in range(height)]

    for n, field in enumerate(ORDER):
        name, cols = palette(field)
        art = rows(name)
        if len(art) != SIZE or any(len(r) != SIZE for r in art):
            sys.exit("%s: рисунок не %dx%d — %s" %
                     (name, SIZE, SIZE, [len(r) for r in art]))
        # Make(light, mid, dark, hi) — тот же порядок, что в Icons.cs.
        table = {"L": cols[0], "M": cols[1], "D": cols[2], "H": cols[3]}
        ox = GAP + n * (SIZE * SCALE + GAP)
        for y in range(SIZE):
            for x in range(SIZE):
                ch = art[y][x]
                if ch == ".":
                    continue
                for dy in range(SCALE):
                    for dx in range(SCALE):
                        img[GAP + y * SCALE + dy][ox + x * SCALE + dx] = table[ch]

    raw = b"".join(b"\x00" + bytes(v for px in row for v in px) for row in img)

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))

    png = (b"\x89PNG\r\n\x1a\n" +
           chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)) +
           chunk(b"IDAT", zlib.compress(raw, 9)) +
           chunk(b"IEND", b""))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "wb").write(png)
    print("порядок:", ", ".join(f.lstrip("_") for f in ORDER))
    print("превью:", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
