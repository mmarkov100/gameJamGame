using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рассылает глобальные параметры волн в шейдер и (опционально) рисует кольца для отладки.
/// В любой момент может существовать несколько активных волн (до maxWaves).
/// </summary>
public class SonarController : MonoBehaviour
{
    public static SonarController Instance { get; private set; }

    [Header("Волны")]
    [Min(0.1f)] public float waveSpeed = 12f;         // скорость волны (м/с)
    [Min(0.01f)] public float waveWidth = 1.2f;       // «толщина» кольца (м)
    [Min(0.2f)] public float pingInterval = 1.5f;     // частота запуска новых волн (сек)
    [Min(5f)] public float maxDistance = 40f;       // максимум радиуса (м)
    [Range(1, 8)] public int maxWaves = 4;            // одновременно активных волн

    [Header("Визуальная отладка")]
    public bool drawDebugRings = false;

    readonly List<Vector3> origins = new();
    readonly List<float> startTimes = new();

    float nextPingTime;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        // Сразу запускаем первую волну
        EmitWave();
    }

    void Update()
    {
        // Периодически создаём новую волну из текущей позиции игрока
        if (Time.time >= nextPingTime)
            EmitWave();

        // Удаляем отыгравшие волны
        CullWaves();

        // Прокидываем глобали в шейдер
        PushShaderGlobals();

        // Враги могут читать активные волны (для «подсветки на 1 сек»)
        // Ничего делать тут не нужно — они сами прочтут Instance из EnemySonarResponder.
    }

    void EmitWave()
    {
        nextPingTime = Time.time + pingInterval;

        origins.Add(transform.position);
        startTimes.Add(Time.time);

        // ограничиваем количество
        while (origins.Count > maxWaves)
        {
            origins.RemoveAt(0);
            startTimes.RemoveAt(0);
        }
    }

    void CullWaves()
    {
        for (int i = origins.Count - 1; i >= 0; i--)
        {
            float age = Time.time - startTimes[i];
            float radius = waveSpeed * age;
            if (radius > maxDistance + waveWidth * 1.1f)
            {
                origins.RemoveAt(i);
                startTimes.RemoveAt(i);
            }
        }
    }

    void PushShaderGlobals()
    {
        int count = Mathf.Min(origins.Count, 8); // шейдер поддерживает до 8
        Vector4[] pos = new Vector4[8];
        float[] ts = new float[8];

        for (int i = 0; i < count; i++)
        {
            pos[i] = new Vector4(origins[i].x, origins[i].y, origins[i].z, 0f);
            ts[i] = startTimes[i];
        }

        Shader.SetGlobalInt("_SonarWaveCount", count);
        Shader.SetGlobalVectorArray("_SonarOrigins", pos);
        Shader.SetGlobalFloatArray("_SonarStartTimes", ts);
        Shader.SetGlobalFloat("_SonarSpeed", waveSpeed);
        Shader.SetGlobalFloat("_SonarBandWidth", waveWidth);
        Shader.SetGlobalFloat("_SonarMaxDistance", maxDistance);
        Shader.SetGlobalFloat("_TimeNow", Time.time);

        if (drawDebugRings)
        {
            for (int i = 0; i < count; i++)
            {
                float r = (Time.time - startTimes[i]) * waveSpeed;
                DebugDrawRing(origins[i], r, Color.cyan);
            }
        }
    }

    void DebugDrawRing(Vector3 center, float radius, Color c)
    {
        const int steps = 64;
        Vector3 prev = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            Debug.DrawLine(prev + Vector3.up * 0.05f, p + Vector3.up * 0.05f, c, 0, false);
            prev = p;
        }
    }

    // Доступ врагам (чтобы понять, прошла ли волна)
    public int ActiveWaveCount => Mathf.Min(origins.Count, 8);
    public Vector3 GetOrigin(int i) => origins[i];
    public float GetStartTime(int i) => startTimes[i];
}
