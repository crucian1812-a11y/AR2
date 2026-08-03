using UnityEngine;
using UnityEngine.UI;

namespace Koenig
{
    // Экран героя — «лицо» RPG. Здесь ребёнок видит своего спутника Кёню
    // живьём (крутится 3D-модель), своё звание, полоску опыта до следующего
    // звания и что уже собрано. Всё это раньше существовало только внутри
    // (Journey/HeroRank), но нигде не показывалось — от этого игра казалась
    // не RPG, а просто картой. Экран собирает прогресс в одну картинку.
    //
    // Портретная раскладка: масштаб от ширины (ks = Screen.width / 440).
    public class HeroScreen : MonoBehaviour
    {
        private Canvas _canvas;
        private HeroView _view;

        private float KS { get { return Mathf.Max(Screen.width, 1) / 440f; } }
        private int Fs(float logical) { return Mathf.Max(1, Mathf.RoundToInt(logical * KS)); }
        private Vector2 Sz(float w, float h) { return new Vector2(w * KS, h * KS); }

        public static HeroScreen Create(Transform parent)
        {
            GameObject go = new GameObject("HeroScreen");
            go.transform.SetParent(parent, false);
            HeroScreen h = go.AddComponent<HeroScreen>();
            h.Build();
            return h;
        }

        private void Build()
        {
            Journey.Load();
            _canvas = UiKit.CreateCanvas("KoenigHero", 42);
            _canvas.transform.SetParent(transform, false);
            Transform c = _canvas.transform;

            float cx = Screen.width * 0.5f;
            float H = Screen.height;

            UiKit.MakePanel(c, new Vector2(cx, H * 0.5f), new Vector2(6000f, 6000f),
                new Color(0.06f, 0.12f, 0.18f, 1f));

            UiKit.MakeText(c, new Vector2(cx, H - Fs(34)), Sz(430, 40),
                "Твой герой", Fs(24), new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);

            // Живой Кёня в рамке.
            float portraitCy = H * 0.68f;
            UiKit.MakePanel(c, new Vector2(cx, portraitCy), Sz(240, 300),
                new Color(0.08f, 0.16f, 0.26f, 1f));
            _view = HeroView.Create(transform, Guide.ModelId, 320, 400, 3, 34f);
            UiKit.MakeRaw(c, new Vector2(cx, portraitCy), Sz(228, 288), _view.Texture);

            UiKit.MakeText(c, new Vector2(cx, H * 0.505f), Sz(300, 26),
                Guide.Name + " — твой проводник", Fs(15),
                new Color(0.8f, 0.88f, 0.98f), TextAnchor.MiddleCenter);

            // Звание и уровень.
            UiKit.MakeText(c, new Vector2(cx, H * 0.455f), Sz(420, 34),
                HeroRank.Title, Fs(22), new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
            UiKit.MakeText(c, new Vector2(cx, H * 0.42f), Sz(300, 24),
                "Уровень " + HeroRank.Level + " из " + HeroRank.MaxLevel, Fs(14),
                Color.white, TextAnchor.MiddleCenter);

            // Полоска опыта.
            float barW = Screen.width * 0.72f;
            float barY = H * 0.375f;
            UiKit.MakePanel(c, new Vector2(cx, barY), new Vector2(barW, 20f * KS),
                new Color(0.15f, 0.2f, 0.28f, 1f));
            float fill = Mathf.Clamp01(HeroRank.Progress01) * barW;
            if (fill > 2f)
                UiKit.MakePanel(c, new Vector2(cx - (barW - fill) * 0.5f, barY),
                    new Vector2(fill, 20f * KS), new Color(0.4f, 0.8f, 0.45f, 1f));
            UiKit.MakeText(c, new Vector2(cx, barY - 26f * KS), Sz(420, 24),
                HeroRank.NextHint, Fs(13), new Color(0.8f, 0.86f, 0.95f), TextAnchor.MiddleCenter);

            // Строка «добычи»: артефакты как RPG-трофеи.
            string loot = "Печати земель: " + Journey.Count(Artifact.LandSeal) + "/4    " +
                          "Жетоны мостов: " + Journey.Count(Artifact.BridgeToken) + "/7";
            UiKit.MakeText(c, new Vector2(cx, H * 0.3f), Sz(430, 26), loot, Fs(14),
                new Color(1f, 0.82f, 0.45f), TextAnchor.MiddleCenter);

            // Слово проводника про текущий город.
            string city = Journey.CurrentCity();
            string line = Journey.PuzzleSolved ? Guide.Finale : Guide.ForCity(city);
            UiKit.MakeText(c, new Vector2(cx, H * 0.235f), Sz(420, 70), "«" + line + "»",
                Fs(14), new Color(0.85f, 0.9f, 0.98f), TextAnchor.UpperCenter);

            UiKit.MakeButton(c, new Vector2(cx, H * 0.11f), Sz(200, 48),
                "В путь", Fs(17), Close);
        }

        public void Close()
        {
            Snd.Play("click");
            Object.Destroy(gameObject);
        }
    }
}
