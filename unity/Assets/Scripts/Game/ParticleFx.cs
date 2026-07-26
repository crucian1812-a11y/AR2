using UnityEngine;

// Лёгкая система частиц на процедурном меше из billboard-квадов.
// Используется для снега, пыльцы, листьев, брызг фонтана и взрывов врагов.
public class ParticleFx : MonoBehaviour
{
    private struct P
    {
        public Vector3 Pos;
        public Vector3 Vel;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Angle;
        public float Spin;
        public bool Alive;
    }

    // Меш строится в мировых координатах, поэтому сам объект всегда стоит
    // в начале координат, а точка испускания хранится отдельно.
    public Vector3 Origin;
    public Vector3 EmitCenter;
    public Vector3 EmitExtents = new Vector3(1f, 1f, 1f);
    public Vector3 Gravity = new Vector3(0f, -1f, 0f);
    public Vector3 BaseVelocity = Vector3.zero;
    public float SpeedMin = 0.2f;
    public float SpeedMax = 0.6f;
    public float SizeMin = 0.05f;
    public float SizeMax = 0.12f;
    public float LifeMin = 3f;
    public float LifeMax = 6f;
    public float SpinMax;
    public Color Tint = Color.white;
    public bool Loop = true;
    public bool FadeOut = true;
    public bool SphereEmit;

    private P[] _parts;
    private Mesh _mesh;
    private Vector3[] _verts;
    private Vector2[] _uvs;
    private Color[] _colors;
    private int[] _tris;
    private MeshRenderer _mr;
    private bool _ready;
    private int _aliveCount;

    public static ParticleFx Spawn(Transform parent, Vector3 worldPos, int count, Color tint)
    {
        GameObject go = new GameObject("ParticleFx");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        ParticleFx fx = go.AddComponent<ParticleFx>();
        fx.Origin = worldPos;
        fx.Tint = tint;
        fx.Setup(count);
        return fx;
    }

    // Взрыв врага: короткая одноразовая вспышка частиц.
    public static void Burst(Transform parent, Vector3 worldPos, int count, Color tint, float speed)
    {
        ParticleFx fx = Spawn(parent, worldPos, count, tint);
        fx.Loop = false;
        fx.SphereEmit = true;
        fx.EmitExtents = new Vector3(0.3f, 0.3f, 0.3f);
        fx.Gravity = new Vector3(0f, -7f, 0f);
        fx.SpeedMin = speed * 0.6f;
        fx.SpeedMax = speed;
        fx.SizeMin = 0.08f;
        fx.SizeMax = 0.18f;
        fx.LifeMin = 0.35f;
        fx.LifeMax = 0.6f;
        fx.Restart();
    }

    public void Setup(int count)
    {
        count = Mathf.Clamp(count, 1, 600);
        _parts = new P[count];
        _verts = new Vector3[count * 4];
        _uvs = new Vector2[count * 4];
        _colors = new Color[count * 4];
        _tris = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            int v = i * 4;
            _uvs[v] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(1f, 0f);
            _uvs[v + 2] = new Vector2(1f, 1f);
            _uvs[v + 3] = new Vector2(0f, 1f);
            int t = i * 6;
            _tris[t] = v; _tris[t + 1] = v + 2; _tris[t + 2] = v + 1;
            _tris[t + 3] = v; _tris[t + 4] = v + 3; _tris[t + 5] = v + 2;
        }

