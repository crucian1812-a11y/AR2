using System.Collections.Generic;
using UnityEngine;

// Совместные забавы: прятки, оборона деревни и гонка.
//
// Правила считает только хозяин и раскладывает результат в NetManager;
// остальные получают готовое состояние и лишь показывают его. Так же
// устроены враги, и по той же причине: спорить о том, кого осалили,
// двум телефонам нечем.
//
// Все три забавы живут в деревне, у костра на площади. Это осознанное
// ограничение: площадь — единственное место, где компания собирается
// вместе, а маршруты и точки обороны надо расставлять руками под каждый
// мир. Перенести забаву в другой край — это добавить ему RaceRoute и
// RaidSpawns, не трогая ничего здесь.
public class Party : MonoBehaviour
{
    public static Party I;

    // --- Прятки ---
    private const float HideWarmup = 25f;   // сколько прячутся
    private const float HideRound = 150f;   // сколько ищут
    private const float CatchDist = 1.9f;

    // --- Оборона ---
    private const int NightWaves = 5;
    private const float BuildTime = 30f;
    private const int FountainLives = 20;
    // Потолок одновременных мобов. Хозяин игры считает их на своём
    // телефоне, и если волну не ограничить, хуже станет именно ему —
    // тому, кто всех позвал.
    private const int MaxAlive = 18;
    private const float ReachDist = 3f;
    // Сколько моб грызёт один блок.
    private const float ChewTime = 3.5f;
    // 0 — полночь, 0.25 — рассвет. Держим глухую ночь.
    private const float NightHold = 0.97f;

    // --- Гонка ---
    private const float RaceCountdown = 4f;
    private const float RaceLimit = 240f;
    private const float RingDist = 3.4f;

    private static readonly string[] RaidMobs =
        { "creeper", "zombie", "skeleton", "spider", "cubeslime", "enderman" };

    private float _spawnAccum;
    private int _spawnedThisWave;
    private int _waveSize;
    private readonly List<Enemy> _raid = new List<Enemy>();
    // Сколько уже грызут, по номеру врага.
    private readonly Dictionary<int, float> _chew = new Dictionary<int, float>();
    private int _finishers;

    // Кольца гонки — рисуются у всех, но ведёт счёт хозяин.
    private readonly List<GameObject> _rings = new List<GameObject>();
    private Vector3[] _route;

