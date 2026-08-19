using UnityEngine;

// Пояса — от белого к чёрному. Порядок важен: индекс пояса задаёт и
// сложность соперников, и место в списке.
public enum Belt { White, Blue, Purple, Brown, Black }

// Соперник на вечер: имя, склонность, пояс и то, насколько он хорош.
public struct Opponent
{
    public string Name;
    public Style Style;
    public Belt Belt;
    public float Difficulty;
    public string Note;      // одна строка о манере — чтобы было к чему готовиться
}

// Карьера: пояс, полоски, послужной список.
//
// Схватка сама по себе — эпизод. Причина провести вторую появляется только
// тогда, когда первая что-то меняет. Здесь меняет: за победу дают полоску,
// четыре полоски — новый пояс, а каждый следующий соперник заметно лучше
// предыдущего.
//
// Хранится всё в PlayerPrefs. Это единственное место в игре, которое
// переживает выход из приложения, и держать ради него файл или базу не за чем.
public static class Career
{
    public const int StripesPerBelt = 4;

    private const string KeyBelt = "bjj.belt";
    private const string KeyStripe = "bjj.stripe";
    private const string KeyWins = "bjj.wins";
    private const string KeyLosses = "bjj.losses";
    private const string KeySubs = "bjj.subs";
    private const string KeyStreak = "bjj.streak";
    private const string KeyBest = "bjj.best";

    public static Belt Belt { get; private set; }
    public static int Stripes { get; private set; }
    public static int Wins { get; private set; }
    public static int Losses { get; private set; }
    public static int Subs { get; private set; }        // побед приёмом
    public static int Streak { get; private set; }
    public static int BestStreak { get; private set; }

    /// Чёрный пояс с четырьмя полосками — карьера пройдена. Дальше идут
    /// вольные схватки с сильнейшими: расти уже некуда, а бороться есть с кем.
    public static bool Champion
    {
        get { return Belt == Belt.Black && Stripes >= StripesPerBelt; }
    }

    private static bool _loaded;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;

