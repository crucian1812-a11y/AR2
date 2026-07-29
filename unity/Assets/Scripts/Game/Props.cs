using UnityEngine;

// Движущаяся платформа: ездит между двумя точками и переносит на себе
// стоящего игрока. Ход считает каждый клиент одинаково — движение
// детерминировано по игровому времени, синхронизация не нужна.
public class MovingPlatform : MonoBehaviour
{
    public Vector3 PointA;
    public Vector3 PointB;
    public float Period = 4f;
    public float Phase;

    private Vector3 _prevPos;

    public static MovingPlatform Create(Transform parent, Vector3 a, Vector3 b, Vector3 size,
        Material mat, float period, float phase)
    {
        GameObject go = Gfx.Box(parent, a, size, mat);
        go.name = "MovingPlatform";
        MovingPlatform mp = go.AddComponent<MovingPlatform>();
        mp.PointA = a;
        mp.PointB = b;
        mp.Period = Mathf.Max(0.5f, period);
        mp.Phase = phase;
        mp._prevPos = a;
        return mp;
    }

    private void Update()
    {
        float t = Mathf.PingPong(Time.time / Period + Phase, 1f);
        t = t * t * (3f - 2f * t); // сглаживание на концах
        Vector3 target = Vector3.Lerp(PointA, PointB, t);

        Vector3 delta = target - _prevPos;
        transform.localPosition = target;
        _prevPos = target;

        // Переносим игрока, если он стоит сверху.
        BearPlayer local = GameRoot.LocalBear;
        if (local == null || delta.sqrMagnitude < 0.0000001f) return;

        Vector3 half = transform.localScale * 0.5f;
        Vector3 p = local.transform.position - transform.position;
        if (Mathf.Abs(p.x) < half.x + 0.5f && Mathf.Abs(p.z) < half.z + 0.5f &&
            p.y > half.y - 0.3f && p.y < half.y + 1.6f)
        {
            local.ExternalMove(delta);
        }
    }
}

// Батут: подбрасывает игрока вверх при касании.
public class BouncePad : MonoBehaviour
{
    public float Power = 16f;

    private Transform _visual;
    private float _squash;

    public static BouncePad Create(Transform parent, Vector3 pos, float power)
    {
        GameObject go = new GameObject("BouncePad");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        BouncePad bp = go.AddComponent<BouncePad>();
        bp.Power = power;
        bp.Build();
        return bp;
    }

    private void Build()
    {
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        Material rim = Gfx.Mat(new Color(0.35f, 0.3f, 0.28f), 0.1f);
        Gfx.Cyl(_visual, new Vector3(0f, 0.12f, 0f), new Vector3(2.2f, 0.12f, 2.2f), rim);

        Material top = Gfx.MatFull(new Color(1f, 0.45f, 0.55f), 0.55f, 0f,
            new Color(0.55f, 0.12f, 0.18f), 0f, 0f);
        Gfx.Cyl(_visual, new Vector3(0f, 0.3f, 0f), new Vector3(1.9f, 0.08f, 1.9f), top, false);
        Gfx.Glow(transform, new Vector3(0f, 0.4f, 0f), 2.6f, new Color(1f, 0.5f, 0.6f, 0.4f));
    }

    private void Update()
    {
        if (_squash > 0f)
        {
            _squash -= Time.deltaTime * 4f;
            float k = Mathf.Clamp01(_squash);
            _visual.localScale = new Vector3(1f + k * 0.25f, 1f - k * 0.55f, 1f + k * 0.25f);
        }
        else _visual.localScale = Vector3.one;

        BearPlayer local = GameRoot.LocalBear;
        if (local == null) return;
        Vector3 d = local.transform.position - transform.position;
        if (new Vector2(d.x, d.z).magnitude < 1.5f && d.y > -0.4f && d.y < 1.4f)
        {
            if (local.TryBounce(Power))
            {
                _squash = 1f;
                Snd.Play("bounce", 0.9f);
            }
        }
    }
}

// Костёр или фонарь: свет, тепло и восходящие искры.
public class Campfire : MonoBehaviour
{
    public static Campfire Create(Transform parent, Vector3 pos, Color color, float range)
    {
        GameObject go = new GameObject("Campfire");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Campfire c = go.AddComponent<Campfire>();
        c.Build(color, range);
        return c;
    }

