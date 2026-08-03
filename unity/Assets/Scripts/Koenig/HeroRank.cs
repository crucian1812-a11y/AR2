namespace Koenig
{
    // Звание героя — RPG-прогрессия поверх собранных артефактов. Ребёнку
    // важно видеть, что он «растёт»: не просто «5 из 10 точек», а «теперь
    // ты Следопыт». Никакого своего состояния — считаем из Journey, чтобы
    // не было второго источника правды.
    //
    // Растёт по числу пройденных точек; высшее звание даётся только за
    // решённую головоломку-финал.
    public static class HeroRank
    {
        // Пороги по числу пройденных точек. Индекс — уровень (1..N).
        private static readonly int[] Thresholds = { 0, 1, 3, 5, 7 };
        private static readonly string[] Names = {
            "Юный путешественник",
            "Искатель мостов",
            "Следопыт Кёнигсберга",
            "Знаток семи мостов",
            "Хранитель артефактов",
        };
        public const string MasterName = "Мастер семи мостов";

        // Уровень 1..6 (6 — только после финала).
        public static int Level
        {
            get
            {
                if (Journey.PuzzleSolved) return Thresholds.Length + 1;
                int done = Journey.DonePoints;
                int lvl = 1;
                for (int i = 0; i < Thresholds.Length; i++)
                    if (done >= Thresholds[i]) lvl = i + 1;
                return lvl;
            }
        }

        public static int MaxLevel { get { return Thresholds.Length + 1; } }

        public static string Title
        {
            get
            {
                if (Journey.PuzzleSolved) return MasterName;
                int idx = Level - 1;
                if (idx < 0) idx = 0;
                if (idx >= Names.Length) idx = Names.Length - 1;
                return Names[idx];
            }
        }

        // Заполнение полоски опыта до следующего звания (0..1).
        public static float Progress01
        {
            get
            {
                if (Journey.PuzzleSolved) return 1f;
                int done = Journey.DonePoints;
                int cur = Thresholds[Level - 1];
                // Следующий порог — либо очередная ступень, либо финал.
                int next = (Level < Thresholds.Length) ? Thresholds[Level] : Journey.TotalPoints;
                if (next <= cur) return 1f;
                float p = (float)(done - cur) / (next - cur);
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        // Что нужно, чтобы вырасти. Короткая подсказка ребёнку.
        public static string NextHint
        {
            get
            {
                if (Journey.PuzzleSolved) return "Ты прошёл всё путешествие. Ты — мастер!";
                if (Level < Thresholds.Length)
                {
                    int need = Thresholds[Level] - Journey.DonePoints;
                    if (need < 1) need = 1;
                    return "До следующего звания: собери ещё " + need +
                           (need == 1 ? " артефакт" : (need < 5 ? " артефакта" : " артефактов"));
                }
                return "Собери все семь жетонов и разгадай финал, чтобы стать мастером!";
            }
        }
    }
}
