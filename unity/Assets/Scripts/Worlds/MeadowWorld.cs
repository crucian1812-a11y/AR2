using UnityEngine;

// Солнечные луга — просторный холмистый мир: водопад, древнее дерево
// с площадками на ветвях, цепочки движущихся платформ и батуты.
public class MeadowWorld : WorldBuilder
{
    private static readonly Color GrassLow = new Color(0.3f, 0.56f, 0.2f);
    private static readonly Color GrassHigh = new Color(0.55f, 0.82f, 0.34f);
    private static readonly Color Plat = new Color(0.5f, 0.76f, 0.34f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 2f, 46f);

        SetupSky(new Color(0.32f, 0.62f, 0.99f), new Color(0.9f, 0.94f, 0.82f),
            new Color(0.3f, 0.42f, 0.24f), new Vector3(52f, 22f, 0f), 1.25f, 0.0012f,
            new Color(0.86f, 0.92f, 0.86f));
        SetPostFx(1.05f, 1.16f, new Color(1.03f, 1.01f, 0.95f), 0.45f);

        // Ровные места под пруд и чашу водопада
        FlattenArea(-26f, 26f, 13f, 10f);
        FlattenArea(-46f, -20f, 11f, 9f);

        // Настоящие PBR-текстуры поверхности
        TerrainTextures("Grass006_1K-PNG_Color", "Grass006_1K-PNG_NormalGL",
            "marble_rock_03_diff_1k", "marble_rock_03_nor_gl_1k", 0.1f, 0.14f);

        Terrain(Vector3.zero, new Vector2(250f, 250f), 150, 13f, 0.012f,
            GrassLow, GrassHigh, 22f, 7717);

        Material rim = Gfx.MatFull(new Color(0.42f, 0.3f, 0.18f), 0.03f, 0f, Color.black, 5f, 0.7f);
        Gfx.Cone(transform, new Vector3(0f, -34f, 0f), 76f, 26f, rim);

        AddPortal(new Vector3(0f, 1.6f, 56f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        BuildWaterfall();
        BuildAncientTree();
        BuildPlatformRoute();
        BuildPond();
        // Не на камне с батутом (-30, 3.4, -14): точка возрождения попадала
        // в окно батута, и каждая смерть подбрасывала на двенадцать метров
        // обратно на тот же батут.
        AddCheckpoint(OnGround(-24f, -14f, 0.4f));
        AddCheckpoint(new Vector3(-34f, 15.2f, -31f));
        // Та же опорная высота, что у лестницы в BuildPlatformRoute,
        // иначе контрольная точка повисает над площадкой или тонет в ней.
        AddCheckpoint(new Vector3(28f, GroundHeight(6f, 24f) + 12f, 14f));
        BuildNature();
        // Звёзды — цель мира, каждая на своём постаменте с батутом
        AddStarPedestal(OnGround(-14f, -46f), 14f);
        AddStarPedestal(OnGround(56f, 20f), 11f);
        AddStarPedestal(OnGround(-58f, 48f), 9f);
        // Полоса препятствий над низиной и вращающиеся брёвна
        ObstacleRun(OnGround(-8f, -56f, 2.1f), new Vector3(1f, 0f, 0.3f), 7, 1.6f,
            new Color(0.5f, 0.76f, 0.34f));
        Spinner(OnGround(2f, -38f, 4f), 9f, 55f, new Color(0.45f, 0.32f, 0.2f));
        Spinner(OnGround(40f, 34f, 4f), 8f, -65f, new Color(0.45f, 0.32f, 0.2f));
        // Босс лугов — вожак альпак у древнего дерева
        // 20.4 — верх площадки на дереве; на 21 вожак висел над ней в воздухе.
        AddBoss(OnGround(38f, -30f) + new Vector3(-9f, 20.4f, 0f),
            OnGround(38f, -30f) + new Vector3(9f, 20.4f, 0f), "alpaking", 3.2f, 4, 1.5f);
        BuildAtmosphere();

        // Каменные руины и арки: мир должен выглядеть построенным задолго
        // до игрока, а не собранным из кубиков к его приходу.
        for (int i = 0; i < 4; i++)
        {
            float a = 0.6f + i * 92f * Mathf.Deg2Rad;
            float r = 38f + (i % 2) * 9f;
            StoneProp("ruin_wall", OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.1f),
                3.8f, -a * Mathf.Rad2Deg + 90f, Plat);
        }
        for (int i = 0; i < 3; i++)
        {
            float a = 1.9f + i * 118f * Mathf.Deg2Rad;
            float r = 30f;
            StoneProp("stone_column", OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.1f),
                5.8f, i * 47f, Plat);
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
        Weather.Attach(transform, Weather.Kind.Rain, 7717);
        Color mStone = new Color(0.58f, 0.56f, 0.52f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 51f * Mathf.Deg2Rad;
            OreRock(OnGround(Mathf.Cos(a) * (44f + i * 3f), Mathf.Sin(a) * (44f + i * 3f), 0.3f),
                2.2f, mStone);
        }
        Color mLeaf = new Color(0.26f, 0.56f, 0.26f);
        for (int i = 0; i < 8; i++)
        {
            float a = 0.7f + i * 44f * Mathf.Deg2Rad;
            OreTree(OnGround(Mathf.Cos(a) * (52f + (i % 3) * 5f), Mathf.Sin(a) * (52f + (i % 3) * 5f)),
                mLeaf, 1f);
        }
        AddChest(OnGround(-62f, -12f, 0.1f), 40f, 14, Res.Wood, 6);

        AddVillager(OnGround(-6f, 40f), "Пасечница", "Bee", 1.2f,
            "Древнее дерево перестало цвести в ту же ночь.\nОно чувствует Сердце горы лучше любого из нас.");
        AddVillager(OnGround(30f, -18f), "Лесник", "Deer", 2f,
            "Тропа наверх идёт по ветвям. Дерево само её вырастило —\nзначит, кого-то ждёт.");
    }

