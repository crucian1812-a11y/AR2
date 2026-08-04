using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Главный экран игры-путешествия: карта Калининградской области,
    // экран артефактов и объяснение правила. Города стоят по настоящим
    // координатам (широта/долгота спроецированы на экран).
    //
    // ВАЖНО про размеры: игра портретная, поэтому масштаб берётся от
    // ШИРИНЫ экрана (ks = Screen.width / 440), а не от высоты, как в
    // ландшафтной игре про медведя. Первая версия считала от высоты, и на
    // узком экране телефона карточки шириной 700 единиц вылезали за край:
    // текст обрезался слева, кнопки налезали. Всё логическое поле — 440
    // единиц в ширину, ничего шире 430 не ставим.
    //
    // Взять точку можно двумя путями, и оба зовут один Journey.Complete:
    // панель «ты у точки», которую зажигает GPS, и ручная отметка — но она
    // спрятана за родительским кодом (ParentGate), иначе всю игру проходят
    // из дома одним пальцем.
    //
    // Третий путь, скан AR-метки, в комментарии значился, но его нет:
    // ArStation ничего не отмечает. Пока это так, метка — украшение.
    public class RegionMap : MonoBehaviour
    {
        private Transform _root;

        private Text _title, _progress;
        private Button _artBtn, _ruleBtn, _finaleBtn;

        private Image[] _node;
        private Button[] _tap;
        private Text[] _nodeLabel, _nodeBadge;
        private readonly List<Image> _pathSeg = new List<Image>();

        private readonly List<GameObject> _overlay = new List<GameObject>();
        private int _lastW, _lastH;

        private Text _gpsText;
        private Image _nearPanel;
        private Text _nearText;
        private Button _nearBtn;
        private Poi _nearPoi;
        private float _poll;

        // Родительский обход: пять быстрых нажатий по заголовку карточки
        // города открывают панель с кодом. Счётчик и введённые цифры живут
        // здесь, потому что панель перерисовывается на каждую цифру.
        private int _titleTaps;
        private float _titleTapAt;
        private string _pinEntry = "";
        private string _pinFirst = "";
        private string _pinMsg = "";
        private string _pinCity = "";

        // Живой герой на хабе: Кёня крутится в рамке, рядом — звание.
        private HeroView _heroView;
        private Image _heroFrame;
        private RawImage _heroAvatar;
        private Button _heroTap;
        private Text _rankText, _rankSub;

        // Масштаб от ширины: логическое поле 440 единиц.
        private float KS { get { return Mathf.Max(Screen.width, 1) / 440f; } }
        private int Fs(float logical) { return Mathf.Max(1, Mathf.RoundToInt(logical * KS)); }

        public static RegionMap Create(Transform parent)
        {
            GameObject go = new GameObject("RegionMap");
            go.transform.SetParent(parent, false);
            RegionMap m = go.AddComponent<RegionMap>();
            Journey.Load();
            m.Build();
            return m;
        }

        private void Build()
        {
            Canvas canvas = UiKit.CreateCanvas("KoenigMap", 32);
            canvas.transform.SetParent(transform, false);
            _root = canvas.transform;

            UiKit.MakePanel(_root, Vector2.zero, new Vector2(6000f, 6000f),
                new Color(0.16f, 0.34f, 0.5f, 1f));

            _title = UiKit.MakeText(_root, Vector2.zero, Sz(430, 40),
                "Калининградская область", Fs(24), new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);
            _progress = UiKit.MakeText(_root, Vector2.zero, Sz(430, 26), "", Fs(14),
                Color.white, TextAnchor.MiddleCenter);

            _artBtn = UiKit.MakeButton(_root, Vector2.zero, Sz(128, 46), "Сумка", Fs(15), OpenArtifacts);
            _ruleBtn = UiKit.MakeButton(_root, Vector2.zero, Sz(128, 46), "Правило", Fs(15), OpenRule);
            _finaleBtn = UiKit.MakeButton(_root, Vector2.zero, Sz(128, 46), "Финал", Fs(15), OpenFinale);

            BuildCities();
            BuildHeroBanner();

            _gpsText = UiKit.MakeText(_root, Vector2.zero, Sz(430, 24), "", Fs(13),
                new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleCenter);

            _nearPanel = UiKit.MakePanel(_root, Vector2.zero, Sz(420, 84),
                new Color(0.12f, 0.4f, 0.24f, 0.97f));
            _nearText = UiKit.MakeText(_root, Vector2.zero, Sz(400, 40), "", Fs(15),
                Color.white, TextAnchor.MiddleCenter);
            _nearBtn = UiKit.MakeButton(_root, Vector2.zero, Sz(220, 40), "Получить артефакт",
                Fs(15), ClaimNear);
            ShowNear(false);

            LocationGate.Ensure(transform);

            Layout();
            Refresh();
        }

        private Vector2 Sz(float w, float h) { return new Vector2(w * KS, h * KS); }

        private void ShowNear(bool v)
        {
            if (_nearPanel != null) _nearPanel.gameObject.SetActive(v);
            if (_nearText != null) _nearText.gameObject.SetActive(v);
            if (_nearBtn != null) _nearBtn.image.gameObject.SetActive(v);
        }

        private void ClaimNear()
        {
            if (_nearPoi == null) return;
            Journey.Complete(_nearPoi.Id);
            Snd.Play("coin");
            _nearPoi = null;
            ShowNear(false);
            CloseOverlay();
            Refresh();
        }

        // ---------- Проекция координат ----------

        private void MapBounds(out double lonMin, out double lonMax,
                               out double latMin, out double latMax)
        {
            lonMin = latMin = 1e9; lonMax = latMax = -1e9;
            City[] cs = KoenigContent.Cities;
            for (int i = 0; i < cs.Length; i++)
            {
                if (cs[i].Lon < lonMin) lonMin = cs[i].Lon;
                if (cs[i].Lon > lonMax) lonMax = cs[i].Lon;
                if (cs[i].Lat < latMin) latMin = cs[i].Lat;
                if (cs[i].Lat > latMax) latMax = cs[i].Lat;
            }
        }

        private Vector2 CityScreen(City c)
        {
            double lonMin, lonMax, latMin, latMax;
            MapBounds(out lonMin, out lonMax, out latMin, out latMax);
            // Поле карты с полями по краям, чтобы подписи городов не
            // упирались в границы экрана.
            float w = Screen.width * 0.62f;
            float h = Screen.height * 0.4f;
            float left = Screen.width * 0.19f;
            float bottom = Screen.height * 0.31f;
            double fx = (lonMax > lonMin) ? (c.Lon - lonMin) / (lonMax - lonMin) : 0.5;
            double fy = (latMax > latMin) ? (c.Lat - latMin) / (latMax - latMin) : 0.5;
            return new Vector2(left + (float)fx * w, bottom + (float)fy * h);
        }

        private void BuildCities()
        {
            City[] cs = KoenigContent.Cities;
            for (int i = 0; i < KoenigContent.JourneyOrder.Length - 1; i++)
                _pathSeg.Add(UiKit.MakePanel(_root, Vector2.zero, new Vector2(10f, 6f),
                    new Color(1f, 0.85f, 0.5f, 0.5f)));

            _node = new Image[cs.Length];
            _tap = new Button[cs.Length];
            _nodeLabel = new Text[cs.Length];
            _nodeBadge = new Text[cs.Length];
            for (int i = 0; i < cs.Length; i++)
            {
                int idx = i;
                _node[i] = UiKit.MakeImage(_root, Vector2.zero, Sz(46, 46), Gfx.CircleSprite(), Color.white);
                _tap[i] = UiKit.MakeButton(_root, Vector2.zero, Sz(56, 56), "", 1,
                    delegate { OpenCity(KoenigContent.Cities[idx].Id); });
                _tap[i].image.color = new Color(1f, 1f, 1f, 0f);
                _nodeLabel[i] = UiKit.MakeText(_root, Vector2.zero, Sz(132, 24), cs[i].Name, Fs(14),
                    Color.white, TextAnchor.MiddleCenter);
                _nodeBadge[i] = UiKit.MakeText(_root, Vector2.zero, Sz(110, 22), "", Fs(13),
                    new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
            }
        }

        // Баннер героя: живая модель проводника + звание. Именно этого не
        // хватало, чтобы игра читалась как RPG, — самого героя на экране.
        private void BuildHeroBanner()
        {
            _heroFrame = UiKit.MakePanel(_root, Vector2.zero, Sz(82, 100),
                new Color(0.08f, 0.16f, 0.26f, 1f));
            _heroView = HeroView.Create(transform, Guide.ModelId, 176, 220, 2, 30f);
            _heroAvatar = UiKit.MakeRaw(_root, Vector2.zero, Sz(74, 92), _heroView.Texture);

            _rankText = UiKit.MakeText(_root, Vector2.zero, Sz(232, 28), "", Fs(17),
                new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleLeft);
            _rankSub = UiKit.MakeText(_root, Vector2.zero, Sz(240, 40), "", Fs(12),
                new Color(0.8f, 0.86f, 0.95f), TextAnchor.UpperLeft);

            // Прозрачная кнопка на весь баннер — тап открывает экран героя.
            _heroTap = UiKit.MakeButton(_root, Vector2.zero, Sz(422, 100), "", 1, OpenHero);
            _heroTap.image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void OpenHero()
        {
            CloseOverlay();
            Snd.Play("click");
            HeroScreen.Create(transform);
        }

        // Запуск игрового уровня города. Прячем карту (канвас и опрос GPS) и
        // отдаём экран уровню — его камера с высоким depth перекрывает хаб.
        // По выходу возвращаемся и обновляемся: цифровой жетон мог смениться.
        private void PlayCity(string cityId)
        {
            CloseOverlay();
            Snd.Play("click");
            _root.gameObject.SetActive(false);
            enabled = false;
            KoenigLevel.Create(transform, cityId, delegate
            {
                _root.gameObject.SetActive(true);
                enabled = true;
                Layout();
                Refresh();
            });
        }

        // ---------- Модальные экраны ----------

        private void CloseOverlay()
        {
            for (int i = 0; i < _overlay.Count; i++)
                if (_overlay[i] != null) Object.Destroy(_overlay[i]);
            _overlay.Clear();
        }

        private void Keep(Object o)
        {
            Component c = o as Component;
            if (c != null) _overlay.Add(c.gameObject);
        }

        // Затемнение + карточка во всю ширину. Возвращает центр экрана.
        private Vector2 Card(float hLogical, string title)
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            Keep(UiKit.MakePanel(_root, new Vector2(cx, cy), new Vector2(6000f, 6000f),
                new Color(0f, 0f, 0f, 0.6f)));
            Keep(UiKit.MakePanel(_root, new Vector2(cx, cy), new Vector2(Screen.width * 0.94f, hLogical * KS),
                new Color(0.1f, 0.16f, 0.24f, 0.99f)));
            Keep(UiKit.MakeText(_root, new Vector2(cx, cy + (hLogical * 0.5f - 26f) * KS),
                Sz(400, 36), title, Fs(21), new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter));
            Keep(UiKit.MakeButton(_root, new Vector2(cx, cy - (hLogical * 0.5f - 30f) * KS),
                Sz(180, 44), "Назад", Fs(17), CloseOverlay));
            return new Vector2(cx, cy);
        }

        private void OpenCity(string cityId)
        {
            CloseOverlay();
            City c = KoenigContent.FindCity(cityId);
            List<Quest> qs = QuestLog.ForCity(cityId);
            bool hasLevel = KoenigLevel.HasLevel(cityId);
            float hLog = 96f + qs.Count * 132f + (hasLevel ? 64f : 0f);
            Vector2 ctr = Card(hLog, (c != null ? c.Name : "") + " · задания");
            float top = ctr.y + (hLog * 0.5f - 70f) * KS;
            float rowW = Screen.width * 0.88f;

            // Кнопка «поиграть уровень» — цифровая половина квеста города.
            if (hasLevel)
            {
                bool fused = DigitalProgress.IsFused(cityId);
                bool digital = DigitalProgress.IsDone(cityId);
                string label = "▶ Играть уровень" + (digital ? " (пройден)" : "");
                string pid = cityId;
                Keep(UiKit.MakeButton(_root, new Vector2(ctr.x, top),
                    new Vector2(rowW, 50f * KS), label, Fs(17),
                    delegate { PlayCity(pid); }));
                Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 32f * KS),
                    Sz(420, 22), fused ? "🌉 Мост золотой: игра + реальность"
                        : digital ? "🎮 Цифровой жетон есть — нужен настоящий, на месте"
                        : "Собери цифровой жетон в игре", Fs(12),
                    fused ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.86f, 0.95f),
                    TextAnchor.MiddleCenter));
                top -= 64f * KS;
            }

            for (int i = 0; i < qs.Count; i++)
            {
                Quest q = qs[i];
                Poi poi = q.Poi;
                float y = top - i * 128f * KS;
                float cx = ctr.x;
                // Внутренние края строки и две колонки: слева — название и
                // задание, справа — награда и статус. Ширины подобраны так,
                // чтобы колонки не налезали и не уезжали за экран.
                float lx = cx - rowW * 0.5f + 14f * KS;
                float rx = cx + rowW * 0.5f - 14f * KS;
                float leftColW = rowW - 200f * KS;
                float rightColW = 172f * KS;

                Keep(UiKit.MakePanel(_root, new Vector2(cx, y), new Vector2(rowW, 118f * KS),
                    new Color(0.14f, 0.22f, 0.32f, 1f)));
                Keep(UiKit.MakeTextLeft(_root, lx, y + 40f * KS, new Vector2(leftColW, 24f * KS),
                    poi.Name, Fs(15), Color.white));
                Keep(UiKit.MakeTextLeft(_root, lx, y + 4f * KS, new Vector2(leftColW, 46f * KS),
                    poi.ChildTask, Fs(12), new Color(0.8f, 0.86f, 0.95f), TextAnchor.UpperLeft));

                // Награда и хомлин — правый верхний угол строки.
                string marks = ArtifactShort(poi.Gives);
                if (q.HasHomlin) marks += " · хомлин";
                Keep(UiKit.MakeTextRight(_root, rx, y + 40f * KS, new Vector2(rightColW, 22f * KS),
                    marks, Fs(11), new Color(1f, 0.82f, 0.45f)));

                // Нижний ряд: AR-кнопка слева, состояние/отметка справа.
                if (q.HasAr)
                {
                    string art = poi.ArTarget;
                    Keep(UiKit.MakeButton(_root, new Vector2(lx + 46f * KS, y - 34f * KS),
                        Sz(88, 36), "AR", Fs(14), delegate { ArStation.Open(art); }));
                }
                if (q.State == QuestState.Done)
                    Keep(UiKit.MakeTextRight(_root, rx, y - 34f * KS, new Vector2(rightColW, 30f * KS),
                        "✓ Пройдено", Fs(13), new Color(0.5f, 0.9f, 0.5f)));
                else if (q.State == QuestState.Locked)
                    Keep(UiKit.MakeTextRight(_root, rx, y - 34f * KS, new Vector2(rightColW, 30f * KS),
                        "сначала прошлые города", Fs(11), new Color(0.7f, 0.72f, 0.78f)));
                else if (ParentGate.Unlocked)
                {
                    // Обход открыт взрослым — показываем кнопку. Она гаснет
                    // сама вместе с открытием, через пять минут.
                    string pid = poi.Id;
                    Keep(UiKit.MakeButton(_root, new Vector2(rx - 75f * KS, y - 34f * KS),
                        Sz(150, 38), "Отметить", Fs(14), delegate { CompletePoi(pid, cityId); }));
                }
                else
                    Keep(UiKit.MakeTextRight(_root, rx, y - 34f * KS, new Vector2(rightColW, 30f * KS),
                        "📍 отмечается на месте", Fs(11), new Color(0.72f, 0.78f, 0.86f)));
            }

            // Невидимая область поверх заголовка карточки — вход в панель
            // взрослого. Заголовок рисует Card(), и по той же формуле мы
            // кладём кнопку ровно на него. Своей картинки у неё нет:
            // ребёнок не должен видеть, что здесь что-то есть.
            string cid = cityId;
            Button secret = UiKit.MakeButton(_root, new Vector2(ctr.x, ctr.y + (hLog * 0.5f - 26f) * KS),
                Sz(300, 40), "", 1, delegate { TapTitle(cid); });
            secret.image.color = new Color(1f, 1f, 1f, 0f);
            Keep(secret);

            if (ParentGate.Unlocked)
                Keep(UiKit.MakeText(_root, new Vector2(ctr.x, ctr.y - (hLog * 0.5f - 62f) * KS),
                    Sz(420, 22), "Взрослый режим · " + ParentGate.MinutesLeft + " мин", Fs(11),
                    new Color(1f, 0.78f, 0.45f), TextAnchor.MiddleCenter));
        }

        // Пять нажатий подряд, не медленнее секунды между ними. Случайно
        // столько не наберёшь, а взрослый, который знает, попадает с первого
        // раза. Настоящая защита дальше — код.
        private void TapTitle(string cityId)
        {
            float now = Time.unscaledTime;
            if (now - _titleTapAt > 1f) _titleTaps = 0;
            _titleTapAt = now;
            _titleTaps++;
            if (_titleTaps < 5) return;

            _titleTaps = 0;
            _pinEntry = "";
            _pinFirst = "";
            _pinMsg = "";
            _pinCity = cityId;
            OpenParentPad();
        }

        // ---------- Панель взрослого ----------

        private void OpenParentPad()
        {
            CloseOverlay();
            Vector2 ctr = Card(470f, "Взрослым");
            float top = ctr.y + 160f * KS;

            if (ParentGate.Unlocked)
            {
                Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top), Sz(400, 80),
                    "Обход открыт ещё " + ParentGate.MinutesLeft + " мин.\n" +
                    "Кнопки «Отметить» видны в списке заданий.", Fs(14),
                    new Color(0.85f, 0.92f, 1f), TextAnchor.UpperCenter));
                Keep(UiKit.MakeButton(_root, new Vector2(ctr.x, top - 120f * KS),
                    Sz(240, 48), "Запереть сейчас", Fs(15), delegate
                    {
                        ParentGate.Lock();
                        OpenCity(_pinCity);
                    }));
                return;
            }

            bool setup = !ParentGate.HasPin;
            string prompt = setup
                ? (_pinFirst.Length == PinLen ? "Повторите код" : "Придумайте код из 4 цифр")
                : "Код взрослого";

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top), Sz(400, 46),
                prompt, Fs(16), Color.white, TextAnchor.UpperCenter));
            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 34f * KS), Sz(400, 40),
                setup ? "Он понадобится, чтобы отметить точку, не стоя на ней."
                      : "Отметить точку из дома может только взрослый.",
                Fs(11), new Color(0.72f, 0.8f, 0.9f), TextAnchor.UpperCenter));

            // Точки-заполнители: видно, сколько цифр уже набрано.
            string dots = "";
            for (int i = 0; i < PinLen; i++) dots += (i < _pinEntry.Length ? "●" : "·") + "  ";
            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 82f * KS), Sz(400, 40),
                dots, Fs(26), new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter));

            if (_pinMsg.Length > 0)
                Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 118f * KS), Sz(400, 24),
                    _pinMsg, Fs(12), new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter));

            // Клавиатура 3×4: 1–9, стереть, 0, отмена.
            float padTop = top - 152f * KS;
            for (int i = 0; i < 9; i++)
            {
                int digit = i + 1;
                Keep(UiKit.MakeButton(_root,
                    new Vector2(ctr.x + ((i % 3) - 1) * 104f * KS, padTop - (i / 3) * 58f * KS),
                    Sz(92, 50), digit.ToString(), Fs(20), delegate { PinDigit(digit); }));
            }
            Keep(UiKit.MakeButton(_root, new Vector2(ctr.x - 104f * KS, padTop - 174f * KS),
                Sz(92, 50), "←", Fs(20), PinErase));
            Keep(UiKit.MakeButton(_root, new Vector2(ctr.x, padTop - 174f * KS),
                Sz(92, 50), "0", Fs(20), delegate { PinDigit(0); }));
            Keep(UiKit.MakeButton(_root, new Vector2(ctr.x + 104f * KS, padTop - 174f * KS),
                Sz(92, 50), "✕", Fs(18), delegate { OpenCity(_pinCity); }));
        }

        private const int PinLen = ParentGate.PinLength;

        private void PinDigit(int d)
        {
            if (_pinEntry.Length >= PinLen) return;
            _pinMsg = "";
            _pinEntry += d.ToString();
            if (_pinEntry.Length < PinLen) { OpenParentPad(); return; }

            if (!ParentGate.HasPin)
            {
                if (_pinFirst.Length < PinLen)
                {
                    // Первый ввод — запомнили и просим повторить.
                    _pinFirst = _pinEntry;
                    _pinEntry = "";
                }
                else if (_pinFirst == _pinEntry)
                {
                    ParentGate.SetPin(_pinEntry);
                    OpenCity(_pinCity);
                    return;
                }
                else
                {
                    _pinFirst = "";
                    _pinEntry = "";
                    _pinMsg = "Коды не совпали — начните заново";
                }
            }
            else if (ParentGate.TryUnlock(_pinEntry))
            {
                Snd.Play("quest");
                OpenCity(_pinCity);
                return;
            }
            else
            {
                _pinEntry = "";
                _pinMsg = "Неверный код";
            }
            OpenParentPad();
        }

        private void PinErase()
        {
            if (_pinEntry.Length > 0) _pinEntry = _pinEntry.Substring(0, _pinEntry.Length - 1);
            _pinMsg = "";
            OpenParentPad();
        }

        // Отметка без GPS. Единственный вход сюда — список заданий при
        // открытом родительском обходе; сам по себе метод ничего не
        // проверяет, и добавлять к нему второй вызов нельзя.
        private void CompletePoi(string poiId, string cityId)
        {
            Journey.Complete(poiId);
            Snd.Play("coin");
            Refresh();
            OpenCity(cityId);
        }

        private void OpenArtifacts()
        {
            CloseOverlay();
            Vector2 ctr = Card(560f, "Сумка Кёни");
            float top = ctr.y + 200f * KS;

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top), Sz(420, 24),
                "Печати земель — " + Journey.Count(Artifact.LandSeal) + " из 4", Fs(15),
                Color.white, TextAnchor.MiddleCenter));
            for (int i = 0; i < KoenigContent.Lands.Length; i++)
                Chip(new Vector2(ctr.x + (i - 1.5f) * 102f * KS, top - 42f * KS),
                    KoenigContent.Lands[i].Name, new Color(0.55f, 0.7f, 0.45f),
                    Journey.HasLandSeal(i.ToString()));

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 92f * KS), Sz(420, 24),
                "Жетоны мостов — " + Journey.Count(Artifact.BridgeToken) + " из 7", Fs(15),
                Color.white, TextAnchor.MiddleCenter));
            BridgeGraph g = KoenigContent.BuildGraph();
            for (int i = 0; i < g.Bridges.Count; i++)
                Chip(new Vector2(ctr.x + ((i % 4) - 1.5f) * 102f * KS, top - 132f * KS - (i / 4) * 50f * KS),
                    g.Bridges[i].Name, new Color(0.85f, 0.62f, 0.3f),
                    Journey.HasBridgeToken(g.Bridges[i].Id));

            Chip(new Vector2(ctr.x - 96f * KS, top - 296f * KS), "Ключ Эйлера",
                new Color(0.5f, 0.75f, 0.95f), Journey.Count(Artifact.EulerKey) > 0);
            Chip(new Vector2(ctr.x + 96f * KS, top - 296f * KS), "Золотой мост",
                new Color(0.9f, 0.78f, 0.3f), Journey.PuzzleSolved);

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 348f * KS), Sz(420, 44),
                Journey.PuzzleUnlocked ? "Все жетоны собраны — финал открыт!"
                    : "Собери все семь жетонов мостов, чтобы открыть финал.", Fs(14),
                Journey.PuzzleUnlocked ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.8f, 0.85f, 0.95f),
                TextAnchor.MiddleCenter));
        }

        private void Chip(Vector2 pos, string label, Color color, bool filled)
        {
            Color c = filled ? color : new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.6f);
            Keep(UiKit.MakePanel(_root, pos, Sz(96, 38), c));
            Keep(UiKit.MakeText(_root, pos, Sz(92, 34), (filled ? "✓ " : "") + label, Fs(11),
                filled ? Color.white : new Color(0.7f, 0.72f, 0.76f), TextAnchor.MiddleCenter));
        }

        private void OpenRule()
        {
            CloseOverlay();
            Vector2 ctr = Card(560f, "Правило семи мостов");

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, ctr.y + 150f * KS), Sz(410, 180),
                KoenigContent.EulerRuleForKids, Fs(15), Color.white, TextAnchor.UpperCenter));

            BridgeGraph g = KoenigContent.BuildGraph();
            Vector2[] slot = {
                new Vector2(ctr.x - 84f * KS, ctr.y - 30f * KS),
                new Vector2(ctr.x + 84f * KS, ctr.y - 30f * KS),
                new Vector2(ctr.x - 84f * KS, ctr.y - 150f * KS),
                new Vector2(ctr.x + 84f * KS, ctr.y - 150f * KS),
            };
            for (int i = 0; i < g.Lands.Length; i++)
            {
                int d = g.Degree(i);
                bool odd = (d & 1) == 1;
                Keep(UiKit.MakeImage(_root, slot[i], Sz(52, 52), Gfx.CircleSprite(),
                    odd ? new Color(0.9f, 0.45f, 0.4f) : new Color(0.5f, 0.7f, 0.45f)));
                Keep(UiKit.MakeText(_root, slot[i], Sz(52, 52), d.ToString(), Fs(19),
                    Color.white, TextAnchor.MiddleCenter));
                Keep(UiKit.MakeText(_root, slot[i] + new Vector2(0f, -36f * KS), Sz(150, 22),
                    g.Lands[i] + (odd ? " · нечёт" : " · чёт"), Fs(12),
                    odd ? new Color(1f, 0.7f, 0.6f) : new Color(0.7f, 0.9f, 0.7f), TextAnchor.MiddleCenter));
            }
            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, ctr.y - 210f * KS), Sz(420, 30),
                "Четыре нечётных угла — на два больше, чем можно.", Fs(13),
                new Color(1f, 0.8f, 0.6f), TextAnchor.MiddleCenter));
        }

        private static string ArtifactShort(Artifact a)
        {
            if (a == Artifact.LandSeal) return "печать земли";
            if (a == Artifact.BridgeToken) return "жетон моста";
            if (a == Artifact.EulerKey) return "ключ Эйлера";
            return "золотой мост";
        }

        private void OpenFinale()
        {
            if (!Journey.PuzzleUnlocked) { Snd.Play("hurt", 0.6f); return; }
            CloseOverlay();
            Snd.Play("click");
            BridgePuzzle.Create(transform, Refresh);
        }

        // ---------- Раскладка и обновление ----------

        private void Layout()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            float cx = Screen.width * 0.5f;
            float H = Screen.height;

            _title.rectTransform.anchoredPosition = new Vector2(cx, H - 34f * KS);
            _progress.rectTransform.anchoredPosition = new Vector2(cx, H - 62f * KS);

            // Баннер героя — полосой под заголовком, над картой городов.
            float by = H * 0.82f;
            _heroFrame.rectTransform.anchoredPosition = new Vector2(Screen.width * 0.2f, by);
            _heroAvatar.rectTransform.anchoredPosition = new Vector2(Screen.width * 0.2f, by);
            // Текст звания — левым краем справа от аватара (pivot по центру,
            // потому сдвигаем на полширины), иначе налезал на портрет Кёни.
            float rankLeft = Screen.width * 0.31f;
            _rankText.rectTransform.anchoredPosition =
                new Vector2(rankLeft + _rankText.rectTransform.sizeDelta.x * 0.5f, by + 16f * KS);
            _rankSub.rectTransform.anchoredPosition =
                new Vector2(rankLeft + _rankSub.rectTransform.sizeDelta.x * 0.5f, by - 6f * KS);
            _heroTap.image.rectTransform.anchoredPosition = new Vector2(cx, by);

            // Нижний ряд из трёх кнопок — по долям ширины, чтобы влезали.
            _artBtn.image.rectTransform.anchoredPosition = new Vector2(Screen.width * 0.2f, 40f * KS);
            _ruleBtn.image.rectTransform.anchoredPosition = new Vector2(Screen.width * 0.5f, 40f * KS);
            _finaleBtn.image.rectTransform.anchoredPosition = new Vector2(Screen.width * 0.8f, 40f * KS);

            _gpsText.rectTransform.anchoredPosition = new Vector2(cx, 92f * KS);
            _nearPanel.rectTransform.anchoredPosition = new Vector2(cx, 146f * KS);
            _nearText.rectTransform.anchoredPosition = new Vector2(cx, 162f * KS);
            _nearBtn.image.rectTransform.anchoredPosition = new Vector2(cx, 132f * KS);

            City[] cs = KoenigContent.Cities;
            for (int i = 0; i < cs.Length; i++)
            {
                Vector2 p = CityScreen(cs[i]);
                _node[i].rectTransform.anchoredPosition = p;
                _tap[i].image.rectTransform.anchoredPosition = p;
                _nodeLabel[i].rectTransform.anchoredPosition = p + new Vector2(0f, -36f * KS);
                _nodeBadge[i].rectTransform.anchoredPosition = p + new Vector2(0f, 34f * KS);
            }

            string[] order = KoenigContent.JourneyOrder;
            for (int i = 0; i < _pathSeg.Count; i++)
            {
                Vector2 p0 = CityScreen(KoenigContent.FindCity(order[i]));
                Vector2 p1 = CityScreen(KoenigContent.FindCity(order[i + 1]));
                float dx = p1.x - p0.x, dy = p1.y - p0.y;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                RectTransform rt = _pathSeg[i].rectTransform;
                rt.anchoredPosition = new Vector2((p0.x + p1.x) * 0.5f, (p0.y + p1.y) * 0.5f);
                rt.sizeDelta = new Vector2(len, 5f * KS);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                CloseOverlay();
                Layout();
            }

            _poll += Time.unscaledDeltaTime;
            if (_poll >= 1f) { _poll = 0f; PollLocation(); }
        }

        private void PollLocation()
        {
            LocationGate gate = LocationGate.I;
            if (gate == null) return;
            _gpsText.text = gate.Status;

            Poi poi; float meters;
            if (gate.Nearest(out poi, out meters) && gate.WithinRadius(poi, meters))
            {
                _nearPoi = poi;
                _nearText.text = "📍 Ты у точки «" + poi.Name + "» (" + Mathf.RoundToInt(meters) + " м)";
                ShowNear(true);
            }
            else
            {
                _nearPoi = null;
                ShowNear(false);
                if (gate.HasFix && poi != null)
                    _gpsText.text = "Ближайшая точка: «" + poi.Name + "» — " + FormatDist(meters);
            }
        }

        // Расстояние по-человечески: метры вблизи, километры вдали. Раньше
        // показывало «1083005 м» — на экране это нечитаемо.
        private static string FormatDist(float meters)
        {
            if (meters < 950f) return Mathf.RoundToInt(meters) + " м";
            return (meters / 1000f).ToString("0.#") + " км";
        }

        private void Refresh()
        {
            _progress.text = "Пройдено точек: " + Journey.DonePoints + " из " + Journey.TotalPoints;

            _rankText.text = HeroRank.Title;
            _rankSub.text = "Уровень " + HeroRank.Level + " · нажми ›";

            string cur = Journey.CurrentCity();
            int curStep = KoenigContent.CityStep(cur);
            City[] cs = KoenigContent.Cities;
            for (int i = 0; i < cs.Length; i++)
            {
                List<Quest> qs = QuestLog.ForCity(cs[i].Id);
                int done = 0;
                for (int k = 0; k < qs.Count; k++) if (qs[k].State == QuestState.Done) done++;
                bool allDone = qs.Count > 0 && done == qs.Count;
                bool isCur = cs[i].Id == cur;
                bool locked = KoenigContent.CityStep(cs[i].Id) > curStep;

                _node[i].color = allDone ? new Color(0.5f, 0.85f, 0.5f)
                    : isCur ? new Color(1f, 0.85f, 0.4f)
                    : locked ? new Color(0.4f, 0.44f, 0.5f)
                    : new Color(0.72f, 0.8f, 0.88f);
                _nodeBadge[i].text = allDone ? "✓" : isCur ? "вы здесь" : locked ? "заперто" : "";
            }

            _finaleBtn.interactable = Journey.PuzzleUnlocked;
        }
    }
}
