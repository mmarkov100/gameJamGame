using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-300)]
public class SonarController : MonoBehaviour
{
    public static SonarController Instance { get; private set; }

    [Header("Ссылки")]
    public Transform player;             // центр пинга (обычно ГГ)

    [Header("Параметры волн")]
    public float waveSpeed = 8f;         // м/с — как быстро растёт радиус
    public float waveWidth = 1.8f;       // толщина «кольца» (м)
    public float waveInterval = 1.2f;    // период запуска волн (с)
    public float tailFade = 0.0f;        // «послесвечение» позади фронта (0 = нет)

    [Header("Визуал кольца")]
    public Color sonarColor = new Color(1, 1, 1, 1);
    [Range(0f, 4f)] public float sonarIntensity = 1.2f;
    public float worldDarkness = 0.0f;   // базовая видимость сцены в полной темноте (0-1)

    [Header("Управление")]
    public bool autoStart = true;
    public bool paused = false;

    // текущее состояние волны
    public float CurrentRadius { get; private set; }
    public int WaveIndex { get; private set; }

    float nextWaveTime;
    Vector4 colorPacked;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        colorPacked = new Vector4(sonarColor.r, sonarColor.g, sonarColor.b, sonarColor.a);
    }

    void OnEnable()
    {
        if (autoStart) nextWaveTime = Time.time + 0.2f;
        PushGlobals(initial: true);
    }

    void Update()
    {
        if (!player) return;

        // Запуск новой волны по интервалу
        if (!paused && Time.time >= nextWaveTime)
        {
            WaveIndex++;
            CurrentRadius = 0f;
            nextWaveTime = Time.time + waveInterval;
        }

        // Обновляем радиус
        if (!paused)
            CurrentRadius += waveSpeed * Time.deltaTime;

        // Прокидываем параметры в шейдеры (глобально)
        PushGlobals();
    }

    void PushGlobals(bool initial = false)
    {
        Vector3 p = player ? player.position : Vector3.zero;

        Shader.SetGlobalVector("_SonarOrigin", new Vector4(p.x, p.y, p.z, 0));
        Shader.SetGlobalFloat("_SonarRadius", CurrentRadius);
        Shader.SetGlobalFloat("_SonarWidth", Mathf.Max(0.01f, waveWidth));
        Shader.SetGlobalFloat("_SonarTail", Mathf.Max(0f, tailFade));
        Shader.SetGlobalVector("_SonarColor", colorPacked);
        Shader.SetGlobalFloat("_SonarIntensity", sonarIntensity);
        Shader.SetGlobalFloat("_WorldDarkness", Mathf.Clamp01(worldDarkness));
        Shader.SetGlobalFloat("_SonarPaused", paused ? 1f : 0f);
        Shader.SetGlobalInt("_SonarWaveIndex", WaveIndex);
    }

    // ——— API для других компонентов ———

    public bool IsFrontNearHorizontal(Vector3 worldPos, float toleranceMeters)
    {
        Vector2 a = new Vector2(worldPos.x, worldPos.z);
        Vector2 b = player ? new Vector2(player.position.x, player.position.z) : Vector2.zero;
        float dist = Vector2.Distance(a, b);
        return Mathf.Abs(dist - CurrentRadius) <= toleranceMeters;
    }

    public void PauseForSeconds(float seconds)
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(PauseCo(seconds));
    }

    System.Collections.IEnumerator PauseCo(float t)
    {
        paused = true;
        PushGlobals();
        yield return new WaitForSeconds(t);
        paused = false;
    }
}
