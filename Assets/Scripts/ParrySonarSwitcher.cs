using System.Collections;
using UnityEngine;

public class ParrySonarSwitcher : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerParry parry;

    [Tooltip("Все источники света, которые вспыхивают при парировании")]
    public Light[] roomLights;

    [Header("Настройки вспышки")]
    public float lightIntensity = 2.5f;
    public float parryLightTime = 2.0f;

    [Header("Опционально")]
    public PlayerAlwaysGlow playerGlow; // если пусто — найдем автоматически

    // ---- ЗВУК ----
    [Header("Звук парирования")]
    [Tooltip("Источник звука (можно на игрока). Если не задан, будет PlayClipAtPoint.")]
    public AudioSource sfxSource;
    public AudioClip parryClip;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    [Tooltip("Случайное отклонение высоты тона для разнообразия, 0 = выкл")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

    float[] baseIntensities;
    bool[] baseEnabledStates;

    void Awake()
    {
        if (!parry) parry = GetComponent<PlayerParry>();
        if (!playerGlow) playerGlow = FindObjectOfType<PlayerAlwaysGlow>();

        if (roomLights == null) roomLights = new Light[0];
        baseIntensities = new float[roomLights.Length];
        baseEnabledStates = new bool[roomLights.Length];

        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;
            baseIntensities[i] = L.intensity;
            baseEnabledStates[i] = L.enabled;
            L.enabled = true;
            L.intensity = 0f;
        }
    }

    // Назначь в PlayerParry.OnParrySuccess
    public void OnParrySuccessHandler()
    {
        // --- Сначала звук ---
        PlayParrySfx();

        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(BurstCo());
    }

    void PlayParrySfx()
    {
        if (!parryClip) return;

        if (sfxSource)
        {
            // необязательная лёгкая рандомизация питча
            float originalPitch = sfxSource.pitch;
            if (pitchJitter > 0f)
                sfxSource.pitch = Mathf.Clamp(originalPitch + Random.Range(-pitchJitter, pitchJitter), 0.5f, 2f);

            sfxSource.PlayOneShot(parryClip, sfxVolume);

            // вернуть базовый pitch
            if (pitchJitter > 0f) sfxSource.pitch = originalPitch;
        }
        else
        {
            // запасной вариант: одноразовое 3D-воспроизведение в позиции игрока/скрипта
            AudioSource.PlayClipAtPoint(parryClip, transform.position, sfxVolume);
        }
    }

    IEnumerator BurstCo()
    {
        var sonar = SonarController.Instance;

        // 1) Пауза сонара и подсветок
        if (sonar) sonar.paused = true;
        if (playerGlow) playerGlow.SetSuppressed(true);
        EnemySonarResponder.SetSuppressed(true);

        // 2) Свет — одна общая интенсивность
        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;
            L.intensity = lightIntensity;
        }

        yield return new WaitForSeconds(parryLightTime);

        // 3) Откат света
        for (int i = 0; i < roomLights.Length; i++)
        {
            var L = roomLights[i];
            if (!L) continue;
            L.intensity = baseIntensities[i];
            L.enabled = baseEnabledStates[i];
        }

        // 4) Возобновление сонара и подсветок
        if (sonar) sonar.paused = false;
        EnemySonarResponder.SetSuppressed(false);
        if (playerGlow) playerGlow.SetSuppressed(false);
    }
}
