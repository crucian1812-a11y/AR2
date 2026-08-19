using UnityEngine;
using UnityEngine.UI;

// Экран зала: пояс, послужной список и тот, кто ждёт на татами.
//
// Он существует ради одной вещи — чтобы схватка перестала быть эпизодом.
// Пока игра начинается сразу с борьбы и ею же заканчивается, второй раз её
// запускать незачем. Здесь видно, что уже сделано и что осталось до
// следующего пояса, а у соперника есть имя и манера.
//
// Боец на экране — не картинка: это та же модель, тот же шейдер и тот же
// зал, что и в схватке, только камера обходит его по дуге. Отдельного
// «превью» в игре нет и быть не должно — иначе оно начнёт расходиться с тем,
// что игрок увидит на татами.
public class Menu : MonoBehaviour
{
    private System.Action _onFight;
    private Transform _cam;
    private Vector3 _look = new Vector3(0f, 1.0f, 0.62f);
    private float _angle;

    public static Menu Create(Transform parent, System.Action onFight)
    {
        GameObject go = new GameObject("Menu");
        go.transform.SetParent(parent, false);

        Menu menu = go.AddComponent<Menu>();
        menu._onFight = onFight;
        menu.BuildScene();
        menu.BuildUi();
        return menu;
    }

    // ------------------------------------------------------------ сцена

    private void BuildScene()
    {
        GameObject camGo = new GameObject("MenuCamera");
        camGo.transform.SetParent(transform, false);
        camGo.tag = "MainCamera";

        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 42f;
        cam.nearClipPlane = 0.06f;
        cam.farClipPlane = 90f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.045f, 0.05f, 0.065f);
        cam.allowHDR = true;

        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData data =
            camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;

        _cam = camGo.transform;

        // Боец в поясе игрока — с ровно теми нашивками, что заработаны.
        FighterRig rig = FighterRig.Create(transform, Side.A, Arena.GiBlue, Arena.RimBlue,
                                           Career.Belt, Career.Stripes);
        // Клип стойки для верхней роли: он и ставит бойца в точку z = 0.62,
        // вокруг которой ходит камера.
        rig.Play(Res.HoldName(true, Pos.Standing), true, 0.001f);

