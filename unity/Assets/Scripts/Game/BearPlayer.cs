using UnityEngine;

// Медведь-игрок: движение от третьего лица, прыжок с сальто, спин-атака,
// здоровье. Локальным управляет ввод, удалённые интерполируются по сети.
public class BearPlayer : MonoBehaviour
{
    public const int PlayerLayer = 8;

    private const float Speed = 6.5f;
    private const float Accel = 12f;
    // 11.5 при гравитации 22 даёт подъём около 3 метров — на такую высоту
    // и рассчитаны уступы в мирах.
    private const float JumpVelocity = 11.5f;
    private const float Gravity = 22f;
    private const float FlipTime = 0.55f;
    private const float AttackTime = 0.35f;
    private const float HitTime = 0.5f;
    // Высота, вокруг которой крутится сальто — примерно пояс персонажа.
    private const float PivotHeight = 0.9f;

    public int PeerId = 1;
    public string DisplayName = "";
    public float Hue = 0.07f;
    public int CharIndex;
    public bool IsLocal;
    public int Hearts = 3;

    // Модель персонажа. Если её не удалось загрузить, остаётся null и
    // игрок собирается из примитивов, как раньше.
    private CharacterModel _model;

    private CharacterController _cc;
    private Transform _visual;
    private Transform _flipNode;
    private Transform _body;
    private Transform _lArm, _rArm, _lLeg, _rLeg;
    private Transform _camYaw;
    private Transform _camArm;
    private Camera _cam;
    private GameObject _swipe;
    private Material _swipeMat;
    private Renderer[] _renderers;

    private Vector3 _velocity;
    private Vector3 _spawnPos;
    private float _flip;
    private float _lean;
    private float _squash;
    private float _animT;
    private float _attackAnim;
    private float _hitAnim;
    private float _attackCd;
    private float _invuln;
    private float _bounceCd;
    private bool _wasAirborne;
    // Двойной прыжок, удар сверху и плавание.
    private bool _canDoubleJump;
    private bool _pounding;
    private float _poundCd;
    private bool _swimming;
    // Немного времени на прыжок после схода с края: иначе на лестницах
    // прыжок съедается кадром, в котором опора уже потеряна.
    private float _coyote;
    private float _shake;
    private float _camPitch = -22f;
    private float _sendAccum;
    private byte _anim;
    private byte _prevNetAnim;

    private Vector3 _netPos;
    private float _netYaw;

    public byte AnimState { get { return _anim; } }

    public static BearPlayer Spawn(Transform parent, PlayerInfo info, bool isLocal)
    {
        GameObject go = new GameObject("Bear_" + info.Id);
        go.transform.SetParent(parent, false);
        go.layer = PlayerLayer;
        BearPlayer p = go.AddComponent<BearPlayer>();
        p.PeerId = info.Id;
        p.DisplayName = info.Name;
        p.Hue = info.Hue;
        p.CharIndex = info.Char;
        p.IsLocal = isLocal;
        p.Init();
        return p;
    }

    private void Init()
    {
        _cc = gameObject.AddComponent<CharacterController>();
        _cc.height = 1.7f;
        _cc.radius = 0.4f;
        _cc.center = new Vector3(0f, 0.85f, 0f);
        _cc.slopeLimit = 50f;
        // Высокий шаг: на ступенях игрока переставало вжимать в них.
        _cc.stepOffset = 0.65f;

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        BuildNodes();
        _model = CharacterModel.Spawn(_body, Heroes.Id(CharIndex), Heroes.BodyHeight);
        if (_model == null) BuildProceduralBear();
        BuildSwipe();
        BuildTeamRing();
        _renderers = GetComponentsInChildren<Renderer>();

        WorldLabel.Attach(transform, DisplayName, new Vector3(0f, 2.35f, 0f),
            IsLocal ? new Color(0.75f, 1f, 0.75f) : Color.white, 20);

        if (NetManager.I != null) Hearts = NetManager.I.MaxHearts;

        _netPos = transform.position;
        _spawnPos = transform.position;

        if (IsLocal) BuildCamera();
    }

