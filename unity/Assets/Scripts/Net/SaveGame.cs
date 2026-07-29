using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Сохранение прогресса между запусками. Хранится в PlayerPrefs одной
// строкой: монеты, звёзды, стадия задания, победа, купленное в лавке и
// списки собранных монет и побеждённых врагов по мирам.
// Сохраняет только хозяин сессии — клиент получает состояние по сети.
public static class SaveGame
{
    private const string Key = "bear.save.v1";
    private const int Version = 1;

    public static void Save(NetManager net)
    {
        if (net == null) return;
        StringBuilder sb = new StringBuilder();
        sb.Append(Version).Append('|');
        sb.Append(net.CoinsTotal).Append('|');
        sb.Append(net.StarsTotal).Append('|');
        sb.Append(net.QuestStage).Append('|');
        sb.Append(net.VictoryReached ? 1 : 0).Append('|');
        sb.Append(net.MaxHearts).Append('|');
        sb.Append(net.UnlockedChars).Append('|');
        sb.Append(net.CharIndex).Append('|');

        AppendSets(sb, net.ExportCollected());
        sb.Append('#');
        AppendSets(sb, net.ExportKilled());

        PlayerPrefs.SetString(Key, sb.ToString());
        PlayerPrefs.Save();
    }

    private static void AppendSets(StringBuilder sb, Dictionary<int, HashSet<int>> sets)
    {
        bool firstWorld = true;
        foreach (KeyValuePair<int, HashSet<int>> kv in sets)
        {
            if (kv.Value == null || kv.Value.Count == 0) continue;
            if (!firstWorld) sb.Append(';');
            firstWorld = false;
            sb.Append(kv.Key).Append(':');
            bool firstId = true;
            foreach (int id in kv.Value)
            {
                if (!firstId) sb.Append(',');
                firstId = false;
                sb.Append(id);
            }
        }
    }

    public static bool HasSave()
    {
        string raw = PlayerPrefs.GetString(Key, "");
        return !string.IsNullOrEmpty(raw);
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        // Новая игра начинается и с чистого обучения: подсказки про
        // двойной прыжок и удар сверху покажем заново.
        Tutor.Reset();
        PlayerPrefs.Save();
    }

    // Возвращает false, если сохранения нет или оно от другой версии.
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
        if (parts.Length < 9) return false;

        int version;
        if (!int.TryParse(parts[0], out version) || version != Version) return false;

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

    private static int ParseInt(string s)
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
