#!/usr/bin/env python3
"""Проверка шейдеров без Unity.

Компилятора HLSL здесь нет, и полноценной проверки быть не может. Но три
ошибки, на которых мы уже обожглись, ловятся простым разбором текста — и
все три одинаково коварны: шейдер не собирается, Shader.Find возвращает
null, материал из null ничего не рисует, и боец выходит **прозрачным**.
Ни исключения, ни сообщения — только пустое место на экране.

Что проверяется:

1. Свойство объявлено в Properties и используется в HLSL, но не объявлено
   как uniform (в CBUFFER или как TEXTURE2D). Блок Properties сам по себе
   ничего не объявляет.
2. TRANSFORM_TEX(uv, _Tex) без float4 _Tex_ST — макрос разворачивается в
   обращение к _Tex_ST.
3. Обращение к полю структуры, которого в ней нет (IN.tangentOS при
   Attributes без TANGENT).

В редакторе всё это не видно: там шейдер собирается под другой профиль и
проблема всплывает только на устройстве.
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHADERS = os.path.join(ROOT, "unity", "Assets", "Shaders")


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    return text


def properties_block(src):
    i = src.find("Properties")
    if i < 0:
        return "", src
    start = src.find("{", i)
    depth = 0
    for j in range(start, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[start:j], src[:i] + src[j + 1:]
    return "", src


def declared_names(src):
    names = set()
    for block in re.findall(r"CBUFFER_START\(.*?\)(.*?)CBUFFER_END", src, re.S):
        names |= set(re.findall(r"(_\w+)\s*(?:\[[^\]]*\])?\s*;", block))
    names |= set(re.findall(r"TEXTURE2D(?:_ARRAY|_X)?\((_\w+)\)", src))
    names |= set(re.findall(r"TEXTURECUBE\((_\w+)\)", src))
    names |= set(re.findall(r"SAMPLER\((\w+)\)", src))
    # Одиночные uniform вне cbuffer — так объявлен, например, _LightDirection
    # в проходе теней.
    names |= set(re.findall(r"^\s*(?:half|float|int|uint|real)[234]?(?:x[234])?\s+(_\w+)\s*;",
                            src, re.M))
    return names


def structs(src):
    """Имя структуры -> множество её полей."""
    out = {}
    for name, body in re.findall(r"struct\s+(\w+)\s*\{(.*?)\}\s*;", src, re.S):
        fields = set()
        for line in body.split(";"):
            line = line.strip()
            if not line or line.startswith("UNITY_"):
                continue
            m = re.match(r"[\w<>]+[234]?(?:x[234])?\s+(\w+)", line)
            if m:
                fields.add(m.group(1))
        out[name] = fields
    return out


def functions(src):
    """Список (имя, параметры, тело) для каждой функции в тексте."""
    out = []
    for m in re.finditer(r"\b(\w+)\s+(\w+)\s*\(([^)]*)\)\s*(?::\s*\w+\s*)?\{", src):
        start = src.index("{", m.end() - 1)
        depth = 0
        for j in range(start, len(src)):
            if src[j] == "{":
                depth += 1
            elif src[j] == "}":
                depth -= 1
                if depth == 0:
                    out.append((m.group(2), m.group(3), src[start:j]))
                    break
    return out


def check_file(path):
    raw = open(path, encoding="utf-8").read()
    src = strip_comments(raw)
    props_block, code = properties_block(src)

    props = re.findall(r"(_\w+)\s*\(", props_block)
    decl = declared_names(code)
    bad = []

    for p in dict.fromkeys(props):
        if p in decl:
            continue
        if re.search(r"\b" + re.escape(p) + r"\b", code):
            bad.append("свойство %s используется, но не объявлено uniform" % p)

    for tex in set(re.findall(r"TRANSFORM_TEX\s*\([^,]+,\s*(_\w+)\s*\)", code)):
        if tex + "_ST" not in decl:
            bad.append("TRANSFORM_TEX(%s) без объявления %s_ST" % (tex, tex + ""))

    known = structs(code)
    for name, params, body in functions(code):
        # Имена IN/OUT переиспользуются в каждом проходе с разными
        # структурами, поэтому область видимости — тело функции, а не файл.
        types = {}
        for typ, var in re.findall(r"\b(\w+)\s+(\w+)", params):
            if typ in known:
                types[var] = typ
        for typ, var in re.findall(r"\b(\w+)\s+(\w+)\s*[;=]", body):
            if typ in known:
                types[var] = typ

        for var, field in re.findall(r"\b(\w+)\.(\w+)\b", body):
            typ = types.get(var)
            if typ is None:
                continue
            if field not in known[typ]:
                bad.append("%s(): %s.%s — в структуре %s нет такого поля"
                           % (name, var, field, typ))

    return sorted(set(bad))


def main():
    files = sorted(f for f in os.listdir(SHADERS) if f.endswith(".shader"))
    if not files:
        print("шейдеры не найдены в " + SHADERS)
        return 1

    problems = 0
    for f in files:
        bad = check_file(os.path.join(SHADERS, f))
        if bad:
            problems += len(bad)
            print("== " + f)
            for b in bad:
                print("   " + b)

    print("Проверено шейдеров: %d" % len(files))
    if problems:
        print("SHADER CHECK FAILED (%d)" % problems)
        return 1
    print("SHADER CHECK OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
