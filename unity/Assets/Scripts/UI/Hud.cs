using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Интерфейс схватки.
//
// Главное правило здесь — не мешать смотреть. Борьба идёт в центре кадра,
// поэтому середина экрана оставлена пустой: часы и силы сверху, приёмы
// внизу справа под большой палец, а всё, что появляется в центре, живёт
// секунду и уходит.
//
// Второе правило — цвет вместо подписей. Синий и красный закреплены за
// бойцами и в моделях, и в подсветке контура, и здесь; игроку не нужно
// читать, чья это полоска.
public class Hud : MonoBehaviour
{
    private Match _match;
    private Side _player;

    private Text _clock;
    private Text _scoreA;
    private Text _scoreB;
    private Text _position;
    private Text _toast;
    private Image _staminaA;
    private Image _staminaB;
    private Image _staminaAWarn;
    private Image _progress;
    private Image _flash;
    private Transform _buttonRoot;

    private readonly List<GameObject> _buttons = new List<GameObject>();
    private Pos _shownFor = (Pos)(-1);
    private Phase _shownPhase = (Phase)(-1);
    private string _lastEvent = "";

    private Image _lockFill;
    private Image _lockPanel;
    private Text _lockLabel;
    private Text _grips;

    private float _toastLife;
    private float _posPulse;
    private float _flashLife;

    private float _barW;
    private float _s;

    public static Hud Create(Transform parent, Match match, Side player)
    {
        Canvas canvas = UiKit.CreateCanvas("HudCanvas", 10);
        canvas.transform.SetParent(parent, false);

        Hud hud = canvas.gameObject.AddComponent<Hud>();
        hud._match = match;
        hud._player = player;
        hud.Build(canvas.transform);
        return hud;
    }