    private void BuildCamera()
    {
        GameObject yaw = new GameObject("CamYaw");
        yaw.transform.SetParent(transform, false);
        yaw.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        _camYaw = yaw.transform;

        GameObject arm = new GameObject("CamArm");
        arm.transform.SetParent(_camYaw, false);
        _camArm = arm.transform;

        GameObject camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(_camArm, false);
        _cam = camGo.AddComponent<Camera>();
        _cam.fieldOfView = 60f;
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 400f;
        camGo.AddComponent<AudioListener>();
        // Постобработка (bloom, насыщенность, виньетка) — настройки задаёт мир.
        camGo.AddComponent<PostFx>();
    }

    // Сдвиг от внешней силы — например, движущейся платформы под ногами.
    public void ExternalMove(Vector3 delta)
    {
        if (_cc == null || !_cc.enabled) return;
        _cc.Move(delta);
    }

    // Подбрасывание батутом. Возвращает false, если недавно уже подбросило.
    public bool TryBounce(float power)
    {
        if (_bounceCd > 0f) return false;
        _bounceCd = 0.35f;
        _velocity.y = power;
        _flip = 0.0001f;
        _wasAirborne = true;
        return true;
    }

    // Чекпоинт становится новой точкой возрождения.
    public void SetCheckpoint(Vector3 pos)
    {
        _spawnPos = pos;
    }

    public void PlaceAt(Vector3 pos)
    {
        if (_cc != null) _cc.enabled = false;
        transform.position = pos;
        if (_cc != null) _cc.enabled = true;
        _spawnPos = pos;
        _netPos = pos;
        _velocity = Vector3.zero;
    }

