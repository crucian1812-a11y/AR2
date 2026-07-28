using UnityEngine;

// Снежные вершины — просторная зимняя долина: замёрзшее озеро, ледник
// с пещерой, лагерь у костров, ельник и восхождение по спирали на пик.
public class SnowWorld : WorldBuilder
{
    private static readonly Color Snow = new Color(0.93f, 0.95f, 1f);
    private static readonly Color SnowDeep = new Color(0.78f, 0.83f, 0.95f);
    private static readonly Color Ice = new Color(0.66f, 0.84f, 1f);
    private static readonly Color RockCol = new Color(0.5f, 0.53f, 0.62f);

    private Vector3 _peak;

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 2f, 56f);
        _peak = new Vector3(0f, 0f, -24f);

        SetupSky(new Color(0.36f, 0.5f, 0.78f), new Color(0.86f, 0.9f, 0.98f),
            new Color(0.55f, 0.6f, 0.72f), new Vector3(24f, 55f, 0f), 1.05f,
            0.0035f, new Color(0.84f, 0.89f, 0.97f));
        // Снег и так почти белый — свечение держим слабым, иначе засветка.
        SetPostFx(0.85f, 1.04f, new Color(0.94f, 0.98f, 1.08f), 0.6f);

        // Ровные места: чаша замёрзшего озера и подножие пика
        FlattenArea(38f, 22f, 17f, 12f);
        FlattenArea(0f, -24f, 19f, 12f);
        FlattenArea(-44f, -8f, 17f, 12f);

        // Настоящие PBR-текстуры поверхности
        TerrainTextures("Snow015_1K-PNG_Color", "Snow015_1K-PNG_NormalGL",
            "marble_rock_03_diff_1k", "marble_rock_03_nor_gl_1k", 0.1f, 0.14f);

        Terrain(Vector3.zero, new Vector2(260f, 260f), 150, 10f, 0.011f,
            SnowDeep, Snow, 24f, 5252);

        Gfx.Cone(transform, new Vector3(0f, -34f, 0f), 82f, 26f,
            Gfx.MatFull(RockCol * 0.7f, 0.03f, 0f, Color.black, 5f, 0.7f));

        AddPortal(new Vector3(0f, 1.8f, 66f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        BuildPeak();
        BuildFrozenLake();
        BuildGlacier();
        BuildCamp();
        AddCheckpoint(_peak + new Vector3(0f, 8.2f, 12f));
        AddCheckpoint(_peak + new Vector3(0f, 18.2f, 6f));
        AddCheckpoint(new Vector3(-44f + 16f, 3.2f, -8f + 10f));
        BuildForest();
        // Звёзды — цель мира, каждая на своём постаменте с батутом
        AddStarPedestal(OnGround(-20f, 60f), 12f);
        AddStarPedestal(OnGround(56f, -30f), 14f);
        AddStarPedestal(OnGround(-64f, 34f), 10f);
        // Ледяная эстакада вдоль долины
        ObstacleRun(OnGround(-60f, -50f, 3f), new Vector3(1f, 0f, 0.6f), 8, 1.5f,
            new Color(0.8f, 0.9f, 1f));
        Spinner(OnGround(-4f, 48f, 4f), 9f, -60f, new Color(0.66f, 0.84f, 1f));
        // Босс снегов — пчелиный страж над замёрзшим озером
        AddBoss(new Vector3(28f, 6f, 22f), new Vector3(48f, 6f, 22f),
            "armabee", 4.2f, 5, 1.6f);
        BuildAtmosphere();

        AddVillager(OnGround(-12f, 38f), "Смотритель лагеря", "Yeti", 2.2f,
            "Костёр здесь не гаснет триста лет. Говорят, его зажгли\nот того самого Сердца горы.");
        AddVillager(OnGround(20f, 44f), "Ледоруб", "Penguin", 1.5f,
            "Тоннель в леднике прорубили не мы. Он был здесь раньше\nи ведёт куда-то глубже, чем видно.");
    }

    // ---------- Пик и восхождение ----------

    private void BuildPeak()
    {
        Material rock = Gfx.MatFull(RockCol, 0.05f, 0f, Color.black, 4f, 0.8f);
        Material cap = Gfx.MatFull(Snow, 0.22f, 0f, Color.black, 3f, 0.4f);

        // Ступенчатое основание горы
        Gfx.Cyl(transform, _peak + new Vector3(0f, 5f, 0f), new Vector3(34f, 5f, 34f), rock);
        Gfx.Cyl(transform, _peak + new Vector3(0f, 12f, 0f), new Vector3(24f, 3.5f, 24f), rock);
        Gfx.Cyl(transform, _peak + new Vector3(0f, 20f, 0f), new Vector3(15f, 5f, 15f), rock);
        Gfx.Cyl(transform, _peak + new Vector3(0f, 27f, 0f), new Vector3(10f, 2.5f, 10f), cap);
        GameObject tip = Gfx.Cone(transform, _peak + new Vector3(0f, 29f, 0f), 5.2f, 7f, cap);
        Gfx.NoShadow(tip);

        // Спираль уступов вокруг горы: 20 площадок, чередуются снег и лёд
        const int ledges = 20;
        Material iceMat = Gfx.MatFull(Ice, 0.9f, 0.25f, new Color(0.08f, 0.18f, 0.3f), 0f, 0f);
        for (int i = 0; i < ledges; i++)
        {
            float ang = 0.62f * i + Mathf.PI * 0.5f;
            float r = 17f - i * 0.35f;
            Vector3 pos = _peak + new Vector3(Mathf.Cos(ang) * r, 1.6f + i * 1.5f, Mathf.Sin(ang) * r);
            bool icy = i % 3 == 2;
            GameObject slab = Gfx.Box(transform, pos, new Vector3(5.2f, 0.6f, 4.2f),
                icy ? iceMat : Gfx.MatFull(Snow, 0.25f, 0f, Color.black, 2f, 0.3f));
            slab.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
            if (i % 2 == 0) AddCoin(pos + new Vector3(0f, 1.4f, 0f));
            if (i == 6 || i == 13)
            {
                AddEnemy(pos + new Vector3(-1.6f, 0.8f, 0f), pos + new Vector3(1.6f, 0.8f, 0f),
                    "slime", 1.7f + i * 0.05f);
            }
            if (i == 9) AddBouncePad(pos + new Vector3(0f, 0.4f, 0f), 18f);
        }

        // Движущиеся льдины через разлом на середине подъёма
        AddMovingPlatform(_peak + new Vector3(-19f, 13f, 6f), _peak + new Vector3(-19f, 13f, -8f),
            new Vector3(4.2f, 0.6f, 4.2f), Ice, 5.5f, 0f);
        AddMovingPlatform(_peak + new Vector3(-14f, 17f, -14f), _peak + new Vector3(-2f, 17f, -20f),
            new Vector3(4.2f, 0.6f, 4.2f), Ice, 6f, 0.4f);
        AddCoin(_peak + new Vector3(-19f, 15f, -2f));
        AddCoin(_peak + new Vector3(-8f, 19f, -18f));

        // Вершина: смотровая площадка с флагом и щедрой россыпью монет
        Vector3 top = _peak + new Vector3(0f, 1.6f + ledges * 1.5f + 1.4f, 0f);
        Platform(top, new Vector3(9f, 0.7f, 9f), new Color(0.88f, 0.93f, 1f), 0.35f);
        Gfx.Cyl(transform, top + new Vector3(0f, 3f, 0f), new Vector3(0.22f, 3f, 0.22f),
            Gfx.Mat(new Color(0.45f, 0.35f, 0.25f), 0.1f), false);
        GameObject flag = Gfx.Box(transform, top + new Vector3(1.2f, 5.2f, 0f),
            new Vector3(2.4f, 1.4f, 0.08f),
            Gfx.MatFull(new Color(0.95f, 0.35f, 0.35f), 0.2f, 0f, new Color(0.4f, 0.05f, 0.05f), 0f, 0f), false);
        Gfx.NoShadow(flag);
        Gfx.PointLight(transform, top + new Vector3(0f, 3f, 0f), new Color(1f, 0.85f, 0.6f), 22f, 1.2f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            AddCoin(top + new Vector3(Mathf.Cos(a) * 2.8f, 1.6f, Mathf.Sin(a) * 2.8f));
        }
    }

    // ---------- Замёрзшее озеро ----------

    private void BuildFrozenLake()
    {
        Vector3 c = new Vector3(38f, 0f, 22f);
        Gfx.Box(transform, c + new Vector3(0f, -1.2f, 0f), new Vector3(36f, 2f, 30f),
            Gfx.MatFull(new Color(0.4f, 0.44f, 0.5f), 0.03f, 0f, Color.black, 4f, 0.5f));

        // Ледяная корка — крупные многоугольные плиты со швами
        Material sheet = Gfx.MatFull(Ice, 0.92f, 0.3f, new Color(0.06f, 0.16f, 0.28f), 0f, 0f);
        for (int i = 0; i < 12; i++)
        {
            float a = Random.value * 360f;
            Vector3 p = c + new Vector3(Random.Range(-15f, 15f), 0.1f, Random.Range(-12f, 12f));
            GameObject plate = Gfx.Box(transform, p, new Vector3(Random.Range(9f, 15f), 0.35f, Random.Range(8f, 13f)), sheet);
            plate.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            Gfx.NoShadow(plate);
        }

        // Торосы по краю
        for (int i = 0; i < 14; i++)
        {
            float a = i * 25.7f * Mathf.Deg2Rad;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 17f, 0.4f, Mathf.Sin(a) * 14f);
            Gfx.Crystal(transform, p, Random.Range(0.6f, 1.3f), Random.Range(2f, 4.5f),
                sheet, Random.Range(-16f, 16f));
        }

        AddCoin(c + new Vector3(0f, 1.2f, 0f));
        AddCoin(c + new Vector3(8f, 1.2f, 6f));
        AddCoin(c + new Vector3(-9f, 1.2f, -5f));
        AddCoin(c + new Vector3(6f, 1.2f, -9f));
        AddEnemy(c + new Vector3(-12f, 0.4f, 8f), c + new Vector3(12f, 0.4f, 8f), "slime", 3.2f);
        AddEnemy(c + new Vector3(10f, 0.4f, -8f), c + new Vector3(-10f, 0.4f, -4f), "slime", 2.8f);

        // Островок с сокровищем посреди озера
        Platform(c + new Vector3(0f, 0.9f, 0f), new Vector3(7f, 1.6f, 7f), SnowDeep, 0.2f);
        Gfx.Glow(transform, c + new Vector3(0f, 2.4f, 0f), 6f, new Color(0.6f, 0.85f, 1f, 0.35f));
        Gfx.PointLight(transform, c + new Vector3(0f, 2.6f, 0f), new Color(0.6f, 0.85f, 1f), 18f, 1f);
    }

    // ---------- Ледник и пещера ----------

    private void BuildGlacier()
    {
        Vector3 c = new Vector3(-44f, 0f, -8f);
        Material glacier = Gfx.MatFull(new Color(0.72f, 0.86f, 1f), 0.75f, 0.15f,
            new Color(0.07f, 0.16f, 0.28f), 0f, 0f);

        // Массив льда со сколами
        Gfx.Box(transform, c + new Vector3(0f, 8f, 0f), new Vector3(30f, 16f, 26f), glacier);
        Gfx.Box(transform, c + new Vector3(-8f, 17f, 4f), new Vector3(16f, 6f, 14f), glacier);
        for (int i = 0; i < 9; i++)
        {
            Gfx.Crystal(transform,
                c + new Vector3(Random.Range(-14f, 14f), 16.5f, Random.Range(-12f, 12f)),
                Random.Range(0.9f, 2f), Random.Range(4f, 9f), glacier, Random.Range(-14f, 14f));
        }

        // Проход-тоннель сквозь ледник: пол, стены и светящийся свод
        Material tunnel = Gfx.MatFull(new Color(0.55f, 0.78f, 1f), 0.6f, 0.1f,
            new Color(0.12f, 0.26f, 0.42f), 0f, 0f);
        for (int i = 0; i < 8; i++)
        {
            float z = 12f - i * 3.4f;
            Gfx.Box(transform, c + new Vector3(-6f, 0.1f, z), new Vector3(7f, 0.4f, 3.4f), tunnel);
            Gfx.Box(transform, c + new Vector3(-10.2f, 2.4f, z), new Vector3(1.4f, 5f, 3.4f), tunnel);
            Gfx.Box(transform, c + new Vector3(-1.8f, 2.4f, z), new Vector3(1.4f, 5f, 3.4f), tunnel);
            GameObject roof = Gfx.Box(transform, c + new Vector3(-6f, 4.8f, z), new Vector3(9f, 0.5f, 3.4f), tunnel, false);
            Gfx.NoShadow(roof);
            if (i % 2 == 0)
            {
                Gfx.PointLight(transform, c + new Vector3(-6f, 3.6f, z), new Color(0.55f, 0.82f, 1f), 10f, 1.1f);
                AddCoin(c + new Vector3(-6f, 1.4f, z));
            }
        }
        AddEnemy(c + new Vector3(-6f, 0.6f, 10f), c + new Vector3(-6f, 0.6f, -12f), "slime", 2.6f);

        // Подъём на ледник по уступам
        Color step = new Color(0.8f, 0.9f, 1f);
        Platform(c + new Vector3(16f, 2.5f, 10f), new Vector3(5f, 0.7f, 5f), step, 0.4f);
        Platform(c + new Vector3(19f, 5.2f, 3f), new Vector3(5f, 0.7f, 5f), step, 0.4f);
        Platform(c + new Vector3(17f, 7.9f, -5f), new Vector3(5f, 0.7f, 5f), step, 0.4f);
        Platform(c + new Vector3(13f, 10.6f, -11f), new Vector3(5f, 0.7f, 5f), step, 0.4f);
        Platform(c + new Vector3(8f, 13.3f, -13f), new Vector3(5f, 0.7f, 5f), step, 0.4f);
        AddCoin(c + new Vector3(16f, 4f, 10f));
        AddCoin(c + new Vector3(19f, 6.7f, 3f));
        AddCoin(c + new Vector3(17f, 9.4f, -5f));
        AddCoin(c + new Vector3(13f, 12.1f, -11f));
        AddCoin(c + new Vector3(0f, 18f, 0f));
        AddCoin(c + new Vector3(-8f, 21.5f, 4f));
        AddEnemy(c + new Vector3(-6f, 16.4f, -6f), c + new Vector3(8f, 16.4f, -6f), "slime", 2.2f);

        AddBouncePad(c + new Vector3(13f, 11.3f, -11f), 19f);
        AddCoin(c + new Vector3(13f, 19f, -14f));
    }

    // ---------- Лагерь ----------

    private void BuildCamp()
    {
        Vector3 c = OnGround(-16f, 34f);
        Material canvas = Gfx.RimMat(new Color(0.85f, 0.5f, 0.32f), new Color(1f, 0.8f, 0.5f), 0.35f);
        Material wood = Gfx.MatFull(new Color(0.42f, 0.3f, 0.2f), 0.05f, 0f, Color.black, 2f, 0.4f);

        // Три палатки вокруг костра
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Vector3 p = c + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 6.5f);
            p.y = GroundHeight(p.x, p.z);
            GameObject tent = Gfx.Cone(transform, p, 2.6f, 3.6f, canvas, true);
            tent.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            Gfx.Cyl(transform, p + new Vector3(0f, 2f, 0f), new Vector3(0.14f, 2f, 0.14f), wood, false);
        }

        AddCampfire(c + new Vector3(0f, 0.15f, 0f), new Color(1f, 0.62f, 0.3f), 20f);

        // Сложенные дрова и ящики
        for (int i = 0; i < 5; i++)
        {
            GameObject log = Gfx.Cyl(transform, c + new Vector3(3.4f, 0.3f + i * 0.45f, -3f),
                new Vector3(0.3f, 1.4f, 0.3f), wood);
            log.transform.localRotation = Quaternion.Euler(90f, i * 12f, 0f);
        }
        Gfx.Box(transform, c + new Vector3(-3.6f, 0.6f, -3f), new Vector3(1.6f, 1.2f, 1.6f), wood);
        Gfx.Box(transform, c + new Vector3(-3.6f, 1.8f, -3.2f), new Vector3(1.2f, 1.2f, 1.2f), wood);

        AddCoin(c + new Vector3(0f, 2.4f, 0f));
        AddCoin(c + new Vector3(-3.6f, 3.2f, -3.2f));
        AddCoin(c + new Vector3(3.4f, 2.6f, -3f));
    }

    // ---------- Ельник и камни ----------

    private void BuildForest()
    {
        for (int i = 0; i < 46; i++)
        {
            float ang = i * 137.5f * Mathf.Deg2Rad;
            float r = 24f + (i % 9) * 6.5f;
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            if (Mathf.Abs(x - 38f) < 20f && Mathf.Abs(z - 22f) < 18f) continue; // озеро
            if (Mathf.Abs(x + 44f) < 20f && Mathf.Abs(z + 8f) < 17f) continue;  // ледник
            if (z < 2f && new Vector2(x, z + 24f).magnitude < 22f) continue;    // гора
            Pine(OnGround(x, z), true, 0.85f + (i % 5) * 0.22f);
        }

        // Заснеженные валуны
        for (int i = 0; i < 20; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(22f, 76f);
            Vector3 p = OnGround(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0.3f);
            Rock(p, Random.Range(1.2f, 3.4f), Color.Lerp(RockCol, Snow, Random.value * 0.5f));
        }

        // Сугробы — мягкие холмики у деревьев
        Material drift = Gfx.MatFull(Snow, 0.2f, 0f, Color.black, 2f, 0.25f);
        for (int i = 0; i < 24; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(16f, 70f);
            Vector3 p = OnGround(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            Gfx.Blob(transform, p, new Vector3(Random.Range(3f, 6f), Random.Range(0.9f, 1.8f), Random.Range(3f, 6f)),
                drift, i * 13, 0.4f, false);
        }

        // Монеты по долине
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            float r = 16f + (i % 4) * 9f;
            AddCoin(OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r + 24f, 1.3f));
        }

        AddEnemy(OnGround(-10f, 46f), OnGround(10f, 46f), "slime", 2.5f);
        AddEnemy(OnGround(22f, 50f), OnGround(34f, 42f), "slime", 2.9f);
        AddEnemy(OnGround(-30f, 20f), OnGround(-20f, 28f), "slime", 2.3f);
    }

    private void BuildAtmosphere()
    {
        Snowfall(new Vector3(0f, 30f, 0f), new Vector3(60f, 3f, 60f), 700);

        Cloud(new Vector3(-40f, 40f, -34f), 2.8f, new Color(0.9f, 0.93f, 1f));
        Cloud(new Vector3(34f, 46f, -52f), 3.2f, new Color(0.88f, 0.91f, 0.99f));
        Cloud(new Vector3(56f, 38f, 20f), 2.4f, new Color(0.9f, 0.93f, 1f));
        Cloud(new Vector3(-24f, 44f, 46f), 2.6f, new Color(0.88f, 0.91f, 0.99f));
        Cloud(new Vector3(8f, 52f, -10f), 3f, new Color(0.92f, 0.94f, 1f));

        Color mnt = new Color(0.56f, 0.63f, 0.78f);
        Mountain(new Vector3(-150f, -10f, -110f), 56f, 76f, mnt, true);
        Mountain(new Vector3(-24f, -10f, -172f), 66f, 92f, mnt * 0.94f, true);
        Mountain(new Vector3(136f, -10f, -120f), 50f, 68f, mnt * 1.04f, true);
        Mountain(new Vector3(168f, -10f, 30f), 54f, 72f, mnt, true);
        Mountain(new Vector3(-156f, -10f, 70f), 52f, 66f, mnt * 0.96f, true);
        Mountain(new Vector3(40f, -10f, 160f), 46f, 58f, mnt * 0.9f, true);

        // Морозная взвесь искрится в воздухе
        Motes(new Vector3(0f, 5f, 0f), new Vector3(50f, 5f, 50f), 110,
            new Color(0.85f, 0.95f, 1f, 0.7f), 0.11f);
    }
}
