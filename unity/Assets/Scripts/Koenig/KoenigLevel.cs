using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Играбельный уровень города — цифровая половина «двух ключей».
    // Зеленоградск: ребёнок бегает за Кёню по старой улице, собирает рыбок
    // и забирает у башни цифровой жетон моста.
    //
    // Дома собраны заранее из модулей пака Quaternius (CC0) в Blender и
    // выгружены готовыми FBX (Resources/Models/koenig/houses) — Unity лишь
    // расставляет их и красит по именам материалов настоящими текстурами.
    // Уровень горизонтальный.
    public class KoenigLevel : MonoBehaviour
    {
        private System.Action _onExit;
        private string _cityId;
        private KoenigPlayer _player;
        private Transform _world;
        private float _gs = 1f; // масштаб отдельных пропов пака

        private readonly List<Transform> _fish = new List<Transform>();
        private int _collected, _total;
        private Transform _token;
        private bool _tokenReady, _won;

        private Canvas _hud;
        private Text _count, _hint;

        private Combat _combat;
        private Enemy _boss;
        private bool _bossDown;
        private Image _hpFill;
        private Text _hpText;

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
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            SetupSky();

            GameObject worldGo = new GameObject("World");
            worldGo.transform.SetParent(transform, false);
            _world = worldGo.transform;

            float wallH = KoenigProp.NativeHeight(_world, "Wall_Plaster_Straight");
            _gs = wallH > 0.01f ? 3.0f / wallH : 1f;

            BuildTown();

            _player = KoenigPlayer.Spawn(transform, new Vector3(0f, 1.5f, -34f), Guide.ModelId);
            if (_player.Cam != null)
            {
                _player.Cam.clearFlags = CameraClearFlags.Skybox;
                PostFx fx = _player.Cam.gameObject.AddComponent<PostFx>();
                fx.Configure(1.05f, 1.15f, new Color(1f, 0.99f, 0.96f), 0.4f);
            }

            _combat = Combat.Create(transform, _player, _player.Cam);
            SpawnPacks();

            BuildHud();
            KoenigTouch.Create(_hud.transform);

            _player.OnHealthChanged = RefreshHealth;
        }

        private void SetupSky()
        {
            Color top = new Color(0.28f, 0.54f, 0.86f);
            Color horizon = new Color(0.78f, 0.87f, 0.95f);
            Color ground = new Color(0.5f, 0.56f, 0.44f);
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
            RenderSettings.ambientSkyColor = top * 0.55f;
            RenderSettings.ambientEquatorColor = horizon * 0.55f;
            RenderSettings.ambientGroundColor = ground * 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogDensity = 0.004f;

            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.localRotation = Quaternion.Euler(46f, 32f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.55f;
            sun.color = new Color(1f, 0.96f, 0.85f);
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

        // ---------- Мир: улица старого города ----------

        private void BuildTown()
        {
            // Трава.
            Material grass = Gfx.MatFull(new Color(0.42f, 0.6f, 0.32f), 0.03f, 0f, Color.black, 12f, 0.4f);
            Gfx.Box(_world, new Vector3(0f, -0.5f, 0f), new Vector3(150f, 1f, 150f), grass, true);

            // Мощёная улица по центру (север-юг), настоящая каменная текстура.
            Material cobble = Tiled("T_UnevenBrick_BaseColor", 12f, new Color(0.66f, 0.63f, 0.57f));
            Gfx.Box(_world, new Vector3(0f, 0.02f, 2f), new Vector3(26f, 0.2f, 84f), cobble, true);

            // Балтийское море на севере за парапетом.
            Material sea = Gfx.Mat(new Color(0.18f, 0.44f, 0.62f), 0.7f, 0.1f);
            Gfx.Box(_world, new Vector3(0f, -0.4f, 76f), new Vector3(220f, 0.6f, 50f), sea, false);
            Material stone = Tiled("T_RockTrim_BaseColor", 12f, new Color(0.7f, 0.66f, 0.58f));
            Gfx.Box(_world, new Vector3(0f, 0.7f, 46.5f), new Vector3(130f, 1.6f, 0.9f), stone, true);
            Gfx.Box(_world, new Vector3(0f, 3f, -48f), new Vector3(150f, 7f, 1f), stone, true);
            Gfx.Box(_world, new Vector3(-50f, 3f, 0f), new Vector3(1f, 7f, 150f), stone, true);
            Gfx.Box(_world, new Vector3(50f, 3f, 0f), new Vector3(1f, 7f, 150f), stone, true);

            // Готовые дома вдоль улицы: западный ряд смотрит на восток,
            // восточный — на запад. Высоту нормируем, ширина у вариантов
            // своя (проверено в Blender-рендере).
            string[] v = { "houses/House_PlasterA", "houses/House_StoneB",
                           "houses/House_WideC", "houses/House_TallD" };
            float[] zs = { -34f, -20f, -6f, 8f, 22f, 36f };
            for (int i = 0; i < zs.Length; i++)
            {
                string wId = v[i % v.Length];
                string eId = v[(i + 2) % v.Length];
                float wh = wId.Contains("Tall") ? 12f : 9.5f;
                float eh = eId.Contains("Tall") ? 12f : 9.5f;
                KoenigProp.LoadSized(_world, wId, new Vector3(-20f, 0.1f, zs[i]), 90f, wh);
                KoenigProp.LoadSized(_world, eId, new Vector3(20f, 0.1f, zs[i]), 270f, eh);
            }

            // Башня-доминанта в конце улицы (у моря), у неё жетон.
            Tower(new Vector3(0f, 0.1f, 42f));

            // Реквизит пака (родной масштаб).
            Vector3 t;
            KoenigProp.LoadScaled(_world, "Prop_Wagon", new Vector3(-8f, 0.15f, -20f), 50f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(8f, 0.15f, -6f), 20f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(9.2f, 0.15f, -5f), 70f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(-9f, 0.15f, 10f), 10f, _gs, true, out t);
            for (int i = 0; i < 10; i++)
                KoenigProp.LoadScaled(_world, "Prop_WoodenFence_Single",
                    new Vector3(-13f, 0.15f, -34f + i * 8f), 0f, _gs, true, out t);
            for (int i = 0; i < 10; i++)
                KoenigProp.LoadScaled(_world, "Prop_WoodenFence_Single",
                    new Vector3(13f, 0.15f, -34f + i * 8f), 0f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_MetalFence_Simple", new Vector3(6f, 0.15f, 44f), 90f, _gs, true, out t);

            Gfx.Glow(_world, new Vector3(-10f, 4f, 0f), 4f, new Color(1f, 0.9f, 0.6f, 0.4f));
            Gfx.Glow(_world, new Vector3(10f, 4f, 20f), 4f, new Color(1f, 0.9f, 0.6f, 0.4f));

            // Рыбки — 16 вдоль улицы, чтобы было что обежать.
            Vector3[] spots = {
                new Vector3(0f, 1f, -28f), new Vector3(-6f, 1f, -20f), new Vector3(6f, 1f, -14f),
                new Vector3(0f, 1f, -8f), new Vector3(-7f, 1f, -2f), new Vector3(7f, 1f, 4f),
                new Vector3(0f, 1f, 10f), new Vector3(-6f, 1f, 16f), new Vector3(6f, 1f, 22f),
                new Vector3(0f, 1f, 28f), new Vector3(-8f, 1f, 34f), new Vector3(8f, 1f, 34f),
                new Vector3(-10f, 1f, -30f), new Vector3(10f, 1f, -30f), new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1f, 20f),
            };
            for (int i = 0; i < spots.Length; i++) _fish.Add(Fish(spots[i]));
            _total = _fish.Count;

            _token = Token(new Vector3(0f, 1.4f, 38f));
            _token.gameObject.SetActive(false);
        }

        // Население улицы: шесть пачек по четыре гриба вдоль дороги и
        // Дюнный великан у башни. Застройку не трогаем — пачки ставятся в
        // промежутки между рыбками, чтобы за наградой приходилось идти
        // сквозь бой, а не мимо него.
        //
        // Гриб держит 12 урона (два-три удара по 3–6), бьёт на 3 и медленный:
        // от пачки можно уйти, а стоять в ней нельзя.
        private void SpawnPacks()
        {
            // Мостовая — короб высотой 0.2 с центром на 0.02, значит её
            // верх на 0.12. У врага нет гравитации, он едет на уровне своей
            // линии патрулирования: поставишь на ноль — утонет по щиколотку.
            const float y = 0.12f;
            Vector3[] packs = {
                new Vector3(-6f, y, -24f), new Vector3(7f, y, -12f),
                new Vector3(-7f, y, -2f),  new Vector3(8f, y, 12f),
                new Vector3(-8f, y, 22f),  new Vector3(4f, y, 30f),
            };

            int id = 1;
            for (int p = 0; p < packs.Length; p++)
            {
                for (int k = 0; k < 4; k++)
                {
                    // Разброс по кругу — пачка стоит кучкой, а не в линию.
                    float ang = (k / 4f) * Mathf.PI * 2f + p;
                    Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.8f;
                    Vector3 a = packs[p] + off;
                    Vector3 b = a + new Vector3(Mathf.Sin(ang) * 2.5f, 0f, Mathf.Cos(ang) * 2.5f);

                    Enemy e = Enemy.Spawn(_world, a, b, "mushroom", 1.5f, id++);
                    e.Hp = 12;
                    e.Damage = 3;
                    e.AttackCooldown = 1.5f;
                    _combat.Add(e);
                }
            }

            // Дюнный великан: 40 жизни — примерно десять попаданий, минута
            // боя с отходами. Бьёт больно и редко, чтобы успевать убегать.
            _boss = Enemy.SpawnBoss(_world, new Vector3(0f, y, 34f), new Vector3(-5f, y, 36f),
                "yeti", 1.6f, 900, 40, 1.8f);
            _boss.Damage = 7;
            _boss.AttackCooldown = 2f;
            _boss.AttackRange = 1.8f;
            _combat.Add(_boss);
        }

        private void Tower(Vector3 basePos)
        {
            Material stone = Tiled("T_RockTrim_BaseColor", 2.4f, new Color(0.7f, 0.66f, 0.58f));
            float h = 16f;
            Gfx.Box(_world, basePos + new Vector3(0f, h * 0.5f, 0f), new Vector3(5.5f, h, 5.5f), stone, true);
            Vector3 rs;
            KoenigProp.LoadScaled(_world, "Roof_Tower_RoundTiles",
                basePos + new Vector3(0f, h, 0f), 0f, _gs * 1.6f, true, out rs);
            Gfx.Ball(_world, basePos + new Vector3(0f, h - 2.6f, 2.8f), new Vector3(1.4f, 1.4f, 0.3f),
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
                "Собирай рыбок и отбивайся от грибов. У башни ждёт великан.",
                Mathf.RoundToInt(15f * s), new Color(0.85f, 0.9f, 0.98f), TextAnchor.MiddleCenter);

            // Полоска жизни — слева вверху, под подсказкой. Внизу её ставить
            // нельзя: там джойстик, и палец накрывал бы ровно то, за чем
            // надо следить. Рисуем две панели, живая поверх тёмной.
            float barW = 300f * s, barH = 22f * s;
            Vector2 barPos = new Vector2(24f * s + barW * 0.5f, H - 116f * s);
            UiKit.MakePanel(c, barPos, new Vector2(barW, barH),
                new Color(0.1f, 0.06f, 0.08f, 0.8f));
            _hpFill = UiKit.MakePanel(c, barPos, new Vector2(barW, barH),
                new Color(0.85f, 0.25f, 0.3f, 0.95f));
            _hpText = UiKit.MakeText(c, barPos, new Vector2(barW, barH), "",
                Mathf.RoundToInt(13f * s), Color.white, TextAnchor.MiddleCenter);

            RefreshHud();
            RefreshHealth();
        }

        private void RefreshHud()
        {
            _count.text = "Рыбки: " + _collected + " / " + _total;
        }

        // Полоска ужимается влево: сдвигаем и ширину, и центр, иначе она
        // худеет с обеих сторон и выглядит как ползунок, а не как жизнь.
        private void RefreshHealth()
        {
            if (_hpFill == null || _player == null) return;
            float s = UiKit.Scale;
            float full = 300f * s;
            float k = _player.HpFraction;
            float w = Mathf.Max(1f, full * k);

            RectTransform rt = _hpFill.rectTransform;
            rt.sizeDelta = new Vector2(w, 22f * s);
            rt.anchoredPosition = new Vector2(24f * s + w * 0.5f, Screen.height - 116f * s);

            _hpFill.color = k > 0.5f ? new Color(0.85f, 0.25f, 0.3f, 0.95f)
                : k > 0.25f ? new Color(0.95f, 0.6f, 0.2f, 0.95f)
                : new Color(1f, 0.35f, 0.35f, 0.95f);
            _hpText.text = _player.Hp + " / " + KoenigPlayer.MaxHp;
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

            // Великан отбит — Enemy сам себя уничтожает после клипа смерти,
            // поэтому ловим и Dying, и уже исчезнувшую ссылку.
            if (!_bossDown && (_boss == null || _boss.Dying))
            {
                _bossDown = true;
                Snd.Play("victory", 0.8f);
                TryReadyToken();
            }

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
                    TryReadyToken();
                }
            }

            if (_tokenReady && !_won && _token != null)
            {
                _token.Rotate(0f, 50f * dt, 0f);
                Vector3 to = _token.position - pp;
                if (new Vector2(to.x, to.z).magnitude < 2f && Mathf.Abs(to.y) < 3f) Win();
            }
        }

        // Жетон отдаётся за оба дела сразу: рыбки собраны И великан отбит.
        // Раньше хватало рыбок, и бой можно было обойти по краю улицы —
        // тогда он был бы украшением, а не игрой.
        private void TryReadyToken()
        {
            if (_tokenReady) return;

            if (_collected < _total)
            {
                if (_bossDown) _hint.text = "Великан отбит! Осталось собрать рыбок: " +
                    (_total - _collected);
                return;
            }
            if (!_bossDown)
            {
                _hint.text = "Все рыбки собраны! Теперь великан у башни.";
                return;
            }

            _tokenReady = true;
            _token.gameObject.SetActive(true);
            _hint.text = "Путь свободен — забери жетон моста у башни.";
            Snd.Play("coin", 1f, 1.4f);
        }

        private void Exit()
        {
            Snd.Play("click");
            Screen.orientation = ScreenOrientation.Portrait;
            System.Action cb = _onExit;
            _onExit = null;
            Object.Destroy(gameObject);
            if (cb != null) cb();
        }
    }
}
