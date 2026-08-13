using System.Collections.Generic;

// Граф позиций — ядро игры. Здесь нет ни моделей, ни анимаций: только
// правила борьбы. Всё остальное (капсулы на этапе серого бокса, бойцы с
// анимациями потом) читает состояние отсюда и лишь показывает его.
//
// Позиция описывает, кто сверху и насколько ему выгодно. Переход — это
// попытка сменить позицию: она стоит сил, занимает время и может не выйти.
public enum Pos
{
    Standing,        // стойка
    ClosedGuard,     // закрытая гвардия (снизу, ноги сомкнуты на поясе)
    OpenGuard,       // открытая гвардия
    HalfGuard,       // полугвардия
    SideControl,     // удержание сбоку
    Mount,           // верхом
    BackControl,     // спина
    TurtleDown,      // «черепаха» (защита на четвереньках)
    Submitted        // приём проведён — конец схватки
}

// Кто в позиции наверху. Позиция сама по себе несимметрична: в Mount один
// боец доминирует, а в Standing оба равны.
public enum Side { Neutral, A, B }

public static class Positions
{
    // Очки за позицию по правилам IBJJF — начисляются один раз за приход
    // в позицию, а не за удержание. Спорные значения намеренно упрощены:
    // это игра, а не судейский регламент.
    private static readonly Dictionary<Pos, int> ScoreFor = new Dictionary<Pos, int>
    {
        { Pos.Standing, 0 },
        { Pos.ClosedGuard, 0 },
        { Pos.OpenGuard, 0 },
        { Pos.HalfGuard, 0 },
        { Pos.TurtleDown, 0 },
        { Pos.SideControl, 3 },   // проход гвардии
        { Pos.Mount, 4 },
        { Pos.BackControl, 4 },
        { Pos.Submitted, 0 }
    };

    // Насколько позиция выгодна тому, кто наверху. Нужно ИИ, чтобы он
    // стремился улучшать положение, а не дёргался случайно.
    private static readonly Dictionary<Pos, float> Advantage = new Dictionary<Pos, float>
    {
        { Pos.Standing, 0.0f },
        { Pos.ClosedGuard, 0.15f },
        { Pos.OpenGuard, 0.2f },
        { Pos.HalfGuard, 0.35f },
        { Pos.TurtleDown, 0.45f },
        { Pos.SideControl, 0.6f },
        { Pos.Mount, 0.85f },
        { Pos.BackControl, 1.0f },
        { Pos.Submitted, 1.0f }
    };

    // Читаемое имя для HUD. Русские названия намеренно бытовые: «удержание
    // сбоку» понятнее новичку, чем «кесa-гатамэ» или «сайд-контроль».
    private static readonly Dictionary<Pos, string> Names = new Dictionary<Pos, string>
    {
        { Pos.Standing, "Стойка" },
        { Pos.ClosedGuard, "Закрытая гвардия" },
        { Pos.OpenGuard, "Открытая гвардия" },
        { Pos.HalfGuard, "Полугвардия" },
        { Pos.SideControl, "Удержание сбоку" },
        { Pos.Mount, "Верхом" },
        { Pos.BackControl, "Спина" },
        { Pos.TurtleDown, "Черепаха" },
        { Pos.Submitted, "Приём!" }
    };

    public static int Score(Pos p)
    {
        int v;
        return ScoreFor.TryGetValue(p, out v) ? v : 0;
    }

    public static float Adv(Pos p)
    {
        float v;
        return Advantage.TryGetValue(p, out v) ? v : 0f;
    }

    public static string Name(Pos p)
    {
        string v;
        return Names.TryGetValue(p, out v) ? v : p.ToString();
    }

    // В позициях без доминирования наверху никого нет, и «улучшать» её
    // обоим бойцам можно одинаково.
    public static bool IsNeutral(Pos p)
    {
        return p == Pos.Standing || p == Pos.OpenGuard;
    }
}
