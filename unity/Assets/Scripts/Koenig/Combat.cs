using System.Collections.Generic;
using UnityEngine;

namespace Koenig
{
    // Бой уровня. До него в игре нельзя было проиграть вовсе: собери
    // шестнадцать неподвижных шаров и подойди к семнадцатому.
    //
    // Устройство простое и намеренно однокнопочное. Кнопка справа бьёт по
    // ближайшему врагу в конусе перед героем — этого хватает, чтобы пройти
    // весь уровень, не тапнув ни разу по цели. Тап по врагу — уточнение:
    // назначает цель, и герой сам доходит до неё, пока джойстик не трогают.
    //
    // Так и было задумано страховкой: если тап по мелкому врагу на телефоне
    // окажется неудобным, играть всё равно можно одной кнопкой.
    //
    // Врагов гоняет тоже отсюда: Enemy.HostStep вызывается снаружи (в игре
    // про медведя это делает WorldBuilder), сам враг себя не двигает.
    public class Combat : MonoBehaviour
    {
        // Урон героя. Числа те же, что у медвежьего удара, — они уже
        // подобраны под ощущение на телефоне. Характеристики появятся
        // шагом позже и будут двигать именно эти значения.
        public int DamageMin = 3;
        public int DamageMax = 6;
        public float Cooldown = 0.55f;
        public float Reach = 2.5f;
        public float ConeDegrees = 120f;
        public float CritChance = 0.1f;

        private KoenigPlayer _player;
        private Camera _cam;

        private readonly List<Enemy> _enemies = new List<Enemy>();
        private Enemy _target;
        private float _cd;

        public System.Action OnRosterChanged;

        public int AliveCount { get { return _enemies.Count; } }

        public static Combat Create(Transform parent, KoenigPlayer player, Camera cam)
        {
            GameObject go = new GameObject("Combat");
            go.transform.SetParent(parent, false);
            Combat c = go.AddComponent<Combat>();
            c._player = player;
            c._cam = cam;
            c.Hook();
            return c;
        }

        // Враг ничего не знает ни про героя, ни про камеру — обе связи
        // выданы ему хуками на шаге 0. Здесь мы их и заполняем.
        private void Hook()
        {
            Enemy.TargetProvider = delegate(Vector3 from)
            {
                if (_player == null || !_player.Alive) return null;
                return _player.transform;
            };
            Enemy.ShakeCamera = delegate(float amount)
            {
                if (_player != null) _player.Shake(amount);
            };
            Enemy.DealDamage = delegate(Enemy who, int amount)
            {
                if (_player != null) _player.TakeDamage(amount);
            };
        }

        private void OnDestroy()
        {
            // Хуки статические: оставить их висеть — значит утащить
            // мёртвого героя в игру про медведя, если её откроют следом.
            Enemy.TargetProvider = null;
            Enemy.ShakeCamera = null;
            Enemy.DealDamage = null;
        }

        public void Add(Enemy e)
        {
            if (e != null) _enemies.Add(e);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_cd > 0f) _cd -= dt;

            StepEnemies(dt);
            if (_player == null || !_player.Alive)
            {
                _target = null;
                if (_player != null) _player.AutoMove = Vector3.zero;
                return;
            }

            PickTarget();
            Approach();

            bool held = KoenigTouch.AttackHeld || Input.GetKey(KeyCode.Space);
            if (held) Swing();
        }

        private void StepEnemies(float dt)
        {
            bool changed = false;
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _enemies[i];
                if (e == null || e.Dying)
                {
                    _enemies.RemoveAt(i);
                    changed = true;
                    continue;
                }
                e.HostStep(dt);
            }
            if (changed && OnRosterChanged != null) OnRosterChanged();
        }

        // ---------- Цель ----------

        private void PickTarget()
        {
            if (_target != null && _target.Dying) _target = null;

            Vector2 tap;
            if (!Targeting.TryTapPoint(out tap)) return;

            float best = Targeting.Tolerance;
            Enemy found = null;
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e == null || e.Dying) continue;
                float gap;
                if (!Targeting.ScreenGap(_cam, e.Center, tap, out gap)) continue;
                if (gap > best) continue;
                best = gap;
                found = e;
            }
            // Тап мимо всех снимает цель — иначе от неё не отвязаться.
            _target = found;
        }

        // Пока цель назначена и джойстик не трогают, герой идёт к ней сам.
        // Трогают — слушаемся пальца: ребёнок всегда главнее автопилота.
        private void Approach()
        {
            if (_player == null) return;
            if (_target == null || _target.Dying)
            {
                _player.AutoMove = Vector3.zero;
                return;
            }

            Vector3 to = _target.Center - _player.transform.position;
            to.y = 0f;
            float stop = Reach + _target.Radius - 0.4f;
            _player.AutoMove = to.magnitude > stop ? to.normalized : Vector3.zero;
        }

        // ---------- Удар ----------

        private void Swing()
        {
            if (_cd > 0f) return;
            _cd = Cooldown;

            Enemy victim = Choose();
            if (victim != null) _player.FaceTowards(victim.Center);
            _player.PlayAttack();
            Snd.Play("swing", 0.7f);

            if (victim == null) return;   // махнул по воздуху — так честнее,
                                          // чем не отзываться на кнопку вовсе

            int dmg = Random.Range(DamageMin, DamageMax + 1);
            bool crit = Random.value < CritChance;
            if (crit) dmg *= 2;

            if (victim.TakeDamage(dmg))
            {
                victim.DieEffect();
                if (_target == victim) _target = null;
                _enemies.Remove(victim);
                if (OnRosterChanged != null) OnRosterChanged();
            }
            else Snd.Play(crit ? "bosshit" : "stomp", crit ? 0.9f : 0.6f);
        }

        // Кого бьём: назначенную цель, если она в досягаемости, иначе
        // ближайшего в конусе перед героем. Именно второе делает игру
        // проходимой без единого тапа по врагу.
        private Enemy Choose()
        {
            if (_target != null && !_target.Dying && InReach(_target)) return _target;

            Vector3 me = _player.transform.position;
            Vector3 facing = _player.Facing;
            float bestDist = float.MaxValue;
            Enemy best = null;

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e == null || e.Dying) continue;
                if (!InReach(e)) continue;

                Vector3 to = e.Center - me;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) { best = e; break; }
                // Конус через скалярное произведение: косинус половины угла
                // — та же проверка, что и Vector3.Angle, но без градусов.
                float dot = Vector3.Dot(facing.normalized, to.normalized);
                if (dot < Mathf.Cos(ConeDegrees * 0.5f * Mathf.Deg2Rad)) continue;
                float d = to.magnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = e;
            }
            return best;
        }

        private bool InReach(Enemy e)
        {
            Vector3 to = e.Center - _player.transform.position;
            if (Mathf.Abs(to.y) > 1.6f + e.Height * 0.5f) return false;
            to.y = 0f;
            return to.magnitude <= Reach + e.Radius;
        }
    }
}