    public static Party Create(Transform parent)
    {
        GameObject go = new GameObject("Party");
        go.transform.SetParent(parent, false);
        Party p = go.AddComponent<Party>();
        I = p;
        return p;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    public static bool Running
    {
        get
        {
            NetManager net = NetManager.I;
            return net != null && net.ModeKind != NetManager.ModeNone &&
                   net.ModePhase != NetManager.PhaseOff;
        }
    }

    // Можно ли сейчас запустить забаву в этом мире.
    public static bool Available(int kind)
    {
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        if (w == null) return false;
        if (kind == NetManager.ModeRace)
        {
            Vector3[] r = w.RaceRoute();
            return r != null && r.Length >= 2;
        }
        if (kind == NetManager.ModeNight)
        {
            Vector3[] s = w.RaidSpawns();
            return s != null && s.Length > 0;
        }
        return kind == NetManager.ModeHide;
    }

    // ---------- Запуск и остановка ----------

    public void Begin(int kind)
    {
        NetManager net = NetManager.I;
        if (net == null || !net.IsHost) return;

        _raid.Clear();
        _spawnAccum = 0f;
        _spawnedThisWave = 0;
        _finishers = 0;

        if (kind == NetManager.ModeHide) BeginHide(net);
        else if (kind == NetManager.ModeNight) BeginNight(net);
        else if (kind == NetManager.ModeRace) BeginRace(net);
    }

    public void Stop()
    {
        NetManager net = NetManager.I;
        if (net == null || !net.IsHost) return;
        ClearRaid();
        net.RequestMode(NetManager.ModeNone);
    }

    private void ClearRaid()
    {
        NetManager net = NetManager.I;
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        for (int i = 0; i < _raid.Count; i++)
        {
            if (_raid[i] == null) continue;
            int id = _raid[i].Id;
            if (w != null) w.RemoveEnemy(id, false);
            // И у гостей тоже: их мир этих врагов уже создал по EvSpawn,
            // и без такой же отмены волна осталась бы бродить у них
            // навсегда — хост её больше не двигает.
            if (net != null) net.HostDespawnEnemy(net.CurrentWorld, id);
        }
        _raid.Clear();
        _chew.Clear();
    }

    // ---------- Общий ход ----------

    private void Update()
    {
        NetManager net = NetManager.I;
        if (net == null) return;

        // Это показывают все, включая гостей.
        SyncRings(net);
        SyncSeekerMarks(net);
        LocalReact(net);

        if (!net.IsHost)
        {
            // У гостя таймер между пакетами идёт сам, иначе цифра прыгает
            // раз в четверть секунды. Приходящий пакет её поправляет.
            if (Running) net.ModeTimer -= Time.unscaledDeltaTime;
            return;
        }
        if (!Running) return;
        // Время в забавах идёт по неотмасштабированному: панель лавки
        // или пауза не должны замораживать чужой раунд.
        float dt = Time.unscaledDeltaTime;

        // Итог висит несколько секунд и сам гаснет. Ведём его здесь же:
        // раньше это делал LateUpdate, и таймер убывал дважды за кадр.
        if (net.ModePhase == NetManager.PhaseOver)
        {
            net.ModeTimer -= dt;
            if (net.ModeTimer <= 0f)
            {
                ClearRaid();
                net.RequestMode(NetManager.ModeNone);
            }
            else net.HostPublishMode(NetManager.PhaseOver, net.ModeTimer,
                net.ModeWave, net.ModeLives, net.ModeText);
            return;
        }

        if (net.ModeKind == NetManager.ModeHide) StepHide(net, dt);
        else if (net.ModeKind == NetManager.ModeNight) StepNight(net, dt);
        else if (net.ModeKind == NetManager.ModeRace) StepRace(net, dt);
    }

    // Все живые игроки, кроме выбывших из показа.
    private static List<PlayerInfo> Alive(NetManager net)
    {
        List<PlayerInfo> list = new List<PlayerInfo>();
        foreach (KeyValuePair<int, PlayerInfo> kv in net.Players) list.Add(kv.Value);
        return list;
    }

    // Позиция игрока по номеру: у себя берём из сцены, у остальных из сети.
    private static Vector3 PosOf(NetManager net, PlayerInfo p)
    {
        if (p.Id == net.MyId)
        {
            BearPlayer local = GameRoot.LocalBear;
            if (local != null) return local.transform.position;
        }
        return p.Pos;
    }

    // ---------- Прятки ----------

    private void BeginHide(NetManager net)
    {
        List<PlayerInfo> all = Alive(net);
        for (int i = 0; i < all.Count; i++) { all[i].Role = 0; all[i].Score = 0; }

        // Водящий выбирается жребием. В одиночку водить не с кем —
        // забава просто не начнётся, о чём и говорит подпись.
        if (all.Count >= 2)
        {
            PlayerInfo seeker = all[Random.Range(0, all.Count)];
            seeker.Role = 1;
        }

        net.HostPublishMode(NetManager.PhaseWarmup, HideWarmup, 0, 0,
            all.Count >= 2 ? "Прячьтесь!" : "Нужен хотя бы второй игрок");
    }

    private void StepHide(NetManager net, float dt)
    {
        List<PlayerInfo> all = Alive(net);
        if (all.Count < 2) { Finish(net, "Прятки отменены — все разошлись"); return; }

        net.ModeTimer -= dt;

        if (net.ModePhase == NetManager.PhaseWarmup)
        {
            if (net.ModeTimer <= 0f)
                net.HostPublishMode(NetManager.PhaseRun, HideRound, 0, 0, "Водящий пошёл искать!");
            else
                net.HostPublishMode(NetManager.PhaseWarmup, net.ModeTimer, 0, 0, "Прячьтесь!");
            return;
        }

        if (net.ModePhase != NetManager.PhaseRun) return;

        // Осаливание: любой водящий, дотянувшийся до прячущегося, делает
        // его тоже водящим. Никто не выбывает и не ждёт конца раунда —
        // это главное, ради чего выбрана именно снежная схема.
        int hiders = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Role != 0) continue;
            hiders++;
            Vector3 hp = PosOf(net, all[i]);
            for (int k = 0; k < all.Count; k++)
            {
                if (all[k].Role == 0 || all[k].Id == all[i].Id) continue;
                if ((PosOf(net, all[k]) - hp).sqrMagnitude > CatchDist * CatchDist) continue;
                all[i].Role = 1;
                all[k].Score++;
                hiders--;
                break;
            }
        }