    private void BuildWaterfall()
    {
        // Уступ, с которого падает вода
        Material cliff = Gfx.MatFull(new Color(0.5f, 0.46f, 0.38f), 0.03f, 0f, Color.black, 4f, 0.75f);
        Gfx.Box(transform, new Vector3(-46f, 9f, -44f), new Vector3(38f, 26f, 26f), cliff);
        Gfx.Box(transform, new Vector3(-46f, 22.4f, -30f), new Vector3(30f, 1.2f, 8f), cliff);

        Waterfall(new Vector3(-46f, 22f, -27f), 7.5f, 21f, 110);

        // Чаша внизу
        Gfx.Box(transform, new Vector3(-46f, -0.8f, -20f), new Vector3(22f, 2f, 18f),
            Gfx.MatFull(new Color(0.45f, 0.42f, 0.32f), 0.03f, 0f, Color.black, 4f, 0.5f));
        Water(new Vector3(-46f, 0.4f, -20f), new Vector2(21f, 17f));

        AddCoin(new Vector3(-46f, 1.6f, -20f));
        AddCoin(new Vector3(-41f, 1.6f, -16f));
        AddCoin(new Vector3(-51f, 1.6f, -16f));

        // Подъём на уступ по камням и батуту
        Rock(new Vector3(-30f, 1.5f, -14f), 4f, new Color(0.55f, 0.53f, 0.48f));
        Rock(new Vector3(-26f, 3f, -22f), 4.6f, new Color(0.55f, 0.53f, 0.48f));
        AddBouncePad(new Vector3(-30f, 3.4f, -14f), 23f);
        AddCoin(new Vector3(-30f, 9f, -18f));

        // Шаг между уступами — 2.5 м, вписывается в высоту прыжка
        Platform(new Vector3(-31f, 12f, -26f), new Vector3(6f, 0.7f, 6f), Plat);
        Platform(new Vector3(-34f, 14.5f, -31f), new Vector3(5f, 0.7f, 5f), Plat);
        Platform(new Vector3(-38f, 17f, -30f), new Vector3(5f, 0.7f, 5f), Plat);
        Platform(new Vector3(-42f, 19.5f, -32f), new Vector3(5f, 0.7f, 5f), Plat);
        AddCoin(new Vector3(-31f, 13.4f, -26f));
        AddCoin(new Vector3(-34f, 15.9f, -31f));
        AddCoin(new Vector3(-38f, 18.4f, -30f));
        AddCoin(new Vector3(-42f, 20.9f, -32f));
        AddCoin(new Vector3(-46f, 24.2f, -34f));
        AddCoin(new Vector3(-52f, 24.2f, -38f));
        AddEnemy(new Vector3(-40f, 23.4f, -38f), new Vector3(-54f, 23.4f, -38f), "mushroom", 2.6f);
    }

