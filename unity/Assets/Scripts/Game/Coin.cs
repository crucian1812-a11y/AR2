using UnityEngine;

// Золотая монета: вращается, покачивается, светится ореолом.
public class Coin : MonoBehaviour
{
    public int Id;

    private Transform _visual;
    private float _baseY;
    private float _t;
    private bool _collecting;
    private float _fx;
    private bool _requested;

    public static Coin Spawn(Transform parent, Vector3 pos, int id)
    {
        GameObject go = new GameObject("Coin_" + id);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Coin c = go.AddComponent<Coin>();
        c.Id = id;
        c.BuildVisual();
        return c;
    }

    private void BuildVisual()
    {
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        Material gold = Gfx.MatFull(new Color(1f, 0.85f, 0.2f), 0.8f, 0.8f,
            new Color(0.7f, 0.5f, 0.05f), 0f, 0f);
        GameObject disc = Gfx.Cyl(_visual, Vector3.zero, new Vector3(0.64f, 0.04f, 0.64f), gold, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Material core = Gfx.MatFull(new Color(1f, 0.95f, 0.55f), 0.6f, 0.3f,
            new Color(0.9f, 0.75f, 0.2f), 0f, 0f);
        GameObject inner = Gfx.Cyl(_visual, Vector3.zero, new Vector3(0.4f, 0.05f, 0.4f), core, false);
        inner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Gfx.Glow(transform, Vector3.zero, 1.5f, new Color(1f, 0.8f, 0.25f, 0.75f));

        _baseY = transform.localPosition.y;
        _t = Random.Range(0f, 6.28f);
    }

    private void Update()
    {
        if (_collecting)
        {
            _fx += Time.deltaTime;
            float k = Mathf.Clamp01(_fx / 0.2f);
            transform.localScale = Vector3.one * (1f + k * 1.4f);
            Vector3 p = transform.localPosition;
            p.y = _baseY + k * 1.2f;
            transform.localPosition = p;
            if (k >= 1f) Object.Destroy(gameObject);
            return;
        }

        _t += Time.deltaTime;
        _visual.localRotation = Quaternion.Euler(0f, _t * 145f, 0f);
        Vector3 pos = transform.localPosition;
        pos.y = _baseY + Mathf.Sin(_t * 2f) * 0.12f;
        transform.localPosition = pos;
    }

    // Возвращает true, если игрок достаточно близко и запрос ещё не отправлен.
    public bool TryPickup(Vector3 playerPos)
    {
        if (_collecting || _requested) return false;
        if ((transform.position - playerPos).sqrMagnitude > 1.4f * 1.4f) return false;
        _requested = true;
        return true;
    }

    public void CollectEffect()
    {
        if (_collecting) return;
        _collecting = true;
        _fx = 0f;
        Snd.Play("coin", 1f, Random.Range(0.95f, 1.1f));
    }
}
