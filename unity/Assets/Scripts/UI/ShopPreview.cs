using UnityEngine;

// Живая витрина героя: модель медленно крутится на подставке, а картинка
// уходит в интерфейс через RenderTexture.
//
// Покупать героя по одному имени в строке — всё равно что выбирать
// вслепую: из двадцати девяти имён игрок не знает ни одного. Поэтому
// показываем саму модель, ту же самую, которой он будет играть.
public class ShopPreview : MonoBehaviour
{
    // Подставка стоит далеко под миром. Так камере не нужен отдельный
    // слой отрисовки: заводить слой пришлось бы в ProjectSettings, то
    // есть тащить в репозиторий ещё один файл настроек ради витрины, а
    // здесь достаточно того, что вокруг на километры ничего нет.
    private const float StageY = -4000f;

    private Camera _cam;
    private Transform _pivot;
    private CharacterModel _model;
    private RenderTexture _rt;
    private int _shown = -1;

    public RenderTexture Texture { get { return _rt; } }

    // Каждой витрине своя подставка: два точечных источника света рядом
    // засвечивали бы соседа.
    public static ShopPreview Create(Transform parent, int slot)
    {
        GameObject go = new GameObject("ShopPreview" + slot);
        go.transform.SetParent(parent, false);
        ShopPreview p = go.AddComponent<ShopPreview>();
        p.Setup(slot);
        return p;
    }

    private void Setup(int slot)
    {
        Vector3 stage = new Vector3(slot * 60f, StageY, 0f);

        _rt = new RenderTexture(288, 324, 16);
        _rt.antiAliasing = 2;
        _rt.Create();

        GameObject camGo = new GameObject("Cam");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = stage + new Vector3(0f, 1.02f, -2.75f);
        camGo.transform.rotation = Quaternion.Euler(3.5f, 0f, 0f);
        _cam = camGo.AddComponent<Camera>();
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.07f, 0.10f, 0.17f, 1f);
        _cam.fieldOfView = 40f;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 12f;
        _cam.targetTexture = _rt;
        // Пока лавка закрыта, камера выключена и ничего не стоит.
        _cam.enabled = false;

        // Свет точечный, а не направленный. Направленный в Unity светит
        // всей сцене независимо от того, где стоит — ради одной витрины
        // перекрасился бы весь уровень.
        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.position = stage + new Vector3(1.5f, 2.5f, -2.2f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 10f;
        l.intensity = 3.4f;
        l.color = new Color(1f, 0.96f, 0.9f);
        l.shadows = LightShadows.None;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(transform, false);
        fill.transform.position = stage + new Vector3(-1.8f, 1.2f, -1.6f);
        Light f = fill.AddComponent<Light>();
        f.type = LightType.Point;
        f.range = 8f;
        f.intensity = 1.3f;
        f.color = new Color(0.6f, 0.75f, 1f);
        f.shadows = LightShadows.None;

        GameObject pivotGo = new GameObject("Pivot");
        pivotGo.transform.SetParent(transform, false);
        pivotGo.transform.position = stage;
        _pivot = pivotGo.transform;
    }

    public void SetActive(bool on)
    {
        if (_cam != null) _cam.enabled = on;
    }

    public void Show(int charIndex)
    {
        charIndex = Heroes.Clamp(charIndex);
        if (charIndex == _shown) return;
        _shown = charIndex;

        if (_model != null) Object.Destroy(_model.gameObject);
        _model = CharacterModel.Spawn(_pivot, Heroes.Id(charIndex), Heroes.BodyHeight);
        if (_model != null) _model.Play(_model.Pick("Idle", "Walk"), 1f, true);
        _pivot.localRotation = Quaternion.Euler(0f, 18f, 0f);
    }

    private void Update()
    {
        if (_cam == null || !_cam.enabled || _pivot == null) return;
        // В одиночной игре лавка ставит timeScale в ноль, поэтому
        // deltaTime здесь всегда нулевой: витрина замирала бы ровно
        // тогда, когда на неё смотрят. Считаем по неотмасштабированному.
        _pivot.Rotate(0f, 42f * Time.unscaledDeltaTime, 0f);
    }

    private void OnDestroy()
    {
        if (_rt != null)
        {
            _rt.Release();
            _rt = null;
        }
    }
}
