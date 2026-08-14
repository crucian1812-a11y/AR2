using System.Collections.Generic;
using UnityEngine;

// Что делает жест в текущем положении.
//
// Жест отвечает намерению, а не приёму. «Давить» из стойки — это проход в
// ноги, из гвардии — проход гвардии, из удержания — выход верхом. Игрок
// думает категориями борьбы, а конкретный приём подбирает игра; ровно так
// же думает и настоящий борец, у которого нет меню.
//
// Каждому намерению отвечает лучший подходящий приём, а не первый
// попавшийся: если из позиции есть два способа давить, берётся тот, у
// которого выше ожидаемая отдача.
public static class Controls
{
    public enum Intent { None, Advance, Escape, Reverse, Submit, Grip }

    public static Intent Of(Gesture g)
    {
        switch (g)
        {
            case Gesture.SwipeForward: return Intent.Advance;
            case Gesture.SwipeBack: return Intent.Escape;
            case Gesture.SwipeLeft:
            case Gesture.SwipeRight: return Intent.Reverse;
            case Gesture.Hold: return Intent.Submit;
            case Gesture.Tap: return Intent.Grip;
            default: return Intent.None;
        }
    }

    /// Подходит ли приём под намерение.
    public static bool Matches(Match m, Side me, Move mv, Intent intent)
    {
        switch (intent)
        {
            case Intent.Submit:
                return mv.IsSubmission;

            case Intent.Advance:
                // Давить — значит улучшать своё положение, оставаясь
                // хозяином позиции.
                return !mv.IsSubmission &&
                       Positions.Adv(mv.To) > Positions.Adv(m.Position) + 0.01f;

            case Intent.Escape:
                // Уходить — значит выбираться туда, где сопернику хуже
                // контролировать: из-под удержания в полугвардию, из
                // черепахи в стойку.
                return !mv.IsSubmission && !mv.ByTop &&
                       Positions.Adv(mv.To) < Positions.Adv(m.Position) - 0.01f;

            case Intent.Reverse:
                // Переворот: ход снизу, после которого наверху оказываюсь я.
                return !mv.IsSubmission && !mv.ByTop &&
                       Positions.Adv(mv.To) >= Positions.Adv(m.Position) - 0.01f;

            default:
                return false;
        }
    }

    /// Лучший приём под намерение, или false если такого нет.
    public static bool Pick(Match m, Side me, Intent intent, out Move chosen)
    {
        chosen = default(Move);
        List<Move> options = m.Available(me);
        if (options.Count == 0) return false;

        Side other = Match.Other(me);
        float best = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < options.Count; i++)
        {
            Move mv = options[i];
            if (!Matches(m, me, mv, intent)) continue;
            if (m.Stamina(me) < mv.Stamina) continue;

            float chance = Transitions.Chance(mv, m.Stamina(me), m.Stamina(other));
            float gain = mv.IsSubmission
                ? 1.2f
                : Mathf.Abs(Positions.Adv(mv.To) - Positions.Adv(m.Position)) + 0.15f;

            float score = gain * chance;
            if (score > best)
            {
                best = score;
                chosen = mv;
                found = true;
            }
        }
        return found;
    }

    /// Выполняет жест. Возвращает подпись действия для интерфейса —
    /// пустую, если жест сейчас ничего не делает.
    public static string Apply(Match m, Side me, Gesture g)
    {
        if (m.Finished) return "";

        // В этих фазах жест не выбирает приём: делать можно ровно одно, и
        // любое движение пальца означает именно это.
        if (m.Now == Phase.Submission)
            return m.TryStruggle(me) ? (m.Mover == me ? "дожим" : "рывок") : "";

        if (m.Now == Phase.Move)
        {
            if (m.Mover == me) return "";
            return m.TryResist(me) ? "сопротивление" : "";
        }

        Intent intent = Of(g);

        if (intent == Intent.Grip)
        {
            // Тап берёт свой захват, а когда своих уже два — срывает чужой.
            if (m.Grips(me) < Match.MaxGrips && m.TryGrip(me)) return "захват";
            if (m.TryBreakGrip(me)) return "срыв захвата";
            return "";
        }

        Move chosen;
        if (!Pick(m, me, intent, out chosen)) return "";
        return m.TryMove(me, chosen) ? chosen.Name : "";
    }

    /// Что жест сделает прямо сейчас — для подсказки на экране.
    public static string Preview(Match m, Side me, Gesture g)
    {
        if (m.Finished) return "";
        if (m.Now == Phase.Submission) return m.Mover == me ? "дожать" : "вырваться";
        if (m.Now == Phase.Move) return m.Mover == me ? "" : "сопротивляться";

        Intent intent = Of(g);
        if (intent == Intent.Grip)
        {
            if (m.Grips(me) < Match.MaxGrips) return "взять захват";
            if (m.Grips(Match.Other(me)) > 0) return "сорвать захват";
            return "";
        }

        Move chosen;
        return Pick(m, me, intent, out chosen) ? chosen.Name : "";
    }
}
