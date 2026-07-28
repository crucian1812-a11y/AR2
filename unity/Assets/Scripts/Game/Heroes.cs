using UnityEngine;

// Каталог играбельных персонажей и моделей для врагов.
// Все модели — из пака Cute Animated Monsters (Quaternius, CC0),
// лежат в Assets/Resources/Models/monsters.
public static class Heroes
{
    // Первые три доступны сразу, остальные открываются в лавке.
    public const int FreeChars = 3;

    public static readonly string[] Ids =
    {
        "Panda", "Yeti", "GreenDemon",
        "Pig", "Chicken", "Penguin", "Deer", "Crab",
        "Mushroom", "Cactus", "Ghost", "Skull",
        "Cyclops", "Alien", "Alien_Tall", "Demon", "Tree",
        "Cat", "Dog", "Ninja", "Alpaking", "Armabee",
        "Mushnub_Evolved", "Alpaking_Evolved", "Armabee_Evolved",
        "Characters_Captain_Barbarossa", "Characters_Shark", "Dragon"
    };
    public static readonly string[] Names =
    {
        "Панда", "Йети", "Зелёный демон",
        "Поросёнок", "Цыплёнок", "Пингвин", "Олень", "Краб",
        "Грибок", "Кактус", "Призрак", "Череп",
        "Циклоп", "Пришелец", "Длинный пришелец", "Демон", "Древень",
        "Кот", "Пёс", "Ниндзя", "Альпака", "Пчелодав",
        "Грибогрыз", "Альпака-вожак", "Пчелиный страж",
        "Капитан Барбаросса", "Акула", "Дракон"
    };

    // Чем дальше по списку, тем дороже.
    public static int Price(int index)
    {
        index = Clamp(index);
        if (index < FreeChars) return 0;
        return 40 + (index - FreeChars) * 15;
    }

    // Все герои приводятся к одному росту, чтобы физика и камера
    // работали одинаково независимо от выбранной модели.
    public const float BodyHeight = 1.75f;

    public static int Clamp(int index)
    {
        if (index < 0) return 0;
        if (index >= Ids.Length) return Ids.Length - 1;
        return index;
    }

    public static string Id(int index)
    {
        return Ids[Clamp(index)];
    }

    public static string Name(int index)
    {
        return Names[Clamp(index)];
    }

    // Реквизит природы из пака Kenney Nature Kit (CC0).
    public static readonly string[] Trees =
        { "tree_default", "tree_oak", "tree_fat", "tree_detailed", "tree_tall", "tree_blocks" };
    public static readonly string[] TreesFall =
        { "tree_default_fall", "tree_oak_fall", "tree_fat_fall", "tree_detailed_fall" };
    public static readonly string[] Pines =
        { "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundA", "tree_pineTallA" };
    public static readonly string[] RocksLarge =
        { "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF" };
    public static readonly string[] RocksSmall =
        { "rock_smallA", "rock_smallB", "rock_smallC", "rock_smallD", "rock_smallE" };
    public static readonly string[] Bushes =
        { "plant_bush", "plant_bushDetailed", "plant_bushLarge", "plant_bushSmall" };

    public static string Pick(string[] set, int index)
    {
        if (set == null || set.Length == 0) return null;
        return set[Mathf.Abs(index) % set.Length];
    }

    // Модель и рост для каждого типа врага.
    public static string EnemyModel(string kind)
    {
        if (kind == "slime") return "Penguin";
        if (kind == "beetle") return "Cactus";
        if (kind == "bat") return "Bat";
        if (kind == "ghost") return "Ghost";
        if (kind == "skull") return "Skull";
        if (kind == "bee") return "Bee";
        if (kind == "crab") return "Crab";
        if (kind == "dragon") return "YellowDragon";
        if (kind == "cthulhu") return "Cthulhu";
        if (kind == "shark") return "Characters_Shark";
        if (kind == "pirate") return "Characters_Captain_Barbarossa";
        if (kind == "bigdragon") return "Dragon_Evolved";
        if (kind == "alpaking") return "Alpaking_Evolved";
        if (kind == "armabee") return "Armabee_Evolved";
        return "Mushroom";
    }

    public static float EnemyHeight(string kind)
    {
        if (kind == "bat") return 1.1f;
        if (kind == "bee") return 0.9f;
        if (kind == "beetle") return 1.7f;
        if (kind == "ghost") return 1.5f;
        if (kind == "skull") return 1.5f;
        if (kind == "crab") return 1.2f;
        if (kind == "dragon") return 2.6f;
        if (kind == "cthulhu") return 2.8f;
        if (kind == "shark") return 2.4f;
        if (kind == "pirate") return 2.2f;
        if (kind == "bigdragon") return 3.2f;
        if (kind == "alpaking") return 2.6f;
        if (kind == "armabee") return 2.2f;
        return 1.35f;
    }

    // Летающие держатся над землёй и не «шагают».
    public static bool Flies(string kind)
    {
        return kind == "bat" || kind == "bee" || kind == "ghost" ||
               kind == "dragon" || kind == "cthulhu" ||
               kind == "bigdragon" || kind == "armabee";
    }
}
