using System;
using UnityEngine;

// Жестовое управление.
//
// Кнопки со списком приёмов были заглушкой с самого начала: они занимают
// треть экрана — ровно ту треть, где идёт борьба, — и заставляют читать
// названия вместо того, чтобы смотреть на бойцов.
//
// Жест решает обе беды. Направление свайпа отвечает намерению, а не
// конкретному приёму: «вперёд» это всегда давить и проходить, «назад» —
// уходить и вставать, «в сторону» — переворачивать. Какой именно приём
// сработает, зависит от позиции, и это правильно: игрок думает
// категориями борьбы, а не пунктами меню.
//
// Тап и удержание отданы двум действиям, которые нужны постоянно и не
// имеют направления: взять захват и давить в сабмишне.
public enum Gesture
{
    None,
    SwipeForward,   // давить, проходить, атаковать
    SwipeBack,      // уходить, вставать, защищаться
    SwipeLeft,
    SwipeRight,
    Tap,
    Hold
}

public class Gestures : MonoBehaviour
{
    // Порог в долях короткой стороны экрана, а не в пикселях: иначе на
    // планшете свайп требует вдвое большего движения пальцем, чем на
    // телефоне.
    private const float SwipeFraction = 0.055f;
    private const float HoldSeconds = 0.28f;

    private Vector2 _start;
    private float _startTime;
    private bool _tracking;
    private bool _holdFired;

    public event Action<Gesture> OnGesture;

    public static Gestures Create(Transform parent)
    {
        GameObject go = new GameObject("Gestures");
        go.transform.SetParent(parent, false);
        return go.AddComponent<Gestures>();
    }

    private void Update()
    {
        Vector2 pos;
        bool down, up, held;
        if (!Read(out pos, out down, out up, out held)) return;

        if (down)
        {
            _start = pos;
            _startTime = Time.unscaledTime;
            _tracking = true;
            _holdFired = false;
            return;
        }

        if (!_tracking) return;

        float threshold = Mathf.Min(Screen.width, Screen.height) * SwipeFraction;
        Vector2 delta = pos - _start;

        // Удержание срабатывает, пока палец ещё на экране: ждать отпускания
        // значило бы задержать самое частое действие в сабмишне.
        if (held && !_holdFired &&
            Time.unscaledTime - _startTime > HoldSeconds &&
            delta.magnitude < threshold)
        {
            _holdFired = true;
            Fire(Gesture.Hold);
            return;
        }

        if (!up) return;
        _tracking = false;

        if (_holdFired) return;

        if (delta.magnitude < threshold)
        {
            Fire(Gesture.Tap);
            return;
        }

        // Направление по преобладающей оси. Диагональ намеренно не
        // распознаётся отдельно: в горячий момент палец не рисует
        // диагонали, и лишняя категория давала бы промахи.
        if (Mathf.Abs(delta.y) >= Mathf.Abs(delta.x))
            Fire(delta.y > 0f ? Gesture.SwipeForward : Gesture.SwipeBack);
        else
            Fire(delta.x > 0f ? Gesture.SwipeRight : Gesture.SwipeLeft);
    }

    // Ввод читается и с касаний, и с мыши: на телефоне работает первое,
    // в редакторе и при отладке — второе.
    private bool Read(out Vector2 pos, out bool down, out bool up, out bool held)
    {
        pos = Vector2.zero;
        down = up = held = false;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            pos = t.position;
            down = t.phase == TouchPhase.Began;
            up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            held = !up;
            return true;
        }

        if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
        {
            Vector3 mouse = Input.mousePosition;
            pos = new Vector2(mouse.x, mouse.y);
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
            return true;
        }

        return false;
    }

    private void Fire(Gesture g)
    {
        if (OnGesture != null) OnGesture(g);
    }

    public static string Describe(Gesture g)
    {
        switch (g)
        {
            case Gesture.SwipeForward: return "свайп вверх — давить";
            case Gesture.SwipeBack: return "свайп вниз — уходить";
            case Gesture.SwipeLeft: return "свайп влево";
            case Gesture.SwipeRight: return "свайп вправо";
            case Gesture.Tap: return "тап — захват";
            case Gesture.Hold: return "удержание";
            default: return "";
        }
    }
}
