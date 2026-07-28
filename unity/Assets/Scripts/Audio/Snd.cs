using System.Collections.Generic;
using UnityEngine;

// Процедурный звук: все эффекты и фоновая музыка синтезируются кодом
// при запуске, внешних аудиофайлов нет.
public class Snd : MonoBehaviour
{
    public const int Rate = 22050;

    public static Snd I;

    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> _pool = new List<AudioSource>();
    private int _poolIndex;
    private AudioSource _music;
    private bool _muted;

    public static bool Muted { get { return I != null && I._muted; } }

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
        for (int i = 0; i < 8; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            _pool.Add(s);
        }
        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.loop = true;
        _music.volume = 0.22f;

        GenerateSfx();
        SetTheme(0);
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

    public static void SetMuted(bool m)
    {
        if (I == null) return;
        I._muted = m;
        AudioListener.volume = m ? 0f : 1f;
    }

    // ---------- Синтез ----------

    private static AudioClip FromSamples(string name, float[] buf, bool loop)
    {
        AudioClip clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // wave: 0 = синус, 1 = меандр, 2 = пила
    private static float[] Synth(float duration, float f0, float f1, int wave,
        float attack, float decayPow, float vol, float noiseMix)
    {
        int n = Mathf.Max(1, (int)(duration * Rate));
        float[] buf = new float[n];
        double phase = 0.0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2.0 * Mathf.PI * f / Rate;
            float s;
            if (wave == 1) s = Mathf.Sign(Mathf.Sin((float)phase)) * 0.6f;
            else if (wave == 2) s = (2f * Mathf.Repeat((float)phase / (2f * Mathf.PI), 1f) - 1f) * 0.7f;
            else s = Mathf.Sin((float)phase);

            if (noiseMix > 0f) s = Mathf.Lerp(s, Random.value * 2f - 1f, noiseMix);
            float env = Mathf.Min(t / Mathf.Max(attack, 0.0001f), 1f) * Mathf.Pow(1f - t, decayPow);
            buf[i] = s * env * vol;
        }
        return buf;
    }

    // notes: [частота, старт(сек), длительность(сек), громкость]
    private static float[] MixNotes(float total, float[][] notes, float vol)
    {
        int n = Mathf.Max(1, (int)(total * Rate));
        float[] buf = new float[n];
        for (int k = 0; k < notes.Length; k++)
        {
            float freq = notes[k][0];
            int i0 = (int)(notes[k][1] * Rate);
            int cnt = Mathf.Max(1, (int)(notes[k][2] * Rate));
            float amp = notes[k].Length > 3 ? notes[k][3] : 1f;
            for (int j = 0; j < cnt; j++)
            {
                int idx = i0 + j;
                if (idx >= n) break;
                float t = (float)j / cnt;
                float env = Mathf.Min(t * 40f, 1f) * Mathf.Pow(1f - t, 2.5f);
                buf[idx] += Mathf.Sin(2f * Mathf.PI * freq * j / Rate) * env * amp;
            }
        }
        for (int i = 0; i < n; i++) buf[i] = Mathf.Clamp(buf[i] * vol, -1f, 1f);
        return buf;
    }

    private void Add(string name, float[] samples)
    {
        _clips[name] = FromSamples(name, samples, false);
    }

