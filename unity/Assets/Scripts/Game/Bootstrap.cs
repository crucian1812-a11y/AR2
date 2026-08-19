using UnityEngine;

// Точка входа и весь жизненный цикл приложения: зал → схватка → итог → зал.
// Сцену создаёт BuildScript во время сборки, в репозитории её нет.
//
// Зал, пост-обработка и звук строятся здесь один раз и живут дольше любой
// схватки. Раньше их собирал GameRoot, и это было незаметно ровно до тех
// пор, пока схватка не стала одной из многих: пересобирать трибуны между
// боями незачем, а слушатель звука вообще обязан быть один на приложение.
public class Bootstrap : MonoBehaviour
{
    private GameRoot _game;
    private Menu _menu;
    private Result _result;

    private bool _autotest;
    private float _timer;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.multiTouchEnabled = true;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

        Career.Load();

        Snd.Create();
        gameObject.AddComponent<AudioListener>();

        Arena.Build(transform);
        PostFx.Build(transform);

        // Сломанный шейдер не роняет сборку — он просто перестаёт рисовать,
        // и боец выходит прозрачным. Поэтому спрашиваем прямо и говорим
        // вслух: молчаливая пустота на экране дороже любой ошибки.
        string broken = ShaderCheck.Verify();
        if (broken != "")
        {
            Debug.LogError("Шейдеры не готовы:\n" + broken);
            Warn(broken);
        }

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--autotest") _autotest = true;

        // Прогон без рук: сборка в CI запускает игру с этим ключом и
        // убеждается, что она доходит до схватки и не падает.
        if (_autotest)
        {
            Debug.Log("AUTOTEST: match started");
            StartBout();
        }
        else
        {
            ShowMenu();
        }
    }

    // ------------------------------------------------------------ переходы

    private void ShowMenu()
    {
        if (_menu != null) return;
        _menu = Menu.Create(transform, StartBout);
    }

    private void StartBout()
    {
        if (_menu != null)
        {
            Destroy(_menu.gameObject);
            _menu = null;
        }
        if (_result != null)
        {
            Destroy(_result.gameObject);
            _result = null;
        }

        _game = GameRoot.Create(Career.Next());
        _game.transform.SetParent(transform, false);
        _game.OnFinished += Finish;
    }

    private void Finish(Side winner)
    {
        bool won = winner == Side.A;
        bool bySub = _game.BySubmission;

        // Карьера правится до показа: экран итога обязан показывать уже
        // новое состояние, иначе полоска появится только в меню и повышение
        // пройдёт незамеченным.
        bool promoted = false;
        if (won) promoted = Career.Win(bySub);
        else Career.Lose();

        _result = Result.Create(transform, won, bySub,
                                _game.ScoreA, _game.ScoreB, _game.Rival,
                                promoted, BackToMenu);
    }

    private void BackToMenu()
    {
        if (_result != null)
        {
            Destroy(_result.gameObject);
            _result = null;
        }
        if (_game != null)
        {
            _game.OnFinished -= Finish;
            Destroy(_game.gameObject);
            _game = null;
        }
        ShowMenu();
    }

    // Сообщение об отсутствующем шейдере. Держится на экране всё время:
    // такую поломку нельзя не заметить, и лучше испортить кадр, чем
    // отдать пользователю игру с невидимыми бойцами.
    private void Warn(string text)
    {
        Canvas canvas = UiKit.CreateCanvas("WarnCanvas", 100);
        canvas.transform.SetParent(transform, false);
        UiKit.MakeText(canvas.transform,
                       new Vector2(Screen.width * 0.5f, Screen.height * 0.08f),
                       new Vector2(Screen.width * 0.94f, 120f * UiKit.Scale),
                       "ШЕЙДЕРЫ НЕ СОБРАЛИСЬ:\n" + text,
                       Mathf.RoundToInt(18f * UiKit.Scale),
                       new Color(1f, 0.45f, 0.4f), TextAnchor.LowerCenter);
    }

    private void Update()
    {
        if (!_autotest) return;
        _timer += Time.deltaTime;
        if (_timer > 6f)
        {
            Debug.Log("AUTOTEST OK");
            _autotest = false;
            Application.Quit();
        }
    }
}
