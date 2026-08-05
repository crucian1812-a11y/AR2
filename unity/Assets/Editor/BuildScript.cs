using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Сборка Android APK из командной строки.
// Сцена создаётся кодом, поэтому в репозитории нет бинарных ассетов Unity.
public static class BuildScript
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    // Вторая игра — путешествие по Калининградской области — собирается в
    // отдельный APK из своей сцены и своим методом BuildKoenig.
    private const string KoenigScenePath = "Assets/Scenes/Koenig.unity";

    // Все шейдеры создаются в рантайме через Shader.Find, поэтому их нужно
    // явно включить в сборку — иначе Unity вырежет их как неиспользуемые.
    private static readonly string[] RequiredShaders =
    {
        "Bear/Lit",
        "Bear/Additive",
        "Bear/Grass",
        "Bear/Water",
        "Bear/Portal",
        "Bear/Sky",
        "Bear/Foliage",
        "Bear/Terrain",
        "Bear/MossyStone",
        "Bear/Waterfall",
        "Bear/Shaft",
        "UI/Default",
        "Sprites/Default"
        // Universal Render Pipeline/Lit сюда добавлять нельзя: у него
        // 1.18 млн вариантов, и принудительное включение обрушивает сборку.
        // Как запасной вариант в Gfx он всё равно не нужен — Bear/Lit есть
        // всегда, а Shader.Find найдёт URP-шейдер и без always-include.
    };

    public static void BuildAndroid()
    {
        try
        {
            Prepare();

            string output = ResolveOutputPath();
            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new string[] { ScenePath };
            options.locationPathName = output;
            options.target = BuildTarget.Android;
            options.targetGroup = BuildTargetGroup.Android;
            options.options = BuildOptions.None;

            Debug.Log("BuildScript: building APK to " + output);
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report == null)
            {
                Debug.LogError("BuildScript: no build report returned");
                EditorApplication.Exit(1);
                return;
            }

            // BuildSummary — структура, поэтому сравнивать её с null нельзя.
            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("BuildScript: build result = " + summary.result);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("BuildScript: build succeeded, size = " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("BuildScript: exception " + e);
            EditorApplication.Exit(1);
        }
    }

    // Сборка второй игры — «Кёнигсберг: Семь мостов». Тот же проект и тот
    // же конвейер, но своя сцена, своё имя и свой пакет, чтобы обе игры
    // ставились на телефон рядом.
    public static void BuildKoenig()
    {
        try
        {
            PrepareKoenig();

            string output = ResolveOutputPath("Koenigsberg");
            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new string[] { KoenigScenePath };
            options.locationPathName = output;
            options.target = BuildTarget.Android;
            options.targetGroup = BuildTargetGroup.Android;
            options.options = BuildOptions.None;

            Debug.Log("BuildScript: building Koenig APK to " + output);
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null) { Debug.LogError("BuildScript: no build report"); EditorApplication.Exit(1); return; }

            UnityEditor.Build.Reporting.BuildSummary summary = report.summary;
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("BuildScript: build result = " + summary.result);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log("BuildScript: Koenig build succeeded, size = " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("BuildScript: exception " + e);
            EditorApplication.Exit(1);
        }
    }

    // Настройка проекта без сборки — используется для проверки компиляции в CI.
    public static void PrepareOnly()
    {
        try
        {
            Prepare();
            Debug.Log("BuildScript: PREPARE OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("BuildScript: exception " + e);
            EditorApplication.Exit(1);
        }
    }

    private static void Prepare()
    {
        EnsureUrpPipeline();
        ConfigurePlayerSettings();
        EnsureAlwaysIncludedShaders();
        CreateMainScene();
    }

    private static void PrepareKoenig()
    {
        EnsureUrpPipeline();
        ConfigureKoenigPlayerSettings();
        EnsureAlwaysIncludedShaders();
        CreateKoenigScene();
    }

    // URP-ассеты тоже создаются кодом: в репозитории нет ни одного
    // бинарного или сериализованного ассета Unity.
    private static void EnsureUrpPipeline()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Settings"));
        AssetDatabase.Refresh();

        const string rendererPath = "Assets/Settings/BearRenderer.asset";
        const string pipelinePath = "Assets/Settings/BearPipeline.asset";

        UniversalRendererData renderer =
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "BearRenderer";
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        // Глубина и нормали нужны для SSAO и глубины резкости.
        renderer.depthPrimingMode = DepthPrimingMode.Disabled;
        AddAmbientOcclusion(renderer, rendererPath);

        UniversalRenderPipelineAsset pipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            pipeline.name = "BearPipeline";
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        SerializedObject so = new SerializedObject(pipeline);
        SerializedProperty list = so.FindProperty("m_RendererDataList");
        if (list != null)
        {
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        }
        SerializedProperty index = so.FindProperty("m_DefaultRendererIndex");
        if (index != null) index.intValue = 0;
        so.ApplyModifiedProperties();

        // HDR — то, ради чего всё затевалось: свет считается за пределами
        // единицы, а тонмаппинг сводит его в кадр без выгорания в белое.
        pipeline.supportsHDR = true;
        pipeline.msaaSampleCount = 4;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = false;
        // Девяносто метров растягивали три каскада на всю эту длину, и
        // тени выходили мылом. Игровая камера Кёнигсберга стоит в 42 м от
        // героя, медвежья — ближе; семидесяти хватает обеим с запасом, а
        // каскады ложатся заметно плотнее.
        pipeline.shadowDistance = 70f;
        pipeline.shadowCascadeCount = 3;
        pipeline.shadowDepthBias = 0.6f;
        pipeline.shadowNormalBias = 0.6f;

        // Мягкие тени доступны только через сериализованное поле —
        // публичное свойство у ассета доступно лишь для чтения.
        SerializedObject shadowSo = new SerializedObject(pipeline);
        SerializedProperty soft = shadowSo.FindProperty("m_SoftShadowsSupported");
        if (soft != null) soft.boolValue = true;
        shadowSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        Debug.Log("BuildScript: URP pipeline asset assigned");
    }

    // SSAO — отдельная возможность рендерера. Настройки приватные, поэтому
    // хватает значений по умолчанию; при неудаче сборка продолжается без него.
    private static void AddAmbientOcclusion(UniversalRendererData renderer, string rendererPath)
    {
        try
        {
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                if (renderer.rendererFeatures[i] is ScreenSpaceAmbientOcclusion) return;

            ScreenSpaceAmbientOcclusion ssao =
                ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            renderer.rendererFeatures.Add(ssao);
            AssetDatabase.AddObjectToAsset(ssao, rendererPath);

            // Список идентификаторов должен совпадать со списком возможностей,
            // иначе рендерер считает данные повреждёнными и игнорирует их.
            SerializedObject rso = new SerializedObject(renderer);
            SerializedProperty map = rso.FindProperty("m_RendererFeatureMap");
            if (map != null)
            {
                map.arraySize = renderer.rendererFeatures.Count;
                for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                {
                    long id;
                    string guid;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        renderer.rendererFeatures[i], out guid, out id);
                    map.GetArrayElementAtIndex(i).longValue = id;
                }
                rso.ApplyModifiedProperties();
            }
            Debug.Log("BuildScript: SSAO renderer feature added");
        }
        catch (Exception e)
        {
            Debug.LogWarning("BuildScript: SSAO не добавлен — " + e.Message);
        }
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "Crucian";
        PlayerSettings.productName = "Медвежьи Приключения";
        PlayerSettings.bundleVersion = "1.0";
        // Линейное пространство обязательно для HDR и ACES-тонмаппинга.
        PlayerSettings.colorSpace = ColorSpace.Linear;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.crucian.bearadventure");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.bundleVersionCode = 1;

        // Игра рассчитана на альбомную ориентацию.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // Сглаживание, тени и лимит источников теперь берутся из URP-ассета.
        QualitySettings.pixelLightCount = 4;
    }

    // Настройки второй игры: своё имя и пакет (чтобы ставилась рядом с
    // медведем) и ПОРТРЕТНАЯ ориентация — карта и интерфейс свёрстаны под
    // вертикальный экран телефона.
    private static void ConfigureKoenigPlayerSettings()
    {
        PlayerSettings.companyName = "Crucian";
        PlayerSettings.productName = "Кёнигсберг: Семь мостов";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.crucian.koenigsberg");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        // Интернет нужен, чтобы открывать AR-станцию в браузере; разрешение
        // на геолокацию Unity добавит в манифест само, раз используется
        // LocationService.
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.bundleVersionCode = 1;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        QualitySettings.pixelLightCount = 4;
    }

    private static void EnsureAlwaysIncludedShaders()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("BuildScript: GraphicsSettings.asset not found, shaders may be stripped");
            return;
        }

        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty array = so.FindProperty("m_AlwaysIncludedShaders");
        if (array == null)
        {
            Debug.LogWarning("BuildScript: m_AlwaysIncludedShaders property not found");
            return;
        }

        List<Shader> existing = new List<Shader>();
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            if (element != null) existing.Add(element.objectReferenceValue as Shader);
        }

        for (int i = 0; i < RequiredShaders.Length; i++)
        {
            Shader shader = Shader.Find(RequiredShaders[i]);
            if (shader == null)
            {
                Debug.LogWarning("BuildScript: shader not found: " + RequiredShaders[i]);
                continue;
            }
            if (existing.Contains(shader)) continue;

            int index = array.arraySize;
            array.InsertArrayElementAtIndex(index);
            SerializedProperty element = array.GetArrayElementAtIndex(index);
            element.objectReferenceValue = shader;
            existing.Add(shader);
            Debug.Log("BuildScript: always-include shader " + RequiredShaders[i]);
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    private static void CreateMainScene()
    {
        // Application.dataPath указывает на <проект>/Assets, поэтому папка
        // создаётся верно независимо от текущего каталога процесса Unity.
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject boot = new GameObject("Bootstrap");
        boot.AddComponent<Bootstrap>();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("Failed to save scene at " + ScenePath);

        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };
        AssetDatabase.Refresh();
        Debug.Log("BuildScript: scene created at " + ScenePath);
    }

    private static void CreateKoenigScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject boot = new GameObject("KoenigBoot");
        boot.AddComponent<Koenig.KoenigBoot>();

        if (!EditorSceneManager.SaveScene(scene, KoenigScenePath))
            throw new Exception("Failed to save scene at " + KoenigScenePath);

        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(KoenigScenePath, true)
        };
        AssetDatabase.Refresh();
        Debug.Log("BuildScript: Koenig scene created at " + KoenigScenePath);
    }

    private static string ResolveOutputPath() { return ResolveOutputPath("BearAdventure"); }

    private static string ResolveOutputPath(string defaultName)
    {
        string custom = GetArg("-customBuildPath");
        string name = GetArg("-customBuildName");
        string explicitPath = GetArg("-outputPath");

        if (!string.IsNullOrEmpty(explicitPath)) return explicitPath;

        if (!string.IsNullOrEmpty(custom))
        {
            if (custom.EndsWith(".apk")) return custom;
            string file = string.IsNullOrEmpty(name) ? defaultName : name;
            if (!file.EndsWith(".apk")) file += ".apk";
            return Path.Combine(custom, file);
        }

        return Path.Combine("build", defaultName + ".apk");
    }

    private static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }
}
