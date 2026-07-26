using UnityEngine;

// Враг (гриб или ледяной слизень): патрулирует между двумя точками.
// Движение считает хост, клиенты интерполируют полученные позиции.
public class Enemy : MonoBehaviour
{
    public int Id;
    public string Kind = "mushroom";
    public float Speed = 2f;
    public Vector3 PointA;
    public Vector3 PointB;
    public bool Dying;

    private Transform _visual;
    private Vector3 _target;
    private Vector3 _netPos;
    private float _netYaw;
    private bool _hasNet;
    private float _bob;
    private float _dieT = -1f;

    public float Yaw { get { return _visual != null ? _visual.localEulerAngles.y : 0f; } }

    public static Enemy Spawn(Transform parent, Vector3 a, Vector3 b, string kind, float speed, int id)
    {
        GameObject go = new GameObject("Enemy_" + id);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = a;
        Enemy e = go.AddComponent<Enemy>();
        e.Id = id;
        e.Kind = kind;
        e.Speed = speed;
        e.PointA = a;
        e.PointB = b;
        e.Init();
        return e;
    }

    private void Init()
    {
        _target = PointB;
        _netPos = transform.position;
        _bob = Random.Range(0f, 6.28f);

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        if (Kind == "slime") BuildSlime();
        else BuildMushroom();
    }

    private void BuildMushroom()
    {
        Gfx.Cyl(_visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0.5f),
            Gfx.Mat(new Color(0.93f, 0.87f, 0.7f)), false);
        Gfx.Ball(_visual, new Vector3(0f, 0.62f, 0f), new Vector3(0.85f, 0.5f, 0.85f),
            Gfx.Mat(new Color(0.85f, 0.2f, 0.15f)));

        Material dots = Gfx.Mat(new Color(0.95f, 0.95f, 0.9f));
        Gfx.Ball(_visual, new Vector3(0.2f, 0.78f, 0.1f), new Vector3(0.12f, 0.08f, 0.12f), dots);
        Gfx.Ball(_visual, new Vector3(-0.18f, 0.76f, -0.12f), new Vector3(0.1f, 0.07f, 0.1f), dots);
        Gfx.Ball(_visual, new Vector3(0f, 0.8f, -0.2f), new Vector3(0.09f, 0.06f, 0.09f), dots);

        Material eye = Gfx.Mat(new Color(0.1f, 0.07f, 0.06f));
        Gfx.Ball(_visual, new Vector3(-0.1f, 0.42f, 0.22f), new Vector3(0.08f, 0.1f, 0.05f), eye);
        Gfx.Ball(_visual, new Vector3(0.1f, 0.42f, 0.22f), new Vector3(0.08f, 0.1f, 0.05f), eye);
    }

    private void BuildSlime()
    {
        Material body = Gfx.MatFull(new Color(0.5f, 0.78f, 1f), 0.85f, 0.15f,
            new Color(0.12f, 0.28f, 0.55f), 0f, 0f);
        Gfx.Ball(_visual, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.75f, 0.9f), body);

        Material eye = Gfx.Mat(new Color(0.08f, 0.1f, 0.2f));
        Gfx.Ball(_visual, new Vector3(-0.14f, 0.5f, 0.35f), new Vector3(0.1f, 0.12f, 0.06f), eye);
        Gfx.Ball(_visual, new Vector3(0.14f, 0.5f, 0.35f), new Vector3(0.1f, 0.12f, 0.06f), eye);
        Gfx.Glow(_visual, new Vector3(0f, 0.4f, 0f), 1.5f, new Color(0.4f, 0.7f, 1f, 0.35f));
    }

    // Патрулирование считает только хост.
    public void HostStep(float dt)
    {
        if (Dying) return;
        Vector3 pos = transform.localPosition;
        Vector3 to = _target - pos;
        if (to.magnitude < 0.08f)
        {
            _target = (_target - PointB).sqrMagnitude < 0.001f ? PointA : PointB;
            to = _target - pos;
        }
        transform.localPosition = Vector3.MoveTowards(pos, _target, Speed * dt);
        if (to.magnitude > 0.01f)
            _visual.localRotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
    }

    public void SetNetState(Vector3 pos, float yaw)
    {
        _netPos = pos;
        _netYaw = yaw;
        _hasNet = true;
    }

    private void Update()
    {
        if (_dieT >= 0f)
        {
            _dieT += Time.deltaTime;
            float k = Mathf.Clamp01(_dieT / 0.25f);
            _visual.localScale = new Vector3(1f + k * 0.4f, 1f - k * 0.9f, 1f + k * 0.4f);
            if (k >= 1f) Object.Destroy(gameObject);
            return;
        }

        if (Dying) return;

        NetManager net = NetManager.I;
        bool remote = net != null && net.Online && !net.IsHost;
        if (remote && _hasNet)
        {
            transform.position = Vector3.Lerp(transform.position, _netPos, Mathf.Min(Time.deltaTime * 10f, 1f));
            if ((transform.position - _netPos).magnitude > 8f) transform.position = _netPos;
            float cur = _visual.localEulerAngles.y;
            _visual.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(cur, _netYaw, Mathf.Min(Time.deltaTime * 10f, 1f)), 0f);
        }

        _bob += Time.deltaTime * 6f;
        Vector3 s = _visual.localScale;
        s.y = 1f + Mathf.Sin(_bob) * 0.05f;
        _visual.localScale = new Vector3(1f, s.y, 1f);
    }

    public void DieEffect()
    {
        if (Dying) return;
        Dying = true;
        _dieT = 0f;
        Snd.Play("stomp");
        Color tint = Kind == "slime" ? new Color(0.5f, 0.75f, 1f) : new Color(0.9f, 0.3f, 0.2f);
        Transform root = transform.parent != null ? transform.parent : null;
        ParticleFx.Burst(root, transform.position + new Vector3(0f, 0.5f, 0f), 14, tint, 4.5f);
    }
}
