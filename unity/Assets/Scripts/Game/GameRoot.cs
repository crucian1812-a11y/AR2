using System.Collections.Generic;
using UnityEngine;

// Корень игровой сцены: держит текущий мир и всех медведей,
// переключает миры и на хосте считает поведение врагов.
public class GameRoot : MonoBehaviour
{
    public static GameRoot I;

    public WorldBuilder World;
    public BearPlayer LocalPlayer;

    // Быстрый доступ для реквизита сцены (платформы, батуты).
    public static BearPlayer LocalBear
    {
        get { return I != null ? I.LocalPlayer : null; }
    }

    private readonly Dictionary<int, BearPlayer> _players = new Dictionary<int, BearPlayer>();
    private readonly HashSet<int> _visited = new HashSet<int>();

    // Ближайший к точке медведь — врагам нужно знать, за кем гнаться.
    // В одиночной игре это всегда локальный, в сети — любой из подключённых.
    public static BearPlayer NearestPlayer(Vector3 worldPos)
    {
        if (I == null) return null;
        BearPlayer best = null;
        float bestSqr = float.MaxValue;
        foreach (KeyValuePair<int, BearPlayer> kv in I._players)
        {
            if (kv.Value == null) continue;
            float d = (kv.Value.transform.position - worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = kv.Value; }
        }
        return best;
    }

    private Transform _worldHolder;
    private Transform _playersHolder;
    private Hud _hud;
    private ShopUI _shop;
    private float _enemySendAccum;
    private bool _worldReady;

    public static GameRoot Create()
    {
        GameObject go = new GameObject("GameRoot");
        GameRoot root = go.AddComponent<GameRoot>();
        I = root;
        root.Init();
        return root;
    }

    private void Init()
    {
        GameObject wh = new GameObject("World");
        wh.transform.SetParent(transform, false);
        _worldHolder = wh.transform;

        GameObject ph = new GameObject("Players");
        ph.transform.SetParent(transform, false);
        _playersHolder = ph.transform;

        _hud = Hud.Create();
        _hud.transform.SetParent(transform, false);
        _shop = ShopUI.Create(transform);

        NetManager net = NetManager.I;
        net.OnWorldChanged += LoadWorld;
        net.OnPlayersChanged += RefreshPlayers;
        net.OnCoinRemoved += OnCoinRemoved;
        net.OnEnemyRemoved += OnEnemyRemoved;
        net.OnEnemyStates += OnEnemyStates;
        net.OnQuestChanged += OnQuestChanged;
        net.OnBrokenRemoved += OnBrokenRemoved;
        net.OnBlockPlaced += OnBlockPlaced;
        net.OnBlockRemoved += OnBlockRemoved;

        if (net.IsHost)
        {
            LoadWorld(net.CurrentWorld);
            RefreshPlayers();
        }
        else
        {
            _hud.ShowLoading(true);
            LoadWorld(net.CurrentWorld);
            RefreshPlayers();
        }
    }

    private void OnDestroy()
    {
        NetManager net = NetManager.I;
        if (net != null)
        {
            net.OnWorldChanged -= LoadWorld;
            net.OnPlayersChanged -= RefreshPlayers;
            net.OnCoinRemoved -= OnCoinRemoved;
            net.OnEnemyRemoved -= OnEnemyRemoved;
            net.OnEnemyStates -= OnEnemyStates;
            net.OnQuestChanged -= OnQuestChanged;
            net.OnBrokenRemoved -= OnBrokenRemoved;
            net.OnBlockPlaced -= OnBlockPlaced;
            net.OnBlockRemoved -= OnBlockRemoved;
        }
        if (I == this) I = null;
    }

    // ---------- Миры ----------

