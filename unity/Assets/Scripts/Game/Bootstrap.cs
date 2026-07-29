using UnityEngine;

// Точка входа: создаёт менеджеры и переключает меню и игру.
// Единственный компонент, который лежит в стартовой сцене.
public class Bootstrap : MonoBehaviour
{
    private MenuUI _menu;
    private GameRoot _game;
    private bool _autotest;
    private float _autotestTimer;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        // Сглаживание, дальность теней и лимит источников задаёт URP-ассет,
        // который собирается в BuildScript.
        Input.multiTouchEnabled = true;

        NetManager.Create();
        Snd.Create();

        // Купленное подтягиваем сразу, ещё до меню. Сохранение читалось
        // только внутри StartSolo/StartHost, то есть уже ПОСЛЕ выхода из
        // меню: на холодном старте в выборе героя всегда было три штуки,
        // сколько бы их ни куплено.
        SaveGame.Load(NetManager.I, true);

        NetManager.I.OnEnterGame += EnterGame;
        NetManager.I.OnLeaveToMenu += LeaveToMenu;

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--autotest") _autotest = true;

        ShowMenu();

        if (_autotest)
        {
            Debug.Log("AUTOTEST: starting solo game");
            NetManager.I.StartSolo();
        }
    }

    private void OnDestroy()
    {
        if (NetManager.I != null)
        {
            NetManager.I.OnEnterGame -= EnterGame;
            NetManager.I.OnLeaveToMenu -= LeaveToMenu;
        }
    }

    private void ShowMenu()
    {
        if (_menu == null) _menu = MenuUI.Create();
        _menu.transform.SetParent(transform, false);
    }

    private void EnterGame()
    {
        Ctrl.Reset();
        if (_menu != null)
        {
            Object.Destroy(_menu.gameObject);
            _menu = null;
        }
        if (_game != null)
        {
            Object.Destroy(_game.gameObject);
            _game = null;
        }
        _game = GameRoot.Create();
        _game.transform.SetParent(transform, false);
    }

    private void LeaveToMenu(string reason)
    {
        Ctrl.Reset();
        if (_game != null)
        {
            Object.Destroy(_game.gameObject);
            _game = null;
        }
        ShowMenu();
    }

    private void Update()
    {
        if (!_autotest) return;
        _autotestTimer += Time.deltaTime;
        if (_autotestTimer > 4f)
        {
            Debug.Log("AUTOTEST OK");
            Application.Quit();
            _autotest = false;
        }
    }
}
