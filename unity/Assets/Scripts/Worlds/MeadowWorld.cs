using UnityEngine;

// Солнечные луга — платформинг, монеты, грибы-вредины, пруд с камнями.
public class MeadowWorld : WorldBuilder
{
    private static readonly Color Grass = new Color(0.42f, 0.72f, 0.28f);
    private static readonly Color Plat = new Color(0.52f, 0.78f, 0.33f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 1.5f, 24f);

        SetupSky(new Color(0.33f, 0.6f, 0.98f), new Color(0.88f, 0.92f, 0.8f),
            new Color(0.3f, 0.4f, 0.25f), new Vector3(55f, 20f, 0f), 1.25f, 0f, Color.white);

        // Поле
        Ground(new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 90f), Grass, 8f, 0.35f);
        Ground(new Vector3(0f, -3f, 0f), new Vector3(80f, 4f, 80f), new Color(0.45f, 0.32f, 0.2f), 10f, 0.6f);

        GrassField(new Vector3(14f, 0.03f, 0f), new Vector2(26f, 38f), 4200,
            new Color(0.22f, 0.5f, 0.12f), new Color(0.62f, 0.9f, 0.3f));
        GrassField(new Vector3(-24f, 0.03f, 20f), new Vector2(14f, 14f), 1200,
            new Color(0.22f, 0.5f, 0.12f), new Color(0.62f, 0.9f, 0.3f));
        GrassField(new Vector3(24f, 9.53f, -42f), new Vector2(5f, 5f), 350,
            new Color(0.22f, 0.5f, 0.12f), new Color(0.62f, 0.9f, 0.3f));

        Cloud(new Vector3(-40f, 28f, -25f), 2.4f, Color.white);
        Cloud(new Vector3(20f, 34f, -45f), 3f, Color.white);
        Cloud(new Vector3(45f, 26f, 20f), 2f, Color.white);
        Cloud(new Vector3(-20f, 30f, 35f), 2.2f, Color.white);
        Cloud(new Vector3(0f, 36f, 0f), 2.6f, Color.white);

        Color mnt = new Color(0.42f, 0.55f, 0.62f);
        Mountain(new Vector3(-110f, -4f, -80f), 42f, 52f, mnt, true);
        Mountain(new Vector3(0f, -4f, -125f), 48f, 62f, mnt * 0.95f, true);
        Mountain(new Vector3(100f, -4f, -90f), 36f, 46f, mnt * 1.04f, true);
        Mountain(new Vector3(125f, -4f, 30f), 40f, 50f, mnt, true);
        Mountain(new Vector3(-120f, -4f, 55f), 38f, 44f, mnt * 0.97f, false);

        Motes(new Vector3(0f, 3f, 0f), new Vector3(30f, 2.5f, 30f), 70,
            new Color(1f, 0.98f, 0.7f, 0.65f), 0.13f);
        Leaves(new Vector3(26f, 16f, -44f), new Vector3(6f, 1f, 6f), 24, new Color(0.55f, 0.75f, 0.3f));
        Leaves(new Vector3(-14f, 8f, 20f), new Vector3(5f, 1f, 5f), 16, new Color(0.5f, 0.7f, 0.28f));
        Leaves(new Vector3(22f, 7f, -6f), new Vector3(4f, 1f, 4f), 14, new Color(0.9f, 0.6f, 0.3f));

        // Холмы-декор
        GameObject h1 = Gfx.Ball(transform, new Vector3(-34f, -2f, -30f), new Vector3(30f, 12f, 26f),
            Gfx.Mat(Grass * 0.92f));
        Gfx.NoShadow(h1);
        GameObject h2 = Gfx.Ball(transform, new Vector3(36f, -3f, -24f), new Vector3(26f, 14f, 24f),
            Gfx.Mat(Grass * 0.88f));
        Gfx.NoShadow(h2);
        GameObject h3 = Gfx.Ball(transform, new Vector3(30f, -4f, 32f), new Vector3(28f, 12f, 22f),
            Gfx.Mat(Grass * 0.95f));
        Gfx.NoShadow(h3);

        // Портал домой
        AddPortal(new Vector3(0f, 1.4f, 32f), 0, "В деревню", new Color(1f, 0.8f, 0.4f), 0f);

        // Монеты и грибы на поле
        AddCoin(new Vector3(4f, 1f, 16f));
        AddCoin(new Vector3(-5f, 1f, 12f));
        AddCoin(new Vector3(8f, 1f, 8f));
        AddCoin(new Vector3(-9f, 1f, 4f));
        AddCoin(new Vector3(2f, 1f, 0f));
        AddCoin(new Vector3(-3f, 1f, -6f));
        AddCoin(new Vector3(7f, 1f, -10f));
        AddCoin(new Vector3(-8f, 1f, -14f));
        AddEnemy(new Vector3(-6f, 0f, 8f), new Vector3(6f, 0f, 8f), "mushroom", 2.2f);
        AddEnemy(new Vector3(5f, 0f, -2f), new Vector3(-5f, 0f, -6f), "mushroom", 2.5f);

        // Пруд с камнями-ступеньками
        Ground(new Vector3(-20f, -0.4f, -8f), new Vector3(16f, 0.8f, 20f),
            new Color(0.5f, 0.42f, 0.3f), 5f, 0.5f);
        Water(new Vector3(-20f, 0.15f, -8f), new Vector2(15f, 19f));

        Color stone = new Color(0.62f, 0.6f, 0.55f);
        Vector3[] steps = {
            new Vector3(-15f, 0.3f, -2f), new Vector3(-18f, 0.3f, -6f),
            new Vector3(-21f, 0.3f, -10f), new Vector3(-24f, 0.3f, -13f)
        };
        for (int i = 0; i < steps.Length; i++)
        {
            Platform(steps[i], new Vector3(2f, 0.7f, 2f), stone);
            AddCoin(steps[i] + new Vector3(0f, 1.1f, 0f));
        }

        // Парящие платформы наверх
        Vector3[] rise = {
            new Vector3(6f, 1.5f, -18f), new Vector3(10f, 3f, -22f), new Vector3(14f, 4.5f, -26f),
            new Vector3(10f, 6f, -30f), new Vector3(14f, 7.5f, -34f), new Vector3(18f, 8.5f, -38f)
        };
        for (int i = 0; i < rise.Length; i++)
        {
            Platform(rise[i], new Vector3(3.2f, 0.6f, 3.2f), Plat);
            AddCoin(rise[i] + new Vector3(0f, 1.2f, 0f));
        }

        // Плато с большим деревом
        Platform(new Vector3(24f, 9f, -42f), new Vector3(12f, 1f, 12f), Grass * 1.05f);
        Tree(new Vector3(26f, 9.5f, -44f), new Color(0.3f, 0.6f, 0.25f), 1.8f);
        AddEnemy(new Vector3(20f, 9.7f, -40f), new Vector3(28f, 9.7f, -40f), "mushroom", 2f);
        AddCoin(new Vector3(22f, 10.6f, -40f));
        AddCoin(new Vector3(26f, 10.6f, -40f));
        AddCoin(new Vector3(24f, 10.6f, -46f));
        AddCoin(new Vector3(20f, 10.6f, -46f));

        // Растительность
        Tree(new Vector3(16f, 0f, 14f), new Color(0.25f, 0.6f, 0.25f), 1f);
        Tree(new Vector3(-14f, 0f, 20f), new Color(0.32f, 0.55f, 0.2f), 1.2f);
        Tree(new Vector3(22f, 0f, -6f), new Color(0.85f, 0.55f, 0.3f), 0.9f);
        Tree(new Vector3(-26f, 0f, 12f), new Color(0.25f, 0.55f, 0.3f), 1f);
        Rock(new Vector3(12f, 0.3f, 2f), 1.5f, new Color(0.55f, 0.55f, 0.58f));
        Rock(new Vector3(-2f, 0.25f, 22f), 1.1f, new Color(0.55f, 0.55f, 0.58f));
        Bush(new Vector3(10f, 0f, 18f), new Color(0.22f, 0.48f, 0.2f));
        Bush(new Vector3(-10f, 0f, -20f), new Color(0.25f, 0.5f, 0.18f));

        Color[] flowers = {
            new Color(0.95f, 0.4f, 0.45f), new Color(0.95f, 0.85f, 0.3f),
            new Color(0.6f, 0.5f, 0.95f), new Color(1f, 0.65f, 0.3f)
        };
        for (int i = 0; i < 26; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(4f, 34f);
            Vector3 p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
            if (p.x > -12f || p.z > 4f) Flower(p, flowers[i % 4]);
        }
    }
}
