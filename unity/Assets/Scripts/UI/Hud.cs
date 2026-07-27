using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Игровой интерфейс: монеты, жизни, задание, диалоги, экран победы,
// подписи объектов в мире и сенсорное управление.
public class Hud : MonoBehaviour
{
    public static Hud I;

    private Canvas _canvas;
    private Text _coins;
    private Text _hearts;
    private Text _quest;
    private Text _info;
    private Image _dialogPanel;
    private Text _dialogText;
    private Image _loading;
    private Text _loadingText;
    private Image _victoryOverlay;
    private Text _victoryTitle;
    private Text _victoryText;
    private Button _menuButton;

    private readonly List<Text> _labelPool = new List<Text>();
    private float _dialogTimer;
    private int _lastW, _lastH;

    public static Hud Create()
    {
        GameObject go = new GameObject("Hud");
        Hud hud = go.AddComponent<Hud>();
        hud.Build();
        I = hud;
        return hud;
    }

    private void Build()
    {
        _canvas = UiKit.CreateCanvas("HudCanvas", 10);
        _canvas.transform.SetParent(transform, false);
        float s = UiKit.Scale;
        Transform c = _canvas.transform;

        _coins = UiKit.MakeText(c, Vector2.zero, new Vector2(400f * s, 46f * s), "Монеты: 0",
            Mathf.RoundToInt(30f * s), new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleLeft);
        _hearts = UiKit.MakeText(c, Vector2.zero, new Vector2(400f * s, 40f * s), "Жизни: 3",
            Mathf.RoundToInt(25f * s), new Color(1f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
        _quest = UiKit.MakeText(c, Vector2.zero, new Vector2(700f * s, 60f * s), "",
            Mathf.RoundToInt(21f * s), Color.white, TextAnchor.UpperCenter);
        _info = UiKit.MakeText(c, Vector2.zero, new Vector2(420f * s, 70f * s), "",
            Mathf.RoundToInt(18f * s), new Color(0.85f, 0.9f, 1f), TextAnchor.UpperRight);

        _dialogPanel = UiKit.MakePanel(c, Vector2.zero, new Vector2(660f * s, 90f * s),
            new Color(0.06f, 0.09f, 0.16f, 0.88f));
        _dialogText = UiKit.MakeText(_dialogPanel.transform, Vector2.zero, new Vector2(620f * s, 80f * s),
            "", Mathf.RoundToInt(21f * s), Color.white, TextAnchor.MiddleCenter);
        RectTransform drt = _dialogText.rectTransform;
        drt.anchorMin = new Vector2(0.5f, 0.5f);
        drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = Vector2.zero;
        _dialogPanel.gameObject.SetActive(false);

        _menuButton = UiKit.MakeButton(c, Vector2.zero, new Vector2(110f * s, 44f * s), "Меню",
            Mathf.RoundToInt(20f * s), OnMenu);

        _loading = UiKit.MakePanel(c, Vector2.zero, new Vector2(4000f, 4000f),
            new Color(0.06f, 0.08f, 0.14f, 0.92f));
        _loadingText = UiKit.MakeText(_loading.transform, Vector2.zero, new Vector2(600f * s, 60f * s),
            "Подключение...", Mathf.RoundToInt(32f * s), Color.white, TextAnchor.MiddleCenter);
        RectTransform lrt = _loadingText.rectTransform;
        lrt.anchorMin = new Vector2(0.5f, 0.5f);
        lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        _loading.gameObject.SetActive(false);

        _victoryOverlay = UiKit.MakePanel(c, Vector2.zero, new Vector2(4000f, 4000f),
            new Color(0.05f, 0.05f, 0.12f, 0.7f));
        _victoryTitle = UiKit.MakeText(_victoryOverlay.transform, Vector2.zero,
            new Vector2(800f * s, 90f * s), "ПОБЕДА!", Mathf.RoundToInt(64f * s),
            new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter);
        _victoryText = UiKit.MakeText(_victoryOverlay.transform, Vector2.zero,
            new Vector2(800f * s, 90f * s), "Сердце горы найдено!\nВозвращаемся в деревню...",
            Mathf.RoundToInt(26f * s), Color.white, TextAnchor.UpperCenter);
        _victoryOverlay.gameObject.SetActive(false);

        TouchControls.Create(c);
        Layout();

        NetManager net = NetManager.I;
        if (net != null)
        {
            net.OnVictory += ShowVictory;
            net.OnWorldChanged += OnWorldChanged;
            net.OnQuestChanged += OnQuestChanged;
        }
    }

    private void OnDestroy()
    {
        NetManager net = NetManager.I;
        if (net != null)
        {
            net.OnVictory -= ShowVictory;
            net.OnWorldChanged -= OnWorldChanged;
            net.OnQuestChanged -= OnQuestChanged;
        }
        if (I == this) I = null;
    }

    private void Layout()
    {
        float s = UiKit.Scale;
        float w = Screen.width;
        float h = Screen.height;
        _lastW = Screen.width;
        _lastH = Screen.height;

        _coins.rectTransform.anchoredPosition = new Vector2(20f * s + 200f * s, h - 30f * s);
        _hearts.rectTransform.anchoredPosition = new Vector2(20f * s + 200f * s, h - 70f * s);
        _quest.rectTransform.anchoredPosition = new Vector2(w * 0.5f, h - 26f * s);
        _info.rectTransform.anchoredPosition = new Vector2(w - 220f * s, h - 100f * s);
        _menuButton.image.rectTransform.anchoredPosition = new Vector2(w - 70f * s, h - 30f * s);
        _dialogPanel.rectTransform.anchoredPosition = new Vector2(w * 0.5f, 120f * s);
        _loading.rectTransform.anchoredPosition = new Vector2(w * 0.5f, h * 0.5f);
        _victoryOverlay.rectTransform.anchoredPosition = new Vector2(w * 0.5f, h * 0.5f);
        _victoryTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _victoryTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _victoryTitle.rectTransform.anchoredPosition = new Vector2(0f, 40f * s);
        _victoryText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _victoryText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _victoryText.rectTransform.anchoredPosition = new Vector2(0f, -50f * s);
    }

    private void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH) Layout();

        NetManager net = NetManager.I;
        if (net == null) return;

        _coins.text = "Монеты: " + net.CoinsTotal;

        GameRoot root = GameRoot.I;
        if (root != null && root.LocalPlayer != null)
            _hearts.text = "Жизни: " + root.LocalPlayer.Hearts;

        _quest.text = QuestText(net);

        string info = "Игроки: " + Mathf.Max(net.Players.Count, 1);
        if (net.Online && net.IsHost) info += "\nВаш IP: " + net.GetLocalIpText();
        else if (net.Online) info += "\n(клиент)";
        _info.text = info;

        if (_dialogTimer > 0f)
        {
            _dialogTimer -= Time.deltaTime;
            if (_dialogTimer <= 0f) HideDialog();
        }

        DrawWorldLabels();
    }

