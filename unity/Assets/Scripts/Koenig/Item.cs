using UnityEngine;

namespace Koenig
{
    // Предмет. Четыре ступени редкости вместо шести, как в Diablo 2, — для
    // семилетнего ребёнка больше не читается, а четыре различаются цветом
    // с одного взгляда.
    public enum Rarity { Common, Good, Rare, Named }

    // Три слота вместо десяти. Сетка-тетрис из D2 не переносится: она
    // единственное, что там по-настоящему требует мышь.
    public enum Slot { Weapon, Armor, Talisman }

    // Свойства предметов. Их восемь, а не десять из разбора: «−% кулдауна
    // умений» и «+ к Стойкости» относятся к умениям и характеристикам,
    // которых пока нет. Свойство, которое ничего не делает, — это враньё
    // на экране, поэтому они появятся вместе со своими системами.
    public enum Affix
    {
        Damage,       // +N к урону
        AttackSpeed,  // +N% скорости удара
        Health,       // +N к жизни
        Crit,         // +N% крита
        MoveSpeed,    // +N% скорости бега
        MagicFind,    // +N% шанса редкой находки
        AmberFind,    // +N янтаря с врага
        BossDamage,   // +N% урона по боссам
        Count
    }

    public class Item
    {
        public string BaseId = "knife";
        public Slot Slot;
        public Rarity Rarity;
        public int Level = 1;
        public string NamedId = "";
        public int[] Affixes = new int[(int)Affix.Count];

        public int Value(Affix a) { return Affixes[(int)a]; }

        // ---------- Базовые вещи ----------
        // Оружие даёт урон, доспех — жизнь, талисман сам по себе ничего:
        // он весь в свойствах, и это делает его самым интересным слотом.

        public static string BaseName(string id)
        {
            switch (id)
            {
                case "knife": return "Рыбацкий нож";
                case "spear": return "Острога";
                case "hook": return "Багор";
                case "hammer": return "Кузнечный молот";
                case "shirt": return "Холщовая рубаха";
                case "vest": return "Кожаная безрукавка";
                case "gambeson": return "Стёганый доспех";
                case "amber": return "Кусок янтаря";
                case "whistle": return "Костяной свисток";
                case "token": return "Медный жетон";
            }
            return "Вещь";
        }

        public static Slot BaseSlot(string id)
        {
            if (id == "shirt" || id == "vest" || id == "gambeson") return Slot.Armor;
            if (id == "amber" || id == "whistle" || id == "token") return Slot.Talisman;
            return Slot.Weapon;
        }

        // Урон базовой вещи: пара «меньше-больше», растёт с уровнем вещи.
        public void BaseDamage(out int min, out int max)
        {
            min = 0; max = 0;
            if (Slot != Slot.Weapon) return;
            switch (BaseId)
            {
                case "spear": min = 3; max = 7; break;
                case "hook": min = 4; max = 8; break;
                case "hammer": min = 5; max = 10; break;
                default: min = 2; max = 5; break;   // нож
            }
            min += Level - 1;
            max += Level - 1;
        }

        public int BaseHealth
        {
            get
            {
                if (Slot != Slot.Armor) return 0;
                int b = BaseId == "gambeson" ? 12 : BaseId == "vest" ? 8 : 5;
                return b + (Level - 1) * 2;
            }
        }

        // ---------- Именные ----------
        // Их немного и каждый привязан к настоящему месту. Описание —
        // одна честная строка: игра учит области, и выдуманная деталь тут
        // дороже, чем кажется.

        public static string NamedTitle(string id)
        {
            if (id == "murarium") return "Ключи смотрителя Мурариума";
            if (id == "fisher") return "Острога кранцского рыбака";
            return "";
        }

        public static string NamedStory(string id)
        {
            if (id == "murarium")
                return "Водонапорная башня Кранца, 1905 год. Сегодня в ней музей кошек.";
            if (id == "fisher")
                return "Кранц был рыбацкой деревней у входа на косу задолго до того, " +
                       "как стал курортом.";
            return "";
        }

