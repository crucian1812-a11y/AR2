using System.Collections.Generic;

namespace Koenig
{
    public enum QuestState
    {
        Locked,  // город ещё впереди по пути
        Active,  // текущий город, точка не пройдена
        Done     // точка пройдена
    }

    // Одна запись журнала: точка на местности как задание.
    public class Quest
    {
        public Poi Poi;
        public QuestState State;
        public Quest(Poi p, QuestState s) { Poi = p; State = s; }

        public string Title { get { return Poi.Name; } }
        public string Task { get { return Poi.ChildTask; } }
        public Artifact Reward { get { return Poi.Gives; } }
        public bool HasAr { get { return Poi.ArTarget.Length > 0; } }
        public bool HasHomlin { get { return Poi.HomlinNote.Length > 0; } }
    }

    // Журнал квестов: точки, сгруппированные по городам в порядке пути,
    // с состоянием каждой. Чистая производная от KoenigContent и Journey —
    // своего состояния не держит, поэтому всегда согласован с прогрессом.
    public static class QuestLog
    {
        // Состояние точки: пройдена → Done; иначе город текущий или уже
        // пройденный → Active, будущий → Locked. Прошлые непройденные
        // точки остаются Active, чтобы к ним можно было вернуться, —
        // ребёнок мог проехать мимо и захотеть заглянуть позже.
        public static QuestState StateOf(Poi p)
        {
            if (Journey.IsDone(p.Id)) return QuestState.Done;
            int step = KoenigContent.CityStep(p.CityId);
            int cur = KoenigContent.CityStep(Journey.CurrentCity());
            return step <= cur ? QuestState.Active : QuestState.Locked;
        }

        // Все квесты в порядке пути.
        public static List<Quest> All()
        {
            List<Quest> list = new List<Quest>();
            string[] order = KoenigContent.JourneyOrder;
            for (int c = 0; c < order.Length; c++)
            {
                Poi[] pts = KoenigContent.Points;
                for (int i = 0; i < pts.Length; i++)
                    if (pts[i].CityId == order[c])
                        list.Add(new Quest(pts[i], StateOf(pts[i])));
            }
            return list;
        }

        // Квесты одного города.
        public static List<Quest> ForCity(string cityId)
        {
            List<Quest> list = new List<Quest>();
            Poi[] pts = KoenigContent.Points;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].CityId == cityId)
                    list.Add(new Quest(pts[i], StateOf(pts[i])));
            return list;
        }
    }
}
