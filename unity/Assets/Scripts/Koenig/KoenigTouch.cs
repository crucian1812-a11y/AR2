using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Простое сенсорное управление для игрового уровня: плавающий джойстик
    // в левой половине и одна кнопка прыжка справа — ровно то, что нужно
    // семилетке. Пишет в общий Ctrl, откуда читает KoenigPlayer. Не берём
    // TouchControls медведя целиком: там ещё удар и постройка блоков,
    // которых в этой игре нет.
    public class KoenigTouch : MonoBehaviour
    {
        private float _s = 1f;
        private RectTransform _ring, _knob, _jump;
        private Text _jumpLabel;

        private int _joyFinger = -1, _jumpFinger = -1;
        private Vector2 _joyOrigin, _knobOffset;
        private bool _touchDevice;

        private const float JoyRadius = 110f;
        private const float KnobRadius = 46f;
        private const float JumpRadius = 82f;

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
            _jump = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (JumpRadius * 2f * _s),
                Gfx.CircleSprite(), new Color(0.4f, 0.9f, 0.55f, 0.28f)).rectTransform;
            _jumpLabel = UiKit.MakeText(canvas, Vector2.zero, new Vector2(180f * _s, 40f * _s),
                "Прыжок", Mathf.RoundToInt(20f * _s), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

            if (!_touchDevice) SetVisible(false);
            Layout();
        }

        private void SetVisible(bool v)
        {
            _ring.gameObject.SetActive(v);
            _knob.gameObject.SetActive(v);
            _jump.gameObject.SetActive(v);
            _jumpLabel.gameObject.SetActive(v);
        }

        private Vector2 JoyHome { get { return new Vector2(190f * _s, 190f * _s); } }
        private Vector2 JoyCenter { get { return _joyFinger >= 0 ? _joyOrigin : JoyHome; } }
        private Vector2 JumpCenter { get { return new Vector2(Screen.width - 150f * _s, 165f * _s); } }

        private void Layout()
        {
            _ring.anchoredPosition = JoyCenter;
            _knob.anchoredPosition = JoyCenter + _knobOffset;
            _jump.anchoredPosition = JumpCenter;
            _jumpLabel.rectTransform.anchoredPosition = JumpCenter;
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
                    if (Vector2.Distance(p, JumpCenter) < JumpRadius * _s && _jumpFinger < 0)
                    {
                        _jumpFinger = t.fingerId;
                        Ctrl.QueueJump();
                    }
                    else if (p.x < Screen.width * 0.5f && _joyFinger < 0)
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
                    else if (t.fingerId == _jumpFinger) _jumpFinger = -1;
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
