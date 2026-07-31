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
    private Image _walletIcon;
    private Button _heartBtn;
    private Image _heartIcon;
    private Button _charBtn;
    private Button _closeBtn;
    // Примерочная: купленное надо ещё и надеть, и делать это логично
    // здесь же, а не в главном меню.
    private Button _prevBtn;
    private Button _nextBtn;
    private Text _wearLabel;
    private Text _offerLabel;
    private Text _note;

    // Две живые витрины: слева надетый герой, справа тот, что в продаже.
    // Списком имён выбор был вслепую — из двадцати девяти названий
    // игроку не говорит ничего ни одно.
    private ShopPreview _wornView;
    private ShopPreview _offerView;
    private Image _wornFrame;
    private Image _offerFrame;
    private RawImage _wornRaw;
    private RawImage _offerRaw;

    // Верстак: рецепты из добытого. Списком, а не сеткой 3x3 — на телефоне
    // перетаскивать по клеткам мучительно, а выбор из списка честный.
    private Button[] _craftBtn;
    // Значки материалов в цене рецепта: по картинке цена читается сразу,
    // строку «3 камня + 2 железа» приходилось разбирать глазами.
    private Image[,] _craftIcon;
    private Text[,] _craftCount;
    private Text _craftTitle;
    private const int MaxCostIcons = 4;
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
            // Цена ушла из названия в значки слева от строки, поэтому
            // здесь осталось только то, что рецепт ДАЁТ.
            return new Recipe[]
            {
                Make("Доски → 12 монет", C(4, 0, 0, 0), 12, 0, -1, 0),
                Make("Слиток → 26 монет", C(0, 3, 2, 0), 26, 0, -1, 0),
                Make("Сердце: +1 к пределу", C(0, 0, 2, 1), 0, 1, -1, 0),
                Make("Дробить камень → 3 дерева", C(0, 1, 0, 0), 0, 0, Res.Wood, 3)
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

        _panel = UiKit.MakePanel(c, Vector2.zero, new Vector2(620f * s, 690f * s),
            new Color(0.1f, 0.13f, 0.2f, 0.95f));
        _title = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 40f * s),
            "Лавка торговки", Mathf.RoundToInt(28f * s),
            new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
        _walletIcon = UiKit.MakeImage(c, Vector2.zero, new Vector2(26f * s, 26f * s),
            Icons.CoinIcon, Color.white);
        _wallet = UiKit.MakeText(c, Vector2.zero, new Vector2(480f * s, 30f * s), "",
            Mathf.RoundToInt(19f * s), Color.white, TextAnchor.MiddleLeft);

        // Витрины. Рамка — просто подложка чуть больше картинки, чтобы
        // модель не висела в пустоте панели.
        _wornView = ShopPreview.Create(transform, 0);
        _offerView = ShopPreview.Create(transform, 1);
        _wornFrame = UiKit.MakePanel(c, Vector2.zero, new Vector2(140f * s, 156f * s),
            new Color(0.05f, 0.07f, 0.12f, 1f));
        _wornRaw = UiKit.MakeRaw(c, Vector2.zero, new Vector2(132f * s, 148f * s),
            _wornView.Texture);
        _offerFrame = UiKit.MakePanel(c, Vector2.zero, new Vector2(140f * s, 156f * s),
            new Color(0.05f, 0.07f, 0.12f, 1f));
        _offerRaw = UiKit.MakeRaw(c, Vector2.zero, new Vector2(132f * s, 148f * s),
            _offerView.Texture);

        _prevBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(38f * s, 50f * s), "◀",
            Mathf.RoundToInt(22f * s), PrevChar);
        _nextBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(38f * s, 50f * s), "▶",
            Mathf.RoundToInt(22f * s), NextChar);
        _wearLabel = UiKit.MakeText(c, Vector2.zero, new Vector2(200f * s, 48f * s), "",
            Mathf.RoundToInt(17f * s), Color.white, TextAnchor.UpperCenter);
        _offerLabel = UiKit.MakeText(c, Vector2.zero, new Vector2(200f * s, 48f * s), "",
            Mathf.RoundToInt(17f * s), new Color(0.85f, 0.92f, 1f), TextAnchor.UpperCenter);

        _charBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 46f * s), "",
            Mathf.RoundToInt(19f * s), BuyChar);
        _heartBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 46f * s), "",
            Mathf.RoundToInt(19f * s), BuyHeart);
        _heartIcon = UiKit.MakeImage(c, Vector2.zero, new Vector2(26f * s, 26f * s),
            Icons.HeartIcon, Color.white);

        _craftTitle = UiKit.MakeText(c, Vector2.zero, new Vector2(520f * s, 28f * s),
            "Верстак", Mathf.RoundToInt(20f * s),
            new Color(0.8f, 0.92f, 0.7f), TextAnchor.MiddleCenter);
        Recipe[] rs = Recipes;
        _craftBtn = new Button[rs.Length];
        _craftIcon = new Image[rs.Length, MaxCostIcons];
        _craftCount = new Text[rs.Length, MaxCostIcons];
        for (int i = 0; i < rs.Length; i++)
        {
            int idx = i;
            _craftBtn[i] = UiKit.MakeButton(c, Vector2.zero, new Vector2(520f * s, 42f * s), "",
                Mathf.RoundToInt(17f * s), delegate { Craft(idx); });
            // Подпись кнопки сдвигаем вправо: слева встанет цена значками.
            Text label = _craftBtn[i].GetComponentInChildren<Text>();
            if (label != null)
            {
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.sizeDelta = new Vector2(300f * s, 42f * s);
                label.rectTransform.anchoredPosition = new Vector2(-10f * s, 0f);
            }
            // Значки создаются заранее на все четыре материала и прячутся
            // прозрачностью — так их не нужно пересоздавать при каждом
            // обновлении витрины.
            for (int k = 0; k < MaxCostIcons; k++)
            {
                _craftIcon[i, k] = UiKit.MakeImage(c, Vector2.zero,
                    new Vector2(22f * s, 22f * s), Icons.Material(k), Color.white);
                _craftCount[i, k] = UiKit.MakeText(c, Vector2.zero,
                    new Vector2(26f * s, 22f * s), "", Mathf.RoundToInt(15f * s),
                    Color.white, TextAnchor.MiddleLeft);
            }
        }

        _note = UiKit.MakeText(c, Vector2.zero, new Vector2(560f * s, 44f * s), "",
            Mathf.RoundToInt(16f * s), new Color(0.75f, 0.82f, 0.95f), TextAnchor.UpperCenter);
        _closeBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(200f * s, 40f * s), "Закрыть",
            Mathf.RoundToInt(19f * s), Close);

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
        _title.rectTransform.anchoredPosition = new Vector2(cx, cy + 302f * s);
        _walletIcon.rectTransform.anchoredPosition = new Vector2(cx - 250f * s, cy + 266f * s);
        _wallet.rectTransform.anchoredPosition = new Vector2(cx - 8f * s, cy + 266f * s);

        // Витрины по краям, стрелки примерочной — по бокам от левой.
        float wx = cx - 150f * s;
        float ox = cx + 150f * s;
        _wornFrame.rectTransform.anchoredPosition = new Vector2(wx, cy + 170f * s);
        _wornRaw.rectTransform.anchoredPosition = new Vector2(wx, cy + 170f * s);
        _offerFrame.rectTransform.anchoredPosition = new Vector2(ox, cy + 170f * s);
        _offerRaw.rectTransform.anchoredPosition = new Vector2(ox, cy + 170f * s);
        _prevBtn.image.rectTransform.anchoredPosition = new Vector2(wx - 92f * s, cy + 170f * s);
        _nextBtn.image.rectTransform.anchoredPosition = new Vector2(wx + 92f * s, cy + 170f * s);
        _wearLabel.rectTransform.anchoredPosition = new Vector2(wx, cy + 66f * s);
        _offerLabel.rectTransform.anchoredPosition = new Vector2(ox, cy + 66f * s);

        _charBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 16f * s);
        _heartBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 34f * s);
        _heartIcon.rectTransform.anchoredPosition = new Vector2(cx - 200f * s, cy - 34f * s);

        _craftTitle.rectTransform.anchoredPosition = new Vector2(cx, cy - 71f * s);
        for (int i = 0; i < _craftBtn.Length; i++)
        {
            float ry = cy - 106f * s - i * 42f * s;
            _craftBtn[i].image.rectTransform.anchoredPosition = new Vector2(cx, ry);
            for (int k = 0; k < MaxCostIcons; k++)
            {
                float kx = cx + 110f * s + k * 48f * s;
                _craftIcon[i, k].rectTransform.anchoredPosition = new Vector2(kx, ry);
                _craftCount[i, k].rectTransform.anchoredPosition = new Vector2(kx + 17f * s, ry);
            }
        }

        _note.rectTransform.anchoredPosition = new Vector2(cx, cy - 278f * s);
        _closeBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 322f * s);
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
        // Камеры витрин включаются только на время показа: две лишние
        // камеры, работающие весь уровень, телефону ни к чему.
        PreviewCameras(true);
        Refresh();
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        Snd.Play("click");
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
        PreviewCameras(false);
        gameObject.SetActive(false);
    }

    private void PreviewCameras(bool on)
    {
        if (_wornView != null) _wornView.SetActive(on);
        if (_offerView != null) _offerView.SetActive(on);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
        PreviewCameras(false);
    }

    public bool IsOpen { get { return gameObject.activeSelf; } }

    private void Refresh()
    {
        NetManager net = NetManager.I;
        if (net == null) return;

        _wallet.text = net.CoinsTotal + " монет      Сердец: " + net.MaxHearts +
                       "      Героев: " + net.UnlockedChars + " / " + Heroes.Ids.Length;

        bool heartsMaxed = net.MaxHearts >= MaxHeartsCap;
        SetButton(_heartBtn, heartsMaxed
            ? "     Сердца больше не нужны"
            : "     Лишнее сердце — " + HeartPrice + " монет", !heartsMaxed);

        bool allChars = net.UnlockedChars >= Heroes.Ids.Length;
        SetButton(_charBtn, allChars
            ? "Все герои открыты"
            : "Открыть «" + Heroes.Name(net.UnlockedChars) + "» — " +
              Heroes.Price(net.UnlockedChars) + " монет", !allChars);

        // Примерочная. Листать можно только по купленным.
        int owned = Mathf.Clamp(net.UnlockedChars, 1, Heroes.Ids.Length);
        _wearLabel.text = "Надет\n" + Heroes.Name(net.CharIndex) +
                          " (" + (net.CharIndex + 1) + " из " + owned + ")";
        bool canSwitch = owned > 1;
        SetButton(_prevBtn, "◀", canSwitch);
        SetButton(_nextBtn, "▶", canSwitch);

        // Витрины: слева надетый, справа очередной на продажу. Когда
        // куплены все, справа показываем последнего — пустая рамка
        // выглядела бы поломкой.
        int offer = allChars ? Heroes.Ids.Length - 1 : net.UnlockedChars;
        _offerLabel.text = (allChars ? "Открыт\n" : "В продаже\n") + Heroes.Name(offer) +
                           (allChars ? "" : " — " + Heroes.Price(offer) + " монет");
        _wornView.Show(net.CharIndex);
        _offerView.Show(offer);

        Recipe[] rs = Recipes;
        for (int i = 0; i < _craftBtn.Length && i < rs.Length; i++)
        {
            bool can = true;
            for (int k = 0; k < Res.Count; k++)
                if (net.ResCount(k) < rs[i].Cost[k]) can = false;
            SetButton(_craftBtn[i], rs[i].Title, can);

            // Цена — значками. Показываем только те материалы, что
            // действительно нужны, остальные прячем прозрачностью:
            // сдвигать значки по строке пришлось бы каждый раз заново.
            int slot = 0;
            for (int k = 0; k < Res.Count && k < MaxCostIcons; k++)
            {
                if (rs[i].Cost[k] <= 0) continue;
                _craftIcon[i, slot].sprite = Icons.Material(k);
                // Не хватает — значок гаснет, и видно, чего именно.
                bool have = net.ResCount(k) >= rs[i].Cost[k];
                _craftIcon[i, slot].color = have ? Color.white : new Color(1f, 0.5f, 0.5f, 0.5f);
                _craftCount[i, slot].text = "" + rs[i].Cost[k];
                _craftCount[i, slot].color = have ? Color.white : new Color(1f, 0.6f, 0.6f);
                slot++;
            }
            for (int k = slot; k < MaxCostIcons; k++)
            {
                _craftIcon[i, k].color = new Color(0f, 0f, 0f, 0f);
                _craftCount[i, k].text = "";
            }
        }

        _note.text = "Звёзды открывают миры, монеты тратятся здесь. " +
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
