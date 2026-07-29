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
    // Верстак: рецепты из добытого. Списком, а не сеткой 3x3 — на телефоне
    // перетаскивать по клеткам мучительно, а выбор из списка честный.
    private Button[] _craftBtn;
    private Text _craftTitle;
    private int _lastW, _lastH;

    // Рецепт: что стоит и что даёт.
    private struct Recipe
    {
        public string Title;
        public int[] Cost;      // по индексам Res
        public int GiveCoins;
        public int GiveHeart;   // +1 к пределу здоровья
        public int GiveRes;     // вид материала
        public int GiveResAmount;
    }

    private static Recipe[] Recipes
    {
        get
        {
            return new Recipe[]
            {
                Make("Доски: 4 дерева → 12 монет", C(4, 0, 0, 0), 12, 0, -1, 0),
                Make("Слиток: 3 камня + 2 железа → 26 монет", C(0, 3, 2, 0), 26, 0, -1, 0),
                Make("Сердце: 2 железа + 1 кристалл", C(0, 0, 2, 1), 0, 1, -1, 0),
                Make("Дробить камень: 1 камень → 3 дерева", C(0, 1, 0, 0), 0, 0, Res.Wood, 3)
            };
        }
    }

    private static int[] C(int wood, int stone, int iron, int crystal)
    {
        return new int[] { wood, stone, iron, crystal };
    }

    private static Recipe Make(string title, int[] cost, int coins, int heart,
        int giveRes, int giveAmount)
    {
        Recipe r;
        r.Title = title;
        r.Cost = cost;
        r.GiveCoins = coins;
        r.GiveHeart = heart;
        r.GiveRes = giveRes;
        r.GiveResAmount = giveAmount;
        return r;
    }

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

        _panel = UiKit.MakePanel(c, Vector2.zero, new Vector2(600f * s, 690f * s),
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

        _craftTitle = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 32f * s),
            "Верстак", Mathf.RoundToInt(22f * s),
            new Color(0.8f, 0.92f, 0.7f), TextAnchor.MiddleCenter);
        Recipe[] rs = Recipes;
        _craftBtn = new Button[rs.Length];
        for (int i = 0; i < rs.Length; i++)
        {
            int idx = i;
            _craftBtn[i] = UiKit.MakeButton(c, Vector2.zero, new Vector2(500f * s, 46f * s), "",
                Mathf.RoundToInt(18f * s), delegate { Craft(idx); });
        }

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
        _title.rectTransform.anchoredPosition = new Vector2(cx, cy + 300f * s);
        _wallet.rectTransform.anchoredPosition = new Vector2(cx, cy + 262f * s);
        _heartBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 210f * s);
        _charBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 152f * s);

        _prevBtn.image.rectTransform.anchoredPosition = new Vector2(cx - 200f * s, cy + 94f * s);
        _wearLabel.rectTransform.anchoredPosition = new Vector2(cx, cy + 94f * s);
        _nextBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 200f * s, cy + 94f * s);

        _craftTitle.rectTransform.anchoredPosition = new Vector2(cx, cy + 36f * s);
        for (int i = 0; i < _craftBtn.Length; i++)
            _craftBtn[i].image.rectTransform.anchoredPosition =
                new Vector2(cx, cy - 8f * s - i * 50f * s);

        _note.rectTransform.anchoredPosition = new Vector2(cx, cy - 232f * s);
        _closeBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 300f * s);
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

        Recipe[] rs = Recipes;
        for (int i = 0; i < _craftBtn.Length && i < rs.Length; i++)
        {
            bool can = true;
            for (int k = 0; k < Res.Count; k++)
                if (net.ResCount(k) < rs[i].Cost[k]) can = false;
            SetButton(_craftBtn[i], rs[i].Title + (can ? "" : "  (не хватает)"), can);
        }

        _note.text = "Звёзды открывают миры, монеты тратятся здесь.\n" +
                     "Материал добывается ударом по камням и деревьям.";
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

    private void Craft(int index)
    {
        NetManager net = NetManager.I;
        Recipe[] rs = Recipes;
        if (net == null || index < 0 || index >= rs.Length) return;

        Recipe r = rs[index];
        if (r.GiveHeart > 0 && net.MaxHearts >= MaxHeartsCap)
        {
            _note.text = "Сердца больше не нужны — предел уже взят.";
            Snd.Play("hurt", 0.6f);
            return;
        }
        if (!net.SpendRes(r.Cost)) { Deny(); return; }

        if (r.GiveCoins > 0) net.AddCoins(r.GiveCoins);
        if (r.GiveRes >= 0) net.AddRes(r.GiveRes, r.GiveResAmount);
        if (r.GiveHeart > 0)
        {
            net.MaxHearts += r.GiveHeart;
            BearPlayer local = GameRoot.LocalBear;
            if (local != null) local.Hearts = net.MaxHearts;
        }

        net.SaveProgress();
        Achievements.Grant("firstcraft");
        Snd.Play("quest", 0.9f);
        Refresh();
    }

    private void Deny()
    {
        Snd.Play("hurt", 0.6f);
        _note.text = "Не хватает. Монеты рассыпаны по мирам,\n" +
                     "материал добывается ударом по камням и деревьям.";
    }
}
