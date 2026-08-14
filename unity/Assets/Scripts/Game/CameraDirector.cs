using UnityEngine;
using UnityEngine.Rendering.Universal;

// Камера. По плану (§3 в docs/bjj/PLAN.md) она даёт половину эффекта
// дорогой картинки, и это не преувеличение: одни и те же модели под
// телевизионным ракурсом и под «видом сверху из угла» выглядят как игры
// разных бюджетов.
//
// Здесь четыре вещи, которых нет у камеры, просто следящей за целью:
//
// 1. Свой ракурс на каждую позицию — из Staging.
// 2. Медленный наезд, пока позиция держится. Статичная камера мертвеет
//    через несколько секунд, а еле заметное сближение держит напряжение.
// 3. Тряска на броске и на входе в сабмишн.
// 4. Добивание: замедление времени, наезд вплотную, глубина резкости.
public class CameraDirector : MonoBehaviour
{
    private Match _match;

    private Vector3 _focus = new Vector3(0f, 0.55f, 0f);
    private Vector3 _wantPos;
    private Vector3 _wantFocus;

    private float _dwell;          // сколько держится текущая позиция
    private float _shake;
    private float _shakeDecay = 3.2f;
    private Pos _lastPos = (Pos)(-1);
    private bool _finished;

    // Разворот ракурса, когда наверху оказывается второй боец: иначе
    // камера смотрит доминирующему в спину.
    private float _sideSign = 1f;

    public static CameraDirector Create(Transform parent, Match match)
    {
        GameObject go = new GameObject("MainCamera");
        go.transform.SetParent(parent, false);
        go.tag = "MainCamera";

        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 46f;
        cam.nearClipPlane = 0.06f;
        cam.farClipPlane = 90f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.045f, 0.05f, 0.065f);
        cam.allowHDR = true;

        UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        go.AddComponent<AudioListener>();

        CameraDirector dir = go.AddComponent<CameraDirector>();
        dir._match = match;
        dir.Snap();
        return dir;
    }

    public void Kick(float amount)
    {
        _shake = Mathf.Max(_shake, amount);
    }

    private Stage Current()
    {
        return Staging.For(_match.Position);
    }

    private void Compute()
    {
        Stage st = Current();

        _sideSign = _match.Top == Side.B ? -1f : 1f;

        Vector3 offset = st.CamOffset;
        offset.x *= _sideSign;
        offset.z *= _sideSign;

        // Наезд по мере удержания позиции: 1.0 в начале, 0.86 через
        // десять секунд. Больше нельзя — камера начнёт лезть в бойцов.
        float creep = Mathf.Lerp(1f, 0.86f, Mathf.Clamp01(_dwell / 10f));
        offset *= creep;

        if (_match.Now == Phase.Submission || (_match.Finished && _match.Position == Pos.Submitted))
        {
            // Приём захвачен — камера идёт вплотную и низко, почти на
            // уровень татами, и держится там до развязки.
            offset = new Vector3(1.5f * _sideSign, 0.95f, 1.5f * _sideSign);
            _focus = new Vector3(0f, 0.42f, 0f);
        }
        else
        {
            _focus = new Vector3(0f, 0.55f, 0f);
        }

        _wantPos = _focus + offset;
        _wantFocus = _focus;
    }

    private void Snap()
    {
        _dwell = 0f;
        Compute();
        transform.position = _wantPos;
        transform.rotation = Quaternion.LookRotation(_wantFocus - _wantPos);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_lastPos != _match.Position)
        {
            _lastPos = _match.Position;
            _dwell = 0f;
            // Смена позиции — это всегда рывок: бросок, проход, свипт.
            Kick(_match.Position == Pos.Submitted ? 0.5f : 0.22f);
        }
        _dwell += dt;

        Compute();

        // Переезд между ракурсами. Быстрее при добивании: там важно
        // успеть подойти, пока идёт замедление.
        float speed = (_match.Finished || _match.Now == Phase.Submission) ? 4.2f : 2.4f;
        transform.position = Vector3.Lerp(transform.position, _wantPos, dt * speed);

        Quaternion look = Quaternion.LookRotation(_wantFocus - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * (speed + 0.8f));

        ApplyShake(dt);
        ApplyFinish();
    }

    private void ApplyShake(float dt)
    {
        if (_shake <= 0.0005f) return;

        _shake = Mathf.Max(0f, _shake - dt * _shakeDecay * _shake.Clamp01Min());

        // Шум Перлина, а не случайные числа: случайная тряска дрожит, а
        // перлин даёт связное движение — как камера в руках оператора.
        float t = Time.unscaledTime * 22f;
        float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;

        transform.position += transform.right * x * _shake * 0.09f
                            + transform.up * y * _shake * 0.09f;
        transform.rotation *= Quaternion.Euler(y * _shake * 1.6f, x * _shake * 1.6f, 0f);
    }

    // Замедление и глубина резкости на финише. Время трогаем только здесь
    // и только один раз, поэтому его не с чем «не поделить».
    private void ApplyFinish()
    {
        bool sub = _match.Finished && _match.Position == Pos.Submitted;

        if (sub && !_finished)
        {
            _finished = true;
            Time.timeScale = 0.32f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            PostFx.SetCinematic(true, 2.2f);
        }
        else if (!sub && _finished)
        {
            _finished = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            PostFx.SetCinematic(false, 3f);
        }

        if (_finished)
        {
            // Возврат к нормальному времени: замедление на весь показ
            // утомляет, полутора секунд достаточно.
            Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 0.45f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}

internal static class FloatExt
{
    // Затухание тряски пропорционально ей самой даёт мягкий хвост; ноль
    // в знаменателе при этом невозможен.
    public static float Clamp01Min(this float v)
    {
        return Mathf.Max(0.25f, Mathf.Min(1f, v));
    }
}