    private Light _light;
    private float _t;
    private float _baseIntensity;

    private void Build(Color color, float range)
    {
        Material logMat = Gfx.Mat(new Color(0.32f, 0.21f, 0.12f), 0.05f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 45f;
            GameObject log = Gfx.Cyl(transform, new Vector3(0f, 0.12f, 0f),
                new Vector3(0.22f, 0.6f, 0.22f), logMat, false);
            log.transform.localRotation = Quaternion.Euler(78f, a, 0f);
        }

        Material emberMat = Gfx.MatFull(new Color(1f, 0.55f, 0.2f), 0.4f, 0f,
            new Color(1f, 0.45f, 0.1f), 0f, 0f);
        Gfx.Ball(transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.35f, 0.55f), emberMat);
        Gfx.Glow(transform, new Vector3(0f, 0.5f, 0f), 3.2f, new Color(1f, 0.6f, 0.25f, 0.55f));

        _baseIntensity = 1.5f;
        _light = Gfx.PointLight(transform, new Vector3(0f, 0.9f, 0f), color, range, _baseIntensity);

        ParticleFx fx = ParticleFx.Spawn(transform, transform.position + new Vector3(0f, 0.4f, 0f),
            20, new Color(1f, 0.65f, 0.25f, 0.9f));
        fx.EmitExtents = new Vector3(0.22f, 0.1f, 0.22f);
        fx.BaseVelocity = new Vector3(0f, 1.4f, 0f);
        fx.Gravity = new Vector3(0.05f, 0.5f, 0.05f);
        fx.SpeedMin = 0.15f;
        fx.SpeedMax = 0.5f;
        fx.SizeMin = 0.05f;
        fx.SizeMax = 0.12f;
        fx.LifeMin = 0.9f;
        fx.LifeMax = 1.7f;
        fx.Prewarm();
    }

    private void Update()
    {
        if (_light == null) return;
        _t += Time.deltaTime * 7f;
        // Живое мерцание пламени.
        _light.intensity = _baseIntensity * (0.82f + 0.18f * Mathf.Sin(_t) * Mathf.Cos(_t * 0.7f));
    }
}

// Медленно вращающийся объект — лопасти мельницы, парящие кристаллы.
public class Spinner : MonoBehaviour
{
    public Vector3 Axis = Vector3.forward;
    public float Speed = 30f;
    public float BobAmplitude;
    public float BobSpeed = 1f;
    // Половина длины бревна: за её пределами бревно игрока не задевает.
    public float Reach = 5f;
    // Крылья мельницы крутятся высоко над землёй и толкать никого не должны.
    public bool Push = true;

    private Vector3 _basePos;
    private float _t;

    private void Start() { _basePos = transform.localPosition; }

    private void Update()
    {
        transform.Rotate(Axis * (Speed * Time.deltaTime));
        if (BobAmplitude > 0f)
        {
            _t += Time.deltaTime * BobSpeed;
            transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(_t) * BobAmplitude, 0f);
        }

        Sweep();
    }

    // Бревно должно сбивать с платформы, но само по себе оно этого не
    // делает: CharacterController двигается только собственным Move(),
    // и чужой коллайдер, наезжающий на него, не толкает — получался либо
    // проход насквозь, либо застревание. Толкаем вручную.
    private void Sweep()
    {
        if (!Push) return;
        BearPlayer p = GameRoot.LocalBear;
        if (p == null) return;

        Vector3 local = transform.InverseTransformPoint(p.transform.position + new Vector3(0f, 0.85f, 0f));
        // Бревно лежит вдоль локальной оси X, толщина 0.7.
        if (Mathf.Abs(local.x) > Reach) return;
        if (Mathf.Abs(local.y) > 1.1f || Mathf.Abs(local.z) > 0.95f) return;

        // Толкаем по касательной — в ту сторону, куда идёт бревно.
        Vector3 fromAxis = transform.TransformPoint(new Vector3(local.x, 0f, 0f));
        Vector3 outward = p.transform.position - fromAxis;
        outward.y = 0f;
        if (outward.magnitude < 0.01f) outward = transform.forward;
        Vector3 tangent = Vector3.Cross(Vector3.up, outward.normalized) * Mathf.Sign(Speed);

        p.ExternalMove((tangent * 5.5f + outward.normalized * 2.5f) * Time.deltaTime);
        p.Shake(0.12f);
    }
}
