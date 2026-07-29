using UnityEngine;
using UnityEngine.UI;

// Лавка торговки: тратим монеты на сердца и новых персонажей.
// Открывается при подходе к торговке, закрывается кнопкой.
public class ShopUI : MonoBehaviour
{
    public const int HeartPrice = 18;
    public const int MaxHeartsCap = 6;

    private Canvas _canvas;
    private Image _panel;
    private Text _title;
    private Text _wallet;
    private Button _heartBtn;
    private Button _charBtn;
    private Button _closeBtn;
    // Примерочная: купленное надо ещё и надеть, и делать это логично
    // здесь же, а не в главном меню.
    private Button _prevBtn;
    private Button _nextBtn;
    private Text _wearLabel;
    private Text _note;
    private int _lastW, _lastH;

    public static ShopUI Create(Transform parent)
    {
        GameObject go = new GameObject("ShopUI");
        ShopUI s = go.AddComponent<ShopUI>();
        go.transform.SetParent(parent, false);
        s.Build();
        return s;
    }

    private void Build()
    {
        _canvas = UiKit.CreateCanvas("ShopCanvas", 30);
        _canvas.transform.SetParent(transform, false);
        Transform c = _canvas.transform;
        float s = UiKit.Scale;

        _panel = UiKit.MakePanel(c, Vector2.zero, new Vector2(560f * s, 470f * s),
            new Color(0.1f, 0.13f, 0.2f, 0.95f));
        _title = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 44f * s),
            "Лавка торговки", Mathf.RoundToInt(30f * s),
            new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        _wallet = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 34f * s), "",
            Mathf.RoundToInt(21f * s), Color.white, TextAnchor.MiddleCenter);

        _heartBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(440f * s, 52f * s), "",
            Mathf.RoundToInt(21f * s), BuyHeart);
        _charBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(440f * s, 52f * s), "",
            Mathf.RoundToInt(21f * s), BuyChar);
        _prevBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(60f * s, 52f * s), "◀",
            Mathf.RoundToInt(24f * s), PrevChar);
        _wearLabel = UiKit.MakeText(c, Vector2.zero, new Vector2(300f * s, 52f * s), "",
            Mathf.RoundToInt(20f * s), Color.white, TextAnchor.MiddleCenter);
        _nextBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(60f * s, 52f * s), "▶",
            Mathf.RoundToInt(24f * s), NextChar);

        _note = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 60f * s), "",
            Mathf.RoundToInt(18f * s), new Color(0.75f, 0.82f, 0.95f), TextAnchor.UpperCenter);
        _closeBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(220f * s, 48f * s), "Закрыть",
            Mathf.RoundToInt(21f * s), Close);

        Layout();
        Refresh();
        gameObject.SetActive(false);
    }

    private void Layout()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        float s = UiKit.Scale;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        _panel.rectTransform.anchoredPosition = new Vector2(cx, cy);
        _title.rectTransform.anchoredPosition = new Vector2(cx, cy + 186f * s);
        _wallet.rectTransform.anchoredPosition = new Vector2(cx, cy + 146f * s);
        _heartBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 88f * s);
        _charBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 28f * s);

        _prevBtn.image.rectTransform.anchoredPosition = new Vector2(cx - 190f * s, cy - 34f * s);
        _wearLabel.rectTransform.anchoredPosition = new Vector2(cx, cy - 34f * s);
        _nextBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 190f * s, cy - 34f * s);

        _note.rectTransform.anchoredPosition = new Vector2(cx, cy - 86f * s);
        _closeBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 186f * s);
    }

    private void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH) Layout();
    }

    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        // В одиночной игре мир на паузе, пока открыта лавка: раньше медведь
        // бегал по джойстику под панелью. В сетевой игре время не трогаем —
        // остальные игроки продолжают играть.
        if (NetManager.I == null || !NetManager.I.Online) Time.timeScale = 0f;
        Ctrl.Reset();
        TouchControls.SetVisible(false);
        Refresh();
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        Snd.Play("click");
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    private void Refresh()
    {
        NetManager net = NetManager.I;
        if (net == null) return;

        _wallet.text = "Монет: " + net.CoinsTotal + "     Сердец: " + net.MaxHearts +
                       "     Героев: " + net.UnlockedChars + " / " + Heroes.Ids.Length;

        bool heartsMaxed = net.MaxHearts >= MaxHeartsCap;
        SetButton(_heartBtn, heartsMaxed
            ? "Сердца больше не нужны"
            : "Лишнее сердце — " + HeartPrice + " монет", !heartsMaxed);

        bool allChars = net.UnlockedChars >= Heroes.Ids.Length;
        SetButton(_charBtn, allChars
            ? "Все герои открыты"
            : "Открыть «" + Heroes.Name(net.UnlockedChars) + "» — " +
              Heroes.Price(net.UnlockedChars) + " монет", !allChars);

        // Примерочная. Листать можно только по купленным.
        int owned = Mathf.Clamp(net.UnlockedChars, 1, Heroes.Ids.Length);
        _wearLabel.text = "Надет: " + Heroes.Name(net.CharIndex) +
                          "\n<" + (net.CharIndex + 1) + " из " + owned + ">";
        bool canSwitch = owned > 1;
        SetButton(_prevBtn, "◀", canSwitch);
        SetButton(_nextBtn, "▶", canSwitch);

        _note.text = "Звёзды открывают миры, монеты тратятся здесь.\n" +
                     "Стрелками примеряй купленных героев — смена сразу.";
    }

    private void PrevChar() { Wear(-1); }
    private void NextChar() { Wear(1); }

    // Переодевание прямо в лавке. Раньше купить героя можно было здесь,
    // а надеть — только выйдя в главное меню и прощёлкав одной кнопкой
    // вперёд до нужного: после покупки игрок оставался в прежнем облике,
    // и ничто не подсказывало, где его сменить.
    private void Wear(int step)
    {
        NetManager net = NetManager.I;
        if (net == null) return;
        int owned = Mathf.Clamp(net.UnlockedChars, 1, Heroes.Ids.Length);
        if (owned <= 1) { Deny(); return; }

        net.CharIndex = ((net.CharIndex + step) % owned + owned) % owned;
        net.SaveProgress();
        Snd.Play("click");

        // Свою запись в списке игроков тоже правим — по ней собираются
        // модели для тех, кто подключится позже.
        PlayerInfo me;
        if (net.Players.TryGetValue(net.MyId, out me)) me.Char = net.CharIndex;

        BearPlayer local = GameRoot.LocalBear;
        if (local != null) local.SetCharacter(net.CharIndex);
        Refresh();
    }

    private static void SetButton(Button b, string label, bool enabled)
    {
        if (b == null) return;
        Text t = b.GetComponentInChildren<Text>();
        if (t != null) t.text = label;
        b.interactable = enabled;
    }

    private void BuyHeart()
    {
        NetManager net = NetManager.I;
        if (net == null || net.MaxHearts >= MaxHeartsCap) return;
        if (!net.Spend(HeartPrice)) { Deny(); return; }
        net.MaxHearts++;
        net.SaveProgress();
        Snd.Play("coin");
        BearPlayer local = GameRoot.LocalBear;
        if (local != null) local.Hearts = net.MaxHearts;
        Refresh();
    }

    private void BuyChar()
    {
        NetManager net = NetManager.I;
        if (net == null || net.UnlockedChars >= Heroes.Ids.Length) return;
        if (!net.Spend(Heroes.Price(net.UnlockedChars))) { Deny(); return; }
        net.UnlockedChars++;
        net.SaveProgress();
        Snd.Play("coin");
        Refresh();
    }

    private void Deny()
    {
        Snd.Play("hurt", 0.6f);
        _note.text = "Не хватает монет.\nМонеты рассыпаны по всем мирам — поищи ещё.";
    }
}