    private void GenerateSfx()
    {
        Add("click", Synth(0.06f, 1200f, 900f, 0, 0.005f, 1.2f, 0.25f, 0f));
        Add("jump", Synth(0.18f, 320f, 640f, 0, 0.01f, 1.4f, 0.4f, 0f));
        Add("land", Synth(0.12f, 130f, 60f, 0, 0.005f, 1.5f, 0.35f, 0f));
        Add("swing", Synth(0.16f, 500f, 180f, 0, 0.02f, 1.2f, 0.3f, 0.85f));
        Add("stomp", Synth(0.22f, 300f, 80f, 1, 0.005f, 1.5f, 0.4f, 0.3f));
        Add("hurt", Synth(0.3f, 260f, 150f, 2, 0.005f, 1.4f, 0.35f, 0.2f));
        Add("portal", Synth(0.55f, 200f, 900f, 0, 0.25f, 1.2f, 0.32f, 0.25f));
        Add("bounce", Synth(0.24f, 180f, 720f, 0, 0.005f, 1.1f, 0.42f, 0f));
        Add("crystal", MixNotes(0.5f, new float[][] {
            new float[] { 1318.5f, 0f, 0.2f, 0.9f },
            new float[] { 1975.5f, 0.09f, 0.3f, 0.6f }
        }, 0.3f));

        Add("coin", MixNotes(0.32f, new float[][] {
            new float[] { 1046.5f, 0f, 0.14f, 1f },
            new float[] { 1568f, 0.07f, 0.22f, 0.8f }
        }, 0.4f));

        Add("quest", MixNotes(0.4f, new float[][] {
            new float[] { 880f, 0f, 0.16f, 1f },
            new float[] { 1174.7f, 0.1f, 0.28f, 0.9f }
        }, 0.35f));

        Add("splash", Synth(0.3f, 900f, 200f, 2, 0.01f, 1.3f, 0.3f, 0.7f));
        Add("star", MixNotes(0.9f, new float[][] {
            new float[] { 659.26f, 0f, 0.18f, 1f },
            new float[] { 987.77f, 0.12f, 0.2f, 0.9f },
            new float[] { 1318.5f, 0.24f, 0.5f, 1.1f }
        }, 0.42f));
        Add("bosshit", Synth(0.26f, 220f, 70f, 1, 0.005f, 1.6f, 0.5f, 0.4f));

        Add("victory", MixNotes(1.1f, new float[][] {
            new float[] { 523.25f, 0f, 0.2f, 1f },
            new float[] { 659.26f, 0.16f, 0.2f, 1f },
            new float[] { 783.99f, 0.32f, 0.2f, 1f },
            new float[] { 1046.5f, 0.48f, 0.6f, 1.2f }
        }, 0.45f));
    }

    private int _theme = -1;

    // У каждого мира своя тема: свой лад, темп и голос баса.
    // Треки синтезируются один раз и кэшируются вместе с эффектами.
    public static void SetTheme(int world)
    {
        if (I == null || I._theme == world) return;
        I._theme = world;
        string key = "music" + world;
        AudioClip clip;
        if (!I._clips.TryGetValue(key, out clip))
        {
            clip = I.MakeMusic(world);
            I._clips[key] = clip;
        }
        I._music.clip = clip;
        I._music.volume = world == 4 ? 0.16f : 0.22f;
        if (!I._muted) I._music.Play();
    }

