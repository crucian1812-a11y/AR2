using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Играбельный уровень города — цифровая половина «двух ключей».
    // Зеленоградск: ребёнок бегает за Кёню по старому городу, собирает
    // рыбок и забирает у башни цифровой жетон моста.
    //
    // Дома, крыши, брусчатка — из пака Quaternius (CC0) с настоящими
    // текстурами. Все модели грузятся ЕДИНЫМ масштабом (KoenigProp) —
    // тогда пропорции родные и вытянутые куски не раздуваются.
    //
    // Уровень горизонтальный: на входе форсируем landscape, на выходе
    // возвращаем портрет карты.
    public class KoenigLevel : MonoBehaviour
    {
        private System.Action _onExit;
        private string _cityId;
        private KoenigPlayer _player;
        private Transform _world;
        private float _gs = 1f; // единый масштаб моделей пака

        private readonly List<Transform> _fish = new List<Transform>();
        private int _collected, _total;
        private Transform _token;
        private bool _tokenReady, _won;

        private Canvas _hud;
        private Text _count, _hint;

        public static bool HasLevel(string cityId) { return cityId == "zeln"; }

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
            // Уровень горизонтальный — так и просили, и платформеру так удобнее.
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            SetupSky();

            GameObject worldGo = new GameObject("World");
            worldGo.transform.SetParent(transform, false);
            _world = worldGo.transform;

            // Калибровка: единый масштаб так, чтобы стена была ~3.4 м.
            float wallH = KoenigProp.NativeHeight(_world, "Wall_Plaster_Straight");
            _gs = wallH > 0.01f ? 3.4f / wallH : 1f;

            BuildTown();

            _player = KoenigPlayer.Spawn(transform, new Vector3(0f, 1.5f, 16f), Guide.ModelId);
            if (_player.Cam != null)
            {
                _player.Cam.clearFlags = CameraClearFlags.Skybox;
                PostFx fx = _player.Cam.gameObject.AddComponent<PostFx>();
                fx.Configure(1.05f, 1.14f, new Color(1f, 0.99f, 0.96f), 0.42f);
            }

            BuildHud();
            KoenigTouch.Create(_hud.transform);
        }

        private void SetupSky()
        {
            Color top = new Color(0.30f, 0.55f, 0.85f);
            Color horizon = new Color(0.76f, 0.86f, 0.94f);
            Color ground = new Color(0.52f, 0.58f, 0.48f);
            Shader sky = Shader.Find("Bear/Sky");
            if (sky != null)
            {
                Material m = new Material(sky);
                m.SetColor("_TopColor", top);
                m.SetColor("_HorizonColor", horizon);
                m.SetColor("_GroundColor", ground);
                RenderSettings.skybox = m;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = top * 0.5f;
            RenderSettings.ambientEquatorColor = horizon * 0.5f;
            RenderSettings.ambientGroundColor = ground * 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogDensity = 0.005f;

            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.localRotation = Quaternion.Euler(48f, 35f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.96f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
        }

        private Material Tiled(string tex, float tile, Color fallback)
        {
            Material m = new Material(Gfx.Standard);
            Texture2D t = Resources.Load<Texture2D>("Textures/koenig/" + tex);
            if (t != null) { m.mainTexture = t; m.SetTextureScale("_MainTex", new Vector2(tile, tile)); }
            else m.color = fallback;
            m.SetFloat("_Glossiness", 0.05f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        // ---------- Мир ----------

        private void BuildTown()
        {
            // Трава под всем городом (крупная площадка).
            Material grass = Gfx.MatFull(new Color(0.42f, 0.6f, 0.32f), 0.03f, 0f, Color.black, 10f, 0.4f);
            Gfx.Box(_world, new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), grass, true);

            // Мощёная площадь и главная улица (настоящая каменная текстура).
            Material cobble = Tiled("T_UnevenBrick_BaseColor", 10f, new Color(0.66f, 0.63f, 0.57f));
            Gfx.Box(_world, new Vector3(0f, 0.02f, 0f), new Vector3(46f, 0.2f, 52f), cobble, true);

            // Балтийское море за северным парапетом.
            Material sea = Gfx.Mat(new Color(0.18f, 0.44f, 0.62f), 0.7f, 0.1f);
            Gfx.Box(_world, new Vector3(0f, -0.4f, 66f), new Vector3(200f, 0.6f, 50f), sea, false);
            Material stone = Tiled("T_RockTrim_BaseColor", 10f, new Color(0.7f, 0.66f, 0.58f));
            Gfx.Box(_world, new Vector3(0f, 0.7f, 34.5f), new Vector3(110f, 1.6f, 0.9f), stone, true);
            // Границы — стены (не убежать за город).
            Gfx.Box(_world, new Vector3(0f, 2.5f, -44f), new Vector3(130f, 6f, 1f), stone, true);
            Gfx.Box(_world, new Vector3(-44f, 2.5f, 0f), new Vector3(1f, 6f, 130f), stone, true);
            Gfx.Box(_world, new Vector3(44f, 2.5f, 0f), new Vector3(1f, 6f, 130f), stone, true);

            // Дома в два ряда вдоль площади (по 5) — целый город.
            string[] walls = { "Wall_Plaster_Window_Wide_Round", "Wall_Plaster_Door_Round",
                               "Wall_UnevenBrick_Window_Wide_Round" };
            string[] roofs = { "Roof_RoundTiles_6x6", "Roof_RoundTiles_6x8", "Roof_RoundTiles_4x4" };
            float[] xs = { -24f, -12f, 0f, 12f, 24f };
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] == 0f) continue; // центр под вид на башню
                House(new Vector3(xs[i], 0.1f, -26f), 0f, walls[i % 3], roofs[i % 3], i % 3 == 2);
                House(new Vector3(xs[i], 0.1f, 26f), 180f, walls[(i + 1) % 3], roofs[(i + 1) % 3], i % 3 == 1);
            }
            // Пара домов по бокам для глубины.
            House(new Vector3(-36f, 0.1f, 0f), 90f, walls[0], roofs[1], false);
            House(new Vector3(36f, 0.1f, 0f), 270f, walls[2], roofs[0], true);

            // Доминанта — башня с настоящей черепичной крышей, у неё жетон.
            Tower(new Vector3(0f, 0.1f, -30f));

            // Реквизит пака (родной масштаб).
            Vector3 t;
            KoenigProp.LoadScaled(_world, "Prop_Wagon", new Vector3(-12f, 0.1f, 6f), 40f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(11f, 0.1f, -3f), 20f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(12.4f, 0.1f, -2f), 70f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(8f, 0.1f, 10f), 10f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Support", new Vector3(-18f, 0.1f, 14f), 0f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Support", new Vector3(18f, 0.1f, 14f), 0f, _gs, true, out t);
            for (int i = 0; i < 8; i++)
                KoenigProp.LoadScaled(_world, "Prop_WoodenFence_Single",
                    new Vector3(-20f + i * 2.4f, 0.1f, 17f), 90f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_MetalFence_Simple", new Vector3(8f, 0.1f, 30f), 0f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Vine1", new Vector3(-11.8f, 0.1f, -22f), 0f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Vine1", new Vector3(12.2f, 0.1f, 22f), 180f, _gs, true, out t);

            Gfx.Glow(_world, new Vector3(-10f, 3f, 0f), 3.5f, new Color(1f, 0.9f, 0.6f, 0.4f));
            Gfx.Glow(_world, new Vector3(10f, 3f, 0f), 3.5f, new Color(1f, 0.9f, 0.6f, 0.4f));

            // Рыбки — 16 по всему городу, чтобы было что обежать.
            Vector3[] spots = {
                new Vector3(-8f, 1f, 3f), new Vector3(8f, 1f, -4f), new Vector3(-16f, 1f, -10f),
                new Vector3(16f, 1f, 10f), new Vector3(0f, 1f, 12f), new Vector3(-4f, 1f, -16f),
                new Vector3(20f, 1f, -8f), new Vector3(-20f, 1f, 8f), new Vector3(4f, 1f, 20f),
                new Vector3(-24f, 1f, -18f), new Vector3(24f, 1f, 18f), new Vector3(-30f, 1f, 4f),
                new Vector3(30f, 1f, -4f), new Vector3(0f, 1f, -20f), new Vector3(14f, 1f, 4f),
                new Vector3(-14f, 1f, -4f),
            };
            for (int i = 0; i < spots.Length; i++) _fish.Add(Fish(spots[i]));
            _total = _fish.Count;

            _token = Token(new Vector3(0f, 1.4f, -25f));
            _token.gameObject.SetActive(false);
        }

        // Дом: настоящая черепичная крыша задаёт размер, под неё строим
        // короб-основу с текстурой, спереди — настоящий фасад с окном/дверью.
        private void House(Vector3 basePos, float yaw, string wallId, string roofId, bool stone)
        {
            float bodyH = Random.Range(3.6f, 4.4f);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = q * Vector3.forward;

            Vector3 rs;
            KoenigProp.LoadScaled(_world, roofId, basePos + new Vector3(0f, bodyH, 0f), yaw, _gs, true, out rs);
            float bw = Mathf.Clamp(rs.x, 5f, 13f);
            float bd = Mathf.Clamp(rs.z, 5f, 13f);

            Material body = stone
                ? Tiled("T_UnevenBrick_BaseColor", 2.4f, new Color(0.66f, 0.63f, 0.57f))
                : Tiled("T_Plaster_BaseColor", 1.7f, new Color(0.88f, 0.83f, 0.7f));
            Gfx.Box(_world, basePos + new Vector3(0f, bodyH * 0.5f, 0f),
                new Vector3(bw * 0.92f, bodyH, bd * 0.92f), body, true);

            Vector3 t;
            KoenigProp.LoadScaled(_world, wallId, basePos + fwd * (bd * 0.46f), yaw, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Chimney",
                basePos + q * new Vector3(bw * 0.28f, bodyH + 0.2f, -bd * 0.22f), yaw, _gs, true, out t);
        }

        private void Tower(Vector3 basePos)
        {
            Material stone = Tiled("T_RockTrim_BaseColor", 2.4f, new Color(0.7f, 0.66f, 0.58f));
            float h = 14f;
            Gfx.Box(_world, basePos + new Vector3(0f, h * 0.5f, 0f), new Vector3(5f, h, 5f), stone, true);
            Vector3 rs;
            KoenigProp.LoadScaled(_world, "Roof_Tower_RoundTiles",
                basePos + new Vector3(0f, h, 0f), 0f, _gs * 1.4f, true, out rs);
            Gfx.Ball(_world, basePos + new Vector3(0f, h - 2.4f, 2.55f), new Vector3(1.3f, 1.3f, 0.3f),
                Gfx.MatFull(new Color(0.95f, 0.93f, 0.85f), 0.2f, 0f, Color.black, 0f, 0f));
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

            UiKit.MakeText(c, new Vector2(W * 0.5f, H - 34f * s), new Vector2(520f * s, 40f * s),
                "Зеленоградск — город кошек", Mathf.RoundToInt(22f * s),
                new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);

            _count = UiKit.MakeTextRight(c, W - 24f * s, H - 34f * s, new Vector2(220f * s, 40f * s),
                "", Mathf.RoundToInt(20f * s), Color.white);

            UiKit.MakeButton(c, new Vector2(80f * s, H - 34f * s), new Vector2(140f * s, 46f * s),
                "← Карта", Mathf.RoundToInt(16f * s), Exit);

            _hint = UiKit.MakeText(c, new Vector2(W * 0.5f, H - 72f * s), new Vector2(640f * s, 40f * s),
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

            UiKit.MakePanel(c, new Vector2(cx, cy), new Vector2(9000f, 9000f), new Color(0f, 0f, 0f, 0.55f));
            UiKit.MakePanel(c, new Vector2(cx, cy), new Vector2(Screen.width * 0.7f, 340f * s),
                new Color(0.1f, 0.18f, 0.26f, 0.99f));
            UiKit.MakeText(c, new Vector2(cx, cy + 120f * s), new Vector2(520f * s, 40f * s),
                "Цифровой жетон получен!", Mathf.RoundToInt(24f * s),
                new Color(1f, 0.88f, 0.45f), TextAnchor.MiddleCenter);

            bool real = DigitalProgress.RealDone(_cityId);
            string msg = real
                ? "И в игре, и в реальности — обе половины собраны! Зелёный мост стал золотым."
                : "Это половина ключа. Вторую — настоящий жетон — возьмёшь на месте, в самом " +
                  "Зеленоградске, отметив точку. Тогда мост станет золотым.";
            UiKit.MakeText(c, new Vector2(cx, cy + 20f * s), new Vector2(Screen.width * 0.62f, 150f * s),
                msg, Mathf.RoundToInt(16f * s),
                real ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.85f, 0.9f, 0.98f), TextAnchor.UpperCenter);

            UiKit.MakeButton(c, new Vector2(cx, cy - 120f * s), new Vector2(240f * s, 52f * s),
                "На карту", Mathf.RoundToInt(18f * s), Exit);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_player == null) return;
            Vector3 pp = _player.transform.position;

            for (int i = 0; i < _fish.Count; i++)
            {
                Transform f = _fish[i];
                if (f == null || !f.gameObject.activeSelf) continue;
                f.localPosition += new Vector3(0f, Mathf.Sin(Time.time * 2f + i) * 0.003f, 0f);
                f.Rotate(0f, 60f * dt, 0f);

                Vector3 to = f.position - pp;
                if (new Vector2(to.x, to.z).magnitude < 1.4f && Mathf.Abs(to.y) < 2.5f)
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
                if (new Vector2(to.x, to.z).magnitude < 2f && Mathf.Abs(to.y) < 3f) Win();
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
            Screen.orientation = ScreenOrientation.Portrait; // вернуть портрет карте
            System.Action cb = _onExit;
            _onExit = null;
            Object.Destroy(gameObject);
            if (cb != null) cb();
        }
    }
}
