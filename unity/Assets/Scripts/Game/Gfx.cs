using System.Collections.Generic;
using UnityEngine;

// Графические утилиты: процедурные текстуры, кэш материалов, примитивы,
// billboard-свечения. Все ассеты создаются кодом, внешних файлов нет.
public static class Gfx
{
    private static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
    private static Texture2D _noiseTex;
    private static Texture2D _noiseNormal;
    private static Texture2D _waterNormal;
    private static Texture2D _glowTex;
    private static Texture2D _circleTex;
    private static Texture2D _ringTex;
    private static Texture2D _whiteTex;
    private static Shader _standard;
    private static Shader _additive;

    public static void ClearCache()
    {
        _matCache.Clear();
    }

    // Все шейдеры игры — собственные (см. Assets/Shaders) и принудительно
    // включены в сборку через BuildScript, поэтому Shader.Find их находит.
    public static Shader Standard
    {
        get
        {
            if (_standard == null)
            {
                _standard = Shader.Find("Bear/Lit");
                if (_standard == null) _standard = Shader.Find("Standard");
                if (_standard == null) _standard = Shader.Find("Diffuse");
            }
            return _standard;
        }
    }

    public static Shader Additive
    {
        get
        {
            if (_additive == null)
            {
                _additive = Shader.Find("Bear/Additive");
                if (_additive == null) _additive = Standard;
            }
            return _additive;
        }
    }

    // ---------- Процедурные текстуры ----------

