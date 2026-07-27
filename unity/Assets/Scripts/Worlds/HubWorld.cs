using UnityEngine;

// Медвежья деревня — стартовый мир: холмы, площадь с фонтаном, домики,
// мельница, рынок, озеро с мостом и причалом, четыре портала.
public class HubWorld : WorldBuilder
{
    private static readonly Color GrassLow = new Color(0.26f, 0.5f, 0.22f);
    private static readonly Color GrassHigh = new Color(0.44f, 0.72f, 0.32f);
    private static readonly Color PathCol = new Color(0.82f, 0.7f, 0.48f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 1.5f, 14f);

        SetupSky(new Color(0.26f, 0.54f, 0.96f), new Color(0.8f, 0.89f, 0.98f),
            new Color(0.24f, 0.31f, 0.24f), new Vector3(46f, 35f, 0f), 1.15f, 0.0016f,
            new Color(0.78f, 0.86f, 0.95f));
        SetPostFx(1.0f, 1.12f, new Color(1.02f, 1f, 0.97f), 0.5f);

        // Ровные места под озеро и русло ручья
        FlattenArea(34f, 30f, 16f, 11f);
        FlattenArea(-30f, 14f, 10f, 9f);

        // Холмистый остров
        // Настоящие PBR-текстуры поверхности
        TerrainTextures("Grass003_1K-PNG_Color", "Grass003_1K-PNG_NormalGL",
            "marble_rock_03_diff_1k", "marble_rock_03_nor_gl_1k", 0.11f, 0.14f);

        Terrain(Vector3.zero, new Vector2(210f, 210f), 140, 9f, 0.011f,
            GrassLow, GrassHigh, 40f, 1812);

        // Обрыв по краю острова, чтобы мир выглядел парящим
        Material cliff = Gfx.MatFull(new Color(0.44f, 0.31f, 0.19f), 0.03f, 0f, Color.black, 5f, 0.7f);
        Gfx.Cyl(transform, new Vector3(0f, -9f, 0f), new Vector3(208f, 8f, 208f), cliff, false);
        Gfx.Cone(transform, new Vector3(0f, -32f, 0f), 94f, 26f, cliff);

        BuildPlaza();
        BuildHouses();
        BuildLake();
        AddCheckpoint(new Vector3(0f, 0.5f, 12f));
        BuildForest();
        BuildAtmosphere();

        // Старейшина и порталы вокруг площади
        AddNpc(new Vector3(4.5f, 0f, -4.5f));
        AddPortal(new Vector3(-28f, 1.5f, 0f), NetManager.WorldMeadow,
            "Солнечные луга", new Color(0.4f, 1f, 0.5f), 90f);
        AddPortal(new Vector3(28f, 1.5f, 0f), NetManager.WorldDesert,
            "Песчаный каньон", new Color(1f, 0.72f, 0.3f), 90f);
        AddPortal(new Vector3(0f, 1.5f, -30f), NetManager.WorldSnow,
            "Снежные вершины", new Color(0.55f, 0.82f, 1f), 0f);
        AddPortal(new Vector3(0f, 1.5f, 32f), NetManager.WorldCave,
            "Кристальная пещера", new Color(0.75f, 0.5f, 1f), 0f);
        AddPortal(new Vector3(-20f, 1.5f, 22f), NetManager.WorldTurtle,
            "Черепахоград", new Color(0.4f, 0.9f, 0.75f), 45f);

