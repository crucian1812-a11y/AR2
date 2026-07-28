using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Главное меню: имя игрока, одиночная игра, создание игры по Wi-Fi,
// поиск и подключение к играм в локальной сети.
public class MenuUI : MonoBehaviour
{
    private Canvas _canvas;
    private Image _bg;
    private Text _title;
    private Text _subtitle;
    private Text _status;
    private InputField _nameField;
    private Text _nameLabel;
    private InputField _ipField;
    private Text _searchLabel;
    private Button _soloBtn, _hostBtn, _joinBtn, _soundBtn, _connectBtn, _backBtn;
    private Button _resetBtn;
    // Стереть прогресс — в два касания: первое переспрашивает.
    private bool _resetArmed;
    private Button _charBtn;

    private readonly List<Button> _serverButtons = new List<Button>();
    private bool _joinMode;
    private int _lastW, _lastH;

    public static MenuUI Create()
    {
        GameObject go = new GameObject("MenuUI");
        MenuUI m = go.AddComponent<MenuUI>();
        m.Build();
        return m;
    }

    private void Build()
    {
        _canvas = UiKit.CreateCanvas("MenuCanvas", 20);
        _canvas.transform.SetParent(transform, false);
        Transform c = _canvas.transform;
        float s = UiKit.Scale;

        _bg = UiKit.MakePanel(c, Vector2.zero, new Vector2(4000f, 4000f), new Color(0.09f, 0.12f, 0.2f, 1f));

        _title = UiKit.MakeText(c, Vector2.zero, new Vector2(1000f * s, 70f * s),
            "МЕДВЕЖЬИ ПРИКЛЮЧЕНИЯ", Mathf.RoundToInt(48f * s),
            new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleCenter);
        _subtitle = UiKit.MakeText(c, Vector2.zero, new Vector2(1000f * s, 40f * s),
            "3D-платформер · мультиплеер по Wi-Fi", Mathf.RoundToInt(21f * s),
            new Color(0.7f, 0.78f, 0.9f), TextAnchor.MiddleCenter);

        _nameLabel = UiKit.MakeText(c, Vector2.zero, new Vector2(400f * s, 32f * s), "Ваше имя:",
            Mathf.RoundToInt(19f * s), new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleCenter);
        _nameField = UiKit.MakeInput(c, Vector2.zero, new Vector2(380f * s, 50f * s),
            NetManager.I != null ? NetManager.I.PlayerName : "Медведь", Mathf.RoundToInt(22f * s));

        _charBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 50f * s),
            CharButtonText(), Mathf.RoundToInt(21f * s), OnNextChar);

        _soloBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 54f * s), "Играть одному",
            Mathf.RoundToInt(23f * s), OnSolo);
        _hostBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 54f * s),
            "Создать игру (по Wi-Fi)", Mathf.RoundToInt(23f * s), OnHost);
        _joinBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 54f * s),
            "Присоединиться к игре", Mathf.RoundToInt(23f * s), OnJoinOpen);
        _soundBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 54f * s),
            Snd.Muted ? "Звук: выкл" : "Звук: вкл", Mathf.RoundToInt(23f * s), OnToggleSound);

        // Начать заново. Раньше стереть прогресс было нечем вообще:
        // SaveGame.Clear() существовал, но его никто не вызывал, и игра
        // навсегда продолжалась с последнего сохранения.
        _resetBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 46f * s),
            "Начать заново", Mathf.RoundToInt(20f * s), OnResetSave);

        _searchLabel = UiKit.MakeText(c, Vector2.zero, new Vector2(700f * s, 36f * s),
            "Поиск игр в вашей сети...", Mathf.RoundToInt(22f * s), Color.white, TextAnchor.MiddleCenter);
        _ipField = UiKit.MakeInput(c, Vector2.zero, new Vector2(300f * s, 48f * s), "",
            Mathf.RoundToInt(20f * s));
        _connectBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(180f * s, 48f * s), "Подключиться",
            Mathf.RoundToInt(20f * s), OnManualJoin);
        _backBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(380f * s, 50f * s), "Назад",
            Mathf.RoundToInt(22f * s), OnBack);

        _status = UiKit.MakeText(c, Vector2.zero, new Vector2(900f * s, 40f * s), "",
            Mathf.RoundToInt(19f * s), new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);

        NetManager net = NetManager.I;
        if (net != null)
        {
            _status.text = net.StatusMessage;
            net.StatusMessage = "";
            net.OnJoinFailed += OnJoinFailed;
            net.OnServerListUpdated += RefreshServers;
        }

        SetJoinMode(false);
        Layout();
    }

    private void OnDestroy()
    {
        NetManager net = NetManager.I;
        if (net != null)
        {
            net.OnJoinFailed -= OnJoinFailed;
            net.OnServerListUpdated -= RefreshServers;
            net.StopBrowse();
        }
    }

    private void Layout()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        float s = UiKit.Scale;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        _bg.rectTransform.anchoredPosition = new Vector2(cx, cy);
        _title.rectTransform.anchoredPosition = new Vector2(cx, cy + 250f * s);
        _subtitle.rectTransform.anchoredPosition = new Vector2(cx, cy + 200f * s);

        _nameLabel.rectTransform.anchoredPosition = new Vector2(cx, cy + 152f * s);
        _nameField.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 114f * s);
        _charBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy + 56f * s);
        _soloBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 4f * s);
        _hostBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 64f * s);
        _joinBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 124f * s);
        _soundBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 184f * s);
        _resetBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 238f * s);

        _searchLabel.rectTransform.anchoredPosition = new Vector2(cx, cy + 150f * s);
        _ipField.image.rectTransform.anchoredPosition = new Vector2(cx - 110f * s, cy - 110f * s);
        _connectBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 145f * s, cy - 110f * s);
        _backBtn.image.rectTransform.anchoredPosition = new Vector2(cx, cy - 175f * s);

        _status.rectTransform.anchoredPosition = new Vector2(cx, cy - 290f * s);
        LayoutServerButtons();
    }

    private void LayoutServerButtons()
    {
        float s = UiKit.Scale;
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        for (int i = 0; i < _serverButtons.Count; i++)
        {
            if (_serverButtons[i] == null) continue;
            _serverButtons[i].image.rectTransform.anchoredPosition =
                new Vector2(cx, cy + 100f * s - i * 56f * s);
        }
    }

    private void SetJoinMode(bool join)
    {
        _joinMode = join;
        _nameLabel.gameObject.SetActive(!join);
        _nameField.gameObject.SetActive(!join);
        _charBtn.gameObject.SetActive(!join);
        _soloBtn.gameObject.SetActive(!join);
        _hostBtn.gameObject.SetActive(!join);
        _joinBtn.gameObject.SetActive(!join);
        _soundBtn.gameObject.SetActive(!join);
        // Стирать нечего, пока нечего стирать.
        _resetBtn.gameObject.SetActive(!join && SaveGame.HasSave());

        _searchLabel.gameObject.SetActive(join);
        _ipField.gameObject.SetActive(join);
        _connectBtn.gameObject.SetActive(join);
        _backBtn.gameObject.SetActive(join);
        for (int i = 0; i < _serverButtons.Count; i++)
            if (_serverButtons[i] != null) _serverButtons[i].gameObject.SetActive(join);
    }

    private void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH) Layout();
    }

    // ---------- Действия ----------

    private void SaveName()
    {
        if (NetManager.I == null) return;
        string n = _nameField.text != null ? _nameField.text.Trim() : "";
        if (n.Length > 0) NetManager.I.PlayerName = n;
    }

    // Персонаж выбирается перебором: нажатие переключает на следующего.
    private static string CharButtonText()
    {
        int i = NetManager.I != null ? NetManager.I.CharIndex : 0;
        return "Персонаж: " + Heroes.Name(i) + "  \u25B6";
    }

    private void OnNextChar()
    {
        Snd.Play("click");
        NetManager net = NetManager.I;
        if (net == null) return;
        int limit = Mathf.Clamp(net.UnlockedChars, 1, Heroes.Ids.Length);
        net.CharIndex = (net.CharIndex + 1) % limit;
        Text label = _charBtn.GetComponentInChildren<Text>();
        if (label != null) label.text = CharButtonText();
    }

    private void OnSolo()
    {
        Snd.Play("click");
        SaveName();
        if (NetManager.I != null) NetManager.I.StartSolo();
    }

    private void OnHost()
    {
        Snd.Play("click");
        SaveName();
        if (NetManager.I != null && !NetManager.I.StartHost())
            _status.text = NetManager.I.StatusMessage;
    }

    private void OnJoinOpen()
    {
        Snd.Play("click");
        SaveName();
        _status.text = "";
        SetJoinMode(true);
        if (NetManager.I != null) NetManager.I.StartBrowse();
        RefreshServers();
    }

    private void OnBack()
    {
        Snd.Play("click");
        if (NetManager.I != null) NetManager.I.StopBrowse();
        SetJoinMode(false);
    }

    private void OnManualJoin()
    {
        Snd.Play("click");
        JoinTo(_ipField.text);
    }

    private void OnToggleSound()
    {
        Snd.SetMuted(!Snd.Muted);
        Snd.Play("click");
        Text label = _soundBtn.GetComponentInChildren<Text>();
        if (label != null) label.text = Snd.Muted ? "Звук: выкл" : "Звук: вкл";
    }

    private void OnResetSave()
    {
        Snd.Play("click");
        Text label = _resetBtn.GetComponentInChildren<Text>();

        if (!_resetArmed)
        {
            _resetArmed = true;
            if (label != null) label.text = "Точно стереть? Нажмите ещё раз";
            _status.text = "Пропадут монеты, звёзды, скины и открытые миры.";
            return;
        }

        _resetArmed = false;
        SaveGame.Clear();
        if (label != null) label.text = "Начать заново";
        _resetBtn.gameObject.SetActive(false);
        _status.text = "Прогресс стёрт.";
    }

    private void JoinTo(string ip)
    {
        NetManager net = NetManager.I;
        if (net == null) return;
        net.StopBrowse();
        if (net.StartJoin(ip)) _status.text = "Подключение к " + ip.Trim() + "...";
        else _status.text = net.StatusMessage;
    }

    private void OnJoinFailed(string message)
    {
        _status.text = message;
        SetJoinMode(true);
        if (NetManager.I != null) NetManager.I.StartBrowse();
    }

    private void RefreshServers()
    {
        for (int i = 0; i < _serverButtons.Count; i++)
            if (_serverButtons[i] != null) Object.Destroy(_serverButtons[i].gameObject);
        _serverButtons.Clear();

        NetManager net = NetManager.I;
        if (net == null) return;
        float s = UiKit.Scale;

        if (net.FoundServers.Count == 0)
        {
            _searchLabel.text = "Поиск игр в вашей сети...\n(хост должен создать игру)";
            LayoutServerButtons();
            return;
        }

        _searchLabel.text = "Найденные игры:";
        foreach (KeyValuePair<string, FoundServer> kv in net.FoundServers)
        {
            string ip = kv.Key;
            string label = kv.Value.Name + " — " + ip + " (игроков: " + kv.Value.Players + ")";
            Button b = UiKit.MakeButton(_canvas.transform, Vector2.zero,
                new Vector2(460f * s, 50f * s), label, Mathf.RoundToInt(19f * s), null);
            b.onClick.AddListener(delegate { JoinTo(ip); });
            b.gameObject.SetActive(_joinMode);
            _serverButtons.Add(b);
        }
        LayoutServerButtons();
    }
}
