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

        Terrain(Vector3.zero, new Vector2(180f, 180f), 130, 11f, 0.014f,
            GrassLow, GrassHigh, 18f, 7717);

        Material rim = Gfx.MatFull(new Color(0.42f, 0.3f, 0.18f), 0.03f, 0f, Color.black, 5f, 0.7f);
        Gfx.Cone(transform, new Vector3(0f, -34f, 0f), 76f, 26f, rim);

        AddPortal(new Vector3(0f, 1.6f, 56f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        BuildWaterfall();
        BuildAncientTree();
        BuildPlatformRoute();
        BuildPond();
        BuildNature();
        BuildAtmosphere();
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
        AddBouncePad(new Vector3(-30f, 3.4f, -14f), 19f);
        AddCoin(new Vector3(-30f, 9f, -18f));

        Platform(new Vector3(-31f, 12f, -26f), new Vector3(6f, 0.7f, 6f), Plat);
        Platform(new Vector3(-34f, 16f, -31f), new Vector3(5f, 0.7f, 5f), Plat);
        Platform(new Vector3(-38f, 20f, -30f), new Vector3(5f, 0.7f, 5f), Plat);
        AddCoin(new Vector3(-31f, 13.4f, -26f));
        AddCoin(new Vector3(-34f, 17.4f, -31f));
        AddCoin(new Vector3(-38f, 21.4f, -30f));
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
        // Цепочка движущихся платформ над низиной
        AddMovingPlatform(new Vector3(10f, 6f, 6f), new Vector3(10f, 6f, 20f),
            new Vector3(4f, 0.6f, 4f), Plat, 5f, 0f);
        AddMovingPlatform(new Vector3(18f, 9f, 20f), new Vector3(30f, 9f, 20f),
            new Vector3(4f, 0.6f, 4f), Plat, 6f, 0.3f);
        AddMovingPlatform(new Vector3(34f, 12f, 12f), new Vector3(34f, 12f, -2f),
            new Vector3(4f, 0.6f, 4f), Plat, 5.5f, 0.6f);

        Platform(new Vector3(10f, 4.5f, -2f), new Vector3(6f, 0.7f, 6f), Plat);
        Platform(new Vector3(42f, 14f, -6f), new Vector3(8f, 0.7f, 8f), Plat);
        AddCoin(new Vector3(10f, 8f, 13f));
        AddCoin(new Vector3(24f, 11f, 20f));
        AddCoin(new Vector3(34f, 14f, 5f));
        AddCoin(new Vector3(42f, 15.6f, -6f));

        AddBouncePad(new Vector3(10f, 5.2f, -2f), 21f);
        AddBouncePad(OnGround(0f, 22f, 0.3f), 17f);
        AddCoin(new Vector3(0f, 8f, 22f));
        AddCoin(new Vector3(0f, 11f, 22f));
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
        GrassField(new Vector3(0f, 0f, 10f), new Vector2(52f, 46f), 6400,
            new Color(0.2f, 0.46f, 0.11f), new Color(0.66f, 0.92f, 0.32f), true);
        GrassField(new Vector3(30f, 0f, 26f), new Vector2(26f, 24f), 2200,
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
