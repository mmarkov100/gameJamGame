using System.Collections;
using UnityEngine;

public class ParrySonarSwitcher : MonoBehaviour
{
    [System.Serializable]
    public struct LightEntry
    {
        public Light light;             // источник света
        public float flashIntensity;    // интенсивность на время вспышки
    }

    [Header("Ссылки")]
    public PlayerParry parry;

    [Tooltip("Источники света и их индивидуальные интенсивности на время вспышки")]
    public LightEntry[] lights;         // <-- вместо roomLights + общей интенсивности

    [Header("Настройки вспышки")]
    public float parryLightTime = 2.0f;

    [Header("Опционально")]
    public PlayerAlwaysGlow playerGlow; // если пусто — найдём автоматически

    // ---- ЗВУК ----
    [Header("Звук парирования")]
    [Tooltip("Источник звука (можно на игрока). Если не задан, будет PlayClipAtPoint.")]
    public AudioSource sfxSource;
    public AudioClip parryClip;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    [Tooltip("Случайное отклонение высоты тона для разнообразия, 0 = выкл")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

    // базовые значения для отката
    float[] baseIntensities;
    bool[] baseEnabled;

    void Awake()
    {
        if (!parry) parry = GetComponent<PlayerParry>();
        if (!playerGlow) playerGlow = FindObjectOfType<PlayerAlwaysGlow>();

        if (lights == null) lights = new LightEntry[0];
        baseIntensities = new float[lights.Length];
        baseEnabled = new bool[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            var L = lights[i].light;
            if (!L) continue;

            baseIntensities[i] = L.intensity;
            baseEnabled[i] = L.enabled;

            // подготовим: включим, но сделаем тёмным
            L.enabled = true;
            L.intensity = 0f;
        }
    }

    // Назначь в PlayerParry.OnParrySuccess
    public void OnParrySuccessHandler()
    {
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
            float p0 = sfxSource.pitch;
            if (pitchJitter > 0f)
                sfxSource.pitch = Mathf.Clamp(p0 + Random.Range(-pitchJitter, pitchJitter), 0.5f, 2f);

            sfxSource.PlayOneShot(parryClip, sfxVolume);

            if (pitchJitter > 0f) sfxSource.pitch = p0;
        }
        else
        {
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

        // Дадим кадр на снятие материалов/эмиссии
        yield return null;

        // 2) Включаем каждый свет своей интенсивностью
        for (int i = 0; i < lights.Length; i++)
        {
            var entry = lights[i];
            var L = entry.light;
            if (!L) continue;

            L.enabled = true;
            L.intensity = entry.flashIntensity;  // индивидуально!
        }

        yield return new WaitForSeconds(parryLightTime);

        // 3) Откат света к исходным значениям
        for (int i = 0; i < lights.Length; i++)
        {
            var L = lights[i].light;
            if (!L) continue;

            L.intensity = baseIntensities[i];
            L.enabled = baseEnabled[i];
        }

        // 4) Возврат эффектов
        EnemySonarResponder.SetSuppressed(false);
        if (playerGlow) playerGlow.SetSuppressed(false);
        if (sonar) sonar.paused = false;
    }
}
