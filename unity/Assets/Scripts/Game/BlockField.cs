using System.Collections.Generic;
using UnityEngine;

// Блоки, поставленные игроками. Живут отдельным полем поверх мира:
// сам мир строится кодом заново при каждом входе, а блоки приходят из
// сохранения и из сети.
//
// Сетка своя, с шагом NetManager.BlockSize — не воксельный мир, а
// именно «поставить кубик»: рельеф остаётся сеткой из шума, блок просто
// добавляет твёрдую коробку в свободной клетке.
public class BlockField : MonoBehaviour
{
    public static BlockField I;

    private readonly Dictionary<int, GameObject> _made = new Dictionary<int, GameObject>();
    private int _world;

    public static BlockField Create(Transform parent, int world)
    {
        GameObject go = new GameObject("Blocks");
        go.transform.SetParent(parent, false);
        BlockField f = go.AddComponent<BlockField>();
        f._world = world;
        I = f;
        f.RebuildAll();
        return f;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    private void RebuildAll()
    {
        NetManager net = NetManager.I;
        if (net == null) return;
        foreach (KeyValuePair<int, int> kv in net.BlocksOf(_world))
            Spawn(kv.Key, kv.Value);
    }

    public void OnPlaced(int world, int cell, int kind)
    {
        if (world != _world) return;
        Spawn(cell, kind);
    }

    public void OnRemoved(int world, int cell)
    {
        if (world != _world) return;
        GameObject go;
        if (!_made.TryGetValue(cell, out go)) return;
        _made.Remove(cell);
        if (go == null) return;
        ParticleFx.Burst(transform, go.transform.position, 12, Res.Tint(kindOf(go)), 4f);
        Object.Destroy(go);
    }

    private static int kindOf(GameObject go)
    {
        BlockTag t = go.GetComponent<BlockTag>();
        return t != null ? t.Kind : Res.Stone;
    }

    private void Spawn(int cell, int kind)
    {
        if (_made.ContainsKey(cell)) return;
        Vector3 pos = NetManager.CellCenter(cell);
        float s = NetManager.BlockSize;

        Color c = Res.Tint(kind);
        GameObject go = Gfx.Box(transform, pos, new Vector3(s, s, s),
            Gfx.MatFull(c, 0.06f, 0f, Color.black, 2.2f, 0.4f));
        go.name = "Block_" + cell;
        BlockTag tag = go.AddComponent<BlockTag>();
        tag.Cell = cell;
        tag.Kind = kind;
        _made[cell] = go;
    }

    // Занята ли клетка поставленным блоком.
    public bool Has(int cell)
    {
        NetManager net = NetManager.I;
        return net != null && net.BlocksOf(_world).ContainsKey(cell);
    }

    // Блок в клетке или null.
    public GameObject At(int cell)
    {
        GameObject go;
        return _made.TryGetValue(cell, out go) ? go : null;
    }
}

// Метка на кубе: по ней ломающий удар узнаёт, какую клетку освобождать.
public class BlockTag : MonoBehaviour
{
    public int Cell;
    public int Kind;
}
