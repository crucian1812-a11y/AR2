using UnityEngine;

namespace Koenig
{
    // Родительский обход. До него точку можно было отметить кнопкой прямо
    // в списке заданий — без GPS, без метки, без поездки. Ребёнок проходил
    // всю игру за сорок секунд, не выходя из комнаты, и весь смысл «двух
    // ключей» держался на том, что кнопку не нажмут.
    //
    // Обход нужен по-настоящему: GPS в подвале не ловит, метка мокнет под
    // дождём, ребёнок стоит у башни и плачет. Но это инструмент взрослого,
    // и открываться он должен взрослому.
    //
    // Код придумывает сам родитель при первом входе — своего зашитого кода
    // здесь нет намеренно: зашитый секрет одинаков у всех, кто поставил
    // игру, и перестаёт быть секретом в тот день, когда его кто-то назовёт.
    //
    // Открытие живёт пять минут и гаснет само. Родитель отмечает, что
    // нужно, и телефон возвращается ребёнку уже запертым — без отдельного
    // «не забудь выйти».
    public static class ParentGate
    {
        private const string KeyPin = "koenig.parent.pin";
        private const float UnlockSeconds = 300f;

        public const int PinLength = 4;

        // Момент, до которого обход открыт. Ноль — заперт. Живёт только в
        // памяти: перезапуск игры запирает, и это правильно.
        private static float _until;

        public static bool HasPin
        {
            get { return PlayerPrefs.GetString(KeyPin, "").Length == PinLength; }
        }

        public static bool Unlocked
        {
            get { return _until > Time.unscaledTime; }
        }

        // Сколько минут осталось — для подписи на панели. Округляем вверх,
        // чтобы «0 мин» не висело целую минуту.
        public static int MinutesLeft
        {
            get
            {
                if (!Unlocked) return 0;
                return Mathf.CeilToInt((_until - Time.unscaledTime) / 60f);
            }
        }

        // Первая настройка: родитель задал код. Сразу и открываем — он уже
        // доказал, что он взрослый, второй раз вводить незачем.
        public static void SetPin(string pin)
        {
            if (pin == null || pin.Length != PinLength) return;
            PlayerPrefs.SetString(KeyPin, pin);
            PlayerPrefs.Save();
            Open();
        }

        public static bool TryUnlock(string pin)
        {
            if (pin == null || pin.Length != PinLength) return false;
            if (PlayerPrefs.GetString(KeyPin, "") != pin) return false;
            Open();
            return true;
        }

        public static void Lock() { _until = 0f; }

        private static void Open() { _until = Time.unscaledTime + UnlockSeconds; }

        // Сброс кода — на случай, если родитель его забыл. Зовётся только
        // из общего сброса прогресса, где и так всё стирается.
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(KeyPin);
            PlayerPrefs.Save();
            Lock();
        }
    }
}
