using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Постобработка кадра через URP-Volume: ACES-тонмаппинг, bloom,
// цветокоррекция и виньетка. Профиль собирается кодом — ассетов в
// проекте нет принципиально.
// Вешается на камеру локального игрока, настройки задаёт текущий мир.
public class PostFx : MonoBehaviour
{
    public float Intensity = 1.05f;
    public float Vignette = 0.55f;
    public float Saturation = 1.12f;
    public float Contrast = 1.06f;
    public Color Tint = new Color(1.02f, 1.0f, 0.98f);

    private Volume _volume;
    private Bloom _bloom;
    private ColorAdjustments _grade;
    private Vignette _vignette;

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                // FXAA — на телефоне дешевле SMAA и достаточно на такой картинке.
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = true;
            }
        }

        BuildProfile();
        Apply();
    }

    private void BuildProfile()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Тонмаппинг — главное, ради чего затевался URP: яркие места
        // перестают выгорать в белое и держат цвет.
        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        _bloom = profile.Add<Bloom>(true);
        _bloom.threshold.Override(1.1f);
        _bloom.scatter.Override(0.7f);
        _bloom.intensity.Override(Intensity);

        _grade = profile.Add<ColorAdjustments>(true);
        _grade.postExposure.Override(0.15f);
        _grade.saturation.Override(0f);
        _grade.contrast.Override(0f);

        _vignette = profile.Add<Vignette>(true);
        _vignette.intensity.Override(0.25f);
        _vignette.smoothness.Override(0.6f);

        // Дальний план мягче — персонаж читается в фокусе.
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(45f);
        dof.gaussianEnd.Override(160f);
        dof.gaussianMaxRadius.Override(0.8f);

        // Едва заметное зерно снимает «пластиковость» градиентов неба.
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.18f);

        ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(0.08f);

        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f;
        _volume.weight = 1f;
        _volume.profile = profile;
    }

    // Позволяет мирам менять настроение картинки (снег холоднее, пещера темнее).
    public void Configure(float intensity, float saturation, Color tint, float vignette)
    {
        Intensity = intensity;
        Saturation = saturation;
        Tint = tint;
        Vignette = vignette;
        Apply();
    }

    private void Apply()
    {
        if (_bloom == null) return;
        _bloom.intensity.Override(Mathf.Max(Intensity - 0.4f, 0f));

        // Насыщенность и контраст в URP задаются в процентах от -100 до 100,
        // а миры присылают множитель около единицы.
        _grade.saturation.Override(Mathf.Clamp((Saturation - 1f) * 100f, -100f, 100f));
        _grade.contrast.Override(Mathf.Clamp((Contrast - 1f) * 100f, -100f, 100f));
        _grade.colorFilter.Override(Tint);

        _vignette.intensity.Override(Mathf.Clamp01(Vignette * 0.45f));
    }
}
