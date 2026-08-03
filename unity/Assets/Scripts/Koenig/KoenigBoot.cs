using UnityEngine;

namespace Koenig
{
    // Точка входа игры-путешествия. Единственный компонент в стартовой
    // сцене (её создаёт BuildScript.CreateKoenigScene). Ставит минимум,
    // что нужно карте: камеру для чистого фона, звук и сам экран-хаб.
    //
    // NetManager и GameRoot медведя здесь не нужны — игра одиночная.
    public class KoenigBoot : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;

            // Интерфейс — overlay-канвас и рисуется без камеры, но пустая
            // сцена без камеры даёт предупреждение и грязный фон. Даём
            // простую камеру с заливкой цвета моря.
            GameObject camGo = new GameObject("Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.34f, 0.5f);

            Snd.Create();
            RegionMap.Create(transform);
        }
    }
}
