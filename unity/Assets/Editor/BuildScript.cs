using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Сборка Android APK из командной строки.
// Сцена создаётся кодом, поэтому в репозитории нет бинарных ассетов Unity.
public static class BuildScript
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

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
        "UI/Default",
        "Sprites/Default",
        "Legacy Shaders/Diffuse"
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

            if (report == null || report.summary == null)
            {
                Debug.LogError("BuildScript: no build report returned");
                EditorApplication.Exit(1);
                return;
            }

            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError("BuildScript: build result = " + report.summary.result);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("BuildScript: build succeeded, size = " + report.summary.totalSize + " bytes");
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
        ConfigurePlayerSettings();
        EnsureAlwaysIncludedShaders();
        CreateMainScene();
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "Crucian";
        PlayerSettings.productName = "Медвежьи Приключения";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.colorSpace = ColorSpace.Gamma;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.crucian.bearadventure");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Disabled);

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

        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 70f;
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

    private static string ResolveOutputPath()
    {
        string custom = GetArg("-customBuildPath");
        string name = GetArg("-customBuildName");
        string explicitPath = GetArg("-outputPath");

        if (!string.IsNullOrEmpty(explicitPath)) return explicitPath;

        if (!string.IsNullOrEmpty(custom))
        {
            if (custom.EndsWith(".apk")) return custom;
            string file = string.IsNullOrEmpty(name) ? "BearAdventure" : name;
            if (!file.EndsWith(".apk")) file += ".apk";
            return Path.Combine(custom, file);
        }

        return Path.Combine("build", "BearAdventure.apk");
    }

    private static string GetArg(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == key) return args[i + 1];
        return null;
    }
}
