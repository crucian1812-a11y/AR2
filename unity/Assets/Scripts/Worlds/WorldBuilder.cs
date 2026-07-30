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
    public readonly List<Breakable> Breakables = new List<Breakable>();
    public readonly List<Chest> Chests = new List<Chest>();

    public Npc Elder;
    public readonly List<Npc> Npcs = new List<Npc>();
    public StarGoal Star;

    private int _coinCounter;
    private int _starCounter;
    private int _heartCounter;
    private int _enemyCounter;
    private int _breakCounter;
    private int _chestCounter;
    private readonly List<EnemyState> _stateScratch = new List<EnemyState>();

    protected abstract void Build();

    // Палитра воды. Тропическая по умолчанию — в референсе именно такая:
    // на мели светящаяся бирюза, в глубине плотный синий.
    protected virtual Color ShallowWater { get { return new Color(0.42f, 0.92f, 0.92f); } }
    protected virtual Color DeepWater { get { return new Color(0.04f, 0.32f, 0.55f); } }

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

        // Добытое не отрастает: сломанные валуны и деревья не возвращаются.
        for (int i = Breakables.Count - 1; i >= 0; i--)
        {
            Breakable b = Breakables[i];
            if (b == null) { Breakables.RemoveAt(i); continue; }
            if (!net.IsBroken(worldIndex, b.Id)) continue;
            Breakables.RemoveAt(i);
            Object.Destroy(b.gameObject);
        }

        for (int i = Chests.Count - 1; i >= 0; i--)
        {
            Chest c = Chests[i];
            if (c == null) { Chests.RemoveAt(i); continue; }
            if (net.IsCollected(worldIndex, c.Id)) c.SetOpened();
        }
    }

    // Убрать разрушенный объект по событию от хоста.
    public void RemoveBreakable(int id, bool effect)
    {
        for (int i = 0; i < Breakables.Count; i++)
        {
            Breakable b = Breakables[i];
            if (b == null || b.Id != id) continue;
            Breakables.RemoveAt(i);
            if (b == null) return;
            if (effect) b.BreakEffect();
            else Object.Destroy(b.gameObject);
            return;
        }
    }

    // ---------- Добыча и сундуки ----------

    // Сделать уже построенный объект добываемым.
    protected void MakeBreakable(GameObject target, int kind, int amount, int hp,
        float radius, float height)
    {
        if (target == null) return;
        Breakable b = Breakable.Attach(target, BreakIdBase + _breakCounter++,
            kind, amount, hp, radius, height);
        if (b != null) Breakables.Add(b);
    }

    // Рудная жила: светящиеся кристаллы в породе. Отдельный вид добычи,
    // ради которого стоит лезть в пещеру.
    protected void OreVein(Vector3 pos, int kind, int amount, float scale = 1f)
    {
        Color tint = Res.Tint(kind);
        Material rock = Gfx.MatFull(new Color(0.34f, 0.32f, 0.36f), 0.05f, 0f,
            Color.black, 3f, 0.6f);
        GameObject host = Gfx.Ball(transform, pos + new Vector3(0f, 0.55f * scale, 0f),
            new Vector3(1.1f, 0.95f, 1.1f) * scale, rock, true);
        host.name = "Ore";

        Material gem = Gfx.MatFull(tint, 0.85f, 0.3f, tint * 0.8f, 0f, 0f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            Gfx.Crystal(host.transform,
                new Vector3(Mathf.Cos(a) * 0.42f, 0.45f, Mathf.Sin(a) * 0.42f),
                0.16f, 0.5f, gem, Random.Range(-18f, 18f));
        }
        Gfx.Glow(host.transform, new Vector3(0f, 0.5f, 0f), 2.2f * scale,
            new Color(tint.r, tint.g, tint.b, 0.5f));

        MakeBreakable(host, kind, amount, 3, 1.1f * scale, 1.4f * scale);
    }

    protected void AddChest(Vector3 pos, float yaw, int coins, int resKind, int resAmount)
    {
        Chest c = Chest.Create(transform, pos, yaw, ChestIdBase + _chestCounter++,
            coins, resKind, resAmount);
        if (c != null) Chests.Add(c);
    }

    // Добываемый валун: тот же камень, что и декоративный, но ломается.
    protected void OreRock(Vector3 pos, float size, Color color, int amount = 2)
    {
        Rock(pos, size, color);
        // Rock ставит модель или примитив последним ребёнком — берём его.
        Transform last = transform.GetChild(transform.childCount - 1);
        MakeBreakable(last.gameObject, Res.Stone, amount, 2, size * 0.6f, size);
    }

    // Добываемое дерево.
    protected void OreTree(Vector3 pos, Color leaf, float scale, int amount = 3)
    {
        Tree(pos, leaf, scale);
        Transform last = transform.GetChild(transform.childCount - 1);
        MakeBreakable(last.gameObject, Res.Wood, amount, 3, 0.9f * scale, 4f * scale);
    }

    // Диапазоны ID, чтобы добыча и сундуки не пересекались с монетами.
    public const int BreakIdBase = 1000;
    public const int ChestIdBase = 30000;

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

        // Заливающий свет держим НИЗКИМ. Это выяснилось на рендере грота:
        // при ambient в 0.6 от неба сцена тонет в синем, зелень становится
        // мятной, камень белёсым, а огонь факелов не читается вовсе. Небо
        // должно быть ярким, но светить слабо — весь объём даёт солнце.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = top * 0.32f;
        RenderSettings.ambientEquatorColor = horizon * 0.26f;
        RenderSettings.ambientGroundColor = ground * 0.20f;

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
        // Заливку убрали — свет и объём теперь целиком на солнце.
        sun.intensity = sunIntensity * 1.45f;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.shadowNormalBias = 0.05f;

        // Смена суток. Мир задаёт свою ДНЕВНУЮ палитру, ночная выводится
        // из неё же — общая ночь на все шесть миров смотрелась бы чужой.
        // В пещере суток нет: там своё небо и свет от кристаллов.
        if (HasDayCycle)
        {
            DayCycle.Attach(transform, sun, RenderSettings.skybox,
                top, horizon, ground, fogColor, fogDensity, sunIntensity, sunEuler.y);
        }
    }

    // Пещера переопределяет на false.
    protected virtual bool HasDayCycle { get { return true; } }

    // ---------- Ландшафт и декор ----------

    protected GameObject Ground(Vector3 pos, Vector3 size, Color color, float detail, float normal,
        float smoothness = 0.1f)
    {
        GameObject go = Gfx.Box(transform, pos, size, Gfx.MatFull(color, smoothness, 0f, Color.black, detail, normal));
        go.name = "Ground";
        return go;
    }

    // Платформа. По умолчанию — мшистый камень: в референсе почти всё
    // сложено из него, и одна эта замена меняет вид игры сильнее, чем
    // любой отдельный ассет. Плоскую окраску оставляем для льда и металла.
    protected GameObject Platform(Vector3 pos, Vector3 size, Color color, float smoothness = 0.1f,
        float metallic = 0f)
    {
        if (metallic > 0.01f || smoothness > 0.3f)
            return Gfx.Box(transform, pos, size, Gfx.Mat(color, smoothness, metallic));

        // Цвет мира задаёт оттенок мха, камень под ним общий: так каждая
        // локация остаётся узнаваемой, а материал — одним и тем же.
        Color moss = Color.Lerp(color, new Color(0.34f, 0.62f, 0.26f), 0.55f);
        Color stone = Color.Lerp(new Color(0.60f, 0.58f, 0.55f), color, 0.18f);
        return Gfx.Box(transform, pos, size,
            Gfx.MossyMat(stone, moss, Mathf.Clamp(size.x * 0.22f, 0.5f, 1.4f)));
    }

    // Платформа, обжитая зеленью: по краям папоротники, снизу лианы.
    // Именно эта обводка не даёт камню выглядеть положенным поверх мира.
    protected GameObject LushPlatform(Vector3 pos, Vector3 size, Color color,
        int ferns = 3, int vines = 3)
    {
        GameObject go = Platform(pos, size, color);
        Color leaf = Color.Lerp(color, new Color(0.3f, 0.6f, 0.28f), 0.7f);

        float hx = size.x * 0.5f, hz = size.z * 0.5f, top = pos.y + size.y * 0.5f;
        for (int i = 0; i < ferns; i++)
        {
            float a = Random.Range(0f, 6.28f);
            Vector3 edge = new Vector3(Mathf.Cos(a) * hx * 0.82f, 0f, Mathf.Sin(a) * hz * 0.82f);
            Fern(new Vector3(pos.x, top, pos.z) + edge, leaf, Random.Range(0.7f, 1.15f));
        }
        if (ferns > 2)
            FlowerPatch(new Vector3(pos.x, top, pos.z) +
                new Vector3(Random.Range(-hx * 0.6f, hx * 0.6f), 0f,
                            Random.Range(-hz * 0.6f, hz * 0.6f)),
                new Color(0.95f, 0.55f, 0.75f), 0.9f);

        for (int i = 0; i < vines; i++)
        {
            float a = Random.Range(0f, 6.28f);
            Vector3 edge = new Vector3(Mathf.Cos(a) * hx * 0.95f, 0f, Mathf.Sin(a) * hz * 0.95f);
            Vine(new Vector3(pos.x, pos.y - size.y * 0.5f + 0.05f, pos.z) + edge,
                Random.Range(1.2f, 3.4f), leaf);
        }
        return go;
    }

    protected void Tree(Vector3 pos, Color leaf, float scale)
    {
        // Модель из пака вместо стопки «клякс». Высота подбирается так,
        // чтобы прежний масштаб давал примерно тот же силуэт.
        int seedIdx = Mathf.Abs(Mathf.RoundToInt(pos.x * 7.3f + pos.z * 3.1f));
        // Разброс высоты и лёгкий наклон ствола. Ровный ряд одинаковых
        // деревьев под прямым углом к земле — первое, что выдаёт в лесу
        // расставленные копии одной модели.
        float vary = 0.78f + ((seedIdx * 13) % 100) / 100f * 0.55f;
        GameObject t = Gfx.Prop(transform, Heroes.Pick(Heroes.Trees, seedIdx), pos,
            5.2f * scale * vary, (seedIdx * 37) % 360, 0.5f * scale);
        if (t != null)
        {
            float tiltX = (((seedIdx * 7) % 100) / 100f - 0.5f) * 7f;
            float tiltZ = (((seedIdx * 23) % 100) / 100f - 0.5f) * 7f;
            t.transform.localRotation =
                Quaternion.Euler(tiltX, (seedIdx * 37) % 360, tiltZ);
            return;
        }

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
                6f * scale, (seedIdx * 53) % 360, 0.45f * scale) != null) return;

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

    // Травинка из четырёх ярусов: 8 вершин, 6 треугольников.
    private const int GrassLevels = 4;
    private const int GrassVerts = GrassLevels * 2;
    private const int GrassIndices = (GrassLevels - 1) * 6;

    protected void GrassField(Vector3 center, Vector2 extents, int count, Color baseCol, Color tipCol,
        bool followTerrain = false)
    {
        count = Mathf.Clamp(count, 1, 24000);
        Vector3[] verts = new Vector3[count * GrassVerts];
        Vector2[] uvs = new Vector2[count * GrassVerts];
        Vector2[] uv2 = new Vector2[count * GrassVerts];
        Color[] colors = new Color[count * GrassVerts];
        Vector3[] normals = new Vector3[count * GrassVerts];
        int[] tris = new int[count * GrassIndices];

        for (int i = 0; i < count; i++)
        {
            Vector3 p = center + new Vector3(
                Random.Range(-extents.x, extents.x), 0f, Random.Range(-extents.y, extents.y));
            if (followTerrain) p.y = GroundHeight(p.x, p.z) + 0.02f;

            float yaw = Random.Range(0f, Mathf.PI);
            float w = 0.05f * Random.Range(0.75f, 1.5f);
            float h = 0.6f * Random.Range(0.55f, 1.8f);
            float phase = Random.value;

            Vector3 side = new Vector3(Mathf.Cos(yaw) * w, 0f, Mathf.Sin(yaw) * w);

            // Наклон стебля: травинка не стоит по струнке, а изгибается —
            // ярусы уходят в сторону всё сильнее (t в квадрате).
            float bend = Random.Range(0.12f, 0.5f) * h;
            float bendYaw = Random.Range(0f, Mathf.PI * 2f);
            Vector3 bendDir = new Vector3(Mathf.Cos(bendYaw), 0f, Mathf.Sin(bendYaw));

            // Свой оттенок у каждой травинки. Раньше всё поле красилось двумя
            // цветами на всех, и вблизи это читалось однородным ковром.
            float shade = Random.Range(0.78f, 1.16f);
            float warm = Random.Range(-0.04f, 0.06f);

            // Нормаль: в основном вверх, с примесью «лица» травинки —
            // одинаковая нормаль у всех давала ровную заливку без объёма.
            Vector3 face = Vector3.Cross(side.normalized, Vector3.up);
            Vector3 nrm = (Vector3.up * 0.72f + face * 0.28f).normalized;

            int v = i * GrassVerts;
            for (int L = 0; L < GrassLevels; L++)
            {
                float t = (float)L / (GrassLevels - 1);
                // Сужение к острию: верхний ярус почти сходится в точку.
                float wk = Mathf.Lerp(1f, 0.06f, t * t * 0.85f + t * 0.15f);
                Vector3 lvl = p + Vector3.up * (h * t) + bendDir * (bend * t * t);

                int a = v + L * 2;
                verts[a] = lvl - side * wk;
                verts[a + 1] = lvl + side * wk;

                uvs[a] = new Vector2(0f, t);
                uvs[a + 1] = new Vector2(1f, t);
                uv2[a] = new Vector2(phase, t);
                uv2[a + 1] = new Vector2(phase, t);
                normals[a] = nrm;
                normals[a + 1] = nrm;

                // У земли темнее — самозатенение делает ковёр объёмным.
                Color c = Color.Lerp(baseCol, tipCol, t);
                float ao = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(t * 1.6f));
                c = new Color(c.r * shade * ao + warm, c.g * shade * ao,
                              c.b * shade * ao - warm * 0.5f, 1f);
                colors[a] = c;
                colors[a + 1] = c;
            }

            int tri = i * GrassIndices;
            for (int L = 0; L < GrassLevels - 1; L++)
            {
                int a = v + L * 2;
                int b = a + 2;
                tris[tri] = a; tris[tri + 1] = b; tris[tri + 2] = a + 1;
                tris[tri + 3] = a + 1; tris[tri + 4] = b; tris[tri + 5] = b + 1;
                tri += 6;
            }
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

    // ---------- Зелень на камне ----------

    // Свисающая лиана: лента из сегментов, качается на ветру шейдером
    // листвы. В референсе они висят с каждого уступа и с каждой ветки —
    // именно они связывают камень с зеленью, чтобы платформа не выглядела
    // положенной сверху.
    protected void Vine(Vector3 top, float length, Color leaf, float width = 0.16f)
    {
        Material mat = Gfx.FoliageMat(leaf, leaf * 1.5f);
        int segs = Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), 2, 9);
        // Лиана слегка отклоняется — вертикальная струна выглядит верёвкой.
        float driftYaw = Random.Range(0f, 6.28f);
        float drift = Random.Range(0.04f, 0.14f);

        for (int i = 0; i < segs; i++)
        {
            float t = (i + 0.5f) / segs;
            float h = length / segs;
            Vector3 p = top + new Vector3(
                Mathf.Cos(driftYaw) * drift * length * t * t,
                -length * t,
                Mathf.Sin(driftYaw) * drift * length * t * t);
            // Книзу лиана тоньше, а листья крупнее.
            float w = width * (1.15f - t * 0.5f);
            GameObject seg = Gfx.Box(transform, p, new Vector3(w, h * 1.05f, w * 0.55f), mat, false);
            seg.transform.localRotation = Quaternion.Euler(0f, driftYaw * Mathf.Rad2Deg, 0f);
            Gfx.NoShadow(seg);

            if (i % 2 == 1)
            {
                GameObject leafBit = Gfx.Ball(transform,
                    p + new Vector3(Mathf.Cos(driftYaw + t * 5f) * w * 2.2f, 0f,
                                    Mathf.Sin(driftYaw + t * 5f) * w * 2.2f),
                    new Vector3(w * 3.4f, w * 1.6f, w * 3.4f), mat);
                Gfx.NoShadow(leafBit);
            }
        }
    }

    // Папоротник: веер длинных листьев от одной точки. В референсе такими
    // кустами закрыты все стыки камня с землёй — без них платформа
    // выглядит поставленной на пол.
    protected void Fern(Vector3 pos, Color leaf, float scale = 1f)
    {
        Material mat = Gfx.FoliageMat(leaf, leaf * 1.55f);
        int blades = Random.Range(6, 10);
        for (int i = 0; i < blades; i++)
        {
            float a = (i / (float)blades) * 6.28f + Random.Range(-0.2f, 0.2f);
            float len = (0.75f + Random.Range(-0.15f, 0.35f)) * scale;
            float lean = Random.Range(38f, 68f);
            // Лист — вытянутая пластина, наклонённая от центра наружу.
            GameObject blade = Gfx.Box(transform,
                pos + new Vector3(Mathf.Cos(a) * len * 0.42f, len * 0.42f,
                                  Mathf.Sin(a) * len * 0.42f),
                new Vector3(0.1f * scale, len, 0.34f * scale), mat, false);
            blade.transform.localRotation =
                Quaternion.Euler(0f, -a * Mathf.Rad2Deg, lean);
            Gfx.NoShadow(blade);
        }
        // Центр куста — плотный ком, чтобы не просвечивал.
        Gfx.NoShadow(Gfx.Ball(transform, pos + new Vector3(0f, 0.12f * scale, 0f),
            new Vector3(0.3f, 0.22f, 0.3f) * scale, mat));
    }

    // Кустик цветов — тёплые точки в зелени.
    protected void FlowerPatch(Vector3 pos, Color petal, float scale = 1f)
    {
        Material stem = Gfx.FoliageMat(new Color(0.28f, 0.5f, 0.24f), new Color(0.5f, 0.8f, 0.4f));
        Material bloom = Gfx.MatFull(petal, 0.4f, 0f, petal * 0.35f, 0f, 0f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.26f + Random.Range(-0.3f, 0.3f);
            float r = Random.Range(0.1f, 0.34f) * scale;
            Vector3 p = pos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float h = Random.Range(0.24f, 0.42f) * scale;
            Gfx.NoShadow(Gfx.Box(transform, p + new Vector3(0f, h * 0.5f, 0f),
                new Vector3(0.035f, h, 0.035f) * scale, stem, false));
            Gfx.NoShadow(Gfx.Ball(transform, p + new Vector3(0f, h, 0f),
                new Vector3(0.13f, 0.09f, 0.13f) * scale, bloom));
        }
    }

    // Настенный факел: тёплый свет, мерцание и угольки. В референсе они
    // расставлены по всей каменной кладке и держат на себе весь тёплый
    // полюс картинки против холодной воды и неба.
    protected void WallTorch(Vector3 pos, float yaw, float scale = 1f)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Material iron = Gfx.MatFull(new Color(0.22f, 0.2f, 0.2f), 0.3f, 0.7f,
            Color.black, 0f, 0f);
        Material wood = Gfx.Mat(new Color(0.36f, 0.24f, 0.14f), 0.06f);

        // Кронштейн из стены и наклонная рукоять.
        Gfx.NoShadow(Gfx.Box(transform, pos, new Vector3(0.16f, 0.3f, 0.16f) * scale, iron, false));
        GameObject shaft = Gfx.Cyl(transform, pos + rot * new Vector3(0f, 0.28f, 0.2f) * scale,
            new Vector3(0.07f, 0.3f, 0.07f) * scale, wood, false);
        shaft.transform.localRotation = rot * Quaternion.Euler(28f, 0f, 0f);
        Gfx.NoShadow(shaft);

        Vector3 tip = pos + rot * new Vector3(0f, 0.56f, 0.42f) * scale;
        // Чаша и само пламя.
        Gfx.NoShadow(Gfx.Cyl(transform, tip + new Vector3(0f, -0.06f, 0f),
            new Vector3(0.19f, 0.07f, 0.19f) * scale, iron, false));
        Campfire.Create(transform, tip, new Color(1f, 0.68f, 0.34f), 13f * scale);
    }

    // Друза кристаллов в породе — холодные светящиеся точки, которыми в
    // референсе набита вся пещера.
    protected void GemCluster(Vector3 pos, Color tint, float scale = 1f)
    {
        Material gem = Gfx.MatFull(tint, 0.9f, 0.25f, tint * 0.85f, 0f, 0f);
        int n = Random.Range(3, 6);
        for (int i = 0; i < n; i++)
        {
            float a = i * (6.28f / n) + Random.Range(-0.4f, 0.4f);
            float r = Random.Range(0.05f, 0.24f) * scale;
            Gfx.NoShadow(Gfx.Crystal(transform,
                pos + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r),
                Random.Range(0.09f, 0.17f) * scale,
                Random.Range(0.35f, 0.8f) * scale, gem,
                Random.Range(-24f, 24f)));
        }
        Gfx.Glow(transform, pos + new Vector3(0f, 0.28f * scale, 0f), 2.4f * scale,
            new Color(tint.r, tint.g, tint.b, 0.55f));
        Gfx.PointLight(transform, pos + new Vector3(0f, 0.34f * scale, 0f), tint,
            7f * scale, 0.85f);
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

        // Финишная площадка. Раньше полоса просто обрывалась, а контрольная
        // точка ставилась на «p минус один шаг» — то есть с лишним rise,
        // на два метра выше плиты. При чётном steps последняя плита ещё и
        // движущаяся, так что точка возрождения половину времени висела над
        // пустотой: в Черепахограде — над водой залива.
        Platform(p, new Vector3(5.4f, 0.6f, 5.4f), color);
        AddCoin(p + new Vector3(0f, 1.5f, 0f));
        AddCheckpoint(p + new Vector3(0f, 0.4f, 0f));
    }

    // Вращающееся бревно — сбивает с платформы, если зазеваться.
    //
    // Вместе с бревном строится и сама платформа: раньше девятиметровое
    // бревно крутилось в четырёх метрах над чистым полем, сбивать с него
    // было не с чего, а обойти его можно было просто по земле.
    protected void Spinner(Vector3 pos, float length, float speed, Color color,
        bool withPlatform = true)
    {
        if (withPlatform)
        {
            float r = length * 0.5f + 1.6f;
            // Диск под бревном и столб от него до земли, чтобы площадка
            // не висела в воздухе.
            Platform(pos + new Vector3(0f, -0.9f, 0f), new Vector3(r * 2f, 0.7f, r * 2f), color);
            float ground = GroundHeight(pos.x, pos.z);
            float pillar = Mathf.Max(0.6f, pos.y - 1.25f - ground);
            Gfx.Cyl(transform, new Vector3(pos.x, ground + pillar * 0.5f, pos.z),
                new Vector3(3.4f, pillar * 0.5f, 3.4f),
                Gfx.MatFull(color * 0.8f, 0.05f, 0f, Color.black, 3f, 0.6f));
            AddCoin(pos + new Vector3(r - 1.2f, 1.4f, 0f));
            AddCoin(pos + new Vector3(-(r - 1.2f), 1.4f, 0f));
        }

        GameObject hub = new GameObject("SpinnerHub");
        hub.transform.SetParent(transform, false);
        hub.transform.localPosition = pos;
        Gfx.Box(hub.transform, Vector3.zero, new Vector3(length, 0.7f, 0.7f),
            Gfx.MatFull(color, 0.08f, 0f, Color.black, 2f, 0.4f));
        Spinner sp = hub.AddComponent<Spinner>();
        sp.Axis = Vector3.up;
        sp.Speed = speed;
        sp.Reach = length * 0.5f;
    }

    protected void AddCheckpoint(Vector3 pos)
    {
        Checkpoints.Add(Checkpoint.Create(transform, pos));
        // У каждой контрольной точки лежит сердце: до него игрок как раз
        // добирается потрёпанным, и лечиться прыжком в пропасть больше не надо.
        AddHeart(pos + new Vector3(2.2f, 1.2f, 0f));
    }

    // Скользкий участок. Вида у него нет — лёд уже нарисован, — зона
    // отвечает только за то, что на нём хуже держат ноги.
    protected void Slippery(Vector3 pos, Vector2 size, float slip = 0.18f)
    {
        IceZone.Create(transform, pos, size, slip);
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
            // Тропическая вода: на мели почти бирюза, в глубине насыщенный
            // синий, и широкая полоса пены у берега. Прежние цвета были
            // «просто синими» и мели от глубины не отличали.
            m.SetColor("_ShallowColor", ShallowWater);
            m.SetColor("_DeepColor", DeepWater);
            m.SetFloat("_DepthFade", 3.4f);
            m.SetFloat("_FoamWidth", 1.1f);
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
        Shader ws = Shader.Find("Bear/Waterfall");
        Vector3 center = top + new Vector3(0f, -height * 0.5f, 0f);

        if (ws != null)
        {
            // Три полотна на разной глубине и с разной скоростью. Одно
            // читалось плоской пластиной: у воды нет объёма, если она вся
            // движется одним рисунком в одной плоскости.
            float[] depth = { -0.22f, 0f, 0.26f };
            float[] speed = { 1.35f, 1.9f, 2.5f };
            float[] wide = { 1.06f, 1f, 0.82f };
            float[] alpha = { 0.55f, 0.85f, 0.45f };
            for (int i = 0; i < 3; i++)
            {
                Material m = new Material(ws);
                m.SetFloat("_Speed", speed[i]);
                m.SetFloat("_Alpha", alpha[i]);
                m.SetFloat("_Tiling", Mathf.Max(1.4f, height * 0.16f));
                m.SetColor("_Color", new Color(0.40f, 0.74f, 0.90f));
                GameObject sheet = Gfx.Box(transform,
                    center + new Vector3(0f, 0f, depth[i]),
                    new Vector3(width * wide[i], height, 0.04f), m, false);
                Gfx.NoShadow(sheet);
            }

            // Отдельные струи, сорвавшиеся с гребня: узкие и быстрые.
            int strands = Mathf.Clamp(Mathf.RoundToInt(width * 2.2f), 2, 7);
            for (int i = 0; i < strands; i++)
            {
                Material m = new Material(ws);
                m.SetFloat("_Speed", Random.Range(2.4f, 3.4f));
                m.SetFloat("_Alpha", 0.7f);
                m.SetFloat("_Tiling", 3.2f);
                m.SetFloat("_EdgeFade", 0.45f);
                float sx = Random.Range(-width * 0.46f, width * 0.46f);
                float sh = height * Random.Range(0.55f, 1f);
                GameObject strand = Gfx.Box(transform,
                    top + new Vector3(sx, -sh * 0.5f, Random.Range(-0.3f, 0.34f)),
                    new Vector3(width * Random.Range(0.07f, 0.16f), sh, 0.03f), m, false);
                Gfx.NoShadow(strand);
            }
        }
        else
        {
            Material sheet = Gfx.MatFull(new Color(0.6f, 0.85f, 1f), 0.9f, 0.1f,
                new Color(0.15f, 0.35f, 0.55f), 0f, 0f);
            Gfx.NoShadow(Gfx.Box(transform, center,
                new Vector3(width, height, 0.4f), sheet, false));
        }

        // Гребень: вода перед срывом собирается в валик с пеной.
        Gfx.NoShadow(Gfx.Cyl(transform, top + new Vector3(0f, 0.05f, 0f),
            new Vector3(width * 1.08f, 0.09f, 0.5f),
            Gfx.MatFull(new Color(0.86f, 0.96f, 1f), 0.8f, 0f,
                new Color(0.3f, 0.55f, 0.7f), 0f, 0f), false));

        // Пенное кольцо у подошвы — там, где струя бьёт в воду.
        Vector3 foot = top + new Vector3(0f, -height, 0f);
        Material foamMat = Gfx.AdditiveMat(new Color(0.9f, 0.98f, 1f, 0.5f), Gfx.GlowTexture());
        for (int i = 0; i < 3; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider qc = ring.GetComponent<Collider>();
            if (qc != null) Object.Destroy(qc);
            ring.name = "FallFoam";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = foot + new Vector3(0f, 0.08f + i * 0.05f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float r = width * (1.5f + i * 0.7f);
            ring.transform.localScale = new Vector3(r, r, 1f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = foamMat;
            Gfx.NoShadow(ring);
        }

        ParticleFx mist = ParticleFx.Spawn(transform, foot, particles,
            new Color(0.8f, 0.93f, 1f, 0.75f));
        mist.EmitExtents = new Vector3(width * 0.7f, 0.4f, 0.6f);
        mist.BaseVelocity = new Vector3(0f, 1.4f, 0f);
        mist.Gravity = new Vector3(0f, -0.5f, 0f);
        mist.SpeedMin = 0.3f;
        mist.SpeedMax = 1.6f;
        mist.SizeMin = 0.16f;
        mist.SizeMax = 0.44f;
        mist.LifeMin = 1.2f;
        mist.LifeMax = 2.6f;
        mist.Prewarm();

        ParticleFx spray = ParticleFx.Spawn(transform, center, particles,
            new Color(0.9f, 0.96f, 1f, 0.6f));
        spray.EmitExtents = new Vector3(width * 0.5f, height * 0.45f, 0.35f);
        spray.BaseVelocity = new Vector3(0f, -7f, 0f);
        spray.Gravity = new Vector3(0f, -3f, 0f);
        spray.SpeedMin = 0.1f;
        spray.SpeedMax = 0.8f;
        spray.SizeMin = 0.06f;
        spray.SizeMax = 0.15f;
        spray.LifeMin = 0.5f;
        spray.LifeMax = 1.1f;
        spray.Prewarm();
    }

    // Каменная постройка из собственных ассетов. Мох ей дорисовывает
    // шейдер: CustomProp приносит материалы из FBX, поэтому подменяем их
    // на Bear/MossyStone — иначе арка вышла бы чисто вымытой, а вокруг
    // всё поросшим.
    protected GameObject StoneProp(string id, Vector3 pos, float height, float yaw,
        Color tint)
    {
        GameObject go = Gfx.CustomProp(transform, id, pos, height, yaw);
        if (go == null) return null;

        Color moss = Color.Lerp(tint, new Color(0.3f, 0.58f, 0.24f), 0.6f);
        Color stone = Color.Lerp(new Color(0.56f, 0.54f, 0.51f), tint, 0.2f);
        Material m = Gfx.MossyMat(stone, moss, 0.55f, 0.6f);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null) continue;
            Material[] set = new Material[rs[i].sharedMaterials.Length];
            for (int k = 0; k < set.Length; k++) set[k] = m;
            rs[i].sharedMaterials = set;
        }
        return go;
    }

    // Арка-проём с лучом света и факелами по сторонам — готовый узел,
    // из которого в референсе собрана вся каменная часть.
    protected void ArchGate(Vector3 pos, float yaw, float height, Color tint,
        bool shaft = true)
    {
        StoneProp("stone_arch", pos, height, yaw, tint);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        float half = height * 0.32f;
        for (int sx = -1; sx <= 1; sx += 2)
            WallTorch(pos + rot * new Vector3(sx * half, height * 0.45f, 0f),
                yaw + sx * 90f, 1f);
        // Свет из проёма падает внутрь — арка перестаёт быть плоской рамкой.
        if (shaft)
            LightShaft(pos + new Vector3(0f, height * 0.92f, 0f),
                height * 0.9f, half * 1.3f,
                new Color(1f, 0.93f, 0.72f), 0.16f);
        Vine(pos + rot * new Vector3(-half * 0.8f, height * 0.86f, 0f),
            Random.Range(1f, 2.2f), Color.Lerp(tint, new Color(0.3f, 0.6f, 0.3f), 0.7f));
        Vine(pos + rot * new Vector3(half * 0.8f, height * 0.86f, 0f),
            Random.Range(1f, 2.2f), Color.Lerp(tint, new Color(0.3f, 0.6f, 0.3f), 0.7f));
    }

    // Объёмный луч света: конус аддитивного свечения от проёма вниз.
    // Вершина конуса — сам проём, поэтому позиция задаётся по низу луча.
    //
    // Про strength: конус рисуется без отсечения грани, поэтому передняя и
    // задняя стенки прибавляют свет каждая — видимая яркость выходит около
    // 2 * strength. Замер на превью показал, что при 0.13 прибавка к фону
    // была 0.25, то есть вдвое ярче самого фона, и луч читался белым
    // молоком. Рабочий диапазон — сотые и первые десятые доли.
    protected void LightShaft(Vector3 apex, float length, float spread, Color tint,
        float strength = 0.16f)
    {
        Shader sh = Shader.Find("Bear/Shaft");
        if (sh == null) return;

        Material m = new Material(sh);
        m.SetColor("_Color", tint);
        m.SetFloat("_Strength", strength);

        // Конус строится вершиной вверх от своей позиции, значит ставим его
        // на дальний конец луча, а вершина сама придётся на проём.
        GameObject go = Gfx.Cone(transform, apex + new Vector3(0f, -length, 0f),
            spread, length, m);
        Gfx.NoShadow(go);

        // Пылинки в луче — без них он выглядит нарисованным.
        ParticleFx dust = ParticleFx.Spawn(transform,
            apex + new Vector3(0f, -length * 0.5f, 0f), 26,
            new Color(tint.r, tint.g, tint.b, 0.5f));
        dust.EmitExtents = new Vector3(spread * 0.5f, length * 0.45f, spread * 0.5f);
        dust.BaseVelocity = new Vector3(0.15f, -0.3f, 0.1f);
        dust.Gravity = new Vector3(0.05f, -0.08f, 0.03f);
        dust.SpeedMin = 0.04f;
        dust.SpeedMax = 0.2f;
        dust.SizeMin = 0.04f;
        dust.SizeMax = 0.1f;
        dust.LifeMin = 4f;
        dust.LifeMax = 8f;
        dust.Prewarm();
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
        // Мельница — собственный ассет: сужающаяся башня, опоясывающий
        // балкон с перилами, купольный колпак и хвостовое бревно. Крылья
        // отдельным файлом, потому что их надо вращать.
        // Высота 15 м при домах в 5 делала мельницу не ориентиром, а
        // великаном посреди деревни. 10.5 — вдвое выше дома, этого хватает.
        const float MillHeight = 10.5f;
        // Вал крыльев в модели сидит на 0.862 её высоты (см. build_assets.py).
        const float HubFraction = 0.862f;
        const float BladeSpan = 7.4f;

        if (Gfx.CustomProp(transform, "windmill", pos, MillHeight, 0f) != null)
        {
            GameObject rotor = new GameObject("Blades");
            rotor.transform.SetParent(transform, false);
            rotor.transform.localPosition =
                pos + new Vector3(0f, MillHeight * HubFraction, 2.4f);
            // anchorCenter: втулка крыльев должна попасть НА ось вращения.
            // Иначе модель сдвигается низом в ноль, центр уезжает на
            // пол-размаха вверх, и махи ходят огромным кругом, ныряя под землю.
            Gfx.CustomProp(rotor.transform, "windmill_blades", Vector3.zero,
                BladeSpan, 0f, true);

            Spinner rot = rotor.AddComponent<Spinner>();
            rot.Axis = Vector3.forward;
            rot.Speed = 22f;
            // Махи крутятся в девяти метрах над землёй: толкать там некого,
            // а Sweep рассчитан на бревно, лежащее плашмя.
            rot.Push = false;

            Gfx.PointLight(transform, pos + new Vector3(0f, MillHeight, 0f),
                new Color(1f, 0.85f, 0.55f), 18f, 0.8f);
            return;
        }

        // Запасная сборка из примитивов, если модель не загрузилась.
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

    // Сердце восстанавливает единицу здоровья.
    protected void AddHeart(Vector3 pos)
    {
        int id = NetManager.HeartIdBase + _heartCounter++;
        Coin c = Coin.Spawn(transform, pos, id);
        Coins[id] = c;
    }

    // Звезда на собственном постаменте: снизу батут, наверху площадка.
    // Расставлять звёзды по выверенным координатам оказалось ненадёжно —
    // они попадали внутрь крон и крыш. Постамент делает награду
    // достижимой из любой точки под ней и заметной издалека.
    protected void AddStarPedestal(Vector3 groundPos, float height)
    {
        height = Mathf.Clamp(height, 4f, 22f);

        // Над батутом ничего быть не должно: раньше игрок упирался головой
        // в плиту постамента и до звезды не долетал. Колонны стоят кольцом
        // по краям, а путь вверх остаётся свободным.
        AddBouncePad(groundPos + new Vector3(0f, 0.4f, 0f),
            Mathf.Sqrt(2f * 22f * (height + 2.5f)));

        Material stone = Gfx.MatFull(new Color(0.68f, 0.62f, 0.52f), 0.1f, 0f,
            Color.black, 3f, 0.4f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad + 0.78f;
            // Колонны без коллайдера — они обрамляют, но не мешают.
            Gfx.Cyl(transform,
                groundPos + new Vector3(Mathf.Cos(a) * 3.6f, height * 0.45f, Mathf.Sin(a) * 3.6f),
                new Vector3(0.5f, height * 0.45f, 0.5f), stone, false);
            // Уступы по краям: до звезды можно и допрыгать, а не только взлететь.
            Platform(groundPos + new Vector3(Mathf.Cos(a) * 3.6f, height * 0.9f + 0.3f,
                Mathf.Sin(a) * 3.6f), new Vector3(2.6f, 0.5f, 2.6f),
                new Color(0.72f, 0.66f, 0.55f));
        }

        Gfx.Cyl(transform, groundPos + new Vector3(0f, 0.15f, 0f),
            new Vector3(9f, 0.3f, 9f), stone);

        Vector3 star = groundPos + new Vector3(0f, height, 0f);
        Gfx.PointLight(transform, star, new Color(1f, 0.85f, 0.45f), 20f, 1.4f);
        AddStarPickup(star);
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
