using UnityEngine;

// Фонарь, который зажигается к ночи.
//
// Раньше все источники в мире горели с одинаковой силой круглые сутки:
// днём они не были нужны и только съедали бюджет, а ночью их не хватало,
// и деревня тонула в темноте. Здесь сила света идёт за временем суток —
// днём тлеет, ночью светит в полную.
//
// Плавность через SmoothStep, а не по прямой: свет, нарастающий линейно,
// заметно «включается» на закате, и вечер выглядит щелчком выключателя.
public class NightLight : MonoBehaviour
{
    public float DayIntensity = 0.2f;
    public float NightIntensity = 2.6f;

    private Light _light;
    // Ореол гаснет вместе со светом: горящая днём клякса выдаёт фонарь
    // нарисованным.
    private Renderer _halo;
    private Color _haloColor;

    public static NightLight Attach(Light light, float dayI, float nightI, Renderer halo)
    {
        if (light == null) return null;
        NightLight n = light.gameObject.AddComponent<NightLight>();
        n._light = light;
        n.DayIntensity = dayI;
        n.NightIntensity = nightI;
        n._halo = halo;
        if (halo != null && halo.sharedMaterial != null)
            n._haloColor = halo.sharedMaterial.color;
        n.Apply();
        return n;
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (_light == null) return;
        float k = Mathf.SmoothStep(0f, 1f, DayCycle.Darkness);
        _light.intensity = Mathf.Lerp(DayIntensity, NightIntensity, k);

        if (_halo == null || _halo.sharedMaterial == null) return;
        Color c = _haloColor;
        c.a = _haloColor.a * Mathf.Lerp(0.25f, 1f, k);
        // Свой материал у каждого ореола: AdditiveMat создаёт его на
        // каждый вызов и в общий кэш не кладёт, поэтому правка здесь
        // соседей не задевает. Цвет пишем в оба свойства — аддитивный
        // шейдер держит оттенок в _TintColor, а не в _Color.
        _halo.sharedMaterial.color = c;
        if (_halo.sharedMaterial.HasProperty("_TintColor"))
            _halo.sharedMaterial.SetColor("_TintColor", c);
    }
}