        if (hiders <= 0) { Finish(net, "Всех нашли! Водящие победили"); return; }
        if (net.ModeTimer <= 0f)
        {
            Finish(net, "Спрятались! Осталось не найдено: " + hiders);
            return;
        }
        net.HostPublishMode(NetManager.PhaseRun, net.ModeTimer, 0, hiders,
            "Не найдено: " + hiders);
    }

    // ---------- Оборона деревни ----------

    private void BeginNight(NetManager net)
    {
        // Ночь наступает сразу: ждать её по часам значило бы стоять без
        // дела до нескольких минут.
        net.DayTime = NightHold;
        _waveSize = 6;
        net.HostPublishMode(NetManager.PhaseWarmup, BuildTime, 1, FountainLives,
            "Волна 1 — стройте стены!");
    }

    private void StepNight(NetManager net, float dt)
    {
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        if (w == null) return;

        // Держим ночь всю оборону: иначе к третьей волне рассветёт.
        net.DayTime = NightHold;
        net.ModeTimer -= dt;

        if (net.ModePhase == NetManager.PhaseWarmup)
        {
            if (net.ModeTimer <= 0f)
            {
                _spawnedThisWave = 0;
                _spawnAccum = 0f;
                _waveSize = 4 + net.ModeWave * 3;
                net.HostPublishMode(NetManager.PhaseRun, 0f, net.ModeWave, net.ModeLives,
                    "Волна " + net.ModeWave);
            }
            else net.HostPublishMode(NetManager.PhaseWarmup, net.ModeTimer,
                net.ModeWave, net.ModeLives,
                "Волна " + net.ModeWave + " — стройте стены!");
            return;
        }

        if (net.ModePhase != NetManager.PhaseRun) return;

        Vector3 goal = w.DefendPoint();
        Vector3[] spawns = w.RaidSpawns();
        if (spawns == null || spawns.Length == 0) { Finish(net, "Оборонять нечего"); return; }

        // Подкрепление подходит порциями, а не всей волной разом.
        _spawnAccum += dt;
        int alive = CountRaid();
        if (_spawnedThisWave < _waveSize && alive < MaxAlive && _spawnAccum >= 1.6f)
        {
            _spawnAccum = 0f;
            Vector3 from = spawns[Random.Range(0, spawns.Length)];
            string kind = RaidMobs[Random.Range(0, Mathf.Min(RaidMobs.Length, 2 + net.ModeWave))];
            Enemy e = w.SpawnRuntimeEnemy(from, goal, kind, 1.9f + net.ModeWave * 0.18f, true, -1);
            if (e != null)
            {
                _raid.Add(e);
                net.HostSpawnEnemy(net.CurrentWorld, e.Id, kind, from, goal, e.Speed);
            }
            _spawnedThisWave++;
        }

        // Стены из блоков — не декорация. Враги двигаются заданием
        // позиции, а не физикой, и сквозь поставленный блок проходили бы
        // насквозь: вся стройка между волнами не значила бы ничего.
        // Упёршийся моб встаёт и прогрызает блок за несколько секунд —
        // стена не спасает навсегда, но выигрывает время, ради которого
        // её и ставят.
        Dictionary<int, int> blocks = net.BlocksOf(net.CurrentWorld);
        for (int i = 0; i < _raid.Count; i++)
        {
            Enemy e = _raid[i];
            if (e == null || e.Dying) continue;

            Vector3 dir = goal - e.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f) { e.Blocked = false; continue; }
            Vector3 probe = e.transform.position + dir.normalized * 0.9f +
                            new Vector3(0f, 0.6f, 0f);
            int cell = NetManager.CellAt(probe);

            if (!blocks.ContainsKey(cell))
            {
                e.Blocked = false;
                _chew.Remove(e.Id);
                continue;
            }

            e.Blocked = true;
            float t;
            _chew.TryGetValue(e.Id, out t);
            t += dt;
            if (t < ChewTime) { _chew[e.Id] = t; continue; }

            _chew.Remove(e.Id);
            e.Blocked = false;
            // Снимаем блок общим путём: он же разошлёт его гостям и
            // положит в сохранение.
            net.RequestPlace(net.CurrentWorld, cell, -1);
        }

        // Дошедший до фонтана исчезает и уносит одну жизнь.
        int lives = net.ModeLives;
        for (int i = 0; i < _raid.Count; i++)
        {
            Enemy e = _raid[i];
            if (e == null || e.Dying) continue;
            Vector3 flat = e.transform.position - goal;
            flat.y = 0f;
            if (flat.magnitude > ReachDist) continue;
            lives--;
            w.RemoveEnemy(e.Id, false);
            net.HostDespawnEnemy(net.CurrentWorld, e.Id);
            _raid[i] = null;
        }

        if (lives <= 0)
        {
            ClearRaid();
            Finish(net, "Фонтан разрушен. Деревня продержалась " + net.ModeWave + " волн");
            return;
        }

        alive = CountRaid();
        if (_spawnedThisWave >= _waveSize && alive == 0)
        {
            if (net.ModeWave >= NightWaves)
            {
                Finish(net, "Деревня выстояла все пять ночей!");
                return;
            }
            // Волна кончилась — список пуст по смыслу, чистим и по факту,
            // иначе он растёт мёртвыми ссылками все пять ночей.
            _raid.Clear();
            _chew.Clear();
            net.HostPublishMode(NetManager.PhaseWarmup, BuildTime, net.ModeWave + 1, lives,
                "Волна " + (net.ModeWave + 1) + " — стройте стены!");
            return;
        }

        net.HostPublishMode(NetManager.PhaseRun, 0f, net.ModeWave, lives,
            "Волна " + net.ModeWave + " — осталось врагов: " + alive);
    }

    private int CountRaid()
    {
        int n = 0;
        for (int i = 0; i < _raid.Count; i++)
            if (_raid[i] != null && !_raid[i].Dying) n++;
        return n;
    }

    // ---------- Гонка ----------

    private void BeginRace(NetManager net)
    {
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        Vector3[] route = w != null ? w.RaceRoute() : null;
        if (route == null || route.Length < 2) { Finish(net, "В этом краю нет маршрута"); return; }

        List<PlayerInfo> all = Alive(net);
        for (int i = 0; i < all.Count; i++) { all[i].Score = 0; all[i].Role = 0; }

        // На старт каждый встаёт сам, в LocalReact: хост чужого медведя
        // не двигает.

        net.HostPublishMode(NetManager.PhaseWarmup, RaceCountdown, 0, route.Length,
            "На старт!");
    }

    private void StepRace(NetManager net, float dt)
    {
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        Vector3[] route = w != null ? w.RaceRoute() : null;
        if (route == null || route.Length < 2) { Finish(net, "Маршрут пропал"); return; }

        net.ModeTimer -= dt;

        if (net.ModePhase == NetManager.PhaseWarmup)
        {
            if (net.ModeTimer <= 0f)
                net.HostPublishMode(NetManager.PhaseRun, RaceLimit, 0, route.Length, "Побежали!");
            else
                net.HostPublishMode(NetManager.PhaseWarmup, net.ModeTimer, 0, route.Length,
                    "До старта: " + Mathf.CeilToInt(net.ModeTimer));
            return;
        }

        if (net.ModePhase != NetManager.PhaseRun) return;

        List<PlayerInfo> all = Alive(net);
        for (int i = 0; i < all.Count; i++)
        {
            PlayerInfo p = all[i];
            if (p.Score >= route.Length) continue;
            Vector3 flat = PosOf(net, p) - route[p.Score];
            flat.y *= 0.45f;   // по высоте кольцо прощает больше, чем по земле
            if (flat.magnitude > RingDist) continue;
            p.Score++;
            if (p.Score >= route.Length)
            {
                _finishers++;
                p.Role = (byte)Mathf.Min(_finishers, 255);
            }
        }

        int done = 0;
        for (int i = 0; i < all.Count; i++) if (all[i].Score >= route.Length) done++;
        if (done >= all.Count && all.Count > 0) { Finish(net, "Все на финише!"); return; }
        if (net.ModeTimer <= 0f) { Finish(net, "Время вышло"); return; }

        net.HostPublishMode(NetManager.PhaseRun, net.ModeTimer, done, route.Length,
            "Финишировало: " + done + " из " + all.Count);
    }

    private void Finish(NetManager net, string text)
    {
        net.HostPublishMode(NetManager.PhaseOver, 8f, net.ModeWave, net.ModeLives, text);
    }

    // ---------- Что каждый делает у себя ----------

    private int _seenKind = -1;
    private int _seenPhase = -1;

    // Часть забавы нельзя сделать с хоста: чужого медведя он не двигает,
    // у каждого игрока свой. Поэтому на смену фазы каждый отзывается сам.
    private void LocalReact(NetManager net)
    {
        if (net.ModeKind == _seenKind && net.ModePhase == _seenPhase) return;

        // Гонка начинается с общей черты. Без этого хозяин вставал на
        // старт, а гости бежали с того места, где их застало начало.
        if (net.ModeKind == NetManager.ModeRace && net.ModePhase == NetManager.PhaseWarmup)
        {
            WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
            Vector3[] r = w != null ? w.RaceRoute() : null;
            BearPlayer local = GameRoot.LocalBear;
            if (r != null && r.Length > 0 && local != null)
                local.PlaceAt(r[0] + new Vector3(Random.Range(-2.2f, 2.2f), 0.4f,
                                                 Random.Range(-1.4f, 1.4f)));
        }

        _seenKind = net.ModeKind;
        _seenPhase = net.ModePhase;
    }

    // ---------- Кто водит ----------

    private readonly Dictionary<int, GameObject> _marks = new Dictionary<int, GameObject>();

    // Водящего должно быть видно издалека: без метки прятки не играются,
    // потому что непонятно, от кого убегать.
    private void SyncSeekerMarks(NetManager net)
    {
        bool want = net.ModeKind == NetManager.ModeHide &&
                    net.ModePhase != NetManager.PhaseOff;

        foreach (KeyValuePair<int, PlayerInfo> kv in net.Players)
        {
            bool seeker = want && kv.Value.Role != 0;
            GameObject mark;
            bool has = _marks.TryGetValue(kv.Key, out mark) && mark != null;

            if (!seeker)
            {
                if (has) Object.Destroy(mark);
                if (_marks.ContainsKey(kv.Key)) _marks.Remove(kv.Key);
                continue;
            }
            if (has) continue;

            BearPlayer p = GameRoot.PlayerById(kv.Key);
            if (p == null) continue;
            GameObject holder = new GameObject("Seeker");
            holder.transform.SetParent(p.transform, false);
            holder.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            Material m = Gfx.MatFull(new Color(1f, 0.3f, 0.25f), 0.7f, 0f,
                new Color(1f, 0.25f, 0.15f), 0f, 0f);
            Gfx.NoShadow(Gfx.Ball(holder.transform, Vector3.zero,
                new Vector3(0.45f, 0.45f, 0.45f), m));
            Gfx.Glow(holder.transform, Vector3.zero, 3.2f, new Color(1f, 0.35f, 0.25f, 0.5f));
            _marks[kv.Key] = holder;
        }

        // Ушедшие игроки метку за собой не оставляют.
        if (_marks.Count == 0) return;
        List<int> gone = new List<int>();
        foreach (KeyValuePair<int, GameObject> kv in _marks)
            if (!net.Players.ContainsKey(kv.Key)) gone.Add(kv.Key);
        for (int i = 0; i < gone.Count; i++)
        {
            if (_marks[gone[i]] != null) Object.Destroy(_marks[gone[i]]);
            _marks.Remove(gone[i]);
        }
    }

    // ---------- Кольца гонки ----------

    private void SyncRings(NetManager net)
    {
        bool want = net.ModeKind == NetManager.ModeRace &&
                    net.ModePhase != NetManager.PhaseOff;
        WorldBuilder w = GameRoot.I != null ? GameRoot.I.World : null;
        Vector3[] route = want && w != null ? w.RaceRoute() : null;

        if (route == null)
        {
            if (_rings.Count > 0) DropRings();
            return;
        }
        if (_rings.Count == 0 || _route != route) BuildRings(route);

        // Ближайшее нужное кольцо горит ярко, остальные приглушены —
        // иначе на маршруте из семи колец непонятно, куда бежать.
        PlayerInfo me;
        int next = 0;
        if (net.Players.TryGetValue(net.MyId, out me)) next = me.Score;
        for (int i = 0; i < _rings.Count; i++)
        {
            if (_rings[i] == null) continue;
            bool active = i == next;
            _rings[i].transform.localScale = Vector3.one * (active ? 1.15f : 0.75f);
        }
    }

    private void DropRings()
    {
        for (int i = 0; i < _rings.Count; i++)
            if (_rings[i] != null) Object.Destroy(_rings[i]);
        _rings.Clear();
        _route = null;
    }

    private void BuildRings(Vector3[] route)
    {
        DropRings();
        _route = route;
        for (int i = 0; i < route.Length; i++)
        {
            GameObject holder = new GameObject("Ring" + i);
            holder.transform.SetParent(transform, false);
            holder.transform.position = route[i];

            Color tint = i == route.Length - 1
                ? new Color(1f, 0.85f, 0.3f)
                : new Color(0.4f, 0.9f, 1f);
            Material m = Gfx.MatFull(tint, 0.7f, 0f, tint * 0.8f, 0f, 0f);
            for (int k = 0; k < 14; k++)
            {
                float a = k * (360f / 14f) * Mathf.Deg2Rad;
                GameObject seg = Gfx.Box(holder.transform,
                    new Vector3(Mathf.Cos(a) * 1.9f, 1.9f + Mathf.Sin(a) * 1.9f, 0f),
                    new Vector3(0.3f, 0.3f, 0.3f), m, false);
                Gfx.NoShadow(seg);
            }
            Gfx.Glow(holder.transform, new Vector3(0f, 1.9f, 0f), 4.4f,
                new Color(tint.r, tint.g, tint.b, 0.35f));
            _rings.Add(holder);
        }
    }
}
