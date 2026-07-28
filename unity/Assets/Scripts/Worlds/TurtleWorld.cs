using UnityEngine;

// Черепахоград — город, выстроенный на панцирях гигантских черепах,
// дремлющих в тёплой лагуне. Панцири соединены мостами, между ними ходят
// паромы-черепашата, а по берегам бегают крабы.
public class TurtleWorld : WorldBuilder
{
    private static readonly Color SandLow = new Color(0.82f, 0.74f, 0.55f);
    private static readonly Color SandHigh = new Color(0.94f, 0.89f, 0.72f);
    private static readonly Color ShellDark = new Color(0.24f, 0.4f, 0.3f);
    private static readonly Color ShellLight = new Color(0.42f, 0.62f, 0.44f);
    private static readonly Color Plank = new Color(0.62f, 0.44f, 0.28f);

    // Центры панцирей: главный в середине, остальные вокруг лагуны.
    private static readonly Vector3[] Shells =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(-34f, 0f, -20f),
        new Vector3(32f, 0f, -24f),
        new Vector3(-30f, 0f, 26f),
        new Vector3(34f, 0f, 24f)
    };
    private static readonly float[] ShellRadius = { 20f, 13f, 14f, 12f, 13f };
    private static readonly float[] ShellHeight = { 9f, 6.5f, 7f, 6f, 6.5f };

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 12f, 8f);

        SetupSky(new Color(0.24f, 0.6f, 0.92f), new Color(0.98f, 0.9f, 0.72f),
            new Color(0.5f, 0.62f, 0.55f), new Vector3(38f, 200f, 0f), 1.2f, 0.0018f,
            new Color(0.92f, 0.88f, 0.78f));
        SetPostFx(1.15f, 1.2f, new Color(1.05f, 1.0f, 0.92f), 0.42f);

        TerrainTextures("aerial_beach_01_diff_1k", "aerial_beach_01_nor_gl_1k",
            "marble_rock_03_diff_1k", "marble_rock_03_nor_gl_1k", 0.09f, 0.14f);

        // Дно лагуны: пологая чаша, весь центр под водой.
        FlattenArea(0f, 0f, 70f, 26f, -6f);
        Terrain(Vector3.zero, new Vector2(240f, 240f), 140, 9f, 0.011f,
            SandLow, SandHigh, 26f, 4242);

        // Сама лагуна
        Water(new Vector3(0f, -0.6f, 0f), new Vector2(150f, 150f));

        for (int i = 0; i < Shells.Length; i++)
            BuildTurtle(Shells[i], ShellRadius[i], ShellHeight[i], i);

        BuildBridges();
        BuildTown();
        BuildFerry();
        AddCheckpoint(new Vector3(0f, ShellHeight[0] + 0.9f, 6f));
        AddCheckpoint(new Vector3(-2f, ShellHeight[0] + 7.9f, 6f));
        BuildShore();
        // Звёзды — цель мира, каждая на своём постаменте с батутом
        // Постаменты стоят на берегу: центр лагуны опущен на -6 и залит
        // водой, и внутри неё батут оказывался под поверхностью.
        AddStarPedestal(OnGround(-86f, -62f), 10f);
        AddStarPedestal(OnGround(84f, 64f), 12f);
        AddStarPedestal(OnGround(-94f, 70f), 9f);
        // Плоты и брёвна над лагуной
        ObstacleRun(OnGround(-92f, -30f, 1.5f), new Vector3(1f, 0f, 0.4f), 8, 1.2f,
            new Color(0.62f, 0.44f, 0.28f));
        Spinner(Shells[0] + new Vector3(0f, ShellHeight[0] + 1.4f, -8f), 10f, 60f,
            new Color(0.62f, 0.44f, 0.28f));
        // Боссы лагуны — акула у берега и капитан на главном панцире
        AddBoss(new Vector3(-24f, 1.2f, 60f), new Vector3(24f, 1.2f, 60f),
            "shark", 4.5f, 5, 1.5f);
        AddBoss(Shells[0] + new Vector3(-9f, ShellHeight[0] + 0.8f, -6f),
            Shells[0] + new Vector3(9f, ShellHeight[0] + 0.8f, -6f),
            "pirate", 3.4f, 6, 1.4f);
        BuildAtmosphere();

        AddPortal(new Vector3(0f, ShellHeight[0] + 1.6f, -14f), NetManager.WorldHub,
            "В деревню", new Color(1f, 0.8f, 0.4f), 0f);
    }

    // ---------- Черепаха ----------

    private void BuildTurtle(Vector3 c, float radius, float height, int index)
    {
        Material shell = Gfx.MatFull(index % 2 == 0 ? ShellDark : ShellDark * 1.12f,
            0.14f, 0f, Color.black, 2f, 0.5f);
        Material plate = Gfx.MatFull(ShellLight, 0.18f, 0f, Color.black, 3f, 0.45f);
        Material skin = Gfx.RimMat(new Color(0.45f, 0.58f, 0.38f),
            new Color(0.75f, 0.95f, 0.6f), 0.3f);

        // Собственная модель черепахи из tools/blender: осознанная форма
        // вместо случайной шумовой сферы. По панцирю можно ходить.
        float dirYaw = index * 72f;
        bool modelled = Gfx.CustomProp(transform, "turtle_giant", c,
            height * 1.55f, dirYaw) != null;

        if (!modelled)
        {
            GameObject dome = Gfx.Blob(transform, c + new Vector3(0f, height * 0.45f, 0f),
                new Vector3(radius * 2f, height * 1.5f, radius * 1.9f), shell,
                index * 17 + 3, 0.18f, false);
            MeshFilter domeMf = dome.GetComponent<MeshFilter>();
            if (domeMf != null)
            {
                MeshCollider mc = dome.AddComponent<MeshCollider>();
                mc.sharedMesh = domeMf.sharedMesh;
            }
        }

        // Плоская площадка на макушке, чтобы город стоял ровно.
        Gfx.Cyl(transform, c + new Vector3(0f, height, 0f),
            new Vector3(radius * 1.25f, 0.35f, radius * 1.25f), plate);

        if (modelled) return;

        // Дальше — запасная сборка из примитивов, если модель не загрузилась.
        // Роговые щитки по кругу
        for (int ring = 0; ring < 2; ring++)
        {
            int count = ring == 0 ? 8 : 12;
            float rr = radius * (ring == 0 ? 0.62f : 0.92f);
            float hh = height * (ring == 0 ? 0.92f : 0.62f);
            for (int i = 0; i < count; i++)
            {
                float a = (float)i / count * Mathf.PI * 2f + ring * 0.3f;
                GameObject seg = Gfx.Box(transform,
                    c + new Vector3(Mathf.Cos(a) * rr, hh, Mathf.Sin(a) * rr),
                    new Vector3(radius * 0.34f, 0.5f, radius * 0.34f), plate, false);
                seg.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                Gfx.NoShadow(seg);
            }
        }

        // Голова, лапы и хвост торчат из-под панциря
        Quaternion rot = Quaternion.Euler(0f, dirYaw, 0f);

        Vector3 headPos = c + rot * new Vector3(0f, height * 0.32f, radius * 1.15f);
        Gfx.Blob(transform, headPos, new Vector3(radius * 0.44f, radius * 0.36f, radius * 0.52f),
            skin, index * 7 + 11, 0.2f, false);
        Material eye = Gfx.MatFull(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0f, Color.black, 0f, 0f);
        Gfx.Ball(transform, headPos + rot * new Vector3(-radius * 0.15f, radius * 0.1f, radius * 0.2f),
            new Vector3(radius * 0.09f, radius * 0.11f, radius * 0.07f), eye);
        Gfx.Ball(transform, headPos + rot * new Vector3(radius * 0.15f, radius * 0.1f, radius * 0.2f),
            new Vector3(radius * 0.09f, radius * 0.11f, radius * 0.07f), eye);

        for (int s = -1; s <= 1; s += 2)
        {
            for (int f = -1; f <= 1; f += 2)
            {
                Gfx.Blob(transform,
                    c + rot * new Vector3(radius * 0.85f * s, height * 0.2f, radius * 0.6f * f),
                    new Vector3(radius * 0.5f, radius * 0.22f, radius * 0.34f),
                    skin, index * 13 + s * 3 + f, 0.25f, false);
            }
        }
        Gfx.Blob(transform, c + rot * new Vector3(0f, height * 0.28f, -radius * 1.1f),
            new Vector3(radius * 0.3f, radius * 0.22f, radius * 0.5f), skin, index * 5 + 2, 0.3f, false);
    }

    // ---------- Мосты между панцирями ----------

    private void BuildBridges()
    {
        for (int i = 1; i < Shells.Length; i++)
        {
            Vector3 a = Shells[0];
            Vector3 b = Shells[i];
            Vector3 dir = (b - a).normalized;

            Vector3 from = a + dir * (ShellRadius[0] * 1.1f);
            from.y = ShellHeight[0] + 0.5f;
            Vector3 to = b - dir * (ShellRadius[i] * 1.1f);
            to.y = ShellHeight[i] + 0.5f;

            Bridge(from, to, 4.4f, Plank);
            AddCoin(Vector3.Lerp(from, to, 0.5f) + new Vector3(0f, 1.2f, 0f));
            AddCoin(Vector3.Lerp(from, to, 0.25f) + new Vector3(0f, 1.2f, 0f));
            AddCoin(Vector3.Lerp(from, to, 0.75f) + new Vector3(0f, 1.2f, 0f));
        }
    }

    // ---------- Город на главном панцире ----------

    private void BuildTown()
    {
        Vector3 c = Shells[0];
        float top = ShellHeight[0];

        // Площадь с костром и лавками
        Gfx.Cyl(transform, c + new Vector3(0f, top + 0.4f, 0f),
            new Vector3(16f, 0.2f, 16f), Gfx.MatFull(Plank, 0.06f, 0f, Color.black, 3f, 0.4f));
        AddCampfire(c + new Vector3(0f, top + 0.6f, 4f), new Color(1f, 0.7f, 0.4f), 20f);

        MarketStall(c + new Vector3(-7f, top + 0.5f, -2f), new Color(0.9f, 0.45f, 0.35f), 30f);
        MarketStall(c + new Vector3(7f, top + 0.5f, -3f), new Color(0.4f, 0.7f, 0.85f), -35f);

        // Хижины по краю панциря
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad + 0.4f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 13f, top + 0.5f, Mathf.Sin(a) * 13f);
            Hut(p, -a * Mathf.Rad2Deg, 1f + (i % 3) * 0.15f);
        }

        // Смотровая вышка с наградой наверху
        Material wood = Gfx.MatFull(Plank, 0.06f, 0f, Color.black, 2.5f, 0.45f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Gfx.Cyl(transform, c + new Vector3(Mathf.Cos(a) * 2.2f, top + 4f, Mathf.Sin(a) * 2.2f),
                new Vector3(0.5f, 4f, 0.5f), wood);
        }
        Platform(c + new Vector3(0f, top + 8.2f, 0f), new Vector3(7f, 0.6f, 7f), Plank);
        Gfx.Cone(transform, c + new Vector3(0f, top + 8.5f, 0f), 5f, 3.2f,
            Gfx.Mat(new Color(0.85f, 0.4f, 0.3f), 0.06f));
        Gfx.PointLight(transform, c + new Vector3(0f, top + 9f, 0f),
            new Color(1f, 0.85f, 0.55f), 26f, 1.4f);
        AddCoin(c + new Vector3(0f, top + 9.6f, 0f));
        Gfx.CustomProp(transform, "treasure_chest", c + new Vector3(2f, top + 8.6f, 0f), 1.2f, 30f);

        // Подъём на вышку по ящикам
        Vector3[] steps = {
            c + new Vector3(6f, top + 1.3f, 5f),
            c + new Vector3(4f, top + 3.3f, 7f),
            c + new Vector3(1f, top + 5.3f, 7.5f),
            c + new Vector3(-2f, top + 7.3f, 6f)
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Platform(steps[i], new Vector3(2.8f, 0.6f, 2.8f), Plank);
            AddCoin(steps[i] + new Vector3(0f, 1.3f, 0f));
        }

        // Жители
        AddNpc(c + new Vector3(-4f, top + 0.5f, 3f));
        AddVillager(c + new Vector3(5f, top + 0.5f, 1f), "Рыбак", "Penguin", 1.6f,
            "Черепахи спят уже триста лет. Мы построили город прямо у них на спинах —\nони и не заметили.");
        AddVillager(c + new Vector3(-9f, top + 0.5f, -6f), "Мастер мостов", "Pig", 1.6f,
            "Мосты держатся на честном слове и хорошем настроении.\nПрыгай смелее, но не смотри вниз.");
        AddVillager(c + new Vector3(9f, top + 0.5f, 6f), "Смотритель маяка", "Chicken", 1.4f,
            "Когда старая черепаха вздохнёт — весь город качается.\nПривыкаешь на третий год.");
    }

    private void Hut(Vector3 pos, float yaw, float scale)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Material wall = Gfx.MatFull(new Color(0.78f, 0.66f, 0.48f), 0.06f, 0f, Color.black, 2.5f, 0.4f);
        Material roof = Gfx.Mat(new Color(0.5f, 0.62f, 0.42f), 0.06f);

        GameObject body = Gfx.Box(transform, pos + new Vector3(0f, 1.5f * scale, 0f),
            new Vector3(4.4f, 3f, 4f) * scale, wall);
        body.transform.localRotation = rot;

        GameObject r = Gfx.Cone(transform, pos + new Vector3(0f, 3f * scale, 0f),
            3.8f * scale, 2.6f * scale, roof);
        r.transform.localRotation = Quaternion.Euler(0f, yaw + 45f, 0f);

        // Тёплое окно
        Material win = Gfx.MatFull(new Color(1f, 0.9f, 0.6f), 0.4f, 0f,
            new Color(1f, 0.78f, 0.35f), 0f, 0f);
        Vector3 wp = pos + rot * new Vector3(0f, 1.7f * scale, 2.05f * scale);
        GameObject w = Gfx.Box(transform, wp, new Vector3(1.2f, 1f, 0.14f) * scale, win, false);
        w.transform.localRotation = rot;
        Gfx.PointLight(transform, wp + rot * new Vector3(0f, 0f, 0.6f),
            new Color(1f, 0.8f, 0.5f), 9f * scale, 1f);

        // Сваи под хижиной — панцирь выпуклый
        Material pole = Gfx.Mat(new Color(0.45f, 0.32f, 0.2f), 0.06f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Gfx.Cyl(transform, pos + rot * new Vector3(1.8f * sx * scale, -0.6f, 1.7f * sz * scale),
                    new Vector3(0.24f, 1f, 0.24f) * scale, pole, false);
    }

    // ---------- Паром-черепашонок ----------

    private void BuildFerry()
    {
        // Движущаяся платформа-плот между дальними панцирями
        AddMovingPlatform(Shells[1] + new Vector3(0f, ShellHeight[1] + 0.6f, 12f),
            Shells[3] + new Vector3(0f, ShellHeight[3] + 0.6f, -12f),
            new Vector3(6f, 0.7f, 6f), Plank, 9f, 0f);
        AddMovingPlatform(Shells[2] + new Vector3(0f, ShellHeight[2] + 0.6f, 12f),
            Shells[4] + new Vector3(0f, ShellHeight[4] + 0.6f, -12f),
            new Vector3(6f, 0.7f, 6f), Plank, 9f, 0.5f);

        AddCoin(Shells[1] + new Vector3(0f, ShellHeight[1] + 2f, 0f));
        AddCoin(Shells[2] + new Vector3(0f, ShellHeight[2] + 2f, 0f));
        AddCoin(Shells[3] + new Vector3(0f, ShellHeight[3] + 2f, 0f));
        AddCoin(Shells[4] + new Vector3(0f, ShellHeight[4] + 2f, 0f));

        // Батуты-медузы на малых панцирях
        AddBouncePad(Shells[1] + new Vector3(4f, ShellHeight[1] + 0.5f, 0f), 19f);
        AddBouncePad(Shells[4] + new Vector3(-4f, ShellHeight[4] + 0.5f, 0f), 19f);
        AddCoin(Shells[1] + new Vector3(4f, ShellHeight[1] + 5f, 0f));
        AddCoin(Shells[4] + new Vector3(-4f, ShellHeight[4] + 5f, 0f));

        // Крабы на панцирях
        for (int i = 1; i < Shells.Length; i++)
        {
            Vector3 c = Shells[i];
            float h = ShellHeight[i] + 0.6f;
            AddEnemy(c + new Vector3(-5f, h, 0f), c + new Vector3(5f, h, 0f), "crab", 2.6f);
        }
        AddEnemy(Shells[0] + new Vector3(-10f, ShellHeight[0] + 0.6f, 8f),
                 Shells[0] + new Vector3(10f, ShellHeight[0] + 0.6f, 8f), "crab", 3f);
    }

    // ---------- Берег ----------

    private void BuildShore()
    {
        for (int i = 0; i < 30; i++)
        {
            float a = i * 137.5f * Mathf.Deg2Rad;
            float r = 78f + (i % 5) * 8f;
            Vector3 p = OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (p.y < 0.2f) continue;
            Tree(p, new Color(0.3f, 0.6f, 0.34f), 0.9f + (i % 4) * 0.2f);
        }

        for (int i = 0; i < 22; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(72f, 108f);
            Vector3 p = OnGround(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (p.y < 0.1f) continue;
            Rock(p + new Vector3(0f, 0.3f, 0f), Random.Range(1.4f, 3.8f),
                new Color(0.66f, 0.62f, 0.56f));
            if (i % 3 == 0) AddCoin(p + new Vector3(0f, 1.6f, 0f));
        }

        // Причал с лодкой у берега
        Dock(new Vector3(0f, 0.6f, 76f), 10f, 0f);
        AddCoin(new Vector3(0f, 1.6f, 80f));
        AddEnemy(OnGround(-16f, 74f), OnGround(16f, 74f), "crab", 2.4f);
    }

    private void BuildAtmosphere()
    {
        Cloud(new Vector3(-46f, 40f, -34f), 2.8f, Color.white);
        Cloud(new Vector3(38f, 46f, -52f), 3.2f, Color.white);
        Cloud(new Vector3(60f, 38f, 26f), 2.4f, Color.white);
        Cloud(new Vector3(-28f, 44f, 46f), 2.6f, Color.white);

        Color mnt = new Color(0.5f, 0.6f, 0.66f);
        Mountain(new Vector3(-160f, -10f, -110f), 54f, 62f, mnt, false);
        Mountain(new Vector3(-20f, -10f, -170f), 62f, 74f, mnt * 0.94f, false);
        Mountain(new Vector3(150f, -10f, -120f), 48f, 56f, mnt * 1.04f, false);
        Mountain(new Vector3(170f, -10f, 40f), 52f, 60f, mnt, false);
        Mountain(new Vector3(-158f, -10f, 76f), 50f, 54f, mnt * 0.96f, false);

        Motes(new Vector3(0f, 6f, 0f), new Vector3(60f, 6f, 60f), 110,
            new Color(1f, 0.96f, 0.7f, 0.6f), 0.15f);
        Leaves(new Vector3(0f, 16f, 0f), new Vector3(30f, 3f, 30f), 40,
            new Color(0.95f, 0.85f, 0.5f));
    }
}
