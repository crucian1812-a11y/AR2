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

    public static FighterRig Create(Transform parent, Side who, Color gi, Color rim)
    {
        GameObject holder = new GameObject("Fighter" + who);
        holder.transform.SetParent(parent, false);

        FighterRig rig = holder.AddComponent<FighterRig>();
        rig.Who = who;
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

    private Material MakeMaterial(string name, Color gi, Color rim)
    {
        string low = name.ToLowerInvariant();

        if (low.StartsWith("skin"))
        {
            Material m = new Material(Shader.Find("Bjj/Skin"));
            m.SetColor("_Color", new Color(0.80f, 0.60f, 0.47f));
            m.SetColor("_RimColor", rim);
            m.SetFloat("_RimStrength", 0.35f);
            _skin.Add(m);
            return m;
        }

        if (low.StartsWith("gidark"))
        {
            Material m = ClothMaterial(gi * 0.62f, rim);
            return m;
        }
        if (low.StartsWith("gi"))
        {
            return ClothMaterial(gi, rim);
        }
        if (low.StartsWith("belt"))
        {
            Material m = ClothMaterial(new Color(0.05f, 0.05f, 0.06f), rim);
            m.SetFloat("_SheenStrength", 0.25f);
            return m;
        }
        if (low.StartsWith("hair"))
        {
            Material m = new Material(Shader.Find("Bjj/Skin"));
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
            Material m = new Material(Shader.Find("Bjj/Skin"));
            m.SetColor("_Color", new Color(0.05f, 0.045f, 0.05f));
            m.SetFloat("_SubsurfaceStrength", 0f);
            m.SetFloat("_Smoothness", 0.85f);
            return m;
        }

        return Arena.Lit(gi);
    }

    private Material ClothMaterial(Color color, Color rim)
    {
        Material m = new Material(Shader.Find("Bjj/Cloth"));
        m.SetColor("_Color", color);
        m.SetColor("_RimColor", rim);
        m.SetFloat("_RimStrength", 0.4f);
        _cloth.Add(m);
        return m;
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