    private AudioClip MakeMusic(int world)
    {
        // Деревня — мажор, луга светлее и быстрее, каньон на увеличенном
        // ладу, снега прозрачные, пещера мрачная, лагуна покачивается.
        float[] tempo = { 0.5f, 0.44f, 0.52f, 0.6f, 0.72f, 0.56f };
        float beat = tempo[Mathf.Clamp(world, 0, tempo.Length - 1)];

        float[][][] palettes = new float[][][] {
            // Деревня: C - Am - F - G
            new float[][] {
                new float[] { 261.63f, 329.63f, 392f },
                new float[] { 220f, 261.63f, 329.63f },
                new float[] { 174.61f, 220f, 261.63f },
                new float[] { 196f, 246.94f, 293.66f } },
            // Луга: D - Bm - G - A
            new float[][] {
                new float[] { 293.66f, 369.99f, 440f },
                new float[] { 246.94f, 293.66f, 369.99f },
                new float[] { 196f, 246.94f, 293.66f },
                new float[] { 220f, 277.18f, 329.63f } },
            // Каньон: увеличенные интервалы, восточный оттенок
            new float[][] {
                new float[] { 261.63f, 311.13f, 392f },
                new float[] { 233.08f, 293.66f, 349.23f },
                new float[] { 207.65f, 261.63f, 311.13f },
                new float[] { 246.94f, 311.13f, 369.99f } },
            // Снега: открытые квинты, много воздуха
            new float[][] {
                new float[] { 349.23f, 440f, 523.25f },
                new float[] { 293.66f, 392f, 466.16f },
                new float[] { 261.63f, 349.23f, 440f },
                new float[] { 311.13f, 392f, 493.88f } },
            // Пещера: минор с пониженной второй
            new float[][] {
                new float[] { 174.61f, 207.65f, 261.63f },
                new float[] { 155.56f, 196f, 233.08f },
                new float[] { 138.59f, 174.61f, 207.65f },
                new float[] { 164.81f, 196f, 246.94f } },
            // Лагуна: покачивающийся мажор
            new float[][] {
                new float[] { 246.94f, 311.13f, 369.99f },
                new float[] { 207.65f, 261.63f, 311.13f },
                new float[] { 185f, 233.08f, 277.18f },
                new float[] { 220f, 277.18f, 329.63f } }
        };
        float[][] chords = palettes[Mathf.Clamp(world, 0, palettes.Length - 1)];

        // Раньше тема была ровно четыре такта — восемь секунд в деревне,
        // и на второй минуте от неё уже дёргался глаз. Теперь шестнадцать:
        // восьмитактовая гармония проходит дважды, во второй половине
        // мелодия уходит октавой выше и меняет рисунок.
        int[] progression = { 0, 1, 2, 3, 0, 2, 1, 3 };

        // Мелодические фигуры — номера ступеней в наборе аккорда.
        // Соседние такты берут разные, поэтому рисунок не повторяется подряд.
        int[][] motifs = {
            new[] { 0, 1, 2, 1, 2, 3, 2, 1 },
            new[] { 2, 1, 0, 1, 2, 2, 3, 2 },
            new[] { 0, 2, 1, 3, 2, 0, 1, 0 },
            new[] { 3, 2, 1, 0, 1, 2, 3, 2 }
        };

        List<float[]> notes = new List<float[]>();
        const int Bars = 16;
        for (int b = 0; b < Bars; b++)
        {
            float[] chord = chords[progression[b % progression.Length]];
            float barT = b * beat * 4f;
            bool second = b >= Bars / 2;
            // Ступени: тоника, терция, квинта и терция октавой выше.
            float[] pool = { chord[0], chord[1], chord[2], chord[1] * 2f };

            notes.Add(new float[] { chord[0] * 0.5f, barT, 0.9f, 0.9f });
            notes.Add(new float[] { chord[0] * 0.5f, barT + beat * 2f, 0.9f, 0.7f });
            // Во второй половине бас отвечает квинтой на слабую долю.
            if (second)
                notes.Add(new float[] { chord[2] * 0.5f, barT + beat * 3f, 0.6f, 0.5f });

            // Последний такт каждой половины оставляем почти пустым —
            // без паузы петля звучит как заевшая пластинка.
            bool breath = b % (Bars / 2) == (Bars / 2 - 1);
            if (!breath)
            {
                int[] motif = motifs[(b * 3 + (second ? 1 : 0)) % motifs.Length];
                float octave = second ? 2f : 1f;
                for (int k = 0; k < motif.Length; k++)
                    notes.Add(new float[] {
                        pool[motif[k]] * octave, barT + k * beat * 0.5f, 0.35f,
                        second ? 0.34f : 0.45f });
            }

            notes.Add(new float[] { chord[2] * 2f, barT + beat, 0.7f, 0.2f });
        }

        float total = Bars * beat * 4f;
        return FromSamples("music" + world, MixNotes(total, notes.ToArray(), 0.33f), true);
    }
}
