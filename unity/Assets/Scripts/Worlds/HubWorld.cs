using UnityEngine;

// Медвежья деревня — стартовый мир: старейшина, фонтан, домики, порталы.
public class HubWorld : WorldBuilder
{
    private static readonly Color Grass = new Color(0.34f, 0.66f, 0.27f);
    private static readonly Color PathCol = new Color(0.82f, 0.7f, 0.48f);

    protected override void Build()
    {
        SpawnPoint = new Vector3(0f, 1.5f, 10f);

        SetupSky(new Color(0.28f, 0.55f, 0.95f), new Color(0.78f, 0.88f, 0.98f),
            new Color(0.25f, 0.32f, 0.25f), new Vector3(48f, 35f, 0f), 1.15f, 0f, Color.white);

        // Остров
        Ground(new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f), Grass, 6f, 0.35f);
        Ground(new Vector3(0f, -2.5f, 0f), new Vector3(52f, 3f, 52f), new Color(0.45f, 0.32f, 0.2f), 8f, 0.6f);
        Ground(new Vector3(0f, -5f, 0f), new Vector3(40f, 2f, 40f), new Color(0.38f, 0.27f, 0.17f), 8f, 0.6f);

        // Дорожки
        Ground(new Vector3(0f, 0.03f, 5f), new Vector3(3f, 0.1f, 20f), PathCol, 4f, 0.4f);
        Ground(new Vector3(-8f, 0.03f, 0f), new Vector3(14f, 0.1f, 3f), PathCol, 4f, 0.4f);
        Ground(new Vector3(8f, 0.03f, 0f), new Vector3(14f, 0.1f, 3f), PathCol, 4f, 0.4f);

        // Трава, облака, горы, пыльца
        GrassField(new Vector3(0f, 0.03f, 0f), new Vector2(27f, 27f), 2600,
            new Color(0.2f, 0.45f, 0.14f), new Color(0.55f, 0.85f, 0.3f));

        Cloud(new Vector3(-30f, 26f, -20f), 2f, Color.white);
        Cloud(new Vector3(15f, 30f, -35f), 2.6f, Color.white);
        Cloud(new Vector3(40f, 24f, 10f), 1.8f, Color.white);
        Cloud(new Vector3(-15f, 32f, 30f), 2.2f, Color.white);
        Cloud(new Vector3(55f, 28f, -8f), 2.4f, Color.white);

        Color mnt = new Color(0.45f, 0.52f, 0.68f);
        Mountain(new Vector3(-90f, -4f, -70f), 34f, 46f, mnt, true);
        Mountain(new Vector3(-30f, -4f, -105f), 40f, 55f, mnt * 0.94f, true);
        Mountain(new Vector3(60f, -4f, -95f), 30f, 40f, mnt, true);
        Mountain(new Vector3(105f, -4f, -20f), 36f, 48f, mnt * 1.05f, true);
        Mountain(new Vector3(95f, -4f, 70f), 28f, 36f, mnt, false);
        Mountain(new Vector3(-100f, -4f, 45f), 32f, 42f, mnt * 0.96f, true);

        Motes(new Vector3(0f, 3f, 0f), new Vector3(24f, 2.5f, 24f), 60,
            new Color(1f, 0.95f, 0.6f, 0.7f), 0.13f);

