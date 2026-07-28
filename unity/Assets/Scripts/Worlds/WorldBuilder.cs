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
    public readonly List<Npc> Npcs = new List<Npc>();
    public StarGoal Star;

    private int _coinCounter;
    private int _starCounter;
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

        // Заливающий свет держим умеренным: при высоком ambient картинка
        // становится плоской и выцветшей, тени пропадают.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = top * 0.6f;
        RenderSettings.ambientEquatorColor = horizon * 0.5f;
        RenderSettings.ambientGroundColor = ground * 0.42f;

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
        sun.shadowStrength = 0.85f;
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
        // Модель из пака вместо стопки «клякс». Высота подбирается так,
        // чтобы прежний масштаб давал примерно тот же силуэт.
        int seedIdx = Mathf.Abs(Mathf.RoundToInt(pos.x * 7.3f + pos.z * 3.1f));
        if (Gfx.Prop(transform, Heroes.Pick(Heroes.Trees, seedIdx), pos,
                5.2f * scale, (seedIdx * 37) % 360) != null) return;

        Material bark = Gfx.MatFull(new Color(0.42f, 0.28f, 0.15f), 0.05f, 0f, Color.black, 1.5f, 0.6f);
        Gfx.Cyl(transform, pos + new Vector3(0f, 1.2f * scale, 0f),
            new Vector3(0.62f * scale, 1.25f * scale, 0.62f * scale), bark);

        // Ветви — тонкие цилиндры под углом, крона из неровных «клякс».
        for (int i = 0; i < 3; i++)
        {
            float a = 40f + i * 115f;
            GameObject branch = Gfx.Cyl(transform,
                pos + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.45f, 2.1f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.45f) * scale,
                new Vector3(0.2f, 0.55f, 0.2f) * scale, bark, false);
            branch.transform.localRotation = Quaternion.Euler(28f, a, 0f);
        }

        Color rim = leaf * 1.4f;
        Material f1 = Gfx.FoliageMat(leaf, rim);
        Material f2 = Gfx.FoliageMat(leaf * 1.12f, rim);
        Material f3 = Gfx.FoliageMat(leaf * 0.88f, rim);
        int seed = Mathf.Abs(Mathf.RoundToInt(pos.x * 7.3f + pos.z * 3.1f));

        Gfx.Blob(transform, pos + new Vector3(0f, 3.1f, 0f) * scale,
            new Vector3(2.9f, 2.4f, 2.9f) * scale, f1, seed, 0.45f, false);
        Gfx.Blob(transform, pos + new Vector3(1.0f, 2.4f, 0.45f) * scale,
            new Vector3(1.8f, 1.6f, 1.8f) * scale, f2, seed + 3, 0.5f, false);
        Gfx.Blob(transform, pos + new Vector3(-0.9f, 2.55f, -0.55f) * scale,
            new Vector3(1.7f, 1.5f, 1.7f) * scale, f3, seed + 7, 0.5f, false);
        Gfx.Blob(transform, pos + new Vector3(0.2f, 4.05f, -0.3f) * scale,
            new Vector3(1.5f, 1.3f, 1.5f) * scale, f2, seed + 11, 0.55f, false);
    }

    protected void Pine(Vector3 pos, bool snowy, float scale)
    {
        int seedIdx = Mathf.Abs(Mathf.RoundToInt(pos.x * 5.1f + pos.z * 9.7f));
        if (Gfx.Prop(transform, Heroes.Pick(Heroes.Pines, seedIdx), pos,
                6f * scale, (seedIdx * 53) % 360) != null) return;

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
        int seedIdx = Mathf.Abs(Mathf.RoundToInt(pos.x * 11.7f + pos.z * 5.9f + size * 31f));
        string[] set = size > 2.5f ? Heroes.RocksLarge : Heroes.RocksSmall;
        GameObject prop = Gfx.Prop(transform, Heroes.Pick(set, seedIdx), pos,
            size * 1.1f, (seedIdx * 29) % 360);
        if (prop != null)
        {
            // Камни — часть геометрии уровня, по ним можно ходить.
            MeshFilter mf0 = prop.GetComponentInChildren<MeshFilter>();
            if (mf0 != null)
            {
                MeshCollider mc = mf0.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf0.sharedMesh;
                mc.convex = true;
            }
            return;
        }

        Material m = Gfx.MatFull(color, 0.03f, 0f, Color.black, 1.2f, 0.85f);
        m.SetFloat("_AOStrength", 0.7f);
        int seed = Mathf.Abs(Mathf.RoundToInt(pos.x * 11.7f + pos.z * 5.9f + size * 31f));
        Gfx.Blob(transform, pos, new Vector3(size, size * 0.75f, size * 0.95f), m, seed, 0.7f, true);
    }

    protected void Bush(Vector3 pos, Color color)
    {
        int seedIdx = Mathf.Abs(Mathf.RoundToInt(pos.x * 3.7f + pos.z * 13.1f));
        if (Gfx.Prop(transform, Heroes.Pick(Heroes.Bushes, seedIdx), pos,
                1.3f, (seedIdx * 41) % 360) != null) return;

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

    // Настроение картинки конкретного мира: сила свечения, насыщенность,
    // оттенок и виньетка. Применяется к камере локального игрока.
    protected void SetPostFx(float intensity, float saturation, Color tint, float vignette)
    {
        _postIntensity = intensity;
        _postSaturation = saturation;
        _postTint = tint;
        _postVignette = vignette;
        _hasPostFx = true;
        ApplyPostFx();
    }

    private bool _hasPostFx;
    private float _postIntensity = 1f;
    private float _postSaturation = 1.12f;
    private Color _postTint = Color.white;
    private float _postVignette = 0.5f;

    // Камера появляется вместе с игроком, поэтому настройки применяются
    // и при построении мира, и позже, когда камера уже существует.
    public void ApplyPostFx()
    {
        if (!_hasPostFx) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        PostFx fx = cam.GetComponent<PostFx>();
        if (fx != null) fx.Configure(_postIntensity, _postSaturation, _postTint, _postVignette);
    }

    protected void GrassField(Vector3 center, Vector2 extents, int count, Color baseCol, Color tipCol,
        bool followTerrain = false)
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
            if (followTerrain) p.y = GroundHeight(p.x, p.z) + 0.02f;
            float yaw = Random.Range(0f, Mathf.PI);
            float w = 0.055f * Random.Range(0.7f, 1.5f);
            float h = 0.55f * Random.Range(0.6f, 1.7f);
            float phase = Random.value;

            Vector3 side = new Vector3(Mathf.Cos(yaw) * w, 0f, Mathf.Sin(yaw) * w);
            // Травинка сужается кверху и слегка заваливается — плоские
            // прямоугольники читались как пластиковые карточки.
            float lean = Random.Range(0.1f, 0.35f);
            float leanYaw = Random.Range(0f, Mathf.PI * 2f);
            Vector3 up = new Vector3(Mathf.Cos(leanYaw) * lean * h, h,
                                     Mathf.Sin(leanYaw) * lean * h);

            int v = i * 4;
            verts[v] = p - side;
            verts[v + 1] = p + side;
            verts[v + 2] = p + side * 0.18f + up;
            verts[v + 3] = p - side * 0.18f + up;

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
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

    public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();

    // Полоса препятствий: чередование неподвижных плит и движущихся
    // между ними — заполняет пустые куски мира настоящей игрой.
    protected void ObstacleRun(Vector3 start, Vector3 dir, int steps, float rise, Color color)
    {
        dir = dir.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 p = start;
        for (int i = 0; i < steps; i++)
        {
            if (i % 2 == 0)
            {
                Platform(p, new Vector3(4.6f, 0.6f, 4.6f), color);
                AddCoin(p + new Vector3(0f, 1.5f, 0f));
            }
            else
            {
                AddMovingPlatform(p - side * 4f, p + side * 4f,
                    new Vector3(4f, 0.6f, 4f), color, 4.5f + (i % 3) * 0.8f, i * 0.27f);
            }
            p += dir * 7.5f + new Vector3(0f, rise, 0f);
        }
        AddCheckpoint(p - dir * 7.5f + new Vector3(0f, 0.5f, 0f));
    }

    // Вращающееся бревно — сбивает с платформы, если зазеваться.
    protected void Spinner(Vector3 pos, float length, float speed, Color color)
    {
        GameObject hub = new GameObject("SpinnerHub");
        hub.transform.SetParent(transform, false);
        hub.transform.localPosition = pos;
        Gfx.Box(hub.transform, Vector3.zero, new Vector3(length, 0.7f, 0.7f),
            Gfx.MatFull(color, 0.08f, 0f, Color.black, 2f, 0.4f));
        Spinner sp = hub.AddComponent<Spinner>();
        sp.Axis = Vector3.up;
        sp.Speed = speed;
    }

    protected void AddCheckpoint(Vector3 pos)
    {
        Checkpoints.Add(Checkpoint.Create(transform, pos));
    }

    protected void Water(Vector3 pos, Vector2 size)
    {
        // Плавать можно только там, где зарегистрирована зона —
        // сама поверхность отвечает лишь за вид.
        WaterZone.Create(transform, pos, size, 14f);

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

    // ---------- Рельеф ----------

    private bool _hasTerrain;
    private Vector3 _terrainCenter;
    private float _terrainAmp;
    private float _terrainFreq;
    private float _terrainFlat;
    private int _terrainSeed;

    private static float HeightAt(float x, float z, Vector3 center, float amp, float freq,
        float flatRadius, int seed)
    {
        float ox = seed * 0.37f;
        float oz = seed * 0.71f;
        float h = 0f;
        float a = 1f;
        float f = freq;
        float total = 0f;
        for (int o = 0; o < 3; o++)
        {
            h += (Mathf.PerlinNoise(ox + x * f, oz + z * f) - 0.5f) * a;
            total += a;
            a *= 0.5f;
            f *= 2.1f;
        }
        h = h / Mathf.Max(total, 0.0001f) * amp;

        // Площадка вокруг центра остаётся ровной — там спавн и постройки.
        if (flatRadius > 0f)
        {
            float d = new Vector2(x - center.x, z - center.z).magnitude;
            float k = Mathf.Clamp01((d - flatRadius) / Mathf.Max(flatRadius * 0.8f, 0.001f));
            h *= k * k;
        }
        return center.y + h;
    }

    // Ровные площадки поверх шума: озёра, площади, дно каньона.
    // Регистрируются ДО вызова Terrain(), иначе меш их не учтёт.
    private struct FlatSpot
    {
        public Vector2 Center;
        public float Radius;
        public float Falloff;
        public float Level;
    }

    private readonly List<FlatSpot> _flats = new List<FlatSpot>();

    protected void FlattenArea(float x, float z, float radius, float falloff = 10f, float level = 0f)
    {
        FlatSpot f = new FlatSpot();
        f.Center = new Vector2(x, z);
        f.Radius = radius;
        f.Falloff = Mathf.Max(falloff, 0.01f);
        f.Level = level;
        _flats.Add(f);
    }

    // Высота земли в точке — миры расставляют по ней объекты.
    public float GroundHeight(float x, float z)
    {
        if (!_hasTerrain) return 0f;
        float h = HeightAt(x, z, _terrainCenter, _terrainAmp, _terrainFreq, _terrainFlat, _terrainSeed);
        for (int i = 0; i < _flats.Count; i++)
        {
            FlatSpot f = _flats[i];
            float d = new Vector2(x - f.Center.x, z - f.Center.y).magnitude;
            float k = Mathf.Clamp01((d - f.Radius) / f.Falloff);
            k = k * k * (3f - 2f * k); // плавный переход к рельефу
            h = Mathf.Lerp(f.Level, h, k);
        }
        return h;
    }

    private Material _terrainMat;

    // Задаётся ДО вызова Terrain(). Имена — файлы из Resources/Textures/world
    // без расширения.
    protected void TerrainTextures(string flatTex, string flatNormal,
        string slopeTex, string slopeNormal, float tiling = 0.12f, float slopeTiling = 0.16f)
    {
        _terrainMat = Gfx.TerrainMat(flatTex, flatNormal, slopeTex, slopeNormal,
            tiling, slopeTiling);
    }

    protected Vector3 OnGround(float x, float z, float lift = 0f)
    {
        return new Vector3(x, GroundHeight(x, z) + lift, z);
    }

    // Холмистая земля из сетки с шумом. Заменяет плоские коробки и
    // сразу делает мир заметно живее.
    protected void Terrain(Vector3 center, Vector2 size, int resolution, float amplitude,
        float freq, Color lowColor, Color highColor, float flatRadius, int seed,
        float detailTiling = 6f, float normalScale = 0.35f)
    {
        resolution = Mathf.Clamp(resolution, 8, 160);
        _hasTerrain = true;
        _terrainCenter = center;
        _terrainAmp = amplitude;
        _terrainFreq = freq;
        _terrainFlat = flatRadius;
        _terrainSeed = seed;

        int side = resolution + 1;
        Vector3[] verts = new Vector3[side * side];
        Vector2[] uvs = new Vector2[side * side];
        Color[] colors = new Color[side * side];
        int[] tris = new int[resolution * resolution * 6];

        for (int z = 0; z < side; z++)
        {
            for (int x = 0; x < side; x++)
            {
                float fx = (float)x / resolution;
                float fz = (float)z / resolution;
                float wx = center.x + (fx - 0.5f) * size.x;
                float wz = center.z + (fz - 0.5f) * size.y;
                // Через GroundHeight, чтобы меш и запросы высоты совпадали
                // с учётом зарегистрированных ровных площадок.
                float wy = GroundHeight(wx, wz);

                int idx = z * side + x;
                verts[idx] = new Vector3(wx, wy, wz);
                uvs[idx] = new Vector2(fx * size.x * 0.1f, fz * size.y * 0.1f);

                float t = Mathf.Clamp01((wy - center.y) / Mathf.Max(amplitude, 0.001f) * 0.5f + 0.5f);
                Color c = Color.Lerp(lowColor, highColor, t);
                colors[idx] = c;
            }
        }

        int ti = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int a = z * side + x;
                int b = (z + 1) * side + x;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Terrain");
        go.transform.SetParent(transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        // Настоящие PBR-текстуры поверхности, если мир их задал.
        mr.sharedMaterial = _terrainMat != null
            ? _terrainMat
            : Gfx.VertexColorMat(0.03f, detailTiling, normalScale);
        MeshCollider mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    // ---------- Крупный реквизит ----------

    protected void Waterfall(Vector3 top, float width, float height, int particles = 90)
    {
        Material sheet = Gfx.MatFull(new Color(0.6f, 0.85f, 1f), 0.9f, 0.1f,
            new Color(0.15f, 0.35f, 0.55f), 0f, 0f);
        GameObject fall = Gfx.Box(transform, top + new Vector3(0f, -height * 0.5f, 0f),
            new Vector3(width, height, 0.4f), sheet, false);
        Gfx.NoShadow(fall);

        ParticleFx mist = ParticleFx.Spawn(transform, top + new Vector3(0f, -height, 0f),
            particles, new Color(0.8f, 0.93f, 1f, 0.75f));
        mist.EmitExtents = new Vector3(width * 0.5f, 0.4f, 0.5f);
        mist.BaseVelocity = new Vector3(0f, 1.1f, 0f);
        mist.Gravity = new Vector3(0f, -0.5f, 0f);
        mist.SpeedMin = 0.3f;
        mist.SpeedMax = 1.4f;
        mist.SizeMin = 0.12f;
        mist.SizeMax = 0.3f;
        mist.LifeMin = 1f;
        mist.LifeMax = 2.2f;
        mist.Prewarm();

        ParticleFx spray = ParticleFx.Spawn(transform, top + new Vector3(0f, -height * 0.4f, 0f),
            particles, new Color(0.9f, 0.96f, 1f, 0.6f));
        spray.EmitExtents = new Vector3(width * 0.45f, height * 0.4f, 0.3f);
        spray.BaseVelocity = new Vector3(0f, -6f, 0f);
        spray.Gravity = new Vector3(0f, -3f, 0f);
        spray.SpeedMin = 0.1f;
        spray.SpeedMax = 0.6f;
        spray.SizeMin = 0.08f;
        spray.SizeMax = 0.16f;
        spray.LifeMin = 0.6f;
        spray.LifeMax = 1.1f;
        spray.Prewarm();
    }

    protected void Bridge(Vector3 from, Vector3 to, float width, Color plank)
    {
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        int planks = Mathf.Max(2, Mathf.RoundToInt(len / 0.9f));
        Material plankMat = Gfx.MatFull(plank, 0.05f, 0f, Color.black, 2f, 0.4f);
        Material ropeMat = Gfx.Mat(new Color(0.45f, 0.35f, 0.2f), 0.05f);
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        for (int i = 0; i <= planks; i++)
        {
            float t = (float)i / planks;
            Vector3 p = Vector3.Lerp(from, to, t);
            // Лёгкий провис в середине.
            p.y -= Mathf.Sin(t * Mathf.PI) * 0.35f;
            GameObject board = Gfx.Box(transform, p, new Vector3(width, 0.16f, 0.62f), plankMat);
            board.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < planks; i++)
            {
                float t = (float)i / planks;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 0.35f;
                Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(width * 0.5f * side, 0.55f, 0f);
                GameObject post = Gfx.Box(transform, p + off, new Vector3(0.1f, 0.9f, 0.1f), ropeMat, false);
                post.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }

    protected void Cactus(Vector3 pos, float scale)
    {
        Material m = Gfx.RimMat(new Color(0.25f, 0.5f, 0.28f), new Color(0.6f, 0.9f, 0.5f), 0.3f);
        Gfx.Cyl(transform, pos + new Vector3(0f, 1.5f * scale, 0f),
            new Vector3(0.7f * scale, 1.5f * scale, 0.7f * scale), m);
        Gfx.Ball(transform, pos + new Vector3(0f, 3f * scale, 0f),
            new Vector3(0.7f, 0.7f, 0.7f) * scale, m);

        for (int i = -1; i <= 1; i += 2)
        {
            Gfx.Cyl(transform, pos + new Vector3(0.55f * i * scale, 1.9f * scale, 0f),
                new Vector3(0.34f, 0.34f, 0.34f) * scale, m, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Gfx.Cyl(transform, pos + new Vector3(0.78f * i * scale, 2.35f * scale, 0f),
                new Vector3(0.34f * scale, 0.55f * scale, 0.34f * scale), m, false);
            Gfx.Ball(transform, pos + new Vector3(0.78f * i * scale, 2.9f * scale, 0f),
                new Vector3(0.36f, 0.36f, 0.36f) * scale, m);
        }
    }

    protected void CrystalCluster(Vector3 pos, float scale, Color color, bool light)
    {
        Material m = Gfx.MatFull(color, 0.92f, 0.25f, color * 0.75f, 0f, 0f);
        Gfx.Crystal(transform, pos, 0.42f * scale, 2.4f * scale, m, Random.Range(-9f, 9f));
        Gfx.Crystal(transform, pos + new Vector3(0.5f, -0.15f, 0.3f) * scale,
            0.27f * scale, 1.5f * scale, m, Random.Range(-22f, 22f));
        Gfx.Crystal(transform, pos + new Vector3(-0.45f, -0.2f, -0.25f) * scale,
            0.22f * scale, 1.2f * scale, m, Random.Range(-22f, 22f));
        Gfx.Glow(transform, pos + new Vector3(0f, 0.6f * scale, 0f), 4f * scale,
            new Color(color.r, color.g, color.b, 0.5f));
        if (light) Gfx.PointLight(transform, pos + new Vector3(0f, 1f * scale, 0f), color, 14f * scale, 1.2f);
    }

    // Каменная арка — ориентир на горизонте и заодно проходной портал-рамка.
    protected void Arch(Vector3 pos, float width, float height, Color color)
    {
        Material m = Gfx.MatFull(color, 0.03f, 0f, Color.black, 1.5f, 0.7f);
        Gfx.Box(transform, pos + new Vector3(-width * 0.5f, height * 0.5f, 0f),
            new Vector3(width * 0.22f, height, width * 0.28f), m);
        Gfx.Box(transform, pos + new Vector3(width * 0.5f, height * 0.5f, 0f),
            new Vector3(width * 0.22f, height, width * 0.28f), m);
        int steps = 7;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.PI * i / steps;
            Vector3 p = pos + new Vector3(-Mathf.Cos(a) * width * 0.5f,
                height + Mathf.Sin(a) * width * 0.32f, 0f);
            GameObject seg = Gfx.Box(transform, p,
                new Vector3(width * 0.2f, width * 0.2f, width * 0.28f), m);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
        }
    }

    protected void Windmill(Vector3 pos, Color wall, Color blade)
    {
        Material wallMat = Gfx.MatFull(wall, 0.05f, 0f, Color.black, 2.5f, 0.35f);
        Gfx.Cyl(transform, pos + new Vector3(0f, 3f, 0f), new Vector3(4.4f, 3f, 4.4f), wallMat);
        GameObject roof = Gfx.Cone(transform, pos + new Vector3(0f, 6f, 0f), 2.9f, 2.2f,
            Gfx.Mat(new Color(0.55f, 0.25f, 0.2f), 0.05f));
        Gfx.NoShadow(roof);

        GameObject hub = new GameObject("Blades");
        hub.transform.SetParent(transform, false);
        hub.transform.localPosition = pos + new Vector3(0f, 5f, 2.4f);
        Material bladeMat = Gfx.Mat(blade, 0.05f);
        for (int i = 0; i < 4; i++)
        {
            GameObject b = Gfx.Box(hub.transform, Vector3.zero, new Vector3(0.5f, 5.4f, 0.16f), bladeMat, false);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            b.transform.localPosition = b.transform.localRotation * new Vector3(0f, 2.7f, 0f);
        }
        Spinner sp = hub.AddComponent<Spinner>();
        sp.Axis = Vector3.forward;
        sp.Speed = 22f;
    }

    protected void Dock(Vector3 pos, float length, float yaw)
    {
        Material wood = Gfx.MatFull(new Color(0.52f, 0.38f, 0.24f), 0.05f, 0f, Color.black, 2f, 0.4f);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        int boards = Mathf.Max(3, Mathf.RoundToInt(length / 1.2f));
        for (int i = 0; i < boards; i++)
        {
            Vector3 p = pos + rot * new Vector3(0f, 0f, i * 1.2f);
            GameObject b = Gfx.Box(transform, p, new Vector3(3f, 0.2f, 1.1f), wood);
            b.transform.localRotation = rot;
            if (i % 2 == 0)
            {
                Gfx.Cyl(transform, p + new Vector3(-1.3f, -0.7f, 0f), new Vector3(0.24f, 0.7f, 0.24f), wood, false);
                Gfx.Cyl(transform, p + new Vector3(1.3f, -0.7f, 0f), new Vector3(0.24f, 0.7f, 0.24f), wood, false);
            }
        }
    }

    protected void MarketStall(Vector3 pos, Color cloth, float yaw)
    {
        Material wood = Gfx.Mat(new Color(0.5f, 0.36f, 0.22f), 0.05f);
        Material clothMat = Gfx.RimMat(cloth, cloth * 1.5f, 0.2f);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        GameObject table = Gfx.Box(transform, pos + new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 0.16f, 1.4f), wood);
        table.transform.localRotation = rot;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Gfx.Cyl(transform, pos + rot * new Vector3(1.1f * sx, 0.45f, 0.55f * sz),
                    new Vector3(0.14f, 0.45f, 0.14f), wood, false);
                Gfx.Cyl(transform, pos + rot * new Vector3(1.1f * sx, 1.6f, 0.55f * sz),
                    new Vector3(0.1f, 0.75f, 0.1f), wood, false);
            }
        }
        GameObject canopy = Gfx.Box(transform, pos + new Vector3(0f, 2.45f, 0f),
            new Vector3(3f, 0.14f, 1.9f), clothMat, false);
        canopy.transform.localRotation = rot * Quaternion.Euler(-9f, 0f, 0f);

        Material fruit = Gfx.RimMat(new Color(0.9f, 0.35f, 0.3f), new Color(1f, 0.7f, 0.5f), 0.3f);
        for (int i = 0; i < 5; i++)
        {
            Gfx.Ball(transform, pos + rot * new Vector3(-0.9f + i * 0.45f, 1.08f, Random.Range(-0.3f, 0.3f)),
                new Vector3(0.26f, 0.26f, 0.26f), fruit);
        }
    }

    protected void AddMovingPlatform(Vector3 a, Vector3 b, Vector3 size, Color color, float period, float phase)
    {
        MovingPlatform.Create(transform, a, b, size, Gfx.Mat(color, 0.08f), period, phase);
    }

    protected void AddBouncePad(Vector3 pos, float power)
    {
        BouncePad.Create(transform, pos, power);
    }

    protected void AddCampfire(Vector3 pos, Color color, float range)
    {
        Campfire.Create(transform, pos, color, range);
    }

    // ---------- Игровые объекты ----------

    protected void AddCoin(Vector3 pos)
    {
        int id = _coinCounter++;
        Coin c = Coin.Spawn(transform, pos, id);
        Coins[id] = c;
    }

    // Звезда — цель мира. Идентификаторы идут с 10000, поэтому сеть и
    // сохранение отличают их от монет без отдельного списка.
    protected void AddStarPickup(Vector3 pos)
    {
        int id = NetManager.StarIdBase + _starCounter++;
        Coin c = Coin.Spawn(transform, pos, id);
        Coins[id] = c;
    }

    // Звезда на собственном постаменте: снизу батут, наверху площадка.
    // Расставлять звёзды по выверенным координатам оказалось ненадёжно —
    // они попадали внутрь крон и крыш. Постамент делает награду
    // достижимой из любой точки под ней и заметной издалека.
    protected void AddStarPedestal(Vector3 groundPos, float height)
    {
        height = Mathf.Clamp(height, 4f, 24f);
        // Скорость батута подобрана так, чтобы заброс был выше площадки.
        AddBouncePad(groundPos + new Vector3(0f, 0.4f, 0f),
            Mathf.Sqrt(2f * 22f * (height + 3f)));

        Material stone = Gfx.MatFull(new Color(0.68f, 0.62f, 0.52f), 0.1f, 0f,
            Color.black, 3f, 0.4f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Gfx.Cyl(transform,
                groundPos + new Vector3(Mathf.Cos(a) * 2.4f, height * 0.5f, Mathf.Sin(a) * 2.4f),
                new Vector3(0.5f, height * 0.5f, 0.5f), stone, false);
        }

        Vector3 top = groundPos + new Vector3(0f, height, 0f);
        Gfx.Cyl(transform, top, new Vector3(7f, 0.4f, 7f), stone);
        Gfx.PointLight(transform, top + new Vector3(0f, 2f, 0f),
            new Color(1f, 0.85f, 0.45f), 18f, 1.3f);
        AddStarPickup(top + new Vector3(0f, 1.8f, 0f));
    }

    protected void AddEnemy(Vector3 a, Vector3 b, string kind, float speed)
    {
        int id = _enemyCounter++;
        Enemy e = Enemy.Spawn(transform, a, b, kind, speed, id);
        Enemies[id] = e;
    }

    protected void AddBoss(Vector3 a, Vector3 b, string kind, float speed, int hp, float scale)
    {
        int id = _enemyCounter++;
        Enemy e = Enemy.SpawnBoss(transform, a, b, kind, speed, id, hp, scale);
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
        Npcs.Add(Elder);
    }

    // Житель для атмосферы и сюжета: своя реплика, своя модель.
    protected void AddVillager(Vector3 pos, string title, string modelId,
        float height, string line)
    {
        Npc n = Npc.Spawn(transform, pos, title, modelId, height);
        n.CustomLine = line;
        Npcs.Add(n);
    }

    // Торговка открывает лавку вместо реплики.
    protected void AddShopkeeper(Vector3 pos, string title, string modelId, float height)
    {
        Npc n = Npc.Spawn(transform, pos, title, modelId, height);
        n.IsShop = true;
        n.CustomLine = "Загляни в лавку — монеты тут не лежат без дела.";
        Npcs.Add(n);
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
