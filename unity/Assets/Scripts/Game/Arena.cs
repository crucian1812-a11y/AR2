using UnityEngine;

// Серый бокс: татами, свет и камера. Всё строится кодом — в репозитории
// нет ни сцен, ни префабов (см. docs/bjj/FINDINGS.md §2).
//
// Бойцы здесь — капсулы. Это сознательно: ядро должно играться до того,
// как появятся модели, иначе анимации начнут диктовать правила.
public static class Arena
{
    public static readonly Color MatInner = new Color(0.15f, 0.35f, 0.55f);
    public static readonly Color MatOuter = new Color(0.75f, 0.32f, 0.16f);
    public static readonly Color GiBlue = new Color(0.18f, 0.34f, 0.72f);
    public static readonly Color GiRed = new Color(0.72f, 0.18f, 0.2f);

    public static Material Lit(Color c, float smoothness = 0.1f)
    {
        // Свой шейдер, а не URP/Lit: он принудительно включён в сборку,
        // иначе Unity вырежет его как неиспользуемый — материалы-то
        // создаются в рантайме, ссылок из сцены на них нет.
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

        // Татами: внутренний квадрат боевой зоны и красная кайма вокруг —
        // силуэт узнаваем с первого кадра и задаёт масштаб сцены.
        Slab(root.transform, "MatOuter", new Vector3(0f, -0.06f, 0f),
             new Vector3(12f, 0.1f, 12f), MatOuter);
        Slab(root.transform, "MatInner", new Vector3(0f, 0f, 0f),
             new Vector3(8f, 0.1f, 8f), MatInner);

        // Зал вокруг — тёмная коробка. Нужна не для красоты: без неё
        // камера смотрит в пустой skybox и масштаб не читается.
        Slab(root.transform, "Floor", new Vector3(0f, -0.2f, 0f),
             new Vector3(40f, 0.2f, 40f), new Color(0.09f, 0.09f, 0.11f));

        BuildLight(root.transform);
        return root;
    }

    private static void Slab(Transform parent, string name, Vector3 pos, Vector3 size, Color c)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Lit(c);
        return;
    }

    // Один ключевой источник с мягкой тенью плюс заливка из ambient.
    // Спортзал — замкнутая сцена, этого достаточно, а теней от второго
    // источника на мобиле мы себе позволить не можем.
    private static void BuildLight(Transform parent)
    {
        GameObject go = new GameObject("KeyLight");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.96f, 0.9f);
        l.intensity = 1.6f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.75f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.28f, 0.31f, 0.38f);
        RenderSettings.ambientEquatorColor = new Color(0.2f, 0.21f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.11f);
    }
}