    private void LoadWorld(int worldIndex)
    {
        _worldReady = false;
        for (int i = _worldHolder.childCount - 1; i >= 0; i--)
            Object.Destroy(_worldHolder.GetChild(i).gameObject);
        World = null;
        Gfx.ClearCache();

        GameObject go = new GameObject("WorldInstance");
        go.transform.SetParent(_worldHolder, false);

        WorldBuilder wb;
        if (worldIndex == NetManager.WorldMeadow) wb = go.AddComponent<MeadowWorld>();
        else if (worldIndex == NetManager.WorldDesert) wb = go.AddComponent<DesertWorld>();
        else if (worldIndex == NetManager.WorldSnow) wb = go.AddComponent<SnowWorld>();
        else if (worldIndex == NetManager.WorldCave) wb = go.AddComponent<CaveWorld>();
        else if (worldIndex == NetManager.WorldTurtle) wb = go.AddComponent<TurtleWorld>();
        else wb = go.AddComponent<HubWorld>();

        Snd.SetTheme(worldIndex);
        wb.Construct(worldIndex);
        World = wb;
        // Поставленные блоки — отдельный слой поверх мира: мир строится
        // кодом заново, а блоки приходят из сохранения и из сети.
        BlockField.Create(go.transform, worldIndex);
        _worldReady = true;

        // Побывал во всех шести краях.
        _visited.Add(worldIndex);
        if (_visited.Count >= NetManager.WorldIds.Length) Achievements.Grant("allworlds");

        PlaceAllPlayers();
        // Камера принадлежит игроку и может появиться позже мира —
        // настроение картинки применяем ещё раз, когда она уже есть.
        wb.ApplyPostFx();
        if (_hud != null) _hud.ShowLoading(false);
    }

    private void PlaceAllPlayers()
    {
        if (World == null) return;
        int idx = 0;
        foreach (KeyValuePair<int, BearPlayer> kv in _players)
        {
            if (kv.Value == null) continue;
            Vector3 offset = new Vector3((idx % 4) * 1.4f - 2.1f, 0f, (idx / 4) * 1.4f);
            kv.Value.PlaceAt(World.SpawnPoint + offset);
            idx++;
        }
    }

    // ---------- Игроки ----------

    private void RefreshPlayers()
    {
        NetManager net = NetManager.I;
        if (net == null) return;

        foreach (KeyValuePair<int, PlayerInfo> kv in net.Players)
        {
            if (_players.ContainsKey(kv.Key)) continue;
            bool isLocal = kv.Key == net.MyId;
            BearPlayer p = BearPlayer.Spawn(_playersHolder, kv.Value, isLocal);
            _players[kv.Key] = p;
            if (isLocal)
            {
                LocalPlayer = p;
                // Камера создана вместе с локальным медведем — можно
                // применить постобработку текущего мира.
                if (World != null) World.ApplyPostFx();
            }
            if (World != null)
            {
                int idx = _players.Count - 1;
                p.PlaceAt(World.SpawnPoint + new Vector3((idx % 4) * 1.4f - 2.1f, 0f, (idx / 4) * 1.4f));
            }
        }

        List<int> stale = new List<int>();
        foreach (KeyValuePair<int, BearPlayer> kv in _players)
            if (!net.Players.ContainsKey(kv.Key)) stale.Add(kv.Key);
        for (int i = 0; i < stale.Count; i++)
        {
            BearPlayer p = _players[stale[i]];
            _players.Remove(stale[i]);
            if (p != null) Object.Destroy(p.gameObject);
        }
    }

    // ---------- Цикл ----------

    private void Update()
    {
        NetManager net = NetManager.I;
        if (net == null || !_worldReady || World == null) return;

        if (DayCycle.IsNight) Achievements.Grant("firstnight");

        // Самая первая подсказка: базовое управление игра не объясняла
        // вообще. Показываем её сразу, как только игрок появился в мире.
        if (LocalPlayer != null)
            Tutor.Show("jump", "Слева — джойстик, справа — Прыжок и Удар. Врагов можно бить или прыгать им на голову.");

        // Удалённые игроки следуют за состоянием из сети.
        foreach (KeyValuePair<int, BearPlayer> kv in _players)
        {
            if (kv.Value == null || kv.Value.IsLocal) continue;
            PlayerInfo info;
            if (net.Players.TryGetValue(kv.Key, out info))
                kv.Value.ApplyNetState(info.Pos, info.Yaw, info.Anim, info.Flip);
        }

        if (net.IsHost)
        {
            World.SimulateEnemies(Time.deltaTime);
            if (net.Online)
            {
                _enemySendAccum += Time.deltaTime;
                if (_enemySendAccum >= 0.1f)
                {
                    _enemySendAccum = 0f;
                    net.PublishEnemyStates(World.GatherEnemyStates());
                }
            }
        }

        if (LocalPlayer != null) CheckProximity(net, LocalPlayer.transform.position);
    }

