using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Сохранение прогресса между запусками. Хранится в PlayerPrefs одной
// строкой. Сохраняет только хозяин сессии — клиент получает состояние
// по сети.
//
// ФОРМАТ. Версия 1 была позиционной: значения шли по порядку через '|',
// и любое новое поле сдвигало все следующие. Хуже того, Load возвращал
// false при несовпадении версии, то есть добавление одного числа молча
// стирало весь прогресс игрока.
//
// Версия 2 — именованные секции «ключ=значение», разделённые '|':
//
//     2|coins=120|stars=4|res=wood:12,stone:5|col=0:1,2;1:7|...
//
// Незнакомые ключи пропускаются, отсутствующие берут значение по
// умолчанию. Значит, следующее поле можно добавить, ничего не сломав, а
// сохранение версии 1 читается отдельной веткой и переезжает на новый
// формат при первой же записи.
public static class SaveGame
{
    private const string Key = "bear.save.v1";
    private const int Version = 2;

    public static void Save(NetManager net)
    {
        if (net == null) return;
        StringBuilder sb = new StringBuilder();
        sb.Append(Version);

        Put(sb, "coins", net.CoinsTotal);
        Put(sb, "stars", net.StarsTotal);
        Put(sb, "quest", net.QuestStage);
        Put(sb, "victory", net.VictoryReached ? 1 : 0);
        Put(sb, "hearts", net.MaxHearts);
        Put(sb, "chars", net.UnlockedChars);
        Put(sb, "char", net.CharIndex);
        Put(sb, "day", Mathf.RoundToInt(net.DayTime * 1000f));

        Put(sb, "col", Sets(net.ExportCollected()));
        Put(sb, "kil", Sets(net.ExportKilled()));
        Put(sb, "brk", Sets(net.ExportBroken()));
        Put(sb, "res", net.ExportResources());
        Put(sb, "blk", net.ExportBlocks());
        Put(sb, "ach", Achievements.Export());

        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }

    private static void Put(StringBuilder sb, string key, int value)
    {
        sb.Append('|').Append(key).Append('=').Append(value);
    }

    private static void Put(StringBuilder sb, string key, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append('|').Append(key).Append('=').Append(value);
    }

    // Наборы ID по мирам: «мир:id,id;мир:id».
    private static string Sets(Dictionary<int, HashSet<int>> sets)
    {
        if (sets == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<int, HashSet<int>> kv in sets)
        {
            if (kv.Value == null || kv.Value.Count == 0) continue;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(kv.Key).Append(':');
            bool first = true;
            foreach (int id in kv.Value)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(id);
            }
        }
        return sb.ToString();
    }

    public static bool HasSave()
    {
        return !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, ""));
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        // Новая игра начинается и с чистого обучения: подсказки про
        // двойной прыжок и удар сверху покажем заново.
        Tutor.Reset();
        Achievements.Reset();
        PlayerPrefs.Save();
    }

    // Возвращает false, только если сохранения нет вообще.
    //
    // withChar разделяет два случая. На старте игры (Bootstrap) героя из
    // сохранения взять надо — иначе в меню всегда предлагались бы первые
    // три. А при входе в мир — нельзя: игрок только что выбрал героя в
    // меню или в лавке, и подменять этот выбор прошлой игрой значит
    // сделать выбор бесполезным.
    public static bool Load(NetManager net, bool withChar = false)
    {
        if (net == null) return false;
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw)) return false;

        string[] parts = raw.Split('|');
        if (parts.Length < 2) return false;

        int version = ParseInt(parts[0]);
        if (version <= 1) return LoadV1(net, parts, withChar);

        Dictionary<string, string> f = new Dictionary<string, string>();
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq <= 0) continue;
            f[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
        }

        net.CoinsTotal = Field(f, "coins");
        net.StarsTotal = Field(f, "stars");
        net.QuestStage = Field(f, "quest");
        net.VictoryReached = Field(f, "victory") == 1;
        net.MaxHearts = Mathf.Clamp(Field(f, "hearts", 3), 3, 9);
        net.UnlockedChars = Mathf.Clamp(Field(f, "chars", Heroes.FreeChars),
            Heroes.FreeChars, Heroes.Ids.Length);
        net.CharIndex = Mathf.Clamp(withChar ? Field(f, "char") : net.CharIndex,
            0, net.UnlockedChars - 1);
        net.DayTime = Mathf.Repeat(Field(f, "day", 250) / 1000f, 1f);

        net.ImportCollected(ParseSets(Text(f, "col")));
        net.ImportKilled(ParseSets(Text(f, "kil")));
        net.ImportBroken(ParseSets(Text(f, "brk")));
        net.ImportResources(Text(f, "res"));
        net.ImportBlocks(Text(f, "blk"));
        Achievements.Import(Text(f, "ach"));
        return true;
    }

    // Старое позиционное сохранение. Читаем, чтобы прогресс не пропал;
    // при следующей записи оно само переедет на версию 2.
    private static bool LoadV1(NetManager net, string[] parts, bool withChar)
    {
        if (parts.Length < 9) return false;

        net.CoinsTotal = ParseInt(parts[1]);
        net.StarsTotal = ParseInt(parts[2]);
        net.QuestStage = ParseInt(parts[3]);
        net.VictoryReached = ParseInt(parts[4]) == 1;
        net.MaxHearts = Mathf.Clamp(ParseInt(parts[5]), 3, 9);
        net.UnlockedChars = Mathf.Clamp(ParseInt(parts[6]), Heroes.FreeChars, Heroes.Ids.Length);
        net.CharIndex = Mathf.Clamp(withChar ? ParseInt(parts[7]) : net.CharIndex,
            0, net.UnlockedChars - 1);

        string tail = parts[8];
        for (int i = 9; i < parts.Length; i++) tail += "|" + parts[i];

        string[] halves = tail.Split('#');
        net.ImportCollected(ParseSets(halves.Length > 0 ? halves[0] : ""));
        net.ImportKilled(ParseSets(halves.Length > 1 ? halves[1] : ""));
        return true;
    }

    private static int Field(Dictionary<string, string> f, string key, int fallback = 0)
    {
        string v;
        if (!f.TryGetValue(key, out v)) return fallback;
        int n;
        return int.TryParse(v, out n) ? n : fallback;
    }

    private static string Text(Dictionary<string, string> f, string key)
    {
        string v;
        return f.TryGetValue(key, out v) ? v : "";
    }

    public static int ParseInt(string s)
    {
        int v;
        return int.TryParse(s, out v) ? v : 0;
    }

    private static Dictionary<int, HashSet<int>> ParseSets(string s)
    {
        Dictionary<int, HashSet<int>> result = new Dictionary<int, HashSet<int>>();
        if (string.IsNullOrEmpty(s)) return result;

        string[] worlds = s.Split(';');
        for (int i = 0; i < worlds.Length; i++)
        {
            if (string.IsNullOrEmpty(worlds[i])) continue;
            string[] pair = worlds[i].Split(':');
            if (pair.Length != 2) continue;

            int world = ParseInt(pair[0]);
            HashSet<int> set = new HashSet<int>();
            string[] ids = pair[1].Split(',');
            for (int k = 0; k < ids.Length; k++)
            {
                if (string.IsNullOrEmpty(ids[k])) continue;
                set.Add(ParseInt(ids[k]));
            }
            result[world] = set;
        }
        return result;
    }
}
