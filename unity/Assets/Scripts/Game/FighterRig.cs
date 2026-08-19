using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Боец на экране: модель, анимация и материалы.
//
// Анимация играется через **Playables**, а не через AnimatorController.
// Причина простая: контроллер — это ассет, а в репозитории ассетов нет,
// сцена и всё остальное собирается кодом. Playables проигрывают клип
// напрямую и вдобавок дают ровно то, что здесь нужно, — смешивание двух
// клипов с управляемым весом, то есть плавный переход между позициями.
//
// Ядро (Match) об этом классе ничего не знает: оно меняет позицию, а
// сюда приходит только «играй такой-то клип».
public class FighterRig : MonoBehaviour
{
    private Animator _animator;
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationClipPlayable _current;
    private AnimationClipPlayable _previous;

    private float _blend = 1f;
    private float _blendSpeed = 6f;
    private string _playing = "";

    private readonly List<Material> _cloth = new List<Material>();
    private readonly List<Material> _skin = new List<Material>();

    public Side Who { get; private set; }

    // Пояс бойца: цвет разряда и число нашивок. Второе шейдер сравнивает
    // с номером, запечённым в маске, — заслуженные полоски остаются белыми,
    // остальные красятся вместе с полотном.
    private Color _belt = Color.white;
    private int _stripes;
    private Color _skinTone = Color.white;

    public static FighterRig Create(Transform parent, Side who, Color gi, Color rim,
                                    Belt belt, int stripes, Color skin, float build)
    {
        GameObject holder = new GameObject("Fighter" + who);
        holder.transform.SetParent(parent, false);
        // Рост задаётся масштабом всего бойца — вместе с корнем, который
        // двигает клип. Отклонения держим малыми: парная анимация ставит
        // обоих в одну точку, и разошедшийся масштаб развёл бы захваты.
        holder.transform.localScale = Vector3.one * build;

        FighterRig rig = holder.AddComponent<FighterRig>();
        rig.Who = who;
        rig._belt = Career.BeltColor(belt);
        rig._stripes = stripes;
        rig._skinTone = skin;
        rig.Build(gi, rim);
        return rig;
    }

    private void Build(Color gi, Color rim)
    {
        GameObject prefab = Res.FighterPrefab();
        if (prefab == null)
        {
            // Модель не собралась — не роняем игру, показываем капсулу.
            // Без этого пропущенный шаг сборки анимаций превращался бы в
            // чёрный экран вместо понятной ошибки.
            Debug.LogError("FighterRig: модель не найдена, показываю заглушку");
            GameObject stub = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            stub.transform.SetParent(transform, false);
            stub.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            stub.GetComponent<Renderer>().sharedMaterial = Arena.Lit(gi);
            return;
        }

        GameObject model = Object.Instantiate(prefab, transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        _animator = model.GetComponent<Animator>();
        if (_animator == null) _animator = model.AddComponent<Animator>();
        // Корень двигает код, а не клип: положение бойца на татами задаёт
        // игра, иначе за серию переходов бойцы уползают с ковра.
        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        ApplyMaterials(model, gi, rim);
        ClothBones.Attach(model);
        BuildGraph();
    }

    // Материалы из FBX заменяются на игровые шейдеры по имени: в Blender
    // они названы Gi, GiDark, Skin, Belt, Hair, Eye, и это единственная
    // связь между моделью и рендером — так материалы не нужно хранить
    // ассетами.
    private void ApplyMaterials(GameObject model, Color gi, Color rim)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] source = renderers[r].sharedMaterials;
            Material[] result = new Material[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                string name = source[i] != null ? source[i].name : "";
                result[i] = MakeMaterial(name, gi, rim);
            }
            renderers[r].sharedMaterials = result;

