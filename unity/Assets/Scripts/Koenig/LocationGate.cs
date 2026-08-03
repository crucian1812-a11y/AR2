using System.Collections;
using UnityEngine;
using UnityEngine.Android;

namespace Koenig
{
    // Геолокация: понимает, что ребёнок реально доехал до точки. Одна из
    // двух дорог разблокировки (вторая — скан AR-метки); достаточно любой.
    //
    // Всё устроено мягко: нет разрешения, выключена геолокация, слабый
    // сигнал — приложение не ломается, просто GPS-путь недоступен, и
    // остаётся скан метки и ручная отметка родителем. На телефоне это
    // проверяется живьём; в CI код только компилируется.
    public class LocationGate : MonoBehaviour
    {
        public static LocationGate I;

        public bool Running { get; private set; }
        public bool HasFix { get; private set; }
        public double Lat { get; private set; }
        public double Lon { get; private set; }
        public float Accuracy { get; private set; }
        public string Status { get; private set; }

        public static LocationGate Ensure(Transform parent)
        {
            if (I != null) return I;
            GameObject go = new GameObject("LocationGate");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<LocationGate>();
            I.Status = "Включаю геолокацию…";
            I.StartCoroutine(I.Boot());
            return I;
        }

        private void OnDestroy() { if (I == this) I = null; }

        private IEnumerator Boot()
        {
            // Разрешение на точную геолокацию. На не-Android вызовы —
            // безвредные заглушки, ветку это не ломает.
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Permission.RequestUserPermission(Permission.FineLocation);
                float t = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && t < 12f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Status = "Нет доступа к геолокации — можно сканировать метку или отметить вручную.";
                yield break;
            }

            if (!Input.location.isEnabledByUser)
            {
                Status = "Геолокация выключена в телефоне.";
                yield break;
            }

            Input.location.Start(12f, 8f);
            int guard = 0;
            while (Input.location.status == LocationServiceStatus.Initializing && guard < 200)
            {
                guard++;
                yield return new WaitForSeconds(0.1f);
            }
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Status = "Не удалось включить геолокацию.";
                yield break;
            }
            Running = true;
            Status = "Геолокация включена.";
        }

        private void Update()
        {
            if (!Running) return;
            if (Input.location.status != LocationServiceStatus.Running) { Running = false; return; }
            LocationInfo d = Input.location.lastData;
            Lat = d.latitude;
            Lon = d.longitude;
            Accuracy = d.horizontalAccuracy;
            HasFix = true;
        }

        // Расстояние между двумя точками на Земле в метрах (гаверсинус).
        public static double DistanceM(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371000.0;
            const double d2r = System.Math.PI / 180.0;
            double dLat = (lat2 - lat1) * d2r;
            double dLon = (lon2 - lon1) * d2r;
            double a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
                       System.Math.Cos(lat1 * d2r) * System.Math.Cos(lat2 * d2r) *
                       System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
            return r * 2.0 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1.0 - a));
        }

        // Ближайшая точка, которую можно взять здесь и сейчас: ещё не
        // пройдена и её город не заперт. Возвращает false, если фикса нет
        // или брать нечего.
        public bool Nearest(out Poi poi, out float meters)
        {
            poi = null; meters = 0f;
            if (!HasFix) return false;
            double best = double.MaxValue;
            Poi[] pts = KoenigContent.Points;
            for (int i = 0; i < pts.Length; i++)
            {
                if (Journey.IsDone(pts[i].Id)) continue;
                if (QuestLog.StateOf(pts[i]) == QuestState.Locked) continue;
                double m = DistanceM(Lat, Lon, pts[i].Lat, pts[i].Lon);
                if (m < best) { best = m; poi = pts[i]; }
            }
            if (poi == null) return false;
            meters = (float)best;
            return true;
        }

        // Достаточно ли близко, чтобы засчитать. Радиус точки плюс запас
        // на погрешность GPS: у зданий он легко 20–30 м, и без запаса
        // ребёнок стоял бы у самой стены, а точка не срабатывала.
        public bool WithinRadius(Poi poi, float meters)
        {
            if (poi == null) return false;
            float slack = Mathf.Clamp(Accuracy, 0f, 40f);
            return meters <= poi.RadiusM + slack;
        }
    }
}