    public void ApplyNetState(Vector3 pos, float yaw, byte anim, float flip)
    {
        _netPos = pos;
        _netYaw = yaw;
        if (anim != _prevNetAnim)
        {
            if (_prevNetAnim == 2 && anim != 2) _squash = 0.22f;
            if (anim == 3)
            {
                _attackAnim = AttackTime;
                if (_model != null) _model.Restart("Bite_InPlace", 1.6f);
            }
            _prevNetAnim = anim;
        }
        _anim = anim;
        _flip = flip;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (IsLocal)
        {
            UpdateCamera(dt);
            LocalMove(dt);
            EnemyInteractions();
            Timers(dt);
            if (transform.position.y < -30f) FallRespawn();
        }
        else
        {
            Vector3 p = Vector3.Lerp(transform.position, _netPos, Mathf.Min(dt * 10f, 1f));
            if ((transform.position - _netPos).magnitude > 12f) p = _netPos;
            if (_cc != null) _cc.enabled = false;
            transform.position = p;
            if (_cc != null) _cc.enabled = true;
            float cur = _visual.localEulerAngles.y;
            _visual.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(cur, _netYaw, Mathf.Min(dt * 10f, 1f)), 0f);
        }
        Animate(dt);
    }

    private void UpdateCamera(float dt)
    {
        if (_camYaw == null) return;
        Vector2 look = Ctrl.ConsumeLook();
        if (!Application.isMobilePlatform)
        {
            if (Input.GetMouseButton(1) || Input.GetMouseButton(0))
                look += new Vector2(Input.GetAxisRaw("Mouse X") * 2.2f, Input.GetAxisRaw("Mouse Y") * 2.2f);
            float kx = 0f;
            if (Input.GetKey(KeyCode.Q)) kx -= 1.6f;
            if (Input.GetKey(KeyCode.E)) kx += 1.6f;
            look.x += kx;
        }

        _camYaw.localRotation = Quaternion.Euler(0f, _camYaw.localEulerAngles.y + look.x, 0f);
        _camPitch = Mathf.Clamp(_camPitch - look.y, -70f, 25f);
        _camArm.localRotation = Quaternion.Euler(-_camPitch, 0f, 0f);

        // Пружинная рука камеры: не даём ей уйти внутрь геометрии.
        float desired = 6.8f;
        Vector3 origin = _camYaw.position;
        Vector3 dir = -_camArm.forward;
        RaycastHit hit;
        float dist = desired;
        // Сфера вместо луча: тонкий луч проскакивал мимо стволов и веток,
        // и камера оказывалась внутри кроны.
        if (Physics.SphereCast(origin, 0.45f, dir, out hit, desired + 0.3f, ~(1 << PlayerLayer)))
            dist = Mathf.Max(1.4f, hit.distance - 0.15f);
        Vector3 camPos = new Vector3(0f, 0f, -dist);
        if (_shake > 0f)
        {
            _shake = Mathf.Max(0f, _shake - dt * 2.2f);
            float a = _shake * 0.6f;
            camPos += new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
        }
        _cam.transform.localPosition = camPos;
    }

    private void LocalMove(float dt)
    {
        Vector2 mv = Ctrl.Move;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) mv.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) mv.y -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) mv.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) mv.x += 1f;
        if (mv.magnitude > 1f) mv = mv.normalized;

        Vector3 fwd = _camYaw != null ? _camYaw.forward : Vector3.forward;
        Vector3 right = _camYaw != null ? _camYaw.right : Vector3.right;
        fwd.y = 0f; right.y = 0f;
        Vector3 dir = (fwd.normalized * mv.y + right.normalized * mv.x);
        if (dir.magnitude > 1f) dir = dir.normalized;

        float k = Mathf.Min(Accel * dt, 1f);
        _velocity.x = Mathf.Lerp(_velocity.x, dir.x * Speed, k);
        _velocity.z = Mathf.Lerp(_velocity.z, dir.z * Speed, k);

        bool jumpPressed = Ctrl.ConsumeJump() || Input.GetKeyDown(KeyCode.Space);
        bool grounded = _cc.isGrounded;

        // ---------- Вода ----------
        float surface = WaterZone.SurfaceAt(transform.position + new Vector3(0f, 0.6f, 0f));
        bool inWater = surface > float.MinValue;
        if (inWater)
        {
            if (!_swimming)
            {
                _swimming = true;
                _pounding = false;
                Snd.Play("splash", 0.9f);
                ParticleFx.Burst(transform.parent, transform.position, 18,
                    new Color(0.7f, 0.9f, 1f), 5f);
            }

            // Выталкивание к поверхности плюс медленное погружение.
            float depth = surface - (transform.position.y + 0.9f);
            float buoyancy = Mathf.Clamp(depth * 6f, -4f, 7f);
            _velocity.y = Mathf.Lerp(_velocity.y, buoyancy, Mathf.Min(dt * 5f, 1f));

            if (jumpPressed) _velocity.y = 6.5f;
            if (Input.GetKey(KeyCode.LeftShift)) _velocity.y -= 6f * dt;

            // В воде медленнее и без инерции броска.
            _velocity.x = Mathf.Lerp(_velocity.x, dir.x * Speed * 0.62f, Mathf.Min(dt * 6f, 1f));
            _velocity.z = Mathf.Lerp(_velocity.z, dir.z * Speed * 0.62f, Mathf.Min(dt * 6f, 1f));
            _canDoubleJump = true;
            _wasAirborne = false;
        }
        else
        {
            if (_swimming)
            {
                _swimming = false;
                // Выпрыгиваем из воды с небольшим толчком.
                _velocity.y = Mathf.Max(_velocity.y, 5f);
            }

            if (grounded)
            {
                if (_wasAirborne)
                {
                    _wasAirborne = false;
                    // Приседание только при заметном падении: на ступенях
                    // мягкие касания шли подряд и персонажа постоянно плющило.
                    if (_pounding) { _squash = 0.4f; PoundImpact(); }
                    else if (_velocity.y < -9f) _squash = 0.22f;
                    Snd.Play("land", _pounding ? 1f : 0.7f);
                }
                _pounding = false;
                _canDoubleJump = true;
                _coyote = 0.14f;
                if (_velocity.y < 0f) _velocity.y = -2f;
            }
            else
            {
                _wasAirborne = true;
                _coyote -= dt;
                // Второй прыжок в воздухе — чуть слабее первого.
                if (jumpPressed && _canDoubleJump && !_pounding && _coyote <= 0f)
                {
                    _canDoubleJump = false;
                    _velocity.y = JumpVelocity * 0.86f;
                    _flip = 0.0001f;
                    Snd.Play("jump", 1.05f);
                    ParticleFx.Burst(transform.parent, transform.position, 12,
                        new Color(0.85f, 0.95f, 1f), 3.5f);
                }

                // Удар сверху: рывок вниз, пробивает врагов при приземлении.
                if (!_pounding && _poundCd <= 0f &&
                    (Ctrl.ConsumeAttackHeld() || Input.GetKey(KeyCode.LeftControl)))
                {
                    _pounding = true;
                    _poundCd = 0.7f;
                    _velocity = new Vector3(0f, -6f, 0f);
                    Snd.Play("swing", 1.1f);
                }

                _velocity.y -= (_pounding ? Gravity * 2.6f : Gravity) * dt;
                if (_velocity.y < -45f) _velocity.y = -45f;
            }

            // Обычный прыжок доступен и пару кадров после схода с опоры.
            if (jumpPressed && (grounded || _coyote > 0f) && !_pounding)
            {
                _coyote = 0f;
                _velocity.y = JumpVelocity;
                _flip = 0.0001f;
                _wasAirborne = true;
                Snd.Play("jump", 0.85f);
            }
        }

        _cc.Move(_velocity * dt);

        if (dir.magnitude > 0.1f && _attackAnim <= 0f)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float cur = _visual.localEulerAngles.y;
            _visual.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(cur, yaw, Mathf.Min(10f * dt, 1f)), 0f);
        }

        bool attack = Ctrl.ConsumeAttack() || Input.GetKeyDown(KeyCode.E) ||
                      (!Application.isMobilePlatform && Input.GetMouseButtonDown(0));
        if (attack && _attackCd <= 0f) DoAttack();

        if (_attackAnim > 0f) _anim = 3;
        else if (!grounded) _anim = 2;
        else if (new Vector2(_velocity.x, _velocity.z).magnitude > 0.8f) _anim = 1;
        else _anim = 0;

        _sendAccum += dt;
        if (_sendAccum >= 0.05f)
        {
            _sendAccum = 0f;
            if (NetManager.I != null)
                NetManager.I.SendLocalState(transform.position, _visual.localEulerAngles.y, _anim, _flip);
        }
    }

    private void Timers(float dt)
    {
        if (_attackCd > 0f) _attackCd -= dt;
        if (_bounceCd > 0f) _bounceCd -= dt;
        if (_poundCd > 0f) _poundCd -= dt;
        if (_invuln > 0f)
        {
            _invuln -= dt;
            bool show = Mathf.Repeat(_invuln, 0.2f) > 0.1f;
            SetVisible(show);
        }
        else SetVisible(true);
    }

    private void SetVisible(bool v)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].enabled = v;
    }

    private void DoAttack()
    {
        _attackCd = 0.55f;
        _attackAnim = AttackTime;
        Snd.Play("swing", 0.9f);
        if (_model != null) _model.Restart("Bite_InPlace", 1.6f);

        // Рывок вперёд, урон наносится по кругу — целиться не нужно.
        Vector3 fwd = _visual.forward;
        _velocity.x += fwd.x * 3.5f;
        _velocity.z += fwd.z * 3.5f;

        GameRoot root = GameRoot.I;
        if (root == null || root.World == null) return;
        foreach (System.Collections.Generic.KeyValuePair<int, Enemy> kv in root.World.Enemies)
        {
            Enemy e = kv.Value;
            if (e == null || e.Dying) continue;
            Vector3 to = e.transform.position - transform.position;
            if (to.magnitude < 2.5f && Mathf.Abs(to.y) < 1.6f)
                NetManager.I.RequestKill(NetManager.I.CurrentWorld, kv.Key);
        }
    }

    // Приземление после удара сверху бьёт всех врагов вокруг.
    // Короткий толчок камеры — удар ощущается весомее.
    public void Shake(float amount)
    {
        _shake = Mathf.Max(_shake, amount);
    }

    private void PoundImpact()
    {
        Shake(0.35f);
        ParticleFx.Burst(transform.parent, transform.position, 22,
            new Color(1f, 0.9f, 0.6f), 6f);
        GameRoot root = GameRoot.I;
        if (root == null || root.World == null || NetManager.I == null) return;
        foreach (System.Collections.Generic.KeyValuePair<int, Enemy> kv in root.World.Enemies)
        {
            Enemy e = kv.Value;
            if (e == null || e.Dying) continue;
            Vector3 to = e.transform.position - transform.position;
            if (to.magnitude < 4.5f && Mathf.Abs(to.y) < 2.5f)
                NetManager.I.RequestKill(NetManager.I.CurrentWorld, kv.Key);
        }
    }

    private void EnemyInteractions()
    {
        GameRoot root = GameRoot.I;
        if (root == null || root.World == null) return;
        foreach (System.Collections.Generic.KeyValuePair<int, Enemy> kv in root.World.Enemies)
        {
            Enemy e = kv.Value;
            if (e == null || e.Dying) continue;
            Vector3 dv = e.transform.position - transform.position;
            if (new Vector2(dv.x, dv.z).magnitude < 1f && Mathf.Abs(dv.y) < 1.4f)
            {
                if (_velocity.y < -2f && transform.position.y > e.transform.position.y + 0.4f)
                {
                    _velocity.y = 8f;
                    NetManager.I.RequestKill(NetManager.I.CurrentWorld, kv.Key);
                }
                else if (_invuln <= 0f)
                {
                    TakeDamage(e.transform.position);
                }
            }
        }
    }

    private void TakeDamage(Vector3 from)
    {
        Hearts--;
        _invuln = 1.5f;
        _hitAnim = HitTime;
        Snd.Play("hurt", 0.9f);
        Shake(0.3f);
        if (_model != null) _model.Restart("HitRecieve", 1.2f);
        Vector3 push = transform.position - from;
        push.y = 0f;
        if (push.magnitude < 0.01f) push = -_visual.forward;
        push = push.normalized * 8f;
        _velocity = new Vector3(push.x, 6f, push.z);
        if (Hearts <= 0)
        {
            Hearts = NetManager.I != null ? NetManager.I.MaxHearts : 3;
            _invuln = 2f;
            PlaceAt(_spawnPos);
        }
    }

    private void FallRespawn()
    {
        Hearts--;
        if (Hearts <= 0) Hearts = NetManager.I != null ? NetManager.I.MaxHearts : 3;
        _invuln = 2f;
        Snd.Play("hurt", 0.9f);
        PlaceAt(_spawnPos);
    }

    // ---------- Анимация ----------

    private void Animate(float dt)
    {
        if (IsLocal && _flip > 0f)
        {
            _flip += dt * 360f / FlipTime;
            if (_flip >= 360f) _flip = 0f;
        }
        if (_hitAnim > 0f) _hitAnim -= dt;

        float leanTarget = _anim == 1 ? 9f : 0f;
        _lean = Mathf.Lerp(_lean, leanTarget, Mathf.Min(dt * 8f, 1f));
        _flipNode.localRotation = Quaternion.Euler(_flip + _lean, 0f, 0f);

        Vector3 squashTarget = Vector3.one;
        if (_squash > 0f)
        {
            _squash -= dt;
            squashTarget = new Vector3(1.15f, 0.78f, 1.15f);
        }
        _flipNode.localScale = Vector3.Lerp(_flipNode.localScale, squashTarget, Mathf.Min(dt * 14f, 1f));

        if (_attackAnim > 0f)
        {
            _attackAnim -= dt;
            float pr = 1f - Mathf.Clamp01(_attackAnim / AttackTime);
            _visual.localRotation = Quaternion.Euler(0f, _visual.localEulerAngles.y + dt * 360f / AttackTime, 0f);
            _swipe.SetActive(true);
            float s = 0.5f + 1.9f * pr;
            _swipe.transform.localScale = new Vector3(s, s, s);
            if (_swipeMat != null)
                _swipeMat.color = new Color(1f, 1f, 0.9f, 0.55f * (1f - pr));
        }
        else if (_swipe.activeSelf) _swipe.SetActive(false);

        if (_model != null)
        {
            AnimateModel();
            return;
        }

        float runSpeed = _anim == 1 ? 11f : 2f;
        _animT += dt * runSpeed;

        float la = 0f, ra = 0f, laz = 0f, raz = 0f, ll = 0f, rl = 0f, bob = 0f;
        if (_anim == 1)
        {
            ll = Mathf.Sin(_animT) * 54f;
            rl = -ll;
            la = -Mathf.Sin(_animT) * 43f;
            ra = -la;
            bob = Mathf.Abs(Mathf.Sin(_animT)) * 0.07f;
        }
        else if (_anim == 2)
        {
            ll = -34f; rl = 40f; la = -143f; ra = -143f;
        }
        else if (_anim == 3)
        {
            laz = 80f; raz = -80f;
        }
        else
        {
            la = Mathf.Sin(_animT) * 4.5f;
            ra = -la;
        }

        float k = Mathf.Min(dt * 14f, 1f);
        LerpLimb(_lArm, la, laz, k);
        LerpLimb(_rArm, ra, raz, k);
        LerpLimb(_lLeg, ll, 0f, k);
        LerpLimb(_rLeg, rl, 0f, k);
        _body.localPosition = Vector3.Lerp(_body.localPosition, new Vector3(0f, -1f + bob, 0f), k);
    }

    // Клипы модели по состоянию. Удар и урон запускаются отдельно —
    // они перебивают текущий клип и доигрывают сами.
    private void AnimateModel()
    {
        if (_attackAnim > 0f || _hitAnim > 0f) return;

        if (_anim == 2)
        {
            _model.Play("Jump", 1f, false);
            return;
        }
        if (_anim == 1)
        {
            // Клипа бега в паке нет — ускоряем шаг по фактической скорости.
            float v = new Vector2(_velocity.x, _velocity.z).magnitude;
            _model.Play("Walk", Mathf.Clamp(v / 3.2f, 0.9f, 2.1f), true);
            return;
        }
        _model.Play("Idle", 1f, true);
    }

    private static void LerpLimb(Transform limb, float xDeg, float zDeg, float k)
    {
        Vector3 e = limb.localEulerAngles;
        float x = Mathf.LerpAngle(e.x, xDeg, k);
        float z = Mathf.LerpAngle(e.z, zDeg, Mathf.Min(k * 1.5f, 1f));
        limb.localRotation = Quaternion.Euler(x, 0f, z);
    }

    // ---------- Модель ----------

    private Transform Limb(Transform parent, Vector3 pivotPos, float length, float radius,
        Material fur, Material paw)
    {
        GameObject pivot = new GameObject("Limb");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = pivotPos;
        Gfx.Prim(PrimitiveType.Capsule, pivot.transform,
            new Vector3(0f, -length * 0.5f + radius * 0.5f, 0f),
            new Vector3(radius * 2f, length * 0.5f, radius * 2f), fur, false);
        Gfx.Ball(pivot.transform, new Vector3(0f, -length * 0.72f, 0.07f),
            new Vector3(0.22f, 0.13f, 0.28f), paw);
        return pivot.transform;
    }

    // Узлы, общие для модели и запасного медведя из примитивов:
    // Flip крутит сальто и приседание, Body держит ступни на нуле.
    private void BuildNodes()
    {
        GameObject flip = new GameObject("Flip");
        flip.transform.SetParent(_visual, false);
        flip.transform.localPosition = new Vector3(0f, PivotHeight, 0f);
        _flipNode = flip.transform;

        GameObject body = new GameObject("Body");
        body.transform.SetParent(_flipNode, false);
        body.transform.localPosition = new Vector3(0f, -PivotHeight, 0f);
        _body = body.transform;
    }

    // Круг под ногами в цвете игрока — в мультиплеере сразу видно, кто где.
    private void BuildTeamRing()
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Collider col = ring.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);
        ring.name = "TeamRing";
        ring.transform.SetParent(_visual, false);
        ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ring.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        Color c = Color.HSVToRGB(Hue, 0.8f, 1f);
        MeshRenderer mr = ring.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Gfx.AdditiveMat(new Color(c.r, c.g, c.b, IsLocal ? 0.55f : 0.35f),
            Gfx.RingTexture());
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void BuildSwipe()
    {
        // Кольцо ударной волны спин-атаки
        _swipe = new GameObject("Swipe");
        _swipe.transform.SetParent(_visual, false);
        _swipe.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        MeshFilter mf = _swipe.AddComponent<MeshFilter>();
        mf.mesh = Gfx.TorusMesh(0.95f, 0.1f, 20, 8);
        MeshRenderer mr = _swipe.AddComponent<MeshRenderer>();
        _swipeMat = Gfx.AdditiveMat(new Color(1f, 1f, 0.9f, 0.5f), Gfx.WhiteTexture());
        mr.sharedMaterial = _swipeMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        _swipe.SetActive(false);
    }

    // Запасной медведь из примитивов: используется, только если модель
    // не загрузилась, чтобы игра осталась играбельной.
    private void BuildProceduralBear()
    {
        Color furCol = Color.HSVToRGB(Hue, 0.5f, 0.55f);
        Color furLightCol = Color.HSVToRGB(Hue, 0.32f, 0.8f);
        Material fur = Gfx.Mat(furCol, 0.08f);
        Material furLight = Gfx.Mat(furLightCol, 0.1f);
        Material dark = Gfx.Mat(new Color(0.1f, 0.08f, 0.07f), 0.2f);
        Material white = Gfx.Mat(new Color(0.95f, 0.95f, 0.95f), 0.25f);

        // Туловище
        Gfx.Ball(_body, new Vector3(0f, 0.78f, 0f), new Vector3(1f, 1.15f, 0.9f), fur);
        Gfx.Ball(_body, new Vector3(0f, 0.76f, 0.27f), new Vector3(0.6f, 0.8f, 0.45f), furLight);
        Gfx.Ball(_body, new Vector3(0f, 0.68f, -0.42f), new Vector3(0.26f, 0.26f, 0.26f), furLight);

        // Голова
        Gfx.Ball(_body, new Vector3(0f, 1.5f, 0f), new Vector3(0.85f, 0.8f, 0.82f), fur);
        Gfx.Ball(_body, new Vector3(0f, 1.4f, 0.34f), new Vector3(0.4f, 0.3f, 0.32f), furLight);
        Gfx.Ball(_body, new Vector3(0f, 1.47f, 0.49f), new Vector3(0.13f, 0.1f, 0.1f), dark);
        Gfx.Ball(_body, new Vector3(-0.15f, 1.58f, 0.33f), new Vector3(0.11f, 0.13f, 0.06f), white);
        Gfx.Ball(_body, new Vector3(0.15f, 1.58f, 0.33f), new Vector3(0.11f, 0.13f, 0.06f), white);
        Gfx.Ball(_body, new Vector3(-0.15f, 1.58f, 0.37f), new Vector3(0.05f, 0.07f, 0.03f), dark);
        Gfx.Ball(_body, new Vector3(0.15f, 1.58f, 0.37f), new Vector3(0.05f, 0.07f, 0.03f), dark);
        Gfx.Ball(_body, new Vector3(-0.28f, 1.85f, -0.02f), new Vector3(0.26f, 0.26f, 0.15f), fur);
        Gfx.Ball(_body, new Vector3(0.28f, 1.85f, -0.02f), new Vector3(0.26f, 0.26f, 0.15f), fur);
        Gfx.Ball(_body, new Vector3(-0.28f, 1.86f, 0.03f), new Vector3(0.13f, 0.13f, 0.08f), furLight);
        Gfx.Ball(_body, new Vector3(0.28f, 1.86f, 0.03f), new Vector3(0.13f, 0.13f, 0.08f), furLight);

        // Шарф цвета игрока
        Color scarfCol = Color.HSVToRGB(Hue, 0.85f, 0.95f);
        Material scarf = Gfx.MatFull(scarfCol, 0.2f, 0f, scarfCol * 0.35f, 0f, 0f);
        GameObject scarfGo = Gfx.Torus(_body, new Vector3(0f, 1.16f, 0f), 0.33f, 0.11f, scarf);
        scarfGo.transform.localScale = new Vector3(1f, 0.7f, 1f);

        // Конечности
        _lArm = Limb(_body, new Vector3(-0.52f, 1.06f, 0f), 0.52f, 0.13f, fur, furLight);
        _rArm = Limb(_body, new Vector3(0.52f, 1.06f, 0f), 0.52f, 0.13f, fur, furLight);
        _lLeg = Limb(_body, new Vector3(-0.23f, 0.5f, 0f), 0.5f, 0.15f, fur, furLight);
        _rLeg = Limb(_body, new Vector3(0.23f, 0.5f, 0f), 0.5f, 0.15f, fur, furLight);
    }
}
