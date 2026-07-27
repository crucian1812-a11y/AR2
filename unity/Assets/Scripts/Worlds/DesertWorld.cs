using UnityEngine;

// Песчаный каньон — жаркий мир из дюн, столовых гор и древних руин.
// Путь наверх идёт по уступам каньона и цепочке движущихся плит,
// в центре — оазис с пальмами, вокруг бродят жуки-панцирники.
public class DesertWorld : WorldBuilder
{
    private static readonly Color SandLow = new Color(0.78f, 0.62f, 0.36f);
    private static readonly Color SandHigh = new Color(0.93f, 0.82f, 0.55f);
    private static readonly Color Stone = new Color(0.72f, 0.44f, 0.28f);
    private static readonly Color StoneDark = new Color(0.56f, 0.32f, 0.2f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 2f, 52f);

        SetupSky(new Color(0.36f, 0.6f, 0.92f), new Color(1f, 0.85f, 0.6f),
            new Color(0.6f, 0.42f, 0.24f), new Vector3(58f, 140f, 0f), 1.35f, 0.0022f,
            new Color(0.96f, 0.82f, 0.6f));
        SetPostFx(1.25f, 1.08f, new Color(1.08f, 1.0f, 0.88f), 0.55f);

        // Ровные места: оазис, дно ущелья и площадка руин
        FlattenArea(30f, 8f, 13f, 11f);
        FlattenArea(-12f, -40f, 28f, 14f);
        FlattenArea(6f, 30f, 13f, 10f);

        // Настоящие PBR-текстуры поверхности
        TerrainTextures("aerial_beach_01_diff_1k", "aerial_beach_01_nor_gl_1k",
            "marble_rock_03_diff_1k", "marble_rock_03_nor_gl_1k", 0.08f, 0.13f);

        Terrain(Vector3.zero, new Vector2(270f, 270f), 150, 11f, 0.0095f,
            SandLow, SandHigh, 20f, 3131);

        // Основание под всем каньоном — мир «висит» над пустотой
        Gfx.Cone(transform, new Vector3(0f, -36f, 0f), 86f, 28f,
            Gfx.MatFull(StoneDark, 0.03f, 0f, Color.black, 5f, 0.7f));

