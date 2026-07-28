using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public enum Msg : byte
{
    Join = 1,
    ClientState = 2,
    ReqCollect = 3,
    ReqKill = 4,
    ReqPortal = 5,
    ReqQuest = 6,
    ReqVictory = 7,
    Bye = 8,
    Ping = 9,
    ReqSpend = 10,

    Welcome = 20,
    Snapshot = 21,
    PlayerStates = 22,
    EnemyStates = 23,
    EvCoin = 24,
    EvKill = 25,
    EvWorld = 26,
    EvQuest = 27,
    EvVictory = 28
}

public class PlayerInfo
{
    public int Id;
    public string Name = "";
    public float Hue;
    public int Char;
    public Vector3 Pos;
    public float Yaw;
    public byte Anim;
    public float Flip;
    public float LastSeen;
}

public class EnemyState
{
    public int Id;
    public Vector3 Pos;
    public float Yaw;
}

public class FoundServer
{
    public string Ip;
    public string Name;
    public int Players;
    public float Seen;
}

// Сетевой менеджер: свой UDP-протокол (хост авторитетен), обнаружение
// хостов в локальной сети через UDP-broadcast. Периодические снапшоты
// восстанавливают состояние, если отдельные пакеты потерялись.
public class NetManager : MonoBehaviour
{
    public const int GamePort = 8910;
    public const int DiscoveryPort = 8911;
    public const string DiscoveryMsg = "BEAR_DISCOVER";
    // Звёзды живут в том же наборе собранного, что и монеты, но с
    // идентификаторами от 10000 — так их синхронизация достаётся даром.
    public const int StarIdBase = 10000;
    public static bool IsStarId(int id) { return id >= StarIdBase && id < HeartIdBase; }

    // Сердца — тот же механизм собранного, свой диапазон идентификаторов.
    public const int HeartIdBase = 20000;
    public static bool IsHeartId(int id) { return id >= HeartIdBase; }

    // Звёзды открывают миры, монеты тратятся в лавке.
    // Всего до пещеры доступно 14 звёзд: деревня 2 плюс четыре мира по 3.
    // Требовать 11 значило закрыть на сто процентов четыре мира из пяти —
    // одна пропущенная звезда в каждом, и игрок упирается в стену, не
    // понимая, где именно недобрал. Девять оставляют запас.
    public const int QuestStars = 2;
    public const int QuestStarsCity = 6;
    public const int QuestStarsFinal = 9;

    // Награда за победу. Считалась под пул монет в мирах (около 230) и
    // витрину лавки: с ней на полное прохождение выходит примерно 570.
    public const int EnemyReward = 2;
    public const int BossReward = 20;

    public const int QuestCoins = 15;
    public const int QuestCoinsCity = 40;
    public const int QuestCoinsFinal = 70;
    public const int MaxPlayers = 8;

    // 0 — деревня, 1 — луга, 2 — каньон, 3 — вершины, 4 — пещера.
    public static readonly string[] WorldIds =
        { "hub", "meadow", "desert", "snow", "cave", "turtle" };
    public const int WorldHub = 0;
    public const int WorldMeadow = 1;
    public const int WorldDesert = 2;
    public const int WorldSnow = 3;
    public const int WorldCave = 4;
    public const int WorldTurtle = 5;

    // Какая стадия задания нужна, чтобы портал открылся.
    public static int RequiredStage(int world)
    {
        if (world == WorldDesert || world == WorldSnow) return 2;
        if (world == WorldTurtle) return 3;
        if (world == WorldCave) return 4;
        return 0;
    }

    public static NetManager I;

    // --- Состояние сессии ---
    public string PlayerName = "Медведь";
    // Выбранный в меню персонаж (индекс в Heroes.Ids).
    public int CharIndex;
    public bool IsHost = true;
    public bool Online;
    public int MyId = 1;
    public int CurrentWorld;
    public int CoinsTotal;
    // Звёзды — настоящая цель: по три спрятано в каждом мире.
    public int StarsTotal;
    // Куплено в лавке: сердец и открытых персонажей.
    public int MaxHearts = 3;
    public int UnlockedChars = Heroes.FreeChars;
    public int QuestStage;
    public bool VictoryReached;
    public string StatusMessage = "";

    public readonly Dictionary<int, PlayerInfo> Players = new Dictionary<int, PlayerInfo>();
    public readonly Dictionary<string, FoundServer> FoundServers = new Dictionary<string, FoundServer>();

    private readonly Dictionary<int, HashSet<int>> _collected = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, HashSet<int>> _killed = new Dictionary<int, HashSet<int>>();

