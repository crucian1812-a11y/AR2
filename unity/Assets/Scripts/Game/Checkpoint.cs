using System.Collections.Generic;
using UnityEngine;

// Флаг-чекпоинт: при касании становится точкой возрождения. До этого
// смерть на подъёме отбрасывала к самому входу в мир.
public class Checkpoint : MonoBehaviour
{
    public static readonly List<Checkpoint> All = new List<Checkpoint>();

    private bool _active;
    private Material _clothMat;
    private Transform _cloth;
    private float _t;

    public static Checkpoint Create(Transform parent, Vector3 pos)
    {
        GameObject go = new GameObject("Checkpoint");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Checkpoint c = go.AddComponent<Checkpoint>();
        c.Build();
        return c;
    }

    private void Build()
    {
        Material pole = Gfx.MatFull(new Color(0.5f, 0.42f, 0.3f), 0.1f, 0f, Color.black, 2f, 0.4f);
        Gfx.Cyl(transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.18f, 1.6f, 0.18f), pole, false);
        Gfx.Cyl(transform, new Vector3(0f, 0.08f, 0f), new Vector3(1.1f, 0.08f, 1.1f), pole, false);

        _clothMat = Gfx.MatFull(new Color(0.7f, 0.7f, 0.75f), 0.2f, 0f,
            new Color(0.1f, 0.1f, 0.12f), 0f, 0f);
        GameObject cloth = Gfx.Box(transform, new Vector3(0.95f, 2.65f, 0f),
            new Vector3(1.8f, 1.1f, 0.07f), _clothMat, false);
        Gfx.NoShadow(cloth);
        _cloth = cloth.transform;

        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    public bool Active { get { return _active; } }

    public void Activate()
    {
        if (_active) return;
        _active = true;
        // Флаг вспыхивает цветом и поднимается — видно, что засчитан.
        _clothMat.color = new Color(0.35f, 0.95f, 0.55f);
        _clothMat.SetColor("_EmissionColor", new Color(0.2f, 0.7f, 0.35f));
        Gfx.Glow(transform, new Vector3(0f, 2.7f, 0f), 4f, new Color(0.4f, 1f, 0.6f, 0.5f));
        Gfx.PointLight(transform, new Vector3(0f, 2.7f, 0f), new Color(0.4f, 1f, 0.6f), 10f, 1.2f);
        Snd.Play("coin", 0.8f);

        ParticleFx fx = ParticleFx.Spawn(transform.parent, transform.position + new Vector3(0f, 1.5f, 0f),
            20, new Color(0.5f, 1f, 0.7f, 0.9f));
        fx.BaseVelocity = new Vector3(0f, 2.5f, 0f);
        fx.Gravity = new Vector3(0f, -3f, 0f);
        fx.LifeMin = 0.5f;
        fx.LifeMax = 1f;
    }

    private void Update()
    {
        // Полотнище полощется на ветру.
        _t += Time.deltaTime * (_active ? 3.2f : 1.6f);
        if (_cloth != null)
            _cloth.localRotation = Quaternion.Euler(0f, Mathf.Sin(_t) * 9f, Mathf.Sin(_t * 1.3f) * 4f);
    }

    // Возвращает true, если игрок только что активировал флаг.
    public bool TryReach(Vector3 playerPos)
    {
        if (_active) return false;
        Vector3 d = transform.position - playerPos;
        d.y = 0f;
        if (d.sqrMagnitude > 2.2f * 2.2f) return false;
        if (Mathf.Abs(transform.position.y - playerPos.y) > 3f) return false;
        Activate();
        return true;
    }
}
