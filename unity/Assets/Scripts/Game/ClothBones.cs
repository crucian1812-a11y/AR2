using System.Collections.Generic;
using UnityEngine;

// Динамика кимоно: юбка куртки и хвосты пояса качаются, отставая от тела.
//
// Зачем. Ткань, намертво прибитая к телу, читается доспехом. В борьбе за
// кимоно всё время тянут, боец переваливается с боку на бок — и если при
// этом ничего не колышется, картинка мертвеет, сколько ни улучшай
// материалы.
//
// Почему не симуляция ткани. Unity Cloth — это меш-солвер: он считает
// сотни частиц с коллизиями и на телефоне стоит непозволительно дорого
// ради трёх лоскутов. Здесь вместо него пружина на кость: у каждой кости
// своя угловая скорость, которая догоняет положение покоя и отстаёт от
// ускорения родителя. Это то же самое, что делают все «dynamic bone»
// решения, и стоит оно три десятка операций на кадр.
public class ClothBones : MonoBehaviour
{
    // Имена костей ткани в модели. Совпадают с rig.py: Skirt, BeltTailL,
    // BeltTailR. Если кости нет — просто пропускаем, модель без неё
    // работает по-прежнему.
    private static readonly string[] BoneNames = { "Skirt", "BeltTailL", "BeltTailR" };

    private class Strand
    {
        public Transform Bone;
        public Quaternion Rest;
        public Vector3 PrevParentPos;
        public Vector3 Velocity;      // угловое смещение, накопленное пружиной
    }

    private readonly List<Strand> _strands = new List<Strand>();

    // Жёсткость и затухание. Ги — плотная хлопковая ткань: она не
    // развевается как шёлк, а качается коротко и вязко. Отсюда высокое
    // затухание и умеренная жёсткость.
    private const float Stiffness = 42f;
    private const float Damping = 7.5f;
    private const float MaxAngle = 26f;

    public static ClothBones Attach(GameObject model)
    {
        ClothBones cloth = model.AddComponent<ClothBones>();
        cloth.Collect(model.transform);
        return cloth;
    }

    private void Collect(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            for (int n = 0; n < BoneNames.Length; n++)
            {
                if (all[i].name != BoneNames[n]) continue;

                Strand s = new Strand();
                s.Bone = all[i];
                s.Rest = all[i].localRotation;
                s.PrevParentPos = all[i].parent != null
                    ? all[i].parent.position
                    : all[i].position;
                _strands.Add(s);
            }
        }
    }

    private void LateUpdate()
    {
        // Именно LateUpdate: анимация уже проиграна, и мы добавляем
        // качание поверх её позы. В Update поза перезаписалась бы.
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, 0.05f);   // защита от рывка после паузы

        for (int i = 0; i < _strands.Count; i++)
        {
            Strand s = _strands[i];
            if (s.Bone == null || s.Bone.parent == null) continue;

            Vector3 parentPos = s.Bone.parent.position;
            Vector3 motion = (parentPos - s.PrevParentPos) / dt;
            s.PrevParentPos = parentPos;

            // Ткань отстаёт от движения тела: сила действует против него.
            Vector3 local = s.Bone.parent.InverseTransformDirection(-motion);

            // Пружина к положению покоя плюс сопротивление среды.
            s.Velocity += local * Stiffness * dt;
            s.Velocity -= s.Velocity * Damping * dt;
            s.Velocity = Vector3.ClampMagnitude(s.Velocity, MaxAngle);

            // Качание вокруг горизонтальных осей: ткань не закручивается
            // вокруг собственной длины, и разрешать это — значит получить
            // винт вместо складки.
            Quaternion swing = Quaternion.Euler(s.Velocity.z, 0f, -s.Velocity.x);
            s.Bone.localRotation = s.Rest * swing;
        }
    }
}
