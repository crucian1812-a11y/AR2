using UnityEngine;

// Мелкая графика, создаваемая кодом. В репозитории нет ни одного
// импортированного спрайта — панели и полоски рисуются одним белым
// пикселем, который красится через цвет Image.
public static class Gfx
{
    private static Sprite _white;

    public static Sprite WhiteSprite()
    {
        if (_white != null) return _white;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        _white = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return _white;
    }
}
