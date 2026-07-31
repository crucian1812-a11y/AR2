using UnityEngine;

// Кубические враги в духе майнкрафта: крипер, зомби, скелет, паук,
// эндермен и слизь-куб. Собираются из коробок прямо кодом — как и всё
// остальное в игре.
//
// Пропорции взяты из майнкрафта в его же единицах: там всё меряется
// пикселями, шестнадцать на блок. Поэтому размеры ниже записаны в
// пикселях (крипер — 26, зомби — 32, эндермен — 50), а P переводит их в
// метры. Записывать сразу в метрах значило бы потерять узнаваемость:
// эти существа опознаются именно по соотношениям, а не по деталям.
public static class BlockMob
{
    // Один пиксель майнкрафта в наших единицах. Блок выходит 0.96 —
    // почти метр, и зомби ростом ровно в два блока встаёт вровень с
    // нашими героями (1.75).
    private const float P = 0.06f;

    // Что нужно шевелить в Update. Конечности разведены по двум фазам:
    // при ходьбе левая рука идёт вперёд вместе с правой ногой.
    public class Rig
    {
        public Transform[] PhaseA;
        public Transform[] PhaseB;
        public float SwingDeg = 38f;
        // Слизь не шагает, а сплющивается.
        public bool Squash;

        // Исходная поза запоминается при сборке, и качание идёт ОТ неё.
        // Ноги паука разведены по Z на 62 градуса, руки зомби подняты по
        // X на 80 — если каждый кадр читать текущий угол и писать поверх,
        // накапливается ошибка перевода кватерниона в углы Эйлера, и
        // конечности медленно уползают из позы.
        public Vector3[] BaseA;
        public Vector3[] BaseB;

        public void Freeze()
        {
            BaseA = Capture(PhaseA);
            BaseB = Capture(PhaseB);
        }

        private static Vector3[] Capture(Transform[] set)
        {
            if (set == null) return new Vector3[0];
            Vector3[] outp = new Vector3[set.Length];
            for (int i = 0; i < set.Length; i++)
                outp[i] = set[i] != null ? set[i].localEulerAngles : Vector3.zero;
            return outp;
        }
    }

    public static bool Handles(string kind)
    {
        return kind == "creeper" || kind == "zombie" || kind == "skeleton" ||
               kind == "spider" || kind == "enderman" || kind == "cubeslime";
    }

    public static Rig Build(Transform visual, string kind)
    {
        Rig r = null;
        if (kind == "creeper") r = Creeper(visual);
        else if (kind == "zombie") r = Humanoid(visual, false);
        else if (kind == "skeleton") r = Humanoid(visual, true);
        else if (kind == "spider") r = Spider(visual);
        else if (kind == "enderman") r = Enderman(visual);
        else if (kind == "cubeslime") r = CubeSlime(visual);
        if (r != null) r.Freeze();
        return r;
    }

    // ---------- помощники ----------

    private static Vector3 V(float x, float y, float z)
    {
        return new Vector3(x * P, y * P, z * P);
    }

    // Коробка по центру и размеру, обе в пикселях.
    private static GameObject B(Transform parent, float x, float y, float z,
        float sx, float sy, float sz, Material m)
    {
        return Gfx.Box(parent, V(x, y, z), V(sx, sy, sz), m, false);
    }

    // Конечность на шарнире: пустышка стоит в плече или бедре, коробка
    // висит под ней. Поворот пустышки качает конечность целиком — если
    // вертеть саму коробку, она крутится вокруг своей середины и нога
    // уезжает в землю.
    private static Transform Limb(Transform parent, float x, float y, float z,
        float sx, float sy, float sz, Material m)
    {
        GameObject pivot = new GameObject("Limb");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = V(x, y, z);
        Gfx.Box(pivot.transform, V(0f, -sy * 0.5f, 0f), V(sx, sy, sz), m, false);
        return pivot.transform;
    }

    private static Material Flat(float r, float g, float b)
    {
        return Gfx.Mat(new Color(r, g, b), 0.05f);
    }