        AddPortal(new Vector3(0f, 1.8f, 62f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        BuildMesas();
        BuildCanyonClimb();
        BuildOasis();
        BuildRuins();
        AddCheckpoint(new Vector3(-12f, 3.1f, -14f));
        AddCheckpoint(new Vector3(-16f, 10.9f, -38f));
        // Босс каньона стережёт верхнюю площадку
        AddBoss(new Vector3(-52f, 25.5f, -24f), new Vector3(-52f, 25.5f, -12f),
            "dragon", 4f, 4, 1.6f);
        BuildDunes();
        // Звёзды — цель мира
        AddStarPickup(new Vector3(-52f, 24.6f, -18f));
        AddStarPickup(OnGround(46f, -34f, 29f));
        AddStarPickup(new Vector3(30f, 3.6f, 6f));
        BuildAtmosphere();

        AddVillager(OnGround(24f, 14f), "Хранитель оазиса", "Cactus", 1.8f,
            "Руины старше деревни. На алтаре когда-то лежало\nвторое сердце — но его унесли ещё до меня.");
        AddVillager(OnGround(-6f, 44f), "Погонщик", "Alien_Tall", 2.2f,
            "В каньоне ветер поёт по ночам. Не ходи туда без дела —\nа с делом иди смело.");
    }

    // ---------- Столовые горы и арки ----------

    private void BuildMesas()
    {
        Mesa(new Vector3(-52f, 0f, -18f), 17f, 22f);
        Mesa(new Vector3(46f, 0f, -34f), 21f, 27f);
        Mesa(new Vector3(-38f, 0f, 44f), 13f, 15f);
        Mesa(new Vector3(62f, 0f, 26f), 15f, 18f);

        Arch(new Vector3(-14f, 0f, -58f), 22f, 14f, Stone);
        Arch(OnGround(30f, 58f), 16f, 10f, Stone * 1.06f);

        // Балансирующие камни — характерный силуэт каньона
        for (int i = 0; i < 7; i++)
        {
            float a = i * 51f * Mathf.Deg2Rad;
            float r = 40f + (i % 3) * 12f;
            Vector3 p = OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Rock(p + new Vector3(0f, 1.6f, 0f), 4.2f, StoneDark);
            Rock(p + new Vector3(0.4f, 4.4f, -0.3f), 2.6f, Stone);
            Rock(p + new Vector3(0f, 6.2f, 0.2f), 1.5f, Stone * 1.1f);
        }
    }

    private void Mesa(Vector3 basePos, float radius, float height)
    {
        Vector3 p = OnGround(basePos.x, basePos.z);
        Material rock = Gfx.MatFull(Stone, 0.03f, 0f, Color.black, 4f, 0.8f);
        Material band = Gfx.MatFull(StoneDark, 0.03f, 0f, Color.black, 6f, 0.8f);

        // Слоистые уступы: каждый следующий слой уже предыдущего
        int layers = 5;
        for (int i = 0; i < layers; i++)
        {
            float t = (float)i / layers;
            float r = radius * (1f - t * 0.42f);
            float h = height / layers;
            Gfx.Cyl(transform, p + new Vector3(0f, h * (i + 0.5f), 0f),
                new Vector3(r * 2f, h * 0.5f, r * 2f), i % 2 == 0 ? rock : band);
        }
        // Плоская вершина с монетами
        Gfx.Cyl(transform, p + new Vector3(0f, height + 0.4f, 0f),
            new Vector3(radius * 1.16f, 0.4f, radius * 1.16f), Gfx.Mat(SandHigh, 0.04f));
        AddCoin(p + new Vector3(0f, height + 2f, 0f));
        AddCoin(p + new Vector3(radius * 0.35f, height + 2f, radius * 0.3f));
        AddCoin(p + new Vector3(-radius * 0.3f, height + 2f, -radius * 0.35f));
    }

    // ---------- Подъём по каньону ----------

    private void BuildCanyonClimb()
    {
        // Ущелье между двумя стенами уходит на запад
        Material wall = Gfx.MatFull(Stone, 0.03f, 0f, Color.black, 5f, 0.85f);
        for (int i = 0; i < 8; i++)
        {
            float z = -10f - i * 9f;
            float w = 10f + (i % 3) * 3f;
            Gfx.Box(transform, new Vector3(-24f - i * 1.2f, 7f, z),
                new Vector3(w, 16f + (i % 4) * 3f, 9f), wall);
            Gfx.Box(transform, new Vector3(-2f + i * 1.2f, 6f, z),
                new Vector3(w * 0.85f, 13f + (i % 3) * 4f, 9f), wall);
        }

        // Ступени-плиты вдоль стены
        Color slab = new Color(0.8f, 0.55f, 0.34f);
        Vector3[] steps = {
            new Vector3(-12f, 2.4f, -14f), new Vector3(-16f, 5f, -22f),
            new Vector3(-11f, 7.6f, -30f), new Vector3(-16f, 10.2f, -38f)
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Platform(steps[i], new Vector3(5.2f, 0.7f, 5.2f), slab, 0.05f);
            AddCoin(steps[i] + new Vector3(0f, 1.5f, 0f));
        }

        AddBouncePad(new Vector3(-13f, 10.9f, -38f), 20f);
        AddCoin(new Vector3(-13f, 15f, -42f));
        AddCoin(new Vector3(-13f, 18f, -46f));

        // Движущиеся плиты над ущельем
        AddMovingPlatform(new Vector3(-13f, 16f, -46f), new Vector3(-13f, 16f, -60f),
            new Vector3(4.4f, 0.6f, 4.4f), slab, 5.5f, 0f);
        AddMovingPlatform(new Vector3(-22f, 19f, -60f), new Vector3(-38f, 19f, -56f),
            new Vector3(4.4f, 0.6f, 4.4f), slab, 6.5f, 0.35f);
        AddMovingPlatform(new Vector3(-46f, 22f, -48f), new Vector3(-46f, 22f, -32f),
            new Vector3(4.4f, 0.6f, 4.4f), slab, 6f, 0.7f);

        AddCoin(new Vector3(-13f, 18f, -53f));
        AddCoin(new Vector3(-30f, 21f, -58f));
        AddCoin(new Vector3(-46f, 24f, -40f));

        // Верхняя площадка примыкает к самой высокой столовой горе
        Vector3 topPad = new Vector3(-52f, 22.6f, -18f);
        Platform(topPad, new Vector3(9f, 0.8f, 9f), slab, 0.05f);
        AddCoin(topPad + new Vector3(0f, 2f, 0f));
        AddEnemy(topPad + new Vector3(-3f, 0.8f, 0f), topPad + new Vector3(3f, 0.8f, 0f), "beetle", 2.4f);

        // Жуки в самом ущелье
        AddEnemy(new Vector3(-12f, 0.4f, -16f), new Vector3(-12f, 0.4f, -34f), "beetle", 3f);
        AddEnemy(new Vector3(-18f, 0.4f, -44f), new Vector3(-6f, 0.4f, -44f), "beetle", 2.6f);
    }

    // ---------- Оазис ----------

    private void BuildOasis()
    {
        Vector3 c = new Vector3(30f, 0f, 8f);

        Gfx.Box(transform, c + new Vector3(0f, -0.9f, 0f), new Vector3(24f, 2f, 20f),
            Gfx.MatFull(new Color(0.62f, 0.5f, 0.32f), 0.03f, 0f, Color.black, 4f, 0.5f));
        Water(c + new Vector3(0f, 0.35f, 0f), new Vector2(23f, 19f));

        // Влажная зелень по берегу
        for (int i = 0; i < 16; i++)
        {
            float a = i * 22.5f * Mathf.Deg2Rad;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 13.5f, 0.2f, Mathf.Sin(a) * 11.5f);
            Bush(p, new Color(0.24f, 0.46f, 0.2f));
        }
        GrassField(c, new Vector2(15f, 13f), 3420,
            new Color(0.22f, 0.44f, 0.14f), new Color(0.62f, 0.86f, 0.34f));

        Palm(c + new Vector3(-11f, 0f, 7f), 1.15f);
        Palm(c + new Vector3(10f, 0f, 8f), 0.95f);
        Palm(c + new Vector3(13f, 0f, -6f), 1.25f);
        Palm(c + new Vector3(-12f, 0f, -7f), 1.05f);
        Palm(c + new Vector3(0f, 0f, 12f), 0.9f);

        // Камни-ступеньки к островку в центре озера
        Color st = new Color(0.68f, 0.5f, 0.36f);
        Platform(c + new Vector3(-7f, 0.5f, 3f), new Vector3(2.6f, 1f, 2.6f), st);
        Platform(c + new Vector3(-3f, 0.5f, 0f), new Vector3(2.6f, 1f, 2.6f), st);
        Platform(c + new Vector3(0f, 0.9f, -2f), new Vector3(6f, 1.8f, 6f), st);
        AddCoin(c + new Vector3(-7f, 1.6f, 3f));
        AddCoin(c + new Vector3(-3f, 1.6f, 0f));
        AddCoin(c + new Vector3(0f, 2.6f, -2f));

        AddCampfire(c + new Vector3(-14f, 0.3f, 0f), new Color(1f, 0.7f, 0.35f), 15f);
        AddEnemy(c + new Vector3(-16f, 0.2f, 10f), c + new Vector3(14f, 0.2f, 12f), "beetle", 2.8f);
    }

