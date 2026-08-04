using UnityEngine;

namespace Koenig
{
    // Тап по цели. В Diablo 2 мышь делает и то, и другое: клик по земле —
    // идти, клик по врагу — бить. У нас движение осталось на джойстике —
    // палец ребёнка закрывает как раз ту точку, куда он целится, а поиска
    // пути в проекте нет (модуля навигации нет в manifest.json). Поэтому
    // тап значит ровно одно: «вот эта цель».
    //
    // Живёт отдельно от боя намеренно: здесь только «куда попал палец»,
    // без единого знания о здоровье, уроне и врагах. Бой появится следующим
    // шагом и будет спрашивать отсюда.
    //
    // Луч строится через ScreenToWorldPoint, а не ScreenPointToRay: обе
    // дороги в Unity равнозначны, но первая уже есть в заглушках, и
    // проверка кода не требует новых.
    public static class Targeting
    {
        // Сколько метров вперёд смотрим. Дальше дальней плоскости камеры
        // (90 м) заглядывать бессмысленно.
        private const float MaxDist = 90f;

        // Верхняя полоса экрана отдана HUD: там «← Карта» и счётчик, и тап
        // по ним не должен заодно назначать цель.
        private const float HudTop = 90f;

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
                if (t.position.x < Screen.width * 0.5f) continue;
                if (t.position.y > Screen.height - HudTop * s) continue;
                screenPos = t.position;
                return true;
            }

            // На десктопе мышью можно тыкать куда угодно, кроме HUD:
            // джойстика там всё равно нет.
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                Vector3 m = Input.mousePosition;
                if (m.y > Screen.height - HudTop * s) return false;
                screenPos = new Vector2(m.x, m.y);
                return true;
            }
            return false;
        }

        // Во что упёрся тап. Возвращает false, если палец ушёл в небо.
        public static bool Pick(Camera cam, out RaycastHit hit)
        {
            hit = new RaycastHit();
            if (cam == null) return false;

            Vector2 sp;
            if (!TryTapPoint(out sp)) return false;
            return Cast(cam, sp, out hit);
        }

        // Тот же луч, но по заданной точке экрана — для случаев, когда тап
        // уже поймал кто-то другой (например, кнопка удара с прицелом).
        public static bool Cast(Camera cam, Vector2 screenPos, out RaycastHit hit)
        {
            hit = new RaycastHit();
            if (cam == null) return false;

            Vector3 near = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane));
            Vector3 far = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, MaxDist));
            Vector3 dir = far - near;
            if (dir.sqrMagnitude < 0.0001f) return false;

            return Physics.Raycast(near, dir.normalized, out hit, MaxDist);
        }

        // Компонент цели на попадании. Модели пака многоуровневые, и
        // коллайдер почти никогда не висит на том же объекте, что и логика,
        // — поэтому ищем вверх по родителям.
        public static T Owner<T>(RaycastHit hit) where T : Component
        {
            if (hit.transform == null) return null;
            return hit.transform.GetComponentInParent<T>();
        }
    }
}