    // ---------- крипер ----------
    // Ноги 0..6, тело 6..18, голова 18..26.
    private static Rig Creeper(Transform v)
    {
        Material skin = Flat(0.29f, 0.62f, 0.24f);
        Material dark = Flat(0.20f, 0.45f, 0.17f);
        Material black = Flat(0.03f, 0.05f, 0.03f);

        B(v, 0f, 12f, 0f, 8f, 12f, 4f, skin);
        B(v, 0f, 22f, 0f, 8f, 8f, 8f, skin);
        // Пятна камуфляжа: без них крипер — просто зелёный столбик.
        B(v, -2.6f, 14f, 2.1f, 2f, 5f, 0.4f, dark);
        B(v, 2.2f, 9f, 2.1f, 3f, 3f, 0.4f, dark);
        B(v, 0.5f, 16f, -2.1f, 3f, 4f, 0.4f, dark);

        // Лицо. Оно и делает крипера крипером, поэтому собрано по клеткам:
        // два квадратных глаза и рот с опущенными углами.
        B(v, -2f, 23f, 4.2f, 2f, 2f, 0.5f, black);
        B(v, 2f, 23f, 4.2f, 2f, 2f, 0.5f, black);
        B(v, 0f, 20.5f, 4.2f, 2f, 3f, 0.5f, black);
        B(v, -2f, 19.5f, 4.2f, 2f, 2f, 0.5f, black);
        B(v, 2f, 19.5f, 4.2f, 2f, 2f, 0.5f, black);

        Transform fl = Limb(v, -2f, 6f, 2f, 4f, 6f, 4f, skin);
        Transform fr = Limb(v, 2f, 6f, 2f, 4f, 6f, 4f, skin);
        Transform bl = Limb(v, -2f, 6f, -2f, 4f, 6f, 4f, skin);
        Transform br = Limb(v, 2f, 6f, -2f, 4f, 6f, 4f, skin);

        Rig r = new Rig();
        // По диагонали, как у четвероногих: передняя левая с задней правой.
        r.PhaseA = new Transform[] { fl, br };
        r.PhaseB = new Transform[] { fr, bl };
        r.SwingDeg = 26f;
        return r;
    }

    // ---------- зомби и скелет ----------
    // Различаются толщиной конечностей и цветом, скелет заметно костлявее.
    private static Rig Humanoid(Transform v, bool bones)
    {
        Material skin = bones ? Flat(0.79f, 0.79f, 0.75f) : Flat(0.21f, 0.44f, 0.28f);
        Material shirt = bones ? skin : Flat(0.24f, 0.37f, 0.55f);
        Material pants = bones ? skin : Flat(0.22f, 0.23f, 0.40f);
        Material black = Flat(0.03f, 0.03f, 0.04f);

        float limbW = bones ? 2f : 4f;
        float armX = bones ? 5f : 6f;

        B(v, 0f, 18f, 0f, 8f, 12f, 4f, shirt);
        B(v, 0f, 28f, 0f, 8f, 8f, 8f, skin);
        B(v, -2f, 29f, 4.2f, 2f, 2f, 0.5f, black);
        B(v, 2f, 29f, 4.2f, 2f, 2f, 0.5f, black);
        if (bones)
        {
            // Рёбра — иначе скелет неотличим от бледного зомби.
            for (int i = 0; i < 3; i++)
                B(v, 0f, 15f + i * 3f, 2.1f, 7f, 1f, 0.4f, Flat(0.62f, 0.62f, 0.58f));
        }

        Transform ll = Limb(v, -2f, 12f, 0f, limbW, 12f, limbW, pants);
        Transform lr = Limb(v, 2f, 12f, 0f, limbW, 12f, limbW, pants);
        Transform al = Limb(v, -armX, 23f, 0f, limbW, 12f, limbW, skin);
        Transform ar = Limb(v, armX, 23f, 0f, limbW, 12f, limbW, skin);

        // Руки вытянуты вперёд — поза, по которой зомби узнают издалека.
        al.localRotation = Quaternion.Euler(-80f, 0f, 0f);
        ar.localRotation = Quaternion.Euler(-80f, 0f, 0f);

        Rig r = new Rig();
        r.PhaseA = new Transform[] { ll };
        r.PhaseB = new Transform[] { lr };
        r.SwingDeg = 32f;
        return r;
    }

    // ---------- паук ----------
    // Низкий и широкий: голова спереди, брюшко сзади, восемь ног враскоряку.
    private static Rig Spider(Transform v)
    {
        Material body = Flat(0.19f, 0.13f, 0.11f);
        Material hair = Flat(0.11f, 0.08f, 0.07f);
        Material eye = Gfx.MatFull(new Color(1f, 0.15f, 0.1f), 0.6f, 0f,
            new Color(0.9f, 0.1f, 0.05f), 0f, 0f);

        B(v, 0f, 6f, -6f, 10f, 8f, 12f, body);
        B(v, 0f, 6f, 4f, 8f, 8f, 8f, body);
        B(v, 0f, 9f, -6f, 6f, 1f, 8f, hair);

        // Восемь красных глаз двумя рядами — визитная карточка.
        for (int i = 0; i < 2; i++)
            for (int s = -1; s <= 1; s += 2)
            {
                B(v, s * 1.4f, 7.5f - i * 2f, 8.2f, 1.2f, 1.2f, 0.5f, eye);
                B(v, s * 3.2f, 7.5f - i * 2f, 8.2f, 1.2f, 1.2f, 0.5f, eye);
            }

        Transform[] a = new Transform[4];
        Transform[] b = new Transform[4];
        int ia = 0, ib = 0;
        for (int i = 0; i < 4; i++)
        {
            float z = -7f + i * 4.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Transform leg = SpiderLeg(v, s, z, hair);
                if ((i + (s > 0 ? 1 : 0)) % 2 == 0) a[ia++] = leg;
                else b[ib++] = leg;
            }
        }

