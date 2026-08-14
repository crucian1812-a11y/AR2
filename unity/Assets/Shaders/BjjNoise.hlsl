#ifndef BJJ_NOISE_INCLUDED
#define BJJ_NOISE_INCLUDED

// Процедурная детализация поверхности без единой текстуры.
//
// Почему не текстуры: у модели, построенной кодом, нет развёртки, а
// делать её автоматически бессмысленно — сгенерированный атлас всё равно
// нечем осмысленно заполнить. Шум же даёт и поры кожи, и переплетение
// ткани, и потёртости, причём на любом масштабе и без единого байта в
// репозитории.
//
// Ключевая тонкость — В КАКИХ координатах считать шум. Пространство
// объекта не годится: скиннинг двигает вершины, и узор ползёт по коже
// при каждом движении. Поэтому модель несёт координаты ПОЗЫ ПОКОЯ в
// дополнительных UV-каналах (см. tools/blender/fighter.py,
// bake_rest_coords) — они неподвижны относительно тела.

// Координата позы покоя из двух UV-каналов.
float3 BjjRestPos(float2 restXY, float2 restZ)
{
    return float3(restXY.x - 0.5, restXY.y - 0.5, restZ.x) * 2.0;
}

float BjjHash(float3 p)
{
    p = frac(p * 0.3183099 + float3(0.71, 0.113, 0.419));
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Значение шума с гладкой интерполяцией.
float BjjNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = BjjHash(i + float3(0, 0, 0));
    float n100 = BjjHash(i + float3(1, 0, 0));
    float n010 = BjjHash(i + float3(0, 1, 0));
    float n110 = BjjHash(i + float3(1, 1, 0));
    float n001 = BjjHash(i + float3(0, 0, 1));
    float n101 = BjjHash(i + float3(1, 0, 1));
    float n011 = BjjHash(i + float3(0, 1, 1));
    float n111 = BjjHash(i + float3(1, 1, 1));

    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);

    return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
}

// Сумма октав. Три — компромисс: четвёртая на телефоне уже не видна, а
// стоит столько же, сколько первые три вместе.
float BjjFbm(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    for (int i = 0; i < 3; i++)
    {
        sum += BjjNoise(p) * amp;
        p *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

// Возмущение нормали по градиенту высоты. Считается конечными разностями
// той же функции высоты — карту нормалей заменяет ровно это.
//
// Дешёвый способ (три лишних вызова шума) сделать поверхность не
// идеально гладкой. Идеальная гладкость и есть то, что выдаёт
// компьютерную модель: у настоящей кожи и ткани нормаль дрожит.
float3 BjjPerturbNormal(float3 N, float3 T, float3 B, float h, float3 grad, float strength)
{
    float3 n = N - (T * grad.x + B * grad.y) * strength;
    return normalize(n);
}

// Градиент шума конечными разностями.
float3 BjjNoiseGrad(float3 p, float eps)
{
    float c = BjjFbm(p);
    float dx = BjjFbm(p + float3(eps, 0, 0)) - c;
    float dy = BjjFbm(p + float3(0, eps, 0)) - c;
    float dz = BjjFbm(p + float3(0, 0, eps)) - c;
    return float3(dx, dy, dz) / eps;
}

// Касательный базис из нормали. Развёртки нет, поэтому настоящих
// касательных тоже нет — для возмущения шумом произвольный устойчивый
// базис ничем не хуже.
void BjjBasis(float3 N, out float3 T, out float3 B)
{
    float3 up = abs(N.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0);
    T = normalize(cross(up, N));
    B = cross(N, T);
}

#endif
