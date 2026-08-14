using System.Collections.Generic;
using UnityEngine;

// Звук синтезируется кодом при запуске — аудиофайлов в проекте нет, как
// нет сцен и текстур.
//
// Для борьбы это оказалось удачнее, чем библиотека сэмплов. Здесь почти
// нет «событийных» звуков вроде удара: почти всё — шорох ткани, дыхание,
// возня на татами, то есть шум с огибающей. Такое синтезируется точнее,
// чем ищется, и даром параметризуется: то же дыхание становится тяжелее
// от усталости сдвигом двух чисел.
public class Snd : MonoBehaviour
{
    public const int Rate = 22050;

    public static Snd I;

    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> _pool = new List<AudioSource>();
    private int _poolIndex;

    private AudioSource _crowd;      // ровный гул зала
    private AudioSource _tension;    // низкий гул напряжения

    private static System.Random _rng = new System.Random(12345);

    public static void Create()
    {
        if (I != null) return;
        GameObject go = new GameObject("Snd");
        DontDestroyOnLoad(go);
        I = go.AddComponent<Snd>();
        I.Init();
    }

    private void Init()
    {
        for (int i = 0; i < 10; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            _pool.Add(s);
        }

        Generate();

        _crowd = gameObject.AddComponent<AudioSource>();
        _crowd.clip = _clips["crowd"];
        _crowd.loop = true;
        _crowd.volume = 0.30f;
        _crowd.Play();

        _tension = gameObject.AddComponent<AudioSource>();
        _tension.clip = _clips["tension"];
        _tension.loop = true;
        _tension.volume = 0.0f;
        _tension.Play();
    }

    public static void Play(string name, float volume = 1f, float pitch = 1f)
    {
        if (I == null) return;
        AudioClip clip;
        if (!I._clips.TryGetValue(name, out clip)) return;

        AudioSource src = I._pool[I._poolIndex];
        I._poolIndex = (I._poolIndex + 1) % I._pool.Count;
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.Play();
    }

    /// Громкость гула зала: растёт, когда идёт борьба за позицию.
    public static void SetCrowd(float level)
    {
        if (I == null || I._crowd == null) return;
        I._crowd.volume = Mathf.Lerp(I._crowd.volume, Mathf.Clamp01(level) * 0.55f + 0.16f,
                                     Time.unscaledDeltaTime * 1.6f);
    }

    /// Напряжение: низкий гул под доминирующей позицией и сабмишном.
    public static void SetTension(float level)
    {
        if (I == null || I._tension == null) return;
        I._tension.volume = Mathf.Lerp(I._tension.volume, Mathf.Clamp01(level) * 0.30f,
                                       Time.unscaledDeltaTime * 1.2f);
    }

    // ------------------------------------------------------------ синтез

    private static float Rnd()
    {
        return (float)_rng.NextDouble() * 2f - 1f;
    }

    private static AudioClip Make(string name, float[] buf)
    {
        AudioClip clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // Однополюсный фильтр низких частот. Один коэффициент, а разница
    // между «шипением» и «шорохом ткани» — целиком в нём.
    private static void LowPass(float[] buf, float cutoff)
    {
        float a = Mathf.Clamp01(cutoff);
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            y += (buf[i] - y) * a;
            buf[i] = y;
        }
    }

