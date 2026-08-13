using System.Collections.Generic;

// Рёбра графа позиций. Каждое ребро — это приём: откуда, куда, сколько
// стоит сил, сколько занимает времени и насколько вероятно проходит.
//
// Развитие идёт именно отсюда: чтобы добавить приём, нужна одна строчка в
// таблице, а не новый класс. Анимация к приёму привязывается по полю Clip,
// и пока её нет, боец просто «телепортируется» в новую позицию — на этапе
// серого бокса это нормально и даже удобно.
public struct Move
{
    public string Name;      // как показывать игроку
    public Pos From;
    public Pos To;
    public bool ByTop;       // выполняет тот, кто сверху (иначе — снизу)
    public float Stamina;    // сколько сил стоит попытка
    public float Time;       // сколько секунд занимает
    public float BaseChance; // базовая вероятность успеха, 0..1
    public bool IsSubmission;// приводит к сдаче, а не к смене позиции
    public string Clip;      // имя анимационного клипа

    public Move(string name, Pos from, Pos to, bool byTop,
                float stamina, float time, float chance,
                string clip, bool submission = false)
    {
        Name = name;
        From = from;
        To = to;
        ByTop = byTop;
        Stamina = stamina;
        Time = time;
        BaseChance = chance;
        Clip = clip;
        IsSubmission = submission;
    }
}

public static class Transitions
{
    // Порядок внутри позиции важен: ИИ и панель управления берут приёмы
    // в этом порядке, поэтому сначала идут более «дешёвые» и безопасные.
    public static readonly Move[] All =
    {
        // --- из стойки ---
        new Move("Проход в ноги", Pos.Standing, Pos.SideControl, true,
                 22f, 1.1f, 0.42f, "Takedown_Double"),
        new Move("Бросок через бедро", Pos.Standing, Pos.SideControl, true,
                 26f, 1.2f, 0.36f, "Takedown_Hip"),
        new Move("Сесть в гвардию", Pos.Standing, Pos.ClosedGuard, false,
                 8f, 0.8f, 0.95f, "PullGuard"),

        // --- сверху: улучшение позиции ---
        new Move("Проход гвардии", Pos.ClosedGuard, Pos.HalfGuard, true,
                 18f, 1.3f, 0.38f, "Pass_Closed"),
        new Move("Проход в удержание", Pos.HalfGuard, Pos.SideControl, true,
                 16f, 1.2f, 0.45f, "Pass_Half"),
        new Move("Выход верхом", Pos.SideControl, Pos.Mount, true,
                 15f, 1.0f, 0.5f, "Mount_Transition"),
        new Move("Взять спину", Pos.TurtleDown, Pos.BackControl, true,
                 14f, 1.0f, 0.55f, "TakeBack"),
        new Move("Взять спину из верхом", Pos.Mount, Pos.BackControl, true,
                 16f, 1.1f, 0.4f, "TakeBack_Mount"),

        // --- снизу: защита и возврат ---
        new Move("Свипт", Pos.ClosedGuard, Pos.Mount, false,
                 20f, 1.1f, 0.32f, "Sweep_Scissor"),
        new Move("Вернуть гвардию", Pos.HalfGuard, Pos.ClosedGuard, false,
                 14f, 1.0f, 0.45f, "Recover_Guard"),
        new Move("Уйти из удержания", Pos.SideControl, Pos.HalfGuard, false,
                 18f, 1.2f, 0.35f, "Escape_Side"),
        new Move("Уйти из-под верхом", Pos.Mount, Pos.HalfGuard, false,
                 24f, 1.4f, 0.28f, "Escape_Mount"),
        new Move("Уйти со спины", Pos.BackControl, Pos.TurtleDown, false,
                 22f, 1.3f, 0.3f, "Escape_Back"),
        new Move("Встать", Pos.TurtleDown, Pos.Standing, false,
                 16f, 1.0f, 0.4f, "StandUp"),
        new Move("Встать из гвардии", Pos.OpenGuard, Pos.Standing, false,
                 14f, 0.9f, 0.5f, "StandUp_Guard"),

        // --- сабмишны ---
        // Вероятность здесь — это шанс ЗАХВАТИТЬ приём. Дожать его —
        // отдельная мини-игра, она в SubmissionAttempt.
        new Move("Удушение сзади", Pos.BackControl, Pos.Submitted, true,
                 20f, 1.2f, 0.55f, "Sub_RNC", true),
        new Move("Рычаг локтя сверху", Pos.Mount, Pos.Submitted, true,
                 22f, 1.3f, 0.4f, "Sub_Armbar_Mount", true),
        new Move("Рычаг локтя из гвардии", Pos.ClosedGuard, Pos.Submitted, false,
                 22f, 1.3f, 0.35f, "Sub_Armbar_Guard", true),
        new Move("Треугольник", Pos.ClosedGuard, Pos.Submitted, false,
                 24f, 1.4f, 0.3f, "Sub_Triangle", true),
        new Move("Кимура из удержания", Pos.SideControl, Pos.Submitted, true,
                 20f, 1.2f, 0.38f, "Sub_Kimura", true)
    };

    // Приёмы, доступные бойцу прямо сейчас. `amTop` — я ли наверху.
    // В нейтральных позициях наверху формально никого нет, и доступны
    // ходы обеих сторон: из стойки любой может и войти в ноги, и сесть.
    public static List<Move> For(Pos pos, bool amTop)
    {
        List<Move> result = new List<Move>();
        bool neutral = Positions.IsNeutral(pos);

        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].From != pos) continue;
            if (!neutral && All[i].ByTop != amTop) continue;
            result.Add(All[i]);
        }
        return result;
    }

    // Итоговый шанс с поправкой на усталость: вымотанный боец проводит
    // приёмы хуже, а сопротивляется слабее. Именно это делает стамину
    // ресурсом, а не украшением.
    public static float Chance(Move m, float attackerStamina, float defenderStamina)
    {
        float atk = Clamp01(attackerStamina / 100f);
        float def = Clamp01(defenderStamina / 100f);

        // Разница сил сдвигает шанс не более чем на треть в каждую сторону:
        // полностью свежий боец не должен проходить приём автоматически.
        float delta = (atk - def) * 0.33f;
        return Clamp(m.BaseChance + delta, 0.05f, 0.95f);
    }

    private static float Clamp01(float v)
    {
        return v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    private static float Clamp(float v, float lo, float hi)
    {
        return v < lo ? lo : (v > hi ? hi : v);
    }
}
