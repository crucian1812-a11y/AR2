using System.Collections.Generic;
using UnityEngine;

// Подпись над объектом в мире. Рисуется HUD-ом как экранный текст
// в спроецированной позиции — это чётче и дешевле, чем 3D-текст.
public class WorldLabel : MonoBehaviour
{
    public static readonly List<WorldLabel> All = new List<WorldLabel>();

    public string Text = "";
    public Color Tint = Color.white;
    public Vector3 Offset = new Vector3(0f, 2.3f, 0f);
    public int FontSize = 22;
    public float MaxDistance = 45f;

    public static WorldLabel Attach(Transform parent, string text, Vector3 offset, Color tint, int fontSize)
    {
        WorldLabel l = parent.gameObject.AddComponent<WorldLabel>();
        l.Text = text;
        l.Offset = offset;
        l.Tint = tint;
        l.FontSize = fontSize;
        return l;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }
}