    private static float Fbm(float x, float y, float freq)
    {
        float v = 0f;
        float amp = 1f;
        float total = 0f;
        for (int o = 0; o < 3; o++)
        {
            v += Mathf.PerlinNoise(x * freq, y * freq) * amp;
            total += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return v / Mathf.Max(total, 0.0001f);
    }

    public static Texture2D NoiseTexture()
    {
        if (_noiseTex != null) return _noiseTex;
        const int n = 128;
        Texture2D t = new Texture2D(n, n);
        t.wrapMode = TextureWrapMode.Repeat;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float v = Fbm(x, y, 0.06f);
                float g = 0.78f + 0.22f * v;
                px[y * n + x] = new Color(g, g, g, 1f);
            }
        }
        t.SetPixels(px);
        t.Apply();
        _noiseTex = t;
        return t;
    }

    private static Texture2D BuildNormal(float freq, float strength)
    {
        const int n = 128;
        Texture2D t = new Texture2D(n, n);
        t.wrapMode = TextureWrapMode.Repeat;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float hl = Fbm(x - 1, y, freq);
                float hr = Fbm(x + 1, y, freq);
                float hd = Fbm(x, y - 1, freq);
                float hu = Fbm(x, y + 1, freq);
                Vector3 nrm = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                // Unity ожидает нормаль в канале A/G для DXT5nm, но для обычной
                // RGBA-текстуры достаточно классической упаковки RGB.
                px[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
        }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    public static Texture2D NoiseNormal()
    {
        if (_noiseNormal == null) _noiseNormal = BuildNormal(0.1f, 4f);
        return _noiseNormal;
    }

    public static Texture2D WaterNormal()
    {
        if (_waterNormal == null) _waterNormal = BuildNormal(0.15f, 2.5f);
        return _waterNormal;
    }

    public static Texture2D GlowTexture()
    {
        if (_glowTex != null) return _glowTex;
        const int n = 64;
        Texture2D t = new Texture2D(n, n);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        _glowTex = t;
        return t;
    }

    public static Texture2D CircleTexture()
    {
        if (_circleTex != null) return _circleTex;
        const int n = 64;
        Texture2D t = new Texture2D(n, n);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - d) * half * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        _circleTex = t;
        return t;
    }

    public static Texture2D RingTexture()
    {
        if (_ringTex != null) return _ringTex;
        const int n = 64;
        Texture2D t = new Texture2D(n, n);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float half = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((1f - d) * half * 0.5f);
                float inner = Mathf.Clamp01((d - 0.78f) * half * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Min(outer, inner));
            }
        }
        t.SetPixels(px);
        t.Apply();
        _ringTex = t;
        return t;
    }

    public static Texture2D WhiteTexture()
    {
        if (_whiteTex != null) return _whiteTex;
        Texture2D t = new Texture2D(4, 4);
        Color[] px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px);
        t.Apply();
        _whiteTex = t;
        return t;
    }

    public static Sprite CircleSprite()
    {
        Texture2D t = CircleTexture();
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
    }

    public static Sprite RingSprite()
    {
        Texture2D t = RingTexture();
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
    }

    public static Sprite WhiteSprite()
    {
        Texture2D t = WhiteTexture();
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
    }

    // ---------- Материалы ----------

    public static Material Mat(Color color, float smoothness = 0.15f, float metallic = 0f)
    {
        return MatFull(color, smoothness, metallic, Color.black, 0f, 0f);
    }

    public static Material Emissive(Color color, Color emission, float smoothness = 0.5f, float metallic = 0f)
    {
        return MatFull(color, smoothness, metallic, emission, 0f, 0f);
    }

    // detailTiling > 0 включает шумовую текстуру, normalScale > 0 — рельеф.
    public static Material MatFull(Color color, float smoothness, float metallic,
        Color emission, float detailTiling, float normalScale)
    {
        string key = color.r + "_" + color.g + "_" + color.b + "_" + color.a + "|" +
                     smoothness + "|" + metallic + "|" +
                     emission.r + "_" + emission.g + "_" + emission.b + "|" +
                     detailTiling + "|" + normalScale;
        Material cached;
        if (_matCache.TryGetValue(key, out cached) && cached != null) return cached;

        Material m = new Material(Standard);
        m.color = color;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        m.SetColor("_EmissionColor", emission);
        if (detailTiling > 0f)
        {
            m.mainTexture = NoiseTexture();
            m.SetTextureScale("_MainTex", new Vector2(detailTiling, detailTiling));
        }
        if (normalScale > 0f)
        {
            m.SetTexture("_BumpMap", NoiseNormal());
            m.SetFloat("_NormalScale", Mathf.Clamp01(normalScale));
            float tile = Mathf.Max(detailTiling, 1f);
            m.SetTextureScale("_BumpMap", new Vector2(tile, tile));
        }
        _matCache[key] = m;
        return m;
    }

    // Материал, который берёт цвет из вершинных цветов меша.
    // Нужен рельефу: он красится по высоте, а базовый цвет остаётся белым.
    public static Material VertexColorMat(float smoothness, float detailTiling, float normalScale)
    {
        Material m = new Material(Standard);
        m.color = Color.white;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetColor("_EmissionColor", Color.black);
        m.SetFloat("_VertexTint", 1f);
        if (detailTiling > 0f)
        {
            m.mainTexture = NoiseTexture();
            m.SetTextureScale("_MainTex", new Vector2(detailTiling, detailTiling));
        }
        if (normalScale > 0f)
        {
            m.SetTexture("_BumpMap", NoiseNormal());
            m.SetFloat("_NormalScale", Mathf.Clamp01(normalScale));
            float tile = Mathf.Max(detailTiling, 1f);
            m.SetTextureScale("_BumpMap", new Vector2(tile, tile));
        }
        return m;
    }

    public static Material AdditiveMat(Color tint, Texture2D tex)
    {
        Material m = new Material(Additive);
        m.color = tint;
        m.mainTexture = tex;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint);
        m.renderQueue = 3000;
        return m;
    }

    // ---------- Примитивы ----------

    public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos,
        Vector3 scale, Material mat, bool collide)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;
        if (!collide)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
        return go;
    }

    public static GameObject Ball(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collide = false)
    {
        return Prim(PrimitiveType.Sphere, parent, pos, scale, mat, collide);
    }

    public static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collide = true)
    {
        return Prim(PrimitiveType.Cube, parent, pos, scale, mat, collide);
    }

    public static GameObject Cyl(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collide = true)
    {
        return Prim(PrimitiveType.Cylinder, parent, pos, scale, mat, collide);
    }

    // Конус (для ёлок и гор): цилиндр со схлопнутой вершиной.
    public static GameObject Cone(Transform parent, Vector3 pos, float radius, float height,
        Material mat, bool collide = false)
    {
        GameObject go = new GameObject("Cone");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.mesh = ConeMesh(radius, height, 16);
        mr.sharedMaterial = mat;
        if (collide)
        {
            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.mesh;
            mc.convex = true;
        }
        return go;
    }

    public static Mesh ConeMesh(float radius, float height, int segments)
    {
        Mesh mesh = new Mesh();
        int vcount = segments * 3 + segments * 3;
        List<Vector3> verts = new List<Vector3>(vcount);
        List<int> tris = new List<int>(vcount);
        List<Vector2> uvs = new List<Vector2>(vcount);

        Vector3 apex = new Vector3(0f, height, 0f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = (float)i / segments * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

            int b = verts.Count;
            verts.Add(apex); verts.Add(p1); verts.Add(p0);
            uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 0f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);

            b = verts.Count;
            verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
            uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Тор (кольца порталов и ударной волны).
    public static Mesh TorusMesh(float majorRadius, float minorRadius, int majorSeg, int minorSeg)
    {
        Mesh mesh = new Mesh();
        int vcount = (majorSeg + 1) * (minorSeg + 1);
        Vector3[] verts = new Vector3[vcount];
        Vector2[] uvs = new Vector2[vcount];
        List<int> tris = new List<int>(majorSeg * minorSeg * 6);

        for (int i = 0; i <= majorSeg; i++)
        {
            float u = (float)i / majorSeg;
            float ua = u * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(ua) * majorRadius, 0f, Mathf.Sin(ua) * majorRadius);
            for (int j = 0; j <= minorSeg; j++)
            {
                float v = (float)j / minorSeg;
                float va = v * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ua) * Mathf.Cos(va), Mathf.Sin(va), Mathf.Sin(ua) * Mathf.Cos(va));
                int idx = i * (minorSeg + 1) + j;
                verts[idx] = center + dir * minorRadius;
                uvs[idx] = new Vector2(u, v);
            }
        }
        for (int i = 0; i < majorSeg; i++)
        {
            for (int j = 0; j < minorSeg; j++)
            {
                int a = i * (minorSeg + 1) + j;
                int b = (i + 1) * (minorSeg + 1) + j;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static GameObject Torus(Transform parent, Vector3 pos, float major, float minor, Material mat)
    {
        GameObject go = new GameObject("Torus");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.mesh = TorusMesh(major, minor, 24, 10);
        mr.sharedMaterial = mat;
        return go;
    }

    // Светящийся billboard-ореол (замена постобработке bloom).
    public static GameObject Glow(Transform parent, Vector3 pos, float size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        go.name = "Glow";
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size, size, size);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AdditiveMat(color, GlowTexture());
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.AddComponent<Billboard>();
        return go;
    }

    public static void NoShadow(GameObject go)
    {
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    // ---------- Материалы с подсветкой контура и листвой ----------

    private static Shader _foliage;

    public static Shader FoliageShader
    {
        get
        {
            if (_foliage == null)
            {
                _foliage = Shader.Find("Bear/Foliage");
                if (_foliage == null) _foliage = Standard;
            }
            return _foliage;
        }
    }

    public static Material FoliageMat(Color color, Color rim)
    {
        string key = "foliage|" + color.r + "_" + color.g + "_" + color.b + "|" + rim.r + "_" + rim.g;
        Material cached;
        if (_matCache.TryGetValue(key, out cached) && cached != null) return cached;

        Material m = new Material(FoliageShader);
        m.color = color;
        m.SetFloat("_Glossiness", 0.06f);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimStrength", 0.28f);
        _matCache[key] = m;
        return m;
    }

    // Материал с подсветкой силуэта — для персонажей и заметных объектов.
    public static Material RimMat(Color color, Color rim, float strength, float smoothness = 0.1f)
    {
        string key = ("rim|" + color.r + "_" + color.g + "_" + color.b + "|" +
                      rim.r + "_" + rim.g + "_" + rim.b + "|" + strength + "|" + smoothness);
        Material cached;
        if (_matCache.TryGetValue(key, out cached) && cached != null) return cached;

        Material m = new Material(Standard);
        m.color = color;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimStrength", strength);
        m.SetFloat("_RimPower", 3f);
        _matCache[key] = m;
        return m;
    }

    // ---------- Процедурные меши ----------

    // Сфера с шумовым смещением — из неё получаются валуны и кроны,
    // которые не выглядят одинаковыми шарами.
    public static Mesh BlobMesh(int seed, float lumpiness, int rings = 10, int segments = 14)
    {
        Mesh mesh = new Mesh();
        int vcount = (rings + 1) * (segments + 1);
        Vector3[] verts = new Vector3[vcount];
        Vector2[] uvs = new Vector2[vcount];
        Color[] colors = new Color[vcount];
        List<int> tris = new List<int>(rings * segments * 6);

        float ox = (seed % 37) * 3.7f;
        float oz = (seed % 53) * 2.3f;

        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float phi = v * Mathf.PI;
            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float theta = u * Mathf.PI * 2f;
                Vector3 dir = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta));

                float n = Mathf.PerlinNoise(ox + dir.x * 2.1f + 4f, oz + dir.z * 2.1f + 4f);
                float n2 = Mathf.PerlinNoise(ox + dir.y * 3.3f + 9f, oz + dir.x * 3.3f + 9f);
                float radius = 0.5f * (1f + (n - 0.5f) * lumpiness + (n2 - 0.5f) * lumpiness * 0.5f);

                int idx = r * (segments + 1) + s;
                verts[idx] = dir * radius;
                uvs[idx] = new Vector2(u, v);
                // Вершинный цвет = запечённое затенение: снизу темнее.
                float ao = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(-0.5f, 0.5f, dir.y));
                colors[idx] = new Color(ao, ao, ao, 1f);
            }
        }

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = (r + 1) * (segments + 1) + s;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Гранёный кристалл: шестигранная призма с остриями сверху и снизу.
    public static Mesh CrystalMesh(float radius, float height, int sides = 6)
    {
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float waist = height * 0.32f;
        Vector3 top = new Vector3(0f, height * 0.5f, 0f);
        Vector3 bottom = new Vector3(0f, -height * 0.5f, 0f);

        for (int i = 0; i < sides; i++)
        {
            float a0 = (float)i / sides * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, waist, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, waist, Mathf.Sin(a1) * radius);
            Vector3 q0 = new Vector3(Mathf.Cos(a0) * radius, -waist, Mathf.Sin(a0) * radius);
            Vector3 q1 = new Vector3(Mathf.Cos(a1) * radius, -waist, Mathf.Sin(a1) * radius);

            int b = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(q1); verts.Add(q0);
            uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 0f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);

            b = verts.Count;
            verts.Add(top); verts.Add(p1); verts.Add(p0);
            uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(1f, 0.6f)); uvs.Add(new Vector2(0f, 0.6f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);

            b = verts.Count;
            verts.Add(bottom); verts.Add(q0); verts.Add(q1);
            uvs.Add(new Vector2(0.5f, 0f)); uvs.Add(new Vector2(0f, 0.4f)); uvs.Add(new Vector2(1f, 0.4f));
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static GameObject Blob(Transform parent, Vector3 pos, Vector3 scale, Material mat,
        int seed, float lumpiness, bool collide)
    {
        GameObject go = new GameObject("Blob");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(0f, seed * 37f % 360f, 0f);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = BlobMesh(seed, lumpiness);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        if (collide)
        {
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.46f;
        }
        return go;
    }

    public static GameObject Crystal(Transform parent, Vector3 pos, float radius, float height,
        Material mat, float tiltDeg)
    {
        GameObject go = new GameObject("Crystal");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(tiltDeg, Random.Range(0f, 360f), 0f);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.mesh = CrystalMesh(radius, height);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    // Точечный источник света — окна, кристаллы, костры.
    public static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
    {
        GameObject go = new GameObject("Light");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }
}

// Поворачивает объект лицом к камере.
// Свечений в кадре под сотню, поэтому поворот камеры берётся один раз
// за кадр в общий кэш, а не ищется каждым билбордом отдельно.
public class Billboard : MonoBehaviour
{
    private static int _frame = -1;
    private static Quaternion _rot = Quaternion.identity;
    private static bool _valid;

    private void LateUpdate()
    {
        if (_frame != Time.frameCount)
        {
            _frame = Time.frameCount;
            Camera cam = Camera.main;
            _valid = cam != null;
            if (_valid) _rot = cam.transform.rotation;
        }
        if (_valid) transform.rotation = _rot;
    }
}