    // Взаимодействие локального игрока с монетами, порталами, НПС и звездой.
    private void CheckProximity(NetManager net, Vector3 pos)
    {
        foreach (KeyValuePair<int, Coin> kv in World.Coins)
        {
            if (kv.Value == null || !kv.Value.TryPickup(pos)) continue;
            // Сердце лечит того, кто его поднял, поэтому здоровье
            // прибавляем здесь, а не на хосте.
            if (NetManager.IsHeartId(kv.Key) && LocalPlayer != null) LocalPlayer.Heal(1);
            // Что такое звёзды, игра нигде не объясняла: об этом узнавали,
            // только упёршись в закрытый портал.
            if (NetManager.IsStarId(kv.Key))
            {
                Tutor.Show("star", "Звезда! Они открывают порталы в деревне — ищи по три в каждом краю.");
                Achievements.Grant("firststar");
            }
            else if (!NetManager.IsHeartId(kv.Key)) Achievements.Grant("firstcoin");
            net.RequestCollect(net.CurrentWorld, kv.Key);
        }

        for (int i = 0; i < World.Portals.Count; i++)
        {
            Portal p = World.Portals[i];
            if (p == null) continue;
            if (p.TryEnter(pos)) net.RequestPortal(p.Target);
            if (p.PendingHint != null)
            {
                if (_hud != null) _hud.ShowDialog(p.PendingHint);
                p.PendingHint = null;
            }
        }

        for (int i = 0; i < World.Checkpoints.Count; i++)
        {
            Checkpoint cp = World.Checkpoints[i];
            if (cp != null && cp.TryReach(pos) && LocalPlayer != null)
            {
                LocalPlayer.SetCheckpoint(cp.transform.position + new Vector3(0f, 0.4f, 0f));
                if (_hud != null) _hud.ShowDialog("Контрольная точка");
            }
        }

        for (int i = 0; i < World.Npcs.Count; i++)
        {
            Npc npc = World.Npcs[i];
            if (npc == null) continue;
            bool left;
            bool entered = npc.UpdateProximity(pos, out left);
            if (entered)
            {
                // Задание двигает только старейшина, остальные просто говорят.
                if (npc == World.Elder) net.RequestQuest();
                if (npc.IsShop && _shop != null)
                {
                    _shop.Open();
                    Tutor.Show("shop", "У торговки меняют монеты на сердца и новых героев.");
                }
                if (_hud != null) _hud.ShowDialog(npc.DialogText());
            }
            else if (left && _hud != null) _hud.HideDialog();
        }

        for (int i = 0; i < World.Chests.Count; i++)
        {
            Chest ch = World.Chests[i];
            if (ch == null || !ch.TryOpen(pos)) continue;
            // Материал получает тот, кто открыл — он личный. Монеты
            // начисляет хост: кошелёк в сетевой игре общий.
            net.AddRes(ch.ResKind, ch.ResAmount);
            Achievements.Grant("firstchest");
            if (_hud != null)
                _hud.ShowDialog("В сундуке: " + ch.Coins + " монет и " +
                                ch.ResAmount + " — " + Res.Name(ch.ResKind).ToLower());
            net.RequestCollect(net.CurrentWorld, ch.Id);
        }

        if (World.Star != null && World.Star.TryReach(pos)) net.RequestVictory();
    }

    // ---------- События сети ----------

    private void OnCoinRemoved(int world, int coinId)
    {
        if (World == null || NetManager.I == null || world != NetManager.I.CurrentWorld) return;
        World.RemoveCoin(coinId, true);
    }

    private void OnEnemyRemoved(int world, int enemyId)
    {
        if (World == null || NetManager.I == null || world != NetManager.I.CurrentWorld) return;
        World.RemoveEnemy(enemyId, true);
    }

    private void OnBrokenRemoved(int world, int propId)
    {
        if (World == null || NetManager.I == null || world != NetManager.I.CurrentWorld) return;
        World.RemoveBreakable(propId, true);
    }

    private void OnBlockPlaced(int world, int cell, int kind)
    {
        if (BlockField.I != null) BlockField.I.OnPlaced(world, cell, kind);
    }

    private void OnBlockRemoved(int world, int cell)
    {
        if (BlockField.I != null) BlockField.I.OnRemoved(world, cell);
    }

    private void OnEnemyStates(List<EnemyState> states)
    {
        if (World != null) World.ApplyEnemyStates(states);
    }

    private void OnQuestChanged(int stage)
    {
        if (World == null) return;
        for (int i = 0; i < World.Portals.Count; i++)
            if (World.Portals[i] != null) World.Portals[i].Refresh();
    }
}
