using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Сумка на экране уровня. Ландшафтная, в две колонки: слева надетое,
    // справа то, что лежит. Списком, без клеток и перетаскивания.
    //
    // Стрелки «стало лучше/хуже» рядом с числами — не украшение. Семилетний
    // ребёнок читает цифры, но не читает их смысл: «+8% к скорости удара»
    // ему ничего не говорит, а зелёный треугольник говорит всё.
    public class InventoryUI : MonoBehaviour
    {
        private Transform _canvas;
        private readonly List<GameObject> _drawn = new List<GameObject>();
        private bool _open;

        public bool IsOpen { get { return _open; } }
        public System.Action OnClosed;

        public static InventoryUI Create(Transform canvas)
        {
            GameObject go = new GameObject("InventoryUI");
            go.transform.SetParent(canvas, false);
            InventoryUI ui = go.AddComponent<InventoryUI>();
            ui._canvas = canvas;
            return ui;
        }

        private void Keep(Object o)
        {
            Component c = o as Component;
            if (c != null) _drawn.Add(c.gameObject);
        }

        private void Clear()
        {
            for (int i = 0; i < _drawn.Count; i++)
                if (_drawn[i] != null) Object.Destroy(_drawn[i]);
            _drawn.Clear();
        }

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            _open = true;
            Draw();
        }

        public void Close()
        {
            _open = false;
            Clear();
            if (OnClosed != null) OnClosed();
        }

        private void Draw()
        {
            Clear();
            float s = UiKit.Scale;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

            Keep(UiKit.MakePanel(_canvas, new Vector2(cx, cy), new Vector2(9000f, 9000f),
                new Color(0f, 0f, 0f, 0.6f)));
            Keep(UiKit.MakePanel(_canvas, new Vector2(cx, cy),
                new Vector2(Screen.width * 0.92f, Screen.height * 0.86f),
                new Color(0.1f, 0.16f, 0.24f, 0.99f)));

            float top = cy + Screen.height * 0.36f;
            Keep(UiKit.MakeText(_canvas, new Vector2(cx, top), new Vector2(600f * s, 36f * s),
                "Сумка Кёни · янтарь: " + Inventory.Amber, Mathf.RoundToInt(21f * s),
                new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter));

            DrawGear(cx - Screen.width * 0.22f, top - 54f * s, s);
            DrawBag(cx + Screen.width * 0.20f, top - 54f * s, s);

            Keep(UiKit.MakeButton(_canvas, new Vector2(cx, cy - Screen.height * 0.38f),
                new Vector2(220f * s, 48f * s), "Закрыть", Mathf.RoundToInt(17f * s), Close));
        }

        // ---------- Надетое ----------

        private void DrawGear(float cx, float top, float s)
        {
            Keep(UiKit.MakeText(_canvas, new Vector2(cx, top), new Vector2(360f * s, 26f * s),
                "На себе", Mathf.RoundToInt(16f * s), new Color(0.8f, 0.88f, 0.98f),
                TextAnchor.MiddleCenter));

            for (int i = 0; i < 3; i++)
            {
                Slot slot = (Slot)i;
                Item it = Inventory.Equipped(slot);
                float y = top - (36f + i * 78f) * s;

                Keep(UiKit.MakePanel(_canvas, new Vector2(cx, y), new Vector2(400f * s, 70f * s),
                    new Color(0.14f, 0.22f, 0.32f, 1f)));
                Keep(UiKit.MakeTextLeft(_canvas, cx - 190f * s, y + 20f * s,
                    new Vector2(150f * s, 22f * s), Item.SlotName(slot), Mathf.RoundToInt(12f * s),
                    new Color(0.66f, 0.74f, 0.85f)));

                if (it == null)
                {
                    Keep(UiKit.MakeTextLeft(_canvas, cx - 190f * s, y - 8f * s,
                        new Vector2(360f * s, 22f * s), "— пусто —", Mathf.RoundToInt(14f * s),
                        new Color(0.5f, 0.56f, 0.64f)));
                    continue;
                }

                Keep(UiKit.MakeTextLeft(_canvas, cx - 60f * s, y + 20f * s,
                    new Vector2(250f * s, 24f * s), it.Title, Mathf.RoundToInt(15f * s), it.Tint));
                Keep(UiKit.MakeTextLeft(_canvas, cx - 190f * s, y - 12f * s,
                    new Vector2(300f * s, 34f * s), it.AffixLine(), Mathf.RoundToInt(11f * s),
                    new Color(0.78f, 0.85f, 0.94f), TextAnchor.UpperLeft));

                Slot cap = slot;
                Keep(UiKit.MakeButton(_canvas, new Vector2(cx + 150f * s, y - 16f * s),
                    new Vector2(90f * s, 32f * s), "Снять", Mathf.RoundToInt(13f * s),
                    delegate { Inventory.Unequip(cap); Draw(); }));
            }
        }

        // ---------- Сумка ----------

        private void DrawBag(float cx, float top, float s)
        {
            List<Item> bag = Inventory.Bag;
            Keep(UiKit.MakeText(_canvas, new Vector2(cx, top), new Vector2(360f * s, 26f * s),
                "В сумке " + bag.Count + " из " + Inventory.Capacity, Mathf.RoundToInt(16f * s),
                new Color(0.8f, 0.88f, 0.98f), TextAnchor.MiddleCenter));

            if (bag.Count == 0)
            {
                Keep(UiKit.MakeTextLeft(_canvas, cx - 210f * s, top - 46f * s,
                    new Vector2(420f * s, 26f * s), "Пока пусто — сходи побей грибов.",
                    Mathf.RoundToInt(13f * s), new Color(0.6f, 0.68f, 0.78f)));
                return;
            }

            for (int i = 0; i < bag.Count; i++)
            {
                Item it = bag[i];
                float y = top - (34f + i * 40f) * s;

                Keep(UiKit.MakePanel(_canvas, new Vector2(cx, y), new Vector2(440f * s, 36f * s),
                    new Color(0.13f, 0.2f, 0.29f, 1f)));
                Keep(UiKit.MakeTextLeft(_canvas, cx - 214f * s, y + 6f * s,
                    new Vector2(230f * s, 20f * s), it.Title, Mathf.RoundToInt(13f * s), it.Tint));
                Keep(UiKit.MakeTextLeft(_canvas, cx - 214f * s, y - 10f * s,
                    new Vector2(250f * s, 18f * s), it.AffixLine(), Mathf.RoundToInt(10f * s),
                    new Color(0.74f, 0.82f, 0.92f)));

                // Стрелка сравнения с тем, что надето в этом же слоте.
                string mark = Compare(it);
                if (mark.Length > 0)
                    Keep(UiKit.MakeTextRight(_canvas, cx + 100f * s, y,
                        new Vector2(40f * s, 24f * s), mark, Mathf.RoundToInt(15f * s),
                        mark == "▲" ? new Color(0.5f, 0.95f, 0.5f) : new Color(0.95f, 0.55f, 0.5f)));

                Item cap = it;
                Keep(UiKit.MakeButton(_canvas, new Vector2(cx + 150f * s, y),
                    new Vector2(80f * s, 30f * s), "Надеть", Mathf.RoundToInt(12f * s),
                    delegate { Inventory.Equip(cap); Draw(); }));
                Keep(UiKit.MakeButton(_canvas, new Vector2(cx + 205f * s, y),
                    new Vector2(28f * s, 30f * s), "✕", Mathf.RoundToInt(12f * s),
                    delegate { Inventory.Discard(cap); Draw(); }));
            }
        }

        // Грубое сравнение «в сумме лучше/хуже» — по цене, которая уже
        // складывает урон, жизнь и свойства в одно число. Точную разницу
        // по каждому свойству ребёнку показывать незачем.
        private static string Compare(Item candidate)
        {
            Item worn = Inventory.Equipped(candidate.Slot);
            if (worn == null) return "▲";
            if (candidate.Price > worn.Price) return "▲";
            if (candidate.Price < worn.Price) return "▼";
            return "";
        }
    }
}
