using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Загрузка моделей средневекового пака Quaternius (Medieval Village
    // MegaKit, CC0) с настоящими текстурами. Модели многоматериальные:
    // одна стена — штукатурка + деревянные балки, крыша — черепица и т.д.
    // Импортёр Unity даёт материалам имена (MI_Plaster, MI_WoodTrim…), по
    // ним и подбираем нужную текстуру каждому подмешу — так у дома стены
    // кремовые, балки коричневые, крыша терракотовая.
    //
    // Текстуры лежат в Resources/Textures/koenig (ужаты до 1К ради размера
    // APK), модели — в Resources/Models/koenig.
    public static class KoenigProp
    {
        private static readonly Dictionary<string, Material> _mat = new Dictionary<string, Material>();

        private static Texture2D Tex(string name)
        {
            return Resources.Load<Texture2D>("Textures/koenig/" + name);
        }

        // Материал категории: базовый цвет + карта нормалей на шейдере Bear/Lit.
        // Публичный — им же красим короб-основу дома и мостовую.
        public static Material CatMaterial(string cat)
        {
            Material m;
            if (_mat.TryGetValue(cat, out m) && m != null) return m;

            string bc = null, nm = null;
            Color flat = new Color(0.72f, 0.68f, 0.62f);
            switch (cat)
            {
                case "plaster": bc = "T_Plaster_BaseColor"; nm = "T_Plaster_Normal"; break;
                case "brick": bc = "T_Brick_BaseColor"; nm = "T_Brick_Normal"; break;
                case "unevenbrick": bc = "T_UnevenBrick_BaseColor"; nm = "T_UnevenBrick_Normal"; break;
                case "redbrick": bc = "T_RedBrick_BaseColor"; break;
                case "rocktrim": bc = "T_RockTrim_BaseColor"; nm = "T_RockTrim_Normal"; break;
                case "roundtiles": bc = "T_RoundTiles_BaseColor"; nm = "T_RoundTiles_Normal"; break;
                case "woodtrim": bc = "T_WoodTrim_BaseColor"; nm = "T_WoodTrim_Normal"; break;
                case "metal": flat = new Color(0.24f, 0.26f, 0.30f); break;
                case "vine": flat = new Color(0.30f, 0.5f, 0.24f); break;
                case "glass": flat = new Color(0.18f, 0.26f, 0.34f); break;
            }

            m = new Material(Gfx.Standard);
            m.color = Color.white;
            m.SetFloat("_Glossiness", cat == "metal" ? 0.4f : 0.05f);
            m.SetFloat("_Metallic", cat == "metal" ? 0.6f : 0f);
            Texture2D t = bc != null ? Tex(bc) : null;
            if (t != null) m.mainTexture = t;
            else m.color = flat;
            if (nm != null)
            {
                Texture2D n = Tex(nm);
                if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_NormalScale", 1f); }
            }
            _mat[cat] = m;
            return m;
        }

        // Склейка подмешей, у которых после переназначения оказался ОДИН
        // материал.
        //
        // Дома пака собраны в Blender так, что каждый несёт от 54 до 80
        // материалов: MI_Plaster, MI_Plaster.001, MI_Plaster.002 и так
        // далее — дубликаты, наплодившиеся при сборке. Мы сводим их к
        // четырём общим по имени, но ЧИСЛО ПОДМЕШЕЙ от этого не меняется,
        // а каждый подмеш — отдельный вызов отрисовки. Двенадцать домов
        // давали под восемьсот вызовов только на застройку; плотную улицу
        // с такой ценой не построить.
        //
        // Здесь треугольники подмешей с одинаковым материалом сливаются в
        // один список. Дом становится четырьмя вызовами вместо шестидесяти.
        // Меш при этом копируется: править общий ресурс нельзя, он один на
        // все экземпляры.
        private static Material[] MergeSubmeshes(Renderer r, Material[] mats)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return mats;
            if (mats.Length < 2 || mf.sharedMesh.subMeshCount != mats.Length) return mats;

            List<Material> uniq = new List<Material>();
            List<List<int>> tris = new List<List<int>>();
            for (int i = 0; i < mats.Length; i++)
            {
                int slot = uniq.IndexOf(mats[i]);
                if (slot < 0) { uniq.Add(mats[i]); tris.Add(new List<int>()); slot = uniq.Count - 1; }
                tris[slot].AddRange(mf.sharedMesh.GetTriangles(i));
            }
            if (uniq.Count == mats.Length) return mats;   // склеивать нечего

            Mesh copy = Object.Instantiate(mf.sharedMesh);
            copy.subMeshCount = uniq.Count;
            for (int i = 0; i < uniq.Count; i++) copy.SetTriangles(tris[i], i, false);
            copy.RecalculateBounds();
            mf.sharedMesh = copy;
            return uniq.ToArray();
        }

        // Категория по имени материала из FBX (важен порядок: частное раньше
        // общего — unevenbrick/redbrick до brick, roundtiles до brick).
        private static string CatFromName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string s = raw.ToLowerInvariant();
            if (s.Contains("glass")) return "glass";
            if (s.Contains("plaster")) return "plaster";
            if (s.Contains("wood")) return "woodtrim";
            if (s.Contains("unevenbrick") || s.Contains("uneven")) return "unevenbrick";
            if (s.Contains("redbrick")) return "redbrick";
            if (s.Contains("roundtiles") || s.Contains("tiles") || s.Contains("roof")) return "roundtiles";
            if (s.Contains("rocktrim") || s.Contains("rock")) return "rocktrim";
            if (s.Contains("metal")) return "metal";
            if (s.Contains("vine") || s.Contains("leaf")) return "vine";
            if (s.Contains("brick")) return "brick";
            return null;
        }

        // Запасная категория по имени модели, если у материала имени нет.
        private static string CatFromId(string id)
        {
            string s = id.ToLowerInvariant();
            if (s.Contains("plaster")) return "plaster";
            if (s.Contains("uneven")) return "unevenbrick";
            if (s.Contains("roundtiles") || s.Contains("tower")) return "roundtiles";
            if (s.Contains("roof") && s.Contains("brick")) return "brick";
            if (s.Contains("floor_red") || s.Contains("redbrick")) return "redbrick";
            if (s.Contains("floor")) return "brick";
            if (s.Contains("metal")) return "metal";
            if (s.Contains("wood") || s.Contains("door") || s.Contains("window") ||
                s.Contains("shutter") || s.Contains("crate") || s.Contains("wagon") ||
                s.Contains("support") || s.Contains("corner") || s.Contains("stair")) return "woodtrim";
            if (s.Contains("chimney")) return "brick";
            if (s.Contains("vine")) return "vine";
            return "plaster";
        }

        // Загрузить и привести к нужной высоте (метры), поставив низом на
        // pos.y. Родные размеры пака заранее неизвестны, а так модель всегда
        // выходит нужного размера — дом домом, бочка бочкой.
        public static GameObject LoadSized(Transform parent, string id, Vector3 pos,
            float yaw, float targetHeight, bool collide = true)
        {
            GameObject go = Load(parent, id, pos, Quaternion.Euler(0f, yaw, 0f), 1f, collide);
            if (go == null) return null;

            Bounds b;
            if (!CombinedBounds(go, out b)) return go;
            float k = targetHeight / Mathf.Max(b.size.y, 0.0001f);
            k = Mathf.Clamp(k, 0.01f, 200f);
            go.transform.localScale = new Vector3(k, k, k);

            // После масштабирования опускаем низ модели ровно на pos.y.
            if (CombinedBounds(go, out b))
            {
                Vector3 lp = go.transform.localPosition;
                go.transform.localPosition = new Vector3(lp.x, lp.y + (pos.y - b.min.y), lp.z);
            }
            return go;
        }

        // Как LoadSized, но приводим к нужной ШИРИНЕ следа (max по X/Z) —
        // для крыш и мостовых, которые «плоские» и по высоте не мерятся.
        // Поставить модуль без масштабирования: серединой основания в
        // заданную точку. Нужен сборщику домов — у модулей пака начало
        // координат стоит где попало, и складывать их «как есть» нельзя.
        // Габарит модели В ЕЁ СОБСТВЕННЫХ КООРДИНАТАХ, посчитанный из
        // sharedMesh.bounds, а не из Renderer.bounds.
        //
        // ЭТО И БЫЛА ПРИЧИНА ПРОПАВШИХ ДОМОВ. Renderer.bounds — величина
        // МИРОВАЯ, и обновляет её система отсечения при отрисовке. У
        // объекта, созданного только что и ещё ни разу не нарисованного,
        // она возвращает габарит меша БЕЗ применённого преобразования —
        // как будто объект лежит в начале координат. В редакторе сцена
        // рисуется постоянно, и значение успевает стать верным; в
        // собранном приложении весь уровень строится до первого кадра, и
        // верным оно не бывает никогда.
        //
        // Дальше арифметика доделывала остальное: модуль вставал не в
        // pos, а примерно в 2·pos плюс положение дома, то есть весь дом
        // уезжал вдвое дальше от улицы и вылетал за кадр. Реквизит уцелел
        // только потому, что его родитель — мир в начале координат, и
        // ошибка сводилась к сантиметрам.
        //
        // sharedMesh.bounds — величина местная, к отрисовке не привязана и
        // верна сразу.
        private static bool LocalBounds(GameObject go, out Bounds b)
        {
            b = new Bounds(Vector3.zero, Vector3.zero);
            MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 inv = go.transform.worldToLocalMatrix;
            bool has = false;
            for (int i = 0; i < mfs.Length; i++)
            {
                if (mfs[i] == null || mfs[i].sharedMesh == null) continue;
                Bounds lb = mfs[i].sharedMesh.bounds;
                Matrix4x4 m = inv * mfs[i].transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3(
                        (c & 1) == 0 ? lb.min.x : lb.max.x,
                        (c & 2) == 0 ? lb.min.y : lb.max.y,
                        (c & 4) == 0 ? lb.min.z : lb.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                    else b.Encapsulate(p);
                }
            }
            return has;
        }

        public static GameObject PlaceByFoot(Transform parent, string id, Vector3 pos, float yaw,
            bool collide = true)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            GameObject go = Load(parent, id, pos, rot, 1f, collide);
            if (go == null) return null;

            Bounds b;
            if (!LocalBounds(go, out b)) return go;

            // Габарит местный, ставим в координатах родителя — значит его
            // надо развернуть поворотом самого модуля. Развороты здесь
            // кратны прямому углу, поэтому низ по Y от поворота не зависит.
            Vector3 c = rot * b.center;
            go.transform.localPosition = new Vector3(
                pos.x - c.x, pos.y - b.min.y, pos.z - c.z);
            return go;
        }

        // Склейка ВСЕХ потомков в один меш на материал.
        //
        // Дом собран из тридцати шести модулей, у каждого два-три подмеша:
        // сто вызовов отрисовки на дом и две тысячи на улицу. Столько не
        // тянет ни один телефон, и это не «медленно», а «слайд-шоу».
        //
        // Здесь треугольники всех модулей переносятся в систему координат
        // дома и сливаются по материалам: дом становится четырьмя мешами
        // вместо ста. Исходные объекты уничтожаются — но ТОЛЬКО после того,
        // как склейка удалась целиком: если меши окажутся нечитаемыми,
        // лучше отрисовать дом дорого, чем не отрисовать вовсе.
        public static void CombineChildren(GameObject root)
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length < 2) return;

            // Сносить будем ПРЯМЫХ потомков: renderer может сидеть не на
            // корне модуля, а на его ребёнке, и удаление одного лишь
            // рисовальщика оставило бы после себя лес пустых узлов.
            List<GameObject> old = new List<GameObject>();
            for (int i = 0; i < root.transform.childCount; i++)
                old.Add(root.transform.GetChild(i).gameObject);

            List<Material> mats = new List<Material>();
            List<List<CombineInstance>> parts = new List<List<CombineInstance>>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

            try
            {
                for (int i = 0; i < rs.Length; i++)
                {
                    MeshFilter mf = rs[i] != null ? rs[i].GetComponent<MeshFilter>() : null;
                    if (mf == null || mf.sharedMesh == null) continue;

                    Material[] ms = rs[i].sharedMaterials;
                    int n = Mathf.Min(ms.Length, mf.sharedMesh.subMeshCount);
                    for (int s = 0; s < n; s++)
                    {
                        int slot = mats.IndexOf(ms[s]);
                        if (slot < 0)
                        {
                            mats.Add(ms[s]);
                            parts.Add(new List<CombineInstance>());
                            slot = mats.Count - 1;
                        }
                        CombineInstance ci = new CombineInstance();
                        ci.mesh = mf.sharedMesh;
                        ci.subMeshIndex = s;
                        ci.transform = toRoot * rs[i].transform.localToWorldMatrix;
                        parts[slot].Add(ci);
                    }
                }
                if (mats.Count == 0) return;

                for (int i = 0; i < mats.Count; i++)
                {
                    Mesh m = new Mesh();
                    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    m.CombineMeshes(parts[i].ToArray(), true, true);
                    m.RecalculateBounds();

                    GameObject part = new GameObject("Part");
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = m;
                    MeshRenderer pr = part.AddComponent<MeshRenderer>();
                    pr.sharedMaterial = mats[i];
                    pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    pr.receiveShadows = true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("KoenigProp: склейка не удалась — " + e.Message);
                return;
            }

            for (int i = 0; i < old.Count; i++)
                if (old[i] != null) Object.Destroy(old[i]);
        }

        public static GameObject LoadSizedWidth(Transform parent, string id, Vector3 pos,
            float yaw, float targetWidth, bool collide = true)
        {
            GameObject go = Load(parent, id, pos, Quaternion.Euler(0f, yaw, 0f), 1f, collide);
            if (go == null) return null;
            Bounds b;
            if (!CombinedBounds(go, out b)) return go;
            float foot = Mathf.Max(b.size.x, b.size.z);
            float k = targetWidth / Mathf.Max(foot, 0.0001f);
            k = Mathf.Clamp(k, 0.01f, 200f);
            go.transform.localScale = new Vector3(k, k, k);
            if (CombinedBounds(go, out b))
            {
                Vector3 lp = go.transform.localPosition;
                go.transform.localPosition = new Vector3(lp.x, lp.y + (pos.y - b.min.y), lp.z);
            }
            return go;
        }

        // Родная высота модели (при масштабе 1) — чтобы подобрать ЕДИНЫЙ
        // масштаб для всего пака: тогда пропорции родные и вытянутые куски
        // (столбы, заборы) не раздуваются, как при нормировке по высоте.
        public static float NativeHeight(Transform parent, string id)
        {
            GameObject go = Load(parent, id, Vector3.zero, Quaternion.identity, 1f, false);
            if (go == null) return 0f;
            Bounds b;
            float h = CombinedBounds(go, out b) ? b.size.y : 0f;
            Object.Destroy(go);
            return h;
        }

        // Загрузить с ЕДИНЫМ масштабом (родные пропорции), поставить низом на
        // pos.y и вернуть мировой размер (для подгонки короба под крышу).
        public static GameObject LoadScaled(Transform parent, string id, Vector3 pos,
            float yaw, float scale, bool collide, out Vector3 worldSize)
        {
            worldSize = Vector3.zero;
            GameObject go = Load(parent, id, pos, Quaternion.Euler(0f, yaw, 0f), scale, collide);
            if (go == null) return null;
            Bounds b;
            if (CombinedBounds(go, out b))
            {
                worldSize = b.size;
                Vector3 lp = go.transform.localPosition;
                go.transform.localPosition = new Vector3(lp.x, lp.y + (pos.y - b.min.y), lp.z);
            }
            return go;
        }

        // Мировой габарит — тоже из sharedMesh.bounds, и по той же причине:
        // Renderer.bounds до первой отрисовки лжёт (см. LocalBounds).
        private static bool CombinedBounds(GameObject go, out Bounds b)
        {
            b = new Bounds(go.transform.position, Vector3.zero);
            MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
            bool has = false;
            for (int i = 0; i < mfs.Length; i++)
            {
                if (mfs[i] == null || mfs[i].sharedMesh == null) continue;
                Bounds lb = mfs[i].sharedMesh.bounds;
                Matrix4x4 m = mfs[i].transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = new Vector3(
                        (c & 1) == 0 ? lb.min.x : lb.max.x,
                        (c & 2) == 0 ? lb.min.y : lb.max.y,
                        (c & 4) == 0 ? lb.min.z : lb.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                    else b.Encapsulate(p);
                }
            }
            return has;
        }

        // Загрузить модель по id, покрасить подмеши, повесить коллайдер.
        public static GameObject Load(Transform parent, string id, Vector3 pos,
            Quaternion rot, float scale, bool collide = true)
        {
            GameObject prefab = Resources.Load<GameObject>("Models/koenig/" + id);
            if (prefab == null) { Debug.LogWarning("KoenigProp: нет модели " + id); return null; }

            GameObject go = Object.Instantiate(prefab);
            go.name = id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(scale, scale, scale);

            string idCat = CatFromId(id);
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                Material[] src = rs[i].sharedMaterials;
                Material[] outM = new Material[src.Length];
                for (int s = 0; s < src.Length; s++)
                {
                    string cat = src[s] != null ? CatFromName(src[s].name) : null;
                    if (cat == null) cat = idCat;
                    outM[s] = CatMaterial(cat);
                }
                outM = MergeSubmeshes(rs[i], outM);
                rs[i].sharedMaterials = outM;
                rs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rs[i].receiveShadows = true;
            }

            if (collide)
            {
                MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>();
                for (int i = 0; i < mfs.Length; i++)
                {
                    if (mfs[i] == null || mfs[i].sharedMesh == null) continue;
                    MeshCollider mc = mfs[i].gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mfs[i].sharedMesh;
                }
            }
            return go;
        }
    }
}