    private static string QuestText(NetManager net)
    {
        if (net.VictoryReached) return "Победа! Сердце горы найдено!";
        if (net.QuestStage >= 4)
            return "Кристальная пещера открыта — доберитесь до золотой звезды";
        if (net.QuestStage == 3)
            return "Черепахоград всплыл! Монет до пещеры: " +
                   net.CoinsTotal + " / " + NetManager.QuestCoinsFinal;
        if (net.QuestStage == 2)
            return "Открыты каньон и вершины. Монет до Черепахограда: " +
                   net.CoinsTotal + " / " + NetManager.QuestCoinsCity;
        if (net.QuestStage == 1)
            return "Задание: соберите монеты (" + net.CoinsTotal + " / " + NetManager.QuestCoins + ")";
        return "Подойдите к старейшине в деревне";
    }

    // ---------- Подписи объектов в мире ----------

    private void DrawWorldLabels()
    {
        Camera cam = Camera.main;
        int used = 0;
        if (cam != null)
        {
            for (int i = 0; i < WorldLabel.All.Count; i++)
            {
                WorldLabel wl = WorldLabel.All[i];
                if (wl == null || string.IsNullOrEmpty(wl.Text)) continue;

                Vector3 world = wl.transform.position + wl.Offset;
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0.5f) continue;
                float dist = Vector3.Distance(cam.transform.position, world);
                if (dist > wl.MaxDistance) continue;

                Text t = GetLabel(used++);
                t.gameObject.SetActive(true);
                t.text = wl.Text;
                float fade = Mathf.Clamp01(1f - (dist / wl.MaxDistance) * 0.7f);
                t.color = new Color(wl.Tint.r, wl.Tint.g, wl.Tint.b, fade);
                t.fontSize = Mathf.Max(9, Mathf.RoundToInt(wl.FontSize * UiKit.Scale * Mathf.Clamp(12f / dist, 0.45f, 1.4f)));
                t.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y);
            }
        }
        for (int i = used; i < _labelPool.Count; i++)
            _labelPool[i].gameObject.SetActive(false);
    }

    private Text GetLabel(int index)
    {
        while (_labelPool.Count <= index)
        {
            Text t = UiKit.MakeText(_canvas.transform, Vector2.zero, new Vector2(320f, 40f),
                "", 20, Color.white, TextAnchor.MiddleCenter);
            _labelPool.Add(t);
        }
        return _labelPool[index];
    }

    // ---------- Публичные действия ----------

    public void ShowDialog(string text)
    {
        _dialogText.text = text;
        _dialogPanel.gameObject.SetActive(true);
        _dialogTimer = 6f;
    }

    public void HideDialog()
    {
        _dialogPanel.gameObject.SetActive(false);
        _dialogTimer = 0f;
    }

    public void ShowLoading(bool v)
    {
        _loading.gameObject.SetActive(v);
    }

    private void ShowVictory()
    {
        Snd.Play("victory");
        _victoryOverlay.gameObject.SetActive(true);
    }

    private void OnWorldChanged(int world)
    {
        _victoryOverlay.gameObject.SetActive(false);
        HideDialog();
        ShowLoading(false);
    }

    private void OnQuestChanged(int stage)
    {
        Snd.Play("quest");
    }

    private void OnMenu()
    {
        Snd.Play("click");
        if (NetManager.I != null) NetManager.I.LeaveToMenu("");
    }
}
