using UnityEngine;

// Снежные вершины — восхождение по спиральным уступам к золотой звезде.
public class SnowWorld : WorldBuilder
{
    private static readonly Color Snow = new Color(0.93f, 0.95f, 1f);
    private static readonly Color Ice = new Color(0.7f, 0.85f, 1f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 1.5f, 22f);

        SetupSky(new Color(0.45f, 0.55f, 0.75f), new Color(0.82f, 0.87f, 0.95f),
            new Color(0.5f, 0.55f, 0.65f), new Vector3(25f, 60f, 0f), 1f,
            0.012f, new Color(0.82f, 0.87f, 0.95f));

        // Долина
        Ground(new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), Snow, 7f, 0.5f, 0.25f);
        Ground(new Vector3(0f, -3f, 0f), new Vector3(70f, 4f, 70f), new Color(0.6f, 0.65f, 0.75f), 9f, 0.7f);

        Snowfall(new Vector3(0f, 22f, 0f), new Vector3(40f, 2f, 40f), 400);

        Cloud(new Vector3(-30f, 30f, -25f), 2.4f, new Color(0.92f, 0.94f, 1f));
        Cloud(new Vector3(25f, 34f, -40f), 2.8f, new Color(0.9f, 0.92f, 0.98f));
        Cloud(new Vector3(40f, 28f, 15f), 2f, new Color(0.92f, 0.94f, 1f));
        Cloud(new Vector3(-18f, 36f, 30f), 2.3f, new Color(0.9f, 0.92f, 0.98f));

        Color mnt = new Color(0.62f, 0.68f, 0.8f);
        Mountain(new Vector3(-75f, -4f, -60f), 30f, 44f, mnt, true);
        Mountain(new Vector3(-15f, -4f, -90f), 38f, 56f, mnt * 0.95f, true);
        Mountain(new Vector3(55f, -4f, -75f), 28f, 40f, mnt, true);
        Mountain(new Vector3(85f, -4f, -5f), 32f, 46f, mnt * 1.04f, true);
        Mountain(new Vector3(70f, -4f, 60f), 26f, 36f, mnt, true);
        Mountain(new Vector3(-80f, -4f, 45f), 30f, 42f, mnt * 0.96f, true);

        // Портал домой
        AddPortal(new Vector3(0f, 1.4f, 30f), 0, "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        // Центральный пик
        Vector3 peak = new Vector3(0f, 0f, -18f);
        Gfx.Cyl(transform, peak + new Vector3(0f, 8f, 0f), new Vector3(10f, 8f, 10f),
            Gfx.MatFull(new Color(0.75f, 0.78f, 0.88f), 0.05f, 0f, Color.black, 3f, 0.6f));
        Gfx.Cyl(transform, peak + new Vector3(0f, 18.5f, 0f), new Vector3(6.4f, 2.5f, 6.4f),
            Gfx.Mat(Snow, 0.2f));
        GameObject tip = Gfx.Cone(transform, peak + new Vector3(0f, 21f, 0f), 3.4f, 4f,
            Gfx.Mat(Snow, 0.2f));
        Gfx.NoShadow(tip);

        // Спиральные уступы
        const int ledges = 14;
        for (int i = 0; i < ledges; i++)
        {
            float ang = 0.85f * i + Mathf.PI * 0.5f;
            const float r = 7.2f;
            Vector3 pos = peak + new Vector3(Mathf.Cos(ang) * r, 1.2f + i * 1.55f, Mathf.Sin(ang) * r);
            bool icy = i % 3 == 2;
            GameObject slab = Platform(pos, new Vector3(3.6f, 0.5f, 3.6f),
                icy ? Ice : Snow, icy ? 0.85f : 0.25f, icy ? 0.3f : 0f);
            slab.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
            if (i % 2 == 0) AddCoin(pos + new Vector3(0f, 1.2f, 0f));
        }

        // Слизни в долине и на уступах
        AddEnemy(new Vector3(-6f, 0.5f, 6f), new Vector3(6f, 0.5f, 6f), "slime", 2.2f);
        AddEnemy(new Vector3(-8f, 0.5f, -4f), new Vector3(-2f, 0.5f, -8f), "slime", 2.6f);

        float a4 = 0.85f * 4f + Mathf.PI * 0.5f;
        Vector3 p4 = peak + new Vector3(Mathf.Cos(a4) * 7.2f, 1.2f + 4 * 1.55f + 0.5f, Mathf.Sin(a4) * 7.2f);
        AddEnemy(p4 + new Vector3(-1.2f, 0f, 0f), p4 + new Vector3(1.2f, 0f, 0f), "slime", 1.5f);

        float a9 = 0.85f * 9f + Mathf.PI * 0.5f;
        Vector3 p9 = peak + new Vector3(Mathf.Cos(a9) * 7.2f, 1.2f + 9 * 1.55f + 0.5f, Mathf.Sin(a9) * 7.2f);
        AddEnemy(p9 + new Vector3(-1.2f, 0f, 0f), p9 + new Vector3(1.2f, 0f, 0f), "slime", 1.8f);

        // Вершина со звездой
        Vector3 top = peak + new Vector3(0f, 1.2f + ledges * 1.55f + 0.8f, 0f);
        Platform(top, new Vector3(6f, 0.6f, 6f), new Color(0.85f, 0.9f, 1f), 0.35f);
        AddStar(top + new Vector3(0f, 1.9f, 0f));

        // Монеты в долине
        AddCoin(new Vector3(8f, 1f, 12f));
        AddCoin(new Vector3(-10f, 1f, 8f));
        AddCoin(new Vector3(12f, 1f, -2f));
        AddCoin(new Vector3(-6f, 1f, 18f));
        AddCoin(new Vector3(14f, 1f, 20f));
        AddCoin(new Vector3(-16f, 1f, -6f));
        AddCoin(new Vector3(6f, 1f, -10f));
        AddCoin(new Vector3(-12f, 1f, 16f));

        // Ели и камни
        Pine(new Vector3(-18f, 0f, 10f), true, 1f);
        Pine(new Vector3(16f, 0f, 8f), true, 1.3f);
        Pine(new Vector3(-14f, 0f, -14f), true, 0.9f);
        Pine(new Vector3(20f, 0f, -10f), true, 1.1f);
        Pine(new Vector3(10f, 0f, 24f), true, 0.8f);
        Pine(new Vector3(-22f, 0f, 22f), true, 1.2f);

        Rock(new Vector3(-8f, 0.3f, 2f), 1.8f, new Color(0.65f, 0.68f, 0.75f));
        Rock(new Vector3(10f, 0.25f, 4f), 1.3f, new Color(0.6f, 0.62f, 0.7f));
        Rock(new Vector3(18f, 0.3f, 14f), 1.5f, new Color(0.68f, 0.7f, 0.78f));
    }
}
