using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Играбельный уровень города — цифровая половина «двух ключей». Пока
    // сделан один: Зеленоградск, приморский город кошек. Ребёнок бегает за
    // Кёню по набережной, собирает рыбок, а потом забирает у башни ЦИФРОВОЙ
    // жетон моста. Настоящий жетон появится уже на месте (Journey) — и
    // только вместе они делают мост золотым (DigitalProgress.IsFused).
    //
    // Самодостаточно: свой мир из примитивов с настоящими коллайдерами,
    // свой герой (KoenigPlayer), свои сенсорные кнопки (KoenigTouch) и своя
    // камера с высоким depth — она перекрывает экран карты, пока играем.
    public class KoenigLevel : MonoBehaviour
    {
        private System.Action _onExit;
        private string _cityId;
        private KoenigPlayer _player;
        private Transform _world;

        private readonly List<Transform> _fish = new List<Transform>();
        private int _collected, _total;
        private Transform _token;
        private bool _tokenReady, _won;

        private Canvas _hud;
        private Text _count, _hint;

        // Пока уровень есть только у Зеленоградска. Остальные города —
        // следующие срезы по этому же образцу.
        public static bool HasLevel(string cityId)
        {
            return cityId == "zeln";
        }

        public static KoenigLevel Create(Transform parent, string cityId, System.Action onExit)
        {
            GameObject go = new GameObject("KoenigLevel");
            go.transform.SetParent(parent, false);
            KoenigLevel lvl = go.AddComponent<KoenigLevel>();
            lvl._cityId = cityId;
            lvl._onExit = onExit;
            lvl.Setup();
            return lvl;
        }

        private void Setup()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Свет для 3D: на экранах-меню его не было (там один UI).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.62f);
            RenderSettings.fog = false;
            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.localRotation = Quaternion.Euler(52f, 40f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.88f);
            sun.shadows = LightShadows.Soft;

            GameObject worldGo = new GameObject("World");
            worldGo.transform.SetParent(transform, false);
            _world = worldGo.transform;

            BuildZelenogradsk();

            _player = KoenigPlayer.Spawn(transform, new Vector3(0f, 1.5f, 9f), Guide.ModelId);

            BuildHud();
            KoenigTouch.Create(_hud.transform);
        }

        // ---------- Мир Зеленоградска ----------

        private void BuildZelenogradsk()
        {
            // Песчаная набережная.
            Material sand = Gfx.Mat(new Color(0.83f, 0.74f, 0.55f), 0.05f);
            Gfx.Box(_world, new Vector3(0f, -0.5f, 0f), new Vector3(38f, 1f, 38f), sand, true);

            // Балтика за дальним парапетом (только вид, туда не ходим).
            Material sea = Gfx.Mat(new Color(0.18f, 0.42f, 0.62f), 0.6f, 0.1f);
            Gfx.Box(_world, new Vector3(0f, -0.7f, 36f), new Vector3(90f, 0.7f, 42f), sea, false);

            // Парапеты по краям — не дают убежать с набережной.
            Material rail = Gfx.Mat(new Color(0.72f, 0.68f, 0.6f), 0.1f);
            Gfx.Box(_world, new Vector3(0f, 0.6f, 18.6f), new Vector3(38f, 1.2f, 0.6f), rail, true);
            Gfx.Box(_world, new Vector3(0f, 0.6f, -18.6f), new Vector3(38f, 1.2f, 0.6f), rail, true);
            Gfx.Box(_world, new Vector3(18.6f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 38f), rail, true);
            Gfx.Box(_world, new Vector3(-18.6f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 38f), rail, true);

            // Водонапорная башня (Мурариум) — узнаваемая доминанта города и
            // место, где ждёт жетон.
            Tower(new Vector3(10f, 0f, -11f));

            // Кошки Зеленоградска — бронзовые фигурки, как настоящие в городе.
            Cat(new Vector3(-8f, 0f, -6f));
            Cat(new Vector3(6f, 0f, 4f));
            Cat(new Vector3(-4f, 0f, 8f));
            Cat(new Vector3(12f, 0f, 6f));

            // Фонари набережной.
            Lamp(new Vector3(-12f, 0f, 12f));
            Lamp(new Vector3(12f, 0f, 12f));
            Lamp(new Vector3(0f, 0f, -14f));

            // Рыбки — то, что собирает кот. Разбросаны по набережной.
            Vector3[] spots = {
                new Vector3(-6f, 1f, 2f), new Vector3(4f, 1f, -3f),
                new Vector3(-10f, 1f, -8f), new Vector3(8f, 1f, 10f),
                new Vector3(0f, 1f, 6f), new Vector3(-2f, 1f, -12f),
            };
            for (int i = 0; i < spots.Length; i++) _fish.Add(Fish(spots[i]));
            _total = _fish.Count;

            // Жетон моста у башни — появится, когда собраны все рыбки.
            _token = Token(new Vector3(10f, 1.1f, -8f));
            _token.gameObject.SetActive(false);
        }

        private void Tower(Vector3 pos)
        {
            Material brick = Gfx.Mat(new Color(0.66f, 0.34f, 0.26f), 0.08f);
            Material band = Gfx.Mat(new Color(0.86f, 0.82f, 0.74f), 0.1f);
            Gfx.Cyl(_world, pos + new Vector3(0f, 2.4f, 0f), new Vector3(2.6f, 2.4f, 2.6f), brick, true);
            Gfx.Cyl(_world, pos + new Vector3(0f, 5.2f, 0f), new Vector3(2.2f, 0.4f, 2.2f), band, true);
            Gfx.Cyl(_world, pos + new Vector3(0f, 7f, 0f), new Vector3(2.3f, 1.6f, 2.3f), brick, true);
            Gfx.Cone(_world, pos + new Vector3(0f, 8.8f, 0f), 2.6f, 2.2f,
                Gfx.Mat(new Color(0.3f, 0.35f, 0.42f), 0.2f));
            // Часы-кружок на башне.
            Gfx.Ball(_world, pos + new Vector3(0f, 6.9f, 2.3f), new Vector3(0.9f, 0.9f, 0.2f), band);
        }

        private void Cat(Vector3 pos)
        {
            Material bronze = Gfx.MatFull(new Color(0.42f, 0.3f, 0.16f), 0.35f, 0.6f, Color.black, 0f, 0f);
            Transform holder = new GameObject("Cat").transform;
            holder.SetParent(_world, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Gfx.Ball(holder, new Vector3(0f, 0.35f, 0f), new Vector3(0.5f, 0.45f, 0.7f), bronze);   // тело
            Gfx.Ball(holder, new Vector3(0f, 0.72f, 0.28f), new Vector3(0.42f, 0.4f, 0.4f), bronze); // голова
            Gfx.Ball(holder, new Vector3(-0.14f, 0.98f, 0.28f), new Vector3(0.14f, 0.2f, 0.08f), bronze); // ухо
            Gfx.Ball(holder, new Vector3(0.14f, 0.98f, 0.28f), new Vector3(0.14f, 0.2f, 0.08f), bronze);  // ухо
            GameObject tail = Gfx.Cyl(holder, new Vector3(0f, 0.5f, -0.42f),
                new Vector3(0.1f, 0.28f, 0.1f), bronze, false);
            tail.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);
        }

        private void Lamp(Vector3 pos)
        {
            Material iron = Gfx.Mat(new Color(0.2f, 0.22f, 0.26f), 0.3f, 0.5f);
            Gfx.Cyl(_world, pos + new Vector3(0f, 1.5f, 0f), new Vector3(0.16f, 1.5f, 0.16f), iron, false);
            Gfx.Ball(_world, pos + new Vector3(0f, 3.1f, 0f), new Vector3(0.5f, 0.6f, 0.5f),
                Gfx.MatFull(new Color(1f, 0.95f, 0.7f), 0.5f, 0f, new Color(1f, 0.9f, 0.55f), 0f, 0f));
            Gfx.Glow(_world, pos + new Vector3(0f, 3.1f, 0f), 2.4f, new Color(1f, 0.9f, 0.6f, 0.5f));
        }

        private Transform Fish(Vector3 pos)
        {
            Transform f = new GameObject("Fish").transform;
            f.SetParent(_world, false);
            f.localPosition = pos;
            Material gold = Gfx.MatFull(new Color(1f, 0.82f, 0.35f), 0.6f, 0.2f,
                new Color(0.6f, 0.45f, 0.1f), 0f, 0f);
            Gfx.Ball(f, new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0.28f, 0.7f), gold);
            GameObject tail = Gfx.Ball(f, new Vector3(0f, 0f, -0.42f), new Vector3(0.3f, 0.28f, 0.18f), gold);
            tail.transform.localScale = new Vector3(0.3f, 0.28f, 0.18f);
            Gfx.Glow(f, Vector3.zero, 1.3f, new Color(1f, 0.85f, 0.4f, 0.5f));
            return f;
        }

        private Transform Token(Vector3 pos)
        {
            Transform t = new GameObject("BridgeToken").transform;
            t.SetParent(_world, false);
            t.localPosition = pos;
            Material gold = Gfx.MatFull(new Color(1f, 0.84f, 0.32f), 0.7f, 0.5f,
                new Color(0.7f, 0.5f, 0.12f), 0f, 0f);
            // Маленькая арка-мост.
            Gfx.Box(t, new Vector3(-0.5f, 0f, 0f), new Vector3(0.2f, 0.7f, 0.2f), gold, false);
            Gfx.Box(t, new Vector3(0.5f, 0f, 0f), new Vector3(0.2f, 0.7f, 0.2f), gold, false);
            Gfx.Box(t, new Vector3(0f, 0.42f, 0f), new Vector3(1.4f, 0.2f, 0.2f), gold, false);
            Gfx.Glow(t, new Vector3(0f, 0.2f, 0f), 2.6f, new Color(1f, 0.85f, 0.35f, 0.6f));
            return t;
        }

        // ---------- Интерфейс ----------

        private void BuildHud()
        {
            _hud = UiKit.CreateCanvas("KoenigLevelHud", 30);
            _hud.transform.SetParent(transform, false);
            Transform c = _hud.transform;
            float s = UiKit.Scale;
            float H = Screen.height, W = Screen.width;

            UiKit.MakeText(c, new Vector2(W * 0.5f, H - 34f * s), new Vector2(420f * s, 40f * s),
                "Зеленоградск — город кошек", Mathf.RoundToInt(22f * s),
                new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);

            _count = UiKit.MakeText(c, new Vector2(W - 120f * s, H - 34f * s), new Vector2(200f * s, 40f * s),
                "", Mathf.RoundToInt(20f * s), Color.white, TextAnchor.MiddleRight);

            UiKit.MakeButton(c, new Vector2(90f * s, H - 34f * s), new Vector2(150f * s, 46f * s),
                "← Карта", Mathf.RoundToInt(16f * s), Exit);

            _hint = UiKit.MakeText(c, new Vector2(W * 0.5f, H - 74f * s), new Vector2(560f * s, 40f * s),
                "Собери рыбок для Кёни, потом забери жетон у башни", Mathf.RoundToInt(15f * s),
                new Color(0.85f, 0.9f, 0.98f), TextAnchor.MiddleCenter);

            RefreshHud();
        }

        private void RefreshHud()
        {
            _count.text = "Рыбки: " + _collected + " / " + _total;
        }

        private void Win()
        {
            if (_won) return;
            _won = true;
            DigitalProgress.MarkDone(_cityId);
            Snd.Play("coin", 1f, 1.2f);

            float s = UiKit.Scale;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            Transform c = _hud.transform;

            UiKit.MakePanel(c, new Vector2(cx, cy), new Vector2(6000f, 6000f),
                new Color(0f, 0f, 0f, 0.55f));
            UiKit.MakePanel(c, new Vector2(cx, cy), new Vector2(Screen.width * 0.9f, 360f * s),
                new Color(0.1f, 0.18f, 0.26f, 0.99f));
            UiKit.MakeText(c, new Vector2(cx, cy + 130f * s), new Vector2(460f * s, 40f * s),
                "Цифровой жетон получен!", Mathf.RoundToInt(24f * s),
                new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleCenter);

            bool real = DigitalProgress.RealDone(_cityId);
            string msg = real
                ? "И в игре, и в реальности — обе половины собраны! " +
                  "Зелёный мост стал золотым."
                : "Это половина ключа. Вторую — настоящий жетон — возьмёшь " +
                  "на месте, в самом Зеленоградске, отметив точку. Тогда " +
                  "мост станет золотым.";
            UiKit.MakeText(c, new Vector2(cx, cy + 30f * s), new Vector2(Screen.width * 0.8f, 160f * s),
                msg, Mathf.RoundToInt(16f * s),
                real ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.85f, 0.9f, 0.98f),
                TextAnchor.UpperCenter);

            UiKit.MakeButton(c, new Vector2(cx, cy - 130f * s), new Vector2(240f * s, 52f * s),
                "На карту", Mathf.RoundToInt(18f * s), Exit);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_player == null) return;
            Vector3 pp = _player.transform.position;

            // Рыбки покачиваются и крутятся; собираются касанием.
            for (int i = 0; i < _fish.Count; i++)
            {
                Transform f = _fish[i];
                if (f == null || !f.gameObject.activeSelf) continue;
                f.localPosition += new Vector3(0f, Mathf.Sin(Time.time * 2f + i) * 0.003f, 0f);
                f.Rotate(0f, 60f * dt, 0f);

                Vector3 to = f.position - pp;
                if (new Vector2(to.x, to.z).magnitude < 1.3f && Mathf.Abs(to.y) < 2.5f)
                {
                    f.gameObject.SetActive(false);
                    _collected++;
                    Snd.Play("coin", 0.9f);
                    ParticleFx.Burst(_world, f.position, 12, new Color(1f, 0.9f, 0.5f), 3.5f);
                    RefreshHud();
                    if (_collected >= _total) ReadyToken();
                }
            }

            if (_tokenReady && !_won && _token != null)
            {
                _token.Rotate(0f, 50f * dt, 0f);
                Vector3 to = _token.position - pp;
                if (new Vector2(to.x, to.z).magnitude < 1.8f && Mathf.Abs(to.y) < 3f) Win();
            }
        }

        private void ReadyToken()
        {
            if (_tokenReady) return;
            _tokenReady = true;
            _token.gameObject.SetActive(true);
            _hint.text = "Все рыбки собраны! Иди к башне за жетоном моста.";
            Snd.Play("coin", 1f, 1.4f);
        }

        private void Exit()
        {
            Snd.Play("click");
            System.Action cb = _onExit;
            _onExit = null;
            Object.Destroy(gameObject);
            if (cb != null) cb();
        }
    }
}
