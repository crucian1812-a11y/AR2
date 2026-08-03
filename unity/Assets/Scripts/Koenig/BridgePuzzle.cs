using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Интерактивный финал: карта четырёх земель и семи мостов, по которой
    // ребёнок пытается пройти, задевая каждый мост по разу. У Кёнигсберга
    // это невозможно — ребёнок в этом убеждается сам, застревая. Тогда
    // проводник объясняет правило чётности, подсвечивает нечётные углы, и
    // ребёнок СТРОИТ золотой мост — после чего обход появляется, и он
    // с триумфом проходит все восемь.
    //
    // Вся математика — в BridgeGraph; здесь только показ и касания.
    //
    // ВАЖНО про размеры: игра портретная, масштаб берётся от ШИРИНЫ экрана
    // (ks = Screen.width / 440), как на карте области. Раньше считалось от
    // высоты, и на узком экране остров с востоком уезжали за правый край.
    public class BridgePuzzle : MonoBehaviour
    {
        private System.Action _onSolved;

        private BridgeGraph _g;
        private bool[] _used;
        private int _current = -1;
        private readonly List<int> _walk = new List<int>();
        private bool _goldenBuilt;
        private int _phase; // 0 пробуем, 1 застряли, 2 победа

        private Canvas _canvas;
        private Text _title;
        private Text _status;
        private Image[] _node;
        private Text[] _nodeLabel;
        private Text[] _nodeBadge;
        private readonly List<Button> _bridgeBtn = new List<Button>();
        private Button _restartBtn;
        private Button _hintBtn;
        private Button _goldenBtn;
        private Button _closeBtn;
        private Transform _bridgeHolder;
        private int _lastW, _lastH;
        private bool _hinting;

        // Масштаб от ширины: логическое поле 440 единиц (как в RegionMap).
        private float KS { get { return Mathf.Max(Screen.width, 1) / 440f; } }
        private int Fs(float logical) { return Mathf.Max(1, Mathf.RoundToInt(logical * KS)); }
        private Vector2 Sz(float w, float h) { return new Vector2(w * KS, h * KS); }

        // Экранные позиции земель относительно центра, в логических единицах
        // (поле 440 в ширину): остров в середине, север сверху, юг снизу,
        // восток справа — как на настоящей карте Кёнигсберга. Смещения по X
        // держим в пределах ±150, чтобы влезть в узкий портретный экран.
        private static Vector2 NodeBase(int land)
        {
            if (land == 0) return new Vector2(0f, 150f);    // Север
            if (land == 2) return new Vector2(0f, -150f);   // Юг
            if (land == 3) return new Vector2(150f, 0f);    // Восток
            return new Vector2(-38f, 0f);                    // Остров (центр)
        }

        public static BridgePuzzle Create(Transform parent, System.Action onSolved)
        {
            GameObject go = new GameObject("BridgePuzzle");
            go.transform.SetParent(parent, false);
            BridgePuzzle p = go.AddComponent<BridgePuzzle>();
            p._onSolved = onSolved;
            p.Build();
            return p;
        }

        private void Build()
        {
            _g = KoenigContent.BuildGraph();
            _used = new bool[_g.Bridges.Count];

            _canvas = UiKit.CreateCanvas("KoenigPuzzle", 40);
            _canvas.transform.SetParent(transform, false);
            Transform c = _canvas.transform;

            UiKit.MakePanel(c, Vector2.zero, new Vector2(6000f, 6000f),
                new Color(0.06f, 0.12f, 0.18f, 1f));
            _title = UiKit.MakeText(c, Vector2.zero, Sz(430, 40),
                "Семь мостов Кёнигсберга", Fs(24),
                new Color(1f, 0.86f, 0.42f), TextAnchor.MiddleCenter);
            _status = UiKit.MakeText(c, Vector2.zero, Sz(420, 96), "",
                Fs(15), Color.white, TextAnchor.UpperCenter);

            GameObject bh = new GameObject("Bridges");
            bh.transform.SetParent(c, false);
            _bridgeHolder = bh.transform;

            // Земли поверх мостов.
            _node = new Image[KoenigContent.Lands.Length];
            _nodeLabel = new Text[KoenigContent.Lands.Length];
            _nodeBadge = new Text[KoenigContent.Lands.Length];
            for (int i = 0; i < _node.Length; i++)
            {
                _node[i] = UiKit.MakeImage(c, Vector2.zero, Sz(64, 64),
                    Gfx.CircleSprite(), new Color(0.5f, 0.62f, 0.42f));
                _nodeLabel[i] = UiKit.MakeText(c, Vector2.zero, Sz(150, 24),
                    KoenigContent.Lands[i].Name, Fs(14),
                    Color.white, TextAnchor.MiddleCenter);
                _nodeBadge[i] = UiKit.MakeText(c, Vector2.zero, Sz(60, 24),
                    "", Fs(15), new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
            }

            _restartBtn = UiKit.MakeButton(c, Vector2.zero, Sz(150, 46),
                "Сначала", Fs(15), Restart);
            _hintBtn = UiKit.MakeButton(c, Vector2.zero, Sz(150, 46),
                "Подсказка", Fs(15), OnHint);
            _goldenBtn = UiKit.MakeButton(c, Vector2.zero, Sz(260, 46),
                "Построить золотой мост", Fs(15), OnBuildGolden);
            _closeBtn = UiKit.MakeButton(c, Vector2.zero, Sz(120, 42),
                "Закрыть", Fs(15), Close);

            BuildBridgeButtons();
            Layout();
            Restart();
        }

        // Кнопки-мосты создаются заново после постройки золотого моста.
        private void BuildBridgeButtons()
        {
            for (int i = 0; i < _bridgeBtn.Count; i++)
                if (_bridgeBtn[i] != null) Object.Destroy(_bridgeBtn[i].gameObject);
            _bridgeBtn.Clear();

            for (int e = 0; e < _g.Bridges.Count; e++)
            {
                int edge = e;
                Button b = UiKit.MakeButton(_bridgeHolder, Vector2.zero,
                    Sz(10, 24), "", 1, delegate { OnBridgeTap(edge); });
                _bridgeBtn.Add(b);
            }
        }

        // Сколько кёнигсбергских мостов сходится к паре земель — чтобы
        // раздвинуть кратные (у острова с севером и с югом их по два).
        private int PairOffset(int edge, out int total)
        {
            Bridge be = _g.Bridges[edge];
            int idx = 0; total = 0;
            for (int e = 0; e < _g.Bridges.Count; e++)
            {
                Bridge o = _g.Bridges[e];
                bool same = (o.A == be.A && o.B == be.B) || (o.A == be.B && o.B == be.A);
                if (!same) continue;
                if (e == edge) idx = total;
                total++;
            }
            return idx;
        }

        private void Layout()
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            float cx = Screen.width * 0.5f;
            // Граф чуть выше центра — снизу оставляем полосу под кнопки.
            float cy = Screen.height * 0.56f;

            _title.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - Fs(30));
            _status.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - Fs(60));

            Vector2[] pos = new Vector2[_node.Length];
            for (int i = 0; i < _node.Length; i++)
            {
                Vector2 nb = NodeBase(i);
                pos[i] = new Vector2(cx + nb.x * KS, cy + nb.y * KS);
                _node[i].rectTransform.anchoredPosition = pos[i];
                _nodeLabel[i].rectTransform.anchoredPosition = pos[i] + new Vector2(0f, -Fs(44));
                _nodeBadge[i].rectTransform.anchoredPosition = pos[i] + new Vector2(0f, Fs(42));
            }

            for (int e = 0; e < _bridgeBtn.Count; e++)
            {
                Bridge be = _g.Bridges[e];
                Vector2 p0 = pos[be.A];
                Vector2 p1 = pos[be.B];
                float dx = p1.x - p0.x, dy = p1.y - p0.y;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) len = 1f;
                float nx = dx / len, ny = dy / len;
                // Перпендикуляр — чтобы раздвинуть кратные мосты.
                int total; int idx = PairOffset(e, out total);
                float spread = (total > 1 ? (idx - (total - 1) * 0.5f) : 0f) * 30f * KS;
                Vector2 shift = new Vector2(-ny, nx) * spread;
                Vector2 mid = new Vector2((p0.x + p1.x) * 0.5f, (p0.y + p1.y) * 0.5f) + shift;

                RectTransform rt = _bridgeBtn[e].image.rectTransform;
                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(len, 24f * KS);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }

            // Кнопки — фиксированной полосой у низа экрана, не привязаны к cy,
            // чтобы не уезжать под нижнюю землю на коротких экранах.
            _restartBtn.image.rectTransform.anchoredPosition =
                new Vector2(Screen.width * 0.28f, Screen.height * 0.14f);
            _hintBtn.image.rectTransform.anchoredPosition =
                new Vector2(Screen.width * 0.72f, Screen.height * 0.14f);
            _goldenBtn.image.rectTransform.anchoredPosition =
                new Vector2(cx, Screen.height * 0.065f);
            _closeBtn.image.rectTransform.anchoredPosition =
                new Vector2(Screen.width - Fs(70), Screen.height - Fs(30));
        }

        private void Update()
        {
            if (Screen.width != _lastW || Screen.height != _lastH) Layout();
        }

        // ---------- Ход ----------

        private void OnBridgeTap(int edge)
        {
            if (_hinting || _phase == 2 || _used[edge]) return;

            // Первый шаг: встаём на один берег моста.
            if (_current < 0) _current = _g.Bridges[edge].A;
            if (!_g.Bridges[edge].Touches(_current)) { Blink(_status); return; }

            _used[edge] = true;
            _walk.Add(edge);
            _current = _g.Bridges[edge].Other(_current);

            int usedCount = _walk.Count;
            if (usedCount == _g.Bridges.Count) Win();
            else if (!AnyWalkable()) Stuck();
            Refresh();
        }

        private bool AnyWalkable()
        {
            for (int e = 0; e < _g.Bridges.Count; e++)
                if (!_used[e] && (_current < 0 || _g.Bridges[e].Touches(_current))) return true;
            return false;
        }

        private void Restart()
        {
            _used = new bool[_g.Bridges.Count];
            _current = -1;
            _walk.Clear();
            if (_phase != 2) _phase = 0;
            Refresh();
        }

        private void Stuck()
        {
            _phase = 1;
        }

        private void Win()
        {
            _phase = 2;
            Journey.MarkSolved();
            if (_onSolved != null) { _onSolved(); _onSolved = null; }
        }

        // ---------- Золотой мост ----------

        private void OnBuildGolden()
        {
            if (_goldenBuilt) return;
            int[] pair = _g.SuggestBridge();
            if (pair == null) return;
            _g.Add(new Bridge("golden", "Золотой мост", pair[0], pair[1], true));
            _goldenBuilt = true;
            BuildBridgeButtons();
            Layout();
            Restart();
        }

        // ---------- Подсказка ----------

        private void OnHint()
        {
            if (_hinting || _phase == 2) return;
            List<int> walk = _g.FindWalk();
            if (walk == null) { _phase = 1; Refresh(); return; }
            StartCoroutine(PlayWalk(walk));
        }

        private IEnumerator PlayWalk(List<int> walk)
        {
            _hinting = true;
            Restart();

            // Старт — свободный конец первого моста (тот, что не общий со
            // вторым). Дальше каждый мост проходим ОТ берега, где стоим:
            // маршрут Хирхольцера задаёт направление, и брать всегда конец
            // A нельзя — со второго моста путь бы рассыпался.
            if (walk.Count == 1)
            {
                _current = _g.Bridges[walk[0]].A;
            }
            else
            {
                Bridge e0 = _g.Bridges[walk[0]];
                Bridge e1 = _g.Bridges[walk[1]];
                int shared = (e0.A == e1.A || e0.A == e1.B) ? e0.A : e0.B;
                _current = e0.Other(shared);
            }

            for (int i = 0; i < walk.Count; i++)
            {
                int e = walk[i];
                _used[e] = true;
                _walk.Add(e);
                _current = _g.Bridges[e].Other(_current);
                Refresh();
                yield return new WaitForSeconds(0.55f);
            }
            _hinting = false;
            if (_walk.Count == _g.Bridges.Count) Win();
            Refresh();
        }

        // ---------- Показ ----------

        private void Refresh()
        {
            for (int e = 0; e < _bridgeBtn.Count; e++)
            {
                bool used = _used[e];
                bool walkable = !used && (_current < 0 || _g.Bridges[e].Touches(_current));
                Image img = _bridgeBtn[e].image;
                if (used) img.color = new Color(0.3f, 0.55f, 0.85f, 0.9f);
                else if (walkable) img.color = new Color(0.95f, 0.78f, 0.3f, 1f);
                else img.color = new Color(0.4f, 0.42f, 0.46f, 0.9f);
                _bridgeBtn[e].interactable = !used && !_hinting;
            }

            List<int> odd = _g.OddLands();
            for (int i = 0; i < _node.Length; i++)
            {
                bool here = i == _current;
                _node[i].color = here
                    ? new Color(1f, 0.85f, 0.4f)
                    : new Color(0.5f, 0.62f, 0.42f);
                float size = (here ? 76f : 64f) * KS;
                _node[i].rectTransform.sizeDelta = new Vector2(size, size);

                // На застревании показываем нечётные углы восклицанием,
                // иначе — число мостов у угла.
                if (_phase == 1 && odd.Contains(i))
                    _nodeBadge[i].text = _g.Degree(i) + " !";
                else
                    _nodeBadge[i].text = _g.Degree(i).ToString();
                _nodeBadge[i].color = (_phase == 1 && odd.Contains(i))
                    ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.8f, 0.4f);
            }

            _goldenBtn.image.gameObject.SetActive(_phase == 1 && !_goldenBuilt);

            if (_phase == 2)
                _status.text = "Получилось! Ты обошёл все мосты по разу. " +
                               "Золотой мост спас прогулку!";
            else if (_phase == 1)
                _status.text = "Застряли — и это правильно! " + KoenigContent.EulerRuleForKids;
            else if (_walk.Count == 0)
                _status.text = "Пройди по каждому мосту ровно один раз. " +
                               "Нажимай на мост, чтобы перейти.";
            else
                _status.text = "Пройдено мостов: " + _walk.Count + " из " + _g.Bridges.Count;
        }

        private void Blink(Text t)
        {
            if (t != null) t.color = new Color(1f, 0.6f, 0.5f);
        }

        public void Close()
        {
            Snd.Play("click");
            Object.Destroy(gameObject);
        }
    }
}
