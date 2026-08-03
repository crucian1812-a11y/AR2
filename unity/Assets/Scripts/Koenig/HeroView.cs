using UnityEngine;

namespace Koenig
{
    // Живой персонаж на экране: модель проводника медленно крутится на
    // подставке далеко под миром, а картинка уходит в интерфейс через
    // RenderTexture. Ровно тот же приём, что у витрины лавки в игре про
    // медведя (ShopPreview) — камере не нужен отдельный слой, потому что
    // вокруг подставки на километры пусто, а основная камера её не видит
    // (её far-plane близко, подставка — на y=-4000).
    //
    // Именно этого не хватало игре-путешествию: были карта, сумка и
    // головоломка, но самого героя не было видно ни разу. Теперь Кёня
    // стоит на экране и оживает.
    public class HeroView : MonoBehaviour
    {
        private const float StageY = -4000f;

        private Camera _cam;
        private Transform _pivot;
        private CharacterModel _model;
        private RenderTexture _rt;
        private float _spin;

        public RenderTexture Texture { get { return _rt; } }

        // stageSlot разносит подставки, чтобы два вида (на карте и на экране
        // героя) не светили и не залезали друг на друга.
        public static HeroView Create(Transform parent, string modelId, int rtW, int rtH,
            int stageSlot, float spinSpeed)
        {
            GameObject go = new GameObject("HeroView");
            go.transform.SetParent(parent, false);
            HeroView v = go.AddComponent<HeroView>();
            v._spin = spinSpeed;
            v.Setup(modelId, rtW, rtH, stageSlot);
            return v;
        }

        private void Setup(string modelId, int rtW, int rtH, int stageSlot)
        {
            Vector3 stage = new Vector3(stageSlot * 80f, StageY, 0f);

            _rt = new RenderTexture(rtW, rtH, 16);
            _rt.antiAliasing = 2;
            _rt.Create();

            GameObject camGo = new GameObject("Cam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = stage + new Vector3(0f, 1.05f, -2.7f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.1f, 0.2f, 0.32f, 1f);
            _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 12f;
            _cam.targetTexture = _rt;

            // Точечный свет, не направленный: направленный залил бы всю
            // сцену, а нам нужна только подставка.
            GameObject key = new GameObject("Key");
            key.transform.SetParent(transform, false);
            key.transform.position = stage + new Vector3(1.4f, 2.4f, -2.1f);
            Light l = key.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 10f;
            l.intensity = 3.3f;
            l.color = new Color(1f, 0.96f, 0.88f);
            l.shadows = LightShadows.None;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(transform, false);
            fill.transform.position = stage + new Vector3(-1.7f, 1.1f, -1.6f);
            Light f = fill.AddComponent<Light>();
            f.type = LightType.Point;
            f.range = 8f;
            f.intensity = 1.4f;
            f.color = new Color(0.55f, 0.72f, 1f);
            f.shadows = LightShadows.None;

            GameObject pivotGo = new GameObject("Pivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.position = stage;
            _pivot = pivotGo.transform;
            _pivot.localRotation = Quaternion.Euler(0f, 16f, 0f);

            _model = CharacterModel.Spawn(_pivot, modelId, 1.7f);
            if (_model != null) _model.Play(_model.Pick("Idle", "Walk"), 1f, true);
        }

        public void SetActive(bool on)
        {
            if (_cam != null) _cam.enabled = on;
        }

        private void Update()
        {
            if (_pivot == null) return;
            // Time.timeScale в оверлеях может быть нулевым — крутим по
            // неотмасштабированному времени, иначе персонаж замирает.
            _pivot.Rotate(0f, _spin * Time.unscaledDeltaTime, 0f);
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); _rt = null; }
        }
    }
}
