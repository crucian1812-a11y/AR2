using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Природа уровня: трава, песок, вода, невидимые границы.
    //
    // Всё это уже было написано для игры про медведя, но живёт методами
    // класса WorldBuilder, от которого KoenigLevel не наследуется и не
    // может: тот строит мир по своим правилам, с террейном, порталами и
    // собственным героем. Поэтому нужные куски здесь переписаны статикой —
    // без наследования и без общего состояния.
    public static class KoenigScenery
    {
        // ---------- Трава ----------
        //
        // Одна травинка — четыре яруса, восемь вершин, шесть треугольников,
        // и всё поле собирается ОДНИМ мешем: десять тысяч отдельных объектов
        // телефон не переживёт, а десять тысяч травинок в одном меше — это
        // один вызов отрисовки.
        //
        // Колыхание считает шейдер Bear/Grass: фаза каждой травинки лежит в
        // UV2, поэтому поле шевелится не единой волной, а вразнобой.
        private const int Levels = 4;
        private const int VertsPer = Levels * 2;
        private const int IndicesPer = (Levels - 1) * 6;

        public static GameObject GrassField(Transform parent, Vector3 center, Vector2 extents,
            int count, Color baseCol, Color tipCol, float minHeight, float maxHeight,
            System.Func<Vector3, float> ground)
        {
            count = Mathf.Clamp(count, 1, 24000);
            Vector3[] verts = new Vector3[count * VertsPer];
            Vector2[] uvs = new Vector2[count * VertsPer];
            Vector2[] uv2 = new Vector2[count * VertsPer];
            Color[] colors = new Color[count * VertsPer];
            Vector3[] normals = new Vector3[count * VertsPer];
            int[] tris = new int[count * IndicesPer];

            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + new Vector3(
                    Random.Range(-extents.x, extents.x), 0f, Random.Range(-extents.y, extents.y));
                p.y = ground != null ? ground(p) : center.y;

                float yaw = Random.Range(0f, Mathf.PI);
                // Ширина травинки была 0.05–0.15 м при высоте до полуметра:
                // отношение как у пшеничного колоса, и обочина читалась
                // полем пшеницы, а не газоном. Втрое уже — и то же поле
                // становится травой.
                float w = 0.018f * Random.Range(0.75f, 1.5f);
                float h = Random.Range(minHeight, maxHeight);
                float phase = Random.value;

                Vector3 side = new Vector3(Mathf.Cos(yaw) * w, 0f, Mathf.Sin(yaw) * w);

                // Травинка изгибается, а не стоит по струнке: ярусы уходят
                // в сторону всё сильнее (t в квадрате).
                float bend = Random.Range(0.12f, 0.5f) * h;
                float bendYaw = Random.Range(0f, Mathf.PI * 2f);
                Vector3 bendDir = new Vector3(Mathf.Cos(bendYaw), 0f, Mathf.Sin(bendYaw));

                float shade = Random.Range(0.78f, 1.16f);
                float warm = Random.Range(-0.04f, 0.06f);

                Vector3 face = Vector3.Cross(side.normalized, Vector3.up);
                Vector3 nrm = (Vector3.up * 0.72f + face * 0.28f).normalized;

                int v = i * VertsPer;
                for (int L = 0; L < Levels; L++)
                {
                    float t = (float)L / (Levels - 1);
                    float wk = Mathf.Lerp(1f, 0.06f, t * t * 0.85f + t * 0.15f);
                    Vector3 lvl = p + Vector3.up * (h * t) + bendDir * (bend * t * t);

                    int a = v + L * 2;
                    verts[a] = lvl - side * wk;
                    verts[a + 1] = lvl + side * wk;
                    uvs[a] = new Vector2(0f, t);
                    uvs[a + 1] = new Vector2(1f, t);
                    uv2[a] = new Vector2(phase, t);
                    uv2[a + 1] = new Vector2(phase, t);
                    normals[a] = nrm;
                    normals[a + 1] = nrm;

                    // У земли темнее: самозатенение делает ковёр объёмным.
                    Color c = Color.Lerp(baseCol, tipCol, t);
                    float ao = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(t * 1.6f));
                    c = new Color(c.r * shade * ao + warm, c.g * shade * ao,
                                  c.b * shade * ao - warm * 0.5f, 1f);
                    colors[a] = c;
                    colors[a + 1] = c;
                }

                int tri = i * IndicesPer;
                for (int L = 0; L < Levels - 1; L++)
                {
                    int a = v + L * 2;
                    int b = a + 2;
                    tris[tri] = a; tris[tri + 1] = b; tris[tri + 2] = a + 1;
                    tris[tri + 3] = a + 1; tris[tri + 4] = b; tris[tri + 5] = b + 1;
                    tri += 6;
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.uv2 = uv2;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            GameObject go = new GameObject("Grass");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Shader sh = Shader.Find("Bear/Grass");
            mr.sharedMaterial = sh != null ? new Material(sh) : Gfx.Mat(baseCol);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------- Земля с рельефом ----------
        //
        // Сетка по высоте от переданной функции. Ею сделан весь берег:
        // дюнный вал, ложбина, мокрый песок и уход под воду — коробом
        // такого не задать, а именно перепад высот превращает плоскую
        // площадку в пляж.
        //
        // Материал трипланарный (Bear/Terrain), поэтому развёртка не нужна
        // вовсе: текстура ложится по мировым координатам и на склоне не
        // растягивается.
        public static GameObject Terrain(Transform parent, Rect area, int cols, int rows,
            System.Func<float, float, float> height, System.Func<float, float, Color> tint,
            Material mat, bool collide)
        {
            cols = Mathf.Clamp(cols, 2, 200);
            rows = Mathf.Clamp(rows, 2, 200);

            int nx = cols + 1, nz = rows + 1;
            Vector3[] verts = new Vector3[nx * nz];
            Color[] colors = new Color[nx * nz];
            Vector2[] uvs = new Vector2[nx * nz];
            int[] tris = new int[cols * rows * 6];

            for (int j = 0; j < nz; j++)
            {
                float z = Mathf.Lerp(area.yMin, area.yMax, (float)j / rows);
                for (int i = 0; i < nx; i++)
                {
                    float x = Mathf.Lerp(area.xMin, area.xMax, (float)i / cols);
                    int k = j * nx + i;
                    float y = height != null ? height(x, z) : 0f;
                    verts[k] = new Vector3(x, y, z);
                    uvs[k] = new Vector2((float)i / cols, (float)j / rows);
                    colors[k] = tint != null ? tint(x, z) : Color.white;
                }
            }

            int t = 0;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int a = j * nx + i;
                    tris[t] = a; tris[t + 1] = a + nx; tris[t + 2] = a + 1;
                    tris[t + 3] = a + 1; tris[t + 4] = a + nx; tris[t + 5] = a + nx + 1;
                    t += 6;
                }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = true;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // ---------- Море ----------
        //
        // Плоскость под шейдером Bear/Water: волна в вершинах, две
        // скользящие карты ряби и — главное — пена по глубине. Пена и
        // делает линию прибоя: она сама находит место, где песок подходит
        // близко к поверхности, поэтому берег не надо размечать руками.
        public static GameObject Sea(Transform parent, Rect area, int cols, int rows,
            Color shallow, Color deep)
        {
            int nx = cols + 1, nz = rows + 1;
            Vector3[] verts = new Vector3[nx * nz];
            Vector2[] uvs = new Vector2[nx * nz];
            int[] tris = new int[cols * rows * 6];

            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = Mathf.Lerp(area.xMin, area.xMax, (float)i / cols);
                    float z = Mathf.Lerp(area.yMin, area.yMax, (float)j / rows);
                    int k = j * nx + i;
                    verts[k] = new Vector3(x, 0f, z);
                    uvs[k] = new Vector2(x * 0.05f, z * 0.05f);
                }

            int t = 0;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int a = j * nx + i;
                    tris[t] = a; tris[t + 1] = a + nx; tris[t + 2] = a + 1;
                    tris[t + 3] = a + 1; tris[t + 4] = a + nx; tris[t + 5] = a + nx + 1;
                    t += 6;
                }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();

            Shader ws = Shader.Find("Bear/Water");
            if (ws != null)
            {
                Material m = new Material(ws);
                m.SetTexture("_BumpMap", Gfx.WaterNormal());
                m.SetTextureScale("_BumpMap", new Vector2(14f, 14f));
                m.SetColor("_ShallowColor", shallow);
                m.SetColor("_DeepColor", deep);
                // Балтика мелкая и прозрачная у самого берега: переход от
                // песка к синеве занимает метры, а не сантиметры.
                m.SetFloat("_DepthFade", 2.2f);
                m.SetFloat("_FoamWidth", 1.5f);
                m.SetFloat("_WaveHeight", 0.05f);
                m.SetFloat("_WaveSpeed", 1.1f);
                m.SetFloat("_Alpha", 0.88f);
                mr.sharedMaterial = m;
            }
            else mr.sharedMaterial = Gfx.Mat(deep, 0.85f, 0.1f);

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ---------- Зелень и камни ----------
        //
        // ПОЧЕМУ НЕ ПАК KENNEY. В Resources/Models/nature лежат триста
        // моделей, и дерево оттуда встало в кадр рядом с текстурованным
        // фахверком: кубическая крона четырьмя плашками, плоская заливка
        // без текстуры, бирюзовые гранёные камни. Рядом с домами Quaternius
        // это выглядит деталью из другой игры — и это ровно та «кубическая
        // графика», от которой мы уходим.
        //
        // Здесь и дерево, и куст, и валун — комковатый шар Gfx.Blob:
        // силуэт неровный, освещение объёмное, а крона идёт под шейдером
        // листвы и качается вместе с травой.
        public static GameObject Tree(Transform parent, Vector3 pos, float height, int seed)
        {
            GameObject root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;

            Material bark = KoenigProp.CatMaterial("woodtrim");
            Material leaf = Gfx.FoliageMat(new Color(0.34f, 0.50f, 0.24f),
                                           new Color(0.70f, 0.82f, 0.44f));

            float trunk = height * 0.42f;
            float r = height * 0.075f;
            Gfx.Cyl(root.transform, new Vector3(0f, trunk * 0.5f, 0f),
                new Vector3(r * 2.4f, trunk * 0.5f, r * 2.4f), bark, true);
            // Развилка: два коротких сука под крону — без них ствол
            // упирается в шар и дерево читается грибом.
            for (int i = 0; i < 2; i++)
            {
                float a = seed * 1.3f + i * Mathf.PI;
                GameObject br = Gfx.Cyl(root.transform,
                    new Vector3(Mathf.Cos(a) * r * 1.6f, trunk * 0.95f, Mathf.Sin(a) * r * 1.6f),
                    new Vector3(r * 1.3f, height * 0.12f, r * 1.3f), bark, false);
                br.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(a) * 22f, 0f, -Mathf.Cos(a) * 22f);
            }

            float cr = height * 0.30f;
            Gfx.Blob(root.transform, new Vector3(0f, trunk + cr * 0.85f, 0f),
                new Vector3(cr * 1.25f, cr, cr * 1.25f), leaf, seed, 0.30f, false);
            Gfx.Blob(root.transform, new Vector3(-cr * 0.55f, trunk + cr * 1.45f, cr * 0.2f),
                new Vector3(cr * 0.8f, cr * 0.7f, cr * 0.8f), leaf, seed + 7, 0.34f, false);
            Gfx.Blob(root.transform, new Vector3(cr * 0.5f, trunk + cr * 1.3f, -cr * 0.3f),
                new Vector3(cr * 0.85f, cr * 0.75f, cr * 0.85f), leaf, seed + 13, 0.32f, false);
            return root;
        }

        public static void Bush(Transform parent, Vector3 pos, float size, int seed)
        {
            Material leaf = Gfx.FoliageMat(new Color(0.30f, 0.46f, 0.22f),
                                           new Color(0.64f, 0.78f, 0.40f));
            Gfx.Blob(parent, pos + new Vector3(0f, size * 0.5f, 0f),
                new Vector3(size * 0.75f, size * 0.5f, size * 0.75f), leaf, seed, 0.38f, false);
            Gfx.Blob(parent, pos + new Vector3(size * 0.35f, size * 0.34f, size * 0.2f),
                new Vector3(size * 0.45f, size * 0.34f, size * 0.45f), leaf, seed + 5, 0.4f, false);
        }

        public static void Boulder(Transform parent, Vector3 pos, float size, int seed)
        {
            Material rock = KoenigProp.CatMaterial("rocktrim");
            Gfx.Blob(parent, pos + new Vector3(0f, size * 0.34f, 0f),
                new Vector3(size * 0.62f, size * 0.42f, size * 0.55f), rock, seed, 0.24f, true);
        }

        // Коряга: ствол, вылизанный морем. Наклон обязателен — лежащий
        // ровно цилиндр читается бревном со стройки, а не находкой.
        public static void Driftwood(Transform parent, Vector3 pos, float length, float yaw, int seed)
        {
            Material bark = KoenigProp.CatMaterial("woodtrim");
            GameObject go = Gfx.Cyl(parent, pos + new Vector3(0f, length * 0.09f, 0f),
                new Vector3(length * 0.17f, length * 0.5f, length * 0.17f), bark, true);
            go.transform.localRotation = Quaternion.Euler(90f, yaw, 8f + (seed % 5) * 3f);
        }

        // ---------- Граница мира ----------
        //
        // Раньше край улицы держали каменные стены семиметровой высоты. При
        // ортографии они лезли в кадр и читались декорацией, которой там
        // быть не должно. Невидимый короб делает ровно то же, ничего не
        // рисуя.
        public static void Fence(Transform parent, Vector3 center, Vector3 size)
        {
            GameObject go = new GameObject("Fence");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }
    }
}
