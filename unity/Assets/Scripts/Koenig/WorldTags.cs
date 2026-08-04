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
        }
    }
}
