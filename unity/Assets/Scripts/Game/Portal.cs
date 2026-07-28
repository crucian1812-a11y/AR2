using UnityEngine;

// Портал между мирами: светящееся кольцо с анимированным водоворотом.
// Портал в Снежные вершины закрыт, пока не выполнено задание.
public class Portal : MonoBehaviour
{
    public int Target;
    public string Label = "";
    public Color Tint = new Color(0.4f, 1f, 0.6f);
    public bool Locked;

    // Подсказка «почему закрыто»: портал выставляет её, когда игрок
    // подошёл вплотную, а GameRoot показывает и стирает.
    public string PendingHint;

    private Material _ringMat;
    private GameObject _swirl;
    private WorldLabel _label;
    private float _cooldown;
    private float _hintCd;
    // Числа, которые сейчас нарисованы на табличке. Пока они не изменились,
    // пересобирать её незачем.
    private int _shownStars = -1;
    private int _shownStage = -1;

    public static Portal Spawn(Transform parent, Vector3 pos, int target, string label, Color color,
        float yaw)
    {
        GameObject go = new GameObject("Portal_" + target);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Portal p = go.AddComponent<Portal>();
        p.Target = target;
        p.Label = label;
        p.Tint = color;
        p.Build();
        return p;
    }

    private void Build()
    {
        _ringMat = new Material(Gfx.Standard);
        _ringMat.color = Tint;
        _ringMat.SetFloat("_Glossiness", 0.7f);
        _ringMat.SetFloat("_Metallic", 0.4f);
        _ringMat.SetColor("_EmissionColor", Tint * 0.8f);

        GameObject ring = Gfx.Torus(transform, new Vector3(0f, 0.2f, 0f), 1.25f, 0.17f, _ringMat);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        _swirl = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Collider sc = _swirl.GetComponent<Collider>();
        if (sc != null) Object.Destroy(sc);
        _swirl.name = "Swirl";
        _swirl.transform.SetParent(transform, false);
        _swirl.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        _swirl.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        MeshRenderer smr = _swirl.GetComponent<MeshRenderer>();
        Shader ps = Shader.Find("Bear/Portal");
        Material sm = ps != null ? new Material(ps) : Gfx.AdditiveMat(Tint, Gfx.GlowTexture());
        sm.SetColor("_Color", Tint);
        smr.sharedMaterial = sm;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.receiveShadows = false;

        Gfx.Glow(transform, new Vector3(0f, 0.2f, 0f), 4f, new Color(Tint.r, Tint.g, Tint.b, 0.3f));

        _label = WorldLabel.Attach(transform, Label, new Vector3(0f, 2.7f, 0f), Color.white, 24);
        Refresh();
    }

    // Сколько звёзд нужно, чтобы этот портал открылся.
    public int NeedStars
    {
        get
        {
            int required = NetManager.RequiredStage(Target);
            if (required >= 4) return NetManager.QuestStarsFinal;
            if (required == 3) return NetManager.QuestStarsCity;
            if (required == 2) return NetManager.QuestStars;
            return 0;
        }
    }

    public void Refresh()
    {
        NetManager net = NetManager.I;
        int required = NetManager.RequiredStage(Target);
        _shownStage = net != null ? net.QuestStage : 0;
        _shownStars = net != null ? net.StarsTotal : 0;
        Locked = _shownStage < required;

        if (Locked)
        {
            _ringMat.color = new Color(0.4f, 0.4f, 0.45f);
            _ringMat.SetColor("_EmissionColor", Color.black);
            _swirl.SetActive(false);
            // Раньше здесь всегда висел счётчик звёзд, и портал мог
            // показывать «2 / 2», оставаясь закрытым: до первой стадии
            // задание вообще не начато, пока не поговоришь со старейшиной.
            _label.Text = Label + "\n" + (_shownStage == 0
                ? "(поговори со старейшиной)"
                : "(звёзд: " + _shownStars + " / " + NeedStars + ")");
            _label.Tint = new Color(0.82f, 0.82f, 0.85f);
        }
        else
        {
            _ringMat.color = Tint;
            _ringMat.SetColor("_EmissionColor", Tint * 0.8f);
            _swirl.SetActive(true);
            _label.Text = Label;
            _label.Tint = Color.white;
        }
    }

    // Возвращает true, если игрок вошёл в портал и переход разрешён.
    public bool TryEnter(Vector3 playerPos)
    {
        if (_cooldown > 0f) return false;
        Vector3 d = transform.position - playerPos;
        d.y = 0f;
        if (d.sqrMagnitude > 1.5f * 1.5f) return false;
        if (Mathf.Abs(transform.position.y - playerPos.y) > 2.5f) return false;

        if (Locked)
        {
            // Молчащий портал выглядел сломанным: игрок стоял в нём и
            // не понимал, чего не хватает. Теперь он говорит об этом сам.
            if (_hintCd <= 0f)
            {
                _hintCd = 5f;
                PendingHint = _shownStage == 0
                    ? "Закрыто. Расспроси старейшину в деревне."
                    : "Закрыто. Нужно звёзд: " + NeedStars + ", собрано " + _shownStars + ".";
            }
            return false;
        }

        _cooldown = 2f;
        Snd.Play("portal");
        return true;
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        if (_hintCd > 0f) _hintCd -= Time.deltaTime;

        // Счётчик звёзд на табличке раньше обновлялся только при смене
        // стадии задания — собранная звезда на нём не появлялась.
        NetManager net = NetManager.I;
        if (net != null && (net.StarsTotal != _shownStars || net.QuestStage != _shownStage))
            Refresh();
    }
}
