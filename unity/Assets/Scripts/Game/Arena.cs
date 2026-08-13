using UnityEngine;

// Зал: татами, трибуны, свет. Всё строится кодом — ни сцен, ни префабов.
//
// Схватка идёт на пятачке два на два метра, поэтому всё, что дальше, —
// это фон, и его задача одна: дать глубину и масштаб. Отсюда решения
// ниже: тёмный зал против светлого татами, ряды зрителей силуэтами,
// туман, гасящий дальний план. Ни один из этих элементов не стоит
// дорого, а вместе они отличают «сцену» от «двух моделей в пустоте».
public static class Arena
{
    public static readonly Color MatInner = new Color(0.10f, 0.30f, 0.48f);
    public static readonly Color MatOuter = new Color(0.62f, 0.26f, 0.13f);
    public static readonly Color GiBlue = new Color(0.13f, 0.26f, 0.68f);
    public static readonly Color GiRed = new Color(0.62f, 0.13f, 0.16f);

    // Контровой цвет на каждого бойца. В партере два тела сливаются в
    // клубок, и подсветка контура — самое дешёвое, что их разделяет.
    public static readonly Color RimBlue = new Color(0.45f, 0.68f, 1.0f);
    public static readonly Color RimRed = new Color(1.0f, 0.52f, 0.42f);

