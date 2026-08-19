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

    private Opponent _opponent;
    private float _endWait = -1f;
    private bool _reported;

    /// Схватка окончена: кто победил и с кем боролись. Дальше решает
    /// Bootstrap — показать итог и вернуться в меню.
    public event System.Action<Side> OnFinished;

    /// Победа приёмом, а не по очкам. Экран итога говорит об этом отдельно,
    /// и карьера считает такие победы отдельной строкой.
    public bool BySubmission { get { return _match != null && _match.Position == Pos.Submitted; } }
    public int ScoreA { get { return _match == null ? 0 : _match.ScoreA; } }
    public int ScoreB { get { return _match == null ? 0 : _match.ScoreB; } }
    public Opponent Rival { get { return _opponent; } }

    public static GameRoot Create(Opponent opponent)
    {
        GameObject go = new GameObject("GameRoot");
        GameRoot root = go.AddComponent<GameRoot>();
        root._opponent = opponent;
        return root;
    }

    private void Awake()
    {
        int seed = System.Environment.TickCount;
        _match = new Match(seed, Career.Edge);

        // Соперник приходит из карьеры: его склонность и класс заданы
        // местом в списке, а не случайным броском. Один и тот же «третий
        // фиолетовый» ведёт схватку одинаково, пока его не пройдёшь.
        _ai = new Ai(_match, Side.B, seed + 7919, _opponent.Difficulty, _opponent.Style);
        _match.OnMoveStart += PlayMove;
        _match.OnEvent += Announce;
        _match.OnGrip += OnGrip;
        _match.OnStruggle += OnStruggle;

        Snd.Play("bell", 0.7f);
        Snd.Play("whistle", 0.35f);

        // Зал и пост-обработка живут дольше схватки — их строит Bootstrap
        // один раз. Пересобирать зал между схватками незачем: он не
        // меняется, а толпа в нём стоит недёшево.

        // Оба бойца стоят в одной точке: взаимное расположение целиком
        // лежит в анимации (см. tools/blender/README.md). Разносить их
        // ещё и здесь значило бы применить смещение дважды.
        _a = FighterRig.Create(transform, Side.A, Arena.GiBlue, Arena.RimBlue,
                               Career.Belt, Career.Stripes, Career.PlayerSkin, 1f);
        _b = FighterRig.Create(transform, Side.B, Arena.GiRed, Arena.RimRed,
                               _opponent.Belt, _opponent.Stripes,
                               _opponent.Skin, _opponent.Build);

        _cam = CameraDirector.Create(transform, _match);
        Hud.Create(transform, _match, Player, _opponent.Name);

        ShowHold(true);
        Debug.Log("Соперник: " + _opponent.Name + ", " + Ai.StyleName(_opponent.Style));
    }

    private void OnDestroy()
    {
        if (_match != null)
        {
            _match.OnMoveStart -= PlayMove;
            _match.OnEvent -= Announce;
            _match.OnGrip -= OnGrip;
            _match.OnStruggle -= OnStruggle;
        }
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
        UpdateAudio(dt);
        UpdateEnding();
    }

    // Пауза между концом схватки и экраном итога. Она нужна: на сдаче
    // камера подъезжает вплотную и время замедляется, и обрывать этот
    // показ табличкой — значит выбросить лучший кадр в игре.
    private void UpdateEnding()
    {
        if (!_match.Finished || _reported) return;

        if (_endWait < 0f) _endWait = 3.2f;

        // Время на добивании замедлено, поэтому отсчёт идёт по
        // неотмасштабированному: иначе пауза растянулась бы втрое.
        _endWait -= Time.unscaledDeltaTime;
        if (_endWait > 0f) return;

        _reported = true;
        if (OnFinished != null) OnFinished(_match.Winner);
    }

    private float _breathTimer = 2f;

    // Дыхание и зал. Оба зависят от состояния схватки, а не идут фоном:
    // зал оживает, когда кто-то доминирует, и затихает в равной позиции —
    // ровно так же, как настоящий.
    private void UpdateAudio(float dt)
    {
        float dominance = Positions.Adv(_match.Position);
        bool sub = _match.Now == Phase.Submission;

        // В сабмишне зал встаёт: это громче любой смены позиции.
        Snd.SetCrowd(sub ? 1f : dominance * 0.7f + (_match.Busy ? 0.3f : 0f));
        Snd.SetTension(_match.Finished ? 0f : (sub ? 1f : dominance));

        // Чем меньше сил, тем чаще и тяжелее дыхание.
        float lowest = Mathf.Min(_match.StaminaA, _match.StaminaB) / Match.MaxStamina;
        _breathTimer -= dt;
        if (_breathTimer <= 0f && !_match.Finished)
        {
            _breathTimer = Mathf.Lerp(1.1f, 3.2f, lowest);
            Snd.Play("breath", Mathf.Lerp(0.55f, 0.20f, lowest),
                     Mathf.Lerp(0.86f, 1.06f, lowest));
        }
    }

    private void PlayMove(Move m, Side who)
    {
        // Роль в парном клипе задаёт сам приём, а не текущее «кто сверху».
        // Приём с ByTop выполняет верхний, без него — нижний, и это верно
        // даже в стойке, где наверху формально никого нет. Если брать роль
        // из Match.Top, то бросок, начатый вторым бойцом, проигрывался бы
        // у него как половина защищающегося.
        bool moverIsTopRole = m.ByTop;
        bool aIsTopRole = who == Side.A ? moverIsTopRole : !moverIsTopRole;

        _a.Play(Res.MoveName(aIsTopRole, m.Clip), false, 0.14f);
        _b.Play(Res.MoveName(!aIsTopRole, m.Clip), false, 0.14f);

        _moveHold = m.Time;
        _cam.Kick(m.IsSubmission ? 0.35f : 0.18f);

        // Звук приёма: рывок ткани в начале, шорох по ходу. Захват и
        // возня слышны раньше, чем виден результат, — так и в зале.
        Snd.Play("grip", 0.55f, Random.Range(0.92f, 1.10f));
        Snd.Play("cloth", 0.40f, Random.Range(0.85f, 1.15f));

        // Позиция сменится только после успеха, поэтому показ обновится
        // сам, когда доиграет переход.
        _shownPos = (Pos)(-1);
    }

    private void OnGrip(Side who)
    {
        Snd.Play("grip", 0.45f, Random.Range(0.95f, 1.15f));
    }

    // Каждый рывок в перетягивании слышен: без звука полоса на экране
    // остаётся абстракцией, а с ним чувствуется борьба.
    private void OnStruggle(Side who, bool attacker)
    {
        Snd.Play(attacker ? "grip" : "cloth", 0.5f, Random.Range(0.9f, 1.2f));
        if (!attacker) _cam.Kick(0.08f);
    }

    // Итог приёма озвучивается отдельно от его начала: между попыткой и
    // результатом проходит секунда с лишним, и это разные события.
    private void Announce(string text)
    {
        if (_match.Position == Pos.Submitted)
        {
            Snd.Play("tap", 0.9f);
            Snd.Play("roar", 0.85f);
            Snd.Play("bell", 0.6f);
            return;
        }

        if (_match.Finished)
        {
            Snd.Play("bell", 0.75f);
            Snd.Play("roar", 0.6f);
            return;
        }

        if (text != null && text.EndsWith("не вышло"))
        {
            Snd.Play("cloth", 0.35f, 0.8f);
            return;
        }

        // Смена позиции — это падение тела на татами.
        Snd.Play("thud", 0.7f, Random.Range(0.9f, 1.1f));
        Snd.Play("cloth", 0.5f);
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
