using System.Collections.Generic;
using UnityEngine;

// Проверка шейдеров при запуске.
//
// Однажды это стоило целой сборки: BjjSkin и BjjCloth перестали
// компилироваться под Android (кончились интерполяторы), Shader.Find вернул
// null, материал из null ничего не рисует — и оба бойца вышли прозрачными.
// Ни лога, ни ошибки: по картинке причину не угадать.
//
// Важно, что в редакторе такую поломку не видно — там шейдер находится и
// собирается под другой профиль. Поэтому проверка делается на устройстве,
// в момент старта, и говорит вслух: не только «нашёлся ли шейдер», но и
// поддерживается ли он этим железом (isSupported).
public static class ShaderCheck
{
    private static readonly string[] Needed =
    {
        "Bjj/Lit", "Bjj/Skin", "Bjj/Cloth", "Bjj/Glow"
    };

    /// Пустая строка — всё в порядке. Иначе описание того, что сломано.
    public static string Verify()
    {
        List<string> bad = new List<string>();

        for (int i = 0; i < Needed.Length; i++)
        {
            Shader sh = Shader.Find(Needed[i]);
            if (sh == null)
            {
                bad.Add(Needed[i] + " — не найден");
                continue;
            }
            if (!sh.isSupported)
            {
                bad.Add(Needed[i] + " — не поддерживается");
            }
        }

        if (bad.Count == 0) return "";
        return string.Join("\n", bad.ToArray());
    }
}
