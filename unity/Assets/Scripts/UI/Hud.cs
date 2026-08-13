using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Интерфейс схватки: часы, очки, полоски сил, название позиции и кнопки
// доступных приёмов.
//
// Кнопки — временная форма управления на этап серого бокса. По плану (§2
// в docs/bjj/PLAN.md) управление будет жестовым, но проверять граф позиций
// удобнее явными подписями: сразу видно, что вообще можно сделать.
public class Hud : MonoBehaviour
{
    private Match _match;
    private Side _player;

    private Text _clock;
    private Text _score;
    private Text _position;
    private Text _event;
    private Image _staminaA;
    private Image _staminaB;
    private Image _progress;
    private Transform _buttonRoot;

    private readonly List<GameObject> _buttons = new List<GameObject>();
    private Pos _shownFor = (Pos)(-1);
    private bool _shownBusy = true;

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
        float s = UiKit.Scale;
        float w = Screen.width;
        float h = Screen.height;

        // Верхняя планка: часы и счёт.
        UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 34f * s),
                        new Vector2(w, 68f * s), new Color(0f, 0f, 0f, 0.55f));

        _clock = UiKit.MakeText(root, new Vector2(w * 0.5f, h - 34f * s),
                                new Vector2(300f * s, 60f * s), "5:00",
                                Mathf.RoundToInt(34f * s), Color.white, TextAnchor.MiddleCenter);

        _score = UiKit.MakeText(root, new Vector2(w * 0.5f, h - 74f * s),
                                new Vector2(400f * s, 40f * s), "0 : 0",
                                Mathf.RoundToInt(22f * s), new Color(0.8f, 0.85f, 0.95f),
                                TextAnchor.MiddleCenter);

        // Полоски сил: слева игрок, справа соперник.
        MakeStaminaBar(root, 24f * s, w * 0.28f, h - 34f * s, Arena.GiBlue, out _staminaA);
        MakeStaminaBar(root, 24f * s, w * 0.72f, h - 34f * s, Arena.GiRed, out _staminaB);

        // Название позиции — главный текст на экране: игрок обязан всегда
        // понимать, где он находится.
        _position = UiKit.MakeText(root, new Vector2(w * 0.5f, h - 130f * s),
                                   new Vector2(w * 0.8f, 50f * s), "Стойка",
                                   Mathf.RoundToInt(30f * s), Color.white, TextAnchor.MiddleCenter);

        _event = UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.5f),
                                new Vector2(w * 0.8f, 60f * s), "",
                                Mathf.RoundToInt(26f * s), new Color(1f, 0.9f, 0.5f),
                                TextAnchor.MiddleCenter);

        // Полоска хода приёма: показывает, что сейчас что-то происходит,
        // и сколько это ещё продлится.
        UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 170f * s),
                        new Vector2(w * 0.5f, 8f * s), new Color(0f, 0f, 0f, 0.5f));
        _progress = UiKit.MakePanel(root, new Vector2(w * 0.25f, h - 170f * s),
                                    new Vector2(0f, 8f * s), new Color(1f, 0.85f, 0.4f, 0.95f));
        _progress.rectTransform.pivot = new Vector2(0f, 0.5f);
        _progress.rectTransform.anchoredPosition = new Vector2(w * 0.25f, h - 170f * s);

        GameObject holder = new GameObject("Moves");
        holder.transform.SetParent(root, false);
        _buttonRoot = holder.transform;
    }

    private void MakeStaminaBar(Transform root, float s, float x, float y, Color color, out Image fill)
    {
        float barW = Screen.width * 0.22f;
        UiKit.MakePanel(root, new Vector2(x, y), new Vector2(barW, 18f),
                        new Color(0f, 0f, 0f, 0.6f));
        fill = UiKit.MakePanel(root, new Vector2(x, y), new Vector2(barW, 18f), color);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.anchoredPosition = new Vector2(x - barW * 0.5f, y);
    }

    private void Update()
    {
        if (_match == null) return;

        int mm = Mathf.FloorToInt(_match.Clock / 60f);
        int ss = Mathf.FloorToInt(_match.Clock % 60f);
        _clock.text = mm + ":" + ss.ToString("00");
        _score.text = _match.ScoreA + " : " + _match.ScoreB;
        _position.text = Positions.Name(_match.Position);
        _event.text = _match.LastEvent;

        float barW = Screen.width * 0.22f;
        _staminaA.rectTransform.sizeDelta =
            new Vector2(barW * (_match.StaminaA / Match.MaxStamina), 18f);
        _staminaB.rectTransform.sizeDelta =
            new Vector2(barW * (_match.StaminaB / Match.MaxStamina), 18f);

        _progress.rectTransform.sizeDelta = new Vector2(
            _match.Busy ? Screen.width * 0.5f * _match.BusyProgress : 0f, 8f * UiKit.Scale);

        // Кнопки пересобираются только когда меняется набор доступных
        // приёмов. Пересборка каждый кадр съедала бы батарею и ломала
        // нажатие: объект исчезал бы под пальцем.
        if (_shownFor != _match.Position || _shownBusy != _match.Busy)
        {
            _shownFor = _match.Position;
            _shownBusy = _match.Busy;
            RebuildButtons();
        }
    }

    private void RebuildButtons()
    {
        for (int i = 0; i < _buttons.Count; i++) Destroy(_buttons[i]);
        _buttons.Clear();

        if (_match.Finished) return;

        List<Move> moves = _match.Available(_player);
        if (moves.Count == 0) return;

        float s = UiKit.Scale;
        float bw = 240f * s;
        float bh = 62f * s;
        float gap = 10f * s;

        // Кнопки в столбик снизу справа — под большой палец правой руки.
        for (int i = 0; i < moves.Count; i++)
        {
            Move m = moves[i];
            float y = 40f * s + (bh + gap) * i;
            bool enough = _match.Stamina(_player) >= m.Stamina;

            string label = m.Name + "  (" + Mathf.RoundToInt(m.Stamina) + ")";
            Button b = UiKit.MakeButton(_buttonRoot, new Vector2(Screen.width - bw * 0.5f - 24f * s, y),
                                        new Vector2(bw, bh), label,
                                        Mathf.RoundToInt(20f * s),
                                        delegate { _match.TryMove(_player, m); });

            Image bg = b.GetComponent<Image>();
            // Сабмишн выделен цветом: это ход, который может закончить бой,
            // и он не должен теряться в списке.
            if (m.IsSubmission) bg.color = new Color(0.55f, 0.16f, 0.2f, 0.95f);
            if (!enough) bg.color = new Color(0.2f, 0.2f, 0.22f, 0.8f);
            b.interactable = enough;

            _buttons.Add(b.gameObject);
        }
    }
}