        _mesh = new Mesh();
        _mesh.MarkDynamic();
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = _mesh;
        _mr = gameObject.AddComponent<MeshRenderer>();
        _mr.sharedMaterial = Gfx.AdditiveMat(Tint, Gfx.GlowTexture());
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows = false;
        _ready = true;
    }

    // Заполняет все частицы (для непрерывных эффектов — с разбросом по времени).
    public void Prewarm()
    {
        if (!_ready) return;
        for (int i = 0; i < _parts.Length; i++)
        {
            Respawn(ref _parts[i]);
            _parts[i].Life = Random.Range(0f, _parts[i].MaxLife);
            float t = _parts[i].MaxLife - _parts[i].Life;
            _parts[i].Pos += _parts[i].Vel * t + Gravity * (0.5f * t * t);
            _parts[i].Vel += Gravity * t;
        }
        _aliveCount = _parts.Length;
    }

    public void Restart()
    {
        if (!_ready) return;
        for (int i = 0; i < _parts.Length; i++) Respawn(ref _parts[i]);
        _aliveCount = _parts.Length;
    }

    private void Respawn(ref P p)
    {
        Vector3 local;
        if (SphereEmit)
        {
            local = Random.insideUnitSphere;
            local = new Vector3(local.x * EmitExtents.x, local.y * EmitExtents.y, local.z * EmitExtents.z);
        }
        else
        {
            local = new Vector3(
                Random.Range(-EmitExtents.x, EmitExtents.x),
                Random.Range(-EmitExtents.y, EmitExtents.y),
                Random.Range(-EmitExtents.z, EmitExtents.z));
        }
        p.Pos = Origin + EmitCenter + local;

        Vector3 dir = Random.insideUnitSphere.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
        p.Vel = BaseVelocity + dir * Random.Range(SpeedMin, SpeedMax);
        p.MaxLife = Random.Range(LifeMin, LifeMax);
        p.Life = p.MaxLife;
        p.Size = Random.Range(SizeMin, SizeMax);
        p.Angle = Random.Range(0f, Mathf.PI * 2f);
        p.Spin = SpinMax > 0f ? Random.Range(-SpinMax, SpinMax) : 0f;
        p.Alive = true;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        float dt = Time.deltaTime;
        Vector3 camRight = cam.transform.right;
        Vector3 camUp = cam.transform.up;

        _aliveCount = 0;
        for (int i = 0; i < _parts.Length; i++)
        {
            int v = i * 4;
            if (!_parts[i].Alive)
            {
                _verts[v] = Vector3.zero; _verts[v + 1] = Vector3.zero;
                _verts[v + 2] = Vector3.zero; _verts[v + 3] = Vector3.zero;
                continue;
            }

            _parts[i].Life -= dt;
            if (_parts[i].Life <= 0f)
            {
                if (Loop) Respawn(ref _parts[i]);
                else
                {
                    _parts[i].Alive = false;
                    _verts[v] = Vector3.zero; _verts[v + 1] = Vector3.zero;
                    _verts[v + 2] = Vector3.zero; _verts[v + 3] = Vector3.zero;
                    continue;
                }
            }

            _parts[i].Vel += Gravity * dt;
            _parts[i].Pos += _parts[i].Vel * dt;
            _parts[i].Angle += _parts[i].Spin * dt;
            _aliveCount++;

            float half = _parts[i].Size * 0.5f;
            Vector3 r = camRight;
            Vector3 u = camUp;
            if (_parts[i].Spin != 0f)
            {
                float c = Mathf.Cos(_parts[i].Angle);
                float s = Mathf.Sin(_parts[i].Angle);
                r = camRight * c + camUp * s;
                u = camUp * c - camRight * s;
            }
            r = r * half;
            u = u * half;

            Vector3 pos = _parts[i].Pos;
            _verts[v] = pos - r - u;
            _verts[v + 1] = pos + r - u;
            _verts[v + 2] = pos + r + u;
            _verts[v + 3] = pos - r + u;

            float alpha = FadeOut ? Mathf.Clamp01(_parts[i].Life / Mathf.Max(_parts[i].MaxLife, 0.001f)) : 1f;
            alpha = Mathf.Clamp01(alpha * 1.6f);
            Color c2 = new Color(Tint.r, Tint.g, Tint.b, Tint.a * alpha);
            _colors[v] = c2; _colors[v + 1] = c2; _colors[v + 2] = c2; _colors[v + 3] = c2;
        }

        if (!Loop && _aliveCount == 0)
        {
            Object.Destroy(gameObject);
            return;
        }

        _mesh.vertices = _verts;
        _mesh.colors = _colors;
        _mesh.uv = _uvs;
        _mesh.triangles = _tris;
        // Границы задаём вручную: вершины лежат в мировых координатах,
        // автоматический bounding box приводил бы к неверному отсечению.
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(600f, 600f, 600f));
    }
}
