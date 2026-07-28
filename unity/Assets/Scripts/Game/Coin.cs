using UnityEngine;

// Золотая монета: вращается, покачивается, светится ореолом.
public class Coin : MonoBehaviour
{
    public int Id;
    public bool IsStar;
    public bool IsHeart;

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
        c.IsStar = NetManager.IsStarId(id);
        c.IsHeart = NetManager.IsHeartId(id);
        if (c.IsHeart) c.BuildHeart();
        else if (c.IsStar) c.BuildStar();
        else c.BuildVisual();
        return c;
    }

    // Сердце: восстанавливает единицу здоровья. Без него единственным
    // способом полечиться был прыжок в пропасть.
    private void BuildHeart()
    {
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        Material red = Gfx.MatFull(new Color(1f, 0.3f, 0.4f), 0.6f, 0f,
            new Color(0.8f, 0.1f, 0.2f), 0f, 0f);
        Gfx.Ball(_visual, new Vector3(-0.17f, 0.12f, 0f), new Vector3(0.4f, 0.4f, 0.4f), red);
        Gfx.Ball(_visual, new Vector3(0.17f, 0.12f, 0f), new Vector3(0.4f, 0.4f, 0.4f), red);
        GameObject tip = Gfx.Box(_visual, new Vector3(0f, -0.14f, 0f),
            new Vector3(0.42f, 0.42f, 0.28f), red, false);
        tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Gfx.Glow(transform, Vector3.zero, 2.6f, new Color(1f, 0.4f, 0.5f, 0.6f));
        _baseY = transform.localPosition.y;
    }

    // Звезда — главная награда мира: крупнее, ярче и со своим светом.
    private void BuildStar()
    {
        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        Material star = Gfx.MatFull(new Color(1f, 0.9f, 0.35f), 0.85f, 0.55f,
            new Color(1f, 0.72f, 0.15f), 0f, 0f);
        // Пять лучей из вытянутых кристаллов.
        for (int i = 0; i < 5; i++)
        {
            GameObject ray = Gfx.Crystal(_visual, Vector3.zero, 0.2f, 1.5f, star, 0f);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 72f);
        }
        Gfx.Ball(_visual, Vector3.zero, new Vector3(0.44f, 0.44f, 0.44f), star);
        Gfx.Glow(transform, Vector3.zero, 5f, new Color(1f, 0.88f, 0.4f, 0.75f));
        Gfx.PointLight(transform, Vector3.zero, new Color(1f, 0.85f, 0.4f), 14f, 1.6f);
        _baseY = transform.localPosition.y;
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
        // Шар радиуса 1.4 от подошв давал на высоте монеты меньше метра
        // по горизонтали — пробегая мимо, её легко было не задеть.
        // Цилиндр вокруг тела прощает гораздо больше.
        Vector3 d = transform.position - (playerPos + new Vector3(0f, 0.85f, 0f));
        if (new Vector2(d.x, d.z).sqrMagnitude > 1.3f * 1.3f) return false;
        if (Mathf.Abs(d.y) > 1.5f) return false;
        _requested = true;
        return true;
    }

    public void CollectEffect()
    {
        if (_collecting) return;
        _collecting = true;
        _fx = 0f;
        Snd.Play(IsHeart ? "quest" : (IsStar ? "star" : "coin"), 1f, Random.Range(0.95f, 1.1f));
    }
}
