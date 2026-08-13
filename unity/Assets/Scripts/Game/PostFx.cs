using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Пост-обработка. Профиль тома создаётся кодом, а не ассетом.
//
// Порядок важен и он не произвольный: тонмаппинг сводит расширенный
// диапазон в кадр, всё остальное настраивается уже под него. Если сперва
// накрутить яркость, а тонмаппинг включить потом, вся настройка съедет.
public static class PostFx
{
    public static Volume Build(Transform parent)
    {
        GameObject go = new GameObject("PostFx");
        go.transform.SetParent(parent, false);

        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        AddTonemapping(profile);
        AddBloom(profile);
        AddColorGrading(profile);
        AddVignette(profile);
        AddDepthOfField(profile);

        return volume;
    }

    // ACES: киношная кривая, которая не выжигает светлое в белый лист.
    // Именно она делает свет над татами «горячим», а не плоским.
    private static void AddTonemapping(VolumeProfile profile)
    {
        Tonemapping t = profile.Add<Tonemapping>(true);
        t.mode.overrideState = true;
        t.mode.value = TonemappingMode.ACES;
    }

    // Свечение только по-настоящему ярких мест: порог выше единицы, иначе
    // белое кимоно начинает светиться и картинка мылится.
    private static void AddBloom(VolumeProfile profile)
    {
        Bloom b = profile.Add<Bloom>(true);
        b.threshold.overrideState = true;
        b.threshold.value = 1.15f;
        b.intensity.overrideState = true;
        b.intensity.value = 0.55f;
        b.scatter.overrideState = true;
        b.scatter.value = 0.62f;
        // Высокое качество на мобиле не окупается: разница видна только
        // при паузе, а стоит заметную часть кадра.
        b.highQualityFiltering.overrideState = true;
        b.highQualityFiltering.value = false;
    }

    private static void AddColorGrading(VolumeProfile profile)
    {
        ColorAdjustments c = profile.Add<ColorAdjustments>(true);
        c.postExposure.overrideState = true;
        c.postExposure.value = 0.15f;
        c.contrast.overrideState = true;
        c.contrast.value = 14f;
        c.saturation.overrideState = true;
        c.saturation.value = 8f;

        // Холодные тени против тёплого света сверху: разделение по цвету
        // читается как объём даже там, где нет теней.
        ShadowsMidtonesHighlights s = profile.Add<ShadowsMidtonesHighlights>(true);
        s.shadows.overrideState = true;
        s.shadows.value = new Vector4(0.86f, 0.94f, 1.12f, 0f);
        s.highlights.overrideState = true;
        s.highlights.value = new Vector4(1.06f, 1.01f, 0.92f, 0f);
    }

    private static void AddVignette(VolumeProfile profile)
    {
        Vignette v = profile.Add<Vignette>(true);
        v.intensity.overrideState = true;
        v.intensity.value = 0.26f;
        v.smoothness.overrideState = true;
        v.smoothness.value = 0.42f;
    }

    // Глубина резкости включается только в добивании: постоянное размытие
    // фона на маленьком экране читается как грязь, а на финише —
    // как кинематографический акцент.
    private static DepthOfField _dof;

    private static void AddDepthOfField(VolumeProfile profile)
    {
        _dof = profile.Add<DepthOfField>(true);
        _dof.mode.overrideState = true;
        _dof.mode.value = DepthOfFieldMode.Bokeh;
        _dof.focusDistance.overrideState = true;
        _dof.focusDistance.value = 3.2f;
        _dof.aperture.overrideState = true;
        _dof.aperture.value = 12f;
        _dof.focalLength.overrideState = true;
        _dof.focalLength.value = 45f;
        _dof.active = false;
    }

    public static void SetCinematic(bool on, float focusDistance)
    {
        if (_dof == null) return;
        _dof.active = on;
        if (on) _dof.focusDistance.value = Mathf.Max(0.2f, focusDistance);
    }
}