        // ---------- Показ ----------

        public string Title
        {
            get
            {
                if (Rarity == Rarity.Named) return NamedTitle(NamedId);
                return BaseName(BaseId);
            }
        }

        public static Color TintOf(Rarity r)
        {
            if (r == Rarity.Named) return new Color(1f, 0.62f, 0.15f);
            if (r == Rarity.Rare) return new Color(1f, 0.85f, 0.35f);
            if (r == Rarity.Good) return new Color(0.45f, 0.65f, 1f);
            return new Color(0.92f, 0.92f, 0.88f);
        }

        public Color Tint { get { return TintOf(Rarity); } }

        public static string SlotName(Slot s)
        {
            if (s == Slot.Armor) return "Доспех";
            if (s == Slot.Talisman) return "Талисман";
            return "Оружие";
        }

        public static string AffixName(Affix a)
        {
            switch (a)
            {
                case Affix.Damage: return "к урону";
                case Affix.AttackSpeed: return "% к скорости удара";
                case Affix.Health: return "к жизни";
                case Affix.Crit: return "% к криту";
                case Affix.MoveSpeed: return "% к скорости бега";
                case Affix.MagicFind: return "% к редкой находке";
                case Affix.AmberFind: return "янтаря с врага";
                case Affix.BossDamage: return "% урона по боссам";
            }
            return "";
        }

        // Строка свойств через запятую — её показывает и сумка, и подпись
        // на земле.
        public string AffixLine()
        {
            string s = "";
            int mn, mx;
            BaseDamage(out mn, out mx);
            if (mx > 0) s = "урон " + mn + "–" + mx;
            if (BaseHealth > 0) s = Join(s, "+" + BaseHealth + " к жизни");

            for (int i = 0; i < (int)Affix.Count; i++)
            {
                if (Affixes[i] == 0) continue;
                s = Join(s, "+" + Affixes[i] + " " + AffixName((Affix)i));
            }
            return s;
        }

        private static string Join(string a, string b)
        {
            return a.Length == 0 ? b : a + ", " + b;
        }

        // Цена в янтаре — она же и «насколько вещь хороша». Торговка
        // появится следующим шагом, но копить есть смысл уже сейчас.
        public int Price
        {
            get
            {
                int p = 4 + Level * 2;
                if (Rarity == Rarity.Good) p += 8;
                else if (Rarity == Rarity.Rare) p += 24;
                else if (Rarity == Rarity.Named) p += 70;
                for (int i = 0; i < (int)Affix.Count; i++) p += Affixes[i];
                return p;
            }
        }

        // ---------- Хранение ----------
        // Формат плоский и короткий: сумка живёт в PlayerPrefs одной
        // строкой, как и весь остальной прогресс путешествия.

        public string Save()
        {
            string a = "";
            for (int i = 0; i < (int)Affix.Count; i++)
                a += (i > 0 ? "," : "") + Affixes[i];
            return BaseId + "|" + (int)Slot + "|" + (int)Rarity + "|" + Level + "|" + NamedId + "|" + a;
        }

        public static Item Load(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            string[] p = raw.Split('|');
            if (p.Length < 6) return null;

            Item it = new Item();
            it.BaseId = p[0];
            int v;
            it.Slot = int.TryParse(p[1], out v) ? (Slot)v : Slot.Weapon;
            it.Rarity = int.TryParse(p[2], out v) ? (Rarity)v : Rarity.Common;
            it.Level = int.TryParse(p[3], out v) ? v : 1;
            it.NamedId = p[4];

            string[] a = p[5].Split(',');
            for (int i = 0; i < a.Length && i < (int)Affix.Count; i++)
                it.Affixes[i] = int.TryParse(a[i], out v) ? v : 0;
            return it;
        }
    }
}