    private void Palm(Vector3 pos, float scale)
    {
        Vector3 p = OnGround(pos.x, pos.z);
        Material bark = Gfx.MatFull(new Color(0.5f, 0.38f, 0.22f), 0.04f, 0f, Color.black, 3f, 0.6f);

        // Изогнутый ствол из наклонных сегментов
        int seg = 6;
        Vector3 cur = p;
        float lean = Random.Range(-9f, 9f);
        for (int i = 0; i < seg; i++)
        {
            float h = 1.15f * scale;
            GameObject s = Gfx.Cyl(transform, cur + new Vector3(0f, h * 0.5f, 0f),
                new Vector3(0.6f * scale * (1f - i * 0.06f), h * 0.5f, 0.6f * scale), bark, i < 2);
            s.transform.localRotation = Quaternion.Euler(0f, 0f, lean * (i * 0.22f));
            cur += new Vector3(Mathf.Sin(lean * (i * 0.22f) * Mathf.Deg2Rad) * -h * 0.35f, h, 0f);
        }

        Color leaf = new Color(0.26f, 0.52f, 0.24f);
        Material fol = Gfx.FoliageMat(leaf, leaf * 1.7f);
        for (int i = 0; i < 7; i++)
        {
            float a = i * (360f / 7f);
            GameObject frond = Gfx.Box(transform,
                cur + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -0.25f * scale, 2.1f * scale),
                new Vector3(0.7f * scale, 0.14f * scale, 4.2f * scale), fol, false);
            frond.transform.localRotation = Quaternion.Euler(20f, a, 0f);
        }
        Gfx.Blob(transform, cur, new Vector3(1.1f, 0.9f, 1.1f) * scale, fol,
            Mathf.Abs(Mathf.RoundToInt(pos.x * 3.1f)), 0.4f, false);

