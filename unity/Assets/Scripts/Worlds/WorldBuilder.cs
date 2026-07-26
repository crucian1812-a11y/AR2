using System.Collections.Generic;
using UnityEngine;

// База для всех миров: небо, свет, туман и помощники для построения
// ландшафта, растительности, воды и игровых объектов.
// Наследники переопределяют Build().
public abstract class WorldBuilder : MonoBehaviour
{
    public Vector3 SpawnPoint = new Vector3(0f, 1.5f, 6f);

    public readonly Dictionary<int, Coin> Coins = new Dictionary<int, Coin>();
    public readonly Dictionary<int, Enemy> Enemies = new Dictionary<int, Enemy>();
    public readonly List<Portal> Portals = new List<Portal>();

    public Npc Elder;
    public StarGoal Star;

    private int _coinCounter;
    private int _enemyCounter;
    private readonly List<EnemyState> _stateScratch = new List<EnemyState>();

    protected abstract void Build();

    public void Construct(int worldIndex)
    {
        // Детерминированная генерация: декор одинаков у всех игроков.
        Random.InitState(1812 + worldIndex * 7919);
        Build();
        ApplyPersistedState(worldIndex);
    }

    private void ApplyPersistedState(int worldIndex)
    {
        NetManager net = NetManager.I;
        if (net == null) return;
        List<int> coinIds = new List<int>(Coins.Keys);
        for (int i = 0; i < coinIds.Count; i++)
            if (net.IsCollected(worldIndex, coinIds[i])) RemoveCoin(coinIds[i], false);
        List<int> enemyIds = new List<int>(Enemies.Keys);
        for (int i = 0; i < enemyIds.Count; i++)
            if (net.IsKilled(worldIndex, enemyIds[i])) RemoveEnemy(enemyIds[i], false);
    }

    // ---------- Небо, свет, туман ----------

    protected void SetupSky(Color top, Color horizon, Color ground, Vector3 sunEuler,
        float sunIntensity, float fogDensity, Color fogColor)
    {
        Shader skyShader = Shader.Find("Bear/Sky");
        if (skyShader != null)
        {
            Material sky = new Material(skyShader);
            sky.SetColor("_TopColor", top);
            sky.SetColor("_HorizonColor", horizon);
            sky.SetColor("_GroundColor", ground);
            RenderSettings.skybox = sky;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = top * 0.9f;
        RenderSettings.ambientEquatorColor = horizon * 0.8f;
        RenderSettings.ambientGroundColor = ground * 0.7f;

        RenderSettings.fog = fogDensity > 0f;
        if (fogDensity > 0f)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        GameObject sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(transform, false);
        sunGo.transform.localRotation = Quaternion.Euler(sunEuler);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = sunIntensity;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.shadowNormalBias = 0.05f;
    }

    // ---------- Ландшафт и декор ----------

    protected GameObject Ground(Vector3 pos, Vector3 size, Color color, float detail, float normal,
        float smoothness = 0.1f)
    {
        GameObject go = Gfx.Box(transform, pos, size, Gfx.MatFull(color, smoothness, 0f, Color.black, detail, normal));
        go.name = "Ground";
        return go;
    }

    protected GameObject Platform(Vector3 pos, Vector3 size, Color color, float smoothness = 0.1f,
        float metallic = 0f)
    {
        return Gfx.Box(transform, pos, size, Gfx.Mat(color, smoothness, metallic));
    }

    protected void Tree(Vector3 pos, Color leaf, float scale)
    {
        Material bark = Gfx.MatFull(new Color(0.42f, 0.28f, 0.15f), 0.1f, 0f, Color.black, 1.5f, 0.6f);
        Gfx.Cyl(transform, pos + new Vector3(0f, 1.2f * scale, 0f),
            new Vector3(0.6f * scale, 1.2f * scale, 0.6f * scale), bark);

        Material m1 = Gfx.Mat(leaf);
        Material m2 = Gfx.Mat(leaf * 1.12f);
        Material m3 = Gfx.Mat(leaf * 0.92f);
        Gfx.Ball(transform, pos + new Vector3(0f, 3.0f * scale, 0f), new Vector3(2.6f, 2.2f, 2.6f) * scale, m1);
        Gfx.Ball(transform, pos + new Vector3(0.9f, 2.4f, 0.4f) * scale, new Vector3(1.6f, 1.4f, 1.6f) * scale, m2);
        Gfx.Ball(transform, pos + new Vector3(-0.8f, 2.5f, -0.5f) * scale, new Vector3(1.5f, 1.3f, 1.5f) * scale, m3);
    }

    protected void Pine(Vector3 pos, bool snowy, float scale)
    {
        Material bark = Gfx.Mat(new Color(0.35f, 0.24f, 0.14f));
        Gfx.Cyl(transform, pos + new Vector3(0f, 0.8f * scale, 0f),
            new Vector3(0.44f * scale, 0.8f * scale, 0.44f * scale), bark);

        Color green = new Color(0.16f, 0.38f, 0.24f);
        float[] radii = { 1.2f, 0.95f, 0.65f };
        Material snowMat = Gfx.Mat(new Color(0.95f, 0.96f, 1f), 0.25f);
        for (int i = 0; i < 3; i++)
        {
            Material needles = Gfx.Mat(green * (1f + i * 0.12f));
            Gfx.Cone(transform, pos + new Vector3(0f, (1.6f + i * 1.0f) * scale, 0f),
                radii[i] * scale, 1.5f * scale, needles);
            if (snowy)
            {
                Gfx.Cone(transform, pos + new Vector3(0f, (2.35f + i * 1.0f) * scale, 0f),
                    radii[i] * 0.55f * scale, 0.7f * scale, snowMat);
            }
        }
    }

    protected void Rock(Vector3 pos, float size, Color color)
    {
        Material m = Gfx.MatFull(color, 0.05f, 0f, Color.black, 1.2f, 0.8f);
        Gfx.Ball(transform, pos, new Vector3(size, size * 0.7f, size * 0.9f), m, true);
    }

    protected void Bush(Vector3 pos, Color color)
    {
        Gfx.Ball(transform, pos + new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.8f, 1.1f), Gfx.Mat(color));
        Gfx.Ball(transform, pos + new Vector3(0.45f, 0.28f, 0.2f), new Vector3(0.7f, 0.55f, 0.7f), Gfx.Mat(color * 1.1f));
        Gfx.Ball(transform, pos + new Vector3(-0.4f, 0.3f, -0.15f), new Vector3(0.65f, 0.5f, 0.65f), Gfx.Mat(color * 0.93f));
    }

