using UnityEngine;
using UnityEngine.UI;

// Интерфейс совместных забав: панель у костра и плашка поверх игры.
//
// Панель открывается подходом к костру и показывает три забавы. Кнопки
// живые только у хозяина: решает он один, иначе двое одновременно
// запустят разное. Остальным честно написано, чего ждать — молчаливая
// серая кнопка выглядит поломкой.
public class PartyUI : MonoBehaviour
{
    private Canvas _canvas;
    private Image _panel;
    private Text _title;
    private Text _hint;
    private Button _hideBtn;
    private Button _nightBtn;
    private Button _raceBtn;
    private Button _stopBtn;
    private Button _closeBtn;

    // Плашка идёт отдельным холстом: она видна всегда, пока забава идёт,
    // а панель — только у костра.
    private Canvas _bannerCanvas;
    private Image _bannerBg;
    private Text _bannerText;
    private Text _bannerTimer;
    // Полноэкранная заслонка водящему, пока остальные прячутся.
    private Image _blind;
    private Text _blindText;

    private int _lastW, _lastH;

    public static PartyUI Create(Transform parent)
    {
        GameObject go = new GameObject("PartyUI");
        go.transform.SetParent(parent, false);
        PartyUI p = go.AddComponent<PartyUI>();
        p.Build();
        return p;
    }