        // Кокосы
        Material nut = Gfx.Mat(new Color(0.36f, 0.26f, 0.16f), 0.1f);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            Gfx.Ball(transform, cur + new Vector3(Mathf.Cos(a) * 0.6f, -0.5f, Mathf.Sin(a) * 0.6f) * scale,
                new Vector3(0.4f, 0.4f, 0.4f) * scale, nut);
        }
    }

    // ---------- Руины ----------

    private void BuildRuins()
    {
        Vector3 c = new Vector3(6f, 0f, 30f);
        Vector3 baseP = OnGround(c.x, c.z);
        Material stone = Gfx.MatFull(new Color(0.84f, 0.74f, 0.56f), 0.06f, 0f, Color.black, 3f, 0.55f);
        Material worn = Gfx.MatFull(new Color(0.72f, 0.62f, 0.46f), 0.04f, 0f, Color.black, 4f, 0.7f);

        // Ступенчатый постамент
        Gfx.Box(transform, baseP + new Vector3(0f, 0.35f, 0f), new Vector3(24f, 0.7f, 20f), worn);
        Gfx.Box(transform, baseP + new Vector3(0f, 0.95f, 0f), new Vector3(20f, 0.6f, 16f), stone);

        // Колонны разной сохранности
        float[] heights = { 6.5f, 4.2f, 7f, 2.4f, 6.8f, 5.5f, 3.1f, 7.2f };
        int k = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float h = heights[k % heights.Length];
                Vector3 p = baseP + new Vector3(7.5f * sx, 1.25f + h * 0.5f, -6f + i * 4f);
                Gfx.Cyl(transform, p, new Vector3(1.5f, h * 0.5f, 1.5f), stone);
                // Каннелюры — тонкие рёбра по кругу
                for (int f = 0; f < 6; f++)
                {
                    float a = f * 60f * Mathf.Deg2Rad;
                    Gfx.Box(transform, p + new Vector3(Mathf.Cos(a) * 0.74f, 0f, Mathf.Sin(a) * 0.74f),
                        new Vector3(0.16f, h * 0.94f, 0.16f), worn, false);
                }
                if (h > 5f)
                {
                    Gfx.Box(transform, p + new Vector3(0f, h * 0.5f + 0.25f, 0f),
                        new Vector3(2.1f, 0.5f, 2.1f), stone);
                    if (k % 3 == 0) AddCoin(p + new Vector3(0f, h * 0.5f + 1.6f, 0f));
                }
                k++;
            }
        }

        // Обрушенная перемычка между двумя колоннами
        GameObject lintel = Gfx.Box(transform, baseP + new Vector3(0f, 7.4f, -6f),
            new Vector3(16f, 0.9f, 1.8f), stone);
        lintel.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);

        // Упавшие барабаны колонн
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = baseP + new Vector3(Random.Range(-9f, 9f), 1.6f, Random.Range(-7f, 7f));
            GameObject drum = Gfx.Cyl(transform, p, new Vector3(1.4f, 0.7f, 1.4f), worn);
            drum.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 180f), 0f);
        }

        // Алтарь в центре: подсвеченная плита и монеты
        Material glyph = Gfx.MatFull(new Color(0.95f, 0.72f, 0.3f), 0.5f, 0.2f,
            new Color(0.9f, 0.5f, 0.12f), 0f, 0f);
        Gfx.Cyl(transform, baseP + new Vector3(0f, 1.6f, 0f), new Vector3(5f, 0.35f, 5f), glyph);
        Gfx.Glow(transform, baseP + new Vector3(0f, 2.2f, 0f), 7f, new Color(1f, 0.7f, 0.3f, 0.4f));
        Gfx.PointLight(transform, baseP + new Vector3(0f, 3f, 0f), new Color(1f, 0.72f, 0.35f), 20f, 1.3f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            AddCoin(baseP + new Vector3(Mathf.Cos(a) * 3f, 2.6f, Mathf.Sin(a) * 3f));
        }

        AddEnemy(baseP + new Vector3(-8f, 1.3f, 6f), baseP + new Vector3(8f, 1.3f, 6f), "beetle", 3.2f);
        AddEnemy(baseP + new Vector3(8f, 1.3f, -6f), baseP + new Vector3(-8f, 1.3f, -6f), "beetle", 2.7f);
        AddBouncePad(baseP + new Vector3(0f, 1.4f, 7f), 18f);
        AddCoin(baseP + new Vector3(0f, 6f, 7f));
    }

    // ---------- Дюны, кактусы, кости ----------

    private void BuildDunes()
    {
        for (int i = 0; i < 34; i++)
        {
            float ang = i * 137.5f * Mathf.Deg2Rad;
            float r = 18f + (i % 8) * 8.5f;
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            if (Mathf.Abs(x - 30f) < 17f && Mathf.Abs(z - 8f) < 15f) continue;  // оазис
            if (Mathf.Abs(x - 6f) < 15f && Mathf.Abs(z - 30f) < 13f) continue;  // руины
            if (x < -2f && z < -8f) continue;                                    // ущелье
            Cactus(OnGround(x, z), 0.8f + (i % 5) * 0.18f);
        }

        Color[] rocks = { StoneDark, Stone, new Color(0.66f, 0.5f, 0.36f) };
        for (int i = 0; i < 26; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(20f, 78f);
            Vector3 p = OnGround(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            Rock(p + new Vector3(0f, 0.4f, 0f), Random.Range(1.2f, 3.6f), rocks[i % 3]);
        }

        // Выбеленные кости — россыпь по пустыне
        Material bone = Gfx.Mat(new Color(0.9f, 0.88f, 0.8f), 0.08f);
        for (int i = 0; i < 5; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(25f, 62f);
            Vector3 p = OnGround(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0.25f);
            for (int j = 0; j < 6; j++)
            {
                GameObject rib = Gfx.Cyl(transform,
                    p + new Vector3(0f, 0.5f, -1.5f + j * 0.6f), new Vector3(0.14f, 0.9f, 0.14f), bone, false);
                rib.transform.localRotation = Quaternion.Euler(0f, 0f, 90f - j * 4f);
            }
            Gfx.Ball(transform, p + new Vector3(0f, 0.4f, 2.4f), new Vector3(1.1f, 0.8f, 1.4f), bone);
        }

        // Монеты кольцом вокруг спавна
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            float r = 14f + (i % 3) * 9f;
            AddCoin(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 1.3f));
        }

        AddEnemy(OnGround(-14f, 20f), OnGround(-26f, 14f), "beetle", 2.9f);
        AddEnemy(OnGround(44f, 44f), OnGround(30f, 50f), "beetle", 3.1f);
    }

    private void BuildAtmosphere()
    {
        Cloud(new Vector3(-50f, 44f, -40f), 2.4f, new Color(1f, 0.96f, 0.88f));
        Cloud(new Vector3(36f, 48f, -62f), 3f, new Color(1f, 0.95f, 0.86f));
        Cloud(new Vector3(70f, 42f, 30f), 2.2f, new Color(1f, 0.97f, 0.9f));

        Color mnt = new Color(0.66f, 0.45f, 0.32f);
        Mountain(new Vector3(-150f, -10f, -100f), 52f, 60f, mnt, false);
        Mountain(new Vector3(-20f, -10f, -164f), 60f, 72f, mnt * 0.94f, false);
        Mountain(new Vector3(140f, -10f, -110f), 46f, 54f, mnt * 1.05f, false);
        Mountain(new Vector3(164f, -10f, 40f), 50f, 58f, mnt, false);
        Mountain(new Vector3(-152f, -10f, 76f), 48f, 52f, mnt * 0.96f, false);
        Mountain(new Vector3(50f, -10f, 158f), 44f, 48f, mnt * 0.9f, false);

        // Позёмка: песчинки летят низко над дюнами
        ParticleFx sand = ParticleFx.Spawn(transform, new Vector3(0f, 2f, 0f), 160,
            new Color(0.95f, 0.85f, 0.6f, 0.5f));
        sand.EmitExtents = new Vector3(70f, 3f, 70f);
        sand.BaseVelocity = new Vector3(5.5f, 0.2f, 1.5f);
        sand.Gravity = new Vector3(0.5f, -0.1f, 0.2f);
        sand.SpeedMin = 0.5f;
        sand.SpeedMax = 2f;
        sand.SizeMin = 0.08f;
        sand.SizeMax = 0.2f;
        sand.LifeMin = 4f;
        sand.LifeMax = 7f;
        sand.Prewarm();

        // Марево над горячим песком
        Motes(new Vector3(0f, 6f, 0f), new Vector3(50f, 6f, 50f), 70,
            new Color(1f, 0.9f, 0.6f, 0.35f), 0.2f);
    }
}
