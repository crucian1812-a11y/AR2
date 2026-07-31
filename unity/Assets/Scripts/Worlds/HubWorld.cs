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
        // Звёзды — цель мира, каждая на своём постаменте с батутом
        AddStarPedestal(OnGround(-52f, 40f), 12f);
        AddStarPedestal(OnGround(46f, -30f), 9f);
        // Тренировочная полоса за мельницей
        ObstacleRun(OnGround(-62f, -20f, 2.1f), new Vector3(0.4f, 0f, 1f), 6, 1.5f,
            new Color(0.5f, 0.7f, 0.4f));

        // Грибы на окраинах острова. В самой деревне врагов нет — площадь
        // остаётся безопасной, — но за околицей есть на ком потренировать
        // прыжок на голову до того, как уйдёшь в первый мир.
        AddEnemy(OnGround(-48f, 34f, 0.1f), OnGround(-56f, 44f, 0.1f), "mushroom", 2.2f);
        AddEnemy(OnGround(42f, -26f, 0.1f), OnGround(50f, -34f, 0.1f), "mushroom", 2.4f);
        AddEnemy(OnGround(-58f, -14f, 0.1f), OnGround(-64f, -26f, 0.1f), "mushroom", 2.6f);
        AddEnemy(OnGround(30f, 44f, 0.1f), OnGround(44f, 44f, 0.1f), "mushroom", 2.3f);
        // Кубические — тоже за околицей: площадь остаётся безопасной.
        AddEnemy(OnGround(-38f, 48f, 0.1f), OnGround(-48f, 52f, 0.1f), "creeper", 2.1f);
        AddEnemy(OnGround(54f, 16f, 0.1f), OnGround(58f, 28f, 0.1f), "zombie", 2f);
        BuildAtmosphere();

        // Каменные руины и арки: мир должен выглядеть построенным задолго
        // до игрока, а не собранным из кубиков к его приходу.
        for (int i = 0; i < 4; i++)
        {
            float a = 0.6f + i * 92f * Mathf.Deg2Rad;
            float r = 38f + (i % 2) * 9f;
            StoneProp("ruin_wall", OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.1f),
                3.8f, -a * Mathf.Rad2Deg + 90f, PathCol);
        }
        for (int i = 0; i < 3; i++)
        {
            float a = 1.9f + i * 118f * Mathf.Deg2Rad;
            float r = 30f;
            StoneProp("stone_column", OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.1f),
                5.8f, i * 47f, PathCol);
        }
        // Лучи солнца сквозь листву — те самые столбы света в референсе.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 97f * Mathf.Deg2Rad;
            float r = 24f + (i % 3) * 12f;
            LightShaft(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 17f),
                16f, 2.2f, new Color(1f, 0.96f, 0.74f), 0.1f);
        }
        // Зелень по камню: без неё платформы выглядят положенными сверху.
        for (int i = 0; i < 14; i++)
        {
            float a = i * 137.5f * Mathf.Deg2Rad;
            float r = 26f + (i % 5) * 8f;
            Fern(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.05f),
                new Color(0.22f, 0.5f, 0.24f), Random.Range(0.8f, 1.4f));
            if (i % 3 == 0)
                FlowerPatch(OnGround(Mathf.Cos(a) * (r + 4f), Mathf.Sin(a) * (r + 4f), 0.05f),
                    i % 2 == 0 ? new Color(0.95f, 0.6f, 0.8f) : new Color(0.98f, 0.85f, 0.45f));
        }
        BuildResources();

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

        // Указатели у развилок — собственный ассет из tools/blender
        Gfx.CustomProp(transform, "signpost", new Vector3(-6f, 0.1f, 18f), 2.6f, 20f);
        Gfx.CustomProp(transform, "signpost", new Vector3(7f, 0.1f, -16f), 2.6f, -150f);
        Gfx.CustomProp(transform, "treasure_chest", new Vector3(13f, 0.1f, 6f), 1.3f, -25f);

        // Жители деревни — сюжет и подсказки
        AddVillager(new Vector3(-8f, 0f, -7f), "Пекарь", "Pig", 1.6f,
            "Сердце горы пропало в ту же ночь, когда потухли все печи в деревне.\nСовпадение? Старейшина говорит, что нет.");
        AddShopkeeper(new Vector3(10f, 0f, 5f), "Торговка", "Chicken", 1.4f);
        AddVillager(new Vector3(-13f, 0f, 9f), "Путешественник", "Cyclops", 2.1f,
            "Я видел город на спинах спящих черепах.\nОн всплывает, только когда в деревне снова становится тепло.");
    }

    // Добыча и сундуки на окраинах. В самой деревне ничего не ломается —
    // площадь должна оставаться площадью.
    private void BuildResources()
    {
        Weather.Attach(transform, Weather.Kind.Rain, 1812);

        Color stone = new Color(0.6f, 0.58f, 0.54f);
        Vector3[] rocks = {
            new Vector3(-40f, 0f, 18f), new Vector3(-44f, 0f, 24f),
            new Vector3(38f, 0f, -34f), new Vector3(45f, 0f, -28f),
            new Vector3(-52f, 0f, -30f), new Vector3(26f, 0f, 48f),
            new Vector3(-18f, 0f, 52f), new Vector3(56f, 0f, 12f)
        };
        for (int i = 0; i < rocks.Length; i++)
            OreRock(OnGround(rocks[i].x, rocks[i].z, 0.3f), 2f + (i % 3) * 0.5f, stone);

        Color leaf = new Color(0.3f, 0.6f, 0.32f);
        Vector3[] trees = {
            new Vector3(-36f, 0f, 8f), new Vector3(-42f, 0f, -6f),
            new Vector3(34f, 0f, 34f), new Vector3(48f, 0f, 30f),
            new Vector3(-24f, 0f, -44f), new Vector3(14f, 0f, -50f)
        };
        for (int i = 0; i < trees.Length; i++)
            OreTree(OnGround(trees[i].x, trees[i].z), leaf, 0.9f + (i % 3) * 0.15f);

        AddChest(OnGround(-58f, 12f, 0.1f), 30f, 8, Res.Wood, 4);
        AddChest(OnGround(52f, -46f, 0.1f), -120f, 12, Res.Stone, 5);
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

        BuildLamps();
        BuildProps();
    }

    // Фонари вдоль дорожек. Каждый со своим источником света — именно они
    // превращают ровно освещённую площадь в вечернюю деревню: дорожки
    // читаются цепочкой тёплых пятен, у столбов ложатся длинные тени.
    private void BuildLamps()
    {
        Vector3[] spots = {
            new Vector3(-4.2f, 0f, 13f), new Vector3(4.2f, 0f, 13f),
            new Vector3(-4.2f, 0f, -13f), new Vector3(4.2f, 0f, -13f),
            new Vector3(-13f, 0f, 4.2f), new Vector3(-13f, 0f, -4.2f),
            new Vector3(13f, 0f, 4.2f), new Vector3(13f, 0f, -4.2f),
            new Vector3(-4.2f, 0f, 27f), new Vector3(4.2f, 0f, 27f),
            new Vector3(-4.2f, 0f, -27f), new Vector3(4.2f, 0f, -27f),
            new Vector3(-27f, 0f, 4.2f), new Vector3(27f, 0f, -4.2f)
        };
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 p = OnGround(spots[i].x, spots[i].z, 0.05f);
            if (Gfx.CustomProp(transform, "lamp_post", p, 4.2f, i * 37f) == null) continue;
            Vector3 head = p + new Vector3(0f, 3.45f, 0f);
            Gfx.Glow(transform, head, 2.2f, new Color(1f, 0.87f, 0.55f, 0.65f));
            Gfx.PointLight(transform, head, new Color(1f, 0.84f, 0.5f), 15f, 1.35f);
        }
    }

    // Мелочь, от которой площадь выглядит обжитой: колодец, телега у лавки,
    // бочки у прилавков.
    private void BuildProps()
    {
        Vector3 wellPos = OnGround(-14f, 12f, 0.05f);
        if (Gfx.CustomProp(transform, "well", wellPos, 3.4f, 28f) != null)
        {
            Gfx.PointLight(transform, wellPos + new Vector3(0f, 2.6f, 0f),
                new Color(0.7f, 0.85f, 1f), 9f, 0.5f);
            AddCoin(wellPos + new Vector3(0f, 3.9f, 0f));
        }

        Gfx.CustomProp(transform, "cart", OnGround(13.5f, 3.2f, 0.05f), 1.5f, -60f);
        Gfx.CustomProp(transform, "cart", OnGround(-12f, -11f, 0.05f), 1.5f, 140f);

        Vector3[] barrels = {
            new Vector3(-11.4f, 0f, 6.2f), new Vector3(-12.6f, 0f, 7.6f),
            new Vector3(11.6f, 0f, 9.1f), new Vector3(-12.2f, 0f, -6.4f),
            new Vector3(9.2f, 0f, -10.6f)
        };
        for (int i = 0; i < barrels.Length; i++)
            Gfx.CustomProp(transform, "barrel", OnGround(barrels[i].x, barrels[i].z, 0.05f),
                1.15f, i * 53f);
    }

    // Палисадник: звенья штакетника по дуге перед домом.
    private void Garden(Vector3 center, float yaw, int segments, float radius)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = (yaw + (i - (segments - 1) * 0.5f) * 26f) * Mathf.Deg2Rad;
            Vector3 p = OnGround(center.x + Mathf.Sin(a) * radius,
                                 center.z + Mathf.Cos(a) * radius, 0.05f);
            Gfx.CustomProp(transform, "fence", p, 1.5f, a * Mathf.Rad2Deg + 90f);
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

        // Палисадники перед домами — участки читаются как чьи-то дворы,
        // а не как дома, расставленные по чистому полю.
        Garden(new Vector3(-21f, 0f, -20f), 22f, 5, 7.5f);
        Garden(new Vector3(21f, 0f, -21f), -24f, 5, 7.8f);
        Garden(new Vector3(-31f, 0f, 15f), 68f, 4, 7.2f);
        Garden(new Vector3(42f, 0f, -6f), -95f, 4, 8f);

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
        // Монета стояла внутри ствола дерева-ориентира — отодвинута.
        AddCoin(OnGround(-36f, 30f, 1.2f));
        // Восемь метров над травой недостижимы: потолок прыжка 5.2 м.
        AddCoin(OnGround(-34f, -26f, 4.5f));
        AddCoin(new Vector3(11f, 1.2f, -8f));
    }

    private void House(Vector3 pos, Color wall, float yaw, float scale)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 baseP = OnGround(pos.x, pos.z);

        // Дом — собственный ассет из tools/blender: каменный цоколь, фахверк
        // с раскосами, черепица рядами, слуховое окно, крыльцо и труба с
        // колпаком. Раньше это была коробка с конусом сверху.
        //
        // Свет остаётся за кодом: модель не умеет светиться окнами и
        // дымить трубой, а именно это делает деревню обжитой.
        if (Gfx.CustomProp(transform, "cottage", baseP, 3.6f * scale, yaw) != null)
        {
            HouseLights(baseP, rot, scale, wall);
            return;
        }

        // Запасная сборка из примитивов, если модель не загрузилась.
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

    // Свет и дым для дома-ассета. Каждый дом получает свой оттенок окон —
    // издалека деревня читается россыпью тёплых огней, а не одинаковыми
    // домиками.
    private void HouseLights(Vector3 baseP, Quaternion rot, float scale, Color wall)
    {
        float s = scale * 0.72f;
        Color warm = Color.Lerp(new Color(1f, 0.78f, 0.42f), wall, 0.25f);

        // Ореолы в окнах фасада и боковых стен
        Vector3[] win = {
            new Vector3(-1.3f, 1.6f, 1.9f), new Vector3(1.3f, 1.6f, 1.9f),
            new Vector3(-2.1f, 1.6f, -0.5f), new Vector3(2.1f, 1.6f, -0.5f)
        };
        for (int i = 0; i < win.Length; i++)
        {
            Vector3 p = baseP + rot * (win[i] * s);
            Gfx.Glow(transform, p, 2.4f * s, new Color(warm.r, warm.g, warm.b, 0.45f));
        }

        // Фонарь над крыльцом и общий тёплый свет от дома
        Vector3 porch = baseP + rot * (new Vector3(0f, 2.3f, 2.4f) * s);
        Gfx.Glow(transform, porch, 1.6f * s, new Color(1f, 0.85f, 0.5f, 0.7f));
        Gfx.PointLight(transform, porch, warm, 13f * s, 1.3f);
        Gfx.PointLight(transform, baseP + new Vector3(0f, 2.6f * s, 0f), warm, 9f * s, 0.5f);

        // Дым из трубы
        Vector3 chim = baseP + rot * (new Vector3(1.5f, 5.1f, -1.1f) * s);
        ParticleFx smoke = ParticleFx.Spawn(transform, chim, 14,
            new Color(0.85f, 0.85f, 0.88f, 0.32f));
        smoke.EmitExtents = new Vector3(0.16f, 0.1f, 0.16f);
        smoke.BaseVelocity = new Vector3(0.25f, 1.3f, 0.1f);
        smoke.Gravity = new Vector3(0.05f, 0.16f, 0f);
        smoke.SpeedMin = 0.2f;
        smoke.SpeedMax = 0.7f;
    }
}
