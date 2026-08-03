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

        // Экранные позиции земель относительно центра, в опорных единицах:
        // остров в середине, север сверху, юг снизу, восток справа — как
        // на настоящей карте Кёнигсберга.
        private static Vector2 NodeBase(int land)
        {
            if (land == 0) return new Vector2(0f, 190f);    // Север
            if (land == 2) return new Vector2(0f, -190f);   // Юг
            if (land == 3) return new Vector2(300f, 0f);    // Восток
            return new Vector2(-30f, 0f);                    // Остров (центр)
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
            float s = UiKit.Scale;

            UiKit.MakePanel(c, Vector2.zero, new Vector2(4000f, 4000f),
                new Color(0.06f, 0.12f, 0.18f, 1f));
            _title = UiKit.MakeText(c, Vector2.zero, new Vector2(760f * s, 44f * s),
                "Семь мостов Кёнигсберга", Mathf.RoundToInt(30f * s),
                new Color(1f, 0.86f, 0.42f), TextAnchor.MiddleCenter);
            _status = UiKit.MakeText(c, Vector2.zero, new Vector2(820f * s, 96f * s), "",
                Mathf.RoundToInt(19f * s), Color.white, TextAnchor.UpperCenter);

            GameObject bh = new GameObject("Bridges");
            bh.transform.SetParent(c, false);
            _bridgeHolder = bh.transform;

            // Земли поверх мостов.
            _node = new Image[KoenigContent.Lands.Length];
            _nodeLabel = new Text[KoenigContent.Lands.Length];
            _nodeBadge = new Text[KoenigContent.Lands.Length];
            for (int i = 0; i < _node.Length; i++)
            {
                _node[i] = UiKit.MakeImage(c, Vector2.zero, new Vector2(64f * s, 64f * s),
                    Gfx.CircleSprite(), new Color(0.5f, 0.62f, 0.42f));
                _nodeLabel[i] = UiKit.MakeText(c, Vector2.zero, new Vector2(150f * s, 26f * s),
                    KoenigContent.Lands[i].Name, Mathf.RoundToInt(17f * s),
                    Color.white, TextAnchor.MiddleCenter);
                _nodeBadge[i] = UiKit.MakeText(c, Vector2.zero, new Vector2(60f * s, 26f * s),
                    "", Mathf.RoundToInt(18f * s), new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter);
            }

            _restartBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(190f * s, 48f * s),
                "Сначала", Mathf.RoundToInt(19f * s), Restart);
            _hintBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(190f * s, 48f * s),
                "Подсказка", Mathf.RoundToInt(19f * s), OnHint);
            _goldenBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(300f * s, 48f * s),
                "Построить золотой мост", Mathf.RoundToInt(18f * s), OnBuildGolden);
            _closeBtn = UiKit.MakeButton(c, Vector2.zero, new Vector2(150f * s, 44f * s),
                "Закрыть", Mathf.RoundToInt(18f * s), Close);

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

            float s = UiKit.Scale;
            for (int e = 0; e < _g.Bridges.Count; e++)
            {
                int edge = e;
                Button b = UiKit.MakeButton(_bridgeHolder, Vector2.zero,
                    new Vector2(10f * s, 26f * s), "", 1, delegate { OnBridgeTap(edge); });
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
            float s = UiKit.Scale;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f - 10f * s;

            _title.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - 40f * s);
            _status.rectTransform.anchoredPosition = new Vector2(cx, Screen.height - 74f * s);

            Vector2[] pos = new Vector2[_node.Length];
            for (int i = 0; i < _node.Length; i++)
            {
                Vector2 nb = NodeBase(i);
                pos[i] = new Vector2(cx + nb.x * s, cy + nb.y * s);
                _node[i].rectTransform.anchoredPosition = pos[i];
                _nodeLabel[i].rectTransform.anchoredPosition = pos[i] + new Vector2(0f, -46f * s);
                _nodeBadge[i].rectTransform.anchoredPosition = pos[i] + new Vector2(0f, 44f * s);
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
                float spread = (total > 1 ? (idx - (total - 1) * 0.5f) : 0f) * 34f * s;
                Vector2 shift = new Vector2(-ny, nx) * spread;
                Vector2 mid = new Vector2((p0.x + p1.x) * 0.5f, (p0.y + p1.y) * 0.5f) + shift;

                RectTransform rt = _bridgeBtn[e].image.rectTransform;
                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(len, 26f * s);
                rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }

            float row = cy - 250f * s;
            _restartBtn.image.rectTransform.anchoredPosition = new Vector2(cx - 150f * s, row);
            _hintBtn.image.rectTransform.anchoredPosition = new Vector2(cx + 60f * s, row);
            _goldenBtn.image.rectTransform.anchoredPosition = new Vector2(cx, row - 56f * s);
            _closeBtn.image.rectTransform.anchoredPosition =
                new Vector2(Screen.width - 90f * s, Screen.height - 40f * s);
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
            float s = UiKit.Scale;

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
                float size = (here ? 76f : 64f) * s;
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
