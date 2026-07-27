using System.Collections.Generic;
using UnityEngine;

// Загруженная из Resources модель монстра: подбирает материал с её
// текстурой, нормирует рост под нужную высоту и проигрывает клипы по
// логическим именам. Клипы лежат внутри FBX (Legacy-риг), поэтому
// AnimatorController не нужен — играем через компонент Animation.
public class CharacterModel : MonoBehaviour
{
    private Animation _anim;
    private readonly Dictionary<string, string> _clips = new Dictionary<string, string>();
    private string _current = "";

    public Renderer[] Renderers { get; private set; }
    public float Height { get; private set; }

    // Возвращает null, если модель не нашлась — вызывающий код тогда
    // строит запасного персонажа из примитивов.
    public static CharacterModel Spawn(Transform parent, string id, float targetHeight)
    {
        if (string.IsNullOrEmpty(id)) return null;
        GameObject prefab = Resources.Load<GameObject>("Models/monsters/" + id);
        if (prefab == null)
        {
            Debug.LogWarning("CharacterModel: модель не найдена — " + id);
            return null;
        }

        GameObject go = Object.Instantiate(prefab);
        go.name = id;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        CharacterModel m = go.AddComponent<CharacterModel>();
        m.Setup(id, targetHeight);
        return m;
    }

    private void Setup(string id, float targetHeight)
    {
        Renderers = GetComponentsInChildren<Renderer>();
        ApplyMaterial(id);
        Normalize(targetHeight);
        CollectClips();
    }

    // В FBX материалы не импортируются (см. ModelImportSettings), поэтому
    // собираем свой: одна текстура-атлас на всю модель.
    private void ApplyMaterial(string id)
    {
        Texture2D tex = Resources.Load<Texture2D>("Textures/monsters/" + id + "_Texture");
        Material m = new Material(Gfx.Standard);
        m.color = Color.white;
        m.SetFloat("_Glossiness", 0.08f);
        m.SetFloat("_Metallic", 0f);
        if (tex != null) m.mainTexture = tex;
        else Debug.LogWarning("CharacterModel: текстура не найдена — " + id);

        for (int i = 0; i < Renderers.Length; i++)
        {
            if (Renderers[i] == null) continue;
            Renderers[i].sharedMaterial = m;
            Renderers[i].receiveShadows = true;
        }
    }

    // Модели в паке разного размера; приводим к общему росту и ставим
    // ступни в ноль, чтобы персонаж не проваливался и не парил.
    private void Normalize(float targetHeight)
    {
        if (Renderers == null || Renderers.Length == 0 || targetHeight <= 0f) return;

        // Считаем по мешам, а не по Renderer.bounds: у скиненных мешей
        // мировые границы обновляются только после первого кадра.
        bool has = false;
        float minY = 0f, maxY = 0f;
        for (int i = 0; i < Renderers.Length; i++)
        {
            Renderer r = Renderers[i];
            if (r == null) continue;

            Mesh mesh = null;
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null) mesh = smr.sharedMesh;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            if (mesh == null) continue;

            // Границы меша переводим в пространство корня модели.
            Matrix4x4 toRoot = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Bounds mb = mesh.bounds;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = new Vector3(
                    (c & 1) == 0 ? mb.min.x : mb.max.x,
                    (c & 2) == 0 ? mb.min.y : mb.max.y,
                    (c & 4) == 0 ? mb.min.z : mb.max.z);
                float y = toRoot.MultiplyPoint3x4(corner).y;
                if (!has) { minY = y; maxY = y; has = true; }
                else
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        float h = maxY - minY;
        if (!has || h < 0.0001f) return;

        float k = targetHeight / h;
        transform.localScale = new Vector3(k, k, k);
        // Ступни ставим в ноль родителя.
        transform.localPosition = new Vector3(0f, -minY * k, 0f);
        Height = targetHeight;
    }

    // Имена клипов приводим к нижнему регистру и отрезаем префикс арматуры,
    // если импортёр его почему-то оставил.
    private void CollectClips()
    {
        _anim = GetComponentInChildren<Animation>();
        if (_anim == null) return;
        foreach (AnimationState st in _anim)
        {
            if (st == null || string.IsNullOrEmpty(st.name)) continue;
            string key = st.name;
            int bar = key.LastIndexOf('|');
            if (bar >= 0 && bar + 1 < key.Length) key = key.Substring(bar + 1);
            key = key.ToLowerInvariant();
            if (!_clips.ContainsKey(key)) _clips[key] = st.name;
        }
    }

    public bool Has(string logical)
    {
        return _anim != null && logical != null && _clips.ContainsKey(logical.ToLowerInvariant());
    }

    // Первый из перечисленных клипов, который есть у модели. Нужен потому,
    // что у летающих монстров нет Idle и Walk, зато есть Flying.
    public string Pick(string a, string b, string c)
    {
        if (Has(a)) return a;
        if (Has(b)) return b;
        if (Has(c)) return c;
        return null;
    }

    public void Play(string logical, float speed, bool loop, float fade = 0.15f)
    {
        if (_anim == null || logical == null) return;
        string real;
        if (!_clips.TryGetValue(logical.ToLowerInvariant(), out real)) return;

        AnimationState st = _anim[real];
        if (st == null) return;
        st.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        st.speed = speed;

        if (real == _current && _anim.IsPlaying(real)) return;
        _current = real;
        if (fade > 0f) _anim.CrossFade(real, fade);
        else _anim.Play(real);
    }

    // Перезапуск клипа с нуля — для удара и получения урона, которые
    // должны срабатывать даже если тот же клип уже играет.
    public void Restart(string logical, float speed)
    {
        if (_anim == null || logical == null) return;
        string real;
        if (!_clips.TryGetValue(logical.ToLowerInvariant(), out real)) return;
        AnimationState st = _anim[real];
        if (st == null) return;
        st.wrapMode = WrapMode.ClampForever;
        st.speed = speed;
        st.time = 0f;
        _current = real;
        _anim.CrossFade(real, 0.06f);
    }

    public void SetVisible(bool v)
    {
        if (Renderers == null) return;
        for (int i = 0; i < Renderers.Length; i++)
            if (Renderers[i] != null) Renderers[i].enabled = v;
    }
}
