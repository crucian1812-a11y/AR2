using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Подписи над объектами мира на экране уровня. WorldLabel — общий
    // реестр, он был и раньше, но рисовал его только Hud медведя. В игре
    // про Кёнигсберг своего рисовальщика не было, и подписи не появлялись
    // вовсе — включая «Босс», который вешает Enemy.SpawnBoss.
    //
    // Текст экранный, а не трёхмерный: так он чётче и дешевле, и это же
    // решение принято в Hud — повторяем его, а не изобретаем второе.
    public class WorldTags : MonoBehaviour
    {
        // Подписи рождаются последними детьми канваса, значит рисуются
        // поверх всего — включая открытую сумку. Пока она открыта, молчим.
        public bool Muted;

        private Camera _cam;
        private Transform _canvas;
        private readonly List<Text> _pool = new List<Text>();

        // Всплывающие числа урона. Без них бой был непрозрачен: ребёнок
        // жал кнопку и не понимал, попал или нет, много снял или мало.
        // Цифра, вылетающая из врага, отвечает на оба вопроса разом.
        private class Pop
        {
            public Text Label;
            public Vector3 World;
            public float Life;
        }
        private readonly List<Pop> _pops = new List<Pop>();
        private static WorldTags _live;

        public static void Damage(Vector3 world, int amount, bool crit)
        {
            if (_live == null) return;
            _live.Spawn(world, amount.ToString(),
                crit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.95f, 0.9f),
                crit ? 30 : 22);
        }

        public static void Note(Vector3 world, string text, Color tint)
        {
            if (_live != null) _live.Spawn(world, text, tint, 20);
        }

        private void Spawn(Vector3 world, string text, Color tint, int size)
        {
            Text t = UiKit.MakeText(_canvas, Vector2.zero,
                new Vector2(220f * UiKit.Scale, 40f * UiKit.Scale),
                text, Mathf.RoundToInt(size * UiKit.Scale), tint, TextAnchor.MiddleCenter);
            Pop p = new Pop();
            p.Label = t;
            p.World = world;
            p.Life = 0f;
            _pops.Add(p);
        }

        private void OnEnable() { _live = this; }
        private void OnDisable() { if (_live == this) _live = null; }

        public static WorldTags Create(Transform canvas, Camera cam)
        {
            GameObject go = new GameObject("WorldTags");
            go.transform.SetParent(canvas, false);
            WorldTags t = go.AddComponent<WorldTags>();
            t._canvas = canvas;
            t._cam = cam;
            return t;
        }

        private Text Get(int i)
        {
            while (_pool.Count <= i)
            {
                Text t = UiKit.MakeText(_canvas, Vector2.zero, new Vector2(280f * UiKit.Scale, 30f * UiKit.Scale),
                    "", 18, Color.white, TextAnchor.MiddleCenter);
                _pool.Add(t);
            }
            return _pool[i];
        }

        private void LateUpdate()
        {
            int used = 0;
            if (_cam != null && !Muted)
            {
                for (int i = 0; i < WorldLabel.All.Count; i++)
                {
                    WorldLabel wl = WorldLabel.All[i];
                    if (wl == null || string.IsNullOrEmpty(wl.Text)) continue;

                    Vector3 world = wl.transform.position + wl.Offset;
                    Vector3 sp = _cam.WorldToScreenPoint(world);
                    if (sp.z <= 0.5f) continue;
                    float dist = Vector3.Distance(_cam.transform.position, world);
                    if (dist > wl.MaxDistance) continue;

                    Text t = Get(used++);
                    t.gameObject.SetActive(true);
                    t.text = wl.Text;
                    // Дальние подписи бледнее — иначе улица превращается в
                    // стену текста.
                    float fade = Mathf.Clamp01(1f - (dist / wl.MaxDistance) * 0.7f);
                    t.color = new Color(wl.Tint.r, wl.Tint.g, wl.Tint.b, fade);
                    t.fontSize = Mathf.Max(9, Mathf.RoundToInt(
                        wl.FontSize * UiKit.Scale * Mathf.Clamp(14f / dist, 0.5f, 1.3f)));
                    t.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y);
                }
            }
            for (int i = used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);

            StepPops();
        }

        // Цифра поднимается на полтора метра за секунду и тает. Полёт
        // вверх нужен, чтобы числа от нескольких ударов подряд не легли
        // друг на друга.
        private void StepPops()
        {
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                Pop p = _pops[i];
                if (p.Label == null) { _pops.RemoveAt(i); continue; }

                p.Life += Time.deltaTime;
                if (p.Life > 1.1f)
                {
                    Object.Destroy(p.Label.gameObject);
                    _pops.RemoveAt(i);
                    continue;
                }

                Vector3 at = p.World + Vector3.up * (0.4f + p.Life * 1.5f);
                Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(at) : Vector3.zero;
                bool seen = _cam != null && sp.z > 0.5f && !Muted;
                p.Label.gameObject.SetActive(seen);
                if (!seen) continue;

                p.Label.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y);
                Color c = p.Label.color;
                c.a = Mathf.Clamp01(1f - (p.Life - 0.5f) / 0.6f);
                p.Label.color = c;
            }
        }
    }
}
