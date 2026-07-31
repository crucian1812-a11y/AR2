using UnityEngine;

// Смена дня и ночи. Мир задаёт свою дневную палитру через SetupSky, а
// этот компонент крутит её по времени суток из NetManager: солнце ходит
// по небу, к вечеру краснеет и садится, ночью его подменяет холодная
// «луна», небо и туман темнеют, заливающий свет уходит в синеву.
//
// Ночь считается не по часам, а по высоте солнца — так один и тот же
// код работает и в пещере, где своё почти чёрное небо.
public class DayCycle : MonoBehaviour
{
    public static DayCycle I;

    private Light _sun;
    private Material _sky;

    private Color _dayTop, _dayHorizon, _dayGround, _dayFog;
    private float _daySunIntensity;
    private float _fogDensity;
    // Наклон орбиты: солнце ходит не строго через зенит, иначе тени в
    // полдень пропадают и картинка становится плоской.
    private float _tilt;

    // Ночная палитра выводится из дневной, а не задаётся отдельно: миры
    // очень разные, и общая «ночь» смотрелась бы на них чужой.
    private static Color Night(Color day, float keep, Color toward)
    {
        return Color.Lerp(new Color(day.r * keep, day.g * keep, day.b * keep), toward, 0.45f);
    }

    public static DayCycle Attach(Transform parent, Light sun, Material sky,
        Color top, Color horizon, Color ground, Color fog, float fogDensity,
        float sunIntensity, float tilt)
    {
        GameObject go = new GameObject("DayCycle");
        go.transform.SetParent(parent, false);
        DayCycle d = go.AddComponent<DayCycle>();
        d._sun = sun;
        d._sky = sky;
        d._dayTop = top;
        d._dayHorizon = horizon;
        d._dayGround = ground;
        d._dayFog = fog;
        d._fogDensity = fogDensity;
        d._daySunIntensity = sunIntensity;
        d._tilt = tilt;
        I = d;
        d.Apply();
        return d;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    // Насколько сейчас ночь: 0 — день, 1 — глухая полночь.
    public static float Darkness
    {
        get { return I != null ? I._darkness : 0f; }
    }

    public static bool IsNight { get { return Darkness > 0.55f; } }

    private float _darkness;

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        NetManager net = NetManager.I;
        float t = net != null ? net.DayTime : 0.3f;

        // Высота солнца: синус от суточной фазы. В полночь -1, в полдень +1.
        float elev = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);
        _darkness = Mathf.Clamp01(-elev * 1.6f + 0.25f);

        // Сумерки — узкая полоса у горизонта, где свет краснеет сильнее всего.
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(elev) * 4.5f);

        if (_sun != null)
        {
            // Солнце обходит небо кругом; наклон орбиты держим постоянным.
            float angle = (t - 0.25f) * 360f;
            _sun.transform.localRotation = Quaternion.Euler(angle, _tilt, 0f);

            Color warm = new Color(1f, 0.97f, 0.9f);
            Color sunset = new Color(1f, 0.62f, 0.34f);
            Color moon = new Color(0.62f, 0.72f, 1f);

            Color c = Color.Lerp(warm, sunset, dusk);
            _sun.color = Color.Lerp(c, moon, _darkness);
            // Луна светит слабо, но не в ноль: иначе ночью не видно ничего,
            // а на телефоне это просто чёрный экран.
            // Дневная сила — та же, что задаёт SetupSky с множителем 1.45.
            float day = _daySunIntensity * 1.45f;
            _sun.intensity = Mathf.Lerp(day, day * 0.26f, _darkness);
            _sun.shadowStrength = Mathf.Lerp(0.85f, 0.35f, _darkness);
        }

        Color nightTop = Night(_dayTop, 0.16f, new Color(0.04f, 0.06f, 0.16f));
        Color nightHor = Night(_dayHorizon, 0.2f, new Color(0.09f, 0.11f, 0.24f));
        Color nightGnd = Night(_dayGround, 0.3f, new Color(0.05f, 0.06f, 0.1f));
        Color duskHor = Color.Lerp(_dayHorizon, new Color(1f, 0.55f, 0.35f), 0.55f);

        Color top = Color.Lerp(_dayTop, nightTop, _darkness);
        Color hor = Color.Lerp(Color.Lerp(_dayHorizon, duskHor, dusk), nightHor, _darkness);
        Color gnd = Color.Lerp(_dayGround, nightGnd, _darkness);

        if (_sky != null)
        {
            _sky.SetColor("_TopColor", top);
            _sky.SetColor("_HorizonColor", hor);
            _sky.SetColor("_GroundColor", gnd);
        }

        // Те же коэффициенты, что в SetupSky: заливка низкая, объём даёт
        // солнце. Иначе смена суток вернула бы выцветшую картинку обратно.
        //
        // Ночью к заливке добавляется небольшой холодный пол. Считая по
        // палитре деревни, к полуночи ambient падал примерно до 0.013,
        // 0.024 и 0.050 по каналам — это не «темно», это чёрный
        // прямоугольник, в котором на телефоне не видно ни края
        // платформы, ни подошедшего врага. Пол поднимает дно, не трогая
        // день: множитель на _darkness днём равен нулю.
        Color floor = new Color(0.055f, 0.065f, 0.095f) * _darkness;
        RenderSettings.ambientSkyColor = top * 0.32f + floor;
        RenderSettings.ambientEquatorColor = hor * 0.26f + floor;
        RenderSettings.ambientGroundColor = gnd * 0.20f + floor * 0.7f;

        if (_fogDensity > 0f)
        {
            RenderSettings.fogColor = Color.Lerp(_dayFog,
                Night(_dayFog, 0.22f, new Color(0.07f, 0.09f, 0.18f)), _darkness);
            // Ночью воздух гуще: дальние холмы тонут, мир кажется теснее.
            RenderSettings.fogDensity = _fogDensity * Mathf.Lerp(1f, 1.5f, _darkness);
        }
    }
}
