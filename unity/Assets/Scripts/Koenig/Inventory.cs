using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Сумка, надетое и янтарь. Единственный источник правды о снаряжении —
    // как Journey для точек, и по той же причине: ребёнок закрывает игру
    // между городами, и вещи обязаны это пережить.
    //
    // Сумка — СПИСОК на двенадцать строк, а не сетка. Перетаскивание
    // предметов по клеткам в D2 держится на мыши; пальцем это мучение, и
    // ничего от игры не отнимается, если его просто нет.
    //
    // Золото D2 здесь янтарь: орден с XIII века держал на балтийский
    // янтарь монополию, собирать его самовольно было нельзя. Это правда,
    // и она заодно объясняет, почему янтарь — деньги.
    public static class Inventory
    {
        public const int Capacity = 12;

        private const string KeyBag = "koenig.bag";
        private const string KeyGear = "koenig.gear";
        private const string KeyAmber = "koenig.amber";

        private static readonly List<Item> _bag = new List<Item>();
        private static readonly Item[] _gear = new Item[3];
        private static int _amber;
        private static bool _loaded;

        // Кому сообщать, что снаряжение изменилось: бой пересчитывает
        // числа, интерфейс перерисовывается.
        public static System.Action Changed;

        public static List<Item> Bag { get { EnsureLoaded(); return _bag; } }
        public static int Amber { get { EnsureLoaded(); return _amber; } }
        public static bool Full { get { EnsureLoaded(); return _bag.Count >= Capacity; } }

        public static Item Equipped(Slot s)
        {
            EnsureLoaded();
            return _gear[(int)s];
        }

        // ---------- Изменения ----------

        public static bool Add(Item it)
        {
            EnsureLoaded();
            if (it == null || _bag.Count >= Capacity) return false;
            _bag.Add(it);
            Save();
            Fire();
            return true;
        }

        public static void Equip(Item it)
        {
            EnsureLoaded();
            if (it == null || !_bag.Contains(it)) return;

            int slot = (int)it.Slot;
            Item old = _gear[slot];
            _bag.Remove(it);
            _gear[slot] = it;
            // Снятое возвращается в сумку — место освободилось только что,
            // так что оно там всегда найдётся.
            if (old != null) _bag.Add(old);
            Save();
            Fire();
        }

        public static void Unequip(Slot s)
        {
            EnsureLoaded();
            Item cur = _gear[(int)s];
            if (cur == null || _bag.Count >= Capacity) return;
            _gear[(int)s] = null;
            _bag.Add(cur);
            Save();
            Fire();
        }

        public static void Discard(Item it)
        {
            EnsureLoaded();
            if (it == null) return;
            if (_bag.Remove(it)) { Save(); Fire(); }
        }

        public static void AddAmber(int n)
        {
            EnsureLoaded();
            if (n <= 0) return;
            _amber += n;
            Save();
            Fire();
        }

        // ---------- Сумма надетого ----------

        public static int Bonus(Affix a)
        {
            EnsureLoaded();
            int sum = 0;
            for (int i = 0; i < _gear.Length; i++)
                if (_gear[i] != null) sum += _gear[i].Value(a);
            return sum;
        }

        public static void WeaponDamage(out int min, out int max)
        {
            EnsureLoaded();
            // Голыми руками бить тоже можно — это стартовые 3–6 из боя.
            min = 3; max = 6;
            Item w = _gear[(int)Slot.Weapon];
            if (w != null)
            {
                int a, b;
                w.BaseDamage(out a, out b);
                if (b > 0) { min = a; max = b; }
            }
            int flat = Bonus(Affix.Damage);
            min += flat;
            max += flat;
        }

        public static int ExtraHealth
        {
            get
            {
                EnsureLoaded();
                int hp = Bonus(Affix.Health);
                Item ar = _gear[(int)Slot.Armor];
                if (ar != null) hp += ar.BaseHealth;
                return hp;
            }
        }

        // Шанс редкой находки. Сюда же входит главный крючок путешествия:
        // каждый ВЗЯТЫЙ НА МЕСТЕ жетон моста добавляет 15%. Съездил —
        // играется лучше, и обойти это из дома нельзя.
        public static float MagicFind
        {
            get
            {
                EnsureLoaded();
                float mf = Bonus(Affix.MagicFind) * 0.01f;
                mf += Journey.Count(Artifact.BridgeToken) * 0.15f;
                return mf;
            }
        }

        // ---------- Хранение ----------

        public static void Load()
        {
            _bag.Clear();
            for (int i = 0; i < _gear.Length; i++) _gear[i] = null;

            string raw = PlayerPrefs.GetString(KeyBag, "");
            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split(';');
                for (int i = 0; i < parts.Length && _bag.Count < Capacity; i++)
                {
                    Item it = Item.Load(parts[i]);
                    if (it != null) _bag.Add(it);
                }
            }

            raw = PlayerPrefs.GetString(KeyGear, "");
            if (!string.IsNullOrEmpty(raw))
            {
                string[] parts = raw.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    Item it = Item.Load(parts[i]);
                    if (it != null) _gear[(int)it.Slot] = it;
                }
            }

            _amber = PlayerPrefs.GetInt(KeyAmber, 0);
            _loaded = true;
        }

        private static void EnsureLoaded() { if (!_loaded) Load(); }

        public static void Save()
        {
            string bag = "";
            for (int i = 0; i < _bag.Count; i++)
                bag += (i > 0 ? ";" : "") + _bag[i].Save();
            PlayerPrefs.SetString(KeyBag, bag);

            string gear = "";
            for (int i = 0; i < _gear.Length; i++)
                if (_gear[i] != null) gear += (gear.Length > 0 ? ";" : "") + _gear[i].Save();
            PlayerPrefs.SetString(KeyGear, gear);

            PlayerPrefs.SetInt(KeyAmber, _amber);
            PlayerPrefs.Save();
        }

        private static void Fire() { if (Changed != null) Changed(); }

        public static void Reset()
        {
            _bag.Clear();
            for (int i = 0; i < _gear.Length; i++) _gear[i] = null;
            _amber = 0;
            _loaded = true;
            Save();
            Fire();
        }
    }
}
