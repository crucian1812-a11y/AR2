using System.Collections.Generic;

namespace Koenig
{
    // Весь контент путешествия одним местом: земли и мосты Кёнигсберга,
    // четыре реальных города, точки на местности, артефакты и квесты.
    // Отделено от механик специально — это «сценарий», который правится
    // без оглядки на код.
    //
    // Порядок пути: Зеленоградск (начало) → Светлогорск → Балтийск →
    // Калининград (финал). В первых трёх городах ребёнок собирает жетоны
    // мостов, печати земель и Ключ Эйлера; в Калининграде, где Эйлер
    // задачу и решил, собирается карта и проходится головоломка. Золотой
    // мост не выдаётся точкой — его СТРОЯТ в финале, это сама развязка.
    //
    // ЧЕСТНО ПРО КООРДИНАТЫ: широты/долготы ниже — ориентировочные, для
    // радиуса геозоны их достаточно, но перед поездкой их НУЖНО сверить с
    // картой. Особенно места бронзовых хомлинов — они маленькие, стоят у
    // конкретных зданий. Поле Verified=false помечает всё, что требует
    // сверки на местности.

    public enum Artifact
    {
        LandSeal,     // печать земли (их 4)
        BridgeToken,  // жетон моста (их 7)
        EulerKey,     // ключ Эйлера — правило чёт/нечет
        GoldenBridge  // золотой мост — строится в финале
    }

    public class Land
    {
        public int Index;
        public string Name;
        public string Modern;
        public Land(int i, string name, string modern) { Index = i; Name = name; Modern = modern; }
    }

    public class City
    {
        public string Id;
        public string Name;
        public string Old;
        public double Lat, Lon;
        public City(string id, string name, string old, double lat, double lon)
        { Id = id; Name = name; Old = old; Lat = lat; Lon = lon; }
    }

    // Точка на местности: где ребёнок реально стоит. Разблокируется, если
    // сработала геозона ЛИБО отсканирована печатная метка (MindAR).
    public class Poi
    {
        public string Id;
        public string CityId;
        public string Name;
        public double Lat, Lon;
        public float RadiusM;
        public string ArTarget;      // id метки AR-станции, "" — без AR
        public Artifact Gives;
        public string BridgeOrLand;  // id моста или индекс земли, если точка их выдаёт
        public string HomlinNote;
        public string ChildTask;
        public bool Verified;

        public Poi(string id, string cityId, string name, double lat, double lon,
                   float radius, string arTarget, Artifact gives, string bol,
                   string homlin, string task, bool verified)
        {
            Id = id; CityId = cityId; Name = name; Lat = lat; Lon = lon;
            RadiusM = radius; ArTarget = arTarget; Gives = gives; BridgeOrLand = bol;
            HomlinNote = homlin; ChildTask = task; Verified = verified;
        }
    }

    public static class KoenigContent
    {
        // ---------- Земли ----------
        public static readonly Land[] Lands =
        {
            new Land(0, "Север",  "Альтштадт — район у Королевского замка"),
            new Land(1, "Остров", "Кнайпхоф — сегодня остров Канта с Кафедральным собором"),
            new Land(2, "Юг",     "Форштадт — южный берег Преголи"),
            new Land(3, "Восток", "Ломзе — сегодня Октябрьский остров"),
        };

        // ---------- Мосты (граф задачи) ----------
        public static BridgeGraph BuildGraph()
        {
            string[] names = new string[Lands.Length];
            for (int i = 0; i < Lands.Length; i++) names[i] = Lands[i].Name;
            BridgeGraph g = new BridgeGraph(names);
            g.Add(new Bridge("lavochny",  "Лавочный",   0, 1, false));
            g.Add(new Bridge("kuznechny", "Кузнечный",  0, 1, false));
            g.Add(new Bridge("zelyony",   "Зелёный",    2, 1, false));
            g.Add(new Bridge("potrohovy", "Потроховый", 2, 1, false));
            g.Add(new Bridge("derevyany", "Деревянный", 0, 3, false));
            g.Add(new Bridge("vysoky",    "Высокий",    2, 3, false));
            g.Add(new Bridge("medovy",    "Медовый",    1, 3, true));
            return g;
        }

        // ---------- Города (в порядке пути) ----------
        public static readonly City[] Cities =
        {
            new City("zeln", "Зеленоградск", "Кранц",      54.9600, 20.4750),
            new City("svtl", "Светлогорск",  "Раушен",     54.9430, 20.1550),
            new City("bltk", "Балтийск",     "Пиллау",     54.6510, 19.9130),
            new City("klgd", "Калининград",  "Кёнигсберг", 54.7104, 20.4522),
        };

