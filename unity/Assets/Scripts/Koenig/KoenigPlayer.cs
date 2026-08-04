using UnityEngine;

namespace Koenig
{
    // Управляемый герой игрового уровня. Ребёнок играет за самого Кёню —
    // город кошек Зеленоградск как раз про это, а модель проводника у нас
    // уже есть со всеми клипами.
    //
    // Камера изометрическая, как в RPG: висит высоко, смотрит вниз под
    // постоянным углом и НИКОГДА не поворачивается — только едет за героем.
    // Раньше она сидела за плечом на высоте 3.4 м и доворачивалась по
    // направлению бега, а джойстик считался от её текущего угла; камера,
    // движение и управление были связаны в один узел, и в бою это узел
    // мешал бы: цель, по которой тапнул ребёнок, уезжала бы из-под пальца
    // вместе с разворотом камеры.
    //
    // Теперь угол площадки задан одной константой CamYaw, и от неё же
    // считается джойстик — разойтись они не могут по устройству.
    //
    // Прыжка нет: в изометрической RPG прыгать некуда, а кнопка справа
    // нужна под удар. Гравитация осталась — она держит героя на земле и на
    // ступенях.
    public class KoenigPlayer : MonoBehaviour
    {
        public const float BaseSpeed = 5.5f;
        private const float Accel = 12f;
        private const float Gravity = 20f;

        // Разворот площадки на экране. Ноль — мировая ось Z смотрит вверх
        // экрана. Улица Зеленоградска идёт вдоль Z и построена коридором,
        // поэтому здесь ноль: камера смотрит вдоль улицы, и та занимает
        // кадр целиком. Для зон, которые будут строиться под изометрию с
        // нуля, сюда ставится 45 — тогда у домов видно два угла, а не
        // плоский фасад. Менять надо ТОЛЬКО здесь: и камера, и джойстик
        // читают эту же константу.
        private const float CamYaw = 0f;
        private const float CamPitch = 50f;

        // ОРТОГРАФИЯ, и это решение с последствиями.
        //
        // Перспективная камера висела на 14 м — то есть на уровне конька
        // девятиметровых домов, с зазором в два метра. Стоило ребёнку
        // сойти с мостовой, и камера въезжала в кровлю: весь экран
        // занимала изнанка крыши. Поднять её было нельзя — герой при этом
        // мельчал вдвое.
        //
        // При ортографии высота камеры НЕ ВЛИЯЕТ на размер кадра вообще:
        // его задаёт только OrthoSize. Поэтому камеру можно унести сколь
        // угодно далеко, и попасть внутрь геометрии она уже не сможет —
        // не «маловероятно», а по устройству.
        //
        // ПОЧЕМУ 40, А НЕ 22. Первая попытка ставила 22 м, и появилась
        // новая беда, которой при перспективе не бывает: ближняя
        // плоскость отсечения при ортографии режет всё, что оказалось «за»
        // камерой по оси взгляда. Камера стояла в 18.5 м южнее героя, и
        // дом высотой 12 м южнее его же на 11 м уходил за эту плоскость —
        // на экране появлялся прямой вертикальный обрез посреди кадра.
        // Сорок метров и дальняя плоскость 120 вмещают всю улицу в слой
        // между плоскостями с запасом.
        //
        // Плата за саму ортографию: пропадает перспектива, дальний дом
        // ровно того же размера, что ближний. Это честная изометрия
        // Diablo 2; глубину теперь держат туман, разная этажность и
        // наложение силуэтов.
        private const float CamHeight = 40f;
        private const float CamBack = 33.6f;   // = 40 / tan(50°), ось смотрит в героя
        private const float CamFollow = 6f;

        // Половина вертикали кадра в метрах. 6.6 даёт 13.2 м по вертикали:
        // герой ростом 1.85 м при наклоне камеры 50° занимает на экране
        // 1.19 м, то есть девятую часть высоты кадра — посадка Diablo 2.
        // При 5.5 в кадр помещался ОДИН дом и больше ничего.
        private const float OrthoSize = 6.6f;

        private CharacterController _cc;
        private Transform _visual;
        private CharacterModel _model;

        private Transform _camRig;
        private Camera _cam;
        private Vector3 _focus;

        private Vector3 _velocity;
        private Vector3 _spawn;
        private float _animT;
        private byte _anim;

        // ---------- Жизнь ----------
        // До этого шага проиграть было нельзя вовсе: единственной бедой
        // было падение ниже -20, и оно возвращало героя без потерь.
        //
        // Штраф за смерть держим символическим намеренно. Семилетнему
        // ребёнку важно, чтобы поражение было заметным, но не обидным:
        // герой встаёт у входа с полным здоровьем, а враги на поляне НЕ
        // воскресают — пройденное остаётся пройденным.
        public const int BaseHp = 40;

        private int _maxHp = BaseHp;
        private float _speed = BaseSpeed;
        private int _hp = BaseHp;
        private float _invuln;      // короткая неуязвимость после удара
        private float _atkAnim;     // сколько ещё не перебивать клип удара

