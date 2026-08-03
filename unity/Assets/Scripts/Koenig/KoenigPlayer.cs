using UnityEngine;

namespace Koenig
{
    // Управляемый герой игрового уровня. Ребёнок играет за самого Кёню —
    // город кошек Зеленоградск как раз про это, а модель проводника у нас
    // уже есть со всеми клипами. Управление простое: джойстик слева,
    // прыжок справа (см. TouchControls), на десктопе — WASD/пробел.
    //
    // Одиночный и самодостаточный: ни сети, ни врагов, ни блоков — в
    // отличие от BearPlayer. Гравитация и прыжок считаются вручную поверх
    // CharacterController, камера едет следом.
    public class KoenigPlayer : MonoBehaviour
    {
        private const float Speed = 5.5f;
        private const float Accel = 12f;
        private const float JumpVelocity = 8.5f;
        private const float Gravity = 20f;

        private CharacterController _cc;
        private Transform _visual;
        private CharacterModel _model;

        private Transform _camYaw;
        private Camera _cam;

        private Vector3 _velocity;
        private Vector3 _spawn;
        private float _coyote;
        private float _animT;
        private byte _anim;
        private float _camYawAngle;

        public Camera Cam { get { return _cam; } }

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

            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            _visual = vis.transform;

            _model = CharacterModel.Spawn(_visual, modelId, 1.5f);
            if (_model != null) _model.Play(_model.Pick("Idle", "Walk"), 1f, true);

            BuildCamera();
        }

        private void BuildCamera()
        {
            GameObject yaw = new GameObject("CamYaw");
            yaw.transform.SetParent(null, false);
            _camYaw = yaw.transform;

            GameObject camGo = new GameObject("KoenigLevelCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(_camYaw, false);
            camGo.transform.localPosition = new Vector3(0f, 3.4f, -6.2f);
            camGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
            _cam.fieldOfView = 58f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 300f;
            // Выше depth камеры-хаба: уровень целиком перекрывает экран
            // карты, пока играем. Канвас карты при этом прячется отдельно.
            _cam.depth = 10f;

            // Звук в игре про мосты нигде не слушался — на уровне даём
            // слушатель, чтобы прыжки и монетки было слышно.
            if (Object.FindObjectOfType<AudioListener>() == null)
                camGo.AddComponent<AudioListener>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Move(dt);
            FollowCamera(dt);
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

            // Движение относительно камеры: «вперёд» на джойстике — от игрока
            // вглубь экрана, куда бы камера ни смотрела.
            Quaternion camRot = Quaternion.Euler(0f, _camYawAngle, 0f);
            Vector3 dir = camRot * new Vector3(mv.x, 0f, mv.y);
            if (dir.magnitude > 1f) dir = dir.normalized;

            float k = Mathf.Min(Accel * dt, 1f);
            _velocity.x = Mathf.Lerp(_velocity.x, dir.x * Speed, k);
            _velocity.z = Mathf.Lerp(_velocity.z, dir.z * Speed, k);

            bool jump = Ctrl.ConsumeJump() || Input.GetKeyDown(KeyCode.Space);
            bool grounded = _cc.isGrounded;
            if (grounded)
            {
                _coyote = 0.12f;
                if (_velocity.y < 0f) _velocity.y = -2f;
            }
            else
            {
                _coyote -= dt;
                _velocity.y -= Gravity * dt;
                if (_velocity.y < -40f) _velocity.y = -40f;
            }

            if (jump && (grounded || _coyote > 0f))
            {
                _coyote = 0f;
                _velocity.y = JumpVelocity;
                Snd.Play("jump", 0.8f);
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
            if (!grounded) _anim = 2;
            else if (flat > 0.8f) _anim = 1;
            else _anim = 0;
        }

        private void FollowCamera(float dt)
        {
            if (_camYaw == null) return;
            // Камера мягко догоняет героя и разворачивается по направлению
            // движения — отдельный джойстик поворота ребёнку не нужен.
            _camYaw.position = Vector3.Lerp(_camYaw.position,
                transform.position + new Vector3(0f, 0.6f, 0f), Mathf.Min(8f * dt, 1f));

            float flat = new Vector2(_velocity.x, _velocity.z).magnitude;
            if (flat > 1.2f)
            {
                float target = Mathf.Atan2(_velocity.x, _velocity.z) * Mathf.Rad2Deg;
                _camYawAngle = Mathf.LerpAngle(_camYawAngle, target, Mathf.Min(2.2f * dt, 1f));
            }
            _camYaw.localRotation = Quaternion.Euler(0f, _camYawAngle, 0f);
        }

        private void Animate(float dt)
        {
            if (_model != null)
            {
                if (_anim == 2) _model.Play("Jump", 1f, false);
                else if (_anim == 1)
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
            Snd.Play("land", 0.6f);
        }

        private void OnDestroy()
        {
            if (_camYaw != null) Object.Destroy(_camYaw.gameObject);
        }
    }
}