        // Жители деревни — сюжет и подсказки
        AddVillager(new Vector3(-8f, 0f, -7f), "Пекарь", "Pig", 1.6f,
            "Сердце горы пропало в ту же ночь, когда потухли все печи в деревне.\nСовпадение? Старейшина говорит, что нет.");
        AddVillager(new Vector3(10f, 0f, 5f), "Торговка", "Chicken", 1.4f,
            "Монеты у нас не простые — они помнят дорогу домой.\nЧем больше соберёшь, тем больше путей откроется.");
        AddVillager(new Vector3(-13f, 0f, 9f), "Путешественник", "Cyclops", 2.1f,
            "Я видел город на спинах спящих черепах.\nОн всплывает, только когда в деревне снова становится тепло.");
    }

    private void BuildPlaza()
    {
        Material stone = Gfx.MatFull(new Color(0.72f, 0.71f, 0.68f), 0.06f, 0f, Color.black, 4f, 0.3f);
        Gfx.Cyl(transform, new Vector3(0f, 0.06f, 0f), new Vector3(34f, 0.06f, 34f), stone);

        // Дорожки к порталам
        // Тени от плоских дорожек ложились на холмы тёмными полосами.
        Material path = Gfx.MatFull(PathCol, 0.05f, 0f, Color.black, 4f, 0.4f);
        Gfx.NoShadow(Gfx.Box(transform, new Vector3(0f, 0.08f, 24f), new Vector3(5f, 0.12f, 32f), path));
        Gfx.NoShadow(Gfx.Box(transform, new Vector3(0f, 0.08f, -24f), new Vector3(5f, 0.12f, 32f), path));
        Gfx.NoShadow(Gfx.Box(transform, new Vector3(-24f, 0.08f, 0f), new Vector3(32f, 0.12f, 5f), path));
        Gfx.NoShadow(Gfx.Box(transform, new Vector3(24f, 0.08f, 0f), new Vector3(32f, 0.12f, 5f), path));

        // Фонтан
        Material basin = Gfx.MatFull(new Color(0.75f, 0.74f, 0.72f), 0.12f, 0f, Color.black, 3f, 0.3f);
        Gfx.Cyl(transform, new Vector3(0f, 0.45f, 0f), new Vector3(7.2f, 0.45f, 7.2f), basin);
        Gfx.Cyl(transform, new Vector3(0f, 0.75f, 0f), new Vector3(6.2f, 0.15f, 6.2f), basin, false);
        Water(new Vector3(0f, 0.86f, 0f), new Vector2(6f, 6f));
        Gfx.Cyl(transform, new Vector3(0f, 1.6f, 0f), new Vector3(1.1f, 0.85f, 1.1f), basin);
        Gfx.Ball(transform, new Vector3(0f, 2.6f, 0f), new Vector3(1.5f, 1.5f, 1.5f),
            Gfx.MatFull(new Color(0.85f, 0.85f, 0.9f), 0.5f, 0.2f, new Color(0.1f, 0.15f, 0.2f), 0f, 0f));
        Fountain(new Vector3(0f, 3.3f, 0f), 55);
        Gfx.PointLight(transform, new Vector3(0f, 3.5f, 0f), new Color(0.6f, 0.85f, 1f), 18f, 0.8f);

        // Рынок и костёр
        MarketStall(new Vector3(-9f, 0f, 8f), new Color(0.85f, 0.35f, 0.35f), 25f);
        MarketStall(new Vector3(9.5f, 0f, 7f), new Color(0.35f, 0.55f, 0.85f), -20f);
        MarketStall(new Vector3(-10f, 0f, -8f), new Color(0.9f, 0.75f, 0.3f), 155f);
        AddCampfire(new Vector3(11f, 0.1f, -8f), new Color(1f, 0.65f, 0.35f), 16f);

        // Скамейки вокруг фонтана
        Material wood = Gfx.MatFull(new Color(0.5f, 0.36f, 0.22f), 0.05f, 0f, Color.black, 2f, 0.4f);
        for (int i = 0; i < 4; i++)
        {
            float a = 45f + i * 90f;
            Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 9f, 0.4f, Mathf.Sin(a * Mathf.Deg2Rad) * 9f);
            GameObject bench = Gfx.Box(transform, p, new Vector3(2.6f, 0.16f, 0.7f), wood);
            bench.transform.localRotation = Quaternion.Euler(0f, -a, 0f);
        }
    }

    private void BuildHouses()
    {
        House(new Vector3(-21f, 0f, -20f), new Color(0.86f, 0.62f, 0.42f), 22f, 1.5f);
        House(new Vector3(21f, 0f, -21f), new Color(0.62f, 0.72f, 0.9f), -24f, 1.65f);
        House(new Vector3(-31f, 0f, 15f), new Color(0.9f, 0.82f, 0.52f), 68f, 1.4f);
        House(new Vector3(32f, 0f, 18f), new Color(0.78f, 0.55f, 0.62f), -62f, 1.55f);
        House(new Vector3(-11f, 0f, 30f), new Color(0.7f, 0.85f, 0.7f), 172f, 1.35f);
        House(new Vector3(42f, 0f, -6f), new Color(0.88f, 0.7f, 0.5f), -95f, 1.7f);

        Windmill(OnGround(-48f, -36f), new Color(0.85f, 0.82f, 0.74f), new Color(0.92f, 0.9f, 0.85f));
    }

    private void BuildLake()
    {
        // Котлован под озеро
        Material bed = Gfx.MatFull(new Color(0.46f, 0.4f, 0.28f), 0.03f, 0f, Color.black, 4f, 0.5f);
        Gfx.Box(transform, new Vector3(34f, -1.1f, 30f), new Vector3(30f, 2f, 26f), bed);
        Water(new Vector3(34f, 0.15f, 30f), new Vector2(29f, 25f));

        Dock(new Vector3(26f, 0.35f, 24f), 8f, 35f);
        AddCoin(new Vector3(30f, 1.4f, 28f));
        AddCoin(new Vector3(36f, 1.4f, 33f));

        // Мостик через ручей к лугам
        Material bedStream = Gfx.MatFull(new Color(0.45f, 0.4f, 0.3f), 0.03f, 0f, Color.black, 4f, 0.5f);
        Gfx.Box(transform, new Vector3(-30f, -0.9f, 6f), new Vector3(9f, 2f, 34f), bedStream);
        Water(new Vector3(-30f, 0.1f, 6f), new Vector2(8.5f, 33f));
        Bridge(new Vector3(-26f, 1.1f, 6f), new Vector3(-34f, 1.1f, 6f), 4.4f,
            new Color(0.55f, 0.4f, 0.25f));
        AddCoin(new Vector3(-30f, 1.9f, 6f));

        // Камни-ступеньки в ручье
        Color stone = new Color(0.6f, 0.58f, 0.54f);
        Rock(new Vector3(-30f, 0.4f, 14f), 1.9f, stone);
        Rock(new Vector3(-28.5f, 0.35f, 18f), 1.6f, stone);
        Rock(new Vector3(-31f, 0.4f, -2f), 2.1f, stone);
    }

    private void BuildForest()
    {
        // Роща по краям острова: деревья садятся на рельеф
        int seedBase = 41;
        for (int i = 0; i < 58; i++)
        {
            float ang = (i * 137.5f) * Mathf.Deg2Rad;
            float r = 46f + (i % 7) * 7.5f;
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            if (Mathf.Abs(x - 34f) < 18f && Mathf.Abs(z - 30f) < 16f) continue; // не в озере
            if (Mathf.Abs(x + 30f) < 7f) continue;                              // не в ручье

            Color leaf = Color.HSVToRGB(0.26f + ((i + seedBase) % 5) * 0.015f, 0.55f, 0.5f + (i % 3) * 0.07f);
            Tree(OnGround(x, z), leaf, 0.85f + (i % 4) * 0.18f);
        }

        // Большое дерево-ориентир
        Tree(OnGround(-40f, 34f), new Color(0.24f, 0.55f, 0.26f), 2.6f);

        Color[] bushCols = {
            new Color(0.2f, 0.45f, 0.2f), new Color(0.26f, 0.5f, 0.18f), new Color(0.18f, 0.42f, 0.24f)
        };
        for (int i = 0; i < 40; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(28f, 82f);
            Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            if (Mathf.Abs(p.x - 34f) < 18f && Mathf.Abs(p.z - 30f) < 16f) continue;
            Bush(OnGround(p.x, p.z), bushCols[i % 3]);
        }

        Color[] flowers = {
            new Color(0.95f, 0.4f, 0.45f), new Color(0.95f, 0.85f, 0.3f),
            new Color(0.6f, 0.5f, 0.95f), new Color(1f, 0.65f, 0.3f)
        };
        for (int i = 0; i < 90; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(20f, 88f);
            Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            if (Mathf.Abs(p.x - 34f) < 17f && Mathf.Abs(p.z - 30f) < 15f) continue;
            Flower(OnGround(p.x, p.z), flowers[i % 4]);
        }

        Rock(OnGround(-18f, 26f), 2.6f, new Color(0.56f, 0.56f, 0.58f));
        Rock(OnGround(24f, -24f), 3.1f, new Color(0.54f, 0.54f, 0.57f));
        Rock(OnGround(-42f, -12f), 2.2f, new Color(0.58f, 0.57f, 0.6f));
    }

    private void BuildAtmosphere()
    {
        GrassField(new Vector3(0f, 0f, 0f), new Vector2(78f, 78f), 15200,
            new Color(0.18f, 0.42f, 0.12f), new Color(0.55f, 0.85f, 0.3f), true);

        Cloud(new Vector3(-38f, 30f, -26f), 2.2f, Color.white);
        Cloud(new Vector3(20f, 34f, -42f), 2.9f, Color.white);
        Cloud(new Vector3(48f, 27f, 14f), 2f, Color.white);
        Cloud(new Vector3(-18f, 36f, 38f), 2.4f, Color.white);
        Cloud(new Vector3(62f, 31f, -12f), 2.6f, Color.white);
        Cloud(new Vector3(-58f, 33f, 8f), 2.3f, Color.white);

        Color mnt = new Color(0.44f, 0.52f, 0.68f);
        Mountain(new Vector3(-110f, -8f, -86f), 40f, 54f, mnt, true);
        Mountain(new Vector3(-34f, -8f, -128f), 48f, 66f, mnt * 0.93f, true);
        Mountain(new Vector3(74f, -8f, -112f), 36f, 47f, mnt, true);
        Mountain(new Vector3(126f, -8f, -24f), 42f, 56f, mnt * 1.05f, true);
        Mountain(new Vector3(112f, -8f, 86f), 33f, 42f, mnt, false);
        Mountain(new Vector3(-120f, -8f, 56f), 38f, 50f, mnt * 0.96f, true);
        Mountain(new Vector3(-70f, -8f, 118f), 34f, 44f, mnt * 0.9f, false);

        Motes(new Vector3(0f, 4f, 0f), new Vector3(42f, 3.5f, 42f), 90,
            new Color(1f, 0.95f, 0.62f, 0.7f), 0.14f);

        // Монеты по деревне
        AddCoin(new Vector3(0f, 1.2f, 9f));
        AddCoin(new Vector3(7f, 1.2f, 6f));
        AddCoin(new Vector3(-7f, 1.2f, 6f));
        AddCoin(OnGround(-22f, -20f, 1.2f));
        AddCoin(OnGround(22f, -20f, 1.2f));
        AddCoin(OnGround(-40f, 34f, 5.5f));
        AddCoin(OnGround(-34f, -26f, 8f));
        AddCoin(new Vector3(11f, 1.2f, -8f));
    }

    private void House(Vector3 pos, Color wall, float yaw, float scale)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 baseP = OnGround(pos.x, pos.z);

        Material wallMat = Gfx.MatFull(wall, 0.05f, 0f, Color.black, 2.4f, 0.3f);
        GameObject body = Gfx.Box(transform, baseP + new Vector3(0f, 1.7f * scale, 0f),
            new Vector3(5.6f, 3.4f, 5f) * scale, wallMat);
        body.transform.localRotation = rot;

        // Фахверк — тёмные балки по углам
        Material beam = Gfx.Mat(new Color(0.32f, 0.22f, 0.14f), 0.05f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                GameObject b = Gfx.Box(transform,
                    baseP + rot * new Vector3(2.7f * sx * scale, 1.7f * scale, 2.4f * sz * scale),
                    new Vector3(0.34f, 3.5f, 0.34f) * scale, beam, false);
                b.transform.localRotation = rot;
            }
        }

        GameObject roof = Gfx.Cone(transform, baseP + new Vector3(0f, 3.4f * scale, 0f),
            4.6f * scale, 2.8f * scale, Gfx.Mat(new Color(0.58f, 0.26f, 0.21f), 0.05f));
        roof.transform.localRotation = Quaternion.Euler(0f, yaw + 45f, 0f);

        Material door = Gfx.Mat(new Color(0.36f, 0.23f, 0.13f), 0.05f);
        GameObject d = Gfx.Box(transform, baseP + rot * new Vector3(0f, 1f * scale, 2.55f * scale),
            new Vector3(1.2f, 2f, 0.18f) * scale, door);
        d.transform.localRotation = rot;

        // Тёплые окна со светом
        Material win = Gfx.MatFull(new Color(1f, 0.9f, 0.58f), 0.4f, 0f,
            new Color(1f, 0.78f, 0.35f), 0f, 0f);
        float[] offsets = { -1.8f, 1.8f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 wp = baseP + rot * new Vector3(offsets[i] * scale, 1.9f * scale, 2.54f * scale);
            GameObject w = Gfx.Box(transform, wp, new Vector3(1f, 1f, 0.14f) * scale, win, false);
            w.transform.localRotation = rot;
            Gfx.Glow(transform, wp + rot * new Vector3(0f, 0f, 0.35f), 2.6f * scale,
                new Color(1f, 0.8f, 0.4f, 0.4f));
        }
        Gfx.PointLight(transform, baseP + rot * new Vector3(0f, 2f * scale, 3.4f * scale),
            new Color(1f, 0.78f, 0.45f), 11f * scale, 1.1f);

        // Труба
        GameObject chimney = Gfx.Box(transform, baseP + rot * new Vector3(1.6f * scale, 5.2f * scale, -1f * scale),
            new Vector3(0.8f, 1.8f, 0.8f) * scale, Gfx.Mat(new Color(0.5f, 0.42f, 0.38f), 0.05f), false);
        chimney.transform.localRotation = rot;
    }
}