            // Тени от бойцов обязательны: без них фигуры «плавают» над
            // татами, и никакой свет этого не исправит.
            renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderers[r].receiveShadows = true;
        }
    }

    // Шейдер с запасным вариантом. Материал, созданный из null, ничего не
    // рисует — боец становится невидимым, и по картинке не догадаешься,
    // что причина в шейдере. Один раз это уже случилось: TEXCOORD8 не
    // компилировался на Android, и оба бойца выходили прозрачными.
    private static Material Shaded(string wanted)
    {
        Shader sh = Shader.Find(wanted);
        if (sh == null)
        {
            Debug.LogError("FighterRig: шейдер " + wanted +
                           " не найден — беру запасной Bjj/Lit");
            sh = Shader.Find("Bjj/Lit");
        }
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        return new Material(sh);
    }

    private Material MakeMaterial(string name, Color gi, Color rim)
    {
        string low = name.ToLowerInvariant();

        if (low.StartsWith("skin"))
        {
            Material m = Shaded("Bjj/Skin");
            // Цвет здесь — множитель к текстуре, а не сам тон: тон
            // запечён в альбедо вместе с бровями, губами и щетиной, а
            // отсюда приходит только оттенок конкретного бойца.
            m.SetColor("_Color", _skinTone);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", 0.35f);
            Maps(m, "skin");
            _skin.Add(m);
            return m;
        }

        if (low.StartsWith("gidark"))
        {
            return ClothMaterial(gi * 0.62f, rim, "gidark");
        }
        if (low.StartsWith("gi"))
        {
            return ClothMaterial(gi, rim, "gi");
        }
        if (low.StartsWith("belt"))
        {
            // Полотно пояса напечатано светлым и нейтральным, цвет разряда
            // приходит отсюда. Иначе пришлось бы печь пять разных атласов
            // или мириться с тем, что белый пояс невозможно получить
            // умножением из чёрного.
            Material m = ClothMaterial(_belt, rim, "belt");
            m.SetFloat("_SheenStrength", 0.25f);
            m.SetFloat("_Stripes", _stripes);
            return m;
        }
        if (low.StartsWith("hair"))
        {
            Material m = Shaded("Bjj/Skin");
            m.SetColor("_Color", new Color(0.09f, 0.07f, 0.06f));
            m.SetFloat("_WrapAmount", 0.2f);
            m.SetFloat("_SubsurfaceStrength", 0.1f);
            m.SetFloat("_Smoothness", 0.4f);
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", 0.5f);
            return m;
        }
        if (low.StartsWith("eye"))
        {
            Material m = Shaded("Bjj/Skin");
            m.SetColor("_Color", new Color(0.05f, 0.045f, 0.05f));
            m.SetFloat("_SubsurfaceStrength", 0f);
            m.SetFloat("_Smoothness", 0.85f);
            return m;
        }

        return Arena.Lit(gi);
    }

    private Material ClothMaterial(Color color, Color rim, string maps)
    {
        Material m = Shaded("Bjj/Cloth");
        m.SetColor("_Color", color);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimStrength", 0.4f);
        Maps(m, maps);
        _cloth.Add(m);
        return m;
    }

    // Три карты на материал. Если какой-то нет, шейдер получит значение
    // по умолчанию («white»/«bump») и просто останется гладким — сборка
    // без текстур не должна падать чёрным экраном.
    private static void Maps(Material m, string set)
    {
        Texture2D albedo = Res.Map(set, "albedo");
        Texture2D normal = Res.Map(set, "normal");
        Texture2D orm = Res.Map(set, "orm");

        if (albedo != null) m.SetTexture("_MainTex", albedo);
        if (normal != null) m.SetTexture("_BumpMap", normal);
        if (orm != null) m.SetTexture("_ORM", orm);
    }

    // Граф: микшер на два входа. Новый клип приходит на вход 0, прежний
    // уезжает на входе 1 — этого достаточно для любого перехода.
    private void BuildGraph()
    {
        if (_animator == null) return;

        _graph = PlayableGraph.Create("FighterRig" + Who);
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        _mixer = AnimationMixerPlayable.Create(_graph, 2);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Anim", _animator);
        output.SetSourcePlayable(_mixer);
        _graph.Play();
    }

    /// Проиграть клип. `blend` — за сколько секунд подмешать его.
    public void Play(string clipName, bool loop, float blend = 0.22f)
    {
        if (_animator == null || clipName == _playing) return;

        AnimationClip clip = Res.Clip(clipName);
        if (clip == null)
        {
            Debug.LogWarning("FighterRig: нет клипа " + clipName);
            return;
        }

        if (_previous.IsValid()) _previous.Destroy();
        _previous = _current;

        _current = AnimationClipPlayable.Create(_graph, clip);
        _current.SetApplyFootIK(false);
        if (!loop) _current.SetDuration(clip.length);

        _mixer.DisconnectInput(0);
        _mixer.DisconnectInput(1);
        if (_previous.IsValid()) _mixer.ConnectInput(1, _previous, 0);
        _mixer.ConnectInput(0, _current, 0);

        _blend = 0f;
        _blendSpeed = blend > 0.001f ? 1f / blend : 1000f;
        _playing = clipName;
    }

    private void Update()
    {
        if (!_graph.IsValid()) return;

        if (_blend < 1f)
        {
            _blend = Mathf.Min(1f, _blend + Time.deltaTime * _blendSpeed);
            // Плавность по косинусу: линейное смешивание даёт заметный
            // рывок в начале и в конце перехода.
            float w = _blend * _blend * (3f - 2f * _blend);
            _mixer.SetInputWeight(0, w);
            _mixer.SetInputWeight(1, 1f - w);
        }
    }

    /// Насыщенность потом, 0..1. Идёт во все материалы бойца сразу.
    public void SetSweat(float v)
    {
        v = Mathf.Clamp01(v);
        for (int i = 0; i < _cloth.Count; i++) _cloth[i].SetFloat("_Sweat", v);
        for (int i = 0; i < _skin.Count; i++) _skin[i].SetFloat("_Sweat", v);
    }

    private void OnDestroy()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }
}
