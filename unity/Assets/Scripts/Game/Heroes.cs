using UnityEngine;

// Каталог играбельных персонажей и моделей для врагов.
// Все модели — из пака Cute Animated Monsters (Quaternius, CC0),
// лежат в Assets/Resources/Models/monsters.
public static class Heroes
{
    public static readonly string[] Ids = { "Panda", "Yeti", "GreenDemon" };
    public static readonly string[] Names = { "Панда", "Йети", "Демон" };

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

    // Модель и рост для каждого типа врага.
    public static string EnemyModel(string kind)
    {
        if (kind == "slime") return "Penguin";
        if (kind == "beetle") return "Cactus";
        if (kind == "bat") return "Bat";
        if (kind == "ghost") return "Ghost";
        if (kind == "skull") return "Skull";
        if (kind == "bee") return "Bee";
        return "Mushroom";
    }

    public static float EnemyHeight(string kind)
    {
        if (kind == "bat") return 1.1f;
        if (kind == "bee") return 0.9f;
        if (kind == "beetle") return 1.7f;
        if (kind == "ghost") return 1.5f;
        if (kind == "skull") return 1.5f;
        return 1.35f;
    }

    // Летающие держатся над землёй и не «шагают».
    public static bool Flies(string kind)
    {
        return kind == "bat" || kind == "bee" || kind == "ghost";
    }
}
