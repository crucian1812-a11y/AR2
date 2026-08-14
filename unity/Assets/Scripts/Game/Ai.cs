using System.Collections.Generic;
using UnityEngine;

// Архетип соперника. Двадцать приёмов у всех одни и те же, но разные
// склонности гоняют схватку по разным ветвям графа — и один и тот же
// набор начинает ощущаться по-разному.
public enum Style
{
    Balanced,   // ровный
    Presser,    // давящий: рвётся проходить и держать сверху
    Guard,      // гардист: садится в гвардию, ищет свипты и сабмишны
    Hunter      // охотник за сабмишнами: лезет в приёмы при первой щели
}

// Соперник. Не «случайные кнопки»: он оценивает, насколько ход улучшает
// его положение, бережёт силы, сопротивляется чужим приёмам и борется в
// сабмишне.
//
// Задержка перед действием важнее, чем кажется. Мгновенная реакция
// читается как нечестность — игрок не успевает понять, что произошло.
public class Ai
{
    private readonly Match _m;
    private readonly Side _me;
    private readonly System.Random _rng;
    private readonly Style _style;

    private float _think;
    private float _react;              // отдельный таймер на сопротивление
    private readonly float _speed;
    private readonly float _aggression;
    private readonly float _grit;      // упорство в перетягивании

    public Style Style { get { return _style; } }

    public Ai(Match m, Side me, int seed, float difficulty, Style style = Style.Balanced)
    {
        _m = m;
        _me = me;
        _rng = new System.Random(seed);
        _style = style;

        _speed = Mathf.Lerp(0.65f, 1.35f, difficulty);
        _aggression = Mathf.Lerp(0.25f, 0.8f, difficulty);
        _grit = Mathf.Lerp(0.35f, 0.9f, difficulty);

        _think = Delay();
    }

    public void Tick(float dt)
    {
        if (_m.Finished) return;

        switch (_m.Now)
        {
            case Phase.Move:
                TickResist(dt);
                return;
            case Phase.Submission:
                TickStruggle(dt);
                return;
        }

        _think -= dt * _speed;
        if (_think > 0f) return;
        _think = Delay();

        if (TryUseGrips()) return;
        ChooseMove();
    }

    // Сопротивление чужому приёму. Тратить силы имеет смысл не всегда:
    // на дешёвый переход, который и так вряд ли пройдёт, лучше не тратить.
    private void TickResist(float dt)
    {
        if (_m.Mover == _me) return;

        _react -= dt;
        if (_react > 0f) return;
        _react = Mathf.Lerp(0.55f, 0.22f, _grit);

        Move m = _m.CurrentMove;
        float stamina = _m.Stamina(_me);
        if (stamina < 18f) return;

        // Сабмишну сопротивляемся почти всегда, переходу — по важности
        // позиции, которую можем потерять.
        float worth = m.IsSubmission ? 1f : Positions.Adv(m.To) - Positions.Adv(_m.Position);
        if (worth < 0.12f && _rng.NextDouble() > 0.35) return;

        _m.TryResist(_me);
    }

    private void TickStruggle(float dt)
    {
        _react -= dt;
        if (_react > 0f) return;
        // Чем упорнее соперник, тем чаще дожимает или рвётся.
        _react = Mathf.Lerp(0.42f, 0.16f, _grit);
        _m.TryStruggle(_me);
    }

    // Захваты: держать их выгодно всегда, но брать бесконечно нельзя.
    // Гардист цепляется за отвороты охотнее прочих.
    private bool TryUseGrips()
    {
        float mine = _m.Grips(_me);
        float theirs = _m.Grips(Match.Other(_me));
        float want = _style == Style.Guard ? 0.8f : 0.5f;

        if (theirs > mine && _rng.NextDouble() < 0.45)
            return _m.TryBreakGrip(_me);

        if (mine < Match.MaxGrips && _rng.NextDouble() < want)
            return _m.TryGrip(_me);

        return false;
    }

    private void ChooseMove()
    {
        List<Move> options = _m.Available(_me);
        if (options.Count == 0) return;

        Move best = default(Move);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        Side other = Match.Other(_me);
        float myStamina = _m.Stamina(_me);

        for (int i = 0; i < options.Count; i++)
        {
            Move m = options[i];
            if (myStamina < m.Stamina) continue;

            float chance = Transitions.Chance(m, myStamina, _m.Stamina(other));
            chance += _m.Grips(_me) * 0.07f - _m.Grips(other) * 0.06f;

            float gain = m.IsSubmission
                ? 1.4f * _aggression
                : Positions.Adv(m.To) - Positions.Adv(_m.Position);

            float score = gain * Mathf.Clamp01(chance);
            score += StyleBonus(m);

            // Беречь силы: тратить последнюю треть на дорогой приём почти
            // всегда хуже, чем отдышаться.
            float left = (myStamina - m.Stamina) / Match.MaxStamina;
            if (left < 0.25f) score -= 0.35f;

            score += (float)_rng.NextDouble() * 0.12f;

            if (score > bestScore)
            {
                bestScore = score;
                best = m;
                found = true;
            }
        }

        if (found && bestScore > 0.02f) _m.TryMove(_me, best);
    }

    // Склонности архетипа. Величины небольшие намеренно: стиль должен
    // смещать выбор, а не отменять здравый смысл — иначе гардист сядет
    // в гвардию из позиции «верхом».
    private float StyleBonus(Move m)
    {
        switch (_style)
        {
            case Style.Presser:
                if (m.ByTop && !m.IsSubmission) return 0.22f;
                if (m.To == Pos.SideControl || m.To == Pos.Mount) return 0.14f;
                return 0f;

            case Style.Guard:
                if (!m.ByTop) return 0.20f;
                if (m.To == Pos.ClosedGuard || m.To == Pos.OpenGuard) return 0.16f;
                return 0f;

            case Style.Hunter:
                if (m.IsSubmission) return 0.34f;
                if (m.To == Pos.BackControl) return 0.18f;
                return 0f;

            default:
                return 0f;
        }
    }

    public static Style RandomStyle(System.Random rng)
    {
        int k = rng.Next(4);
        return (Style)k;
    }

    public static string StyleName(Style s)
    {
        switch (s)
        {
            case Style.Presser: return "давящий";
            case Style.Guard: return "гардист";
            case Style.Hunter: return "охотник за приёмами";
            default: return "универсал";
        }
    }

    private float Delay()
    {
        return 0.9f + (float)_rng.NextDouble() * 1.4f;
    }
}
