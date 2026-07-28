using UnityEngine;

// Одноразовые подсказки по управлению.
//
// Двойной прыжок, удар сверху и ныряние игра раньше не объясняла нигде:
// про них можно было узнать только случайно ткнув кнопку в нужный момент.
// Подсказка появляется ровно тогда, когда движение впервые становится
// нужным, и больше не повторяется — ни в этом запуске, ни в следующем.
public static class Tutor
{
    private const string Prefix = "bear.tip.";

    private static readonly string[] Keys =
    {
        "jump", "double", "pound", "swim", "star", "shop"
    };

    public static void Show(string key, string text)
    {
        if (PlayerPrefs.GetInt(Prefix + key, 0) != 0) return;
        Hud hud = Hud.I;
        if (hud == null) return;
        PlayerPrefs.SetInt(Prefix + key, 1);
        PlayerPrefs.Save();
        hud.ShowDialog(text);
    }

    // Новая игра начинается и с чистого обучения.
    public static void Reset()
    {
        for (int i = 0; i < Keys.Length; i++) PlayerPrefs.DeleteKey(Prefix + Keys[i]);
        PlayerPrefs.Save();
    }
}
