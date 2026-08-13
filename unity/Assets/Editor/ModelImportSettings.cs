using UnityEditor;
using UnityEngine;

// Настройки импорта задаются кодом, а не .meta-файлами: в репозитории лежат
// «голые» FBX и PNG, а Unity в CI импортирует их с нуля. Без этого скрипта
// риг слетал бы на настройки по умолчанию.
//
// Отличие от медвежьего проекта, откуда взят подход: там Legacy-риг, здесь
// Humanoid. Legacy проще (клипы играются по имени, ассет контроллера не
// нужен), но он не даёт ни ретаргета между моделями, ни IK. Для борьбы
// нужно и то, и другое: руки бойца должны держаться за тело соперника,
// а не проходить сквозь него — см. §4 в docs/bjj/PLAN.md.
public class ModelImportSettings : AssetPostprocessor
{
    private const string ModelDir = "Assets/Resources/Models/";
    // Персонажи: скелет Humanoid, анимации импортируются.
    private const string FighterDir = "Assets/Resources/Models/fighters/";
    // Реквизит: татами, стены зала, мебель — статичные меши без скелета.
    private const string PropDir = "Assets/Resources/Models/props/";
    private const string TextureDir = "Assets/Resources/Textures/";

    // Клипы, которые должны проигрываться по кругу. Позиции партера —
    // это именно зацикленные «удержания»: боец в них живёт, пока не
    // сменит позицию, и клип не должен обрываться.
    private static readonly string[] LoopingClips =
    {
        "idle", "idle_standing", "idle_guard", "idle_mount", "idle_side",
        "idle_back", "idle_half", "idle_turtle", "breathe"
    };

    private void OnPreprocessModel()
    {
        if (assetPath == null || !assetPath.StartsWith(ModelDir)) return;

        ModelImporter mi = assetImporter as ModelImporter;
        if (mi == null) return;

        if (assetPath.StartsWith(PropDir))
        {
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        else
        {
            // Humanoid: Unity строит отображение костей на свой эталонный
            // скелет, и клип, снятый на одной модели, играется на любой
            // другой. У пака Quaternius риг как раз Humanoid — без этого
            // ретаргет пришлось бы делать руками для каждой модели.
            mi.animationType = ModelImporterAnimationType.Human;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // Аватар создаётся из самой модели. Для бойцов это верно: у нас
            // один базовый скелет, а не набор моделей под общий аватар.
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        mi.importCameras = false;
        mi.importLights = false;
        mi.isReadable = false;
        // Сжатие меша квантует координаты вершин. На низкополигональных
        // моделях это видно прямо в кадре: грани дрожат, стыки расходятся.
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.optimizeMeshPolygons = true;
        mi.optimizeMeshVertices = true;
    }

    // Blender называет тейки «Armature|Idle». Отрезаем префикс, чтобы код
    // искал клип просто по «Idle».
    private void OnPreprocessAnimation()
    {
        if (assetPath == null || !assetPath.StartsWith(ModelDir)) return;
        if (assetPath.StartsWith(PropDir)) return;

        ModelImporter mi = assetImporter as ModelImporter;
        if (mi == null) return;

        ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++)
        {
            string n = clips[i].name;
            if (string.IsNullOrEmpty(n)) continue;

            int bar = n.LastIndexOf('|');
            if (bar >= 0 && bar + 1 < n.Length) n = n.Substring(bar + 1);
            clips[i].name = n;

            string low = n.ToLowerInvariant();
            bool loop = false;
            for (int k = 0; k < LoopingClips.Length; k++)
                if (low == LoopingClips[k]) loop = true;

            clips[i].loopTime = loop;
            clips[i].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            // Корень на месте: позиции партера привязаны к точке схватки, а
            // не к движению корня в клипе. Иначе бойцы уползали бы с татами
            // за несколько переходов — ошибка, которая копится незаметно.
            clips[i].lockRootRotation = !loop;
            clips[i].keepOriginalPositionY = true;
        }
        mi.clipAnimations = clips;
    }

    private void OnPreprocessTexture()
    {
        if (assetPath == null || !assetPath.StartsWith(TextureDir)) return;

        TextureImporter ti = assetImporter as TextureImporter;
        if (ti == null) return;

        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        // Карты нормалей нельзя помечать как sRGB: видеокарта раскодировала
        // бы им гамму, и в шейдер пришёл бы искажённый вектор.
        bool isNormal = file.Contains("_nor") || file.Contains("normal");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = !isNormal;

        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;

        // У стилизованных моделей нет микродеталей, 1K хватает с запасом.
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.wrapMode = TextureWrapMode.Clamp;
    }
}
