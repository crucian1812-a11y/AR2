using System.Collections.Generic;
using UnityEngine;

// Скользкая поверхность. Пока миры отличались только палитрой, снега были
// теми же лугами в белом: одна и та же походка, одни и те же прыжки.
// На льду медведь разгоняется и тормозит куда медленнее, и знакомая
// цепочка платформ становится другой задачей.
//
// Зона плоская и прямоугольная — она накрывает каток сверху и работает,
// пока игрок стоит на нём, а не проходит под ним.
public class IceZone : MonoBehaviour
{
    public static readonly List<IceZone> All = new List<IceZone>();

    // Во сколько раз медленнее набирается и гасится горизонтальная скорость.
    public float Slip = 0.18f;

    public float SurfaceY;
    private Vector2 _half;

    public static IceZone Create(Transform parent, Vector3 pos, Vector2 size, float slip)
    {
        GameObject go = new GameObject("IceZone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        IceZone z = go.AddComponent<IceZone>();
        z.SurfaceY = go.transform.position.y;
        z._half = size * 0.5f;
        z.Slip = slip;
        All.Add(z);
        return z;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private bool Contains(Vector3 p)
    {
        Vector3 c = transform.position;
        // Считаем по подошвам: полметра выше катка — ещё лёд, метр — уже нет.
        if (p.y > SurfaceY + 1f || p.y < SurfaceY - 1.5f) return false;
        return Mathf.Abs(p.x - c.x) <= _half.x && Mathf.Abs(p.z - c.z) <= _half.y;
    }

    // Множитель сцепления под точкой: 1 — обычная земля, меньше — скользко.
    public static float GripAt(Vector3 p)
    {
        float grip = 1f;
        for (int i = 0; i < All.Count; i++)
        {
            IceZone z = All[i];
            if (z == null || !z.Contains(p)) continue;
            if (z.Slip < grip) grip = z.Slip;
        }
        return grip;
    }
}
