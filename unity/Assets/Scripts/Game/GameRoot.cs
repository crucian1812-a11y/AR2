using UnityEngine;

// Сборка схватки: зал, два бойца, камера, пост-обработка, интерфейс, ИИ.
// Единственное место, где состояние Match превращается в картинку.
public class GameRoot : MonoBehaviour
{
    private const Side Player = Side.A;

    private Match _match;
    private Ai _ai;
    private FighterRig _a;
    private FighterRig _b;
    private CameraDirector _cam;

    private Pos _shownPos = (Pos)(-1);
    private Side _shownTop = (Side)(-1);
    private float _moveHold;      // сколько ещё играть клип перехода

    public static GameRoot Create()
    {
        GameObject go = new GameObject("GameRoot");
        return go.AddComponent<GameRoot>();
    }

    private void Awake()
    {
        int seed = System.Environment.TickCount;
        _match = new Match(seed);
        _ai = new Ai(_match, Side.B, seed + 7919, 0.55f);
        _match.OnMoveStart += PlayMove;

        Arena.Build(transform);
        PostFx.Build(transform);

        // Оба бойца стоят в одной точке: взаимное расположение целиком
        // лежит в анимации (см. tools/blender/README.md). Разносить их
        // ещё и здесь значило бы применить смещение дважды.
        _a = FighterRig.Create(transform, Side.A, Arena.GiBlue, Arena.RimBlue);
        _b = FighterRig.Create(transform, Side.B, Arena.GiRed, Arena.RimRed);

        _cam = CameraDirector.Create(transform, _match);
        Hud.Create(transform, _match, Player);

        ShowHold(true);
    }

    private void OnDestroy()
    {
        if (_match != null) _match.OnMoveStart -= PlayMove;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _match.Tick(dt);
        _ai.Tick(dt);

        if (_moveHold > 0f) _moveHold -= dt;

        // Клип удержания возвращается, только когда доиграл переход:
        // иначе бросок обрывался бы на середине.
        if (_moveHold <= 0f &&
            (_shownPos != _match.Position || _shownTop != _match.Top))
        {
            ShowHold(false);
        }

        UpdateSweat();
    }

    private void PlayMove(Move m, Side who)
    {
        // Роль в клипе — не «кто ходит», а кто сейчас сверху: клипы сняты
        // парой, и нижний должен играть свою половину того же приёма.
        bool aIsTop = _match.Top != Side.B;

        _a.Play(Res.MoveName(aIsTop, m.Clip), false, 0.14f);
        _b.Play(Res.MoveName(!aIsTop, m.Clip), false, 0.14f);

        _moveHold = m.Time;
        _cam.Kick(m.IsSubmission ? 0.35f : 0.18f);

        // Позиция сменится только после успеха, поэтому показ обновится
        // сам, когда доиграет переход.
        _shownPos = (Pos)(-1);
    }

    private void ShowHold(bool instant)
    {
        _shownPos = _match.Position;
        _shownTop = _match.Top;

        bool aIsTop = _match.Top != Side.B;
        float blend = instant ? 0.001f : 0.25f;

        _a.Play(Res.HoldName(aIsTop, _match.Position), true, blend);
        _b.Play(Res.HoldName(!aIsTop, _match.Position), true, blend);
    }

    // Пот растёт по мере усталости и не убывает: отдышаться можно, но
    // высохнуть посреди схватки — нет. Поэтому берётся минимум сил за
    // матч, а не текущее значение.
    private float _sweatA;
    private float _sweatB;

    private void UpdateSweat()
    {
        float wantA = 1f - _match.StaminaA / Match.MaxStamina;
        float wantB = 1f - _match.StaminaB / Match.MaxStamina;

        // Ещё немного от времени раунда: даже свежий боец к пятой минуте
        // мокрый.
        float clock = 1f - _match.Clock / Match.RoundTime;
        wantA = Mathf.Max(wantA, clock * 0.7f);
        wantB = Mathf.Max(wantB, clock * 0.7f);

        _sweatA = Mathf.Max(_sweatA, wantA);
        _sweatB = Mathf.Max(_sweatB, wantB);

        _a.SetSweat(_sweatA);
        _b.SetSweat(_sweatB);
    }
}
