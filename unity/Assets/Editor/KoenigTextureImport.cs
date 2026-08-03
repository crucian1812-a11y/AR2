using UnityEditor;
using UnityEngine;

// Настройки импорта текстур средневекового пака (Resources/Textures/koenig).
// .meta-файлы в репозитории не хранятся, поэтому задаём импорт кодом:
// карты нормалей помечаем как NormalMap (иначе рельеф читается неверно),
// цветовые — sRGB, и держим потолок 1К ради размера APK.
public class KoenigTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (assetPath.IndexOf("Resources/Textures/koenig", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Repeat;

        bool isNormal = assetPath.IndexOf("_Normal", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (isNormal)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.sRGBTexture = false;
        }
        else
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
        }
    }
}
