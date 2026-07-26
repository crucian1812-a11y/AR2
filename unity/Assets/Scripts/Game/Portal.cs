using UnityEngine;

// Портал между мирами: светящееся кольцо с анимированным водоворотом.
// Портал в Снежные вершины закрыт, пока не выполнено задание.
public class Portal : MonoBehaviour
{
    public int Target;
    public string Label = "";
    public Color Tint = new Color(0.4f, 1f, 0.6f);
    public bool Locked;

    private Material _ringMat;
    private GameObject _swirl;
    private WorldLabel _label;
    private float _cooldown;

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

    public void Refresh()
    {
        NetManager net = NetManager.I;
        int required = NetManager.RequiredStage(Target);
        if (net != null) Locked = net.QuestStage < required;

        if (Locked)
        {
            int need = required >= 3 ? NetManager.QuestCoinsFinal : NetManager.QuestCoins;
            int have = net != null ? net.CoinsTotal : 0;
            _ringMat.color = new Color(0.4f, 0.4f, 0.45f);
            _ringMat.SetColor("_EmissionColor", Color.black);
            _swirl.SetActive(false);
            _label.Text = Label + "\n(монет: " + have + " / " + need + ")";
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
        if (Locked) return false;
        Vector3 d = transform.position - playerPos;
        d.y = 0f;
        if (d.sqrMagnitude > 1.5f * 1.5f) return false;
        if (Mathf.Abs(transform.position.y - playerPos.y) > 2.5f) return false;
        _cooldown = 2f;
        Snd.Play("portal");
        return true;
    }

    private void Update()
    {
        if (_cooldown > 0f) _cooldown -= Time.deltaTime;
    }
}