    // --- События для игрового слоя ---
    public event Action<int> OnWorldChanged;
    public event Action<int> OnCoinsChanged;
    public event Action<int> OnQuestChanged;
    public event Action<int, int> OnCoinRemoved;   // world, coinId
    public event Action<int, int> OnEnemyRemoved;  // world, enemyId
    public event Action OnVictory;
    public event Action OnPlayersChanged;
    public event Action<List<EnemyState>> OnEnemyStates;
    public event Action<string> OnJoinFailed;
    public event Action OnServerListUpdated;
    public event Action OnEnterGame;
    public event Action<string> OnLeaveToMenu;

    // --- Сокеты ---
    private UdpClient _game;
    private UdpClient _discServer;
    private UdpClient _discClient;
    private IPEndPoint _hostEp;

    private class ClientConn
    {
        public int Id;
        public IPEndPoint Ep;
        public float LastSeen;
    }

    private readonly List<ClientConn> _conns = new List<ClientConn>();
    private int _nextId = 2;

    private float _stateAccum;
    private float _snapAccum;
    private float _browseAccum;
    private float _lastHostPacket;
    private float _connectDeadline;
    private bool _awaitingWelcome;

    private readonly List<EnemyState> _enemyScratch = new List<EnemyState>();

    public static void Create()
    {
        if (I != null) return;
        GameObject go = new GameObject("Net");
        DontDestroyOnLoad(go);
        I = go.AddComponent<NetManager>();
        I.PlayerName = "Медведь-" + UnityEngine.Random.Range(1, 100);
    }

    public string CurrentWorldId
    {
        get { return WorldIds[Mathf.Clamp(CurrentWorld, 0, WorldIds.Length - 1)]; }
    }

    // ---------- Управление сессией ----------

    private void ResetSession()
    {
        Players.Clear();
        _collected.Clear();
        _killed.Clear();
        _conns.Clear();
        CoinsTotal = 0;
        StarsTotal = 0;
        QuestStage = 0;
        // Купленное в лавке тоже принадлежит сессии: без сброса стёртое
        // сохранение оставляло за игроком скины и сердца прошлой игры.
        MaxHearts = 3;
        UnlockedChars = Heroes.FreeChars;
        VictoryReached = false;
        CurrentWorld = 0;
        MyId = 1;
        _nextId = 2;
        _awaitingWelcome = false;
        _joinedAsClient = false;
    }

    private void CloseSockets()
    {
        if (_game != null) { try { _game.Close(); } catch (Exception) { } _game = null; }
        StopDiscoveryServer();
        StopBrowse();
        _hostEp = null;
    }

    public void StartSolo()
    {
        CloseSockets();
        ResetSession();
        IsHost = true;
        Online = false;
        // Продолжаем с того места, где закончили в прошлый раз.
        SaveGame.Load(this);
        AddLocalPlayer();
        if (OnEnterGame != null) OnEnterGame();
    }

    public bool StartHost()
    {
        CloseSockets();
        ResetSession();
        IsHost = true;
        // Хозяин игры продолжает свой прогресс — в одиночной игре так было
        // всегда, а по сети мир каждый раз начинался с нуля.
        SaveGame.Load(this);
        try
        {
            _game = new UdpClient(GamePort);
            _game.Client.Blocking = false;
        }
        catch (Exception e)
        {
            StatusMessage = "Не удалось открыть порт " + GamePort + " (" + e.Message + ")";
            _game = null;
            return false;
        }
        Online = true;
        AddLocalPlayer();
        StartDiscoveryServer();
        if (OnEnterGame != null) OnEnterGame();
        return true;
    }

    public bool StartJoin(string ip)
    {
        ip = (ip ?? "").Trim();
        IPAddress addr;
        if (ip.Length == 0 || !IPAddress.TryParse(ip, out addr))
        {
            StatusMessage = "Неверный адрес: " + ip;
            return false;
        }
        CloseSockets();
        ResetSession();
        IsHost = false;
        // Прогресс клиента приходит от хоста; свой файл он не трогает,
        // иначе чужое состояние затёрло бы одиночное сохранение.
        _joinedAsClient = true;
        try
        {
            _game = new UdpClient(0);
            _game.Client.Blocking = false;
        }
        catch (Exception e)
        {
            StatusMessage = "Не удалось открыть сокет (" + e.Message + ")";
            _game = null;
            return false;
        }
        Online = true;
        _hostEp = new IPEndPoint(addr, GamePort);
        _awaitingWelcome = true;
        _connectDeadline = Time.time + 6f;
        _lastHostPacket = Time.time;
        SendJoin();
        return true;
    }

