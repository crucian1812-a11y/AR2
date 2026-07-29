using System.Collections.Generic;
using UnityEngine;

// Достижения в духе Minecraft: короткая плашка на первое важное событие.
//
// Хранятся в общем сохранении (секция «ach»), а не в PlayerPrefs россыпью:
// «Начать заново» должно стирать их вместе со всем прогрессом.
public static class Achievements
{
    public struct Item
    {
        public string Key;
        public string Title;
        public string Text;
    }

    public static readonly Item[] All =
    {
        Make("firstcoin", "Первая монета", "Начало положено."),
        Make("firststar", "Звезда в руках", "Звёзды открывают порталы в деревне."),
        Make("firstmine", "Горняк", "Из добытого можно строить и мастерить."),
        Make("firstblock", "Строитель", "Блоками достают туда, куда не допрыгнуть."),
        Make("firstchest", "Кладоискатель", "Сундуки прячут по дальним углам."),
        Make("firstcraft", "Мастеровой", "Верстак у торговки, за прилавком."),
        Make("firstboss", "Победитель", "Боссы платят щедрее прочих."),
        Make("firstnight", "Ночная смена", "Ночью на окраинах небезопасно."),
        Make("allworlds", "Путешественник", "Побывать во всех шести краях."),
        Make("victory", "Сердце горы", "Игра пройдена.")
    };

    private static Item Make(string key, string title, string text)
    {
        Item it;
        it.Key = key;
        it.Title = title;
        it.Text = text;
        return it;
    }

    private static readonly HashSet<string> _got = new HashSet<string>();

    public static bool Has(string key) { return _got.Contains(key); }

    public static int Count { get { return _got.Count; } }

    // Выдать достижение. Повторный вызов ничего не делает — плашка
    // показывается ровно один раз за прохождение.
    public static void Grant(string key)
    {
        if (string.IsNullOrEmpty(key) || _got.Contains(key)) return;

        int idx = -1;
        for (int i = 0; i < All.Length; i++)
            if (All[i].Key == key) { idx = i; break; }
        if (idx < 0) return;

        _got.Add(key);
        Hud hud = Hud.I;
        if (hud != null) hud.ShowAchievement(All[idx].Title, All[idx].Text);
        Snd.Play("quest", 0.9f);

        NetManager net = NetManager.I;
        if (net != null) net.SaveProgress();
    }

    public static void Reset() { _got.Clear(); }

    public static string Export()
    {
        if (_got.Count == 0) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string k in _got)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(k);
        }
        return sb.ToString();
    }

    public static void Import(string raw)
    {
        _got.Clear();
        if (string.IsNullOrEmpty(raw)) return;
        string[] keys = raw.Split(',');
        for (int i = 0; i < keys.Length; i++)
            if (!string.IsNullOrEmpty(keys[i])) _got.Add(keys[i]);
    }
}
