using UnityEngine;

// Как расставить двух бойцов для каждой позиции графа, и куда при этом
// смотрит камера. Разнесено с FighterView намеренно: там «из чего сделан
// боец», здесь — «где он стоит».
//
// Ракурс задаётся для каждой позиции свой: телевизионная подача — половина
// эффекта дорогой картинки (см. §3 в docs/bjj/PLAN.md). Ради этого стоит
// держать таблицу вручную, а не считать камеру одной формулой.
public struct Stage
{
    public Vector3 PosTop;
    public Vector3 EulerTop;
    public bool LyingTop;

    public Vector3 PosBottom;
    public Vector3 EulerBottom;
    public bool LyingBottom;

    public Vector3 CamOffset;   // относительно центра схватки
    public float CamPitch;
}

public static class Staging
{
    public static Stage For(Pos pos)
    {
        Stage s = new Stage();

        switch (pos)
        {
            case Pos.Standing:
                s.PosTop = new Vector3(0f, 0f, 0.75f);
                s.EulerTop = new Vector3(0f, 180f, 0f);
                s.PosBottom = new Vector3(0f, 0f, -0.75f);
                s.EulerBottom = Vector3.zero;
                s.CamOffset = new Vector3(4.2f, 2.6f, 4.2f);
                s.CamPitch = 14f;
                break;

            case Pos.ClosedGuard:
            case Pos.OpenGuard:
                // Нижний лежит на спине, верхний — на коленях между ног.
                s.PosBottom = new Vector3(0f, 0f, -0.35f);
                s.EulerBottom = new Vector3(-88f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0f, 0f, 0.5f);
                s.EulerTop = new Vector3(-20f, 180f, 0f);
                s.CamOffset = new Vector3(3.4f, 2.2f, 0f);
                s.CamPitch = 22f;
                break;

            case Pos.HalfGuard:
                s.PosBottom = new Vector3(0f, 0f, -0.3f);
                s.EulerBottom = new Vector3(-88f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0.25f, 0f, 0.25f);
                s.EulerTop = new Vector3(-55f, 200f, 0f);
                s.LyingTop = true;
                s.CamOffset = new Vector3(3.2f, 2.4f, 1.2f);
                s.CamPitch = 26f;
                break;

            case Pos.SideControl:
                // Верхний лежит поперёк — это и есть узнаваемый силуэт
                // удержания сбоку.
                s.PosBottom = Vector3.zero;
                s.EulerBottom = new Vector3(-88f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0.45f, 0f, 0f);
                s.EulerTop = new Vector3(-80f, 90f, 0f);
                s.LyingTop = true;
                s.CamOffset = new Vector3(0f, 2.8f, 3.4f);
                s.CamPitch = 32f;
                break;

            case Pos.Mount:
                s.PosBottom = Vector3.zero;
                s.EulerBottom = new Vector3(-88f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0f, 0.5f, -0.1f);
                s.EulerTop = new Vector3(-12f, 0f, 0f);
                s.CamOffset = new Vector3(3.0f, 2.2f, 2.2f);
                s.CamPitch = 24f;
                break;

            case Pos.BackControl:
                s.PosBottom = new Vector3(0f, 0f, 0f);
                s.EulerBottom = new Vector3(-35f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0f, 0.25f, -0.5f);
                s.EulerTop = new Vector3(-35f, 0f, 0f);
                s.LyingTop = true;
                s.CamOffset = new Vector3(3.2f, 2.0f, -2.0f);
                s.CamPitch = 18f;
                break;

            case Pos.TurtleDown:
                s.PosBottom = Vector3.zero;
                s.EulerBottom = new Vector3(-60f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0.55f, 0f, -0.2f);
                s.EulerTop = new Vector3(-30f, 90f, 0f);
                s.LyingTop = true;
                s.CamOffset = new Vector3(3.4f, 2.4f, 1.6f);
                s.CamPitch = 26f;
                break;

            default: // Submitted — камера подходит вплотную к финишу
                s.PosBottom = Vector3.zero;
                s.EulerBottom = new Vector3(-88f, 0f, 0f);
                s.LyingBottom = true;
                s.PosTop = new Vector3(0.2f, 0.3f, -0.35f);
                s.EulerTop = new Vector3(-45f, 20f, 0f);
                s.LyingTop = true;
                s.CamOffset = new Vector3(1.9f, 1.5f, 1.9f);
                s.CamPitch = 20f;
                break;
        }

        return s;
    }
}