        // Фонтан
        Gfx.Cyl(transform, new Vector3(0f, 0.4f, 0f), new Vector3(5.2f, 0.4f, 5.2f),
            Gfx.Mat(new Color(0.7f, 0.7f, 0.75f), 0.3f));
        Water(new Vector3(0f, 0.85f, 0f), new Vector2(4.4f, 4.4f));
        Gfx.Cyl(transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 0.8f, 0.7f),
            Gfx.Mat(new Color(0.65f, 0.65f, 0.7f), 0.3f));
        Fountain(new Vector3(0f, 2.4f, 0f), 40);

        // Домики
        House(new Vector3(-10f, 0f, -12f), new Color(0.85f, 0.6f, 0.4f), 20f);
        House(new Vector3(10f, 0f, -12f), new Color(0.6f, 0.7f, 0.9f), -20f);
        House(new Vector3(-16f, 0f, 8f), new Color(0.9f, 0.8f, 0.5f), 70f);

        // Озеро
        Ground(new Vector3(18f, -0.45f, 16f), new Vector3(14f, 0.9f, 12f),
            new Color(0.5f, 0.42f, 0.3f), 5f, 0.5f);
        Water(new Vector3(18f, 0.1f, 16f), new Vector2(13f, 11f));

        // Деревья, камни, кусты
        Tree(new Vector3(-22f, 0f, -18f), new Color(0.25f, 0.6f, 0.25f), 1f);
        Tree(new Vector3(22f, 0f, -20f), new Color(0.32f, 0.55f, 0.2f), 1.2f);
        Tree(new Vector3(-24f, 0f, 14f), new Color(0.2f, 0.5f, 0.28f), 1f);
        Tree(new Vector3(24f, 0f, 2f), new Color(0.3f, 0.62f, 0.25f), 0.9f);
        Tree(new Vector3(-6f, 0f, -22f), new Color(0.85f, 0.5f, 0.3f), 1f);
        Tree(new Vector3(4f, 0f, 20f), new Color(0.28f, 0.58f, 0.25f), 1.1f);

        Rock(new Vector3(-14f, 0.3f, -4f), 1.6f, new Color(0.55f, 0.55f, 0.58f));
        Rock(new Vector3(13f, 0.25f, 7f), 1.2f, new Color(0.55f, 0.55f, 0.58f));
        Rock(new Vector3(-4f, 0.2f, 16f), 1f, new Color(0.55f, 0.55f, 0.58f));

        Bush(new Vector3(-13.2f, 0f, -10f), new Color(0.2f, 0.45f, 0.2f));
        Bush(new Vector3(-6.8f, 0f, -13.5f), new Color(0.25f, 0.5f, 0.18f));
        Bush(new Vector3(13.4f, 0f, -9.6f), new Color(0.2f, 0.45f, 0.2f));
        Bush(new Vector3(-18.5f, 0f, 5.2f), new Color(0.18f, 0.42f, 0.22f));
        Bush(new Vector3(7f, 0f, 18f), new Color(0.2f, 0.45f, 0.2f));

        Color[] flowers = {
            new Color(0.95f, 0.4f, 0.45f), new Color(0.95f, 0.85f, 0.3f),
            new Color(0.6f, 0.5f, 0.95f), new Color(1f, 0.65f, 0.3f)
        };
        for (int i = 0; i < 18; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(5f, 24f);
            Flower(new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r), flowers[i % 4]);
        }

        // Монеты
        AddCoin(new Vector3(0f, 1f, 5f));
        AddCoin(new Vector3(6f, 1f, 4f));
        AddCoin(new Vector3(-6f, 1f, 4f));
        AddCoin(new Vector3(18f, 1.2f, 10f));
        AddCoin(new Vector3(-18f, 1f, -2f));
        AddCoin(new Vector3(0f, 1f, -18f));

        // Старейшина и порталы
        AddNpc(new Vector3(3.5f, 0f, -3.5f));
        AddPortal(new Vector3(-14f, 1.4f, 0f), 1, "Солнечные луга", new Color(0.4f, 1f, 0.5f), 90f);
        AddPortal(new Vector3(14f, 1.4f, 0f), 2, "Снежные вершины", new Color(0.5f, 0.8f, 1f), 90f);
    }

    private void House(Vector3 pos, Color wall, float yaw)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        GameObject body = Gfx.Box(transform, pos + new Vector3(0f, 1.5f, 0f),
            new Vector3(5f, 3f, 4.4f), Gfx.MatFull(wall, 0.08f, 0f, Color.black, 2f, 0.25f));
        body.transform.localRotation = rot;

        GameObject roof = Gfx.Cone(transform, pos + new Vector3(0f, 3f, 0f), 3.9f, 2.2f,
            Gfx.Mat(new Color(0.6f, 0.25f, 0.2f)));
        roof.transform.localRotation = Quaternion.Euler(0f, yaw + 45f, 0f);

        GameObject door = Gfx.Box(transform, pos + rot * new Vector3(0f, 0.9f, 2.25f),
            new Vector3(1.1f, 1.8f, 0.15f), Gfx.Mat(new Color(0.35f, 0.22f, 0.12f)));
        door.transform.localRotation = rot;

        // Тёплые светящиеся окна
        Material win = Gfx.MatFull(new Color(1f, 0.9f, 0.55f), 0.4f, 0f,
            new Color(1f, 0.78f, 0.35f), 0f, 0f);
        float[] offsets = { -1.55f, 1.55f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 wp = pos + rot * new Vector3(offsets[i], 1.7f, 2.24f);
            GameObject w = Gfx.Box(transform, wp, new Vector3(0.9f, 0.9f, 0.12f), win, false);
            w.transform.localRotation = rot;
            Gfx.Glow(transform, wp + rot * new Vector3(0f, 0f, 0.3f), 2.2f,
                new Color(1f, 0.8f, 0.4f, 0.35f));
        }
    }
}
