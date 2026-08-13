using UnityEngine;

// Показ бойца на этапе серого бокса: капсула, голова и пояс.
//
// Смысл этого класса — держать всю «телесность» в одном месте. Когда
// появятся модели Quaternius, меняется только он: ядро (Match) о капсулах
// ничего не знает и знать не должно.
public class FighterView : MonoBehaviour
{
    private Transform _body;
    private Vector3 _targetPos;
    private Quaternion _targetRot;

    public Side Who { get; private set; }

    public static FighterView Create(Transform parent, Side who, Color gi)
    {
        GameObject go = new GameObject("Fighter" + who);
        go.transform.SetParent(parent, false);
        FighterView f = go.AddComponent<FighterView>();
        f.Who = who;
        f.Build(gi);
        return f;
    }

    private void Build(Color gi)
    {
        Material giMat = Arena.Lit(gi, 0.05f);
        Material skinMat = Arena.Lit(new Color(0.85f, 0.68f, 0.55f), 0.12f);
        Material beltMat = Arena.Lit(new Color(0.06f, 0.06f, 0.07f), 0.2f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
        body.GetComponent<Renderer>().sharedMaterial = giMat;
        Object.Destroy(body.GetComponent<Collider>());
        _body = body.transform;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(body.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        head.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
        head.GetComponent<Renderer>().sharedMaterial = skinMat;
        Object.Destroy(head.GetComponent<Collider>());

        // Пояс — не украшение: он показывает, где у капсулы «перёд», и
        // без него в партере невозможно понять, кто как лежит.
        GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        belt.name = "Belt";
        belt.transform.SetParent(body.transform, false);
        belt.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        belt.transform.localScale = new Vector3(1.08f, 0.16f, 1.08f);
        belt.GetComponent<Renderer>().sharedMaterial = beltMat;
        Object.Destroy(belt.GetComponent<Collider>());
    }

    // Позы партера задаются положением и наклоном капсулы. Это заглушка
    // ровно до этапа M2: настоящие позы придут анимациями из Blender,
    // но граф позиций уже сейчас читается на глаз.
    public void Apply(Vector3 pos, Vector3 euler, bool lying)
    {
        _targetPos = pos;
        _targetRot = Quaternion.Euler(euler);
        if (_body != null)
            _body.localPosition = new Vector3(0f, lying ? 0.42f : 0.95f, 0f);
    }

    private void Update()
    {
        // Плавное доведение: мгновенный телепорт между позициями читается
        // как баг, даже когда он задуман.
        transform.localPosition = Vector3.Lerp(transform.localPosition, _targetPos, Time.deltaTime * 7f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, _targetRot, Time.deltaTime * 7f);
    }
}