        Belt = (Belt)Mathf.Clamp(PlayerPrefs.GetInt(KeyBelt, 0), 0, (int)Belt.Black);
        Stripes = Mathf.Clamp(PlayerPrefs.GetInt(KeyStripe, 0), 0, StripesPerBelt);
        Wins = PlayerPrefs.GetInt(KeyWins, 0);
        Losses = PlayerPrefs.GetInt(KeyLosses, 0);
        Subs = PlayerPrefs.GetInt(KeySubs, 0);
        Streak = PlayerPrefs.GetInt(KeyStreak, 0);
        BestStreak = PlayerPrefs.GetInt(KeyBest, 0);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(KeyBelt, (int)Belt);
        PlayerPrefs.SetInt(KeyStripe, Stripes);
        PlayerPrefs.SetInt(KeyWins, Wins);
        PlayerPrefs.SetInt(KeyLosses, Losses);
        PlayerPrefs.SetInt(KeySubs, Subs);
        PlayerPrefs.SetInt(KeyStreak, Streak);
        PlayerPrefs.SetInt(KeyBest, BestStreak);
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        Belt = Belt.White;
        Stripes = 0;
        Wins = 0;
        Losses = 0;
        Subs = 0;
        Streak = 0;
        BestStreak = 0;
        Save();
    }

    /// Победа. Возвращает true, если это дало новый пояс, — экрану итога
    /// нужно знать, объявлять ли повышение.
    public static bool Win(bool bySubmission)
    {
        Load();
        Wins++;
        if (bySubmission) Subs++;
        Streak++;
        if (Streak > BestStreak) BestStreak = Streak;

        bool promoted = false;
        if (!Champion)
        {
            Stripes++;
            if (Stripes >= StripesPerBelt && Belt != Belt.Black)
            {
                Belt = (Belt)((int)Belt + 1);
                Stripes = 0;
                promoted = true;
            }
        }

        Save();
        return promoted;
    }

    public static void Lose()
    {
        Load();
        Losses++;
        Streak = 0;
        // Полоску не отнимаем. Проигрыш и так стоит вечера: тот же соперник
        // останется на месте, пока его не пройдёшь.
        Save();
    }

    // ------------------------------------------------------------ соперники

    // Двадцать имён — по четыре на пояс. Список нарочно жёсткий, а не
    // случайный: соперник должен запоминаться, а «третий фиолетовый» —
    // быть тем же самым, пока его не победишь.
    private static readonly string[] Names =
    {
        "Пауло", "Марсио", "Дэйв", "Тьягу",
        "Рафаэл", "Лукас", "Иван", "Диегу",
        "Бруну", "Жилберту", "Кайу", "Андре",
        "Фелипе", "Отавиу", "Витор", "Леандру",
        "Маркус", "Хикарду", "Жуан", "Эдуарду"
    };

    private static readonly Style[] Styles =
    {
        Style.Balanced, Style.Presser, Style.Guard, Style.Hunter,
        Style.Presser, Style.Guard, Style.Balanced, Style.Hunter,
        Style.Guard, Style.Hunter, Style.Presser, Style.Balanced,
        Style.Hunter, Style.Presser, Style.Guard, Style.Balanced,
        Style.Guard, Style.Hunter, Style.Presser, Style.Hunter
    };

    private static readonly string[] Notes =
    {
        "первый выход на татами",
        "давит весом, но быстро садит силы",
        "тянет в гвардию и ждёт ошибки",
        "лезет в приёмы даже без позиции",

        "проход в сайд — его любимый",
        "открытая гвардия, свипты с обеих сторон",
        "ровный: делает всё понемногу",
        "ищет спину при любом развороте",

        "не отдаёт гвардию ни за что",
        "треугольник ставит из ничего",
        "проходит и добирается до маунта",
        "экономный: тратится только наверняка",

        "сабмишн-охотник со стажем",
        "давит и не даёт вдохнуть",
        "гвардия как капкан",
        "делает всё и делает вовремя",

        "чёрный пояс, гвардия высшей школы",
        "финиширует, как только чувствует шею",
        "проход, маунт, спина — по накатанной",
        "берёт приёмом, не дожидаясь очков"
    };

    /// Кто следующий. Порядок жёсткий: пояс и полоска дают индекс в списке.
    public static Opponent Next()
    {
        Load();

        int index = (int)Belt * StripesPerBelt + Mathf.Min(Stripes, StripesPerBelt - 1);
        if (index >= Names.Length) index = Names.Length - 1;

        Opponent o;
        o.Name = Names[index];
        o.Style = Styles[index];
        o.Note = Notes[index];
        o.Belt = Belt;

        // Сложность растёт и от пояса, и от полоски: четвёртый соперник на
        // поясе ощутимо тяжелее первого, а первый на следующем — тяжелее его.
        o.Difficulty = Mathf.Clamp01(0.28f + (int)Belt * 0.155f + Mathf.Min(Stripes, 3) * 0.038f);

        // Чемпиону подбирают вольного соперника: сильнее любого в списке.
        if (Champion)
        {
            o.Difficulty = 1f;
            o.Note = "вольная схватка — сильнейший из зала";
        }

        return o;
    }

    // ------------------------------------------------------------ названия

    public static string BeltName(Belt b)
    {
        switch (b)
        {
            case Belt.Blue: return "синий пояс";
            case Belt.Purple: return "фиолетовый пояс";
            case Belt.Brown: return "коричневый пояс";
            case Belt.Black: return "чёрный пояс";
            default: return "белый пояс";
        }
    }

    public static Color BeltColor(Belt b)
    {
        switch (b)
        {
            case Belt.Blue: return new Color(0.15f, 0.30f, 0.72f);
            case Belt.Purple: return new Color(0.36f, 0.16f, 0.55f);
            case Belt.Brown: return new Color(0.29f, 0.17f, 0.09f);
            case Belt.Black: return new Color(0.07f, 0.07f, 0.08f);
            default: return new Color(0.92f, 0.90f, 0.86f);
        }
    }
}
