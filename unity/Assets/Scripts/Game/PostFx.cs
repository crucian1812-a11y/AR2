using UnityEngine;

// Постобработка кадра: bloom, цветокоррекция, виньетка.
// Вешается на камеру локального игрока. Размытие считается в четверть
// разрешения — на телефоне это почти бесплатно.
public class PostFx : MonoBehaviour
{
    // Порог высокий намеренно: светиться должны источники света и блики,
    // а не любая освещённая поверхность — иначе кадр выцветает.
    public float Threshold = 0.9f;
    public float SoftKnee = 0.35f;
    public float Intensity = 1.05f;
    public int BlurIterations = 3;
    public float BlurSpread = 1.35f;
    public float Vignette = 0.55f;
    public float Saturation = 1.12f;
    public float Contrast = 1.06f;
    public Color Tint = new Color(1.02f, 1.0f, 0.98f);

    private Material _mat;
    private bool _failed;

    private void Awake()
    {
        Shader shader = Shader.Find("Bear/Post");
        if (shader == null)
        {
            _failed = true;
            Debug.LogWarning("PostFx: shader Bear/Post not found, effect disabled");
            return;
        }
        _mat = new Material(shader);
    }

    // Позволяет мирам менять настроение картинки (снег холоднее, пещера темнее).
    public void Configure(float intensity, float saturation, Color tint, float vignette)
    {
        Intensity = intensity;
        Saturation = saturation;
        Tint = tint;
        Vignette = vignette;
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (_failed || _mat == null)
        {
            Graphics.Blit(src, dst);
            return;
        }

        int w = Mathf.Max(2, src.width / 4);
        int h = Mathf.Max(2, src.height / 4);

        _mat.SetFloat("_Threshold", Threshold);
        _mat.SetFloat("_SoftKnee", SoftKnee);
        _mat.SetFloat("_Intensity", Intensity);
        _mat.SetFloat("_Vignette", Vignette);
        _mat.SetFloat("_Saturation", Saturation);
        _mat.SetFloat("_Contrast", Contrast);
        _mat.SetVector("_Tint", new Vector4(Tint.r, Tint.g, Tint.b, 1f));

        RenderTexture bright = RenderTexture.GetTemporary(w, h, 0, src.format);
        RenderTexture temp = RenderTexture.GetTemporary(w, h, 0, src.format);
        bright.filterMode = FilterMode.Bilinear;
        temp.filterMode = FilterMode.Bilinear;

        Graphics.Blit(src, bright, _mat, 0);

        int iterations = Mathf.Clamp(BlurIterations, 1, 6);
        for (int i = 0; i < iterations; i++)
        {
            float spread = BlurSpread * (1f + i * 0.6f);
            _mat.SetVector("_BlurDir", new Vector4(spread, 0f, 0f, 0f));
            Graphics.Blit(bright, temp, _mat, 1);
            _mat.SetVector("_BlurDir", new Vector4(0f, spread, 0f, 0f));
            Graphics.Blit(temp, bright, _mat, 1);
        }

        _mat.SetTexture("_BloomTex", bright);
        Graphics.Blit(src, dst, _mat, 2);

        RenderTexture.ReleaseTemporary(bright);
        RenderTexture.ReleaseTemporary(temp);
    }
}
