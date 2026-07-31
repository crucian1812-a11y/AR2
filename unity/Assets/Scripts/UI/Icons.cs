using UnityEngine;

// Пиксельные значки для лавки, нарисованные кодом.
//
// Раньше витрина была сплошным текстом: «3 камня + 2 железа» надо было
// читать и держать в голове. Значок узнаётся мгновенно и не требует
// перевода — а шестнадцать на шестнадцать взяты не случайно, это тот же
// размер, в котором нарисованы предметы в майнкрафте, откуда и мобы.
//
// Картинка задаётся строками по символу на пиксель: точка — прозрачно,
// D — обводка, M — тень, L — основной цвет, H — блик. Так рисунок видно
// прямо в исходнике и его можно править, не пересчитывая индексы.
public static class Icons
{
    public const int Size = 16;

    private static readonly string[] Heart =
    {
        "................",
        "...DD......DD...",
        "..DHHD....DHHD..",
        ".DHHLLD..DLLHHD.",
        ".DHLLLLDDLLLLHD.",
        ".DLLLLLLLLLLLLD.",
        ".DLLLLLLLLLLLLD.",
        "..DLLLLLLLLLLD..",
        "..DLLLLLLLLLLD..",
        "...DLLLLLLLLD...",
        "....DLLLLLLD....",
        ".....DLLLLD.....",
        "......DLLD......",
        ".......DD.......",
        "................",
        "................"
    };

    // Монета — чистый диск. Первая версия несла внутри две тёмные
    // засечки «для чеканки», и на витрине они читались не узором, а
    // парой глаз.
    private static readonly string[] Coin =
    {
        "................",
        "................",
        ".....DDDDDD.....",
        "...DDHHHHHHDD...",
        "..DHHHLLLLLLHD..",
        ".DHHLLLLLLLLLHD.",
        ".DHLLLLLLLLLLLD.",
        "DHLLLLLLLLLLLLHD",
        "DHLLLLLLLLLLLLHD",
        ".DHLLLLLLLLLLLD.",
        ".DMLLLLLLLLLLMD.",
        "..DMMLLLLLLMMD..",
        "...DDMMMMMMDD...",
        ".....DDDDDD.....",
        "................",
        "................"
    };

    // Дерево — торец с годовыми кольцами. Бок с корой при таком размере
    // рассыпался в шум, а кольца узнаются мгновенно.
    private static readonly string[] Log =
    {
        "................",
        "..MMMMMMMMMMMM..",
        "..MLLLLLLLLLLM..",
        "..MLLMMMMMMLLM..",
        "..MLMMLLLLMMLM..",
        "..MLMLLMMLLMLM..",
        "..MLMLMHHMLMLM..",
        "..MLMLMHHMLMLM..",
        "..MLMLLMMLLMLM..",
        "..MLMMLLLLMMLM..",
        "..MLLMMMMMMLLM..",
        "..MLLLLLLLLLLM..",
        "..MMMMMMMMMMMM..",
        "................",
        "................",
        "................"
    };

    // Булыжник: крупные камни со швами. Мелкая сетка из тёмных клеток
    // выглядела решёткой, а не камнем.
    private static readonly string[] Cobble =
    {
        "................",
        "................",
        ".DDDDDDDDDDDDDD.",
        ".DLLLLLDLLLLLLD.",
        ".DLLHLLDLLHHLLD.",
        ".DLLLLLDLLLLLLD.",
        ".DDDDDDDDDDDDDD.",
        ".DLLLLLLLDLLLLD.",
        ".DLHHLLLLDLLHLD.",
        ".DLLLLLLLDLLLLD.",
        ".DDDDDDDDDDDDDD.",
        ".DLLLLDLLLLLLLD.",
        ".DLLHLDLLHHLLLD.",
        ".DLLLLDLLLLLLLD.",
        ".DDDDDDDDDDDDDD.",
        "................"
    };