    private void BuildAncientTree()
    {
        Vector3 root = OnGround(38f, -30f);
        Material bark = Gfx.MatFull(new Color(0.4f, 0.27f, 0.16f), 0.03f, 0f, Color.black, 3f, 0.65f);

        Gfx.Cyl(transform, root + new Vector3(0f, 9f, 0f), new Vector3(6.5f, 9f, 6.5f), bark);
        Gfx.Cyl(transform, root + new Vector3(0f, 17f, 0f), new Vector3(4.6f, 4f, 4.6f), bark);

        // Корни-контрфорсы
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            GameObject r = Gfx.Cyl(transform,
                root + new Vector3(Mathf.Cos(a) * 3.4f, 1.1f, Mathf.Sin(a) * 3.4f),
                new Vector3(1.5f, 2.2f, 1.5f), bark, false);
            r.transform.localRotation = Quaternion.Euler(24f, -a * Mathf.Rad2Deg, 0f);
        }

        Color leaf = new Color(0.24f, 0.55f, 0.24f);
        Material fol = Gfx.FoliageMat(leaf, leaf * 1.5f);
        Gfx.Blob(transform, root + new Vector3(0f, 23f, 0f), new Vector3(20f, 13f, 20f), fol, 5, 0.35f, false);
        Gfx.Blob(transform, root + new Vector3(7f, 20f, 4f), new Vector3(11f, 8f, 11f), fol, 9, 0.45f, false);
        Gfx.Blob(transform, root + new Vector3(-8f, 19.5f, -4f), new Vector3(12f, 8f, 12f), fol, 13, 0.45f, false);

        // Площадки на ветвях — подъём по спирали вокруг ствола
        Material branch = Gfx.MatFull(new Color(0.45f, 0.3f, 0.18f), 0.03f, 0f, Color.black, 2f, 0.5f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 52f * Mathf.Deg2Rad;
            float r = 7.5f;
            Vector3 p = root + new Vector3(Mathf.Cos(a) * r, 3.5f + i * 2.3f, Mathf.Sin(a) * r);
            GameObject pl = Gfx.Box(transform, p, new Vector3(4.4f, 0.6f, 3f), branch);
            pl.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            if (i % 2 == 0) AddCoin(p + new Vector3(0f, 1.3f, 0f));
        }

