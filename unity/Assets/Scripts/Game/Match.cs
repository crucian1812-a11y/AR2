using System;
using System.Collections.Generic;
using UnityEngine;

// Состояние схватки: позиция, кто наверху, силы, захваты, очки, часы.
// Модели и анимации сюда не заглядывают — наоборот, они читают состояние.
//
// Схватка живёт в одной из трёх фаз:
//
//   Neutral    — никто ничего не делает, идёт возня и восстановление
//   Move       — кто-то проводит приём, второй может сопротивляться
//   Submission — приём захвачен, идёт перетягивание до сдачи или выхода
//
// Фаза Move существует именно ради сопротивления. Мгновенный бросок кубика
// в момент нажатия сделал бы соперника зрителем; здесь у него есть секунда
// с лишним, чтобы потратить силы и сбить чужой шанс.
public enum Phase { Neutral, Move, Submission }

public class Match
{
    public const float RoundTime = 300f;   // 5 минут, как в белом поясе
    public const float MaxStamina = 100f;
    public const int MaxGrips = 2;

    public Pos Position { get; private set; }
    public Side Top { get; private set; }
    public Phase Now { get; private set; }

    public float StaminaA { get; private set; }
    public float StaminaB { get; private set; }
    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }

    // Захваты за кимоно. Держать захват — значит вести схватку: свои шансы
    // выше, чужие ниже. Это и делает ги не декорацией, а частью правил.
    public int GripsA { get; private set; }
    public int GripsB { get; private set; }

    public float Clock { get; private set; }
    public bool Finished { get; private set; }
    public Side Winner { get; private set; }
    public string LastEvent { get; private set; }

    public bool Busy { get { return Now != Phase.Neutral; } }
    public float BusyProgress { get { return _moveTime <= 0f ? 1f : 1f - _busy / _moveTime; } }
    public Move CurrentMove { get { return _current; } }
    public Side Mover { get { return _mover; } }

    // Перетягивание в сабмишне: 0 — соперник вырвался, 1 — сдача.
    public float Lock { get; private set; }
    public float Resistance { get { return _resist; } }

    private float _busy;
    private float _moveTime;
    private Move _current;
    private Side _mover;
    private bool _currentValid;
    private float _resist;

    private readonly HashSet<Pos> _scoredA = new HashSet<Pos>();
    private readonly HashSet<Pos> _scoredB = new HashSet<Pos>();

    private readonly System.Random _rng;

    public event Action<string> OnEvent;
    public event Action<Move, Side> OnMoveStart;
    public event Action<Side> OnGrip;
    public event Action<Side, bool> OnStruggle;   // кто и «дожал ли» (иначе вырвался)

    public Match(int seed)
    {
        _rng = new System.Random(seed);
        Position = Pos.Standing;
        Top = Side.Neutral;
        Now = Phase.Neutral;
        StaminaA = MaxStamina;
        StaminaB = MaxStamina;
        Clock = RoundTime;
        LastEvent = "Схватка началась";
    }

    public static Side Other(Side s) { return s == Side.A ? Side.B : Side.A; }

    public bool AmTop(Side who)
    {
        if (Positions.IsNeutral(Position)) return true;
        return Top == who;
    }

    public float Stamina(Side who)
    {
        return who == Side.A ? StaminaA : StaminaB;
    }

    public int Grips(Side who)
    {
        return who == Side.A ? GripsA : GripsB;
    }

    public List<Move> Available(Side who)
    {
        if (Finished || Busy) return new List<Move>();
        return Transitions.For(Position, AmTop(who));
    }

    // ------------------------------------------------------------- ходы

    public bool TryMove(Side who, Move m)
    {
        if (Finished || Busy) return false;
        if (m.From != Position) return false;
        if (Stamina(who) < m.Stamina) return false;

        Spend(who, m.Stamina);

        _current = m;
        _mover = who;
        _busy = m.Time;
        _moveTime = m.Time;
        _currentValid = true;
        _resist = 0f;
        Now = Phase.Move;

        if (OnMoveStart != null) OnMoveStart(m, who);
        return true;
    }

    /// Взять захват за кимоно. Дёшево, но занимает ход и время.
    public bool TryGrip(Side who)
    {
        if (Finished || Busy) return false;
        if (Grips(who) >= MaxGrips) return false;
        if (Stamina(who) < 6f) return false;

        Spend(who, 6f);
        if (who == Side.A) GripsA++; else GripsB++;

        // Захват сам по себе занимает мгновение, но открывает соперника:
        // пока держишь, у него на один захват меньше возможностей сорвать.
        Say(who == Side.A ? "Синий взял захват" : "Красный взял захват");
        if (OnGrip != null) OnGrip(who);
        return true;
    }

    /// Сорвать чужой захват. Стоит дороже, чем взять свой.
    public bool TryBreakGrip(Side who)
    {
        if (Finished || Busy) return false;
        Side other = Other(who);
        if (Grips(other) <= 0) return false;
        if (Stamina(who) < 10f) return false;

        Spend(who, 10f);
        if (other == Side.A) GripsA--; else GripsB--;
        Say(who == Side.A ? "Синий сорвал захват" : "Красный сорвал захват");
        return true;
    }

    /// Сопротивление приёму, пока он идёт. Каждое нажатие стоит сил и
    /// снижает чужой шанс — но с убывающей отдачей, иначе достаточно
    /// было бы долбить одну кнопку.
    public bool TryResist(Side who)
    {
        if (Now != Phase.Move) return false;
        if (who == _mover) return false;
        if (Stamina(who) < 4f) return false;

        Spend(who, 4f);
        _resist += (1f - _resist) * 0.28f;
        return true;
    }

    /// Дожать приём (атакующий) или вырваться (защищающийся).
    public bool TryStruggle(Side who)
    {
        if (Now != Phase.Submission) return false;
        if (Stamina(who) < 5f) return false;

        Spend(who, 5f);
        bool attacker = who == _mover;

        // Атакующему дожимать легче, чем защищающемуся вырываться: в этом
        // и состоит опасность позиции. Но защищающийся не обречён —
        // у свежего шанс почти равный.
        float power = attacker ? 0.055f : -0.048f;
        power *= 0.6f + 0.4f * (Stamina(who) / MaxStamina);

        Lock = Mathf.Clamp01(Lock + power);
        if (OnStruggle != null) OnStruggle(who, attacker);
        return true;
    }

    // ------------------------------------------------------------- такт

    public void Tick(float dt)
    {
        if (Finished) return;

        Clock -= dt;
        if (Clock <= 0f)
        {
            Clock = 0f;
            FinishByPoints();
            return;
        }

        switch (Now)
        {
            case Phase.Move:
                _busy -= dt;
                if (_busy <= 0f)
                {
                    _busy = 0f;
                    if (_currentValid) Resolve();
                }
                break;

            case Phase.Submission:
                TickSubmission(dt);
                break;

            default:
                Recover(dt);
                break;
        }
    }

    private void TickSubmission(float dt)
    {
        // Захват затягивается сам по себе: если защищающийся ничего не
        // делает, он проигрывает. Бездействие обязано наказываться,
        // иначе перетягивание превращается в ожидание.
        Lock = Mathf.Clamp01(Lock + dt * 0.085f);

        // Оба горят втрое быстрее обычного: это самая тяжёлая работа в
        // схватке.
        Spend(_mover, dt * 3.4f);
        Spend(Other(_mover), dt * 4.6f);

        if (Lock >= 1f)
        {
            Finished = true;
            Winner = _mover;
            Position = Pos.Submitted;
            Now = Phase.Neutral;
            Say(_current.Name + " — сдача! Побеждает " +
                (_mover == Side.A ? "синий" : "красный"));
            return;
        }

        if (Lock <= 0f)
        {
            Now = Phase.Neutral;
            // Вырвался — но приём стоил обоим, и позиция остаётся прежней.
            Say("Вырвался из приёма!");
        }
    }

    private void Resolve()
    {
        _currentValid = false;
        Now = Phase.Neutral;
        Side other = Other(_mover);

        float chance = Transitions.Chance(_current, Stamina(_mover), Stamina(other));

        // Захваты и сопротивление сдвигают шанс. Захват соперника мешает
        // не меньше, чем собственный помогает.
        chance += Grips(_mover) * 0.07f - Grips(other) * 0.06f;
        chance -= _resist * 0.42f;
        chance = Mathf.Clamp(chance, 0.04f, 0.95f);

        bool ok = _rng.NextDouble() < chance;

        if (!ok)
        {
            Spend(other, _current.Stamina * 0.25f);
            Say(_current.Name + " — не вышло");
            return;
        }

        if (_current.IsSubmission)
        {
            // Приём не заканчивает схватку сразу: он захвачен, дальше
            // перетягивание. Кульминация вида спорта не должна решаться
            // одним броском кубика.
            Now = Phase.Submission;
            Lock = 0.42f;
            Say(_current.Name + " — захвачен!");
            return;
        }

        Position = _current.To;

        if (Positions.IsNeutral(Position)) Top = Side.Neutral;
        else Top = _mover;

        // Смена позиции рвёт захваты: держаться было не за что.
        GripsA = 0;
        GripsB = 0;

        Award(_mover, Position);
        Say(_current.Name);
    }

    private void Award(Side who, Pos pos)
    {
        int pts = Positions.Score(pos);
        if (pts <= 0) return;

        HashSet<Pos> scored = who == Side.A ? _scoredA : _scoredB;
        if (!scored.Add(pos)) return;

        if (who == Side.A) ScoreA += pts; else ScoreB += pts;
    }

    private void Recover(float dt)
    {
        float baseRate = 6.5f;
        bool neutral = Positions.IsNeutral(Position);

        float rateA = baseRate * (neutral || Top == Side.A ? 1f : 0.45f);
        float rateB = baseRate * (neutral || Top == Side.B ? 1f : 0.45f);

        float pressure = Positions.Adv(Position) * 3.2f;
        if (!neutral)
        {
            if (Top == Side.A) rateB -= pressure; else rateA -= pressure;
        }

        StaminaA = Mathf.Clamp(StaminaA + rateA * dt, 0f, MaxStamina);
        StaminaB = Mathf.Clamp(StaminaB + rateB * dt, 0f, MaxStamina);
    }

    private void Spend(Side who, float amount)
    {
        if (who == Side.A) StaminaA = Mathf.Max(0f, StaminaA - amount);
        else StaminaB = Mathf.Max(0f, StaminaB - amount);
    }

    private void FinishByPoints()
    {
        Finished = true;
        Now = Phase.Neutral;

        if (ScoreA > ScoreB) Winner = Side.A;
        else if (ScoreB > ScoreA) Winner = Side.B;
        else Winner = Side.Neutral;

        Say(Winner == Side.Neutral
            ? "Время! Ничья по очкам"
            : "Время! Побеждает " + (Winner == Side.A ? "синий" : "красный"));
    }

    private void Say(string text)
    {
        LastEvent = text;
        if (OnEvent != null) OnEvent(text);
    }
}
