using UnityEngine;

// Добываемые материалы. Их роняет разрушаемый реквизит: деревья, камни,
// рудные жилы. Тратятся у торговки на верстаке и на строительные блоки.
//
// Список намеренно короткий: на телефоне полноценный инвентарь мучителен,
// а четырёх материалов хватает, чтобы рецепты были осмысленным выбором.
public static class Res
{
    public const int Wood = 0;
    public const int Stone = 1;
    public const int Iron = 2;
    public const int Crystal = 3;
    public const int Count = 4;

    public static readonly string[] Names = { "Дерево", "Камень", "Железо", "Кристалл" };
    // Ключи для сохранения — короткие и стабильные, менять нельзя.
    public static readonly string[] Keys = { "wood", "stone", "iron", "crys" };

    public static readonly Color[] Tints =
    {
        new Color(0.55f, 0.38f, 0.22f),
        new Color(0.62f, 0.60f, 0.56f),
        new Color(0.78f, 0.72f, 0.62f),
        new Color(0.55f, 0.85f, 1f)
    };

    public static string Name(int kind)
    {
        return kind >= 0 && kind < Count ? Names[kind] : "?";
    }

    public static Color Tint(int kind)
    {
        return kind >= 0 && kind < Count ? Tints[kind] : Color.white;
    }

    public static int KindByKey(string key)
    {
        for (int i = 0; i < Count; i++)
            if (Keys[i] == key) return i;
        return -1;
    }
}
