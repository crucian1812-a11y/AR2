using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Простое сенсорное управление для игрового уровня: плавающий джойстик
    // в левой половине экрана. Пишет в общий Ctrl, откуда читает
    // KoenigPlayer. Не берём TouchControls медведя целиком: там ещё
    // постройка блоков, которой в этой игре нет.
    //
    // Кнопки прыжка больше нет — в изометрической RPG прыгать некуда.
    // Правая половина намеренно оставлена пустой: там будет кнопка удара,
    // и туда же приходят тапы по целям (см. Targeting). Джойстик по-
    // прежнему ловит только левую половину, поэтому за палец они не
    // спорят.
    public class KoenigTouch : MonoBehaviour
    {
        private float _s = 1f;
        private RectTransform _ring, _knob;

        private int _joyFinger = -1;
        private Vector2 _joyOrigin, _knobOffset;
        private bool _touchDevice;

        private const float JoyRadius = 110f;
        private const float KnobRadius = 46f;

        public static KoenigTouch Create(Transform canvas)
        {
            GameObject go = new GameObject("KoenigTouch");
            go.transform.SetParent(canvas, false);
            KoenigTouch t = go.AddComponent<KoenigTouch>();
            t.Build(canvas);
            return t;
        }

        private void Build(Transform canvas)
        {
            _s = UiKit.Scale;
            _touchDevice = Application.isMobilePlatform || Input.touchSupported;

            _ring = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (JoyRadius * 2f * _s),
                Gfx.RingSprite(), new Color(1f, 1f, 1f, 0.22f)).rectTransform;
            _knob = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (KnobRadius * 2f * _s),
                Gfx.CircleSprite(), new Color(1f, 1f, 1f, 0.34f)).rectTransform;

            if (!_touchDevice) SetVisible(false);
            Layout();
        }

        private void SetVisible(bool v)
        {
            _ring.gameObject.SetActive(v);
            _knob.gameObject.SetActive(v);
        }

        private Vector2 JoyHome { get { return new Vector2(190f * _s, 190f * _s); } }
        private Vector2 JoyCenter { get { return _joyFinger >= 0 ? _joyOrigin : JoyHome; } }

        private void Layout()
        {
            _ring.anchoredPosition = JoyCenter;
            _knob.anchoredPosition = JoyCenter + _knobOffset;
        }

        private void Update()
        {
            if (!_touchDevice) return;
            _s = UiKit.Scale;
            HandleTouches();
            Layout();
        }

        private void HandleTouches()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 p = t.position;

                if (t.phase == TouchPhase.Began)
                {
                    if (p.x < Screen.width * 0.5f && _joyFinger < 0)
                    {
                        _joyFinger = t.fingerId;
                        float m = JoyRadius * _s;
                        _joyOrigin = new Vector2(
                            Mathf.Clamp(p.x, m, Screen.width * 0.5f),
                            Mathf.Clamp(p.y, m, Screen.height - m));
                        UpdateJoystick(p);
                    }
                }
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (t.fingerId == _joyFinger) UpdateJoystick(p);
                }
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    if (t.fingerId == _joyFinger)
                    {
                        _joyFinger = -1;
                        _knobOffset = Vector2.zero;
                        Ctrl.Move = Vector2.zero;
                    }
                }
            }

            if (_joyFinger < 0)
            {
                _knobOffset = Vector2.zero;
                Ctrl.Move = Vector2.zero;
            }
        }

        private void UpdateJoystick(Vector2 screenPos)
        {
            Vector2 v = screenPos - JoyCenter;
            float radius = JoyRadius * _s;
            if (v.magnitude > radius) v = v.normalized * radius;
            _knobOffset = v;
            Ctrl.Move = v / radius;
        }
    }
}
