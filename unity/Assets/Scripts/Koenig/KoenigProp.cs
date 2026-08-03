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
        private static Material MatFor(string cat)
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

        // Категория по имени материала из FBX (важен порядок: частное раньше
        // общего — unevenbrick/redbrick до brick, roundtiles до brick).
        private static string CatFromName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string s = raw.ToLowerInvariant();
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
                    outM[s] = MatFor(cat);
                }
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