        // Наверху — награда и страж
        Platform(root + new Vector3(0f, 20f, 0f), new Vector3(9f, 0.8f, 9f), new Color(0.46f, 0.7f, 0.32f));
        AddCoin(root + new Vector3(2.5f, 21.6f, 2.5f));
        AddCoin(root + new Vector3(-2.5f, 21.6f, 2.5f));
        AddCoin(root + new Vector3(0f, 21.6f, -3f));
        AddEnemy(root + new Vector3(-3.5f, 20.9f, 0f), root + new Vector3(3.5f, 20.9f, 0f), "mushroom", 2.2f);
        Gfx.PointLight(transform, root + new Vector3(0f, 22f, 0f), new Color(0.9f, 1f, 0.7f), 22f, 0.9f);
    }

    private void BuildPlatformRoute()
    {
        // Лестница от земли: шаг ровно 2 метра — в пределах прыжка.
        //
        // Высоту берём одним замером у подножия и дальше отсчитываем от неё.
        // Раньше каждая ступень вставала «на землю в своей точке», а земля
        // здесь уже вне ровной площадки и гуляет метра на два: реальный шаг
        // получался от почти нулевого до четырёх метров, и середина лестницы
        // становилась непроходимой.
        float baseY = GroundHeight(6f, 24f);
        for (int i = 0; i < 5; i++)
        {
            Vector3 p = new Vector3(6f + i * 4.5f, baseY + 1.2f + i * 2f, 24f - i * 2f);
            Platform(p, new Vector3(5f, 0.7f, 5f), Plat);
            AddCoin(p + new Vector3(0f, 1.5f, 0f));
        }

        // Верх лестницы — площадка, с которой начинается цепочка платформ
        Vector3 hub = new Vector3(28f, baseY + 11.2f, 14f);
        Platform(hub, new Vector3(7f, 0.8f, 7f), Plat);
        AddCoin(hub + new Vector3(0f, 1.6f, 0f));

        AddMovingPlatform(hub + new Vector3(0f, 0f, -6f), hub + new Vector3(0f, 0f, -18f),
            new Vector3(4.4f, 0.6f, 4.4f), Plat, 5f, 0f);
        AddMovingPlatform(hub + new Vector3(-6f, 2.5f, -22f), hub + new Vector3(-18f, 2.5f, -22f),
            new Vector3(4.4f, 0.6f, 4.4f), Plat, 6f, 0.3f);
        AddMovingPlatform(hub + new Vector3(-24f, 5f, -16f), hub + new Vector3(-24f, 5f, -4f),
            new Vector3(4.4f, 0.6f, 4.4f), Plat, 5.5f, 0.6f);

        AddCoin(hub + new Vector3(0f, 1.6f, -12f));
        AddCoin(hub + new Vector3(-12f, 4.1f, -22f));
        AddCoin(hub + new Vector3(-24f, 6.6f, -10f));

        Vector3 top = hub + new Vector3(-26f, 7f, 2f);
        Platform(top, new Vector3(8f, 0.8f, 8f), new Color(0.46f, 0.7f, 0.32f));
        AddCoin(top + new Vector3(0f, 1.6f, 0f));
        AddBouncePad(top + new Vector3(0f, 0.5f, 0f), 20f);
        AddCoin(top + new Vector3(0f, 6f, -3f));

        // Батут у спавна — быстрый способ осмотреться сверху
        AddBouncePad(OnGround(0f, 30f, 0.3f), 18f);
        AddCoin(OnGround(0f, 30f, 6f));
        AddCoin(OnGround(0f, 30f, 9f));
    }

    private void BuildPond()
    {
        Gfx.Box(transform, new Vector3(-26f, -0.7f, 26f), new Vector3(26f, 2f, 22f),
            Gfx.MatFull(new Color(0.46f, 0.42f, 0.3f), 0.03f, 0f, Color.black, 4f, 0.5f));
        Water(new Vector3(-26f, 0.3f, 26f), new Vector2(25f, 21f));

        Color stone = new Color(0.62f, 0.6f, 0.55f);
        Vector3[] steps = {
            new Vector3(-18f, 0.6f, 20f), new Vector3(-22f, 0.6f, 25f),
            new Vector3(-27f, 0.6f, 29f), new Vector3(-32f, 0.6f, 32f)
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Platform(steps[i], new Vector3(2.6f, 0.9f, 2.6f), stone);
            AddCoin(steps[i] + new Vector3(0f, 1.4f, 0f));
        }
        Rock(new Vector3(-36f, 0.8f, 22f), 3.4f, stone);
        AddEnemy(new Vector3(-16f, 0.2f, 34f), new Vector3(-34f, 0.2f, 38f), "mushroom", 2.4f);
    }

    private void BuildNature()
    {
        for (int i = 0; i < 42; i++)
        {
            float ang = i * 137.5f * Mathf.Deg2Rad;
            float r = 20f + (i % 9) * 6.5f;
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            if (x < -20f && z < -6f) continue;              // зона водопада
            if (Mathf.Abs(x - 38f) < 14f && Mathf.Abs(z + 30f) < 14f) continue; // древнее дерево
            if (Mathf.Abs(x + 26f) < 15f && Mathf.Abs(z - 26f) < 13f) continue; // пруд

            Color leaf = Color.HSVToRGB(0.25f + (i % 6) * 0.017f, 0.6f, 0.46f + (i % 4) * 0.06f);
            Tree(OnGround(x, z), leaf, 0.85f + (i % 5) * 0.16f);
        }

        for (int i = 0; i < 26; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(14f, 60f);
            Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            Bush(OnGround(p.x, p.z), new Color(0.2f + (i % 3) * 0.03f, 0.45f, 0.2f));
        }

        Color[] flowers = {
            new Color(0.95f, 0.4f, 0.45f), new Color(0.98f, 0.88f, 0.32f),
            new Color(0.62f, 0.5f, 0.96f), new Color(1f, 0.66f, 0.3f),
            new Color(1f, 0.95f, 0.95f)
        };
        for (int i = 0; i < 70; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(8f, 66f);
            Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            Flower(OnGround(p.x, p.z), flowers[i % 5]);
        }

        AddEnemy(OnGround(-8f, 10f), OnGround(8f, 10f), "mushroom", 2.3f);
        AddEnemy(OnGround(6f, -12f), OnGround(-6f, -18f), "mushroom", 2.6f);
        AddEnemy(OnGround(20f, 32f), OnGround(34f, 30f), "mushroom", 2.1f);

        for (int i = 0; i < 10; i++)
        {
            float ang = i * 36f * Mathf.Deg2Rad;
            float r = 12f + (i % 3) * 8f;
            AddCoin(OnGround(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 1.3f));
        }
    }

    private void BuildAtmosphere()
    {
        GrassField(new Vector3(0f, 0f, 10f), new Vector2(52f, 46f), 12160,
            new Color(0.2f, 0.46f, 0.11f), new Color(0.66f, 0.92f, 0.32f), true);
        GrassField(new Vector3(30f, 0f, 26f), new Vector2(26f, 24f), 4180,
            new Color(0.22f, 0.5f, 0.12f), new Color(0.7f, 0.95f, 0.34f), true);

        Cloud(new Vector3(-44f, 32f, -28f), 2.6f, Color.white);
        Cloud(new Vector3(24f, 38f, -50f), 3.2f, Color.white);
        Cloud(new Vector3(52f, 30f, 24f), 2.2f, Color.white);
        Cloud(new Vector3(-24f, 34f, 42f), 2.5f, Color.white);
        Cloud(new Vector3(4f, 41f, 0f), 2.8f, Color.white);
        Cloud(new Vector3(70f, 35f, -20f), 2.4f, Color.white);

        Color mnt = new Color(0.42f, 0.55f, 0.62f);
        Mountain(new Vector3(-130f, -8f, -92f), 46f, 58f, mnt, true);
        Mountain(new Vector3(-10f, -8f, -146f), 54f, 70f, mnt * 0.94f, true);
        Mountain(new Vector3(118f, -8f, -104f), 40f, 52f, mnt * 1.04f, true);
        Mountain(new Vector3(146f, -8f, 34f), 44f, 56f, mnt, true);
        Mountain(new Vector3(-138f, -8f, 62f), 42f, 50f, mnt * 0.96f, false);
        Mountain(new Vector3(60f, -8f, 132f), 38f, 46f, mnt * 0.92f, false);

        Motes(new Vector3(0f, 4f, 0f), new Vector3(40f, 4f, 40f), 90,
            new Color(1f, 0.98f, 0.7f, 0.65f), 0.14f);
        Leaves(new Vector3(38f, 26f, -30f), new Vector3(10f, 2f, 10f), 40, new Color(0.55f, 0.8f, 0.3f));
        Leaves(new Vector3(-20f, 12f, 30f), new Vector3(7f, 2f, 7f), 20, new Color(0.9f, 0.65f, 0.3f));
    }
}
