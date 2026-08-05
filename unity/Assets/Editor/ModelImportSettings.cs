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
    // Собственные ассеты из tools/blender — тоже статичный реквизит.
    private const string CustomDir = "Assets/Resources/Models/custom/";
    // Модули средневекового пака — тоже статичный реквизит. Без этой строки
    // дома попадали в ветку ПЕРСОНАЖЕЙ: им включался Legacy-риг и импорт
    // анимации, а Unity при этом обрабатывает оси иначе, чем для статичной
    // геометрии, — и модель из Blender ЛОЖИЛАСЬ НА БОК. На экране это
    // читалось то интерьером дома, то обрезом кадра; на самом деле дом
    // просто лежал, показывая стену как пол.
    private const string KoenigDir = "Assets/Resources/Models/koenig/";
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

        bool isProp = assetPath.StartsWith(PropDir) || assetPath.StartsWith(CustomDir)
                   || assetPath.StartsWith(KoenigDir);

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

            // Материалы импортируем всегда: у части моделей нет отдельного
            // атласа, и весь их вид держится на материалах из FBX. Там, где
            // атлас есть, материал всё равно переопределяется в рантайме.
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        mi.importCameras = false;
        mi.importLights = false;
        mi.isReadable = false;
        // Сжатие меша квантует координаты вершин и нормалей. На низкополигональных
        // моделях пака это видно прямо в кадре: грани дрожат, стыки листвы
        // расходятся, силуэт кроны становится рваным — то самое «пиксельное»
        // дерево. Место на диске нам не дороже картинки.
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.optimizeMeshPolygons = true;
        mi.optimizeMeshVertices = true;
    }

    // Blender называет тейки «MonsterArmature|Idle». Отрезаем префикс,
    // чтобы код искал клип просто по «Idle».
    private void OnPreprocessAnimation()
    {
        if (assetPath == null || !assetPath.StartsWith(ModelDir)) return;
        if (assetPath.StartsWith(PropDir) || assetPath.StartsWith(CustomDir)
            || assetPath.StartsWith(KoenigDir)) return;

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

        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        // Карты нормалей помечались как обычные цветные текстуры, то есть
        // sRGB: видеокарта раскодировала им гамму, и в шейдер приходил
        // искажённый вектор — рельеф освещался неверно и выглядел грязным.
        //
        // Тип оставляем Default намеренно. NormalMap переупаковал бы данные
        // в каналы (A, G), а Bear/Terrain распаковывает нормаль вручную
        // (sample * 2 - 1) и ждёт обычный RGB. Достаточно снять sRGB.
        bool isNormal = file.Contains("_nor") || file.Contains("normal");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = !isNormal;

        ti.mipmapEnabled = true;

        // Текстуры поверхностей тайлятся по рельефу, атласы монстров — нет.
        //
        // Папка koenig раньше сюда НЕ входила, и это была главная причина
        // «сырой» картинки: её текстуры получали wrapMode = Clamp, а значит
        // SetTextureScale не тайлил вовсе — UV уходили за единицу и
        // зажимались в краевой пиксель. Мостовая превращалась в один
        // отпечаток камня в углу грани и растянутую заливку на остальные
        // двадцать четыре метра.
        //
        // Отдельный KoenigTextureImport, который ставил Repeat, спор не
        // решал: порядок AssetPostprocessor'ов не задан ни в одном из них,
        // и кто выиграет — определялось случаем. Поэтому он удалён, а
        // папка добавлена сюда.
        bool tiling = assetPath.StartsWith(TextureDir + "world/")
                   || assetPath.StartsWith(TextureDir + "koenig/");

        // Билинейная фильтрация без анизотропии — главная причина «пикселей»
        // на земле: под острым углом (а камера смотрит именно так) мип-уровни
        // сменяются ступеньками, и трава разваливается на квадраты.
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = tiling ? 12 : 4;

        ti.maxTextureSize = tiling ? 2048 : 1024;
        ti.textureCompression = tiling
            ? TextureImporterCompression.CompressedHQ
            : TextureImporterCompression.Compressed;
        ti.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
    }
}
