using System.Collections.Generic;
using UnityEngine;

// Соперник. Не «случайные кнопки»: он оценивает, насколько ход улучшает
// его положение, и не тратит последние силы впустую.
//
// Задержка перед ходом важнее, чем кажется. Мгновенная реакция читается
// как нечестность — игрок не успевает понять, что произошло.
public class Ai
{
    private readonly Match _m;
    private readonly Side _me;
    private readonly System.Random _rng;

    private float _think;
    private readonly float _speed;      // 0.6 медленный … 1.4 быстрый
    private readonly float _aggression; // склонность лезть в сабмишн

    public Ai(Match m, Side me, int seed, float difficulty)
    {
        _m = m;
        _me = me;
        _rng = new System.Random(seed);
        _speed = Mathf.Lerp(0.65f, 1.35f, difficulty);
        _aggression = Mathf.Lerp(0.25f, 0.8f, difficulty);
        _think = Delay();
    }

    public void Tick(float dt)
    {
        if (_m.Finished) return;
        if (_m.Busy) return;

        _think -= dt * _speed;
        if (_think > 0f) return;
        _think = Delay();

        List<Move> options = _m.Available(_me);
        if (options.Count == 0) return;

        Move best = default(Move);
        float bestScore = float.NegativeInfinity;
        bool found = false;

        Side other = _me == Side.A ? Side.B : Side.A;
        float myStamina = _m.Stamina(_me);

        for (int i = 0; i < options.Count; i++)
        {
            Move m = options[i];
            if (myStamina < m.Stamina) continue;

            float chance = Transitions.Chance(m, myStamina, _m.Stamina(other));

            // Ожидаемая выгода: насколько лучше станет позиция, с поправкой
            // на шанс. Сабмишн оценивается отдельно — он заканчивает бой.
            float gain = m.IsSubmission
                ? 1.4f * _aggression
                : Positions.Adv(m.To) - Positions.Adv(_m.Position);

            float score = gain * chance;

            // Беречь силы: тратить последнюю треть на дорогой приём
            // почти всегда хуже, чем отдышаться.
            float left = (myStamina - m.Stamina) / Match.MaxStamina;
            if (left < 0.25f) score -= 0.35f;

            // Немного шума, иначе соперник предсказуем до зевоты.
            score += (float)_rng.NextDouble() * 0.12f;

            if (score > bestScore)
            {
                bestScore = score;
                best = m;
                found = true;
            }
        }

        // Отрицательная оценка у всех вариантов означает «лучше подождать
        // и восстановиться» — это осмысленный ход, а не бездействие.
        if (found && bestScore > 0.02f) _m.TryMove(_me, best);
    }

    private float Delay()
    {
        return 0.9f + (float)_rng.NextDouble() * 1.4f;
    }
}