        // Порядок городов в путешествии; последний — финал.
        public static readonly string[] JourneyOrder = { "zeln", "svtl", "bltk", "klgd" };
        public const string FinaleCity = "klgd";

        public static int CityStep(string cityId)
        {
            for (int i = 0; i < JourneyOrder.Length; i++)
                if (JourneyOrder[i] == cityId) return i;
            return JourneyOrder.Length;
        }

        public static City FindCity(string id)
        {
            for (int i = 0; i < Cities.Length; i++)
                if (Cities[i].Id == id) return Cities[i];
            return null;
        }

        // ---------- Точки на местности (в порядке пути) ----------
        public static readonly Poi[] Points =
        {
            // --- Зеленоградск (начало) ---
            new Poi("murarium", "zeln", "Водонапорная башня · Мурариум",
                54.9585, 20.4770, 50f, "tower_zeln",
                Artifact.BridgeToken, "zelyony",
                "", "Поднимись на башню и сосчитай котов в коллекции Мурариума.", false),

            new Poi("zeln_promenade", "zeln", "Променад и Куршская коса",
                54.9640, 20.4790, 80f, "",
                Artifact.LandSeal, "3",
                "", "Найди на променаде столько живых котов, сколько мостов у Востока (три).", false),

            // --- Светлогорск (середина) ---
            new Poi("svtl_tower", "svtl", "Водонапорная башня Светлогорска",
                54.9410, 20.1560, 50f, "tower_shukhov",
                Artifact.BridgeToken, "vysoky",
                "", "Наведи камеру на чертёж башни — и она оживёт (AR-станция инженера).", false),

            new Poi("sundial", "svtl", "Солнечные часы на променаде",
                54.9385, 20.1600, 40f, "sundial_ar",
                Artifact.EulerKey, "",
                "", "Встань на солнечных часах: по тени определи час — так учатся считать чётность.", false),

            // --- Балтийск (середина) ---
            new Poi("baltiysk_light", "bltk", "Маяк Балтийска",
                54.6430, 19.8890, 60f, "lighthouse_ar",
                Artifact.BridgeToken, "derevyany",
                "", "Самый западный маяк России. Сосчитай его окна снизу вверх.", false),

            new Poi("pillau_fort", "bltk", "Цитадель Пиллау",
                54.6380, 19.8930, 80f, "",
                Artifact.BridgeToken, "potrohovy",
                "", "В пятиугольной крепости найди пятый бастион.", false),

            // --- Калининград (финал) ---
            new Poi("kant_island", "klgd", "Остров Канта · Кафедральный собор",
                54.7065, 20.5118, 60f, "sobor",
                Artifact.LandSeal, "1",
                "Бронзовый хомлин рядом — проверь у входа на остров.",
                "Найди могилу Канта у стены собора и сосчитай колонны портала.", false),

            new Poi("honey_bridge", "klgd", "Медовый мост",
                54.7060, 20.5135, 40f, "medovy_ar",
                Artifact.BridgeToken, "medovy",
                "Бронзовый хомлин на перилах Медового моста — сфотографируй.",
                "Пройди Медовый мост — единственный из семи, что уцелел.", false),

            new Poi("fish_village", "klgd", "Рыбная деревня",
                54.7052, 20.5145, 70f, "",
                Artifact.BridgeToken, "lavochny",
                "", "Поднимись на башню «Маяк» и найди реку Преголю — по ней стояли мосты.", false),

            new Poi("amber_museum", "klgd", "Музей янтаря · башня Дона",
                54.7215, 20.5115, 60f, "",
                Artifact.BridgeToken, "kuznechny",
                "Бронзовый хомлин Дед Карл — у Музея янтаря, самый первый из семьи.",
                "Найди у башни Дона первого хомлина и поздоровайся с ним.", false),
        };

        // Итоговая карточка правила — то, что ребёнок должен понять.
        public const string EulerRuleForKids =
            "У каждого угла сосчитай мосты. Где их нечётно — там можно только " +
            "начать или закончить. У прогулки один старт и один финиш, значит " +
            "нечётных углов может быть не больше двух. В Кёнигсберге их четыре — " +
            "поэтому обойти все семь мостов по разу нельзя. Но если достроить " +
            "один мост, нечётных станет два — и тогда можно!";

        // Все жетоны мостов, которые нужно собрать, чтобы открыть финал.
        public static List<string> AllBridgeTokens()
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < Points.Length; i++)
                if (Points[i].Gives == Artifact.BridgeToken) ids.Add(Points[i].BridgeOrLand);
            return ids;
        }
    }
}
