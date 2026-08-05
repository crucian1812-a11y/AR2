using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Играбельный уровень города — цифровая половина «двух ключей».
    // Зеленоградск: рыцарь идёт по старой улице к морю, собирает рыбок,
    // отбивается от воинов и забирает у самой воды цифровой жетон моста.
    //
    // Уровень идёт с юга на север тремя частями: узкая улица старого
    // города, площадь с колодцем и выход к морю через дюны на пляж.
    // Дома собираются из модулей пака Quaternius (CC0) прямо в игре
    // (KoenigHouse), пляж — сетка по функции высоты (KoenigScenery).
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
        private Text _hpText, _amberText;
        private InventoryUI _bagUi;
        private WorldTags _tags;
        private readonly List<Chest> _chests = new List<Chest>();

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

            // Играем за рыцаря, а не за кота: Кёня остаётся проводником на карте,
            // но в RPG нужен воин. Модель KayKit (CC0) со всеми клипами.
            _player = KoenigPlayer.Spawn(transform, new Vector3(0f, 1.5f, -40f), "Knight");
            if (_player.Cam != null)
            {
                _player.Cam.clearFlags = CameraClearFlags.Skybox;
                PostFx fx = _player.Cam.gameObject.AddComponent<PostFx>();
                fx.Configure(1.05f, 1.12f, new Color(1f, 0.985f, 0.95f), 0.34f);
            }

            _combat = Combat.Create(transform, _player, _player.Cam, _world);
            SpawnPacks();
            SpawnChests();

            BuildHud();
            KoenigTouch.Create(_hud.transform);
            _tags = WorldTags.Create(_hud.transform, _player.Cam);

            _bagUi = InventoryUI.Create(_hud.transform);
            _bagUi.OnClosed = CloseBag;

            _player.OnHealthChanged = RefreshHealth;
            Inventory.Changed += RefreshAmber;
        }

        private void SetupSky()
        {
            Color top = new Color(0.28f, 0.54f, 0.86f);
            Color horizon = new Color(0.78f, 0.87f, 0.95f);
            // Ground-цвет неба был оливковым — и именно он давал тот хаки
            // фон. Камера смотрит вниз под 50°, значит верхний край кадра
            // на 27° НИЖЕ горизонта: голубой верх неба не попадает в кадр
            // ни одним пикселем, видно только нижнюю полусферу. Красим её
            // в бледную морскую дымку, чтобы любой просвет читался далью.
            Color ground = new Color(0.74f, 0.83f, 0.88f);
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
            // Подсвет был тёмным, и теневая сторона штукатурки проваливалась
            // в синеву. У балтийского полудня тень светлая.
            RenderSettings.ambientSkyColor = top * 0.80f;
            RenderSettings.ambientEquatorColor = horizon * 0.90f;
            RenderSettings.ambientGroundColor = ground * 0.55f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = horizon;
            // На 30 м (дальний край кадра) прежние 0.004 давали пропускание
            // 98.6% — воздушной перспективы не было вовсе. При 0.012 дальний
            // конец улицы уходит в дымку, а край земли перестаёт быть виден.
            RenderSettings.fogDensity = 0.012f;

            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            // Солнце светило из-за спины камеры: всё освещалось в лоб,
            // теней поперёк кадра не было, объёма не было. Разворачиваем
            // вбок — дом 9.5 м кладёт тень длиной 12 м через всю мостовую,
            // один ряд фасадов горит, другой уходит в тень. Это самая
            // заметная перемена во всём освещении.
            sunGo.transform.localRotation = Quaternion.Euler(38f, -50f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.55f;
            sun.color = new Color(1f, 0.96f, 0.85f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;

            // Подсветка с моря. Одно солнце давало жёсткую двухтоновость:
            // освещённый фасад и провал в тени, и штукатурка в тени
            // читалась серой. Второй источник — холодный, слабый, БЕЗ
            // теней, светит с противоположной стороны — работает как
            // отражённый от воды свет: теневая сторона остаётся тенью, но
            // в ней снова видно рельеф кладки и балки.
            GameObject fillGo = new GameObject("SeaFill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localRotation = Quaternion.Euler(22f, 140f, 0f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.38f;
            fill.color = new Color(0.72f, 0.84f, 1f);
            fill.shadows = LightShadows.None;
        }

        // Тайлинг задаётся ВЕКТОРОМ, а не числом, и вот почему.
        //
        // У куба Unity каждая грань размечена от 0 до 1, поэтому реальный
        // размер камня равен размеру грани, делённому на тайл, ПО КАЖДОЙ
        // ОСИ ОТДЕЛЬНО. Мостовая 26 × 84 при тайле 12 давала камень
        // 2.17 × 7.00 метра — растянутый втрое, он и читался бурой грязью.
        // Парапет 130 × 1.6 при том же числе давал 10.8 × 0.13 — растяжку
        // в восемьдесят раз.
        //
        // Здесь тайл считается из размеров самой грани, поэтому камень
        // выходит квадратным на любом коробе.
        private Material Tiled(string tex, Vector2 metersPerFace, Color fallback,
                               float stoneSize = 2f)
        {
            Vector2 tile = new Vector2(
                Mathf.Max(1f, metersPerFace.x / stoneSize),
                Mathf.Max(1f, metersPerFace.y / stoneSize));

            Material m = new Material(Gfx.Standard);
            Texture2D t = Resources.Load<Texture2D>("Textures/koenig/" + tex + "_BaseColor");
            if (t != null) { m.mainTexture = t; m.SetTextureScale("_MainTex", tile); }
            else m.color = fallback;

            // Карта нормалей у земли не применялась вовсе — при том, что
            // файлы лежат рядом и у домов работают. Отсюда и разница:
            // у фасадов рельеф был, у мостовой нет.
            Texture2D n = Resources.Load<Texture2D>("Textures/koenig/" + tex + "_Normal");
            if (n != null)
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_NormalScale", 1f);
                m.SetTextureScale("_BumpMap", tile);
            }
            m.SetFloat("_Glossiness", 0.10f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        // ---------- Мир: улица старого города ----------

        // ---------- Разметка уровня ----------
        //
        // Ширина всего в кадре подчинена ортокамере. При OrthoSize 6.6 кадр
        // берёт по горизонтали от 23 м (16:9) до 29 м (20:9) — ПОЛОВИНА
        // этого, 11.7 м, и есть всё, что видно вправо от героя.
        //
        // Прежняя улица была 24 м в ширину, а фасады стояли на x = ±12 —
        // ровно за краем кадра. Дома строились, стояли на своих местах и
        // не появлялись в игре ни одним пикселем: ребёнок бежал по серой
        // мостовой во весь экран. Именно это и означало «дома пропали».
        //
        // Теперь мостовая 11 м, за ней метр травяной обочины, и фасад
        // начинается на x = 6.5 — больше половины кадра занимает улица,
        // остальное застройка по краям. Свес кровли (8.25 м при коробе
        // 6 м) выходит над мостовой на 1.1 м, как в старом городе.
        private const float RoadHalf = 5.5f;    // половина мостовой
        private const float FacadeX = 6.5f;     // передняя грань домов
        private const float StreetZ0 = -46f;    // южный конец улицы
        private const float StreetZ1 = 24f;     // где улица переходит в площадь
        private const float SquareZ1 = 36f;     // где площадь переходит в пляж
        private const float SandZ1 = 66f;       // дальний край подводного склона
        private const float WaterZ = 45f;       // где начинается полотно моря

        // ГЛУБИНА ПЛЯЖА ПОДЧИНЕНА КАДРУ. Верхний край кадра отстоит от
        // героя ровно на 12.5 м по оси Z (при OrthoSize 6.6 и наклоне 32°),
        // и всё, что дальше, он не увидит никогда. Первый вариант уводил
        // урез воды на 56 м при бое на 44 — море не попадало в кадр вовсе,
        // и пляж читался белым полем без края. Здесь песок пересекает ноль
        // на 47 м: стоя у жетона, ребёнок видит воду на треть экрана.
        //
        // Вся форма берега — эта одна функция: дюнный вал с проходом по
        // центру, ложбина за ним, ровный песок и уход под воду.
        private static float SandHeight(float x, float z)
        {
            float y = z < 39f
                ? Mathf.Lerp(0f, 0.7f, (z - SquareZ1) / 3f)
                : (z < 47f ? Mathf.Lerp(0.7f, 0f, (z - 39f) / 8f)
                           : Mathf.Lerp(0f, -2.8f, (z - 47f) / 19f));

            // Дюнный вал. По центру его нет: там проход к морю, и он должен
            // читаться проходом, а не дырой в стене.
            float side = Mathf.Clamp01((Mathf.Abs(x) - 4.5f) / 6f);
            float ridge = Mathf.Exp(-Mathf.Pow((z - 39.5f) / 2.6f, 2f));
            y += side * ridge * (1.7f + 0.6f * Mathf.Sin(x * 0.55f));

            // Мелкая рябь. У самой площади гасим её в ноль, иначе на стыке
            // с ровной землёй города появится ступенька.
            float fade = Mathf.Clamp01((z - SquareZ1) / 3f);
            y += fade * (Mathf.Sin(x * 0.31f + z * 0.17f) * 0.09f
                       + Mathf.Sin(z * 0.44f - x * 0.09f) * 0.05f);
            return y;
        }

        // Цвет вершины песка. Мокрый песок у воды темнее и холоднее сухого
        // — без этого пляж выходит однородной жёлтой заливкой, и линия
        // прибоя держится на одной пене.
        private static Color SandTint(float x, float z)
        {
            float y = SandHeight(x, z);
            float wet = Mathf.Clamp01((0.25f - y) / 0.4f) * Mathf.Clamp01((z - 42f) / 4f);
            float crest = Mathf.Clamp01(y / 1.6f);
            Color c = Color.Lerp(new Color(1f, 0.97f, 0.9f), new Color(0.58f, 0.55f, 0.5f), wet);
            return Color.Lerp(c, Color.white, crest * 0.45f);
        }

        private void BuildTown()
        {
            // Трава была ПЛОСКИМ ЦВЕТОМ — при том, что в Textures/world
            // лежат Grass003 и Grass006 с картами нормалей и шероховатости,
            // импортируемые с Repeat и анизотропией 12. Берём трипланарный
            // Bear/Terrain: ему UV коробки не нужны вовсе, поэтому вся
            // история с тайлингом его не касается, а по склону он сам
            // разводит траву и песок — это и даст переход к пляжу.
            Material grass = Gfx.TerrainMat(
                "Grass003_1K-PNG_Color", "Grass003_1K-PNG_NormalGL",
                "aerial_beach_01_diff_1k", "aerial_beach_01_nor_gl_1k",
                0.35f, 0.30f);
            Gfx.Box(_world, new Vector3(0f, -0.5f, -12f), new Vector3(120f, 1f, 96f), grass, true);

            BuildStreet();
            BuildSquare();
            BuildBeach();

            // Границы мира — невидимые. Прежде их держали каменные стены в
            // семь метров: при ортокамере они влезали в кадр и читались
            // декорацией, которой в Зеленоградске взяться неоткуда.
            KoenigScenery.Fence(_world, new Vector3(-24f, 4f, 0f), new Vector3(2f, 8f, 160f));
            KoenigScenery.Fence(_world, new Vector3(24f, 4f, 0f), new Vector3(2f, 8f, 160f));
            KoenigScenery.Fence(_world, new Vector3(0f, 4f, -52f), new Vector3(60f, 8f, 2f));
            KoenigScenery.Fence(_world, new Vector3(0f, 4f, 57f), new Vector3(60f, 8f, 2f));

            // Рыбки: тринадцать вдоль улицы и площади, три на песке — чтобы
            // до моря дошёл и тот, кто пришёл только за рыбками.
            Vector3[] spots = {
                new Vector3(0f, 1f, -34f), new Vector3(-3f, 1f, -28f), new Vector3(3f, 1f, -22f),
                new Vector3(0f, 1f, -16f), new Vector3(-3.4f, 1f, -10f), new Vector3(3.4f, 1f, -4f),
                new Vector3(0f, 1f, 2f), new Vector3(-3f, 1f, 8f), new Vector3(3f, 1f, 14f),
                new Vector3(0f, 1f, 20f), new Vector3(-6f, 1f, 27f), new Vector3(6f, 1f, 31f),
                new Vector3(0f, 1f, 34f),
                new Vector3(-3.5f, 1.4f, 39f), new Vector3(4.5f, 1.3f, 42f), new Vector3(0f, 1.1f, 45f),
            };
            for (int i = 0; i < spots.Length; i++) _fish.Add(Fish(spots[i]));
            _total = _fish.Count;

            // Жетон лежит у самой воды: последний шаг уровня — выйти к морю.
            _token = Token(new Vector3(0f, SandHeight(0f, 46f) + 1.2f, 46f));
            _token.gameObject.SetActive(false);
        }

        // ---------- Улица ----------

        private void BuildStreet()
        {
            float roadLen = SquareZ1 - StreetZ0;
            float roadMid = (StreetZ0 + SquareZ1) * 0.5f;
            Material cobble = Tiled("T_UnevenBrick", new Vector2(RoadHalf * 2f, roadLen),
                new Color(0.66f, 0.63f, 0.57f), 1.6f);
            Gfx.Box(_world, new Vector3(0f, 0.02f, roadMid),
                new Vector3(RoadHalf * 2f, 0.2f, roadLen), cobble, true);

            // Застройка. Шаг — ширина дома плюс полметра на просвет: фасады
            // почти смыкаются, как в старом городе. Этажность чередуется,
            // иначе ровный карниз читается забором.
            for (int side = -1; side <= 1; side += 2)
            {
                float z = StreetZ0 + 2f;
                int n = 0;
                while (z < StreetZ1)
                {
                    int mods = (n % 4 == 1) ? 2 : 3;
                    int storeys = (n % 3 == 0) ? 2 : 1;
                    float w = KoenigHouse.Width(mods);
                    // Фасад ровно по линии застройки: дом сдвигаем на
                    // половину своей глубины наружу.
                    float depth = mods * KoenigHouse.Module;
                    // ЛИЦОМ К КАМЕРЕ, А НЕ К МОСТОВОЙ.
                    //
                    // Дома стояли развёрнутыми на улицу — и это было ровно
                    // то, чего видеть нельзя. Камера смотрит вдоль улицы
                    // (CamYaw = 0), значит нормаль фасада, глядящего вбок,
                    // перпендикулярна взгляду: окна, дверь, ставни, балки и
                    // плющ приходили в кадр РЕБРОМ, шириной в ноль пикселей.
                    // Видно было только скаты кровель и глухие торцы.
                    //
                    // Развёрнутый на юг дом показывает камере ту самую
                    // стену, ради которой пак и брали: щипец с арочными
                    // окнами, дверь, подкосы, свес. Ряд таких щипцов вдоль
                    // улицы — это и есть ганзейский Кёнигсберг.
                    KoenigHouse.Build(_world,
                        new Vector3(side * (FacadeX + depth * 0.5f), 0f, z + w * 0.5f),
                        0f, mods, storeys, n + (side > 0 ? 2 : 0));
                    z += w + 0.5f;
                    n++;
                }
            }

            // Обочина: метр травы между мостовой и фасадом. Она и есть та
            // самая трава, которую видно в игре, — зелёное поле за домами
            // в кадр не попадает ни одним пикселем.
            Color grassLow = new Color(0.26f, 0.42f, 0.18f);
            Color grassTip = new Color(0.62f, 0.78f, 0.36f);
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = side * (RoadHalf + (FacadeX - RoadHalf) * 0.5f);
                KoenigScenery.GrassField(_world,
                    new Vector3(cx, 0f, roadMid), new Vector2(0.55f, roadLen * 0.5f),
                    5200, grassLow, grassTip, 0.16f, 0.5f, null);
            }

            // Липы вдоль обочины. Редко и невысоко: сплошная аллея закрыла
            // бы фасады, ради которых улица и строилась.
            for (int i = 0; i < 5; i++)
            {
                float z = StreetZ0 + 10f + i * 14f;
                float x = (i % 2 == 0) ? -(FacadeX - 0.6f) : (FacadeX - 0.6f);
                Gfx.Prop(_world, i % 2 == 0 ? "tree_detailed" : "tree_oak",
                    new Vector3(x, 0.02f, z), 4.6f, i * 47f, 0.16f);
                Gfx.Prop(_world, "plant_bushSmall",
                    new Vector3(-x, 0.02f, z + 5f), 0.55f, i * 31f);
            }

            // Реквизит: телега и ящики прижаты к обочине, чтобы не мешать
            // бою на мостовой, но попадали в кадр.
            Vector3 t;
            KoenigProp.LoadScaled(_world, "Prop_Wagon", new Vector3(-4.8f, 0.12f, -26f), 12f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(4.7f, 0.12f, -8f), 20f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(4.4f, 0.12f, -6.8f), 70f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(-4.6f, 0.12f, 12f), 10f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_WoodenFence_Single", new Vector3(4.9f, 0.12f, 4f), 0f, _gs, true, out t);

            // Тёплые пятна у дверей — вечерние окна среди дневного света.
            // Они же расставляют вехи по длинной улице.
            for (int i = 0; i < 4; i++)
            {
                float z = StreetZ0 + 14f + i * 16f;
                float x = (i % 2 == 0) ? FacadeX - 0.2f : -(FacadeX - 0.2f);
                Gfx.Glow(_world, new Vector3(x, 2.2f, z), 2.6f, new Color(1f, 0.86f, 0.55f, 0.45f));
            }
        }

        // ---------- Площадь у моря ----------

        private void BuildSquare()
        {
            float len = SquareZ1 - StreetZ1;
            float mid = (StreetZ1 + SquareZ1) * 0.5f;
            Material paving = Tiled("T_RockTrim", new Vector2(20f, len),
                new Color(0.72f, 0.68f, 0.6f), 1.4f);
            Gfx.Box(_world, new Vector3(0f, 0.02f, mid), new Vector3(20f, 0.2f, len), paving, true);

            // Четыре дома в глубине — площадь должна иметь стены, иначе она
            // читается не площадью, а обрывом застройки.
            KoenigHouse.Build(_world, new Vector3(-13f, 0f, StreetZ1 + 3f), 0f, 3, 2, 1);
            KoenigHouse.Build(_world, new Vector3(13f, 0f, StreetZ1 + 3f), 0f, 3, 1, 4);
            KoenigHouse.Build(_world, new Vector3(-13f, 0f, StreetZ1 + 11f), 0f, 2, 1, 2);
            KoenigHouse.Build(_world, new Vector3(13f, 0f, StreetZ1 + 11f), 0f, 3, 2, 3);

            // Колодец в середине площади: круглая кладка и деревянный
            // ворот. Точка, вокруг которой площадь собирается.
            Material stone = KoenigProp.CatMaterial("rocktrim");
            Material wood = KoenigProp.CatMaterial("woodtrim");
            Vector3 well = new Vector3(0f, 0.12f, mid);
            Gfx.Cyl(_world, well + new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.45f, 2.2f), stone, true);
            Gfx.Cyl(_world, well + new Vector3(0f, 0.78f, 0f), new Vector3(1.7f, 0.04f, 1.7f),
                Gfx.Mat(new Color(0.16f, 0.3f, 0.36f), 0.8f, 0.1f), false);
            Gfx.Box(_world, well + new Vector3(-1f, 1.5f, 0f), new Vector3(0.18f, 2.2f, 0.18f), wood, false);
            Gfx.Box(_world, well + new Vector3(1f, 1.5f, 0f), new Vector3(0.18f, 2.2f, 0.18f), wood, false);
            Gfx.Box(_world, well + new Vector3(0f, 2.6f, 0f), new Vector3(2.4f, 0.5f, 1.4f),
                KoenigProp.CatMaterial("roundtiles"), false);

            Vector3 t;
            KoenigProp.LoadScaled(_world, "Prop_Wagon", new Vector3(-7.5f, 0.12f, mid + 3f), 25f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(7.2f, 0.12f, mid - 2f), 40f, _gs, true, out t);
            KoenigProp.LoadScaled(_world, "Prop_Crate", new Vector3(8f, 0.12f, mid - 1f), 15f, _gs, true, out t);

            for (int i = -1; i <= 1; i += 2)
            {
                Gfx.Prop(_world, "tree_detailed", new Vector3(i * 8.5f, 0.12f, mid + 4.5f), 5.2f, i * 40f, 0.18f);
                Gfx.Prop(_world, "plant_bushLarge", new Vector3(i * 9.5f, 0.12f, mid - 4f), 0.9f, i * 25f);
            }
        }

        // ---------- Пляж и море ----------

        private void BuildBeach()
        {
            // Песок: одна сетка 60 × 30 по SandHeight. Трипланарный
            // материал не требует развёртки, поэтому на склоне дюны
            // текстура не растягивается — а именно растяжка и выдаёт
            // «нарисованный» песок.
            Material sand = Gfx.TerrainMat(
                "aerial_beach_01_diff_1k", "aerial_beach_01_nor_gl_1k",
                "aerial_beach_01_diff_1k", "aerial_beach_01_nor_gl_1k",
                0.42f, 0.55f);
            KoenigScenery.Terrain(_world,
                new Rect(-60f, SquareZ1, 120f, SandZ1 - SquareZ1), 96, 40,
                SandHeight, SandTint, sand, true);

            // Море. Уходит за дальнюю границу тумана — горизонта в кадре
            // нет и быть не должно: камера смотрит вниз, и любой край воды
            // читался бы обрывом мира.
            KoenigScenery.Sea(_world, new Rect(-120f, WaterZ, 240f, 115f), 40, 26,
                new Color(0.46f, 0.72f, 0.78f), new Color(0.10f, 0.28f, 0.44f));

            // Марам на дюнах — та самая жёсткая балтийская трава, по которой
            // дюна и опознаётся. Сажаем только на сам вал: в ложбине и на
            // мокром песке ей делать нечего.
            Color duneLow = new Color(0.34f, 0.40f, 0.20f);
            Color duneTip = new Color(0.76f, 0.80f, 0.46f);
            for (int side = -1; side <= 1; side += 2)
            {
                KoenigScenery.GrassField(_world,
                    new Vector3(side * 12f, 0f, 39.5f), new Vector2(9f, 3.6f), 5400,
                    duneLow, duneTip, 0.35f, 0.95f,
                    delegate(Vector3 p) { return SandHeight(p.x, p.z) - 0.05f; });
            }
            // Редкие кустики травы по сухому песку — чтобы пляж не был
            // стерильным полем. Ниже 42 м песок уже мокрый, там их нет.
            KoenigScenery.GrassField(_world, new Vector3(0f, 0f, 41f), new Vector2(22f, 2.6f), 1600,
                duneLow, duneTip, 0.18f, 0.5f,
                delegate(Vector3 p) { return SandHeight(p.x, p.z) - 0.05f; });

            // Перила променада по гребню дюны, с разрывом посередине под
            // проход к воде.
            Vector3 t;
            for (int i = 0; i < 12; i++)
            {
                float x = (i < 6 ? -13.5f + i * 1.7f : 4.2f + (i - 6) * 1.7f);
                if (Mathf.Abs(x) < 3.6f) continue;
                float z = 37.6f;
                KoenigProp.LoadScaled(_world, "Prop_WoodenFence_Single",
                    new Vector3(x, SandHeight(x, z) - 0.1f, z), 90f, _gs, false, out t);
            }

            // Что выносит на балтийский берег: коряги, валуны, лодка.
            Gfx.Prop(_world, "log_large", new Vector3(-7.5f, SandHeight(-7.5f, 43f), 43f), 0.75f, 62f);
            Gfx.Prop(_world, "log", new Vector3(8.5f, SandHeight(8.5f, 45f), 45f), 0.5f, 18f);
            Gfx.Prop(_world, "stump_old", new Vector3(-11f, SandHeight(-11f, 42f), 42f), 1.1f, 0f);
            Gfx.Prop(_world, "rock_smallA", new Vector3(5.5f, SandHeight(5.5f, 47f), 47f), 0.55f, 30f);
            Gfx.Prop(_world, "rock_smallC", new Vector3(-4.5f, SandHeight(-4.5f, 48f), 48f), 0.42f, 80f);
            Gfx.Prop(_world, "rock_largeB", new Vector3(-13f, SandHeight(-13f, 45f), 45f), 1.5f, 120f);
            Gfx.Prop(_world, "rock_smallE", new Vector3(12f, SandHeight(12f, 47f), 47f), 0.7f, 200f);
            Gfx.Prop(_world, "canoe", new Vector3(7.5f, SandHeight(7.5f, 42.5f), 42.5f), 0.7f, 108f);

            // Деревянные мостки в воду. Единственная вертикаль на пляже,
            // кроме маяка, — и единственное, что задаёт масштаб морю.
            Pier(new Vector3(-8.5f, 0f, 44f));

            // Маяк. Дальняя веха уровня: его видно с середины улицы, и
            // ребёнок с самого начала знает, куда идти.
            Lighthouse(new Vector3(12.5f, 0f, 41.5f));
        }

        // Мостки: настил на сваях, уходящий с песка в воду.
        private void Pier(Vector3 at)
        {
            Material wood = KoenigProp.CatMaterial("woodtrim");
            const float deck = 1.0f;
            for (int i = 0; i < 9; i++)
            {
                float z = at.z + i * 2f;
                Gfx.Box(_world, new Vector3(at.x, deck, z), new Vector3(2.6f, 0.16f, 2f), wood, true);
                if (i % 2 != 0) continue;
                for (int s = -1; s <= 1; s += 2)
                {
                    float y = SandHeight(at.x + s * 1.1f, z);
                    Gfx.Box(_world, new Vector3(at.x + s * 1.1f, (y + deck) * 0.5f, z),
                        new Vector3(0.22f, deck - y, 0.22f), wood, false);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                float z = at.z + 1f + i * 4f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Gfx.Box(_world, new Vector3(at.x + s * 1.2f, deck + 0.5f, z),
                        new Vector3(0.12f, 1f, 0.12f), wood, false);
                    Gfx.Box(_world, new Vector3(at.x + s * 1.2f, deck + 1f, z + 2f),
                        new Vector3(0.1f, 0.1f, 4f), wood, false);
                }
            }
        }

        // Маяк вместо прежней башни-короба: белый ствол с красным поясом и
        // фонарём наверху. Фонарь светит по-настоящему — точечный источник
        // хватает и песок, и мостки.
        private void Lighthouse(Vector3 at)
        {
            Material white = Gfx.Mat(new Color(0.94f, 0.93f, 0.9f), 0.12f, 0f);
            Material red = Gfx.Mat(new Color(0.72f, 0.24f, 0.2f), 0.12f, 0f);
            Material stone = Tiled("T_RockTrim", new Vector2(6f, 6f), new Color(0.7f, 0.66f, 0.58f), 1.2f);

            // ВЫСОТА ОГРАНИЧЕНА КАДРОМ, а не вкусом. При OrthoSize 6.6 и
            // наклоне 32° по вертикали кадра помещается 13.2 / 0.848 =
            // 15.6 м геометрии, и то лишь если она занимает экран целиком.
            // Девятнадцатиметровый маяк не показал бы фонарь НИКОГДА:
            // ребёнок видел бы только белый ствол, уходящий за верхний
            // край. Одиннадцать метров влезают вместе с фонарём.
            //
            // У цилиндра Unity масштаб по Y — ПОЛОВИНА высоты (примитив
            // два юнита ростом). Все числа ниже уже с этой поправкой.
            float y0 = SandHeight(at.x, at.z);
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 0.4f, 0f), new Vector3(4.6f, 0.4f, 4.6f), stone, true);
            // Ствол сужается уступами — ровный цилиндр читается трубой.
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 3.05f, 0f), new Vector3(3.4f, 2.25f, 3.4f), white, true);
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 5.9f, 0f), new Vector3(3f, 0.6f, 3f), red, true);
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 8.35f, 0f), new Vector3(2.6f, 1.85f, 2.6f), white, true);
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 10.45f, 0f), new Vector3(3.2f, 0.25f, 3.2f), stone, false);

            Material glassMat = Gfx.MatFull(new Color(1f, 0.92f, 0.65f), 0.6f, 0f,
                new Color(1f, 0.8f, 0.35f), 0f, 0f);
            Gfx.Cyl(_world, at + new Vector3(0f, y0 + 11.4f, 0f), new Vector3(2.1f, 0.7f, 2.1f), glassMat, false);

            Vector3 t;
            KoenigProp.LoadScaled(_world, "Roof_Tower_RoundTiles",
                at + new Vector3(0f, y0 + 12.1f, 0f), 0f, _gs * 0.8f, false, out t);

            Gfx.Glow(_world, at + new Vector3(0f, y0 + 11.4f, 0f), 5f, new Color(1f, 0.88f, 0.5f, 0.55f));
            Gfx.PointLight(_world, at + new Vector3(0f, y0 + 11.4f, 0f),
                new Color(1f, 0.86f, 0.55f), 24f, 2.2f);
        }

        // Население улицы: шесть пачек по четыре гриба вдоль дороги и
        // Дюнный великан у башни. Застройку не трогаем — пачки ставятся в
        // промежутки между рыбками, чтобы за наградой приходилось идти
        // сквозь бой, а не мимо него.
        //
        // Воин держит 12 урона (два-три удара по 3–6), бьёт на 3 и медленный:
        // от пачки можно уйти, а стоять в ней нельзя. Виды чередуются,
        // чтобы пачка читалась отрядом, а не клонами.
        private void SpawnPacks()
        {
            // Мостовая — короб высотой 0.2 с центром на 0.02, значит её
            // верх на 0.12. У врага нет гравитации, он едет на уровне своей
            // линии патрулирования: поставишь на ноль — утонет по щиколотку.
            const float y = 0.12f;
            // Улица теперь девять метров в ширину, а не двадцать четыре:
            // пачки прижаты к середине, иначе воины стояли бы в стенах.
            Vector3[] packs = {
                new Vector3(-2.4f, y, -31f), new Vector3(2.6f, y, -19f),
                new Vector3(-2.6f, y, -7f),  new Vector3(2.4f, y, 5f),
                new Vector3(-2.5f, y, 17f),  new Vector3(3f, y, 29f),
            };

            int id = 1;
            for (int p = 0; p < packs.Length; p++)
            {
                for (int k = 0; k < 4; k++)
                {
                    // Разброс по кругу — пачка стоит кучкой, а не в линию.
                    float ang = (k / 4f) * Mathf.PI * 2f + p;
                    Vector3 off = new Vector3(Mathf.Cos(ang) * 1.2f, 0f, Mathf.Sin(ang) * 2.2f);
                    Vector3 a = packs[p] + off;
                    Vector3 b = a + new Vector3(Mathf.Sin(ang) * 1.4f, 0f, Mathf.Cos(ang) * 3f);
                    a.x = Mathf.Clamp(a.x, -3.6f, 3.6f);
                    b.x = Mathf.Clamp(b.x, -3.6f, 3.6f);

                    string kind = (p + k) % 3 == 0 ? "swordsman"
                                : (p + k) % 3 == 1 ? "raider" : "archer";
                    Enemy e = Enemy.Spawn(_world, a, b, kind, 1.5f, id++);
                    e.Hp = 12;
                    // Было 3 урона раз в 1.5 с. Четверо в пачке снимали
                    // герою все 40 жизни за семь секунд, и он умирал ровно
                    // тогда, когда добивал пачку. Двое урона раз в две
                    // секунды дают время осмотреться и отойти.
                    e.Damage = 2;
                    e.AttackCooldown = 2f;
                    // Дистанция удара считалась от ЦЕНТРА врага плюс его
                    // радиус — вместе с ростом рыцаря это выглядело как
                    // удар по воздуху с полутора метров.
                    e.AttackRange = 0.8f;
                    // Поводок медвежьей игры (1.5 м) не давал воину сойти
                    // с маршрута: он упирался в него и бил издалека.
                    e.Leash = 30f;
                    _combat.Add(e);
                }
            }

            // Дюнный великан: 40 жизни — примерно десять попаданий, минута
            // боя с отходами. Бьёт больно и редко, чтобы успевать убегать.
            // Великан сторожит проход в дюнах: между площадью и морем не
            // пройти иначе как через него.
            float bz = 42f;
            _boss = Enemy.SpawnBoss(_world,
                new Vector3(0f, SandHeight(0f, bz), bz),
                new Vector3(-3.5f, SandHeight(-3.5f, bz + 2f), bz + 2f),
                "yeti", 1.6f, 900, 40, 1.8f);
            _boss.Damage = 6;
            _boss.AttackCooldown = 2.4f;
            _boss.AttackRange = 1.4f;
            // Великан замахивается дольше — по нему видно, что сейчас будет.
            _boss.WindUp = 0.55f;
            _boss.Leash = 30f;
            _combat.Add(_boss);
        }

        // Два сундука в стороне от дороги: за ними надо свернуть, и это
        // единственная причина уйти с прямой линии между рыбками.
        private void SpawnChests()
        {
            // Один сундук в углу площади, второй в дюнах за валом: оба
            // требуют сойти с прямой дороги к морю.
            _chests.Add(Chest.Create(_world, new Vector3(-8.5f, 0.12f, 27f), 25f, 501, 0, 0, 0));
            _chests.Add(Chest.Create(_world, new Vector3(9.5f, SandHeight(9.5f, 44f), 44f),
                -40f, 502, 0, 0, 0));
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
                "Собирай рыбок и отбивайся от воинов. За дюнами ждёт великан.",
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

            _amberText = UiKit.MakeTextRight(c, W - 24f * s, H - 70f * s,
                new Vector2(220f * s, 26f * s), "", Mathf.RoundToInt(15f * s),
                new Color(1f, 0.78f, 0.35f));

            UiKit.MakeButton(c, new Vector2(W - 96f * s, H - 112f * s),
                new Vector2(140f * s, 40f * s), "Сумка", Mathf.RoundToInt(15f * s), OpenBag);

            RefreshHud();
            RefreshHealth();
            RefreshAmber();
        }

        private void RefreshHud()
        {
            _count.text = "Рыбки: " + _collected + " / " + _total;
        }

        private void RefreshAmber()
        {
            if (_amberText != null) _amberText.text = "Янтарь: " + Inventory.Amber;
        }

        // Пока сумка открыта, мир стоит: бой выключен, управление спрятано.
        // Иначе ребёнок разбирает добычу, а его в это время едят.
        private void OpenBag()
        {
            if (_bagUi == null || _bagUi.IsOpen) return;
            Snd.Play("click");
            if (_combat != null) _combat.enabled = false;
            if (_player != null) _player.enabled = false;
            if (_tags != null) _tags.Muted = true;
            _bagUi.Open();
        }

        private void CloseBag()
        {
            if (_combat != null) _combat.enabled = true;
            if (_player != null) _player.enabled = true;
            if (_tags != null) _tags.Muted = false;
            RefreshHealth();
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
            _hpText.text = _player.Hp + " / " + _player.MaxHp;
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

            PickUpDrops(pp);

            for (int i = 0; i < _chests.Count; i++)
            {
                Chest ch = _chests[i];
                if (ch == null || !ch.TryOpen(pp)) continue;
                Inventory.AddAmber(Random.Range(8, 16));
                // В сундуке всегда что-то есть — иначе свернуть за ним
                // второй раз никто не станет. Щедрая находка вместо ролла
                // «а вдруг ничего».
                Item loot = LootTable.FromEnemy(2, Inventory.MagicFind + 1f);
                if (loot == null) loot = LootTable.FromEnemy(2, 3f);
                if (loot != null)
                    Drop.Create(_world, _world.InverseTransformPoint(
                        ch.transform.position + new Vector3(0f, 0.2f, 1.2f)), loot);
            }

            if (_tokenReady && !_won && _token != null)
            {
                _token.Rotate(0f, 50f * dt, 0f);
                Vector3 to = _token.position - pp;
                if (new Vector2(to.x, to.z).magnitude < 2f && Mathf.Abs(to.y) < 3f) Win();
            }
        }

        // Подбор шагом по вещи, как монетки. Сумка полная — вещь остаётся
        // лежать: выбрасывать что-то за ребёнка мы не имеем права.
        private void PickUpDrops(Vector3 pp)
        {
            for (int i = Drop.All.Count - 1; i >= 0; i--)
            {
                Drop d = Drop.All[i];
                if (d == null) continue;
                Vector3 to = d.transform.position - pp;
                if (new Vector2(to.x, to.z).magnitude > 1.5f || Mathf.Abs(to.y) > 2.5f) continue;

                if (Inventory.Full)
                {
                    _hint.text = "Сумка полна — загляни в неё и что-нибудь выбрось.";
                    continue;
                }
                Inventory.Add(d.Item);
                Snd.Play(d.Item.Rarity >= Rarity.Rare ? "crystal" : "coin", 0.8f);
                Object.Destroy(d.gameObject);
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
                _hint.text = "Все рыбки собраны! Теперь великан в дюнах.";
                return;
            }

            _tokenReady = true;
            _token.gameObject.SetActive(true);
            _hint.text = "Путь свободен — жетон моста лежит у самой воды.";
            Snd.Play("coin", 1f, 1.4f);
        }

        private void OnDestroy()
        {
            Inventory.Changed -= RefreshAmber;
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
