using UnityEditor;
using UnityEngine;

// Настройки импорта моделей и текстур задаются кодом, а не .meta-файлами:
// в репозитории лежат «голые» FBX и PNG, скачанные из пака, а Unity в CI
// импортирует их с нуля. Без этого скрипта риг слетал бы на настройках
// по умолчанию и клипы анимации не находились бы по именам.
public class ModelImportSettings : AssetPostprocessor
{
    private const string ModelDir = "Assets/Resources/Models/";
    private const string PropDir = "Assets/Resources/Models/nature/";
    private const string TextureDir = "Assets/Resources/Textures/";

    // Клипы, которые должны проигрываться по кругу.
    private static readonly string[] LoopingClips =
    {
        "idle", "walk", "run", "flying", "dance"
    };

    private void OnPreprocessModel()
    {
        if (assetPath == null || !assetPath.StartsWith(ModelDir)) return;

        ModelImporter mi = assetImporter as ModelImporter;
        if (mi == null) return;

        bool isProp = assetPath.StartsWith(PropDir);

        if (isProp)
        {
            // Реквизит статичный — скелет ему не нужен. Зато материалы
            // обязательны: у моделей Kenney нет текстур, весь вид держится
            // на именованных материалах (woodBark, leafsGreen), и без них
            // дерево стало бы одноцветным.
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
        else
        {
            // Legacy-риг: клипы лежат прямо на компоненте Animation и играются
            // по имени из кода. Generic потребовал бы ассет AnimatorController,
            // а весь проект принципиально собирается без ассетов.
            mi.animationType = ModelImporterAnimationType.Legacy;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;

            // Материалы монстров делаем сами: в FBX они ссылаются на текстуры
            // по путям из Blender, которых у нас нет.
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        mi.importCameras = false;
        mi.importLights = false;
        mi.isReadable = false;
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.importNormals = ModelImporterNormals.Import;
    }

    // Blender называет тейки «MonsterArmature|Idle». Отрезаем префикс,
    // чтобы код искал клип просто по «Idle».
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
        }
        mi.clipAnimations = clips;
    }

    private void OnPreprocessTexture()
    {
        if (assetPath == null || !assetPath.StartsWith(TextureDir)) return;

        TextureImporter ti = assetImporter as TextureImporter;
        if (ti == null) return;

        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.filterMode = FilterMode.Bilinear;

        // Текстуры поверхностей тайлятся по рельефу, атласы монстров — нет.
        bool tiling = assetPath.StartsWith(TextureDir + "world/");
        ti.maxTextureSize = tiling ? 1024 : 512;
        ti.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
    }
}
