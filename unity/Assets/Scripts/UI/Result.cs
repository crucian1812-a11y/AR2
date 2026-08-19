using UnityEngine;
using UnityEngine.UI;

// Итог схватки.
//
// Экран нужен не ради слова «победа» — оно и так понятно. Он нужен, чтобы
// показать, что схватка что-то изменила: полоска на поясе, счёт побед,
// а иногда и новый пояс. Без этого показа карьера остаётся числом в
// PlayerPrefs, о котором игрок не знает.
//
// Схватка при этом не убирается: экран ложится поверх последнего кадра,
// где бойцы так и лежат в финальной позиции.
public class Result : MonoBehaviour
{
    private System.Action _onContinue;
    private float _life;
    private CanvasGroup _group;

    public static Result Create(Transform parent, bool won, bool bySubmission,
                                int scoreMe, int scoreThem, Opponent rival,
                                bool promoted, System.Action onContinue)
    {
        Canvas canvas = UiKit.CreateCanvas("ResultCanvas", 30);
        canvas.transform.SetParent(parent, false);

        Result r = canvas.gameObject.AddComponent<Result>();
        r._onContinue = onContinue;
        r.Build(canvas.transform, won, bySubmission, scoreMe, scoreThem, rival, promoted);
        return r;
    }

    private void Build(Transform root, bool won, bool bySubmission,
                       int scoreMe, int scoreThem, Opponent rival, bool promoted)
    {
        float s = UiKit.Scale;
        float w = Screen.width;
        float h = Screen.height;

        UiKit.MakePanel(root, new Vector2(w * 0.5f, h * 0.5f), new Vector2(w, h),
                        new Color(0f, 0f, 0f, 0.72f));

        string verdict = won ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
        Color vc = won ? new Color(1f, 0.84f, 0.35f) : new Color(0.92f, 0.42f, 0.36f);
        UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.76f), new Vector2(w, 90f * s),
                       verdict, Mathf.RoundToInt(56f * s), vc, TextAnchor.MiddleCenter);

        // Как именно — важнее счёта: сдача и решение по очкам ощущаются
        // по-разному, и это стоит назвать словами.
        string how;
        if (bySubmission) how = won ? "приёмом" : "сдался";
        else if (scoreMe == scoreThem) how = "ничья по очкам — считается поражением";
        else how = "по очкам " + scoreMe + " : " + scoreThem;

        UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.70f), new Vector2(w, 40f * s),
                       how + "   ·   " + rival.Name,
                       Mathf.RoundToInt(22f * s), new Color(0.82f, 0.86f, 0.92f),
                       TextAnchor.MiddleCenter);

        BeltWidget.Draw(root, new Vector2(w * 0.5f, h * 0.56f),
                        Mathf.Min(w * 0.72f, 420f * s), Career.Belt, Career.Stripes);

        string progress;
        if (promoted)
            progress = "НОВЫЙ ПОЯС — " + Career.BeltName(Career.Belt).ToUpperInvariant();
        else if (won && Career.Champion)
            progress = "вольная схватка выиграна";
        else if (won)
            progress = "полоска на пояс: " + Career.Stripes + " из " + Career.StripesPerBelt;
        else
            progress = "тот же соперник ждёт снова";

        UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.48f), new Vector2(w * 0.9f, 46f * s),
                       progress, Mathf.RoundToInt(promoted ? 30f * s : 22f * s),
                       promoted ? new Color(1f, 0.9f, 0.5f) : new Color(0.78f, 0.82f, 0.88f),
                       TextAnchor.MiddleCenter);

        string record = "побед " + Career.Wins + "   ·   поражений " + Career.Losses +
                        "   ·   приёмом " + Career.Subs;
        UiKit.MakeText(root, new Vector2(w * 0.5f, h * 0.42f), new Vector2(w, 32f * s),
                       record, Mathf.RoundToInt(18f * s), new Color(0.62f, 0.68f, 0.76f),
                       TextAnchor.MiddleCenter);

        UiKit.MakeButton(root, new Vector2(w * 0.5f, h * 0.22f),
                         new Vector2(Mathf.Min(w * 0.7f, 420f * s), 84f * s),
                         "В ЗАЛ", Mathf.RoundToInt(28f * s), Continue);

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        if (promoted) Snd.Play("roar", 0.9f);
    }

    private void Update()
    {
        // Появляется не мгновенно: резкая табличка поверх замедленного
        // добивания рвёт кадр.
        _life = Mathf.Min(1f, _life + Time.unscaledDeltaTime * 2.2f);
        if (_group != null) _group.alpha = _life;
    }

    private void Continue()
    {
        Snd.Play("click", 0.6f);
        if (_onContinue != null) _onContinue();
    }
}
