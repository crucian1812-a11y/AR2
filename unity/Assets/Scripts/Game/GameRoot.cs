using UnityEngine;

// Сборка схватки: арена, два бойца, камера, интерфейс и соперник.
// Здесь же — единственное место, где состояние Match превращается в картинку.
public class GameRoot : MonoBehaviour
{
    private Match _match;
    private Ai _ai;
    private FighterView _a;
    private FighterView _b;
    private Camera _cam;
    private Pos _staged = (Pos)(-1);
    private Side _stagedTop = (Side)(-1);

    private const Side Player = Side.A;

    public static GameRoot Create()
    {
        GameObject go = new GameObject("GameRoot");
        return go.AddComponent<GameRoot>();
    }

    private void Awake()
    {
        int seed = System.Environment.TickCount;
        _match = new Match(seed);
        _ai = new Ai(_match, Side.B, seed + 7919, 0.55f);

        Arena.Build(transform);

        _a = FighterView.Create(transform, Side.A, Arena.GiBlue);
        _b = FighterView.Create(transform, Side.B, Arena.GiRed);

        BuildCamera();
        Hud.Create(transform, _match, Player);

        ApplyStaging(true);
    }

    private void BuildCamera()
    {
        GameObject go = new GameObject("MainCamera");
        go.transform.SetParent(transform, false);
        go.tag = "MainCamera";

        _cam = go.AddComponent<Camera>();
        _cam.fieldOfView = 42f;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 120f;
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.05f, 0.055f, 0.07f);
        go.AddComponent<AudioListener>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _match.Tick(dt);
        _ai.Tick(dt);

        if (_staged != _match.Position || _stagedTop != _match.Top) ApplyStaging(false);
        MoveCamera(dt);
    }

    // Раскладка бойцов по позиции. Кто именно наверху, известно из Match,
    // поэтому одна и та же таблица поз работает в обе стороны.
    private void ApplyStaging(bool instant)
    {
        _staged = _match.Position;
        _stagedTop = _match.Top;

        Stage st = Staging.For(_match.Position);

        FighterView top = _match.Top == Side.B ? _b : _a;
        FighterView bottom = _match.Top == Side.B ? _a : _b;

        top.Apply(st.PosTop, st.EulerTop, st.LyingTop);
        bottom.Apply(st.PosBottom, st.EulerBottom, st.LyingBottom);

        if (instant)
        {
            top.transform.localPosition = st.PosTop;
            top.transform.localRotation = Quaternion.Euler(st.EulerTop);
            bottom.transform.localPosition = st.PosBottom;
            bottom.transform.localRotation = Quaternion.Euler(st.EulerBottom);
            PlaceCamera(st, true);
        }
    }

    private void MoveCamera(float dt)
    {
        PlaceCamera(Staging.For(_match.Position), false);
    }

    // Камера ведёт себя как телевизионная: своя точка для каждой позиции и
    // плавный переезд между ними. Резкие скачки ракурса читаются как ошибка.
    private void PlaceCamera(Stage st, bool instant)
    {
        Vector3 focus = new Vector3(0f, 0.55f, 0f);
        Vector3 want = focus + st.CamOffset;

        if (instant)
        {
            _cam.transform.position = want;
            _cam.transform.LookAt(focus);
            return;
        }

        _cam.transform.position =
            Vector3.Lerp(_cam.transform.position, want, Time.deltaTime * 2.6f);

        Quaternion look = Quaternion.LookRotation(focus - _cam.transform.position);
        _cam.transform.rotation =
            Quaternion.Slerp(_cam.transform.rotation, look, Time.deltaTime * 3.2f);
    }
}
