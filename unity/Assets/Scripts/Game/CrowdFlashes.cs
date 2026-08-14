using UnityEngine;

// Вспышки фотокамер на трибунах.
//
// Приём копеечный, а работает сильнее многого дорогого: в тёмном зале
// редкие короткие искры читаются как люди со смартфонами, то есть как
// событие, на которое смотрят. Ровно этого не хватает статичной толпе.
public class CrowdFlashes : MonoBehaviour
{
    private const int Count = 26;

    private Transform[] _quads;
    private float[] _next;
    private float[] _life;

    public void Build()
    {
        _quads = new Transform[Count];
        _next = new float[Count];
        _life = new float[Count];

        Material glow = Arena.Glow(new Color(1f, 0.97f, 0.92f), 2.4f);

        for (int i = 0; i < Count; i++)
        {
            // Вспышки сидят по кольцу вокруг татами, на высоте трибун.
            float angle = (i / (float)Count) * Mathf.PI * 2f + Random.value * 0.2f;
            float dist = Random.Range(8.2f, 11.5f);
            float height = Random.Range(0.8f, 2.4f);

            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Flash";
            q.transform.SetParent(transform, false);
            q.transform.localPosition =
                new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
            q.transform.localScale = Vector3.one * Random.Range(0.18f, 0.34f);

            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Object.Destroy(q.GetComponent<Collider>());

            _quads[i] = q.transform;
            // Разброс по времени: одновременная вспышка всех читается
            // как мигание лампы, а не как толпа.
            _next[i] = Random.Range(0.2f, 7f);
            q.SetActive(false);
        }
    }

    private void Update()
    {
        if (_quads == null) return;

        float dt = Time.unscaledDeltaTime;
        Camera cam = Camera.main;

        for (int i = 0; i < _quads.Length; i++)
        {
            if (_life[i] > 0f)
            {
                _life[i] -= dt;
                if (_life[i] <= 0f) _quads[i].gameObject.SetActive(false);
                else if (cam != null)
                {
                    // Разворот к камере: плоский квад иначе исчезает при
                    // взгляде сбоку.
                    _quads[i].rotation = cam.transform.rotation;
                }
                continue;
            }

            _next[i] -= dt;
            if (_next[i] <= 0f)
            {
                _next[i] = Random.Range(2.5f, 9f);
                _life[i] = Random.Range(0.05f, 0.10f);
                _quads[i].gameObject.SetActive(true);
            }
        }
    }
}