    private void Build(Transform root)
    {
        _s = UiKit.Scale;
        float w = Screen.width;
        float h = Screen.height;
        _barW = w * 0.26f;

        // Верхняя планка — тёмная подложка. Без неё белые цифры теряются
        // на светлом татами ровно в тот момент, когда камера подъезжает.
        UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 40f * _s),
                        new Vector2(w, 80f * _s), new Color(0f, 0f, 0f, 0.62f));

        _clock = UiKit.MakeText(root, new Vector2(w * 0.5f, h - 30f * _s),
                                new Vector2(300f * _s, 56f * _s), "5:00",
                                Mathf.RoundToInt(38f * _s), Color.white, TextAnchor.MiddleCenter);

        // Очки — крупными плашками по краям, цветом бойца.
        MakeScore(root, w * 0.5f - 118f * _s, h - 30f * _s, Arena.GiBlue, out _scoreA);
        MakeScore(root, w * 0.5f + 118f * _s, h - 30f * _s, Arena.GiRed, out _scoreB);

        MakeStamina(root, w * 0.5f - _barW * 0.5f - 132f * _s, h - 66f * _s,
                    Arena.GiBlue, false, out _staminaA, out _staminaAWarn);
        Image dummy;
        MakeStamina(root, w * 0.5f + _barW * 0.5f + 132f * _s, h - 66f * _s,
                    Arena.GiRed, true, out _staminaB, out dummy);

        // Название позиции. Самый важный текст на экране: игрок обязан
        // всегда понимать, где он находится.
        _position = UiKit.MakeText(root, new Vector2(w * 0.5f, h - 118f * _s),
                                   new Vector2(w * 0.9f, 46f * _s), "Стойка",
                                   Mathf.RoundToInt(29f * _s),
                                   new Color(1f, 0.97f, 0.9f), TextAnchor.MiddleCenter);

        // Полоска хода приёма.
        UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 150f * _s),
                        new Vector2(w * 0.42f, 6f * _s), new Color(0f, 0f, 0f, 0.55f));
        _progress = UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 150f * _s),
                                    new Vector2(0f, 6f * _s), new Color(1f, 0.86f, 0.42f, 1f));
        _progress.rectTransform.pivot = new Vector2(0f, 0.5f);
        _progress.rectTransform.anchoredPosition =
            new Vector2(w * 0.5f - w * 0.21f, h - 150f * _s);

        _toast = UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.30f),
                                new Vector2(w * 0.8f, 60f * _s), "",
                                Mathf.RoundToInt(30f * _s),
                                new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);

        // Вспышка на весь экран в момент приёма — короткая, почти
        // незаметная, но именно она делает событие «ударным».
        _flash = UiKit.MakePanel(root, new Vector2(w * 0.5f, h * 0.5f),
                                 new Vector2(w, h), new Color(1f, 1f, 1f, 0f));

        BuildLockBar(root, w, h);

        // Захваты — цифрами под полоской сил игрока. В борьбе за ги это
        // такой же ресурс, как силы, и он должен быть на виду.
        _grips = UiKit.MakeText(root, new Vector2(w * 0.5f - _barW * 0.5f - 132f * _s, h - 92f * _s),
                                new Vector2(_barW, 30f * _s), "",
                                Mathf.RoundToInt(19f * _s),
                                new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);

        GameObject holder = new GameObject("Moves");
        holder.transform.SetParent(root, false);
        _buttonRoot = holder.transform;
    }

    // Полоса перетягивания в сабмишне. Она появляется в центре экрана —
    // единственное, ради чего это правило нарушается: в этот момент
    // смотреть больше не на что, всё решается здесь.
    private void BuildLockBar(Transform root, float w, float h)
    {
        _lockPanel = UiKit.MakePanel(root, new Vector2(w * 0.5f, h * 0.52f),
                                     new Vector2(w * 0.56f, 26f * _s),
                                     new Color(0f, 0f, 0f, 0.75f));

        _lockFill = UiKit.MakePanel(root, new Vector2(w * 0.5f, h * 0.52f),
                                    new Vector2(0f, 22f * _s),
                                    new Color(0.85f, 0.18f, 0.16f, 0.95f));
        _lockFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        _lockFill.rectTransform.anchoredPosition =
            new Vector2(w * 0.5f - w * 0.28f, h * 0.52f);

        _lockLabel = UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.58f),
                                    new Vector2(w * 0.8f, 40f * _s), "",
                                    Mathf.RoundToInt(24f * _s),
                                    new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter);

        _lockPanel.gameObject.SetActive(false);
        _lockFill.gameObject.SetActive(false);
    }

    private void MakeScore(Transform root, float x, float y, Color color, out Text label)
    {
        UiKit.MakePanel(root, new Vector2(x, y), new Vector2(62f * _s, 46f * _s),
                        new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.95f));
        label = UiKit.MakeText(root, new Vector2(x, y), new Vector2(62f * _s, 46f * _s),
                               "0", Mathf.RoundToInt(30f * _s), Color.white, TextAnchor.MiddleCenter);
    }

    // Полоска сил с «тревожной» подложкой: когда сил мало, под полоской
    // проступает красный. Игрок должен видеть это, не читая цифр.
    private void MakeStamina(Transform root, float cx, float y, Color color, bool mirror,
                             out Image fill, out Image warn)
    {
        UiKit.MakePanel(root, new Vector2(cx, y), new Vector2(_barW + 4f, 20f * _s),
                        new Color(0f, 0f, 0f, 0.7f));

        warn = UiKit.MakePanel(root, new Vector2(cx, y), new Vector2(_barW, 16f * _s),
                               new Color(0.7f, 0.12f, 0.1f, 0f));

        fill = UiKit.MakePanel(root, new Vector2(cx, y), new Vector2(_barW, 16f * _s), color);
        // Полоски растут от центра экрана наружу — зеркально, как в
        // спортивных трансляциях.
        fill.rectTransform.pivot = new Vector2(mirror ? 0f : 1f, 0.5f);
        fill.rectTransform.anchoredPosition =
            new Vector2(cx + (mirror ? -_barW * 0.5f : _barW * 0.5f), y);
    }

    private void Update()
    {
        if (_match == null) return;

        float dt = Time.unscaledDeltaTime;

        int mm = Mathf.FloorToInt(_match.Clock / 60f);
        int ss = Mathf.FloorToInt(_match.Clock % 60f);
        _clock.text = mm + ":" + ss.ToString("00");
        // Последние тридцать секунд — красным.
        _clock.color = _match.Clock <= 30f
            ? new Color(1f, 0.42f, 0.36f)
            : Color.white;

        _scoreA.text = _match.ScoreA.ToString();
        _scoreB.text = _match.ScoreB.ToString();

        UpdateStamina();
        UpdatePosition(dt);
        UpdateToast(dt);
        UpdateFlash(dt);

        _progress.rectTransform.sizeDelta = new Vector2(
            _match.Busy ? Screen.width * 0.42f * _match.BusyProgress : 0f, 6f * _s);

        UpdateLock();
        UpdateGrips();

        if (_shownFor != _match.Position || _shownPhase != _match.Now)
        {
            _shownFor = _match.Position;
            _shownPhase = _match.Now;
            RebuildButtons();
        }
    }

    private void UpdateGrips()
    {
        int mine = _match.Grips(_player);
        int theirs = _match.Grips(Match.Other(_player));

        // Точками, а не числом: захваты считаются до двух, и точки
        // читаются быстрее цифры.
        string dots = "";
        for (int i = 0; i < Match.MaxGrips; i++) dots += i < mine ? "●" : "○";
        string theirDots = "";
        for (int i = 0; i < Match.MaxGrips; i++) theirDots += i < theirs ? "●" : "○";

        _grips.text = "захваты  " + dots + "   /   " + theirDots;
    }

    private void UpdateLock()
    {
        bool on = _match.Now == Phase.Submission;
        _lockPanel.gameObject.SetActive(on);
        _lockFill.gameObject.SetActive(on);

        if (!on)
        {
            _lockLabel.text = "";
            return;
        }

        _lockFill.rectTransform.sizeDelta =
            new Vector2(Screen.width * 0.56f * _match.Lock, 22f * _s);

        bool attacking = _match.Mover == _player;
        _lockLabel.text = attacking ? "ДОЖИМАЙ!" : "ВЫРЫВАЙСЯ!";
        _lockFill.color = attacking
            ? new Color(0.30f, 0.75f, 0.35f, 0.95f)
            : new Color(0.85f, 0.18f, 0.16f, 0.95f);
    }

    private void UpdateStamina()
    {
        float a = _match.StaminaA / Match.MaxStamina;
        float b = _match.StaminaB / Match.MaxStamina;

        _staminaA.rectTransform.sizeDelta = new Vector2(_barW * a, 16f * _s);
        _staminaB.rectTransform.sizeDelta = new Vector2(_barW * b, 16f * _s);

        // Цвет полоски игрока темнеет по мере усталости — заметнее, чем
        // просто убывающая длина.
        _staminaA.color = Color.Lerp(new Color(0.75f, 0.25f, 0.2f), Arena.GiBlue,
                                     Mathf.Clamp01(a * 1.6f));
        _staminaB.color = Color.Lerp(new Color(0.75f, 0.25f, 0.2f), Arena.GiRed,
                                     Mathf.Clamp01(b * 1.6f));

        float warn = a < 0.28f ? Mathf.PingPong(Time.unscaledTime * 2.2f, 0.45f) : 0f;
        Color wc = _staminaAWarn.color;
        wc.a = warn;
        _staminaAWarn.color = wc;
    }

    private void UpdatePosition(float dt)
    {
        _position.text = Positions.Name(_match.Position);

        if (_shownFor != _match.Position) _posPulse = 1f;
        _posPulse = Mathf.Max(0f, _posPulse - dt * 2.2f);

        // Смена позиции подчёркивается коротким увеличением: это главное
        // событие игры, и оно не должно проходить незамеченным.
        float scale = 1f + _posPulse * 0.28f;
        _position.rectTransform.localScale = new Vector3(scale, scale, 1f);
        _position.color = Color.Lerp(new Color(1f, 0.97f, 0.9f),
                                     new Color(1f, 0.85f, 0.35f), _posPulse);
    }

    private void UpdateToast(float dt)
    {
        if (_match.LastEvent != _lastEvent)
        {
            _lastEvent = _match.LastEvent;
            _toast.text = _lastEvent;
            _toastLife = 2.2f;
            _flashLife = 0.18f;
        }

        _toastLife = Mathf.Max(0f, _toastLife - dt);

        // Уходит вверх и растворяется — не мигает и не висит.
        float k = _toastLife / 2.2f;
        Color c = _toast.color;
        c.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.4f));
        _toast.color = c;
        _toast.rectTransform.anchoredPosition =
            new Vector2(Screen.width * 0.5f, Screen.height * 0.30f + (1f - k) * 26f * _s);
    }

    private void UpdateFlash(float dt)
    {
        _flashLife = Mathf.Max(0f, _flashLife - dt);
        Color c = _flash.color;
        c.a = _flashLife * 0.5f;
        _flash.color = c;
    }

    private void RebuildButtons()
    {
        for (int i = 0; i < _buttons.Count; i++) Destroy(_buttons[i]);
        _buttons.Clear();

        if (_match.Finished) return;

        // Набор кнопок зависит от фазы. Показывать список приёмов, пока
        // соперник проводит свой, было бы обманом: нажать всё равно
        // нельзя, а единственное осмысленное действие — сопротивляться.
        switch (_match.Now)
        {
            case Phase.Submission:
                BigButton(_match.Mover == _player ? "ДОЖАТЬ" : "ВЫРВАТЬСЯ",
                          _match.Mover == _player
                              ? new Color(0.20f, 0.52f, 0.24f, 0.95f)
                              : new Color(0.62f, 0.16f, 0.15f, 0.95f),
                          delegate { Struggle(); });
                return;

            case Phase.Move:
                if (_match.Mover != _player)
                {
                    BigButton("СОПРОТИВЛЯТЬСЯ", new Color(0.55f, 0.34f, 0.10f, 0.95f),
                              delegate { Resist(); });
                }
                return;
        }

        BuildMoveButtons();
    }

    // Крупная кнопка во всю нижнюю треть: в этих фазах решение одно, и
    // искать его пальцем среди мелких кнопок неправильно.
    private void BigButton(string label, Color color, System.Action onClick)
    {
        float bw = 300f * _s;
        float bh = 92f * _s;

        Button b = UiKit.MakeButton(_buttonRoot,
            new Vector2(Screen.width - bw * 0.5f - 22f * _s, 70f * _s),
            new Vector2(bw, bh), label, Mathf.RoundToInt(26f * _s), onClick);
        b.GetComponent<Image>().color = color;
        _buttons.Add(b.gameObject);
    }

    private void BuildMoveButtons()
    {
        List<Move> moves = _match.Available(_player);

        float bw = 268f * _s;
        float bh = 56f * _s;
        float gap = 7f * _s;
        int row = 0;

        // Захваты идут первыми снизу: это самое частое действие, и оно
        // должно быть под пальцем без промаха.
        if (_match.Grips(_player) < Match.MaxGrips)
        {
            AddButton("Взять захват", 6, row++, bw, bh, gap,
                      new Color(0.16f, 0.30f, 0.36f, 0.95f), delegate { Grip(); });
        }
        if (_match.Grips(Match.Other(_player)) > 0)
        {
            AddButton("Сорвать захват", 10, row++, bw, bh, gap,
                      new Color(0.34f, 0.24f, 0.16f, 0.95f), delegate { BreakGrip(); });
        }

        for (int i = 0; i < moves.Count; i++)
        {
            Move m = moves[i];
            bool enough = _match.Stamina(_player) >= m.Stamina;

            Color color;
            if (m.IsSubmission) color = new Color(0.50f, 0.13f, 0.17f, 0.95f);
            else if (m.ByTop) color = new Color(0.15f, 0.26f, 0.42f, 0.95f);
            else color = new Color(0.17f, 0.34f, 0.31f, 0.95f);
            if (!enough) color = new Color(0.18f, 0.18f, 0.20f, 0.85f);

            Move captured = m;
            Button b = AddButton(m.Name, Mathf.RoundToInt(m.Stamina), row++, bw, bh, gap,
                                 color, delegate { Press(captured); });
            b.interactable = enough;
        }
    }

    private Button AddButton(string label, int cost, int row, float bw, float bh, float gap,
                             Color color, System.Action onClick)
    {
        float y = 34f * _s + (bh + gap) * row;

        Button b = UiKit.MakeButton(_buttonRoot,
            new Vector2(Screen.width - bw * 0.5f - 22f * _s, y),
            new Vector2(bw, bh), label, Mathf.RoundToInt(20f * _s), onClick);
        b.GetComponent<Image>().color = color;

        // Цена отдельной цифрой у левого края: название читается само по
        // себе, а цифры сравниваются между кнопками.
        Text costText = UiKit.MakeText(b.transform, Vector2.zero,
            new Vector2(46f * _s, bh), cost.ToString(), Mathf.RoundToInt(18f * _s),
            new Color(1f, 1f, 1f, 0.72f), TextAnchor.MiddleCenter);
        costText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        costText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        costText.rectTransform.anchoredPosition = new Vector2(-bw * 0.5f + 26f * _s, 0f);

        _buttons.Add(b.gameObject);
        return b;
    }

    private void Resist()
    {
        if (_match.TryResist(_player)) Snd.Play("grip", 0.4f, 1.2f);
    }

    private void Struggle()
    {
        if (_match.TryStruggle(_player)) Snd.Play("cloth", 0.45f, 1.3f);
    }

    private void Grip()
    {
        if (_match.TryGrip(_player)) Snd.Play("grip", 0.5f);
        RebuildButtons();
    }

    private void BreakGrip()
    {
        if (_match.TryBreakGrip(_player)) Snd.Play("cloth", 0.55f, 0.9f);
        RebuildButtons();
    }

    private void Press(Move m)
    {
        Snd.Play("click", 0.5f);
        if (_match.TryMove(_player, m)) _flashLife = 0.12f;
    }
}