        Aim();
    }

    private void Aim()
    {
        // Обход по дуге, а не полный круг: зал освещён спереди, и со спины
        // боец выглядит силуэтом. Дуги в ±26° хватает, чтобы кадр жил.
        float rad = _angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad) * 2.9f, 0.32f, -Mathf.Cos(rad) * 2.9f);
        _cam.position = _look + offset;
        _cam.rotation = Quaternion.LookRotation(_look - _cam.position);
    }

    private void Update()
    {
        _angle = Mathf.Sin(Time.unscaledTime * 0.22f) * 26f;
        Aim();

        // Зал в меню гудит вполголоса: тишина после схватки звучит как
        // выключенный звук, а прежняя громкость — как незаконченный бой.
        Snd.SetCrowd(0.12f);
        Snd.SetTension(0f);
    }

    // ------------------------------------------------------------ панель

    private void BuildUi()
    {
        Canvas canvas = UiKit.CreateCanvas("MenuCanvas", 20);
        canvas.transform.SetParent(transform, false);
        Transform root = canvas.transform;

        float s = UiKit.Scale;
        float w = Screen.width;
        float h = Screen.height;

        // Тёмные полосы сверху и снизу: текст должен читаться поверх зала,
        // а боец — оставаться видимым между ними.
        UiKit.MakePanel(root, new Vector2(w * 0.5f, h - 150f * s),
                        new Vector2(w, 300f * s), new Color(0f, 0f, 0f, 0.62f));

        UiKit.MakeText(root, new Vector2(w * 0.5f, h - 64f * s),
                       new Vector2(w, 70f * s), "ДЖИУ-ДЖИТСУ",
                       Mathf.RoundToInt(44f * s), new Color(1f, 0.96f, 0.9f),
                       TextAnchor.MiddleCenter);
        UiKit.MakeText(root, new Vector2(w * 0.5f, h - 104f * s),
                       new Vector2(w, 34f * s), "схватка в ги",
                       Mathf.RoundToInt(20f * s), new Color(0.72f, 0.78f, 0.86f),
                       TextAnchor.MiddleCenter);

        BeltWidget.Draw(root, new Vector2(w * 0.5f, h - 168f * s),
                        Mathf.Min(w * 0.72f, 420f * s), Career.Belt, Career.Stripes);

        string rank = Career.Champion
            ? "чёрный пояс · карьера пройдена"
            : Career.BeltName(Career.Belt) + " · " + Career.Stripes + " из " +
              Career.StripesPerBelt;
        UiKit.MakeText(root, new Vector2(w * 0.5f, h - 214f * s),
                       new Vector2(w, 32f * s), rank,
                       Mathf.RoundToInt(21f * s), new Color(0.92f, 0.9f, 0.85f),
                       TextAnchor.MiddleCenter);

        string record = "побед " + Career.Wins + "   ·   поражений " + Career.Losses +
                        "   ·   приёмом " + Career.Subs;
        if (Career.BestStreak > 1) record += "   ·   серия " + Career.BestStreak;
        UiKit.MakeText(root, new Vector2(w * 0.5f, h - 248f * s),
                       new Vector2(w, 30f * s), record,
                       Mathf.RoundToInt(18f * s), new Color(0.66f, 0.72f, 0.8f),
                       TextAnchor.MiddleCenter);

        BuildCard(root, w, h, s);

        UiKit.MakeButton(root, new Vector2(w * 0.5f, 132f * s),
                         new Vector2(Mathf.Min(w * 0.74f, 460f * s), 88f * s),
                         "НА ТАТАМИ", Mathf.RoundToInt(30f * s), Fight);

        // Сброс — намеренно мелкий и в углу: он нужен раз в жизни, а
        // нажать его случайно можно каждый раз.
        UiKit.MakeButton(root, new Vector2(w - 96f * s, 44f * s),
                         new Vector2(160f * s, 44f * s),
                         _confirmReset ? "точно?" : "сброс",
                         Mathf.RoundToInt(16f * s), ResetCareer);
    }

    // Карточка соперника. Всё, что нужно знать до схватки: кто, какого
    // разряда, чем опасен и насколько.
    private void BuildCard(Transform root, float w, float h, float s)
    {
        Opponent o = Career.Next();

        float cardH = 190f * s;
        float cy = 300f * s;
        UiKit.MakePanel(root, new Vector2(w * 0.5f, cy),
                        new Vector2(Mathf.Min(w * 0.9f, 560f * s), cardH),
                        new Color(0f, 0f, 0f, 0.66f));
        // Красная полоска слева: цвет соперника тот же, что на татами.
        UiKit.MakePanel(root, new Vector2(w * 0.5f - Mathf.Min(w * 0.9f, 560f * s) * 0.5f + 5f * s, cy),
                        new Vector2(10f * s, cardH), Arena.GiRed);

        UiKit.MakeText(root, new Vector2(w * 0.5f, cy + 62f * s),
                       new Vector2(w * 0.86f, 30f * s), "СЛЕДУЮЩИЙ СОПЕРНИК",
                       Mathf.RoundToInt(16f * s), new Color(0.6f, 0.66f, 0.75f),
                       TextAnchor.MiddleCenter);

        UiKit.MakeText(root, new Vector2(w * 0.5f, cy + 26f * s),
                       new Vector2(w * 0.86f, 44f * s), o.Name.ToUpperInvariant(),
                       Mathf.RoundToInt(32f * s), Color.white, TextAnchor.MiddleCenter);

        UiKit.MakeText(root, new Vector2(w * 0.5f, cy - 8f * s),
                       new Vector2(w * 0.86f, 30f * s),
                       Career.BeltName(o.Belt) + " · " + Ai.StyleName(o.Style),
                       Mathf.RoundToInt(20f * s), new Color(0.86f, 0.84f, 0.8f),
                       TextAnchor.MiddleCenter);

        UiKit.MakeText(root, new Vector2(w * 0.5f, cy - 40f * s),
                       new Vector2(w * 0.84f, 30f * s), o.Note,
                       Mathf.RoundToInt(18f * s), new Color(0.7f, 0.76f, 0.84f),
                       TextAnchor.MiddleCenter);

        // Класс соперника — пятью делениями. Число здесь читалось бы как
        // точность, которой нет.
        int level = Mathf.Clamp(Mathf.RoundToInt(o.Difficulty * 5f), 1, 5);
        float step = 26f * s;
        for (int i = 0; i < 5; i++)
        {
            Color c = i < level ? new Color(0.95f, 0.55f, 0.25f) : new Color(1f, 1f, 1f, 0.16f);
            UiKit.MakePanel(root, new Vector2(w * 0.5f + (i - 2) * step, cy - 70f * s),
                            new Vector2(18f * s, 8f * s), c);
        }
    }

    // ------------------------------------------------------------ действия

    private void Fight()
    {
        Snd.Play("click", 0.6f);
        if (_onFight != null) _onFight();
    }

    private bool _confirmReset;

    private void ResetCareer()
    {
        // Два нажатия: первое переспрашивает. Стереть пояс одним касанием —
        // слишком дорогая цена за промах пальцем.
        if (!_confirmReset)
        {
            _confirmReset = true;
            Rebuild();
            return;
        }

        Career.Reset();
        Snd.Play("whistle", 0.4f);
        _confirmReset = false;
        Rebuild();
    }

    private void Rebuild()
    {
        Transform old = transform.Find("MenuCanvas");
        if (old != null) Destroy(old.gameObject);
        BuildUi();
    }
}