    private void Build()
    {
        float s = UiKit.Scale;

        // --- плашка ---
        _bannerCanvas = UiKit.CreateCanvas("PartyBanner", 26);
        _bannerCanvas.transform.SetParent(transform, false);
        Transform b = _bannerCanvas.transform;

        _blind = UiKit.MakePanel(b, Vector2.zero, new Vector2(4000f, 4000f),
            new Color(0.02f, 0.02f, 0.05f, 0.97f));
        _blindText = UiKit.MakeText(b, Vector2.zero, new Vector2(700f * s, 120f * s), "",
            Mathf.RoundToInt(34f * s), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        _bannerBg = UiKit.MakePanel(b, Vector2.zero, new Vector2(560f * s, 56f * s),
            new Color(0.08f, 0.11f, 0.18f, 0.86f));
        _bannerText = UiKit.MakeText(b, Vector2.zero, new Vector2(430f * s, 50f * s), "",
            Mathf.RoundToInt(20f * s), Color.white, TextAnchor.MiddleLeft);
        _bannerTimer = UiKit.MakeText(b, Vector2.zero, new Vector2(110f * s, 50f * s), "",
            Mathf.RoundToInt(24f * s), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleRight);

        // --- панель у костра ---
        _canvas = UiKit.CreateCanvas("PartyCanvas", 31);
        _canvas.transform.SetParent(transform, false);
        Transform c = _canvas.transform;

        _panel = UiKit.MakePanel(c, Vector2.zero, new Vector2(560f * s, 470f * s),
            new Color(0.1f, 0.13f, 0.2f, 0.95f));
        _title = UiKit.MakeText(c, Vector2.zero, new Vector2(500f * s, 40f * s),
            "Во что играем", Mathf.RoundToInt(28f * s),
            new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);

        _hideBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 54f * s),
            "Прятки", Mathf.RoundToInt(21f * s), StartHide);
        _nightBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 54f * s),
            "Ночь криперов", Mathf.RoundToInt(21f * s), StartNight);
        _raceBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 54f * s),
            "Гонка по кольцам", Mathf.RoundToInt(21f * s), StartRace);
        _stopBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(460f * s, 46f * s),
            "Закончить забаву", Mathf.RoundToInt(19f * s), StopMode);

        _hint = UiKit.MakeText(c, Vector2.zero, new Vector2(500f * s, 64f * s), "",
            Mathf.RoundToInt(16f * s), new Color(0.75f, 0.82f, 0.95f), TextAnchor.UpperCenter);
        _closeBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(200f * s, 44f * s),
            "Закрыть", Mathf.RoundToInt(19f * s), Close);

        Layout();
        _canvas.gameObject.SetActive(false);
        Refresh();
    }

    private void Layout()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        float s = UiKit.Scale;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        _panel.rectTransform.anchoredPosition = new Vector2(cx, cy);
        _title.rectTransform.anchoredPosition = new Vector2(cx, cy + 192f * s);
        _hideBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 128f * s);
        _nightBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 66f * s);
        _raceBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 4f * s);
        _stopBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 54f * s);
        _hint.rectTransform.anchoredPosition = new Vector2(cx, cy - 118f * s);
        _closeBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 200f * s);

        // Плашка вверху по центру, заслонка — во весь экран.
        float top = Screen.height - 52f * s;
        _bannerBg.rectTransform.anchoredPosition = new Vector2(cx, top);
        _bannerText.rectTransform.anchoredPosition = new Vector2(cx - 60f * s, top);
        _bannerTimer.rectTransform.anchoredPosition = new Vector2(cx + 210f * s, top);
        _blind.rectTransform.anchoredPosition = new Vector2(cx, cy);
        _blindText.rectTransform.anchoredPosition = new Vector2(cx, cy);
    }

    private void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH) Layout();
        Refresh();
    }

    public bool IsOpen { get { return _canvas != null && _canvas.gameObject.activeSelf; } }

    public void Open()
    {
        if (IsOpen) return;
        _canvas.gameObject.SetActive(true);
        if (NetManager.I == null || !NetManager.I.Online) Time.timeScale = 0f;
        Ctrl.Reset();
        TouchControls.SetVisible(false);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        Snd.Play("click");
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
        _canvas.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        TouchControls.SetVisible(true);
    }

    // ---------- Кнопки ----------

    private void StartHide() { Launch(NetManager.ModeHide); }
    private void StartNight() { Launch(NetManager.ModeNight); }
    private void StartRace() { Launch(NetManager.ModeRace); }

    private void Launch(int kind)
    {
        NetManager net = NetManager.I;
        if (net == null || !net.IsHost || Party.I == null) { Deny(); return; }
        if (!Party.Available(kind)) { Deny(); return; }
        Snd.Play("click");
        net.RequestMode(kind);
        Party.I.Begin(kind);
        Close();
    }

    private void StopMode()
    {
        NetManager net = NetManager.I;
        if (net == null || !net.IsHost || Party.I == null) { Deny(); return; }
        Snd.Play("click");
        Party.I.Stop();
        Refresh();
    }

    private void Deny()
    {
        Snd.Play("hurt", 0.6f);
    }

    // ---------- Обновление ----------

    private void Refresh()
    {
        NetManager net = NetManager.I;
        if (net == null) return;

        bool host = net.IsHost;
        bool running = Party.Running;
        int players = net.Players.Count;

        if (IsOpen)
        {
            SetBtn(_hideBtn, "Прятки" + (players < 2 ? "  (нужен второй игрок)" : ""),
                host && !running && players >= 2 && Party.Available(NetManager.ModeHide));
            SetBtn(_nightBtn, "Ночь криперов",
                host && !running && Party.Available(NetManager.ModeNight));
            SetBtn(_raceBtn, "Гонка по кольцам",
                host && !running && Party.Available(NetManager.ModeRace));
            SetBtn(_stopBtn, "Закончить забаву", host && running);

            if (!host)
                _hint.text = "Забаву выбирает хозяин игры — тот, кто создал комнату.";
            else if (running)
                _hint.text = "Сейчас идёт забава. Закончите её, чтобы начать другую.";
            else
                _hint.text = "Прятки — водящий ищет и осаливает.\n" +
                             "Ночь криперов — пять волн к фонтану.\n" +
                             "Гонка — семь колец по деревне.";
        }

        // --- плашка ---
        bool showBanner = running;
        _bannerBg.enabled = showBanner;
        _bannerText.enabled = showBanner;
        _bannerTimer.enabled = showBanner;

        if (showBanner)
        {
            string mine = MyLine(net);
            _bannerText.text = net.ModeText + (mine.Length > 0 ? "   " + mine : "");
            _bannerTimer.text = net.ModePhase == NetManager.PhaseRun &&
                                net.ModeKind == NetManager.ModeNight
                ? "♥ " + net.ModeLives
                : Mathf.Max(0, Mathf.CeilToInt(net.ModeTimer)) + " c";
        }

        // --- заслонка водящему ---
        bool blind = running && net.ModeKind == NetManager.ModeHide &&
                     net.ModePhase == NetManager.PhaseWarmup && IsSeeker(net);
        _blind.enabled = blind;
        _blindText.enabled = blind;
        if (blind)
            _blindText.text = "Ты водишь!\nЗакрой глаза и считай:  " +
                              Mathf.Max(0, Mathf.CeilToInt(net.ModeTimer));
    }

    private static bool IsSeeker(NetManager net)
    {
        PlayerInfo me;
        return net.Players.TryGetValue(net.MyId, out me) && me.Role != 0;
    }

    // Что забава значит лично для меня — без этой строки плашка сообщает
    // общий счёт, но не то, что делать.
    private static string MyLine(NetManager net)
    {
        PlayerInfo me;
        if (!net.Players.TryGetValue(net.MyId, out me)) return "";

        if (net.ModeKind == NetManager.ModeHide)
        {
            if (net.ModePhase == NetManager.PhaseOver) return "";
            return me.Role != 0 ? "— ты водишь" : "— ты прячешься";
        }
        if (net.ModeKind == NetManager.ModeRace)
        {
            if (me.Score >= net.ModeLives && net.ModeLives > 0)
                return me.Role > 0 ? "— финиш, место " + me.Role : "— финиш";
            return "— кольцо " + (me.Score + 1) + " из " + Mathf.Max(1, net.ModeLives);
        }
        return "";
    }

    private static void SetBtn(Button b, string label, bool on)
    {
        if (b == null) return;
        Text t = b.GetComponentInChildren<Text>();
        if (t != null) t.text = label;
        b.interactable = on;
    }
}