    public void LeaveToMenu(string msg)
    {
        if (Online && !IsHost && _game != null && _hostEp != null)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.Bye, out ms, out w);
            SendToHost(ms);
        }
        StatusMessage = msg;
        CloseSockets();
        ResetSession();
        Online = false;
        IsHost = true;
        if (OnLeaveToMenu != null) OnLeaveToMenu(msg);
    }

    private void AddLocalPlayer()
    {
        PlayerInfo p = new PlayerInfo();
        p.Id = 1;
        p.Name = Trim16(PlayerName);
        p.Hue = HueFor(0);
        p.Char = CharIndex;
        p.LastSeen = Time.time;
        Players[1] = p;
        MyId = 1;
        if (OnPlayersChanged != null) OnPlayersChanged();
    }

    private static string Trim16(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Медведь";
        return s.Length > 16 ? s.Substring(0, 16) : s;
    }

    private static float HueFor(int index)
    {
        return Mathf.Repeat(0.07f + index * 0.18f, 1f);
    }

    public string GetLocalIpText()
    {
        List<string> ips = new List<string>();
        try
        {
            IPHostEntry entry = Dns.GetHostEntry(Dns.GetHostName());
            for (int i = 0; i < entry.AddressList.Length; i++)
            {
                IPAddress a = entry.AddressList[i];
                if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    ips.Add(a.ToString());
            }
        }
        catch (Exception) { }
        return ips.Count > 0 ? string.Join(", ", ips.ToArray()) : "—";
    }

    // ---------- Запросы игрового слоя ----------

    public bool IsCollected(int world, int coinId)
    {
        HashSet<int> set;
        return _collected.TryGetValue(world, out set) && set.Contains(coinId);
    }

    public bool IsKilled(int world, int enemyId)
    {
        HashSet<int> set;
        return _killed.TryGetValue(world, out set) && set.Contains(enemyId);
    }

    // Для сохранения — всё собранное, кроме сердец. Сердце это лечение,
    // а не награда: попав в файл, оно исчезало из мира навсегда, и после
    // первого визита восстановить здоровье было негде, кроме как умереть.
    // В самой сессии сердца в _collected остаются — иначе подобранное
    // сердце сразу появлялось бы обратно.
    public Dictionary<int, HashSet<int>> ExportCollected()
    {
        Dictionary<int, HashSet<int>> result = new Dictionary<int, HashSet<int>>();
        foreach (KeyValuePair<int, HashSet<int>> kv in _collected)
        {
            if (kv.Value == null) continue;
            HashSet<int> keep = new HashSet<int>();
            foreach (int id in kv.Value)
                if (!IsHeartId(id)) keep.Add(id);
            if (keep.Count > 0) result[kv.Key] = keep;
        }
        return result;
    }

    public Dictionary<int, HashSet<int>> ExportKilled() { return _killed; }

    public void ImportCollected(Dictionary<int, HashSet<int>> src)
    {
        _collected.Clear();
        if (src == null) return;
        foreach (KeyValuePair<int, HashSet<int>> kv in src) _collected[kv.Key] = kv.Value;
    }

    public void ImportKilled(Dictionary<int, HashSet<int>> src)
    {
        _killed.Clear();
        if (src == null) return;
        foreach (KeyValuePair<int, HashSet<int>> kv in src) _killed[kv.Key] = kv.Value;
    }

    // Прогресс пишем только у хозяина: у клиента он приходит по сети.
    // Покупка в лавке. Возвращает false, если не хватает монет.
    public bool Spend(int amount)
    {
        if (amount <= 0 || CoinsTotal < amount) return false;
        // В сетевой игре кошелёк общий и живёт на хосте: локальная трата
        // возвращалась обратно ближайшим снапшотом, и покупки были даром.
        if (Online && !IsHost)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.ReqSpend, out ms, out w);
            w.Write(amount);
            SendToHost(ms);
            return true;
        }
        CoinsTotal -= amount;
        if (OnCoinsChanged != null) OnCoinsChanged(CoinsTotal);
        SaveProgress();
        return true;
    }

    private void HostSpend(int amount)
    {
        if (amount <= 0 || CoinsTotal < amount) return;
        CoinsTotal -= amount;
        if (OnCoinsChanged != null) OnCoinsChanged(CoinsTotal);
        SaveProgress();
        if (Online) BroadcastSnapshot();
    }

    public void SaveProgress()
    {
        // Клиент живёт состоянием хоста — записывать его себе нельзя.
        if (IsHost && !_joinedAsClient) SaveGame.Save(this);
    }

    private bool _joinedAsClient;

    private HashSet<int> CollectedSet(int world)
    {
        HashSet<int> set;
        if (!_collected.TryGetValue(world, out set)) { set = new HashSet<int>(); _collected[world] = set; }
        return set;
    }

    private HashSet<int> KilledSet(int world)
    {
        HashSet<int> set;
        if (!_killed.TryGetValue(world, out set)) { set = new HashSet<int>(); _killed[world] = set; }
        return set;
    }

    public void RequestCollect(int world, int coinId)
    {
        if (IsHost) { HostCollect(world, coinId); return; }
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ReqCollect, out ms, out w);
        w.Write(world); w.Write(coinId);
        SendToHost(ms);
    }

    public void RequestKill(int world, int enemyId)
    {
        if (IsHost) { HostKill(world, enemyId); return; }
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ReqKill, out ms, out w);
        w.Write(world); w.Write(enemyId);
        SendToHost(ms);
    }

    public void RequestPortal(int target)
    {
        if (IsHost) { HostPortal(target); return; }
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ReqPortal, out ms, out w);
        w.Write(target);
        SendToHost(ms);
    }

    public void RequestQuest()
    {
        if (IsHost) { HostQuest(); return; }
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ReqQuest, out ms, out w);
        SendToHost(ms);
    }

    public void RequestVictory()
    {
        if (IsHost) { HostVictory(); return; }
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ReqVictory, out ms, out w);
        SendToHost(ms);
    }

    // ---------- Обработка на хосте ----------

    private void HostCollect(int world, int coinId)
    {
        if (world != CurrentWorld) return;
        HashSet<int> set = CollectedSet(world);
        if (set.Contains(coinId)) return;
        set.Add(coinId);
        // Лечит не хост, а тот, кто подобрал: здоровье личное, и раньше
        // сердце, поднятое клиентом, прибавлялось хозяину игры.
        // Само лечение происходит на месте подбора, в GameRoot.
        if (IsHeartId(coinId)) { }
        else if (IsStarId(coinId)) StarsTotal++;
        else CoinsTotal++;
        ApplyCoin(world, coinId, CoinsTotal, StarsTotal);
        SaveProgress();
        if (Online)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvCoin, out ms, out w);
            w.Write(world); w.Write(coinId); w.Write(CoinsTotal); w.Write(StarsTotal);
            BroadcastRepeat(ms, 2);
        }
        CheckQuest();
    }

    private void HostKill(int world, int enemyId)
    {
        // Босс переживает несколько попаданий: удаляем его только когда
        // здоровье кончилось, иначе рассылаем лишь эффект попадания.
        bool wasBoss = false;
        if (world == CurrentWorld && GameRoot.I != null && GameRoot.I.World != null)
        {
            Enemy e;
            if (GameRoot.I.World.Enemies.TryGetValue(enemyId, out e) && e != null && e.IsBoss)
            {
                wasBoss = true;
                if (!e.TakeHit()) return;
            }
        }

        if (world != CurrentWorld) return;
        HashSet<int> set = KilledSet(world);
        if (set.Contains(enemyId)) return;
        set.Add(enemyId);

        // Враги платят. Раньше за победу не давали ничего — ни за рядового,
        // ни за босса, — а всех монет в шести мирах около 230 при витрине
        // лавки почти на тысячу: половину скинов нельзя было купить не
        // потому что дорого, а потому что монет в игре физически нет.
        CoinsTotal += wasBoss ? BossReward : EnemyReward;
        SaveProgress();

        ApplyKill(world, enemyId);
        if (Online)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvKill, out ms, out w);
            w.Write(world); w.Write(enemyId);
            BroadcastRepeat(ms, 2);
            // Клиентам нужен новый счёт монет — он идёт отдельным событием.
            Begin(Msg.EvCoin, out ms, out w);
            w.Write(world); w.Write(-1); w.Write(CoinsTotal); w.Write(StarsTotal);
            BroadcastRepeat(ms, 2);
        }
    }

    private void HostPortal(int target)
    {
        if (target < 0 || target >= WorldIds.Length) return;
        if (target == CurrentWorld) return;
        if (QuestStage < RequiredStage(target)) return;
        SetWorld(target);
    }

    private void HostQuest()
    {
        if (QuestStage != 0) return;
        QuestStage = 1;
        ApplyQuest(1);
        if (Online)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvQuest, out ms, out w);
            w.Write(QuestStage);
            BroadcastRepeat(ms, 2);
        }
        CheckQuest();
    }

    private void HostVictory()
    {
        if (VictoryReached || CurrentWorld != WorldCave) return;
        VictoryReached = true;
        if (OnVictory != null) OnVictory();
        if (Online)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvVictory, out ms, out w);
            BroadcastRepeat(ms, 3);
        }
        Invoke("ReturnToHub", 7f);
    }

    private void ReturnToHub()
    {
        if (IsHost) SetWorld(WorldHub);
    }

    private void CheckQuest()
    {
        int stage = QuestStage;
        if (stage == 1 && StarsTotal >= QuestStars) stage = 2;
        if (stage == 2 && StarsTotal >= QuestStarsCity) stage = 3;
        if (stage == 3 && StarsTotal >= QuestStarsFinal) stage = 4;
        if (stage == QuestStage) return;

        QuestStage = stage;
        ApplyQuest(stage);
        if (Online)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvQuest, out ms, out w);
            w.Write(QuestStage);
            BroadcastRepeat(ms, 2);
        }
    }

    private void SetWorld(int world)
    {
        CurrentWorld = world;
        ApplyWorld(world);
        if (Online && IsHost)
        {
            MemoryStream ms; BinaryWriter w;
            Begin(Msg.EvWorld, out ms, out w);
            w.Write(world);
            BroadcastRepeat(ms, 3);
        }
    }

    // ---------- Применение состояния (общее для хоста и клиента) ----------

    private void ApplyWorld(int world)
    {
        CurrentWorld = world;
        VictoryReached = false;
        if (OnWorldChanged != null) OnWorldChanged(world);
        SaveProgress();
    }

    private void ApplyCoin(int world, int coinId, int total, int stars)
    {
        // coinId < 0 — это не подбор, а просто новый счёт монет: так
        // рассылается награда за побеждённого врага.
        if (coinId >= 0)
        {
            CollectedSet(world).Add(coinId);
            if (OnCoinRemoved != null) OnCoinRemoved(world, coinId);
        }
        CoinsTotal = total;
        StarsTotal = stars;
        if (OnCoinsChanged != null) OnCoinsChanged(total);
    }

    private void ApplyKill(int world, int enemyId)
    {
        KilledSet(world).Add(enemyId);
        if (OnEnemyRemoved != null) OnEnemyRemoved(world, enemyId);
    }

    private void ApplyQuest(int stage)
    {
        QuestStage = stage;
        if (OnQuestChanged != null) OnQuestChanged(stage);
        SaveProgress();
    }

    // ---------- Отправка локального состояния игрока ----------

    public void SendLocalState(Vector3 pos, float yaw, byte anim, float flip)
    {
        PlayerInfo me;
        if (Players.TryGetValue(MyId, out me))
        {
            me.Pos = pos; me.Yaw = yaw; me.Anim = anim; me.Flip = flip;
        }
        if (!Online || IsHost || _game == null || _hostEp == null) return;
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.ClientState, out ms, out w);
        WriteVec(w, pos); w.Write(yaw); w.Write(anim); w.Write(flip);
        SendToHost(ms);
    }

    public void PublishEnemyStates(List<EnemyState> states)
    {
        if (!Online || !IsHost || states == null) return;
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.EnemyStates, out ms, out w);
        w.Write(CurrentWorld);
        w.Write((byte)Mathf.Min(states.Count, 255));
        int count = Mathf.Min(states.Count, 255);
        for (int i = 0; i < count; i++)
        {
            w.Write(states[i].Id);
            WriteVec(w, states[i].Pos);
            w.Write(states[i].Yaw);
        }
        BroadcastToClients(ms);
    }

    // ---------- Цикл ----------

    private void Update()
    {
        PollGame();
        PollDiscoveryServer();
        PollBrowse();

        if (!Online) return;

        if (IsHost)
        {
            _stateAccum += Time.deltaTime;
            if (_stateAccum >= 0.05f) { _stateAccum = 0f; BroadcastPlayerStates(); }
            _snapAccum += Time.deltaTime;
            if (_snapAccum >= 0.5f) { _snapAccum = 0f; BroadcastSnapshot(); }
            DropStaleClients();
        }
        else
        {
            if (_awaitingWelcome)
            {
                _stateAccum += Time.deltaTime;
                if (_stateAccum >= 0.5f) { _stateAccum = 0f; SendJoin(); }
                if (Time.time > _connectDeadline)
                {
                    _awaitingWelcome = false;
                    StatusMessage = "Не удалось подключиться к хосту";
                    CloseSockets();
                    Online = false;
                    IsHost = true;
                    if (OnJoinFailed != null) OnJoinFailed(StatusMessage);
                }
            }
            else if (Time.time - _lastHostPacket > 6f)
            {
                LeaveToMenu("Хост отключился");
            }
        }
    }

    private void OnApplicationQuit()
    {
        CloseSockets();
    }

    // ---------- Приём ----------

    private void PollGame()
    {
        if (_game == null) return;
        for (int guard = 0; guard < 256; guard++)
        {
            byte[] data = null;
            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                if (_game.Available <= 0) break;
                data = _game.Receive(ref from);
            }
            catch (SocketException) { break; }
            catch (Exception) { break; }
            if (data == null || data.Length < 1) continue;
            try { Handle(data, from); }
            catch (Exception e) { Debug.LogWarning("Net parse error: " + e.Message); }
        }
    }

    private void Handle(byte[] data, IPEndPoint from)
    {
        MemoryStream ms = new MemoryStream(data);
        BinaryReader r = new BinaryReader(ms);
        Msg type = (Msg)r.ReadByte();

        if (IsHost) HandleAsHost(type, r, from);
        else HandleAsClient(type, r);
    }

    private void HandleAsHost(Msg type, BinaryReader r, IPEndPoint from)
    {
        ClientConn conn = FindConn(from);
        if (conn != null) conn.LastSeen = Time.time;

        switch (type)
        {
            case Msg.Join:
                {
                    string name = r.ReadString();
                    int joinChar = r.ReadInt32();
                    if (conn == null)
                    {
                        if (_conns.Count + 1 >= MaxPlayers) return;
                        conn = new ClientConn();
                        conn.Id = _nextId++;
                        conn.Ep = new IPEndPoint(from.Address, from.Port);
                        conn.LastSeen = Time.time;
                        _conns.Add(conn);

                        PlayerInfo p = new PlayerInfo();
                        p.Id = conn.Id;
                        p.Name = Trim16(name);
                        p.Hue = HueFor(Players.Count);
                        p.Char = joinChar;
                        p.LastSeen = Time.time;
                        Players[conn.Id] = p;
                        if (OnPlayersChanged != null) OnPlayersChanged();
                    }
                    MemoryStream wm; BinaryWriter w;
                    Begin(Msg.Welcome, out wm, out w);
                    w.Write(conn.Id);
                    SendTo(wm, conn.Ep);
                    BroadcastSnapshot();
                    break;
                }
            case Msg.ClientState:
                {
                    if (conn == null) return;
                    Vector3 pos = ReadVec(r);
                    float yaw = r.ReadSingle();
                    byte anim = r.ReadByte();
                    float flip = r.ReadSingle();
                    PlayerInfo p;
                    if (Players.TryGetValue(conn.Id, out p))
                    {
                        p.Pos = pos; p.Yaw = yaw; p.Anim = anim; p.Flip = flip; p.LastSeen = Time.time;
                    }
                    break;
                }
            case Msg.ReqCollect: HostCollect(r.ReadInt32(), r.ReadInt32()); break;
            case Msg.ReqKill: HostKill(r.ReadInt32(), r.ReadInt32()); break;
            case Msg.ReqPortal: HostPortal(r.ReadInt32()); break;
            case Msg.ReqQuest: HostQuest(); break;
            case Msg.ReqSpend:
                HostSpend(r.ReadInt32());
                break;
            case Msg.ReqVictory: HostVictory(); break;
            case Msg.Bye:
                if (conn != null)
                {
                    Players.Remove(conn.Id);
                    _conns.Remove(conn);
                    if (OnPlayersChanged != null) OnPlayersChanged();
                }
                break;
        }
    }

    private void HandleAsClient(Msg type, BinaryReader r)
    {
        _lastHostPacket = Time.time;
        switch (type)
        {
            case Msg.Welcome:
                {
                    int id = r.ReadInt32();
                    if (_awaitingWelcome)
                    {
                        _awaitingWelcome = false;
                        MyId = id;
                        if (OnEnterGame != null) OnEnterGame();
                    }
                    break;
                }
            case Msg.Snapshot: ReadSnapshot(r); break;
            case Msg.PlayerStates:
                {
                    int count = r.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int id = r.ReadInt32();
                        Vector3 pos = ReadVec(r);
                        float yaw = r.ReadSingle();
                        byte anim = r.ReadByte();
                        float flip = r.ReadSingle();
                        if (id == MyId) continue;
                        PlayerInfo p;
                        if (Players.TryGetValue(id, out p))
                        {
                            p.Pos = pos; p.Yaw = yaw; p.Anim = anim; p.Flip = flip; p.LastSeen = Time.time;
                        }
                    }
                    break;
                }
            case Msg.EnemyStates:
                {
                    int world = r.ReadInt32();
                    int count = r.ReadByte();
                    _enemyScratch.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        EnemyState es = new EnemyState();
                        es.Id = r.ReadInt32();
                        es.Pos = ReadVec(r);
                        es.Yaw = r.ReadSingle();
                        _enemyScratch.Add(es);
                    }
                    if (world == CurrentWorld && OnEnemyStates != null) OnEnemyStates(_enemyScratch);
                    break;
                }
            case Msg.EvCoin:
                {
                    int world = r.ReadInt32(); int coin = r.ReadInt32();
                    int total = r.ReadInt32(); int stars = r.ReadInt32();
                    if (!IsCollected(world, coin)) ApplyCoin(world, coin, total, stars);
                    break;
                }
            case Msg.EvKill:
                {
                    int world = r.ReadInt32(); int enemy = r.ReadInt32();
                    if (!IsKilled(world, enemy)) ApplyKill(world, enemy);
                    break;
                }
            case Msg.EvWorld:
                {
                    int world = r.ReadInt32();
                    if (world != CurrentWorld) ApplyWorld(world);
                    break;
                }
            case Msg.EvQuest:
                {
                    int stage = r.ReadInt32();
                    if (stage != QuestStage) ApplyQuest(stage);
                    break;
                }
            case Msg.EvVictory:
                if (!VictoryReached)
                {
                    VictoryReached = true;
                    if (OnVictory != null) OnVictory();
                }
                break;
        }
    }

    // ---------- Снапшот ----------

    private void BroadcastSnapshot()
    {
        if (!Online || !IsHost || _conns.Count == 0) return;
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.Snapshot, out ms, out w);
        w.Write(CurrentWorld);
        w.Write(CoinsTotal);
        w.Write(QuestStage);
        w.Write(VictoryReached);

        HashSet<int> col = CollectedSet(CurrentWorld);
        w.Write((ushort)col.Count);
        foreach (int id in col) w.Write(id);

        HashSet<int> kil = KilledSet(CurrentWorld);
        w.Write((ushort)kil.Count);
        foreach (int id in kil) w.Write(id);

        w.Write(StarsTotal);
        w.Write((byte)Players.Count);
        foreach (KeyValuePair<int, PlayerInfo> kv in Players)
        {
            w.Write(kv.Key);
            w.Write(kv.Value.Name);
            w.Write(kv.Value.Hue);
            w.Write(kv.Value.Char);
        }
        BroadcastToClients(ms);
    }

    private void ReadSnapshot(BinaryReader r)
    {
        int world = r.ReadInt32();
        int coins = r.ReadInt32();
        int quest = r.ReadInt32();
        bool victory = r.ReadBoolean();

        int colCount = r.ReadUInt16();
        HashSet<int> col = CollectedSet(world);
        for (int i = 0; i < colCount; i++)
        {
            int id = r.ReadInt32();
            if (col.Add(id) && world == CurrentWorld && OnCoinRemoved != null) OnCoinRemoved(world, id);
        }

        int kilCount = r.ReadUInt16();
        HashSet<int> kil = KilledSet(world);
        for (int i = 0; i < kilCount; i++)
        {
            int id = r.ReadInt32();
            if (kil.Add(id) && world == CurrentWorld && OnEnemyRemoved != null) OnEnemyRemoved(world, id);
        }

        StarsTotal = r.ReadInt32();
        int pCount = r.ReadByte();
        HashSet<int> seen = new HashSet<int>();
        bool changed = false;
        for (int i = 0; i < pCount; i++)
        {
            int id = r.ReadInt32();
            string name = r.ReadString();
            float hue = r.ReadSingle();
            int ch = r.ReadInt32();
            seen.Add(id);
            PlayerInfo p;
            if (!Players.TryGetValue(id, out p))
            {
                p = new PlayerInfo();
                p.Id = id;
                Players[id] = p;
                changed = true;
            }
            p.Name = name;
            p.Hue = hue;
            p.Char = ch;
        }
        List<int> stale = new List<int>();
        foreach (KeyValuePair<int, PlayerInfo> kv in Players)
            if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
        for (int i = 0; i < stale.Count; i++) { Players.Remove(stale[i]); changed = true; }

        if (CoinsTotal != coins) { CoinsTotal = coins; if (OnCoinsChanged != null) OnCoinsChanged(coins); }
        if (QuestStage != quest) ApplyQuest(quest);
        if (world != CurrentWorld) ApplyWorld(world);
        if (victory && !VictoryReached) { VictoryReached = true; if (OnVictory != null) OnVictory(); }
        if (changed && OnPlayersChanged != null) OnPlayersChanged();
    }

    private void BroadcastPlayerStates()
    {
        if (_conns.Count == 0) return;
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.PlayerStates, out ms, out w);
        w.Write((byte)Players.Count);
        foreach (KeyValuePair<int, PlayerInfo> kv in Players)
        {
            PlayerInfo p = kv.Value;
            w.Write(p.Id);
            WriteVec(w, p.Pos);
            w.Write(p.Yaw);
            w.Write(p.Anim);
            w.Write(p.Flip);
        }
        BroadcastToClients(ms);
    }

    private void DropStaleClients()
    {
        for (int i = _conns.Count - 1; i >= 0; i--)
        {
            if (Time.time - _conns[i].LastSeen > 8f)
            {
                Players.Remove(_conns[i].Id);
                _conns.RemoveAt(i);
                if (OnPlayersChanged != null) OnPlayersChanged();
            }
        }
    }

    // ---------- Низкоуровневая отправка ----------

    private static void Begin(Msg type, out MemoryStream ms, out BinaryWriter w)
    {
        ms = new MemoryStream();
        w = new BinaryWriter(ms);
        w.Write((byte)type);
    }

    private static void WriteVec(BinaryWriter w, Vector3 v)
    {
        w.Write(v.x); w.Write(v.y); w.Write(v.z);
    }

    private static Vector3 ReadVec(BinaryReader r)
    {
        return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    private void SendJoin()
    {
        MemoryStream ms; BinaryWriter w;
        Begin(Msg.Join, out ms, out w);
        w.Write(Trim16(PlayerName));
        w.Write(CharIndex);
        SendToHost(ms);
    }

    private void SendToHost(MemoryStream ms)
    {
        if (_game == null || _hostEp == null) return;
        byte[] b = ms.ToArray();
        try { _game.Send(b, b.Length, _hostEp); } catch (Exception) { }
    }

    private void SendTo(MemoryStream ms, IPEndPoint ep)
    {
        if (_game == null) return;
        byte[] b = ms.ToArray();
        try { _game.Send(b, b.Length, ep); } catch (Exception) { }
    }

    private void BroadcastToClients(MemoryStream ms)
    {
        if (_game == null) return;
        byte[] b = ms.ToArray();
        for (int i = 0; i < _conns.Count; i++)
        {
            try { _game.Send(b, b.Length, _conns[i].Ep); } catch (Exception) { }
        }
    }

    private void BroadcastRepeat(MemoryStream ms, int times)
    {
        for (int i = 0; i < times; i++) BroadcastToClients(ms);
    }

    private ClientConn FindConn(IPEndPoint ep)
    {
        for (int i = 0; i < _conns.Count; i++)
        {
            if (_conns[i].Ep.Port == ep.Port && _conns[i].Ep.Address.Equals(ep.Address))
                return _conns[i];
        }
        return null;
    }

    // ---------- Обнаружение хостов ----------

    private void StartDiscoveryServer()
    {
        try
        {
            _discServer = new UdpClient(DiscoveryPort);
            _discServer.Client.Blocking = false;
        }
        catch (Exception) { _discServer = null; }
    }

    private void StopDiscoveryServer()
    {
        if (_discServer != null) { try { _discServer.Close(); } catch (Exception) { } _discServer = null; }
    }

    private void PollDiscoveryServer()
    {
        if (_discServer == null) return;
        for (int guard = 0; guard < 32; guard++)
        {
            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                if (_discServer.Available <= 0) break;
                data = _discServer.Receive(ref from);
            }
            catch (Exception) { break; }
            if (data == null) continue;
            if (Encoding.UTF8.GetString(data) != DiscoveryMsg) continue;
            string reply = Trim16(PlayerName) + "|" + Players.Count;
            byte[] rb = Encoding.UTF8.GetBytes(reply);
            try { _discServer.Send(rb, rb.Length, from); } catch (Exception) { }
        }
    }

    public void StartBrowse()
    {
        StopBrowse();
        FoundServers.Clear();
        try
        {
            _discClient = new UdpClient(0);
            _discClient.Client.Blocking = false;
            _discClient.EnableBroadcast = true;
        }
        catch (Exception) { _discClient = null; return; }
        _browseAccum = 1f;
    }

    public void StopBrowse()
    {
        if (_discClient != null) { try { _discClient.Close(); } catch (Exception) { } _discClient = null; }
    }

    private void PollBrowse()
    {
        if (_discClient == null) return;
        _browseAccum += Time.deltaTime;
        if (_browseAccum >= 1f)
        {
            _browseAccum = 0f;
            byte[] probe = Encoding.UTF8.GetBytes(DiscoveryMsg);
            try
            {
                _discClient.Send(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch (Exception) { }
        }

        bool changed = false;
        for (int guard = 0; guard < 32; guard++)
        {
            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                if (_discClient.Available <= 0) break;
                data = _discClient.Receive(ref from);
            }
            catch (Exception) { break; }
            if (data == null) continue;
            string text = Encoding.UTF8.GetString(data);
            if (text == DiscoveryMsg) continue;
            string[] parts = text.Split('|');
            FoundServer fs;
            string ip = from.Address.ToString();
            if (!FoundServers.TryGetValue(ip, out fs))
            {
                fs = new FoundServer();
                fs.Ip = ip;
                FoundServers[ip] = fs;
            }
            fs.Name = parts.Length > 0 ? parts[0] : "Игра";
            int pc = 1;
            if (parts.Length > 1) int.TryParse(parts[1], out pc);
            fs.Players = pc;
            fs.Seen = Time.time;
            changed = true;
        }

        List<string> stale = new List<string>();
        foreach (KeyValuePair<string, FoundServer> kv in FoundServers)
            if (Time.time - kv.Value.Seen > 5f) stale.Add(kv.Key);
        for (int i = 0; i < stale.Count; i++) { FoundServers.Remove(stale[i]); changed = true; }

        if (changed && OnServerListUpdated != null) OnServerListUpdated();
    }
}
