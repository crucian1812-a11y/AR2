using UnityEngine;

namespace Koenig
{
    // Цифровая половина «двух ключей». Journey хранит РЕАЛЬНЫЙ прогресс
    // (доехал до точки), а здесь — ИГРОВОЙ: пройден ли уровень города в
    // самой игре. Мост становится золотым, только когда собраны обе
    // половины — цифровая (здесь) и настоящая (Journey). Так RPG и
    // офлайн-квест держатся друг за друга, а не идут порознь.
    //
    // Отдельное хранилище, чтобы не путать: уровень можно пройти дома в
    // машине заранее, но настоящий жетон появится лишь на месте.
    public static class DigitalProgress
    {
        private const string Prefix = "koenig.digital.";

        public static bool IsDone(string cityId)
        {
            if (string.IsNullOrEmpty(cityId)) return false;
            return PlayerPrefs.GetInt(Prefix + cityId, 0) != 0;
        }

        public static void MarkDone(string cityId)
        {
            if (string.IsNullOrEmpty(cityId)) return;
            if (IsDone(cityId)) return;
            PlayerPrefs.SetInt(Prefix + cityId, 1);
            PlayerPrefs.Save();
        }

        // Город «сплавлен» — золотой мост получен, когда пройдены обе
        // половины: уровень в игре и точка в реальности.
        public static bool IsFused(string cityId)
        {
            return IsDone(cityId) && RealDone(cityId);
        }

        // Настоящая половина: хотя бы одна точка города отмечена пройденной.
        public static bool RealDone(string cityId)
        {
            Poi[] pts = KoenigContent.Points;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].CityId == cityId && Journey.IsDone(pts[i].Id))
                    return true;
            return false;
        }
    }
}