    public static Material Lit(Color c, float smoothness = 0.1f)
    {
        Shader s = Shader.Find("Bjj/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(s);
        m.SetColor("_Color", c);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    public static GameObject Build(Transform parent)
    {
        GameObject root = new GameObject("Arena");
        root.transform.SetParent(parent, false);

        BuildMat(root.transform);
        BuildHall(root.transform);
        BuildCrowd(root.transform);
        BuildLights(root.transform);
        BuildAtmosphere();

        return root;
    }

    // ------------------------------------------------------------ татами

    private static void BuildMat(Transform parent)
    {
        // Кайма и боевая зона разного цвета — так читается площадка, а не
        // просто пол. Толщина настоящая: у мата видно торец, и это сразу
        // даёт масштаб.
        Slab(parent, "MatOuter", new Vector3(0f, -0.03f, 0f),
             new Vector3(11f, 0.06f, 11f), MatOuter, 0.22f);
        Slab(parent, "MatInner", new Vector3(0f, 0.001f, 0f),
             new Vector3(7.4f, 0.062f, 7.4f), MatInner, 0.22f);

        // Швы между матами: тонкие тёмные полосы. Ровная заливка размером
        // семь метров выглядит пластиковым листом.
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            float p = i * 1.05f;
            Slab(parent, "SeamX", new Vector3(p, 0.033f, 0f),
                 new Vector3(0.02f, 0.002f, 7.4f), MatInner * 0.72f, 0.2f);
            Slab(parent, "SeamZ", new Vector3(0f, 0.033f, p),
                 new Vector3(7.4f, 0.002f, 0.02f), MatInner * 0.72f, 0.2f);
        }

        // Стартовые отметки в центре.
        Slab(parent, "MarkA", new Vector3(0f, 0.034f, 0.72f),
             new Vector3(0.9f, 0.002f, 0.06f), new Color(0.85f, 0.86f, 0.88f), 0.15f);
        Slab(parent, "MarkB", new Vector3(0f, 0.034f, -0.72f),
             new Vector3(0.9f, 0.002f, 0.06f), new Color(0.85f, 0.86f, 0.88f), 0.15f);
    }

    // -------------------------------------------------------------- зал

    private static void BuildHall(Transform parent)
    {
        Color floor = new Color(0.055f, 0.058f, 0.07f);
        Slab(parent, "Floor", new Vector3(0f, -0.09f, 0f),
             new Vector3(46f, 0.12f, 46f), floor, 0.3f);

        Color wall = new Color(0.075f, 0.08f, 0.095f);
        float d = 17f;
        Slab(parent, "WallN", new Vector3(0f, 4.2f, d), new Vector3(46f, 9f, 0.5f), wall, 0.12f);
        Slab(parent, "WallS", new Vector3(0f, 4.2f, -d), new Vector3(46f, 9f, 0.5f), wall, 0.12f);
        Slab(parent, "WallE", new Vector3(d, 4.2f, 0f), new Vector3(0.5f, 9f, 46f), wall, 0.12f);
        Slab(parent, "WallW", new Vector3(-d, 4.2f, 0f), new Vector3(0.5f, 9f, 46f), wall, 0.12f);

        Slab(parent, "Ceiling", new Vector3(0f, 8.6f, 0f),
             new Vector3(46f, 0.4f, 46f), new Color(0.04f, 0.042f, 0.05f), 0.1f);

        // Баннеры по стенам: единственное цветное пятно на дальнем плане.
        Color[] banner =
        {
            new Color(0.55f, 0.12f, 0.14f),
            new Color(0.10f, 0.28f, 0.52f),
            new Color(0.62f, 0.50f, 0.12f)
        };
        for (int i = 0; i < 6; i++)
        {
            float x = -12f + i * 4.8f;
            Slab(parent, "Banner", new Vector3(x, 3.4f, d - 0.35f),
                 new Vector3(3.4f, 1.5f, 0.06f), banner[i % banner.Length], 0.2f);
        }
    }

    // ---------------------------------------------------------- зрители

    // Зрители — силуэты: тёмные капсулы рядами на подиуме. Лиц не видно и
    // не нужно, а ряды тел за краем татами дают ощущение зала. Материал
    // один на всех, поэтому SRP Batcher сводит их в считанные вызовы.
    private static void BuildCrowd(Transform parent)
    {
        GameObject holder = new GameObject("Crowd");
        holder.transform.SetParent(parent, false);

        Material body = Lit(new Color(0.045f, 0.05f, 0.065f), 0.05f);
        Material stand = Lit(new Color(0.035f, 0.038f, 0.048f), 0.1f);

        for (int row = 0; row < 3; row++)
        {
            float dist = 7.6f + row * 1.5f;
            float height = 0.25f + row * 0.55f;

            for (int side = 0; side < 4; side++)
            {
                // Подиум ряда.
                Vector3 pos, size;
                if (side < 2)
                {
                    float z = side == 0 ? dist : -dist;
                    pos = new Vector3(0f, height * 0.5f, z);
                    size = new Vector3(24f, height, 1.4f);
                }
                else
                {
                    float x = side == 2 ? dist : -dist;
                    pos = new Vector3(x, height * 0.5f, 0f);
                    size = new Vector3(1.4f, height, 24f);
                }

                GameObject podium = GameObject.CreatePrimitive(PrimitiveType.Cube);
                podium.name = "Podium";
                podium.transform.SetParent(holder.transform, false);
                podium.transform.localPosition = pos;
                podium.transform.localScale = size;
                podium.GetComponent<Renderer>().sharedMaterial = stand;
                Object.Destroy(podium.GetComponent<Collider>());

                for (int i = 0; i < 16; i++)
                {
                    float t = (i - 7.5f) * 1.32f;
                    Vector3 p = side < 2
                        ? new Vector3(t, height + 0.42f, side == 0 ? dist : -dist)
                        : new Vector3(side == 2 ? dist : -dist, height + 0.42f, t);

                    // Небольшой разброс, иначе ряд выглядит забором.
                    float jitter = Mathf.Sin(i * 12.9898f + row * 78.233f) * 0.12f;
                    p += new Vector3(jitter, 0f, jitter * 0.6f);

                    GameObject person = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    person.name = "Fan";
                    person.transform.SetParent(holder.transform, false);
                    person.transform.localPosition = p;
                    person.transform.localScale = new Vector3(0.42f, 0.44f, 0.42f);
                    person.GetComponent<Renderer>().sharedMaterial = body;
                    person.GetComponent<Renderer>().shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    Object.Destroy(person.GetComponent<Collider>());
                }
            }
        }
    }

    // ------------------------------------------------------------- свет

    private static void BuildLights(Transform parent)
    {
        // Один ключевой источник с тенью. Второй теневой источник на
        // мобиле стоит дороже, чем даёт: тень от него всё равно почти не
        // читается на светлом татами.
        GameObject key = new GameObject("KeyLight");
        key.transform.SetParent(parent, false);
        key.transform.rotation = Quaternion.Euler(58f, -38f, 0f);

        Light kl = key.AddComponent<Light>();
        kl.type = LightType.Directional;
        kl.color = new Color(1f, 0.97f, 0.92f);
        kl.intensity = 2.1f;
        kl.shadows = LightShadows.Soft;
        kl.shadowStrength = 0.82f;
        kl.shadowBias = 0.02f;
        kl.shadowNormalBias = 0.3f;

        // Две подсветки-прожектора над татами: они дают блик на плечах и
        // разделяют бойцов от тёмного зала. Теней не отбрасывают.
        AddSpot(parent, new Vector3(2.6f, 6.4f, 2.6f), new Color(0.72f, 0.82f, 1f), 240f);
        AddSpot(parent, new Vector3(-2.6f, 6.4f, -2.6f), new Color(1f, 0.86f, 0.72f), 200f);

        // Светильники под потолком: сами по себе не светят, но глаз
        // ожидает увидеть источник там, откуда падает свет.
        Material lamp = Lit(new Color(1f, 0.97f, 0.9f), 0.9f);
        lamp.SetColor("_EmissionColor", new Color(2.6f, 2.5f, 2.3f));
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                GameObject fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fixture.name = "Fixture";
                fixture.transform.SetParent(parent, false);
                fixture.transform.localPosition = new Vector3(i * 3.4f, 7.9f, j * 3.4f);
                fixture.transform.localScale = new Vector3(2.6f, 0.12f, 0.5f);
                fixture.GetComponent<Renderer>().sharedMaterial = lamp;
                fixture.GetComponent<Renderer>().shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                Object.Destroy(fixture.GetComponent<Collider>());
            }
        }
    }

    private static void AddSpot(Transform parent, Vector3 pos, Color color, float intensity)
    {
        GameObject go = new GameObject("Spot");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.6f, 0f) - pos);

        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = color;
        l.intensity = intensity;
        l.range = 16f;
        l.spotAngle = 62f;
        l.innerSpotAngle = 30f;
        l.shadows = LightShadows.None;
    }

    private static void BuildAtmosphere()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.20f, 0.23f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.13f, 0.14f, 0.17f);
        RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.06f);

        // Туман съедает дальние ряды и стены — глубина появляется даром.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.045f, 0.05f, 0.065f);
        RenderSettings.fogDensity = 0.028f;
    }

    private static void Slab(Transform parent, string name, Vector3 pos, Vector3 size,
                             Color c, float smoothness)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Lit(c, smoothness);
        Object.Destroy(go.GetComponent<Collider>());
    }
}
