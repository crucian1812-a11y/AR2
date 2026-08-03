using UnityEngine;

namespace Koenig
{
    // Запуск AR-станции. По гибридной схеме сама AR — это веб-страница на
    // MindAR (навёл камеру на печатную метку → объект оживает), как та,
    // что уже лежит на ветке main. Из Unity открываем её браузером: так
    // не нужен ни AR Foundation, ни ARCore внутри APK, а страница —
    // офлайн-способная и работает на любом телефоне.
    //
    // ЧТО ОСТАЁТСЯ СДЕЛАТЬ ПО КОНТЕНТУ: под каждую метку (medovy_ar,
    // tower_shukhov, lighthouse_ar, sundial_ar, sobor, tower_zeln) нужна
    // своя страница-станция и файл-цель .mind, собранные из фотографии
    // реальной достопримечательности. Это полевой контент — его нельзя
    // сгенерировать здесь без снимков мест. Механизм запуска готов;
    // страницы деплоятся на GitHub Pages рядом с существующей AR.
    public static class ArStation
    {
        // База AR-станций. Указывает на GitHub Pages репозитория, где уже
        // развёрнута веб-AR. Поменяй на свой адрес Pages, если он другой.
        public const string Base = "https://crucian1812-a11y.github.io/AR2/ar/";

        public static bool Has(string arTarget)
        {
            return !string.IsNullOrEmpty(arTarget);
        }

        // Открыть станцию по id метки. Страница сама включит камеру и будет
        // ждать печатную картинку этой достопримечательности.
        public static void Open(string arTarget)
        {
            if (!Has(arTarget)) return;
            Snd.Play("click");
            Application.OpenURL(Base + arTarget + "/");
        }
    }
}
