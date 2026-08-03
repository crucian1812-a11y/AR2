using UnityEngine;
using UnityEngine.UI;

// Помощники для сборки интерфейса кодом. Канвас работает в пикселях
// (ConstantPixelSize), поэтому координаты касаний совпадают с координатами UI.
public static class UiKit
{
    private static Font _font;

    public static float Scale
    {
        get { return Mathf.Clamp(Screen.height / 720f, 0.75f, 2.5f); }
    }

    public static Font UiFont
    {
        get
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return _font;
        }
    }

    public static Canvas CreateCanvas(string name, int sortOrder)
    {
        GameObject go = new GameObject(name);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        go.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private static RectTransform Rect(GameObject go, Transform parent)
    {
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        // Якорь в левом нижнем углу: координаты совпадают с экранными пикселями.
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    public static Text MakeText(Transform parent, Vector2 pos, Vector2 size, string content,
        int fontSize, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject("Text");
        RectTransform rt = Rect(go, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Text t = go.AddComponent<Text>();
        t.font = UiFont;
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Все прямоугольники ставятся по ЦЕНТРУ (pivot 0.5). Поэтому выключенный
    // влево текст, которому дали x левого края, уезжал за экран на полширины.
    // Эти помощники берут x как КРАЙ и сами сдвигают центр — левый и правый
    // столбцы строки больше не налезают друг на друга и не обрезаются.
    public static Text MakeTextLeft(Transform parent, float leftX, float centerY, Vector2 size,
        string content, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        return MakeText(parent, new Vector2(leftX + size.x * 0.5f, centerY), size, content,
            fontSize, color, anchor);
    }

    public static Text MakeTextRight(Transform parent, float rightX, float centerY, Vector2 size,
        string content, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleRight)
    {
        return MakeText(parent, new Vector2(rightX - size.x * 0.5f, centerY), size, content,
            fontSize, color, anchor);
    }

    public static Image MakeImage(Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        GameObject go = new GameObject("Image");
        RectTransform rt = Rect(go, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Картинка из RenderTexture: витрина лавки рисует героя живой
    // камерой, а Image умеет только спрайты — нужен именно RawImage.
    public static RawImage MakeRaw(Transform parent, Vector2 pos, Vector2 size, Texture tex)
    {
        GameObject go = new GameObject("Raw");
        RectTransform rt = Rect(go, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        RawImage img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    public static Image MakePanel(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        Image img = MakeImage(parent, pos, size, Gfx.WhiteSprite(), color);
        return img;
    }

    public static Button MakeButton(Transform parent, Vector2 pos, Vector2 size, string label,
        int fontSize, System.Action onClick)
    {
        GameObject go = new GameObject("Button");
        RectTransform rt = Rect(go, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.sprite = Gfx.WhiteSprite();
        bg.color = new Color(0.18f, 0.28f, 0.45f, 0.95f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.colorMultiplier = 1f;
        btn.colors = cb;
        // onClick приходит как System.Action, а UnityEvent ждёт UnityAction —
        // лямбда выполняет преобразование.
        if (onClick != null) btn.onClick.AddListener(delegate { onClick(); });

        Text t = MakeText(go.transform, Vector2.zero, size, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        RectTransform trt = t.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;

        return btn;
    }

    public static InputField MakeInput(Transform parent, Vector2 pos, Vector2 size, string value,
        int fontSize)
    {
        GameObject go = new GameObject("InputField");
        RectTransform rt = Rect(go, parent);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = go.AddComponent<Image>();
        bg.sprite = Gfx.WhiteSprite();
        bg.color = new Color(0.1f, 0.14f, 0.22f, 0.95f);

        Text text = MakeText(go.transform, Vector2.zero, size - new Vector2(20f, 8f), value,
            fontSize, Color.white, TextAnchor.MiddleCenter);
        RectTransform trt = text.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        text.raycastTarget = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        InputField field = go.AddComponent<InputField>();
        field.targetGraphic = bg;
        field.textComponent = text;
        field.text = value;
        field.characterLimit = 16;
        return field;
    }
}
