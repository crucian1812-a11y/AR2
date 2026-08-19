using UnityEngine;

// Пояс на экране: полотно цвета разряда, тёмная нашивная полоса и белые
// полоски по числу заработанных.
//
// Рисуется ровно так же, как на самом бойце (см. tools/blender/texture.py и
// BjjCloth). Совпадение здесь не косметика: игрок сверяет пояс в меню с тем,
// что видит на татами, и любое расхождение читалось бы как ошибка.
public static class BeltWidget
{
    public static void Draw(Transform root, Vector2 center, float width, Belt belt, int stripes)
    {
        float s = UiKit.Scale;
        float bh = 30f * s;

        UiKit.MakePanel(root, center + new Vector2(0f, -3f * s),
                        new Vector2(width + 8f * s, bh + 8f * s), new Color(0f, 0f, 0f, 0.5f));
        UiKit.MakePanel(root, center, new Vector2(width, bh), Career.BeltColor(belt));

        // Полоса на конце — тёмная на любом поясе.
        float barW = width * 0.28f;
        float barX = center.x + width * 0.5f - barW * 0.5f - 6f * s;
        UiKit.MakePanel(root, new Vector2(barX, center.y), new Vector2(barW, bh),
                        new Color(0.07f, 0.07f, 0.08f, 1f));

        for (int i = 0; i < stripes; i++)
        {
            float x = barX - barW * 0.5f + 12f * s + i * 15f * s;
            UiKit.MakePanel(root, new Vector2(x, center.y), new Vector2(7f * s, bh - 8f * s),
                            new Color(0.95f, 0.94f, 0.9f));
        }
    }
}
