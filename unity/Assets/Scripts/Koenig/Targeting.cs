using UnityEngine;

namespace Koenig
{
    // Тап по цели. В Diablo 2 мышь делает и то, и другое: клик по земле —
    // идти, клик по врагу — бить. У нас движение осталось на джойстике —
    // палец ребёнка закрывает как раз ту точку, куда он целится, а поиска
    // пути в проекте нет (модуля навигации нет в manifest.json). Поэтому
    // тап значит ровно одно: «вот эта цель».
    //
    // ВАЖНО, ПОЧЕМУ НЕ ЛУЧ. Первая версия стреляла Physics.Raycast, как и
    // задумывалось на бумаге. Проверка показала, что попадать там не во
    // что: Enemy не вешает ни одного коллайдера — ни сам, ни через
    // CharacterModel. Медведю они были не нужны, он считал удары по
    // расстоянию. Развесить коллайдеры по всем моделям пака — это те самые
    // сотни MeshCollider, которых отдельно велено избегать.
    //
    // Поэтому отбор идёт по экрану: центры врагов проецируются в пиксели,
    // и берётся ближайший к пальцу в пределах радиуса. Это и точнее для
    // ребёнка — палец шириной девять миллиметров прощается допуском, а не
    // требует попасть в силуэт.
    public static class Targeting
    {
        // Допуск вокруг пальца, в логических пикселях. Девять миллиметров
        // пальца — примерно столько на телефонной плотности.
        private const float TapRadius = 110f;

        // Верхняя полоса экрана отдана HUD: там «← Карта» и счётчик, и тап
        // по ним не должен заодно назначать цель.
        private const float HudTop = 90f;

        // Круг кнопки удара, который прицеливание обязано пропускать.
        // Ставит KoenigTouch; ноль радиуса — кнопки нет.
        public static Vector2 ButtonCenter;
        public static float ButtonRadius;

        // Свежий тап в игровой части экрана. Левая половина принадлежит
        // джойстику (см. KoenigTouch), поэтому цели берём только справа —
        // иначе один палец означал бы и ход, и приказ.
        public static bool TryTapPoint(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            float s = UiKit.Scale;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                if (!Allowed(t.position, s)) continue;
                if (t.position.x < Screen.width * 0.5f) continue;
                screenPos = t.position;
                return true;
            }

            // На десктопе мышью можно тыкать куда угодно, кроме HUD и
            // кнопки: джойстика там всё равно нет.
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                Vector3 m = Input.mousePosition;
                Vector2 p = new Vector2(m.x, m.y);
                if (!Allowed(p, s)) return false;
                screenPos = p;
                return true;
            }
            return false;
        }

        private static bool Allowed(Vector2 p, float s)
        {
            if (p.y > Screen.height - HudTop * s) return false;
            if (ButtonRadius > 0f && Vector2.Distance(p, ButtonCenter) < ButtonRadius) return false;
            return true;
        }

        // Насколько далеко цель от пальца по экрану. Возвращает false, если
        // цель за спиной камеры — там WorldToScreenPoint даёт зеркальную
        // точку, и без этой проверки враг позади «попадал» под палец.
        public static bool ScreenGap(Camera cam, Vector3 world, Vector2 tap, out float gap)
        {
            gap = float.MaxValue;
            if (cam == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            gap = Vector2.Distance(new Vector2(sp.x, sp.y), tap);
            return true;
        }

        public static float Tolerance { get { return TapRadius * UiKit.Scale; } }
    }
}
