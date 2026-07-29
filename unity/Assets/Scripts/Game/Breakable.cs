using UnityEngine;

// Добываемый объект: дерево, валун, рудная жила. Ломается ударом за
// несколько попаданий и роняет материал тому, кто его добил.
//
// Устроен как враг: свой ID в мире, разрушение подтверждает хост, набор
// сломанного лежит рядом с наборами собранных монет и побеждённых врагов
// — значит, и синхронизация, и сохранение достаются даром.
public class Breakable : MonoBehaviour
{
    public int Id;
    public int Kind;          // Res.Wood / Stone / Iron / Crystal
    public int Amount = 1;
    public int Hp = 2;
    public float Radius = 1.2f;
    public float Height = 2f;

    private Transform _visual;
    private float _shake;
    private bool _dead;

    public static Breakable Attach(GameObject target, int id, int kind, int amount,
        int hp, float radius, float height)
    {
        if (target == null) return null;
        Breakable b = target.AddComponent<Breakable>();
        b.Id = id;
        b.Kind = kind;
        b.Amount = Mathf.Max(1, amount);
        b.Hp = Mathf.Max(1, hp);
        b.Radius = radius;
        b.Height = height;
        b._visual = target.transform;
        return b;
    }

    public Vector3 Center
    {
        get { return transform.position + new Vector3(0f, Height * 0.5f, 0f); }
    }

    // Попадание. Возвращает true, если объект разрушен.
    public bool Hit()
    {
        if (_dead) return true;
        Hp--;
        _shake = 0.22f;
        Snd.Play("bosshit", 0.6f);
        ParticleFx.Burst(transform.parent, Center, 8, Res.Tint(Kind), 3.2f);
        if (Hp > 0) return false;

        _dead = true;
        return true;
    }

    // Разрушение подтверждено: осколки и материал.
    public void BreakEffect()
    {
        _dead = true;
        Snd.Play("stomp", 0.9f);
        ParticleFx.Burst(transform.parent, Center, 18, Res.Tint(Kind), 5f);
        Object.Destroy(gameObject);
    }

    private void Update()
    {
        if (_shake <= 0f) return;
        _shake -= Time.deltaTime * 3f;
        float k = Mathf.Max(0f, _shake);
        // Короткая дрожь: видно, что удар засчитан, но объект ещё цел.
        _visual.localRotation = Quaternion.Euler(
            Mathf.Sin(Time.time * 60f) * k * 6f, _visual.localEulerAngles.y, 0f);
    }
}
