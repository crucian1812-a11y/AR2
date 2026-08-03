using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Прогресс путешествия: что уже пройдено и что собрано. Хранится в
    // PlayerPrefs одной строкой — приложение может закрываться между
    // точками (ребёнок ездит по области целый день), и состояние обязано
    // переживать это без сервера и без сети.
    //
    // Единственный источник правды о прогрессе. UI и головоломка читают
    // отсюда, а не держат своих копий.
    public static class Journey
    {
        private const string KeyDone = "koenig.done";     // id пройденных точек
        private const string KeySolved = "koenig.solved";  // головоломка решена

        // Пройденные точки — по id. Достаточно множества: артефакт и
        // город точки берутся из KoenigContent, дублировать их незачем.
        private static readonly HashSet<string> _done = new HashSet<string>();
        private static bool _solved;
        private static bool _loaded;

        public static void Load()
        {
            _done.Clear();
            string raw = PlayerPrefs.GetString(KeyDone, "");
            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split(';');
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i].Length > 0) _done.Add(parts[i]);
            }
            _solved = PlayerPrefs.GetInt(KeySolved, 0) != 0;
            _loaded = true;
        }

        private static void EnsureLoaded() { if (!_loaded) Load(); }

        public static void Save()
        {
            PlayerPrefs.SetString(KeyDone, string.Join(";", new List<string>(_done).ToArray()));
            PlayerPrefs.SetInt(KeySolved, _solved ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            _done.Clear();
            _solved = false;
            _loaded = true;
            Save();
        }

        // ---------- Точки ----------

        public static bool IsDone(string poiId)
        {
            EnsureLoaded();
            return _done.Contains(poiId);
        }

        // Отметить точку пройденной и выдать её артефакт (артефакт — это и
        // есть сам факт прохождения, отдельно его хранить не нужно).
        public static void Complete(string poiId)
        {
            EnsureLoaded();
            if (_done.Add(poiId)) Save();
        }

        // ---------- Артефакты ----------

        public static int Count(Artifact kind)
        {
            EnsureLoaded();
            int n = 0;
            Poi[] pts = KoenigContent.Points;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].Gives == kind && _done.Contains(pts[i].Id)) n++;
            return n;
        }

        public static bool HasBridgeToken(string bridgeId)
        {
            EnsureLoaded();
            Poi[] pts = KoenigContent.Points;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].Gives == Artifact.BridgeToken &&
                    pts[i].BridgeOrLand == bridgeId && _done.Contains(pts[i].Id))
                    return true;
            return false;
        }

        // ---------- Финал ----------

        // Головоломка открывается, когда собраны все семь жетонов мостов —
        // без них карту не из чего складывать.
        public static bool PuzzleUnlocked
        {
            get
            {
                List<string> need = KoenigContent.AllBridgeTokens();
                for (int i = 0; i < need.Count; i++)
                    if (!HasBridgeToken(need[i])) return false;
                return need.Count > 0;
            }
        }

        public static bool PuzzleSolved
        {
            get { EnsureLoaded(); return _solved; }
        }

        public static void MarkSolved()
        {
            EnsureLoaded();
            if (!_solved) { _solved = true; Save(); }
        }

        // ---------- Шаг пути ----------

        // Текущий город — первый в порядке пути, где ещё не всё пройдено.
        // Когда всё пройдено, возвращает финальный город.
        public static string CurrentCity()
        {
            EnsureLoaded();
            string[] order = KoenigContent.JourneyOrder;
            for (int c = 0; c < order.Length; c++)
            {
                Poi[] pts = KoenigContent.Points;
                for (int i = 0; i < pts.Length; i++)
                    if (pts[i].CityId == order[c] && !_done.Contains(pts[i].Id))
                        return order[c];
            }
            return KoenigContent.FinaleCity;
        }

        public static int TotalPoints { get { return KoenigContent.Points.Length; } }

        public static int DonePoints
        {
            get { EnsureLoaded(); return _done.Count; }
        }
    }
}
