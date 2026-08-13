using UnityEngine;

// Точка входа. Единственный компонент в стартовой сцене — саму сцену
// создаёт BuildScript во время сборки, в репозитории её нет.
public class Bootstrap : MonoBehaviour
{
    private GameRoot _game;
    private bool _autotest;
    private float _timer;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.multiTouchEnabled = true;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;

        _game = GameRoot.Create();
        _game.transform.SetParent(transform, false);

        // Прогон без рук: сборка в CI может запустить игру с этим ключом
        // и убедиться, что она не падает на старте.
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--autotest") _autotest = true;

        if (_autotest) Debug.Log("AUTOTEST: match started");
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