    private static void HighPass(float[] buf, float cutoff)
    {
        float a = Mathf.Clamp01(cutoff);
        float y = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            y += (buf[i] - y) * a;
            buf[i] = buf[i] - y;
        }
    }

    private static void Normalize(float[] buf, float peak)
    {
        float max = 0.0001f;
        for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
        float k = peak / max;
        for (int i = 0; i < buf.Length; i++) buf[i] *= k;
    }

    private static float[] Noise(float seconds)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        float[] buf = new float[n];
        for (int i = 0; i < n; i++) buf[i] = Rnd();
        return buf;
    }

    // Огибающая «атака — спад» степенной формы.
    private static void Envelope(float[] buf, float attack, float decayPow)
    {
        int n = buf.Length;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float a = Mathf.Min(t / Mathf.Max(attack, 0.0001f), 1f);
            buf[i] *= a * Mathf.Pow(1f - t, decayPow);
        }
    }

    private void Generate()
    {
        _clips["crowd"] = Make("crowd", Crowd(6f));
        _clips["roar"] = Make("roar", Roar(2.6f));
        _clips["tension"] = Make("tension", Tension(4f));

        _clips["thud"] = Make("thud", Thud());
        _clips["cloth"] = Make("cloth", Cloth());
        _clips["grip"] = Make("grip", Grip());
        _clips["breath"] = Make("breath", Breath());
        _clips["bell"] = Make("bell", Bell());
        _clips["whistle"] = Make("whistle", Whistle());
        _clips["tap"] = Make("tap", Tap());
        _clips["click"] = Make("click", Click());
    }

    // Гул зала: розоватый шум с медленными наплывами. Ровный белый шум
    // звучит как помехи, а не как люди.
    private static float[] Crowd(float seconds)
    {
        float[] buf = Noise(seconds);
        LowPass(buf, 0.035f);
        LowPass(buf, 0.10f);

        int n = buf.Length;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            // Три несоизмеримых периода: наплывы не должны повторяться
            // заметно, иначе петля выдаёт себя за пару проходов.
            float swell = 1f
                + 0.30f * Mathf.Sin(t * 0.41f)
                + 0.18f * Mathf.Sin(t * 0.97f + 1.3f)
                + 0.12f * Mathf.Sin(t * 1.73f + 2.7f);
            buf[i] *= swell;
        }

        // Сшивка петли: последние полсекунды переходят в первые.
        int fade = (int)(0.5f * Rate);
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            buf[n - fade + i] = Mathf.Lerp(buf[n - fade + i], buf[fade - i - 1], k);
        }

        Normalize(buf, 0.5f);
        return buf;
    }

    // Рёв на добивании: тот же шум, но ярче, с быстрым нарастанием.
    private static float[] Roar(float seconds)
    {
        float[] buf = Noise(seconds);
        LowPass(buf, 0.12f);

        int n = buf.Length;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Min(t * 9f, 1f) * Mathf.Pow(1f - t, 1.4f);
            // Свист и хлопки поверх гула.
            float tops = Rnd() * 0.25f * Mathf.Pow(1f - t, 2.2f);
            buf[i] = (buf[i] + tops) * env;
        }
        Normalize(buf, 0.85f);
        return buf;
    }

    // Низкий гул напряжения: две расстроенные пилы под порогом слышимости
    // мелодии. Задача — не мелодия, а давление.
    private static float[] Tension(float seconds)
    {
        int n = (int)(seconds * Rate);
        float[] buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float a = Mathf.Repeat(t * 55f, 1f) * 2f - 1f;
            float b = Mathf.Repeat(t * 55.4f, 1f) * 2f - 1f;
            float sub = Mathf.Sin(2f * Mathf.PI * 27.5f * t);
            buf[i] = (a + b) * 0.18f + sub * 0.5f;
        }
        LowPass(buf, 0.05f);

        int fade = (int)(0.4f * Rate);
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            buf[n - fade + i] = Mathf.Lerp(buf[n - fade + i], buf[fade - i - 1], k);
        }
        Normalize(buf, 0.6f);
        return buf;
    }

    // Падение тела на татами: низкий удар плюс глухой шлепок мата.
    private static float[] Thud()
    {
        int n = (int)(0.42f * Rate);
        float[] buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            // Тон уходит вниз — так звучит любой удар по большой упругой
            // поверхности.
            float f = Mathf.Lerp(92f, 44f, t);
            float tone = Mathf.Sin(2f * Mathf.PI * f * i / Rate);
            float body = Rnd() * 0.5f;
            buf[i] = (tone * 0.8f + body) * Mathf.Pow(1f - t, 3.2f);
        }
        LowPass(buf, 0.18f);
        Normalize(buf, 0.9f);
        return buf;
    }

    // Шорох кимоно: шум в средних частотах с быстрым спадом.
    private static float[] Cloth()
    {
        float[] buf = Noise(0.30f);
        HighPass(buf, 0.35f);
        LowPass(buf, 0.55f);
        Envelope(buf, 0.06f, 2.4f);
        Normalize(buf, 0.5f);
        return buf;
    }

    // Захват: короткий рывок ткани, жёстче и выше шороха.
    private static float[] Grip()
    {
        float[] buf = Noise(0.18f);
        HighPass(buf, 0.5f);
        Envelope(buf, 0.02f, 3.4f);
        Normalize(buf, 0.65f);
        return buf;
    }

    // Выдох. Шум, отфильтрованный под голосовой тракт, с лёгким тоном.
    private static float[] Breath()
    {
        int n = (int)(0.55f * Rate);
        float[] buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float voiced = Mathf.Sin(2f * Mathf.PI * 118f * i / Rate) * 0.18f;
            buf[i] = (Rnd() + voiced) * Mathf.Min(t * 5f, 1f) * Mathf.Pow(1f - t, 1.9f);
        }
        LowPass(buf, 0.22f);
        HighPass(buf, 0.02f);
        Normalize(buf, 0.55f);
        return buf;
    }

    // Гонг. Металл звучит негармоничными обертонами — целые кратности
    // дали бы орган, а не удар по металлу.
    private static float[] Bell()
    {
        int n = (int)(2.2f * Rate);
        float[] buf = new float[n];
        float[] partials = { 1.0f, 2.76f, 5.40f, 8.93f, 13.34f };
        float[] amps = { 1.0f, 0.62f, 0.40f, 0.22f, 0.12f };
        float baseF = 420f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float s = 0f;
            for (int k = 0; k < partials.Length; k++)
            {
                // Высокие обертоны гаснут быстрее — так и затухает металл.
                float decay = Mathf.Pow(1f - t, 1.6f + k * 0.9f);
                s += Mathf.Sin(2f * Mathf.PI * baseF * partials[k] * i / Rate) * amps[k] * decay;
            }
            buf[i] = s * Mathf.Min(t * 300f, 1f);
        }
        Normalize(buf, 0.75f);
        return buf;
    }

    private static float[] Whistle()
    {
        int n = (int)(0.5f * Rate);
        float[] buf = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            // Дрожание высоты — свисток трещит горошиной.
            float trill = Mathf.Sin(2f * Mathf.PI * 28f * t) * 40f;
            float f = 2500f + trill;
            buf[i] = Mathf.Sin(2f * Mathf.PI * f * i / Rate) * Mathf.Min(t * 20f, 1f)
                     * Mathf.Pow(1f - t, 1.2f);
            buf[i] += Rnd() * 0.08f * Mathf.Pow(1f - t, 2f);
        }
        Normalize(buf, 0.5f);
        return buf;
    }

    // Хлопки сдачи: три быстрых удара ладонью по мату.
    private static float[] Tap()
    {
        int n = (int)(0.75f * Rate);
        float[] buf = new float[n];
        int[] starts = { 0, (int)(0.18f * Rate), (int)(0.36f * Rate) };

        for (int s = 0; s < starts.Length; s++)
        {
            int len = (int)(0.13f * Rate);
            for (int i = 0; i < len; i++)
            {
                int idx = starts[s] + i;
                if (idx >= n) break;
                float t = (float)i / len;
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 70f, t) * i / Rate);
                buf[idx] += (tone * 0.7f + Rnd() * 0.6f) * Mathf.Pow(1f - t, 3.6f);
            }
        }
        LowPass(buf, 0.30f);
        Normalize(buf, 0.85f);
        return buf;
    }

    private static float[] Click()
    {
        float[] buf = Noise(0.05f);
        HighPass(buf, 0.6f);
        Envelope(buf, 0.01f, 4f);
        Normalize(buf, 0.35f);
        return buf;
    }
}
