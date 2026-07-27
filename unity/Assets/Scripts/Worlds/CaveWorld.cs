using UnityEngine;

// Кристальная пещера — финальный мир. Тёмный подземный зал, освещённый
// только кристаллами и грибами, подземное озеро, мосты через пропасть
// и алтарь с Сердцем горы на самом верху.
public class CaveWorld : WorldBuilder
{
    private static readonly Color RockCol = new Color(0.24f, 0.21f, 0.3f);
    private static readonly Color RockLight = new Color(0.34f, 0.3f, 0.42f);
    private static readonly Color Violet = new Color(0.62f, 0.4f, 1f);
    private static readonly Color Cyan = new Color(0.35f, 0.85f, 1f);
    private static readonly Color Rose = new Color(1f, 0.45f, 0.7f);

    private const float Radius = 104f;

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 2f, 52f);

        // «Небо» пещеры — почти чёрное, весь свет идёт от кристаллов.
        // Ambient считается от цветов неба, поэтому для пещеры он приподнят —
        // иначе вне пятен от кристаллов совсем ничего не видно.
        SetupSky(new Color(0.11f, 0.09f, 0.2f), new Color(0.2f, 0.14f, 0.34f),
            new Color(0.09f, 0.07f, 0.15f), new Vector3(70f, 20f, 0f), 0.25f,
            0.012f, new Color(0.1f, 0.07f, 0.18f));
        SetPostFx(1.9f, 1.25f, new Color(0.94f, 0.92f, 1.12f), 0.85f);

        // Ровные места: подземное озеро, площадка входа и края пропасти
        FlattenArea(-34f, -16f, 17f, 11f);
        FlattenArea(0f, 52f, 11f, 9f);
        FlattenArea(34f, -19f, 13f, 10f);

        Terrain(Vector3.zero, new Vector2(230f, 230f), 145, 7f, 0.013f,
            RockCol, RockLight, 18f, 9091);

        BuildShell();
        BuildPortal();
        BuildCrystalGarden();
        BuildLake();
        BuildChasm();
        BuildAltar();
        BuildFlora();
        BuildAtmosphere();
    }

    // ---------- Оболочка зала: стены, свод, сталактиты ----------

    private void BuildShell()
    {
        Material wall = Gfx.MatFull(RockCol, 0.04f, 0f, Color.black, 5f, 0.85f);
        Material roof = Gfx.MatFull(RockCol * 0.7f, 0.03f, 0f, Color.black, 4f, 0.8f);

        // Кольцо стен из наклонных плит — «неровная» порода без дыр
        const int segs = 34;
        for (int i = 0; i < segs; i++)
        {
            float a = (float)i / segs * Mathf.PI * 2f;
            float r = Radius + Mathf.PerlinNoise(i * 0.6f, 0f) * 6f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 12f, Mathf.Sin(a) * r);
            GameObject slab = Gfx.Box(transform, p, new Vector3(24f, 40f, 16f), wall);
            slab.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        }

        // Свод: купол из колец, опускающихся к краям
        for (int ring = 0; ring < 4; ring++)
        {
            float rr = Radius * (1f - ring * 0.24f);
            float h = 26f + ring * 4f;
            int count = Mathf.Max(6, 20 - ring * 4);
            for (int i = 0; i < count; i++)
            {
                float a = (float)i / count * Mathf.PI * 2f;
                GameObject plate = Gfx.Box(transform,
                    new Vector3(Mathf.Cos(a) * rr, h, Mathf.Sin(a) * rr),
                    new Vector3(rr * 0.7f, 3f, rr * 0.7f), roof, false);
                plate.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                Gfx.NoShadow(plate);
            }
        }
        GameObject cap = Gfx.Box(transform, new Vector3(0f, 40f, 0f), new Vector3(40f, 4f, 40f), roof, false);
        Gfx.NoShadow(cap);

        // Сталактиты и сталагмиты
        Material stone = Gfx.MatFull(RockLight, 0.05f, 0f, Color.black, 3f, 0.7f);
        for (int i = 0; i < 40; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(12f, Radius - 10f);
            Vector3 p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float len = Random.Range(3f, 9f);
            GameObject tite = Gfx.Cone(transform, new Vector3(p.x, 25f, p.z), Random.Range(0.8f, 2.2f), len, stone);
            tite.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            Gfx.NoShadow(tite);
            if (i % 2 == 0)
            {
                Gfx.Cone(transform, OnGround(p.x, p.z), Random.Range(0.8f, 2f), Random.Range(2f, 6f), stone, true);
            }
        }
    }

    private void BuildPortal()
    {
        // Вход в пещеру — освещённая площадка у портала
        Material floor = Gfx.MatFull(RockLight, 0.06f, 0f, Color.black, 3f, 0.6f);
        Gfx.Cyl(transform, new Vector3(0f, 0.1f, 52f), new Vector3(20f, 0.2f, 20f), floor);
        AddPortal(new Vector3(0f, 1.8f, 60f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);
        Gfx.PointLight(transform, new Vector3(0f, 5f, 54f), new Color(0.8f, 0.7f, 1f), 30f, 1.4f);
        CrystalCluster(new Vector3(-8f, 0.4f, 50f), 1.4f, Cyan, true);
        CrystalCluster(new Vector3(8f, 0.4f, 50f), 1.4f, Violet, true);
    }

    // ---------- Кристальный сад ----------

    private void BuildCrystalGarden()
    {
        Color[] palette = { Violet, Cyan, Rose, new Color(0.5f, 1f, 0.7f) };

        // Крупные светящиеся друзы — основной источник света в зале
        for (int i = 0; i < 22; i++)
        {
            float a = i * 137.5f * Mathf.Deg2Rad;
            float r = 16f + (i % 7) * 7.5f;
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;
            if (Mathf.Abs(x + 34f) < 20f && Mathf.Abs(z + 16f) < 18f) continue; // озеро
            if (Mathf.Abs(z - 52f) < 14f && Mathf.Abs(x) < 14f) continue;       // вход
            CrystalCluster(OnGround(x, z), 1.2f + (i % 4) * 0.45f,
                palette[i % 4], i % 2 == 0);
        }

        // Мелкая кристальная россыпь без источников света — только свечение
        for (int i = 0; i < 40; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(10f, Radius - 14f);
            Vector3 p = OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Color col = palette[i % 4];
            Material m = Gfx.MatFull(col, 0.9f, 0.25f, col * 0.8f, 0f, 0f);
            Gfx.Crystal(transform, p + new Vector3(0f, 0.4f, 0f),
                Random.Range(0.15f, 0.35f), Random.Range(0.8f, 1.8f), m, Random.Range(-25f, 25f));
            Gfx.Glow(transform, p + new Vector3(0f, 0.7f, 0f), 1.8f,
                new Color(col.r, col.g, col.b, 0.35f));
        }

        // Парящие кристаллы над садом
        Material floatMat = Gfx.MatFull(Violet, 0.95f, 0.3f, Violet * 0.9f, 0f, 0f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Cos(a) * 22f, 9f + (i % 3) * 2.5f, Mathf.Sin(a) * 22f);
            GameObject cr = Gfx.Crystal(transform, p, 0.7f, 2.6f, floatMat, 0f);
            Gfx.Glow(transform, p, 5f, new Color(Violet.r, Violet.g, Violet.b, 0.4f));
            Spinner sp = cr.AddComponent<Spinner>();
            sp.Axis = Vector3.up;
            sp.Speed = 26f;
            sp.BobAmplitude = 0.8f;
            sp.BobSpeed = 0.8f + i * 0.1f;
        }

        // Летучие мыши в саду
        AddEnemy(new Vector3(-14f, 1.2f, 16f), new Vector3(14f, 1.2f, 16f), "bat", 3.4f);
        AddEnemy(new Vector3(18f, 1.2f, -8f), new Vector3(-4f, 1.2f, -18f), "bat", 3f);
        AddEnemy(new Vector3(24f, 1.2f, 30f), new Vector3(40f, 1.2f, 22f), "bat", 3.6f);

        for (int i = 0; i < 14; i++)
        {
            float a = i * 25.7f * Mathf.Deg2Rad;
            float r = 12f + (i % 4) * 8f;
            AddCoin(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 1.3f));
        }
    }

    // ---------- Подземное озеро ----------

    private void BuildLake()
    {
        Vector3 c = new Vector3(-34f, 0f, -16f);
        Gfx.Box(transform, c + new Vector3(0f, -1.6f, 0f), new Vector3(34f, 3f, 30f),
            Gfx.MatFull(RockCol * 0.8f, 0.03f, 0f, Color.black, 4f, 0.6f));
        Water(c + new Vector3(0f, -0.1f, 0f), new Vector2(33f, 29f));
        Gfx.PointLight(transform, c + new Vector3(0f, 4f, 0f), Cyan, 34f, 1.1f);

        // Островки-ступени через озеро
        Color st = new Color(0.3f, 0.28f, 0.38f);
        Vector3[] steps = {
            c + new Vector3(13f, 0.3f, 11f), c + new Vector3(7f, 0.55f, 5f),
            c + new Vector3(0f, 0.8f, 0f), c + new Vector3(-8f, 0.55f, -5f),
            c + new Vector3(-14f, 0.3f, -11f)
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Platform(steps[i], new Vector3(4.2f, 1.2f, 4.2f), st, 0.15f);
            AddCoin(steps[i] + new Vector3(0f, 1.8f, 0f));
            if (i % 2 == 0) CrystalCluster(steps[i] + new Vector3(1.4f, 0.6f, 1.2f), 0.7f, Cyan, false);
        }

        // Водопад из трещины в скальном выступе над озером
        Material face = Gfx.MatFull(RockLight, 0.04f, 0f, Color.black, 4f, 0.8f);
        Gfx.Box(transform, c + new Vector3(-18f, 9f, 0f), new Vector3(9f, 30f, 20f), face);
        Gfx.Box(transform, c + new Vector3(-14.5f, 16.6f, 0f), new Vector3(3f, 1.2f, 7f), face, false);
        Waterfall(c + new Vector3(-15f, 16f, 0f), 4.5f, 16f, 70);
        Gfx.PointLight(transform, c + new Vector3(-15f, 8f, 0f), Cyan, 20f, 1.3f);

        AddEnemy(c + new Vector3(-12f, 4f, 12f), c + new Vector3(12f, 4f, -12f), "bat", 3.2f);
    }

    // ---------- Пропасть и мосты ----------

    private void BuildChasm()
    {
        // Провал в полу с мостом и цепочкой платформ над ним
        Material edge = Gfx.MatFull(RockLight, 0.05f, 0f, Color.black, 4f, 0.75f);
        Gfx.Box(transform, new Vector3(34f, 4f, -4f), new Vector3(16f, 8f, 12f), edge);
        Gfx.Box(transform, new Vector3(34f, 4f, -34f), new Vector3(16f, 8f, 12f), edge);

        Bridge(new Vector3(34f, 8.6f, -10f), new Vector3(34f, 8.6f, -28f), 4.4f,
            new Color(0.4f, 0.3f, 0.34f));
        AddCoin(new Vector3(34f, 9.6f, -19f));

        // Свечение из глубины провала
        Gfx.Glow(transform, new Vector3(34f, -2f, -19f), 26f, new Color(1f, 0.45f, 0.25f, 0.5f));
        Gfx.PointLight(transform, new Vector3(34f, 1f, -19f), new Color(1f, 0.5f, 0.25f), 32f, 1.6f);

        // Подъём к алтарю: движущиеся плиты и батуты над пропастью
        AddMovingPlatform(new Vector3(34f, 11f, -34f), new Vector3(20f, 11f, -40f),
            new Vector3(4.4f, 0.6f, 4.4f), new Color(0.42f, 0.34f, 0.5f), 6f, 0f);
        AddMovingPlatform(new Vector3(10f, 13.5f, -40f), new Vector3(-4f, 13.5f, -36f),
            new Vector3(4.4f, 0.6f, 4.4f), new Color(0.42f, 0.34f, 0.5f), 6.5f, 0.35f);
        AddMovingPlatform(new Vector3(-14f, 16f, -32f), new Vector3(-14f, 16f, -18f),
            new Vector3(4.4f, 0.6f, 4.4f), new Color(0.42f, 0.34f, 0.5f), 5.5f, 0.7f);

        AddCoin(new Vector3(27f, 13f, -37f));
        AddCoin(new Vector3(3f, 15.5f, -38f));
        AddCoin(new Vector3(-14f, 18f, -25f));

        AddBouncePad(new Vector3(34f, 8.4f, -4f), 20f);
        AddCoin(new Vector3(34f, 13f, -2f));

        AddEnemy(new Vector3(34f, 10f, -12f), new Vector3(34f, 10f, -26f), "bat", 3.8f);
    }

    // ---------- Алтарь с Сердцем горы ----------

    private void BuildAltar()
    {
        Vector3 c = new Vector3(-14f, 17f, -12f);
        Material dais = Gfx.MatFull(new Color(0.34f, 0.28f, 0.44f), 0.2f, 0.1f,
            new Color(0.1f, 0.06f, 0.18f), 2f, 0.4f);

        Gfx.Cyl(transform, c + new Vector3(0f, 0f, 0f), new Vector3(16f, 0.8f, 16f), dais);
        Gfx.Cyl(transform, c + new Vector3(0f, 1.2f, 0f), new Vector3(11f, 0.6f, 11f), dais);

        // Кольцо кристаллов-светильников вокруг постамента
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 6.5f, 1.8f, Mathf.Sin(a) * 6.5f);
            Color col = i % 2 == 0 ? Violet : Rose;
            Material m = Gfx.MatFull(col, 0.95f, 0.3f, col * 0.9f, 0f, 0f);
            Gfx.Crystal(transform, p + new Vector3(0f, 1.6f, 0f), 0.4f, 3.2f, m, 0f);
            Gfx.Glow(transform, p + new Vector3(0f, 1.6f, 0f), 4f, new Color(col.r, col.g, col.b, 0.5f));
            if (i % 2 == 0) Gfx.PointLight(transform, p + new Vector3(0f, 2.4f, 0f), col, 18f, 1.4f);
        }

        // Резной постамент под звезду
        Material pillar = Gfx.MatFull(new Color(0.5f, 0.42f, 0.6f), 0.35f, 0.2f,
            new Color(0.16f, 0.1f, 0.26f), 0f, 0f);
        Gfx.Cyl(transform, c + new Vector3(0f, 2.6f, 0f), new Vector3(3f, 1.2f, 3f), pillar);
        Gfx.Box(transform, c + new Vector3(0f, 4f, 0f), new Vector3(2.6f, 0.4f, 2.6f), pillar);

        AddStar(c + new Vector3(0f, 6.4f, 0f));
        Gfx.PointLight(transform, c + new Vector3(0f, 6.4f, 0f), new Color(1f, 0.88f, 0.5f), 40f, 2.2f);

        // Финальные монеты вокруг алтаря
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            AddCoin(c + new Vector3(Mathf.Cos(a) * 4f, 2.8f, Mathf.Sin(a) * 4f));
        }

        AddEnemy(c + new Vector3(-5f, 2.4f, 5f), c + new Vector3(5f, 2.4f, -5f), "bat", 4f);
        AddEnemy(c + new Vector3(5f, 2.4f, 5f), c + new Vector3(-5f, 2.4f, -5f), "bat", 3.6f);
    }

    // ---------- Подземная флора ----------

    private void BuildFlora()
    {
        // Гигантские светящиеся грибы — мягкий заполняющий свет
        for (int i = 0; i < 16; i++)
        {
            float a = i * 137.5f * Mathf.Deg2Rad;
            float r = 20f + (i % 6) * 7f;
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;
            if (Mathf.Abs(x + 34f) < 20f && Mathf.Abs(z + 16f) < 18f) continue;
            GlowMushroom(OnGround(x, z), 0.8f + (i % 4) * 0.4f,
                i % 3 == 0 ? Cyan : (i % 3 == 1 ? Rose : new Color(0.6f, 1f, 0.6f)), i % 4 == 0);
        }

        // Мох на полу — трава с холодным свечением
        GrassField(new Vector3(0f, 0f, 20f), new Vector2(34f, 26f), 5700,
            new Color(0.1f, 0.24f, 0.22f), new Color(0.3f, 0.85f, 0.75f), true);
        GrassField(new Vector3(-30f, 0f, 20f), new Vector2(18f, 16f), 2280,
            new Color(0.12f, 0.2f, 0.3f), new Color(0.35f, 0.7f, 1f), true);

        // Валуны
        for (int i = 0; i < 18; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(14f, Radius - 16f);
            Rock(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.4f),
                Random.Range(1.4f, 4f), Color.Lerp(RockCol, RockLight, Random.value));
        }
    }

    private void GlowMushroom(Vector3 pos, float scale, Color color, bool light)
    {
        Material stem = Gfx.MatFull(new Color(0.85f, 0.86f, 0.9f), 0.2f, 0f,
            color * 0.25f, 0f, 0f);
        Material capMat = Gfx.MatFull(color, 0.55f, 0.05f, color * 0.85f, 0f, 0f);

        Gfx.Cyl(transform, pos + new Vector3(0f, 1.6f * scale, 0f),
            new Vector3(0.55f * scale, 1.6f * scale, 0.55f * scale), stem);
        Gfx.Blob(transform, pos + new Vector3(0f, 3.4f * scale, 0f),
            new Vector3(3.6f, 1.8f, 3.6f) * scale, capMat,
            Mathf.Abs(Mathf.RoundToInt(pos.x * 5.7f + pos.z * 2.3f)), 0.3f, false);
        Gfx.Glow(transform, pos + new Vector3(0f, 3.2f * scale, 0f), 6f * scale,
            new Color(color.r, color.g, color.b, 0.4f));

        // Мелкие спутники у основания
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            Vector3 p = pos + new Vector3(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.6f) * scale;
            Gfx.Cyl(transform, p + new Vector3(0f, 0.5f * scale, 0f),
                new Vector3(0.2f, 0.5f, 0.2f) * scale, stem, false);
            Gfx.Ball(transform, p + new Vector3(0f, 1f * scale, 0f),
                new Vector3(1f, 0.5f, 1f) * scale, capMat);
        }

        if (light)
            Gfx.PointLight(transform, pos + new Vector3(0f, 3.6f * scale, 0f), color, 20f * scale, 1.2f);
    }

    private void BuildAtmosphere()
    {
        // Пылинки и споры в лучах кристаллов
        Motes(new Vector3(0f, 6f, 0f), new Vector3(50f, 8f, 50f), 140,
            new Color(0.75f, 0.65f, 1f, 0.55f), 0.16f);
        Motes(new Vector3(-34f, 4f, -16f), new Vector3(16f, 5f, 14f), 60,
            new Color(0.5f, 0.9f, 1f, 0.6f), 0.14f);

        // Капель со свода
        ParticleFx drip = ParticleFx.Spawn(transform, new Vector3(0f, 22f, 0f), 90,
            new Color(0.6f, 0.85f, 1f, 0.7f));
        drip.EmitExtents = new Vector3(45f, 2f, 45f);
        drip.BaseVelocity = new Vector3(0f, -4f, 0f);
        drip.Gravity = new Vector3(0f, -6f, 0f);
        drip.SpeedMin = 0.05f;
        drip.SpeedMax = 0.3f;
        drip.SizeMin = 0.05f;
        drip.SizeMax = 0.12f;
        drip.LifeMin = 2.5f;
        drip.LifeMax = 4f;
        drip.Prewarm();
    }
}
