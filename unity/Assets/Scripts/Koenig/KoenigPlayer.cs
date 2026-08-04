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
        private const float Speed = 5.5f;
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
        private const float CamHeight = 14f;
        private const float CamBack = 11.6f;
        private const float CamFollow = 6f;

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

        public Camera Cam { get { return _cam; } }

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

            _model = CharacterModel.Spawn(_visual, modelId, 1.5f);
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
            // Узкий угол — подпись изометрии: перспектива почти не
            // расходится, дальний дом такого же размера, как ближний.
            _cam.fieldOfView = 45f;
            _cam.nearClipPlane = 0.1f;
            // Камера смотрит вниз под 50°, поэтому земля уходит за верхний
            // край кадра метрах в тридцати впереди — держать 300 незачем.
            // Берём 90 с запасом: столько хватает высоким предметам, что
            // торчат над этой границей, и на глаз ничто не выскакивает из
            // ниоткуда.
            //
            // Плата за изометрию, о которой надо знать: камера висит на 14 м
            // над героем и смотрит ВНИЗ, значит всё выше её самой в кадр не
            // попадает вовсе. Башня Мурариума в этом уровне 16-метровая —
            // её верхушку теперь не видно, видно основание. Это не баг
            // дальней плоскости, это угол; чинится не числом здесь, а тем,
            // что ориентиры строятся под изометрию (шаг 5 плана).
            _cam.farClipPlane = 90f;
            // Выше depth камеры-хаба: уровень целиком перекрывает экран
            // карты, пока играем. Канвас карты при этом прячется отдельно.
            _cam.depth = 10f;

            if (Object.FindObjectOfType<AudioListener>() == null)
                camGo.AddComponent<AudioListener>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Move(dt);
            PlaceCamera(Mathf.Min(CamFollow * dt, 1f));
            Animate(dt);
            if (transform.position.y < -20f) Respawn();
        }

        private void Move(float dt)
        {
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
            if (dir.magnitude > 1f) dir = dir.normalized;

            float k = Mathf.Min(Accel * dt, 1f);
            _velocity.x = Mathf.Lerp(_velocity.x, dir.x * Speed, k);
            _velocity.z = Mathf.Lerp(_velocity.z, dir.z * Speed, k);

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
            _camRig.position = _focus + GroundRotation * new Vector3(0f, CamHeight, -CamBack);
        }

        private void Animate(float dt)
        {
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