        public int Hp { get { return _hp; } }
        public int MaxHp { get { return _maxHp; } }
        public float HpFraction { get { return Mathf.Clamp01((float)_hp / _maxHp); } }
        public bool Alive { get { return _hp > 0; } }

        // Кому сообщать, что здоровье изменилось — HUD рисует полоску.
        public System.Action OnHealthChanged;

        // Автоподход к цели. Combat кладёт сюда мировое направление, и оно
        // работает ТОЛЬКО пока джойстик не трогают: палец ребёнка всегда
        // главнее автопилота.
        public Vector3 AutoMove;

        private float _shake;

        public Camera Cam { get { return _cam; } }

        // Куда смотрит герой — по этому направлению Combat отбирает конус.
        public Vector3 Facing
        {
            get { return _visual != null ? _visual.forward : Vector3.forward; }
        }

        // Короткая тряска камеры: попадание по боссу без неё не читается.
        public void Shake(float amount)
        {
            _shake = Mathf.Max(_shake, amount);
        }

        // Куда смотрит площадка. Нужен прицеливанию: экранный тап надо
        // разворачивать в те же оси, в которых ходит герой.
        public static Quaternion GroundRotation
        {
            get { return Quaternion.Euler(0f, CamYaw, 0f); }
        }

        public static KoenigPlayer Spawn(Transform parent, Vector3 pos, string modelId)
        {
            GameObject go = new GameObject("KoenigPlayer");
            go.transform.SetParent(parent, false);
            KoenigPlayer p = go.AddComponent<KoenigPlayer>();
            p.Init(pos, modelId);
            return p;
        }

        private void Init(Vector3 pos, string modelId)
        {
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.height = 1.6f;
            _cc.radius = 0.35f;
            _cc.center = new Vector3(0f, 0.8f, 0f);
            _cc.slopeLimit = 55f;
            _cc.stepOffset = 0.5f;

            transform.position = pos;
            _spawn = pos;
            _focus = pos;

            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            _visual = vis.transform;

            // 1.85 — тот же рост, что записан рыцарю в Heroes.EnemyHeight.
            // Полтора метра достались от кота-проводника, и с ними герой
            // оказывался НИЖЕ собственных врагов (1.55–1.65).
            _model = CharacterModel.Spawn(_visual, modelId, 1.85f);
            if (_model != null) _model.Play(_model.Pick("Idle", "Walk"), 1f, true);

            BuildCamera();
            PlaceCamera(1f);
        }

        private void BuildCamera()
        {
            // Камера — самостоятельный объект в корне сцены, а не потомок
            // поворотного узла: поворачивать её больше нечему.
            GameObject camGo = new GameObject("KoenigLevelCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(null, false);
            camGo.transform.localRotation = Quaternion.Euler(CamPitch, CamYaw, 0f);
            _camRig = camGo.transform;

            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
            _cam.orthographic = true;
            _cam.orthographicSize = OrthoSize;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 120f;
            // Выше depth камеры-хаба: уровень целиком перекрывает экран
            // карты, пока играем. Канвас карты при этом прячется отдельно.
            _cam.depth = 10f;

            if (Object.FindObjectOfType<AudioListener>() == null)
                camGo.AddComponent<AudioListener>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_invuln > 0f) _invuln -= dt;
            if (_atkAnim > 0f) _atkAnim -= dt;
            Move(dt);
            PlaceCamera(Mathf.Min(CamFollow * dt, 1f));
            Animate(dt);
            if (transform.position.y < -20f) Respawn();
        }

        // ---------- Урон и смерть ----------

