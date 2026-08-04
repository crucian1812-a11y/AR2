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
    // На её месте кнопка удара: держишь — герой бьёт по ближайшему врагу
    // в конусе перед собой, и этого хватает, чтобы пройти уровень, ни разу
    // не тапнув по цели. Остальные тапы правой половины уходят в
    // прицеливание (Targeting), круг кнопки оно пропускает.
    //
    // Джойстик по-прежнему ловит только левую половину, поэтому за палец
    // они не спорят.
    public class KoenigTouch : MonoBehaviour
    {
        private float _s = 1f;
        private RectTransform _ring, _knob, _atk;
        private Text _atkLabel;

        private int _joyFinger = -1, _atkFinger = -1;

        // Держат ли кнопку удара прямо сейчас. Читает Combat.
        public static bool AttackHeld;
        private Vector2 _joyOrigin, _knobOffset;
        private bool _touchDevice;

        private const float JoyRadius = 110f;
        private const float KnobRadius = 46f;
        private const float AtkRadius = 82f;

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

            _atk = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (AtkRadius * 2f * _s),
                Gfx.CircleSprite(), new Color(0.95f, 0.6f, 0.35f, 0.30f)).rectTransform;
            _atkLabel = UiKit.MakeText(canvas, Vector2.zero, new Vector2(180f * _s, 40f * _s),
                "Удар", Mathf.RoundToInt(20f * _s), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

            if (!_touchDevice) SetVisible(false);
            Layout();
        }

        private void SetVisible(bool v)
        {
            _ring.gameObject.SetActive(v);
            _knob.gameObject.SetActive(v);
            _atk.gameObject.SetActive(v);
            _atkLabel.gameObject.SetActive(v);
        }

        private Vector2 JoyHome { get { return new Vector2(190f * _s, 190f * _s); } }
        private Vector2 JoyCenter { get { return _joyFinger >= 0 ? _joyOrigin : JoyHome; } }
        private Vector2 AtkCenter { get { return new Vector2(Screen.width - 150f * _s, 165f * _s); } }

        private void Layout()
        {
            _ring.anchoredPosition = JoyCenter;
            _knob.anchoredPosition = JoyCenter + _knobOffset;
            _atk.anchoredPosition = AtkCenter;
            _atkLabel.rectTransform.anchoredPosition = AtkCenter;

            // Прицеливание обязано пропускать круг кнопки, иначе каждый
            // удар заодно сбрасывал бы цель тапом мимо врага.
            Targeting.ButtonCenter = AtkCenter;
            Targeting.ButtonRadius = AtkRadius * _s;
        }

        private void OnDestroy()
        {
            AttackHeld = false;
            Targeting.ButtonRadius = 0f;
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
                    if (Vector2.Distance(p, AtkCenter) < AtkRadius * _s && _atkFinger < 0)
                    {
                        _atkFinger = t.fingerId;
                        AttackHeld = true;
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
                    else if (t.fingerId == _atkFinger)
                    {
                        _atkFinger = -1;
                        AttackHeld = false;
                    }
                }
            }

            if (_joyFinger < 0)
            {
                _knobOffset = Vector2.zero;
                Ctrl.Move = Vector2.zero;
            }
            // Палец мог уйти с экрана мимо фазы Ended (например, при звонке).
            if (_atkFinger < 0) AttackHeld = false;
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
