using UnityEngine;

// Золотая звезда на вершине — финальная цель задания.
public class StarGoal : MonoBehaviour
{
    private Transform _core;
    private float _t;
    private bool _reached;

    public static StarGoal Spawn(Transform parent, Vector3 pos)
    {
        GameObject go = new GameObject("Star");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        StarGoal s = go.AddComponent<StarGoal>();
        s.Build();
        return s;
    }

    private void Build()
    {
        GameObject core = new GameObject("Core");
        core.transform.SetParent(transform, false);
        _core = core.transform;

        Material gold = Gfx.MatFull(new Color(1f, 0.9f, 0.3f), 0.75f, 0.6f,
            new Color(1f, 0.75f, 0.15f), 0f, 0f);
        Gfx.Ball(_core, Vector3.zero, new Vector3(0.75f, 0.75f, 0.55f), gold);

        for (int i = 0; i < 5; i++)
        {
            float ang = Mathf.PI * 2f * i / 5f;
            GameObject spike = Gfx.Cone(_core, Vector3.zero, 0.26f, 0.85f, gold);
            spike.transform.localRotation = Quaternion.Euler(0f, 0f, -ang * Mathf.Rad2Deg);
            spike.transform.localPosition = new Vector3(Mathf.Sin(ang) * 0.2f, Mathf.Cos(ang) * 0.2f, 0f);
        }

        Gfx.Glow(transform, Vector3.zero, 5f, new Color(1f, 0.85f, 0.3f, 0.7f));

        ParticleFx fx = ParticleFx.Spawn(transform, transform.position, 30, new Color(1f, 0.9f, 0.45f, 0.9f));
        fx.EmitExtents = new Vector3(0.9f, 0.9f, 0.9f);
        fx.SphereEmit = true;
        fx.Gravity = new Vector3(0f, 0.25f, 0f);
        fx.SpeedMin = 0.35f;
        fx.SpeedMax = 1.1f;
        fx.SizeMin = 0.07f;
        fx.SizeMax = 0.16f;
        fx.LifeMin = 1.2f;
        fx.LifeMax = 2.2f;
        fx.Prewarm();

        WorldLabel.Attach(transform, "Вершина!", new Vector3(0f, 2f, 0f),
            new Color(1f, 0.95f, 0.6f), 28);
    }

    private void Update()
    {
        _t += Time.deltaTime;
        _core.localRotation = Quaternion.Euler(0f, _t * 85f, 0f);
        _core.localPosition = new Vector3(0f, Mathf.Sin(_t * 2f) * 0.2f + 0.2f, 0f);
    }

    public bool TryReach(Vector3 playerPos)
    {
        if (_reached) return false;
        if ((transform.position - playerPos).sqrMagnitude > 2f * 2f) return false;
        _reached = true;
        return true;
    }
}
