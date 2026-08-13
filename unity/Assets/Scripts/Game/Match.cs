using System;
using System.Collections.Generic;
using UnityEngine;

// Состояние схватки: позиция, кто наверху, силы, очки, часы.
// Модели и анимации сюда не заглядывают — наоборот, они читают состояние.
// Благодаря этому ядро проверяется без графики (и уже играется на капсулах).
public class Match
{
    public const float RoundTime = 300f;   // 5 минут, как в белом поясе
    public const float MaxStamina = 100f;

    public Pos Position { get; private set; }
    public Side Top { get; private set; }

    public float StaminaA { get; private set; }
    public float StaminaB { get; private set; }
    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }

    public float Clock { get; private set; }
    public bool Finished { get; private set; }
    public Side Winner { get; private set; }
    public string LastEvent { get; private set; }

    // Пока идёт приём, оба бойца заняты: новых попыток не принимаем.
    public bool Busy { get { return _busy > 0f; } }
    public float BusyProgress { get { return _moveTime <= 0f ? 1f : 1f - _busy / _moveTime; } }
    public Move CurrentMove { get { return _current; } }

    private float _busy;
    private float _moveTime;
    private Move _current;
    private Side _mover;
    private bool _currentValid;

    // Позиции, за которые очки уже начислены: по правилам они даются
    // один раз, иначе качели «верхом → сбоку → верхом» дают бесконечно.
    private readonly HashSet<Pos> _scoredA = new HashSet<Pos>();
    private readonly HashSet<Pos> _scoredB = new HashSet<Pos>();

    private readonly System.Random _rng;

    public event Action<string> OnEvent;

    public Match(int seed)
    {
        _rng = new System.Random(seed);
        Position = Pos.Standing;
        Top = Side.Neutral;
        StaminaA = MaxStamina;
        StaminaB = MaxStamina;
        Clock = RoundTime;
        LastEvent = "Схватка началась";
    }

    public bool AmTop(Side who)
    {
        // В нейтральной позиции наверху нет никого, но ходы «сверху»
        // должны быть доступны обоим — иначе из стойки нельзя войти в ноги.
        if (Positions.IsNeutral(Position)) return true;
        return Top == who;
    }

    public float Stamina(Side who)
    {
        return who == Side.A ? StaminaA : StaminaB;
    }

    public List<Move> Available(Side who)
    {
        if (Finished || Busy) return new List<Move>();
        return Transitions.For(Position, AmTop(who));
    }

    // Попытка провести приём. Возвращает false, если ход сейчас невозможен —
    // не хватает сил, идёт другой приём или схватка кончилась.
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
        return true;
    }

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

        if (_busy > 0f)
        {
            _busy -= dt;
            if (_busy <= 0f)
            {
                _busy = 0f;
                if (_currentValid) Resolve();
            }
            return;
        }

        Recover(dt);
    }

    // Приём доводится до конца именно здесь, а не в момент нажатия:
    // пока идёт время приёма, соперник видит, что происходит, — и в
    // следующих этапах сможет вмешаться.
    private void Resolve()
    {
        _currentValid = false;
        Side other = _mover == Side.A ? Side.B : Side.A;

        float chance = Transitions.Chance(_current, Stamina(_mover), Stamina(other));
        bool ok = _rng.NextDouble() < chance;

        if (!ok)
        {
            // Неудача тоже стоит сил защищающемуся: он сопротивлялся.
            Spend(other, _current.Stamina * 0.4f);
            Say(_current.Name + " — не вышло");
            return;
        }

        if (_current.IsSubmission)
        {
            Finished = true;
            Winner = _mover;
            Position = Pos.Submitted;
            Say(_current.Name + " — чисто! Победа " + (_mover == Side.A ? "синего" : "красного"));
            return;
        }

        Position = _current.To;

        // Кто наверху после перехода. Свипт и уходы снизу переворачивают
        // расклад: тот, кто был внизу, оказывается сверху.
        if (Positions.IsNeutral(Position)) Top = Side.Neutral;
        else if (_current.ByTop) Top = _mover;
        else Top = _mover;   // ход снизу удался — инициатор занимает верх

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

    // Силы восстанавливаются, но у того, кто внизу, — заметно медленнее:
    // под чужим весом не отдохнёшь. Это и делает позицию ценной сама по себе.
    private void Recover(float dt)
    {
        float baseRate = 6.5f;
        bool neutral = Positions.IsNeutral(Position);

        float rateA = baseRate * (neutral || Top == Side.A ? 1f : 0.45f);
        float rateB = baseRate * (neutral || Top == Side.B ? 1f : 0.45f);

        // Чем хуже позиция, тем дороже в ней просто находиться.
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
