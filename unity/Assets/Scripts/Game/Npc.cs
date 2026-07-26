using UnityEngine;

// Старейшина-медведь: выдаёт задание и комментирует прогресс.
public class Npc : MonoBehaviour
{
    private bool _playerNear;

    public static Npc Spawn(Transform parent, Vector3 pos)
    {
        GameObject go = new GameObject("Elder");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Npc n = go.AddComponent<Npc>();
        n.Build();
        return n;
    }

    private void Build()
    {
        Material fur = Gfx.Mat(new Color(0.55f, 0.55f, 0.58f));
        Material furLight = Gfx.Mat(new Color(0.75f, 0.75f, 0.78f));
        Material dark = Gfx.Mat(new Color(0.12f, 0.1f, 0.1f));

        Gfx.Ball(transform, new Vector3(0f, 0.95f, 0f), new Vector3(1.35f, 1.6f, 1.2f), fur);
        Gfx.Ball(transform, new Vector3(0f, 0.9f, 0.4f), new Vector3(0.85f, 1.1f, 0.55f), furLight);
        Gfx.Ball(transform, new Vector3(0f, 1.95f, 0f), new Vector3(0.95f, 0.9f, 0.9f), fur);
        Gfx.Ball(transform, new Vector3(0f, 1.85f, 0.38f), new Vector3(0.45f, 0.36f, 0.34f), furLight);
        Gfx.Ball(transform, new Vector3(0f, 1.9f, 0.55f), new Vector3(0.14f, 0.12f, 0.1f), dark);
        Gfx.Ball(transform, new Vector3(-0.18f, 2.05f, 0.38f), new Vector3(0.11f, 0.12f, 0.08f), dark);
        Gfx.Ball(transform, new Vector3(0.18f, 2.05f, 0.38f), new Vector3(0.11f, 0.12f, 0.08f), dark);
        Gfx.Ball(transform, new Vector3(-0.32f, 2.35f, 0f), new Vector3(0.28f, 0.28f, 0.16f), fur);
        Gfx.Ball(transform, new Vector3(0.32f, 2.35f, 0f), new Vector3(0.28f, 0.28f, 0.16f), fur);
        Gfx.Ball(transform, new Vector3(0f, 1.62f, 0.42f), new Vector3(0.5f, 0.5f, 0.3f), furLight);

        Gfx.Cyl(transform, new Vector3(0.85f, 1.2f, 0.2f), new Vector3(0.12f, 1.2f, 0.12f),
            Gfx.Mat(new Color(0.4f, 0.26f, 0.13f)), false);

        Material orbMat = Gfx.MatFull(new Color(0.5f, 0.9f, 1f), 0.8f, 0.2f,
            new Color(0.35f, 0.7f, 1f), 0f, 0f);
        Gfx.Ball(transform, new Vector3(0.85f, 2.45f, 0.2f), new Vector3(0.28f, 0.28f, 0.28f), orbMat);
        Gfx.Glow(transform, new Vector3(0.85f, 2.45f, 0.2f), 1.4f, new Color(0.5f, 0.85f, 1f, 0.7f));

        WorldLabel.Attach(transform, "Старейшина", new Vector3(0f, 2.95f, 0f),
            new Color(1f, 0.95f, 0.6f), 22);
    }

    public string DialogText()
    {
        NetManager net = NetManager.I;
        if (net == null) return "";
        if (net.VictoryReached) return "Вы — настоящие герои! Вершина покорена!";
        if (net.QuestStage >= 2)
            return "Портал открыт! Доберитесь до золотой звезды на Снежной вершине!";
        if (net.QuestStage == 1)
            return "Соберите " + NetManager.QuestCoins + " монет (" + net.CoinsTotal + "/" +
                   NetManager.QuestCoins + ") — и я открою портал в Снежные вершины!";
        return "Приветствую, медвежата! У меня есть для вас задание...";
    }

    // Проверка близости игрока; возвращает true при первом входе в зону.
    public bool UpdateProximity(Vector3 playerPos, out bool leftZone)
    {
        Vector3 d = transform.position - playerPos;
        d.y = 0f;
        bool near = d.sqrMagnitude < 3.4f * 3.4f;
        leftZone = _playerNear && !near;
        bool entered = near && !_playerNear;
        _playerNear = near;
        return entered;
    }

    public bool PlayerNear { get { return _playerNear; } }
}
