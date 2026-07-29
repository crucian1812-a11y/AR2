using UnityEngine;
using UnityEngine.UI;

// Ввод игрока, собранный из сенсора и клавиатуры.
public static class Ctrl
{
    public static Vector2 Move;
    private static Vector2 _look;
    private static bool _jump;
    private static bool _attack;
    private static bool _attackHeld;

    public static void AddLook(Vector2 delta) { _look += delta; }
    public static void QueueJump() { _jump = true; }
    public static void QueueAttack() { _attack = true; _attackHeld = true; }
    public static void ReleaseAttack() { _attackHeld = false; }

    private static bool _build;
    public static void QueueBuild() { _build = true; }

    public static bool ConsumeBuild()
    {
        bool v = _build;
        _build = false;
        return v;
    }

    // Признак удержания без сброса — нужен нырянию, которое длится всё
    // время, пока кнопка нажата.
    public static bool AttackHeld { get { return _attackHeld; } }

    // Удержание кнопки удара в воздухе = удар сверху.
    public static bool ConsumeAttackHeld()
    {
        bool v = _attackHeld;
        _attackHeld = false;
        return v;
    }

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
        _attackHeld = false;
        _build = false;
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
    private const float BuildRadius = 52f;

    private RectTransform _joyBase;
    private RectTransform _knob;
    private RectTransform _jumpBtn;
    private RectTransform _attackBtn;
    private RectTransform _buildBtn;
    private Text _jumpLabel;
    private Text _attackLabel;
    private Text _buildLabel;

    private int _joyFinger = -1;
    private int _lookFinger = -1;
    private int _jumpFinger = -1;
    private int _attackFinger = -1;
    private int _buildFinger = -1;
    private Vector2 _knobOffset;
    private Vector2 _joyOrigin;
    private float _scale = 1f;
    private bool _touchDevice;
    private bool _visible;

    private static TouchControls _instance;

    // Убрать кнопки с экрана на время лавки или победного ролика: иначе
    // игрок продолжает вслепую управлять героем сквозь чужое окно.
    // На клавиатуре кнопок и так нет, поэтому показ обратно разрешаем
    // только сенсорным устройствам.
    public static void SetVisible(bool visible)
    {
        if (_instance == null) return;
        _instance.ApplyVisible(visible && _instance._touchDevice);
    }

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
        _instance = this;
        _touchDevice = Application.isMobilePlatform || Input.touchSupported;
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

        _buildBtn = UiKit.MakeImage(canvas, Vector2.zero, Vector2.one * (BuildRadius * 2f * _scale),
            circle, new Color(0.55f, 0.75f, 1f, 0.25f)).rectTransform;
        _buildLabel = UiKit.MakeText(canvas, Vector2.zero, new Vector2(140f * _scale, 40f * _scale),
            "Блок", Mathf.RoundToInt(19f * _scale), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

        Layout();
        ApplyVisible(_touchDevice);
    }

    private void ApplyVisible(bool v)
    {
        _visible = v;
        _joyBase.gameObject.SetActive(v);
        _knob.gameObject.SetActive(v);
        _jumpBtn.gameObject.SetActive(v);
        _attackBtn.gameObject.SetActive(v);
        _jumpLabel.gameObject.SetActive(v);
        _attackLabel.gameObject.SetActive(v);
        _buildBtn.gameObject.SetActive(v);
        _buildLabel.gameObject.SetActive(v);

        if (!v)
        {
            // Пальцы, лежавшие на кнопках в момент скрытия, больше не
            // получат TouchPhase.Ended по своим веткам — сбрасываем сами.
            _joyFinger = -1;
            _lookFinger = -1;
            _jumpFinger = -1;
            _attackFinger = -1;
            _buildFinger = -1;
            _knobOffset = Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // Место, куда джойстик возвращается, когда палец отпущен.
    private Vector2 JoyHome { get { return new Vector2(190f * _scale, 190f * _scale); } }

    // Джойстик плавающий: он появляется там, где палец коснулся экрана.
    // С жёстко прибитым кругом приходилось смотреть на кнопку, а не на игру,
    // и большой палец постоянно соскальзывал с края.
    private Vector2 JoyCenter { get { return _joyFinger >= 0 ? _joyOrigin : JoyHome; } }

    private Vector2 JumpCenter { get { return new Vector2(Screen.width - 155f * _scale, 165f * _scale); } }
    private Vector2 AttackCenter { get { return new Vector2(Screen.width - 330f * _scale, 115f * _scale); } }
    private Vector2 BuildCenter { get { return new Vector2(Screen.width - 175f * _scale, 300f * _scale); } }

    private void Layout()
    {
        _scale = UiKit.Scale;
        _joyBase.anchoredPosition = JoyCenter;
        _knob.anchoredPosition = JoyCenter + _knobOffset;
        _jumpBtn.anchoredPosition = JumpCenter;
        _jumpLabel.rectTransform.anchoredPosition = JumpCenter;
        _attackBtn.anchoredPosition = AttackCenter;
        _attackLabel.rectTransform.anchoredPosition = AttackCenter;
        _buildBtn.anchoredPosition = BuildCenter;
        _buildLabel.rectTransform.anchoredPosition = BuildCenter;
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
                else if (Vector2.Distance(p, BuildCenter) < BuildRadius * _scale * 1.2f && _buildFinger < 0)
                {
                    _buildFinger = t.fingerId;
                    Ctrl.QueueBuild();
                }
                else if (Vector2.Distance(p, AttackCenter) < AttackRadius * _scale * 1.2f && _attackFinger < 0)
                {
                    _attackFinger = t.fingerId;
                    Ctrl.QueueAttack();
                }
                else if (p.x < Screen.width * 0.45f && _joyFinger < 0)
                {
                    _joyFinger = t.fingerId;
                    // Круг встаёт под палец. Отступ от краёв — чтобы у самого
                    // угла экрана оставался полный ход во все стороны.
                    float m = JoyRadius * _scale;
                    _joyOrigin = new Vector2(
                        Mathf.Clamp(p.x, m, Screen.width * 0.45f),
                        Mathf.Clamp(p.y, m, Screen.height - m));
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
                else if (t.fingerId == _buildFinger) _buildFinger = -1;
                else if (t.fingerId == _attackFinger)
                {
                    _attackFinger = -1;
                    // Без этого признак удержания оставался поднятым навсегда,
                    // и следующий прыжок сразу превращался в удар сверху.
                    Ctrl.ReleaseAttack();
                }
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