        public void TakeDamage(int amount)
        {
            if (_hp <= 0 || _invuln > 0f) return;

            _hp -= Mathf.Max(1, amount);
            // Полсекунды неуязвимости: иначе пачка из четырёх грибов
            // снимает всё здоровье за один общий замах, и понять, что
            // произошло, невозможно.
            _invuln = 0.5f;
            Snd.Play("hurt", 0.8f);
            if (_model != null)
            {
                _model.Restart(_model.Pick("HitRecieve", "Jump"), 1.2f);
                _atkAnim = 0.3f;
            }
            if (OnHealthChanged != null) OnHealthChanged();

            if (_hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (_hp <= 0) return;
            _hp = Mathf.Min(_maxHp, _hp + Mathf.Max(1, amount));
            if (OnHealthChanged != null) OnHealthChanged();
        }

        private void Die()
        {
            _hp = 0;
            if (_model != null)
            {
                _model.Restart(_model.Pick("Death", "HitRecieve"), 1f);
                _atkAnim = 1.2f;
            }
            Snd.Play("hurt", 1f, 0.7f);
            if (OnHealthChanged != null) OnHealthChanged();
            Invoke("Revive", 1.6f);
        }

        private void Revive()
        {
            _hp = _maxHp;
            _invuln = 1.5f;
            _atkAnim = 0f;
            Respawn();
            if (OnHealthChanged != null) OnHealthChanged();
        }

        // Пересчёт от надетого. Зовёт бой при любой смене снаряжения.
        public void ApplyGear(int extraHp, float speedMultiplier)
        {
            int was = _maxHp;
            _maxHp = BaseHp + Mathf.Max(0, extraHp);
            // Максимум вырос — столько же добавляем текущему здоровью.
            // Иначе надел доспех и остался с прежней полоской на шкале,
            // которая стала длиннее: выглядит как будто отняли.
            if (_maxHp > was) _hp += _maxHp - was;
            _hp = Mathf.Clamp(_hp, 0, _maxHp);

            _speed = BaseSpeed * Mathf.Clamp(speedMultiplier, 0.5f, 2f);
            if (OnHealthChanged != null) OnHealthChanged();
        }

        // Замах: проигрывает клип и не даёт ходьбе перебить его. Сам урон
        // считает Combat — герой только машет.
        public void PlayAttack()
        {
            if (_model == null) return;
            _model.Restart(_model.Pick("Bite_InPlace", "Attack", "Jump"), 1.4f);
            _atkAnim = 0.35f;
        }

        // Развернуть героя к цели перед ударом — бить в спину странно.
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            _visual.localRotation = Quaternion.Euler(0f,
                Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
        }

        private void Move(float dt)
        {
            // Мёртвый не ходит, но продолжает падать — иначе он повиснет
            // в воздухе на те полторы секунды, что лежит.
            if (_hp <= 0)
            {
                _velocity.x = 0f;
                _velocity.z = 0f;
                if (!_cc.isGrounded) _velocity.y -= Gravity * dt;
                else if (_velocity.y < 0f) _velocity.y = -2f;
                _cc.Move(_velocity * dt);
                _anim = 0;
                return;
            }

            Vector2 mv = Ctrl.Move;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) mv.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) mv.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) mv.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) mv.x += 1f;
            if (mv.magnitude > 1f) mv = mv.normalized;

            // Джойстик считается от неподвижного угла площадки, а не от
            // камеры: «вверх» на джойстике — всегда вверх экрана, куда бы
            // герой ни бежал. Раньше при развороте камеры одно и то же
            // положение пальца означало разные стороны света.
            Vector3 dir = GroundRotation * new Vector3(mv.x, 0f, mv.y);
            // Джойстик не тронут — ведёт автоподход к назначенной цели.
            if (mv.magnitude < 0.05f && AutoMove.sqrMagnitude > 0.01f)
                dir = AutoMove.normalized;
            if (dir.magnitude > 1f) dir = dir.normalized;

            float k = Mathf.Min(Accel * dt, 1f);
            _velocity.x = Mathf.Lerp(_velocity.x, dir.x * _speed, k);
            _velocity.z = Mathf.Lerp(_velocity.z, dir.z * _speed, k);

            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                if (_velocity.y < 0f) _velocity.y = -2f;
            }
            else
            {
                _velocity.y -= Gravity * dt;
                if (_velocity.y < -40f) _velocity.y = -40f;
            }

            _cc.Move(_velocity * dt);

            // Поворот модели по направлению бега.
            if (dir.magnitude > 0.1f)
            {
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float cur = _visual.localEulerAngles.y;
                _visual.localRotation = Quaternion.Euler(0f,
                    Mathf.LerpAngle(cur, yaw, Mathf.Min(12f * dt, 1f)), 0f);
            }

            float flat = new Vector2(_velocity.x, _velocity.z).magnitude;
            _anim = flat > 0.8f ? (byte)1 : (byte)0;
        }

        // Камера едет за героем и только за ним: разворот задан один раз
        // при постройке и больше не трогается.
        private void PlaceCamera(float k)
        {
            if (_camRig == null) return;
            _focus = Vector3.Lerp(_focus, transform.position, k);
            Vector3 pos = _focus + GroundRotation * new Vector3(0f, CamHeight, -CamBack);

            if (_shake > 0.001f)
            {
                _shake = Mathf.Max(0f, _shake - Time.deltaTime * 1.6f);
                float a = _shake * 0.5f;
                pos += new Vector3(Random.Range(-a, a), Random.Range(-a, a), 0f);
            }
            _camRig.position = pos;
        }

        private void Animate(float dt)
        {
            // Пока играет удар или получение урона, ходьба его не перебивает:
            // Play() меняет клип, как только имя другое, и без этой паузы
            // замах гас через кадр.
            if (_atkAnim > 0f) { _animT += dt; return; }

            if (_model != null)
            {
                if (_anim == 1)
                {
                    float v = new Vector2(_velocity.x, _velocity.z).magnitude;
                    _model.Play("Walk", Mathf.Clamp(v / 3f, 0.9f, 2f), true);
                }
                else _model.Play("Idle", 1f, true);
            }
            _animT += dt;
        }

        private void Respawn()
        {
            _cc.enabled = false;
            transform.position = _spawn;
            _cc.enabled = true;
            _velocity = Vector3.zero;
            _focus = _spawn;
            PlaceCamera(1f);
            Snd.Play("land", 0.6f);
        }

        private void OnDestroy()
        {
            if (_camRig != null) Object.Destroy(_camRig.gameObject);
        }
    }
}
