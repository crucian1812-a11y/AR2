using System.Collections.Generic;
using UnityEngine;

// Область воды, в которой игрок плывёт. Регистрируется вместе с каждой
// водной поверхностью мира: сама поверхность — только вид, физика живёт
// здесь. Зона прямоугольная, глубина считается вниз от поверхности.
public class WaterZone : MonoBehaviour
{
    public static readonly List<WaterZone> All = new List<WaterZone>();

    public float SurfaceY;
    public float Depth = 12f;
    private Vector2 _half;

    public static WaterZone Create(Transform parent, Vector3 pos, Vector2 size, float depth)
    {
        GameObject go = new GameObject("WaterZone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        WaterZone z = go.AddComponent<WaterZone>();
        z.SurfaceY = go.transform.position.y;
        z._half = size * 0.5f;
        z.Depth = depth;
        All.Add(z);
        return z;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    public bool Contains(Vector3 p)
    {
        Vector3 c = transform.position;
        if (p.y > SurfaceY + 0.2f) return false;
        if (p.y < SurfaceY - Depth) return false;
        return Mathf.Abs(p.x - c.x) <= _half.x && Mathf.Abs(p.z - c.z) <= _half.y;
    }

    // Уровень поверхности воды под точкой, или float.MinValue вне воды.
    public static float SurfaceAt(Vector3 p)
    {
        float best = float.MinValue;
        for (int i = 0; i < All.Count; i++)
        {
            WaterZone z = All[i];
            if (z == null || !z.Contains(p)) continue;
            if (z.SurfaceY > best) best = z.SurfaceY;
        }
        return best;
    }
}
