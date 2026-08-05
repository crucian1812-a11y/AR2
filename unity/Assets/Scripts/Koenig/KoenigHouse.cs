using UnityEngine;

namespace Koenig
{
    // Дом, собранный из модулей пака, а не загруженный готовым.
    //
    // ПОЧЕМУ НЕ ГОТОВЫЕ. В Resources/Models/koenig/houses лежат четыре
    // запечённых дома, собранных когда-то в Blender. С ними три беды:
    //
    //   1. Пропорция 5.4 × 5.4 в плане при 10.4 в высоту — отношение 1.93,
    //      то есть башня, а не дом. Нормируя высоту до семи метров, мы
    //      получали ширину 3.6 — узкую пластину, и двадцать таких подряд
    //      читались рейками забора.
    //   2. От 54 до 80 материалов на дом: дубликаты, наплодившиеся при
    //      сборке. Склейка подмешей это лечит, но сама причина остаётся.
    //   3. Скрипта, который их собрал, в репозитории НЕТ. Повторить или
    //      поправить их нечем — это бинарники без исходника.
    //
    // Здесь дом складывается из модулей с известными размерами (снято
    // замером самих FBX):
    //
    //   стена            2.00 × 0.41 × 3.12
    //   угловой столб    0.21 × 0.24 × 3.00
    //   крыша 4x4        5.51 × 5.56 × 4.25   — на короб 4 × 4 со свесом
    //   крыша 6x6        8.25 × 8.03 × 5.67   — на короб 6 × 6
    //   труба            0.95 × 1.00 × 3.18
    //
    // Дом 6 × 6 в один этаж выходит 8.25 в ширину при 8.8 в высоту:
    // отношение 1.07, настоящая городская масса.
    //
    // ЦЕНА СБОРКИ И ЧЕМ ОНА ПЛАТИТСЯ. Тридцать шесть модулей на дом — это
    // около сотни вызовов отрисовки, и восемнадцать домов складывались бы
    // в две тысячи. Поэтому сразу после сборки дом склеивается в четыре
    // меша по материалам (KoenigProp.CombineChildren), а столкновение
    // держит один короб вместо тридцати шести мешей-коллайдеров.
    public static class KoenigHouse
    {
        public const float Module = 2.0f;    // ширина стенового модуля
        public const float Storey = 3.12f;   // высота этажа

        // Толщина стены: половину закладываем внутрь, чтобы НАРУЖНАЯ грань
        // легла ровно по габариту дома и фасады соседних домов смыкались.
        private const float WallHalf = 0.205f;

        public static GameObject Build(Transform parent, Vector3 pos, float yaw,
                                       int modules, int storeys, int variant)
        {
            GameObject go = new GameObject("House");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = go.transform;

            float half = modules * Module * 0.5f;
            bool brick = (variant % 2) == 1;
            string plain = brick ? "Wall_UnevenBrick_Straight" : "Wall_Plaster_Straight";
            string win = brick ? "Wall_UnevenBrick_Window_Wide_Round"
                               : "Wall_Plaster_Window_Wide_Round";

            for (int s = 0; s < storeys; s++)
            {
                float y = s * Storey;
                for (int i = 0; i < modules; i++)
                {
                    // Модули идут от края к краю: центр i-го отстоит от
                    // середины стены на (i - (n-1)/2) модулей.
                    float o = (i - (modules - 1) * 0.5f) * Module;

                    // Дверь — только в середине первого этажа фасада, и
                    // только если модулей нечётное число: иначе она уедет
                    // из центра и дом станет косым.
                    bool door = s == 0 && modules % 2 == 1 && i == modules / 2;
                    string front = door ? (brick ? "Wall_UnevenBrick_Straight"
                                                 : "Wall_Plaster_Door_Round")
                                        : (s == 0 ? win : plain);

                    Place(t, front, new Vector3(o, y, -half + WallHalf), 0f);
                    Place(t, s == 0 ? plain : win, new Vector3(o, y, half - WallHalf), 180f);
                    Place(t, s == 0 ? plain : win, new Vector3(-half + WallHalf, y, o), 90f);
                    Place(t, plain, new Vector3(half - WallHalf, y, o), 270f);

                    // Ставни на окнах фасада. Фасад — единственная стена,
                    // которую видно с улицы, и весь характер дома держится
                    // на ней; ставня даёт ей третий план и цветное пятно.
                    if (!door && (i + variant) % 2 == 0)
                        Place(t, "WindowShutters_Wide_Round_Closed",
                              new Vector3(o, y, -half - 0.02f), 0f);
                }

                // Угловые столбы прикрывают стык двух рядов стен.
                for (int c = 0; c < 4; c++)
                {
                    float cx = (c == 0 || c == 3) ? -half : half;
                    float cz = (c < 2) ? -half : half;
                    Place(t, "Corner_Exterior_Wood", new Vector3(cx, y, cz), 0f);
                }
            }

            float top = storeys * Storey;
            // Крыша подбирается под ширину короба: её свес уже заложен в
            // модель, поэтому ставим по центру, не масштабируя.
            string roof = modules >= 3 ? "Roof_RoundTiles_6x6" : "Roof_RoundTiles_4x4";
            Place(t, roof, new Vector3(0f, top, 0f), 0f);

            // Труба на СКЛОНЕ, ОБРАЩЁННОМ К КАМЕРЕ: за коньком она была бы
            // закрыта кровлей целиком, и силуэт дома снова стал бы гладким.
            Place(t, "Prop_Chimney", new Vector3(half * 0.5f, top + 1.1f, -half * 0.25f), 0f);

            // Подкос под свесом кровли у двухэтажных: он объясняет глазу,
            // на чём держится вынос, и разбивает голую полосу второго этажа.
            if (storeys > 1)
            {
                Place(t, "Prop_Support", new Vector3(-half * 0.6f, Storey, -half - 0.1f), 0f);
                Place(t, "Prop_Support", new Vector3(half * 0.6f, Storey, -half - 0.1f), 0f);
            }

            // Плющ на каждом третьем доме: сплошной ряд одинаковых фасадов
            // читается стеной, а редкая зелень его разбивает.
            if (variant % 3 == 0)
                Place(t, "Prop_Vine1", new Vector3(-half * 0.4f, 0.2f, -half - 0.05f), 0f);

            KoenigProp.CombineChildren(go);

            // Столкновение — один короб на дом. Внутрь всё равно не войти,
            // а тридцать шесть мешевых коллайдеров стоили бы дороже всей
            // остальной физики уровня вместе взятой.
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, top * 0.5f, 0f);
            bc.size = new Vector3(modules * Module, top, modules * Module);
            return go;
        }

        // Полная высота дома — нужна тому, кто расставляет их по улице.
        public static float Height(int modules, int storeys)
        {
            return storeys * Storey + (modules >= 3 ? 5.67f : 4.25f);
        }

        // Ширина вместе со свесом кровли: по ней считается шаг застройки.
        public static float Width(int modules)
        {
            return modules >= 3 ? 8.25f : 5.51f;
        }

        private static void Place(Transform parent, string id, Vector3 pos, float yaw)
        {
            KoenigProp.PlaceByFoot(parent, id, pos, yaw, false);
        }
    }
}
