using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Вещь, лежащая на земле. Подпись над ней цветом редкости — это,
    // пожалуй, главная механика показа в Diablo 2: по цвету видно, стоит
    // ли идти, ещё не подойдя.
    //
    // Подбор — шагом по ней, а не тапом. В D2 предметы подбирают кликом,
    // но там мышь; ребёнок пробегает и собирает, как монетки, и это
    // единственный способ не превратить добычу в возню.
    public class Drop : MonoBehaviour
    {
        public static readonly List<Drop> All = new List<Drop>();

        public Item Item;
        private float _spin;

        public static Drop Create(Transform parent, Vector3 pos, Item item)
        {
            if (item == null) return null;

            GameObject go = new GameObject("Drop");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Drop d = go.AddComponent<Drop>();
            d.Item = item;
            d.Build();
            return d;
        }

        private void Build()
        {
            Color tint = Item.Tint;

            // Форма подсказывает слот: оружие вытянуто, доспех широкий,
            // талисман — камешек. Ребёнок узнаёт вещь до подписи.
            Vector3 size = Item.Slot == Slot.Weapon ? new Vector3(0.14f, 0.14f, 0.85f)
                : Item.Slot == Slot.Armor ? new Vector3(0.55f, 0.5f, 0.22f)
                : new Vector3(0.32f, 0.32f, 0.32f);

            Material m = Gfx.MatFull(tint, 0.5f, 0.25f, tint * 0.5f, 0f, 0f);
            GameObject body = Gfx.Box(transform, new Vector3(0f, 0.45f, 0f), size, m, false);
            body.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);

            // Свечение растёт с редкостью — именное видно через всю улицу.
            float glow = Item.Rarity == Rarity.Named ? 3f
                : Item.Rarity == Rarity.Rare ? 2f
                : Item.Rarity == Rarity.Good ? 1.2f : 0f;
            if (glow > 0f)
            {
                float alpha = Item.Rarity == Rarity.Named ? 0.7f
                    : Item.Rarity == Rarity.Rare ? 0.55f : 0.35f;
                Gfx.Glow(transform, new Vector3(0f, 0.45f, 0f), glow,
                    new Color(tint.r, tint.g, tint.b, alpha));
            }

            WorldLabel.Attach(transform, Item.Title, new Vector3(0f, 1.25f, 0f),
                tint, Item.Rarity == Rarity.Common ? 16 : 19);

            Snd.Play(Item.Rarity >= Rarity.Rare ? "crystal" : "coin",
                Item.Rarity >= Rarity.Rare ? 0.9f : 0.5f);
        }

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        private void Update()
        {
            _spin += Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0f, _spin * 55f, 0f);
        }
    }
}
