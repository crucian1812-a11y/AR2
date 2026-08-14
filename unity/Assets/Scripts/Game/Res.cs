using System.Collections.Generic;
using UnityEngine;

// Доступ к ресурсам. Всё грузится по пути через Resources — ссылок из
// сцены нет и быть не может: сцена создаётся скриптом сборки.
public static class Res
{
    public const string FighterPath = "Models/fighters/Fighter";

    private static GameObject _fighterPrefab;
    private static Dictionary<string, AnimationClip> _clips;

    public static GameObject FighterPrefab()
    {
        if (_fighterPrefab == null)
        {
            _fighterPrefab = Resources.Load<GameObject>(FighterPath);
            if (_fighterPrefab == null)
                Debug.LogWarning("Res: не найдена модель бойца " + FighterPath);
        }
        return _fighterPrefab;
    }

    // Клипы лежат подобъектами внутри FBX, поэтому грузятся не по имени
    // файла, а все сразу из одного ассета.
    public static Dictionary<string, AnimationClip> Clips()
    {
        if (_clips != null) return _clips;

        _clips = new Dictionary<string, AnimationClip>();
        AnimationClip[] all = Resources.LoadAll<AnimationClip>(FighterPath);
        if (all != null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                // У импортированной модели есть служебный клип
                // «__preview__…» — в игре он не нужен.
                if (all[i].name.StartsWith("__")) continue;
                _clips[all[i].name] = all[i];
            }
        }
        Debug.Log("Res: клипов загружено " + _clips.Count);
        return _clips;
    }

    public static AnimationClip Clip(string name)
    {
        AnimationClip clip;
        return Clips().TryGetValue(name, out clip) ? clip : null;
    }

    // Имя клипа для роли и позиции: Top_Mount, Bottom_ClosedGuard.
    public static string HoldName(bool top, Pos pos)
    {
        return (top ? "Top_" : "Bottom_") + pos.ToString();
    }

    public static string MoveName(bool top, string clip)
    {
        return (top ? "Top_" : "Bottom_") + clip;
    }

    // Карты рисуются в tools/blender/texture.py и лежат рядом с моделью.
    // Имена — «<материал>_<карта>»: skin_albedo, gi_normal, belt_orm.
    private static readonly Dictionary<string, Texture2D> _textures =
        new Dictionary<string, Texture2D>();

    public static Texture2D Map(string material, string kind)
    {
        string key = material + "_" + kind;
        Texture2D tex;
        if (_textures.TryGetValue(key, out tex)) return tex;

        tex = Resources.Load<Texture2D>("Textures/fighters/" + key);
        if (tex == null)
            Debug.LogWarning("Res: нет карты " + key);
        _textures[key] = tex;
        return tex;
    }
}
