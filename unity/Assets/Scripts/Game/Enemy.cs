using UnityEngine;

// Враг (гриб или ледяной слизень): патрулирует между двумя точками.
// Движение считает хост, клиенты интерполируют полученные позиции.
public class Enemy : MonoBehaviour
{
    public int Id;
    public string Kind = "mushroom";
    public float Speed = 2f;
    public Vector3 PointA;
    public Vector3 PointB;
    public bool Dying;
    // Боссы держат несколько ударов; обычный враг умирает с одного.
    public int Hp = 1;
    public bool IsBoss;
    // Марш: враг идёт в PointB и обратно не поворачивает. Так ходят
    // волны в обороне деревни — им нужен не патруль, а цель.
    public bool March;
    // Упёрся в поставленный игроками блок и грызёт его. Признак ставит
    // и снимает оборона деревни; враг при этом стоит намертво, даже
    // если рядом игрок — иначе стену можно было бы обойти, просто
    // показавшись из-за неё.
    public bool Blocked;

    // ---------- Свой удар ----------
    // В игре про медведя урон снимало касание, и считал его сам игрок
    // (BearPlayer.EnemyInteractions): враг просто ехал вперёд телом. Для
    // RPG нужен обратный ход — враг останавливается на дистанции оружия и
    // бьёт по кулдауну, стоя на месте.
    //
    // Damage = 0 означает «не бьёт сам», и это значение по умолчанию:
    // медвежьи миры продолжают работать ровно как раньше.
    public int Damage;
    public float AttackCooldown = 1.4f;
    public float AttackRange = 1.2f;

    // Пауза между началом замаха и уроном. Без неё удар прилетал В ТОТ ЖЕ
    // КАДР, что и начало анимации: ребёнок физически не успевал ничего
    // сделать — здоровье просто таяло, и бой читался как «бьют издалека».
    // 0.35 с — примерно контактный кадр клипа; за это время видно, кто
    // замахнулся, и можно отойти.
    public float WindUp = 0.35f;

    private float _swing = -1f;   // сколько осталось до удара, -1 — не замахивался

    // Кому уходит удар. Врагу знать про героя незачем — как и с целью.
    public static System.Action<Enemy, int> DealDamage;

    private float _atkCd;

    private Transform _visual;
    private Vector3 _target;
    private Vector3 _netPos;
    private float _netYaw;
    private bool _hasNet;
    private float _bob;
    private float _dieT = -1f;
    private Transform _wingL;
    private Transform _wingR;
    private CharacterModel _model;
    private BlockMob.Rig _rig;
    private bool _flying;
    private bool _chasing;

    public float Yaw { get { return _visual != null ? _visual.localEulerAngles.y : 0f; } }

    // ---------- Геометрия тела ----------
    // transform.position — это точка патрулирования на земле. Модель может
    // стоять выше (летающие подняты на 0.9) и быть крупнее (боссы масштабированы).
    // Урон и удары считаются по этим свойствам, а не по transform.position,
    // иначе зона удара висит под врагом и мимо крупного босса можно пройти.

    public float Scale { get { return transform.localScale.x; } }

    public float Height { get { return Heroes.EnemyHeight(Kind) * transform.localScale.y; } }

    // Центр тела в мировых координатах.
    public Vector3 Center
    {
        get
        {
            Vector3 basePos = _visual != null ? _visual.position : transform.position;
            return basePos + Vector3.up * (Height * 0.5f);
        }
    }

    // Радиус тела по горизонтали: у босса шире ровно во столько раз,
    // во сколько он крупнее.
    public float Radius { get { return 0.55f * Scale; } }

    public static Enemy SpawnBoss(Transform parent, Vector3 a, Vector3 b, string kind,
        float speed, int id, int hp, float scale)
    {
        Enemy e = Spawn(parent, a, b, kind, speed, id);
        e.IsBoss = true;
        e.Hp = hp;
        e.transform.localScale = new Vector3(scale, scale, scale);
        WorldLabel.Attach(e.transform, "Босс", new Vector3(0f, 3.2f, 0f),
            new Color(1f, 0.5f, 0.4f), 24);
        return e;
    }