    protected void Flower(Vector3 pos, Color color)
    {
        Gfx.Cyl(transform, pos + new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.2f, 0.06f),
            Gfx.Mat(new Color(0.3f, 0.55f, 0.25f)), false);
        Gfx.Ball(transform, pos + new Vector3(0f, 0.45f, 0f), new Vector3(0.22f, 0.18f, 0.22f), Gfx.Mat(color));
    }

    protected void Mountain(Vector3 pos, float radius, float height, Color color, bool snowCap)
    {
        GameObject cone = Gfx.Cone(transform, pos, radius, height, Gfx.MatFull(color, 0.05f, 0f, Color.black, 0.2f, 0.5f));
        Gfx.NoShadow(cone);
        if (snowCap)
        {
            GameObject cap = Gfx.Cone(transform, pos + new Vector3(0f, height * 0.68f, 0f),
                radius * 0.34f, height * 0.32f, Gfx.Mat(new Color(0.96f, 0.97f, 1f), 0.2f));
            Gfx.NoShadow(cap);
        }
    }

    protected void Cloud(Vector3 pos, float scale, Color tint)
    {
        GameObject root = new GameObject("Cloud");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = pos;
        Material m = Gfx.Mat(tint, 0.05f);
        Vector3[] offsets = {
            new Vector3(0f, 0f, 0f), new Vector3(2.2f, 0.3f, 0.4f),
            new Vector3(-2.0f, 0.2f, -0.3f), new Vector3(0.8f, 0.8f, -0.5f)
        };
        Vector3[] scales = {
            new Vector3(4.0f, 1.6f, 2.6f), new Vector3(2.6f, 1.2f, 2.0f),
            new Vector3(2.4f, 1.1f, 1.8f), new Vector3(2.2f, 1.3f, 1.8f)
        };
        for (int i = 0; i < 4; i++)
        {
            GameObject part = Gfx.Ball(root.transform, offsets[i] * scale, scales[i] * scale, m);
            Gfx.NoShadow(part);
        }
        Drift drift = root.AddComponent<Drift>();
        drift.Speed = 0.6f;
        drift.Limit = 90f;
    }

    // ---------- Трава (один меш с колышущимися травинками) ----------

    protected void GrassField(Vector3 center, Vector2 extents, int count, Color baseCol, Color tipCol)
    {
        count = Mathf.Clamp(count, 1, 12000);
        Vector3[] verts = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        Vector2[] uv2 = new Vector2[count * 4];
        Color[] colors = new Color[count * 4];
        Vector3[] normals = new Vector3[count * 4];
        int[] tris = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            Vector3 p = center + new Vector3(
                Random.Range(-extents.x, extents.x), 0f, Random.Range(-extents.y, extents.y));
            float yaw = Random.Range(0f, Mathf.PI);
            float w = 0.09f * Random.Range(0.7f, 1.5f);
            float h = 0.42f * Random.Range(0.7f, 1.6f);
            float phase = Random.value;

            Vector3 side = new Vector3(Mathf.Cos(yaw) * w, 0f, Mathf.Sin(yaw) * w);
            Vector3 up = new Vector3(0f, h, 0f);

            int v = i * 4;
            verts[v] = p - side;
            verts[v + 1] = p + side;
            verts[v + 2] = p + side + up;
            verts[v + 3] = p - side + up;

            uvs[v] = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(1f, 0f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(0f, 1f);

            for (int k = 0; k < 4; k++)
            {
                uv2[v + k] = new Vector2(phase, 0f);
                normals[v + k] = Vector3.up;
            }

            colors[v] = baseCol;
            colors[v + 1] = baseCol;
            colors[v + 2] = tipCol;
            colors[v + 3] = tipCol;

            int t = i * 6;
            tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
            tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.uv2 = uv2;
        mesh.colors = colors;
        mesh.normals = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Grass");
        go.transform.SetParent(transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Shader grass = Shader.Find("Bear/Grass");
        mr.sharedMaterial = grass != null ? new Material(grass) : Gfx.Mat(baseCol);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------- Вода ----------

    protected void Water(Vector3 pos, Vector2 size)
    {
        const int seg = 16;
        int vcount = (seg + 1) * (seg + 1);
        Vector3[] verts = new Vector3[vcount];
        Vector2[] uvs = new Vector2[vcount];
        List<int> tris = new List<int>(seg * seg * 6);

        for (int z = 0; z <= seg; z++)
        {
            for (int x = 0; x <= seg; x++)
            {
                float fx = (float)x / seg;
                float fz = (float)z / seg;
                int idx = z * (seg + 1) + x;
                verts[idx] = new Vector3((fx - 0.5f) * size.x, 0f, (fz - 0.5f) * size.y);
                uvs[idx] = new Vector2(fx, fz);
            }
        }
        for (int z = 0; z < seg; z++)
        {
            for (int x = 0; x < seg; x++)
            {
                int a = z * (seg + 1) + x;
                int b = a + seg + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Water");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Shader ws = Shader.Find("Bear/Water");
        if (ws != null)
        {
            Material m = new Material(ws);
            m.SetTexture("_BumpMap", Gfx.WaterNormal());
            float tile = Mathf.Max(size.x, size.y) * 0.35f;
            m.SetTextureScale("_BumpMap", new Vector2(tile, tile));
            mr.sharedMaterial = m;
        }
        else
        {
            mr.sharedMaterial = Gfx.Mat(new Color(0.25f, 0.55f, 0.8f), 0.85f, 0.1f);
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------- Атмосферные частицы ----------

    protected void Motes(Vector3 center, Vector3 extents, int count, Color color, float size)
    {
        ParticleFx fx = ParticleFx.Spawn(transform, center, count, color);
        fx.EmitExtents = extents;
        fx.Gravity = new Vector3(0.1f, 0.05f, 0.08f);
        fx.SpeedMin = 0.12f;
        fx.SpeedMax = 0.45f;
        fx.SizeMin = size * 0.6f;
        fx.SizeMax = size;
        fx.LifeMin = 5f;
        fx.LifeMax = 9f;
        fx.Prewarm();
    }

    protected void Snowfall(Vector3 center, Vector3 extents, int count)
    {
        ParticleFx fx = ParticleFx.Spawn(transform, center, count, new Color(1f, 1f, 1f, 0.9f));
        fx.EmitExtents = extents;
        fx.BaseVelocity = new Vector3(0.3f, -2f, 0f);
        fx.Gravity = new Vector3(0.15f, -0.3f, 0.05f);
        fx.SpeedMin = 0.1f;
        fx.SpeedMax = 0.5f;
        fx.SizeMin = 0.06f;
        fx.SizeMax = 0.14f;
        fx.LifeMin = 7f;
        fx.LifeMax = 11f;
        fx.Prewarm();
    }

    protected void Leaves(Vector3 center, Vector3 extents, int count, Color color)
    {
        ParticleFx fx = ParticleFx.Spawn(transform, center, count, color);
        fx.EmitExtents = extents;
        fx.BaseVelocity = new Vector3(0.4f, -0.7f, 0.15f);
        fx.Gravity = new Vector3(0.2f, -0.25f, 0.1f);
        fx.SpeedMin = 0.15f;
        fx.SpeedMax = 0.5f;
        fx.SizeMin = 0.16f;
        fx.SizeMax = 0.28f;
        fx.LifeMin = 5f;
        fx.LifeMax = 8f;
        fx.SpinMax = 4f;
        fx.Prewarm();
    }

    protected void Fountain(Vector3 pos, int count)
    {
        ParticleFx fx = ParticleFx.Spawn(transform, pos, count, new Color(0.65f, 0.87f, 1f, 0.85f));
        fx.EmitExtents = new Vector3(0.12f, 0.05f, 0.12f);
        fx.BaseVelocity = new Vector3(0f, 4.2f, 0f);
        fx.Gravity = new Vector3(0f, -9f, 0f);
        fx.SpeedMin = 0.3f;
        fx.SpeedMax = 1.1f;
        fx.SizeMin = 0.07f;
        fx.SizeMax = 0.15f;
        fx.LifeMin = 0.9f;
        fx.LifeMax = 1.3f;
        fx.Prewarm();
    }

    // ---------- Игровые объекты ----------

    protected void AddCoin(Vector3 pos)
    {
        int id = _coinCounter++;
        Coin c = Coin.Spawn(transform, pos, id);
        Coins[id] = c;
    }

    protected void AddEnemy(Vector3 a, Vector3 b, string kind, float speed)
    {
        int id = _enemyCounter++;
        Enemy e = Enemy.Spawn(transform, a, b, kind, speed, id);
        Enemies[id] = e;
    }

    protected void AddPortal(Vector3 pos, int target, string label, Color color, float yaw = 0f)
    {
        Portal p = Portal.Spawn(transform, pos, target, label, color, yaw);
        Portals.Add(p);
    }

    protected void AddNpc(Vector3 pos)
    {
        Elder = Npc.Spawn(transform, pos);
    }

    protected void AddStar(Vector3 pos)
    {
        Star = StarGoal.Spawn(transform, pos);
    }

    // ---------- Изменение состояния ----------

    public void RemoveCoin(int id, bool effect)
    {
        Coin c;
        if (!Coins.TryGetValue(id, out c)) return;
        Coins.Remove(id);
        if (c == null) return;
        if (effect) c.CollectEffect();
        else Object.Destroy(c.gameObject);
    }

    public void RemoveEnemy(int id, bool effect)
    {
        Enemy e;
        if (!Enemies.TryGetValue(id, out e)) return;
        Enemies.Remove(id);
        if (e == null) return;
        if (effect) e.DieEffect();
        else Object.Destroy(e.gameObject);
    }

    // ---------- Сетевая синхронизация врагов ----------

    public void SimulateEnemies(float dt)
    {
        foreach (KeyValuePair<int, Enemy> kv in Enemies)
        {
            if (kv.Value != null) kv.Value.HostStep(dt);
        }
    }

    public List<EnemyState> GatherEnemyStates()
    {
        _stateScratch.Clear();
        foreach (KeyValuePair<int, Enemy> kv in Enemies)
        {
            Enemy e = kv.Value;
            if (e == null || e.Dying) continue;
            EnemyState es = new EnemyState();
            es.Id = kv.Key;
            es.Pos = e.transform.position;
            es.Yaw = e.Yaw;
            _stateScratch.Add(es);
        }
        return _stateScratch;
    }

    public void ApplyEnemyStates(List<EnemyState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            Enemy e;
            if (Enemies.TryGetValue(states[i].Id, out e) && e != null)
                e.SetNetState(states[i].Pos, states[i].Yaw);
        }
    }
}

// Плавное горизонтальное движение (облака).
public class Drift : MonoBehaviour
{
    public float Speed = 0.6f;
    public float Limit = 90f;

    private void Update()
    {
        Vector3 p = transform.localPosition;
        p.x += Speed * Time.deltaTime;
        if (p.x > Limit) p.x = -Limit;
        transform.localPosition = p;
    }
}
