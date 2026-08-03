using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Главный экран игры-путешествия: карта Калининградской области с
    // четырьмя городами, экран артефактов и объяснение правила. Города
    // стоят по настоящим координатам (широта/долгота спроецированы на
    // экран), поэтому Зеленоградск на северо-востоке, Балтийск на
    // юго-западе — как в жизни.
    //
    // Хаб, к которому ребёнок возвращается: тапнул город → список его
    // заданий; собрал все жетоны мостов → открывается финальная
    // головоломка. Точку можно взять тремя путями: подойти по GPS (баннер
    // «ты у точки — получить»), открыть AR-станцию и отсканировать метку,
    // либо родитель отмечает вручную. Все три зовут один Journey.Complete.
    //
    // Все элементы висят прямо на канвасе в экранных координатах, а
    // модальные экраны (задания, сумка, правило) — временные наборы,
    // которые создаются при открытии и уничтожаются при закрытии. Так
    // надёжнее вложенных RectTransform.
    public class RegionMap : MonoBehaviour
    {
        private Canvas _canvas;
        private Transform _root;

        private Text _title, _progress;
        private Button _artBtn, _ruleBtn, _finaleBtn;

        private Image[] _node;
        private Button[] _tap;
        private Text[] _nodeLabel, _nodeBadge;
        private readonly List<Image> _pathSeg = new List<Image>();

        private readonly List<GameObject> _overlay = new List<GameObject>();
        private int _lastW, _lastH;

        // GPS: статус и баннер «ты рядом с точкой — получить артефакт».
        private Text _gpsText;
        private Image _nearPanel;
        private Text _nearText;
        private Button _nearBtn;
        private Poi _nearPoi;
        private float _poll;

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
            _canvas = UiKit.CreateCanvas("KoenigMap", 32);
            _canvas.transform.SetParent(transform, false);
            _root = _canvas.transform;
            float s = UiKit.Scale;

            UiKit.MakePanel(_root, Vector2.zero, new Vector2(4000f, 4000f),
                new Color(0.16f, 0.34f, 0.5f, 1f));

            _title = UiKit.MakeText(_root, Vector2.zero, new Vector2(760f * s, 42f * s),
                "Калининградская область", Mathf.RoundToInt(28f * s),
                new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);
            _progress = UiKit.MakeText(_root, Vector2.zero, new Vector2(760f * s, 28f * s),
                "", Mathf.RoundToInt(17f * s), Color.white, TextAnchor.MiddleCenter);

            _artBtn = UiKit.MakeButton(_root, Vector2.zero, new Vector2(180f * s, 44f * s),
                "Сумка", Mathf.RoundToInt(18f * s), OpenArtifacts);
            _ruleBtn = UiKit.MakeButton(_root, Vector2.zero, new Vector2(180f * s, 44f * s),
                "Правило", Mathf.RoundToInt(18f * s), OpenRule);
            _finaleBtn = UiKit.MakeButton(_root, Vector2.zero, new Vector2(240f * s, 46f * s),
                "Финал: мосты", Mathf.RoundToInt(18f * s), OpenFinale);

            BuildCities();

            _gpsText = UiKit.MakeText(_root, Vector2.zero, new Vector2(760f * s, 24f * s),
                "", Mathf.RoundToInt(14f * s), new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleCenter);

            // Баннер близости — держим готовым, показываем при подходе.
            _nearPanel = UiKit.MakePanel(_root, Vector2.zero, new Vector2(720f * s, 64f * s),
                new Color(0.12f, 0.4f, 0.24f, 0.96f));
            _nearText = UiKit.MakeText(_root, Vector2.zero, new Vector2(470f * s, 56f * s),
                "", Mathf.RoundToInt(16f * s), Color.white, TextAnchor.MiddleLeft);
            _nearBtn = UiKit.MakeButton(_root, Vector2.zero, new Vector2(190f * s, 48f * s),
                "Получить артефакт", Mathf.RoundToInt(16f * s), ClaimNear);
            ShowNear(false);

            LocationGate.Ensure(transform);

            Layout();
            Refresh();
        }

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
            float w = Screen.width * 0.62f;
            float h = Screen.height * 0.46f;
            float left = Screen.width * 0.5f - w * 0.5f;
            float bottom = Screen.height * 0.5f - h * 0.4f;
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
            float s = UiKit.Scale;
            for (int i = 0; i < cs.Length; i++)
            {
                int idx = i;
                _node[i] = UiKit.MakeImage(_root, Vector2.zero, new Vector2(54f * s, 54f * s),
                    Gfx.CircleSprite(), Color.white);
                _tap[i] = UiKit.MakeButton(_root, Vector2.zero, new Vector2(58f * s, 58f * s),
                    "", 1, delegate { OpenCity(KoenigContent.Cities[idx].Id); });
                _tap[i].image.color = new Color(1f, 1f, 1f, 0f);
                _nodeLabel[i] = UiKit.MakeText(_root, Vector2.zero, new Vector2(170f * s, 26f * s),
                    cs[i].Name, Mathf.RoundToInt(17f * s), Color.white, TextAnchor.MiddleCenter);
                _nodeBadge[i] = UiKit.MakeText(_root, Vector2.zero, new Vector2(120f * s, 24f * s),
                    "", Mathf.RoundToInt(15f * s), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
            }
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
            // UiKit возвращает компонент; храним его GameObject для удаления.
            Component c = o as Component;
            if (c != null) _overlay.Add(c.gameObject);
        }

        // Затемнение + карточка. Возвращает центр экрана для контента.
        private Vector2 Card(float w, float h, string title)
        {
            float s = UiKit.Scale;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            Keep(UiKit.MakePanel(_root, new Vector2(cx, cy), new Vector2(4000f, 4000f),
                new Color(0f, 0f, 0f, 0.55f)));
            Keep(UiKit.MakePanel(_root, new Vector2(cx, cy), new Vector2(w * s, h * s),
                new Color(0.1f, 0.16f, 0.24f, 0.99f)));
            Keep(UiKit.MakeText(_root, new Vector2(cx, cy + (h * 0.5f - 30f) * s),
                new Vector2((w - 60f) * s, 40f * s), title, Mathf.RoundToInt(24f * s),
                new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter));
            Keep(UiKit.MakeButton(_root, new Vector2(cx, cy - (h * 0.5f - 32f) * s),
                new Vector2(200f * s, 44f * s), "Назад", Mathf.RoundToInt(18f * s), CloseOverlay));
            return new Vector2(cx, cy);
        }

        private void OpenCity(string cityId)
        {
            CloseOverlay();
            City c = KoenigContent.FindCity(cityId);
            Vector2 ctr = Card(700f, 640f, (c != null ? c.Name : "") + " · задания");
            float s = UiKit.Scale;
            List<Quest> qs = QuestLog.ForCity(cityId);
            float top = ctr.y + 230f * s;
            for (int i = 0; i < qs.Count; i++)
            {
                Quest q = qs[i];
                Poi poi = q.Poi;
                float y = top - i * 120f * s;
                float cx = ctr.x;
                Keep(UiKit.MakePanel(_root, new Vector2(cx, y), new Vector2(624f * s, 106f * s),
                    new Color(0.14f, 0.22f, 0.32f, 1f)));
                Keep(UiKit.MakeText(_root, new Vector2(cx - 148f * s, y + 32f * s),
                    new Vector2(320f * s, 26f * s), poi.Name, Mathf.RoundToInt(17f * s),
                    Color.white, TextAnchor.MiddleLeft));
                Keep(UiKit.MakeText(_root, new Vector2(cx - 148f * s, y - 6f * s),
                    new Vector2(330f * s, 46f * s), poi.ChildTask, Mathf.RoundToInt(14f * s),
                    new Color(0.8f, 0.86f, 0.95f), TextAnchor.UpperLeft));

                string marks = ArtifactShort(poi.Gives);
                if (q.HasHomlin) marks += " · хомлин";
                Keep(UiKit.MakeText(_root, new Vector2(cx + 150f * s, y + 34f * s),
                    new Vector2(160f * s, 24f * s), marks, Mathf.RoundToInt(12f * s),
                    new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleRight));

                // AR-станция: навести камеру на печатную метку у места.
                if (q.HasAr)
                {
                    string art = poi.ArTarget;
                    Keep(UiKit.MakeButton(_root, new Vector2(cx + 92f * s, y - 24f * s),
                        new Vector2(104f * s, 38f * s), "AR",
                        Mathf.RoundToInt(14f * s), delegate { ArStation.Open(art); }));
                }

                if (q.State == QuestState.Done)
                    Keep(UiKit.MakeText(_root, new Vector2(cx + 150f * s, y - 22f * s),
                        new Vector2(160f * s, 30f * s), "✓ Пройдено", Mathf.RoundToInt(15f * s),
                        new Color(0.5f, 0.9f, 0.5f), TextAnchor.MiddleRight));
                else if (q.State == QuestState.Locked)
                    // Город ещё впереди по пути — отметить его точки нельзя,
                    // иначе порядок путешествия обходится.
                    Keep(UiKit.MakeText(_root, new Vector2(cx + 150f * s, y - 22f * s),
                        new Vector2(180f * s, 30f * s), "сначала прошлые города",
                        Mathf.RoundToInt(12f * s), new Color(0.7f, 0.72f, 0.78f),
                        TextAnchor.MiddleRight));
                else
                {
                    string pid = poi.Id;
                    Keep(UiKit.MakeButton(_root, new Vector2(cx + 218f * s, y - 24f * s),
                        new Vector2(150f * s, 40f * s), "Отметить",
                        Mathf.RoundToInt(14f * s), delegate { CompletePoi(pid, cityId); }));
                }
            }
        }

        private void CompletePoi(string poiId, string cityId)
        {
            Journey.Complete(poiId);
            Snd.Play("coin");
            Refresh();
            OpenCity(cityId);   // пересобрать список с новым состоянием
        }

        private void OpenArtifacts()
        {
            CloseOverlay();
            Vector2 ctr = Card(700f, 680f, "Сумка Кёни");
            float s = UiKit.Scale;
            float top = ctr.y + 210f * s;

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top), new Vector2(600f * s, 26f * s),
                "Печати земель — " + Journey.Count(Artifact.LandSeal) + " из 4",
                Mathf.RoundToInt(17f * s), Color.white, TextAnchor.MiddleCenter));
            for (int i = 0; i < KoenigContent.Lands.Length; i++)
                Chip(new Vector2(ctr.x + (i - 1.5f) * 150f * s, top - 44f * s),
                    KoenigContent.Lands[i].Name, new Color(0.55f, 0.7f, 0.45f),
                    Journey.HasLandSeal(i.ToString()));

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 96f * s), new Vector2(600f * s, 26f * s),
                "Жетоны мостов — " + Journey.Count(Artifact.BridgeToken) + " из 7",
                Mathf.RoundToInt(17f * s), Color.white, TextAnchor.MiddleCenter));
            BridgeGraph g = KoenigContent.BuildGraph();
            for (int i = 0; i < g.Bridges.Count; i++)
                Chip(new Vector2(ctr.x + ((i % 4) - 1.5f) * 150f * s,
                        top - 140f * s - (i / 4) * 54f * s),
                    g.Bridges[i].Name, new Color(0.85f, 0.62f, 0.3f),
                    Journey.HasBridgeToken(g.Bridges[i].Id));

            Chip(new Vector2(ctr.x - 130f * s, top - 312f * s),
                "Ключ Эйлера", new Color(0.5f, 0.75f, 0.95f), Journey.Count(Artifact.EulerKey) > 0);
            Chip(new Vector2(ctr.x + 130f * s, top - 312f * s),
                "Золотой мост", new Color(0.9f, 0.78f, 0.3f), Journey.PuzzleSolved);

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, top - 372f * s), new Vector2(620f * s, 40f * s),
                Journey.PuzzleUnlocked ? "Все жетоны собраны — финал открыт!"
                    : "Собери все семь жетонов мостов, чтобы открыть финал.",
                Mathf.RoundToInt(16f * s),
                Journey.PuzzleUnlocked ? new Color(0.6f, 0.95f, 0.6f) : new Color(0.8f, 0.85f, 0.95f),
                TextAnchor.MiddleCenter));
        }

        private void Chip(Vector2 pos, string label, Color color, bool filled)
        {
            float s = UiKit.Scale;
            Color c = filled ? color : new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.6f);
            Keep(UiKit.MakePanel(_root, pos, new Vector2(132f * s, 40f * s), c));
            Keep(UiKit.MakeText(_root, pos, new Vector2(128f * s, 36f * s),
                (filled ? "✓ " : "") + label, Mathf.RoundToInt(13f * s),
                filled ? Color.white : new Color(0.7f, 0.72f, 0.76f), TextAnchor.MiddleCenter));
        }

        private void OpenRule()
        {
            CloseOverlay();
            Vector2 ctr = Card(700f, 640f, "Правило семи мостов");
            float s = UiKit.Scale;

            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, ctr.y + 150f * s),
                new Vector2(600f * s, 170f * s), KoenigContent.EulerRuleForKids,
                Mathf.RoundToInt(16f * s), Color.white, TextAnchor.UpperCenter));

            BridgeGraph g = KoenigContent.BuildGraph();
            Vector2[] slot = {
                new Vector2(ctr.x - 90f * s, ctr.y - 20f * s),
                new Vector2(ctr.x + 90f * s, ctr.y - 20f * s),
                new Vector2(ctr.x - 90f * s, ctr.y - 150f * s),
                new Vector2(ctr.x + 90f * s, ctr.y - 150f * s),
            };
            for (int i = 0; i < g.Lands.Length; i++)
            {
                int d = g.Degree(i);
                bool odd = (d & 1) == 1;
                Keep(UiKit.MakeImage(_root, slot[i], new Vector2(56f * s, 56f * s), Gfx.CircleSprite(),
                    odd ? new Color(0.9f, 0.45f, 0.4f) : new Color(0.5f, 0.7f, 0.45f)));
                Keep(UiKit.MakeText(_root, slot[i], new Vector2(56f * s, 56f * s),
                    d.ToString(), Mathf.RoundToInt(20f * s), Color.white, TextAnchor.MiddleCenter));
                Keep(UiKit.MakeText(_root, slot[i] + new Vector2(0f, -38f * s),
                    new Vector2(150f * s, 22f * s), g.Lands[i] + (odd ? " · нечёт" : " · чёт"),
                    Mathf.RoundToInt(13f * s),
                    odd ? new Color(1f, 0.7f, 0.6f) : new Color(0.7f, 0.9f, 0.7f),
                    TextAnchor.MiddleCenter));
            }
            Keep(UiKit.MakeText(_root, new Vector2(ctr.x, ctr.y - 220f * s), new Vector2(620f * s, 30f * s),
                "Четыре нечётных угла — на два больше, чем можно.",
                Mathf.RoundToInt(15f * s), new Color(1f, 0.8f, 0.6f), TextAnchor.MiddleCenter));
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
            float s = UiKit.Scale;
            float cx = Screen.width * 0.5f;

            _title.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - 40f * s);
            _progress.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - 72f * s);
            _artBtn.image.rectTransform.anchoredPosition = new Vector2(cx - 220f * s, 46f * s);
            _ruleBtn.image.rectTransform.anchoredPosition = new Vector2(cx, 46f * s);
            _finaleBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 230f * s, 46f * s);

            _gpsText.rectTransform.anchoredPosition = new Vector2(cx, 108f * s);
            _nearPanel.rectTransform.anchoredPosition = new Vector2(cx, 150f * s);
            _nearText.rectTransform.anchoredPosition = new Vector2(cx - 120f * s, 150f * s);
            _nearBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 250f * s, 150f * s);

            City[] cs = KoenigContent.Cities;
            for (int i = 0; i < cs.Length; i++)
            {
                Vector2 p = CityScreen(cs[i]);
                _node[i].rectTransform.anchoredPosition = p;
                _tap[i].image.rectTransform.anchoredPosition = p;
                _nodeLabel[i].rectTransform.anchoredPosition = p + new Vector2(0f, -42f * s);
                _nodeBadge[i].rectTransform.anchoredPosition = p + new Vector2(0f, 40f * s);
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
                rt.sizeDelta = new Vector2(len, 6f * s);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH)
            {
                CloseOverlay();  // модалка пересоберётся при следующем открытии
                Layout();
            }

            // GPS опрашиваем раз в секунду: чаще незачем, а строку статуса
            // и баннер лишний раз не дёргаем.
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
                _nearText.text = "📍 Ты у точки «" + poi.Name + "» (" +
                                 Mathf.RoundToInt(meters) + " м)";
                ShowNear(true);
            }
            else
            {
                _nearPoi = null;
                ShowNear(false);
                if (gate.HasFix && poi != null)
                    _gpsText.text = "Ближайшая точка: «" + poi.Name + "» — " +
                                    Mathf.RoundToInt(meters) + " м";
            }
        }

        private void Refresh()
        {
            _progress.text = "Пройдено точек: " + Journey.DonePoints + " из " + Journey.TotalPoints;

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