    public static Enemy Spawn(Transform parent, Vector3 a, Vector3 b, string kind, float speed, int id)
    {
        GameObject go = new GameObject("Enemy_" + id);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = a;
        Enemy e = go.AddComponent<Enemy>();
        e.Id = id;
        e.Kind = kind;
        e.Speed = speed;
        e.PointA = a;
        e.PointB = b;
        e.Init();
        return e;
    }

    private void Init()
    {
        _target = PointB;
        _netPos = transform.position;
        _bob = Random.Range(0f, 6.28f);

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(transform, false);
        _visual = vis.transform;

        // Кубические мобы собираются коробками и до готовых моделей не
        // доходят: EnemyModel для них вернул бы гриба по умолчанию.
        if (BlockMob.Handles(Kind))
        {
            _rig = BlockMob.Build(_visual, Kind);
            return;
        }

        _model = CharacterModel.Spawn(_visual, Heroes.EnemyModel(Kind), Heroes.EnemyHeight(Kind));
        if (_model != null)
        {
            // Летающие висят над землёй, у них нет клипов ходьбы.
            _flying = Heroes.Flies(Kind);
            if (_flying) _visual.localPosition = new Vector3(0f, 0.9f, 0f);
            _model.Play(_model.Pick("Walk", "Flying", "Idle"), 1f, true);
            return;
        }

        if (Kind == "slime") BuildSlime();
        else if (Kind == "beetle") BuildBeetle();
        else if (Kind == "bat") BuildBat();
        else BuildMushroom();
    }

