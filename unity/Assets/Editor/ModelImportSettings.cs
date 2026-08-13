using UnityEditor;
using UnityEngine;

// Настройки импорта задаются кодом, а не .meta-файлами: в репозитории лежат
// «голые» FBX и PNG, а Unity в CI импортирует их с нуля. Без этого скрипта
// риг слетал бы на настройки по умолчанию.
//
// Риг бойцов — Generic; почему именно он, а не Humanoid, разобрано ниже,
// в OnPreprocessModel.
public class ModelImportSettings : AssetPostprocessor
{
    private const string ModelDir = "Assets/Resources/Models/";
    // Реквизит: татами, стены зала, мебель — статичные меши без скелета.
    private const string PropDir = "Assets/Resources/Models/props/";
    private const string TextureDir = "Assets/Resources/Textures/";

    // Клипы называются «<роль>_<что>»: Top_Mount, Bottom_Pass_Closed.
    // Зацикливать нужно ровно удержания позиций — боец живёт в позиции,
    // пока не сменит её, и клип не должен обрываться. Переходы, наоборот,
    // играются один раз.
    //
    // Список позиций дублирует enum Pos из Positions.cs. Дублирование
    // осознанное: Editor-скрипт не должен зависеть от игровой сборки, а
    // расхождение сразу видно — клип просто перестанет зацикливаться.
    private static readonly string[] HeldPositions =
    {
        "standing", "closedguard", "openguard", "halfguard",
        "sidecontrol", "mount", "backcontrol", "turtledown"
    };

    private static bool IsHold(string clipName)
    {
        string low = clipName.ToLowerInvariant();
        int underscore = low.IndexOf('_');
        if (underscore < 0 || underscore + 1 >= low.Length) return false;

        string what = low.Substring(underscore + 1);
        for (int i = 0; i < HeldPositions.Length; i++)
            if (what == HeldPositions[i]) return true;
        return false;
    }

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
            // Generic, а не Humanoid — вопреки первоначальному плану.
            //
            // Humanoid нужен для ретаргета клипов между разными моделями,
            // и ради него он и выбирался. Но он же нормирует позу через
            // «мышцы» с пределами подвижности, а позиции борьбы как раз
            // предельные: закрытая гвардия, треугольник, крюки на спине.
            // Humanoid обрезал бы их — руки и ноги не доходили бы до
            // нужных углов, и парные клипы разъехались бы именно там, где
            // точность важнее всего.
            //
            // Клипы сняты на том же скелете, что и модель (tools/blender),
            // поэтому ретаргет не нужен вовсе, а Generic проигрывает позу
            // ровно так, как она была сделана. Если позже подключать
            // модели из чужого пака, здесь меняется одна строка — и тогда
            // придётся смириться с пределами Humanoid.
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
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

            bool loop = IsHold(n);
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
