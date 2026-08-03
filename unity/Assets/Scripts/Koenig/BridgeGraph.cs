using System.Collections.Generic;

namespace Koenig
{
    // Один мост: соединяет две земли. Exists — сохранился ли он сегодня
    // (из семи кёнигсбергских уцелел только Медовый), это нужно, чтобы в
    // игре отличать «исторический» мост от того, что реально стоит.
    public class Bridge
    {
        public string Id;
        public string Name;
        public int A;
        public int B;
        public bool Exists;

        public Bridge(string id, string name, int a, int b, bool exists)
        {
            Id = id; Name = name; A = a; B = b; Exists = exists;
        }

        public bool Touches(int land) { return A == land || B == land; }
        public int Other(int land) { return A == land ? B : A; }
    }

    // Граф мостов и решение задачи Эйлера.
    //
    // Здесь живёт вся математика, на которой держится игра, поэтому она
    // отделена от Unity и проверяется независимо: класс не знает ни про
    // сцену, ни про UI, только про земли и мосты. Правило Эйлера:
    // обойти все мосты по разу можно тогда и только тогда, когда граф
    // связен и нечётных вершин ровно 0 или 2. У Кёнигсберга все четыре
    // земли нечётные — поэтому нельзя; добавление одного моста между
    // двумя нечётными землями делает их чётными и оставляет ровно две
    // нечётные, и тогда обход появляется.
    public class BridgeGraph
    {
        public readonly string[] Lands;
        public readonly List<Bridge> Bridges = new List<Bridge>();

        public BridgeGraph(string[] lands) { Lands = lands; }

        public void Add(Bridge b) { Bridges.Add(b); }

        // Сколько мостов сходится к земле.
        public int Degree(int land)
        {
            int d = 0;
            for (int i = 0; i < Bridges.Count; i++)
                if (Bridges[i].Touches(land)) d++;
            return d;
        }

        public List<int> OddLands()
        {
            List<int> odd = new List<int>();
            for (int i = 0; i < Lands.Length; i++)
                if ((Degree(i) & 1) == 1) odd.Add(i);
            return odd;
        }

        public int OddCount() { return OddLands().Count; }

        // Связность по землям, у которых есть хоть один мост. Земля без
        // мостов в обходе не участвует и связности не ломает.
        public bool Connected()
        {
            int start = -1;
            for (int i = 0; i < Lands.Length; i++)
                if (Degree(i) > 0) { start = i; break; }
            if (start < 0) return true;

            HashSet<int> seen = new HashSet<int>();
            Stack<int> stack = new Stack<int>();
            stack.Push(start);
            seen.Add(start);
            while (stack.Count > 0)
            {
                int v = stack.Pop();
                for (int i = 0; i < Bridges.Count; i++)
                {
                    if (!Bridges[i].Touches(v)) continue;
                    int w = Bridges[i].Other(v);
                    if (seen.Add(w)) stack.Push(w);
                }
            }
            for (int i = 0; i < Lands.Length; i++)
                if (Degree(i) > 0 && !seen.Contains(i)) return false;
            return true;
        }

        // Есть ли обход, проходящий каждый мост ровно один раз (путь
        // Эйлера — начало и конец могут быть в разных землях).
        public bool HasEulerPath()
        {
            if (!Connected()) return false;
            int odd = OddCount();
            return odd == 0 || odd == 2;
        }

        // Сам обход: последовательность индексов мостов в порядке
        // прохождения. null — обхода нет. Алгоритм Хирхольцера: идём,
        // пока есть непройденный мост, упираемся — сдаём землю в маршрут.
        // Кратные мосты (у Кёнигсберга их две пары) учитываются по
        // индексу, а не по паре земель, поэтому не путаются.
        public List<int> FindWalk()
        {
            if (!HasEulerPath()) return null;
            if (Bridges.Count == 0) return new List<int>();

            // Списки смежности: для каждой земли — (индекс моста, соседняя земля).
            List<int>[] incident = new List<int>[Lands.Length];
            for (int i = 0; i < Lands.Length; i++) incident[i] = new List<int>();
            for (int e = 0; e < Bridges.Count; e++)
            {
                incident[Bridges[e].A].Add(e);
                if (Bridges[e].B != Bridges[e].A) incident[Bridges[e].B].Add(e);
            }

            // Старт: при двух нечётных — в одной из них, иначе в любой земле с мостом.
            List<int> odd = OddLands();
            int start = odd.Count == 2 ? odd[0] : -1;
            if (start < 0)
                for (int i = 0; i < Lands.Length; i++)
                    if (Degree(i) > 0) { start = i; break; }

            bool[] used = new bool[Bridges.Count];
            int[] ptr = new int[Lands.Length];
            Stack<int> sv = new Stack<int>();
            Stack<int> se = new Stack<int>();
            List<int> circuit = new List<int>();
            sv.Push(start);
            se.Push(-1);

            while (sv.Count > 0)
            {
                int v = sv.Peek();
                // Промотать до непройденного моста.
                while (ptr[v] < incident[v].Count && used[incident[v][ptr[v]]]) ptr[v]++;
                if (ptr[v] < incident[v].Count)
                {
                    int e = incident[v][ptr[v]++];
                    used[e] = true;
                    sv.Push(Bridges[e].Other(v));
                    se.Push(e);
                }
                else
                {
                    sv.Pop();
                    int e = se.Pop();
                    if (e >= 0) circuit.Add(e);
                }
            }

            // Собрали в обратном порядке; и обход должен покрыть ВСЕ мосты —
            // иначе граф на деле распадался (страховка к Connected).
            if (circuit.Count != Bridges.Count) return null;
            circuit.Reverse();
            return circuit;
        }

        // Какой мост достроить, чтобы обход стал возможен. Возвращает пару
        // нечётных земель: мост между ними делает обе чётными, нечётных
        // остаётся ровно две — и путь Эйлера появляется. null, если и так
        // решаемо. Одного моста хватает именно потому, что у Кёнигсберга
        // нечётных четыре: 4 − 2 = 2.
        public int[] SuggestBridge()
        {
            if (HasEulerPath()) return null;
            List<int> odd = OddLands();
            if (odd.Count < 2) return null;
            return new int[] { odd[0], odd[1] };
        }
    }
}
