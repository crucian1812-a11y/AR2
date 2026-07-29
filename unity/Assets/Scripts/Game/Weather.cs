using UnityEngine;

// Погода: дождь и снег. Осадки едут вместе с камерой, поэтому хватает
// небольшого облака частиц вокруг игрока вместо тучи на весь мир.
//
// Погода не постоянная — она приходит и уходит. Смена идёт по общему
// времени суток, поэтому у всех в сетевой игре дождь начинается разом,
// без единого нового пакета.
public class Weather : MonoBehaviour
{
    public enum Kind { None, Rain, Snow }

    public static Weather I;

    private Kind _kind = Kind.None;
    private ParticleFx _fx;
    private Transform _follow;
    private float _strength;
    // Доля суток, в которую идут осадки. Считается от DayTime, поэтому
    // одинакова у всех игроков.
    private float _windowStart, _windowEnd;
    private float _baseAlpha = 1f;

    public static Weather Attach(Transform parent, Kind kind, int worldSeed)
    {
        GameObject go = new GameObject("Weather");
        go.transform.SetParent(parent, false);
        Weather w = go.AddComponent<Weather>();
        w._kind = kind;
        // Окно осадков своё у каждого мира, но детерминированное.
        float a = (worldSeed % 97) / 97f * 0.7f;
        w._windowStart = a;
        w._windowEnd = a + (kind == Kind.Snow ? 0.34f : 0.22f);
        I = w;
        if (kind != Kind.None) w.Build();
        return w;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    public static bool IsWet { get { return I != null && I._strength > 0.35f; } }

    private void Build()
    {
        bool snow = _kind == Kind.Snow;
        Color tint = snow
            ? new Color(1f, 1f, 1f, 0.85f)
            : new Color(0.72f, 0.82f, 0.95f, 0.55f);

        _baseAlpha = tint.a;
        _fx = ParticleFx.Spawn(transform, Vector3.zero, snow ? 260 : 340, tint);
        // Облако частиц накрывает игрока сверху и по сторонам.
        _fx.EmitExtents = new Vector3(16f, 1.5f, 16f);
        _fx.BaseVelocity = new Vector3(snow ? 0.6f : 1.4f, snow ? -2.2f : -14f, 0f);
        _fx.Gravity = new Vector3(0f, snow ? -0.4f : -6f, 0f);
        _fx.SpeedMin = snow ? 0.2f : 1.5f;
        _fx.SpeedMax = snow ? 0.8f : 4f;
        _fx.SizeMin = snow ? 0.07f : 0.035f;
        _fx.SizeMax = snow ? 0.16f : 0.09f;
        _fx.LifeMin = snow ? 3.5f : 1.1f;
        _fx.LifeMax = snow ? 6f : 1.8f;
        _fx.Prewarm();
    }

    private void Update()
    {
        if (_kind == Kind.None || _fx == null) return;

        NetManager net = NetManager.I;
        float t = net != null ? net.DayTime : 0.3f;

        // Плавный вход и выход: осадки нарастают и стихают, а не щёлкают.
        float target = 0f;
        float span = _windowEnd - _windowStart;
        if (span > 0.001f)
        {
            float k = Mathf.Repeat(t - _windowStart, 1f);
            if (k < span)
            {
                float edge = Mathf.Min(k, span - k) / (span * 0.35f);
                target = Mathf.Clamp01(edge);
            }
        }
        _strength = Mathf.MoveTowards(_strength, target, Time.deltaTime * 0.25f);

        BearPlayer p = GameRoot.LocalBear;
        if (p == null) return;
        if (_follow == null) _follow = p.transform;

        // Меш частиц строится в МИРОВЫХ координатах, а сам объект всегда
        // стоит в нуле — двигать надо точку испускания, а не transform.
        _fx.Origin = _follow.position + new Vector3(0f, 11f, 0f);

        // При нулевой силе прячем целиком, чтобы не тратить кадры на
        // невидимый дождь.
        _fx.gameObject.SetActive(_strength > 0.02f);

        // Сила осадков — это плотность: гасим альфу, а не число частиц,
        // иначе при каждом изменении пришлось бы пересобирать массив.
        Color c = _fx.Tint;
        _fx.Tint = new Color(c.r, c.g, c.b, _baseAlpha * _strength);
    }
}
