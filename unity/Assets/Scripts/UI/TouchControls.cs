using UnityEngine;
using UnityEngine.UI;

// Ввод игрока, собранный из сенсора и клавиатуры.
public static class Ctrl
{
    public static Vector2 Move;
    private static Vector2 _look;
    private static bool _jump;
    private static bool _attack;

    public static void AddLook(Vector2 delta) { _look += delta; }
    public static void QueueJump() { _jump = true; }
    public static void QueueAttack() { _attack = true; }

    public static Vector2 ConsumeLook()
    {
        Vector2 v = _look;
        _look = Vector2.zero;
        return v;
    }

    public static bool ConsumeJump()
    {
        bool v = _jump;
        _jump = false;
        return v;
    }

    public static bool ConsumeAttack()
    {
        bool v = _attack;
        _attack = false;
        return v;
    }

    public static void Reset()
    {
        Move = Vector2.zero;
        _look = Vector2.zero;
        _jump = false;
        _attack = false;
    }
}

// Сенсорное управление: джойстик слева, кнопки прыжка и удара справа,
// свободная зона справа вращает камеру.
public class TouchControls : MonoBehaviour
{
    private const float JoyRadius = 110f;
    private const float KnobRadius = 46f;
    private const float JumpRadius = 85f;
    private const float AttackRadius = 62f;

    private RectTransform _joyBase;
    private RectTransform _knob;
    private RectTransform _jumpBtn;
    private RectTransform _attackBtn;
    private Text _jumpLabel;
    private Text _attackLabel;

    private int _joyFinger = -1;
    private int _lookFinger = -1;
    private int _jumpFinger = -1;
    private int _attackFinger = -1;
    private Vector2 _knobOffset;
    private float _scale = 1f;
    private bool _visible;

    public static TouchControls Create(Transform canvas)
    {
        GameObject go = new GameObject("TouchControls");
        go.transform.SetParent(canvas, false);
        TouchControls tc = go.AddComponent<TouchControls>();
        tc.Build(canvas);
        return tc;
    }

    private void Build(Transform canvas)
    {
        _visible = Application.isMobilePlatform || Input.touchSupported;
        _scale = UiKit.Scale;

        Sprite ring = Gfx.RingSprite();
        Sprite circle = Gfx.CircleSprite();

        _joyBase = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (JoyRadius * 2f * _scale),
            ring, new Color(1f, 1f, 1f, 0.25f)).rectTransform;
        _knob = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (KnobRadius * 2f * _scale),
            circle, new Color(1f, 1f, 1f, 0.35f)).rectTransform;

        _jumpBtn = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (JumpRadius * 2f * _scale),
            circle, new Color(0.4f, 0.9f, 0.5f, 0.25f)).rectTransform;
        _jumpLabel = UiKit.MakeText(canvas, Vector2.zero, new Vector2(180f * _scale, 40f * _scale),
            "Прыжок", Mathf.RoundToInt(22f * _scale), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

        _attackBtn = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (AttackRadius * 2f * _scale),
            circle, new Color(1f, 0.55f, 0.35f, 0.25f)).rectTransform;
        _attackLabel = UiKit.MakeText(canvas, Vector2.zero, new Vector2(140f * _scale, 40f * _scale),
            "Удар", Mathf.RoundToInt(20f * _scale), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

        Layout();
        SetVisible(_visible);
    }

    private void SetVisible(bool v)
    {
        _joyBase.gameObject.SetActive(v);
        _knob.gameObject.SetActive(v);
        _jumpBtn.gameObject.SetActive(v);
        _attackBtn.gameObject.SetActive(v);
        _jumpLabel.gameObject.SetActive(v);
        _attackLabel.gameObject.SetActive(v);
    }

    private Vector2 JoyCenter { get { return new Vector2(190f * _scale, 190f * _scale); } }
    private Vector2 JumpCenter { get { return new Vector2(Screen.width - 155f * _scale, 165f * _scale); } }
    private Vector2 AttackCenter { get { return new Vector2(Screen.width - 330f * _scale, 115f * _scale); } }

    private void Layout()
    {
        _scale = UiKit.Scale;
        _joyBase.anchoredPosition = JoyCenter;
        _knob.anchoredPosition = JoyCenter + _knobOffset;
        _jumpBtn.anchoredPosition = JumpCenter;
        _jumpLabel.rectTransform.anchoredPosition = JumpCenter;
        _attackBtn.anchoredPosition = AttackCenter;
        _attackLabel.rectTransform.anchoredPosition = AttackCenter;
    }

    private void Update()
    {
        if (!_visible) return;
        Layout();
        HandleTouches();
        _knob.anchoredPosition = JoyCenter + _knobOffset;
    }

    private void HandleTouches()
    {
        bool joySeen = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            Vector2 p = t.position;

            if (t.phase == TouchPhase.Began)
            {
                if (Vector2.Distance(p, JumpCenter) < JumpRadius * _scale && _jumpFinger < 0)
                {
                    _jumpFinger = t.fingerId;
                    Ctrl.QueueJump();
                }
                else if (Vector2.Distance(p, AttackCenter) < AttackRadius * _scale * 1.2f && _attackFinger < 0)
                {
                    _attackFinger = t.fingerId;
                    Ctrl.QueueAttack();
                }
                else if (p.x < Screen.width * 0.45f && _joyFinger < 0)
                {
                    _joyFinger = t.fingerId;
                    UpdateJoystick(p);
                    joySeen = true;
                }
                else if (_lookFinger < 0)
                {
                    _lookFinger = t.fingerId;
                }
            }
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                if (t.fingerId == _joyFinger)
                {
                    UpdateJoystick(p);
                    joySeen = true;
                }
                else if (t.fingerId == _lookFinger)
                {
                    Ctrl.AddLook(new Vector2(t.deltaPosition.x * 0.16f, t.deltaPosition.y * 0.12f));
                }
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.fingerId == _joyFinger) _joyFinger = -1;
                else if (t.fingerId == _lookFinger) _lookFinger = -1;
                else if (t.fingerId == _jumpFinger) _jumpFinger = -1;
                else if (t.fingerId == _attackFinger) _attackFinger = -1;
            }
            else if (t.fingerId == _joyFinger)
            {
                joySeen = true;
            }
        }

        if (_joyFinger < 0 || !joySeen)
        {
            if (_joyFinger < 0)
            {
                _knobOffset = Vector2.zero;
                Ctrl.Move = Vector2.zero;
            }
        }
    }

    private void UpdateJoystick(Vector2 screenPos)
    {
        Vector2 v = screenPos - JoyCenter;
        float radius = JoyRadius * _scale;
        if (v.magnitude > radius) v = v.normalized * radius;
        _knobOffset = v;
        Ctrl.Move = v / radius;
    }
}
