#!/usr/bin/env bash
# Локальная проверка C# кода игры: компилирует Assets/Scripts + Assets/Editor
# против заглушек Unity API. Ловит синтаксис, опечатки, несоответствия типов
# и ошибки в собственной логике до того, как запустится настоящая Unity-сборка.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STUBS="$ROOT/tools/csharp-check/UnityStubs.cs"
OUT="${TMPDIR:-/tmp}/bear-csharp-check.dll"
LOG="${TMPDIR:-/tmp}/bear-csharp-check.log"

mapfile -t SRC < <(find "$ROOT/unity/Assets" -name '*.cs' | sort)

if [ ${#SRC[@]} -eq 0 ]; then
  echo "No C# sources found under unity/Assets"
  exit 1
fi

echo "Checking ${#SRC[@]} game source file(s) against Unity API stubs..."
mcs -target:library -nostdlib+ -noconfig \
    -r:/usr/lib/mono/4.5/mscorlib.dll \
    -r:/usr/lib/mono/4.5/System.dll \
    -r:/usr/lib/mono/4.5/System.Core.dll \
    -langversion:7.2 \
    -out:"$OUT" \
    "$STUBS" "${SRC[@]}" >"$LOG" 2>&1
STATUS=$?

grep -v '^Compilation succeeded' "$LOG" || true

if [ $STATUS -eq 0 ]; then
  echo "C# CHECK OK"
else
  echo "C# CHECK FAILED"
fi

# Шейдеры проверяются здесь же: сломанный шейдер не роняет сборку, он
# просто перестаёт рисовать — и это дороже любой ошибки компилятора C#.
python3 "$ROOT/tools/shader-check/check.py" || STATUS=1

exit $STATUS