    // Пустынный жук: панцирь, лапки, усики.
    private void BuildBeetle()
    {
        Material shell = Gfx.RimMat(new Color(0.35f, 0.22f, 0.5f), new Color(0.8f, 0.6f, 1f), 0.45f, 0.6f);
        Material legs = Gfx.Mat(new Color(0.16f, 0.1f, 0.2f), 0.2f);
        Material eye = Gfx.MatFull(new Color(1f, 0.75f, 0.3f), 0.5f, 0f, new Color(0.8f, 0.5f, 0.1f), 0f, 0f);

        Gfx.Ball(_visual, new Vector3(0f, 0.42f, 0f), new Vector3(1.05f, 0.62f, 1.25f), shell);
        Gfx.Ball(_visual, new Vector3(0f, 0.5f, -0.15f), new Vector3(0.5f, 0.4f, 0.6f), legs);
        Gfx.Ball(_visual, new Vector3(0f, 0.4f, 0.62f), new Vector3(0.52f, 0.42f, 0.42f), legs);
        Gfx.Ball(_visual, new Vector3(-0.14f, 0.5f, 0.78f), new Vector3(0.13f, 0.14f, 0.1f), eye);
        Gfx.Ball(_visual, new Vector3(0.14f, 0.5f, 0.78f), new Vector3(0.13f, 0.14f, 0.1f), eye);

        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject leg = Gfx.Cyl(_visual,
                    new Vector3(0.5f * s, 0.2f, -0.35f + i * 0.42f),
                    new Vector3(0.09f, 0.24f, 0.09f), legs, false);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, 42f * s);
            }
        }
        // Рожки-усики
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject horn = Gfx.Cyl(_visual, new Vector3(0.16f * s, 0.68f, 0.6f),
                new Vector3(0.06f, 0.22f, 0.06f), legs, false);
            horn.transform.localRotation = Quaternion.Euler(35f, 0f, 18f * s);
        }
    }

    // Пещерная летучая мышь: парит над землёй, машет крыльями.
    private void BuildBat()
    {
        Material body = Gfx.RimMat(new Color(0.24f, 0.18f, 0.3f), new Color(0.7f, 0.55f, 1f), 0.5f, 0.3f);
        Material eye = Gfx.MatFull(new Color(1f, 0.4f, 0.5f), 0.6f, 0f, new Color(0.9f, 0.2f, 0.3f), 0f, 0f);

        Gfx.Ball(_visual, new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.66f, 0.7f), body);
        Gfx.Ball(_visual, new Vector3(-0.13f, 0.82f, 0.3f), new Vector3(0.12f, 0.13f, 0.08f), eye);
        Gfx.Ball(_visual, new Vector3(0.13f, 0.82f, 0.3f), new Vector3(0.12f, 0.13f, 0.08f), eye);
        Gfx.Ball(_visual, new Vector3(-0.22f, 1.06f, -0.05f), new Vector3(0.2f, 0.3f, 0.1f), body);
        Gfx.Ball(_visual, new Vector3(0.22f, 1.06f, -0.05f), new Vector3(0.2f, 0.3f, 0.1f), body);

        _wingL = new GameObject("WingL").transform;
        _wingL.SetParent(_visual, false);
        _wingL.localPosition = new Vector3(-0.28f, 0.78f, 0f);
        Gfx.Box(_wingL, new Vector3(-0.42f, 0f, 0f), new Vector3(0.9f, 0.07f, 0.6f), body, false);

        _wingR = new GameObject("WingR").transform;
        _wingR.SetParent(_visual, false);
        _wingR.localPosition = new Vector3(0.28f, 0.78f, 0f);
        Gfx.Box(_wingR, new Vector3(0.42f, 0f, 0f), new Vector3(0.9f, 0.07f, 0.6f), body, false);

        Gfx.Glow(_visual, new Vector3(0f, 0.8f, 0f), 2.2f, new Color(0.6f, 0.4f, 1f, 0.3f));
    }

    private void BuildMushroom()
    {
        Gfx.Cyl(_visual, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.25f, 0.5f),
            Gfx.Mat(new Color(0.93f, 0.87f, 0.7f)), false);
        Gfx.Ball(_visual, new Vector3(0f, 0.62f, 0f), new Vector3(0.85f, 0.5f, 0.85f),
            Gfx.Mat(new Color(0.85f, 0.2f, 0.15f)));

        Material dots = Gfx.Mat(new Color(0.95f, 0.95f, 0.9f));
        Gfx.Ball(_visual, new Vector3(0.2f, 0.78f, 0.1f), new Vector3(0.12f, 0.08f, 0.12f), dots);
        Gfx.Ball(_visual, new Vector3(-0.18f, 0.76f, -0.12f), new Vector3(0.1f, 0.07f, 0.1f), dots);
        Gfx.Ball(_visual, new Vector3(0f, 0.8f, -0.2f), new Vector3(0.09f, 0.06f, 0.09f), dots);

        Material eye = Gfx.Mat(new Color(0.1f, 0.07f, 0.06f));
        Gfx.Ball(_visual, new Vector3(-0.1f, 0.42f, 0.22f), new Vector3(0.08f, 0.1f, 0.05f), eye);
        Gfx.Ball(_visual, new Vector3(0.1f, 0.42f, 0.22f), new Vector3(0.08f, 0.1f, 0.05f), eye);
    }

    private void BuildSlime()
    {
        Material body = Gfx.MatFull(new Color(0.5f, 0.78f, 1f), 0.85f, 0.15f,
            new Color(0.12f, 0.28f, 0.55f), 0f, 0f);
        Gfx.Ball(_visual, new Vector3(0f, 0.4f, 0f), new Vector3(0.9f, 0.75f, 0.9f), body);

        Material eye = Gfx.Mat(new Color(0.08f, 0.1f, 0.2f));
        Gfx.Ball(_visual, new Vector3(-0.14f, 0.5f, 0.35f), new Vector3(0.1f, 0.12f, 0.06f), eye);
        Gfx.Ball(_visual, new Vector3(0.14f, 0.5f, 0.35f), new Vector3(0.1f, 0.12f, 0.06f), eye);
        Gfx.Glow(_visual, new Vector3(0f, 0.4f, 0f), 1.5f, new Color(0.4f, 0.7f, 1f, 0.35f));
    }

    // Патрулирование и погоня — считает только хост.
    public void HostStep(float dt)
    {
        if (Dying) return;
        Vector3 pos = transform.localPosition;

        Vector3 chase;
        bool sees = FindChaseTarget(pos, out chase);
        if (sees != _chasing)
        {
            _chasing = sees;
            // Пока враг гонится, шагает он чаще — заметно без единого значка.
            if (_model != null)
                _model.Play(_model.Pick("Run", "Walk", "Flying", "Idle"), sees ? 1.5f : 1f, true);
        }

        if (_atkCd > 0f) _atkCd -= dt;

        // Замах доводится независимо от того, дошёл ли враг: отбежать от
        // уже начатого удара можно, а вот отменить его нельзя — иначе враг
        // будет вечно замахиваться и никогда не попадать.
        if (_swing >= 0f)
        {
            _swing -= dt;
            if (_swing < 0f)
            {
                _swing = -1f;
                Vector3 reach = chase - pos;
                reach.y = 0f;
                if (_chasing && reach.magnitude <= AttackRange + Radius + 0.6f
                    && DealDamage != null) DealDamage(this, Damage);
            }
        }

        Vector3 goal;
        float speed = Speed;
        if (_chasing)
        {
            goal = chase;
            // Быстрее игрока (6.5) враг быть не должен: от погони надо
            // иметь возможность уйти. Без потолка боссы разгонялись до
            // 8.2 и просто загоняли медведя в угол.
            speed = Mathf.Min(Speed * 1.7f, 6.0f);

            // Дошёл на дистанцию удара — встаёт и бьёт. Если не встать, враг
            // будет толкать героя телом и вечно «догонять» вплотную.
            if (Damage > 0)
            {
                Vector3 gap = chase - pos;
                gap.y = 0f;
                if (gap.magnitude <= AttackRange + Radius)
                {
                    goal = pos;
                    if (_atkCd <= 0f && _swing < 0f)
                    {
                        // Начали замах: анимация и звук сразу, урон — потом.
                        _atkCd = AttackCooldown;
                        _swing = WindUp;
                        if (_model != null)
                            _model.Restart(_model.Pick("Bite_InPlace", "Attack", "Jump"), 1.2f);
                        Snd.Play("swing", 0.5f);
                    }
                }
            }
        }
        else if (March)
        {
            goal = PointB;
        }
        else
        {
            if ((_target - pos).magnitude < 0.08f)
                _target = (_target - PointB).sqrMagnitude < 0.001f ? PointA : PointB;
            goal = _target;
        }

        if (!Blocked) transform.localPosition = Vector3.MoveTowards(pos, goal, speed * dt);

        // Смотреть надо на цель, а не на точку, куда идём: встав бить, враг
        // никуда не идёт, и по goal он остался бы стоять к герою боком.
        Vector3 to = (_chasing ? chase : goal) - pos;
        if (new Vector2(to.x, to.z).magnitude > 0.01f)
            _visual.localRotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
    }

    // Дальность, с которой враг замечает медведя. У босса шире — он крупный
    // и стоит на открытой арене.
    private float AggroRange { get { return (IsBoss ? 14f : 8f) * Mathf.Max(1f, Scale); } }

    // Насколько далеко в сторону от своего маршрута враг готов отойти.
    // Держим коротко: у врага нет ни гравитации, ни проверки опоры — он
    // едет на уровне своей линии патрулирования. Линию автор уровня уже
    // проложил по проходимому месту, а поводок вокруг её середины
    // выпускал врага за край площадки, где он повисал в воздухе вне
    // досягаемости удара.
    private const float Leash = 1.5f;

    // Кого догонять. У медведя цель выдаёт GameRoot, у героя Кёнигсберга
    // будет своя — врагу знать про обоих незачем. Пусто и без GameRoot
    // означает «целей нет», и враг просто ходит по маршруту: так же он
    // ведёт себя сейчас в сцене без хозяина игры.
    public static System.Func<Vector3, Transform> TargetProvider;

    // Тряска камеры при попадании. Тот же приём: медведь трясёт свою,
    // Кёнигсберг — свою.
    public static System.Action<float> ShakeCamera;

    private static void Shake(float amount)
    {
        if (ShakeCamera != null) { ShakeCamera(amount); return; }
        BearPlayer local = GameRoot.LocalBear;
        if (local != null) local.Shake(amount);
    }

    // Куда бежать за целью. false — некого догонять, идём по маршруту.
    private bool FindChaseTarget(Vector3 localPos, out Vector3 goal)
    {
        goal = localPos;
        Vector3 worldPos = transform.parent != null
            ? transform.parent.TransformPoint(localPos) : localPos;

        Transform target;
        if (TargetProvider != null) target = TargetProvider(worldPos);
        else
        {
            BearPlayer p = GameRoot.NearestPlayer(worldPos);
            target = p != null ? p.transform : null;
        }
        if (target == null) return false;

        Vector3 theirs = transform.parent != null
            ? transform.parent.InverseTransformPoint(target.position)
            : target.position;

        Vector3 flat = theirs - localPos;
        flat.y = 0f;
        // Гистерезис: заметив, враг не бросает погоню от каждого шага в сторону.
        float range = _chasing ? AggroRange * 1.35f : AggroRange;
        if (flat.magnitude > range) return false;
        // Медведь на уступе выше или в яме ниже — враг его не достанет,
        // и бегать под ним бессмысленно.
        if (Mathf.Abs(theirs.y - localPos.y) > 3.5f + Height) return false;

        // Ближайшая к игроку точка самого маршрута — вместе с её высотой.
        Vector3 ab = PointB - PointA;
        float len2 = ab.sqrMagnitude;
        float t = len2 < 0.0001f
            ? 0f
            : Mathf.Clamp01(Vector3.Dot(theirs - PointA, ab) / len2);
        Vector3 onPath = PointA + ab * t;

        // И короткий шаг с линии в сторону медведя, чтобы погоня не
        // выглядела ездой по рельсам.
        Vector3 off = theirs - onPath;
        off.y = 0f;
        if (off.magnitude > Leash) off = off.normalized * Leash;

        goal = onPath + off;
        return true;
    }

    public void SetNetState(Vector3 pos, float yaw)
    {
        _netPos = pos;
        _netYaw = yaw;
        _hasNet = true;
    }

    private void Update()
    {
        if (_dieT >= 0f)
        {
            _dieT += Time.deltaTime;
            // С моделью проигрывается её клип смерти, примитивы схлопываются.
            if (_model != null)
            {
                if (_dieT >= 0.9f) Object.Destroy(gameObject);
                return;
            }
            float k = Mathf.Clamp01(_dieT / 0.25f);
            _visual.localScale = new Vector3(1f + k * 0.4f, 1f - k * 0.9f, 1f + k * 0.4f);
            if (k >= 1f) Object.Destroy(gameObject);
            return;
        }

        if (Dying) return;

        NetManager net = NetManager.I;
        bool remote = net != null && net.Online && !net.IsHost;
        if (remote && _hasNet)
        {
            transform.position = Vector3.Lerp(transform.position, _netPos, Mathf.Min(Time.deltaTime * 10f, 1f));
            if ((transform.position - _netPos).magnitude > 8f) transform.position = _netPos;
            float cur = _visual.localEulerAngles.y;
            _visual.localRotation = Quaternion.Euler(0f, Mathf.LerpAngle(cur, _netYaw, Mathf.Min(Time.deltaTime * 10f, 1f)), 0f);
        }

        _bob += Time.deltaTime * (Kind == "bat" ? 13f : 6f);

        if (_model != null)
        {
            // Модель анимируется сама; вручную добавляем только парение.
            if (_flying)
                _visual.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(_bob * 0.35f) * 0.3f, 0f);
            return;
        }

        if (_rig != null)
        {
            AnimateRig();
            return;
        }

        if (_wingL != null && _wingR != null)
        {
            float flap = Mathf.Sin(_bob) * 42f;
            _wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
            _wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            _visual.localPosition = new Vector3(0f, Mathf.Sin(_bob * 0.35f) * 0.35f, 0f);
        }
        else
        {
            Vector3 sc = _visual.localScale;
            sc.y = 1f + Mathf.Sin(_bob) * 0.05f;
            _visual.localScale = new Vector3(1f, sc.y, 1f);
        }
    }

    // Шаг кубического моба: конечности качаются двумя противофазами
    // вокруг запомненной при сборке позы.
    private void AnimateRig()
    {
        if (_rig.Squash)
        {
            float k = Mathf.Sin(_bob);
            _visual.localScale = new Vector3(1f + k * 0.09f, 1f - k * 0.12f, 1f + k * 0.09f);
            return;
        }
        float deg = Mathf.Sin(_bob) * _rig.SwingDeg;
        Swing(_rig.PhaseA, _rig.BaseA, deg);
        Swing(_rig.PhaseB, _rig.BaseB, -deg);
    }

    private static void Swing(Transform[] set, Vector3[] baseRot, float deg)
    {
        if (set == null || baseRot == null) return;
        for (int i = 0; i < set.Length && i < baseRot.Length; i++)
        {
            if (set[i] == null) continue;
            set[i].localRotation = Quaternion.Euler(baseRot[i].x + deg, baseRot[i].y, baseRot[i].z);
        }
    }

    // Попадание по боссу. Возвращает true, если он погиб.
    // Медведь бьёт «на единицу» — у него урона как числа нет вовсе.
    public bool TakeHit() { return TakeDamage(1); }

    // Попадание с уроном. Возвращает true, если враг погиб.
    public bool TakeDamage(int amount)
    {
        if (Dying) return true;
        Hp -= Mathf.Max(1, amount);
        if (Hp > 0)
        {
            Snd.Play("bosshit", 0.9f);
            Shake(0.28f);
            if (_model != null) _model.Restart("HitRecieve", 1.3f);
            ParticleFx.Burst(transform.parent, transform.position + new Vector3(0f, 1.2f, 0f),
                10, new Color(1f, 0.6f, 0.4f), 4f);
            return false;
        }
        return true;
    }

    public void DieEffect()
    {
        if (Dying) return;
        Dying = true;
        _dieT = 0f;
        Snd.Play("stomp");
        if (_model != null) _model.Restart("Death", 1.1f);
        Color tint;
        if (Kind == "slime") tint = new Color(0.5f, 0.75f, 1f);
        else if (Kind == "beetle") tint = new Color(0.7f, 0.5f, 1f);
        else if (Kind == "bat") tint = new Color(0.6f, 0.4f, 0.95f);
        else if (Kind == "creeper" || Kind == "cubeslime") tint = new Color(0.45f, 0.9f, 0.3f);
        else if (Kind == "zombie") tint = new Color(0.35f, 0.7f, 0.4f);
        else if (Kind == "skeleton") tint = new Color(0.9f, 0.9f, 0.85f);
        else if (Kind == "spider") tint = new Color(0.8f, 0.25f, 0.2f);
        else if (Kind == "enderman") tint = new Color(0.8f, 0.4f, 1f);
        else tint = new Color(0.9f, 0.3f, 0.2f);
        Transform root = transform.parent != null ? transform.parent : null;

        // Крипер уходит вспышкой: он и в первоисточнике запоминается
        // взрывом, а не тем, как ходит. Урона от неё нет — только зрелище.
        if (Kind == "creeper")
        {
            ParticleFx.Burst(root, transform.position + new Vector3(0f, 0.8f, 0f),
                34, tint, 9f);
            Gfx.Glow(transform, new Vector3(0f, 0.8f, 0f), 4.5f,
                new Color(0.9f, 1f, 0.7f, 0.5f));
            Snd.Play("bosshit", 1f);
            Shake(0.35f);
            return;
        }

        ParticleFx.Burst(root, transform.position + new Vector3(0f, 0.5f, 0f), 14, tint, 4.5f);
    }
}