    private static readonly string[] Ingot =
    {
        "................",
        "................",
        "................",
        "....DDDDDDDD....",
        "...DHHHHHHHHD...",
        "..DHLLLLLLLLHD..",
        ".DHLLLLLLLLLLHD.",
        ".DLLLLLLLLLLLLD.",
        ".DLLLLLLLLLLLLD.",
        ".DMLLLLLLLLLLMD.",
        "..DMMMMMMMMMMD..",
        "...DDDDDDDDDD...",
        "................",
        "................",
        "................",
        "................"
    };

    private static readonly string[] Gem =
    {
        "................",
        "......DDDD......",
        ".....DHHHHD.....",
        "....DHHLLHHD....",
        "...DHLLLLLLHD...",
        "..DHLLLLLLLLHD..",
        ".DHLLLLLLLLLLHD.",
        ".DLLLLLLLLLLLLD.",
        ".DMLLLLLLLLLLMD.",
        "..DMLLLLLLLLMD..",
        "...DMLLLLLLMD...",
        "....DMLLLLMD....",
        ".....DMLLMD.....",
        "......DMMD......",
        ".......DD.......",
        "................"
    };

    private static Sprite _heart, _coin, _wood, _stone, _iron, _crystal;

    public static Sprite HeartIcon
    {
        get
        {
            if (_heart == null)
                _heart = Make(Heart, new Color(0.86f, 0.16f, 0.20f),
                    new Color(0.62f, 0.08f, 0.14f), new Color(0.30f, 0.03f, 0.06f),
                    new Color(1f, 0.46f, 0.46f));
            return _heart;
        }
    }

    public static Sprite CoinIcon
    {
        get
        {
            if (_coin == null)
                _coin = Make(Coin, new Color(1f, 0.80f, 0.18f),
                    new Color(0.78f, 0.55f, 0.06f), new Color(0.36f, 0.24f, 0.02f),
                    new Color(1f, 0.94f, 0.62f));
            return _coin;
        }
    }

    // Значок по индексу материала — порядок тот же, что в Res.
    public static Sprite Material(int res)
    {
        if (res == Res.Stone)
        {
            if (_stone == null)
                _stone = Make(Cobble, new Color(0.62f, 0.62f, 0.64f),
                    new Color(0.44f, 0.44f, 0.47f), new Color(0.22f, 0.22f, 0.25f),
                    new Color(0.78f, 0.78f, 0.80f));
            return _stone;
        }
        if (res == Res.Iron)
        {
            if (_iron == null)
                _iron = Make(Ingot, new Color(0.83f, 0.84f, 0.87f),
                    new Color(0.60f, 0.61f, 0.66f), new Color(0.28f, 0.29f, 0.34f),
                    new Color(0.98f, 0.99f, 1f));
            return _iron;
        }
        if (res == Res.Crystal)
        {
            if (_crystal == null)
                _crystal = Make(Gem, new Color(0.35f, 0.82f, 0.95f),
                    new Color(0.16f, 0.55f, 0.72f), new Color(0.06f, 0.24f, 0.36f),
                    new Color(0.78f, 0.98f, 1f));
            return _crystal;
        }
        if (_wood == null)
            _wood = Make(Log, new Color(0.60f, 0.42f, 0.23f),
                new Color(0.42f, 0.28f, 0.14f), new Color(0.22f, 0.14f, 0.06f),
                new Color(0.76f, 0.58f, 0.34f));
        return _wood;
    }

    // Собирает спрайт из строк. Фильтрация точечная: сглаживание
    // превратило бы пиксельный рисунок в мыло при увеличении до 40 точек.
    private static Sprite Make(string[] rows, Color light, Color mid, Color dark, Color hi)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < Size; y++)
        {
            // Строки записаны сверху вниз, а текстура растёт снизу вверх.
            string row = rows[Size - 1 - y];
            for (int x = 0; x < Size; x++)
            {
                char c = x < row.Length ? row[x] : '.';
                Color col = clear;
                if (c == 'D') col = dark;
                else if (c == 'M') col = mid;
                else if (c == 'L') col = light;
                else if (c == 'H') col = hi;
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f));
    }
}
