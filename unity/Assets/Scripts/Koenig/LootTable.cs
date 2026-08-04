using UnityEngine;

namespace Koenig
{
    // Что и с каким шансом падает. Отдельно от предмета намеренно: это
    // настроечная таблица, её будут крутить живьём, и трогать при этом
    // логику вещей не придётся.
    //
    // Балансировать придётся на устройстве. Первая версия таблицы всегда
    // мимо: либо жёлтое сыплется каждые полминуты и редкость перестаёт
    // что-либо значить, либо не выпадает вовсе за всю вылазку.
    public static class LootTable
    {
        private static readonly string[] Weapons = { "knife", "spear", "hook", "hammer" };
        private static readonly string[] Armors = { "shirt", "vest", "gambeson" };
        private static readonly string[] Talismans = { "amber", "whistle", "token" };

        // Шанс, что с обычного врага вообще что-то упадёт.
        private const float CommonDropChance = 0.35f;

        // Сколько янтаря даёт обычный враг и босс.
        public static int AmberFor(bool boss)
        {
            int b = boss ? Random.Range(18, 31) : Random.Range(1, 4);
            return b + Inventory.Bonus(Affix.AmberFind);
        }

        // Ролл с обычного врага: чаще всего ничего.
        public static Item FromEnemy(int level, float magicFind)
        {
            if (Random.value > CommonDropChance) return null;
            return Make(level, magicFind, false);
        }

        // С босса падает всегда и помногу — это главное событие вылазки.
        public static Item[] FromBoss(int level, float magicFind)
        {
            int n = Random.Range(3, 6);
            Item[] loot = new Item[n];
            for (int i = 0; i < n; i++) loot[i] = Make(level, magicFind, true);
            return loot;
        }

        // ---------- Сборка вещи ----------

        private static Item Make(int level, float magicFind, bool generous)
        {
            Item it = new Item();
            it.Level = Mathf.Max(1, level);
            it.Rarity = RollRarity(magicFind, generous);

            if (it.Rarity == Rarity.Named)
            {
                // Именных мало и они закреплены за местом. Талисман падает
                // с босса, острога — откуда угодно.
                it.NamedId = generous ? "murarium" : "fisher";
                it.BaseId = generous ? "token" : "spear";
                it.Slot = Item.BaseSlot(it.BaseId);
                RollAffixes(it, 3, 1.6f);
                return it;
            }

            int roll = Random.Range(0, 3);
            it.BaseId = roll == 0 ? Weapons[Random.Range(0, Weapons.Length)]
                : roll == 1 ? Armors[Random.Range(0, Armors.Length)]
                : Talismans[Random.Range(0, Talismans.Length)];
            it.Slot = Item.BaseSlot(it.BaseId);

            int count = it.Rarity == Rarity.Rare ? Random.Range(2, 4)
                : it.Rarity == Rarity.Good ? 1 : 0;
            RollAffixes(it, count, 1f);
            return it;
        }

        // Редкость. Магическая находка двигает верхние ступени, а не
        // нижние: с ней начинает чаще падать хорошее, а не больше всего.
        private static Rarity RollRarity(float magicFind, bool generous)
        {
            float mf = 1f + Mathf.Max(0f, magicFind);
            float named = (generous ? 0.04f : 0.004f) * mf;
            float rare = (generous ? 0.22f : 0.07f) * mf;
            float good = (generous ? 0.40f : 0.22f) * mf;

            float r = Random.value;
            if (r < named) return Rarity.Named;
            if (r < named + rare) return Rarity.Rare;
            if (r < named + rare + good) return Rarity.Good;
            return Rarity.Common;
        }

        private static void RollAffixes(Item it, int count, float scale)
        {
            if (count <= 0) return;

            // Свойства не повторяются: «+3 к урону, +2 к урону» на одной
            // вещи читается как ошибка, даже когда это не она.
            bool[] used = new bool[(int)Affix.Count];
            for (int n = 0; n < count; n++)
            {
                int guard = 0;
                int a;
                do { a = Random.Range(0, (int)Affix.Count); guard++; }
                while (used[a] && guard < 20);
                if (used[a]) break;
                used[a] = true;
                it.Affixes[a] = Mathf.Max(1, Mathf.RoundToInt(RollValue((Affix)a, it.Level) * scale));
            }
        }

        private static int RollValue(Affix a, int level)
        {
            switch (a)
            {
                case Affix.Damage: return Random.Range(1, 5) + (level - 1) / 2;
                case Affix.AttackSpeed: return Random.Range(4, 13);
                case Affix.Health: return Random.Range(4, 13) + level;
                case Affix.Crit: return Random.Range(2, 7);
                case Affix.MoveSpeed: return Random.Range(3, 9);
                case Affix.MagicFind: return Random.Range(5, 16);
                case Affix.AmberFind: return Random.Range(1, 5);
                case Affix.BossDamage: return Random.Range(5, 16);
            }
            return 1;
        }
    }
}
