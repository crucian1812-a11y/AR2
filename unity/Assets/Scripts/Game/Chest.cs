using UnityEngine;

// Сундук: открывается при подходе, отдаёт монеты и материалы и остаётся
// открытым навсегда.
//
// Живёт в том же наборе «собранного», что и монеты, но со своим
// диапазоном ID (ChestIdBase) — как звёзды и сердца. Ни новой ветки в
// сети, ни новой секции в сохранении для этого не понадобилось.
public class Chest : MonoBehaviour
{
    public int Id;
    public int Coins = 5;
    public int ResKind = Res.Wood;
    public int ResAmount = 3;

    private Transform _lid;
    private bool _opened;
    private float _anim;
    private GameObject _glow;

    public static Chest Create(Transform parent, Vector3 pos, float yaw, int id,
        int coins, int resKind, int resAmount)
    {
        GameObject go = new GameObject("Chest_" + id);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Chest c = go.AddComponent<Chest>();
        c.Id = id;
        c.Coins = coins;
        c.ResKind = resKind;
        c.ResAmount = resAmount;
        c.Build();
        return c;
    }

    private void Build()
    {
        // Собственный ассет из tools/blender; крышка отдельным узлом,
        // чтобы её можно было откинуть.
        GameObject body = Gfx.CustomProp(transform, "treasure_chest", Vector3.zero, 1.25f, 0f);
        if (body == null)
        {
            Material wood = Gfx.Mat(new Color(0.45f, 0.28f, 0.14f), 0.06f);
            Gfx.Box(transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f), wood);
        }

        GameObject lidGo = new GameObject("Lid");
        lidGo.transform.SetParent(transform, false);
        lidGo.transform.localPosition = new Vector3(0f, 0.78f, -0.42f);
        _lid = lidGo.transform;
        Material gold = Gfx.MatFull(new Color(0.85f, 0.66f, 0.22f), 0.35f, 0.85f,
            Color.black, 0f, 0f);
        Gfx.Box(_lid, new Vector3(0f, 0f, 0.42f), new Vector3(1.16f, 0.12f, 0.84f), gold, false);

        _glow = Gfx.Glow(transform, new Vector3(0f, 0.7f, 0f), 2.4f,
            new Color(1f, 0.85f, 0.4f, 0.45f));
        WorldLabel.Attach(transform, "Сундук", new Vector3(0f, 1.5f, 0f),
            new Color(1f, 0.88f, 0.5f), 18);
    }

    // Восстановление из сохранения: открыт, но без наград и звука.
    public void SetOpened()
    {
        _opened = true;
        _anim = 1f;
        if (_lid != null) _lid.localRotation = Quaternion.Euler(-102f, 0f, 0f);
        if (_glow != null) Object.Destroy(_glow);
    }

    public bool TryOpen(Vector3 playerPos)
    {
        if (_opened) return false;
        Vector3 d = transform.position - playerPos;
        if (new Vector2(d.x, d.z).magnitude > 1.9f) return false;
        if (Mathf.Abs(d.y) > 2f) return false;

        _opened = true;
        Snd.Play("quest", 1f);
        ParticleFx.Burst(transform.parent, transform.position + new Vector3(0f, 1f, 0f),
            22, new Color(1f, 0.88f, 0.45f), 5f);
        if (_glow != null) Object.Destroy(_glow);
        return true;
    }

    private void Update()
    {
        if (!_opened || _anim >= 1f) return;
        _anim = Mathf.Min(1f, _anim + Time.deltaTime * 2.4f);
        // Крышка откидывается с перелётом — так открытие читается живее.
        float k = 1f - Mathf.Pow(1f - _anim, 3f);
        if (_lid != null)
            _lid.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -102f, k), 0f, 0f);
    }
}