        Rig r = new Rig();
        r.PhaseA = a;
        r.PhaseB = b;
        r.SwingDeg = 13f;
        return r;
    }

    // Паучья нога из двух колен: бедро уходит почти горизонтально в
    // сторону и вверх, голень падает от колена отвесно вниз. Прямая
    // палка враскоряку такого силуэта не даёт — у паука узнаётся именно
    // дуга выше спины.
    private static Transform SpiderLeg(Transform v, float s, float z, Material m)
    {
        GameObject hip = new GameObject("Hip");
        hip.transform.SetParent(v, false);
        hip.transform.localPosition = V(s * 5f, 9f, z);
        hip.transform.localRotation = Quaternion.Euler(0f, 0f, s * 70f);
        Gfx.Box(hip.transform, V(0f, -4.5f, 0f), V(1.6f, 9f, 1.6f), m, false);

        GameObject knee = new GameObject("Knee");
        knee.transform.SetParent(hip.transform, false);
        knee.transform.localPosition = V(0f, -9f, 0f);
        // Разворот бедра гасим обратно, иначе голень уезжает вбок вместе
        // с ним и лапа не достаёт до земли.
        knee.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 70f);
        Gfx.Box(knee.transform, V(0f, -3f, 0f), V(1.4f, 6f, 1.4f), m, false);
        return hip.transform;
    }

    // ---------- эндермен ----------
    // Ноги 0..30, тело 30..42, голова 42..50: три блока роста при
    // тельце в полблока. Пропорция и есть весь образ.
    private static Rig Enderman(Transform v)
    {
        Material skin = Flat(0.055f, 0.055f, 0.075f);
        Material eye = Gfx.MatFull(new Color(0.87f, 0.45f, 1f), 0.7f, 0f,
            new Color(0.7f, 0.3f, 1f), 0f, 0f);

        B(v, 0f, 36f, 0f, 8f, 12f, 4f, skin);
        B(v, 0f, 46f, 0f, 8f, 8f, 8f, skin);
        B(v, -2.2f, 46.5f, 4.2f, 3f, 1.6f, 0.5f, eye);
        B(v, 2.2f, 46.5f, 4.2f, 3f, 1.6f, 0.5f, eye);
        Gfx.Glow(v, V(0f, 46f, 0f), 1.6f, new Color(0.7f, 0.35f, 1f, 0.28f));

        Transform ll = Limb(v, -2f, 30f, 0f, 2f, 30f, 2f, skin);
        Transform lr = Limb(v, 2f, 30f, 0f, 2f, 30f, 2f, skin);
        Transform al = Limb(v, -5f, 41f, 0f, 2f, 30f, 2f, skin);
        Transform ar = Limb(v, 5f, 41f, 0f, 2f, 30f, 2f, skin);

        Rig r = new Rig();
        r.PhaseA = new Transform[] { ll, ar };
        r.PhaseB = new Transform[] { lr, al };
        r.SwingDeg = 22f;
        return r;
    }

    // ---------- слизь-куб ----------
    private static Rig CubeSlime(Transform v)
    {
        Material gel = Gfx.MatFull(new Color(0.45f, 0.78f, 0.35f), 0.6f, 0f,
            new Color(0.12f, 0.3f, 0.08f), 0f, 0f);
        Material black = Flat(0.05f, 0.09f, 0.04f);

        B(v, 0f, 8f, 0f, 16f, 16f, 16f, gel);
        B(v, -3f, 10f, 8.2f, 2.4f, 2.4f, 0.5f, black);
        B(v, 3f, 10f, 8.2f, 2.4f, 2.4f, 0.5f, black);
        B(v, 0f, 6f, 8.2f, 2.4f, 1.6f, 0.5f, black);
        Gfx.Glow(v, V(0f, 8f, 0f), 1.4f, new Color(0.5f, 0.9f, 0.4f, 0.25f));

        Rig r = new Rig();
        r.PhaseA = new Transform[0];
        r.PhaseB = new Transform[0];
        r.Squash = true;
        return r;
    }
}
