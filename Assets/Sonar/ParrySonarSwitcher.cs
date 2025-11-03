using System.Collections;
using UnityEngine;

public class ParrySonarSwitcher : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerParry parry;

    [Tooltip("Перетащи сюда все источники света, которые должны вспыхнуть при парировании")]
    public Light[] roomLights;            // <-- МНОЖЕСТВО источников света

    [Header("Настройки вспышки")]
    public float lightIntensity = 2.5f;   // общая интенсивность на время вспышки для всех источников
    public float parryLightTime = 2.0f;   // длительность вспышки

    [Header("Опционально")]
    public PlayerAlwaysGlow playerGlow;   // если пусто — найдём автоматически

    // внутреннее состояние для отката
    float[] baseIntensities;
    bool[] baseEnabledStates;

    void Awake()
    {
        if (!parry) parry = GetComponent<PlayerParry>();
        if (!playerGlow) playerGlow = FindObjectOfType<PlayerAlwaysGlow>();

        // Подготовим массивы сохранения состояния
        if (roomLights == null) roomLights = new Light[0];
        baseIntensities = new float[roomLights.Length];
        baseEnabledStates = new bool[roomLights.Length];

        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;

            // Запомним исходные значения
            baseIntensities[i] = L.intensity;
            baseEnabledStates[i] = L.enabled;

            // Включим светильники, но погасим (intensity=0), чтобы можно было мгновенно зажечь
            L.enabled = true;
            L.intensity = 0f;
        }
    }

    // Назначь этот метод в PlayerParry.OnParrySuccess
    public void OnParrySuccessHandler()
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(BurstCo());
    }

    IEnumerator BurstCo()
    {
        var sonar = SonarController.Instance;

        // 1) Пауза сонара и подсветок
        if (sonar) sonar.paused = true;
        if (playerGlow) playerGlow.SetSuppressed(true);
        EnemySonarResponder.SetSuppressed(true);

        // 2) Включаем вспышку — одна интенсивность на все выбранные источники
        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;
            L.intensity = lightIntensity;
        }

        yield return new WaitForSeconds(parryLightTime);

        // 3) Откат света к исходному состоянию
        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;
            L.intensity = baseIntensities[i];
            L.enabled = baseEnabledStates[i];
        }

        // 4) Возобновляем сонар и подсветки
        if (sonar) sonar.paused = false;
        EnemySonarResponder.SetSuppressed(false);
        if (playerGlow) playerGlow.SetSuppressed(false);
    }
}
