using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подсветка врага при прохождении волны сонара через эмиссию материала.
/// Работает без замены материалов: через MaterialPropertyBlock + _EMISSION.
/// При паррировании подсветка мгновенно снимается (SetSuppressed).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnemySonarResponder : MonoBehaviour
{
    // ==== Глобальный реестр и подавление при парри ====
    static readonly List<EnemySonarResponder> _registry = new();
    static bool _suppressed = false;

    public static void SetSuppressed(bool value)
    {
        _suppressed = value;
        if (value)
        {
            // Мгновенно погасить всех
            foreach (var e in _registry) e.StopGlowNow();
        }
    }

    [Header("Параметры подсветки")]
    public Color glowColor = new(0.4f, 0.8f, 1f, 1f);
    public float glowIntensity = 1.6f;     // множитель яркости
    public float glowDuration = 1.0f;      // сколько горит после касания
    public float triggerTolerance = 0.9f;  // половина толщины «фронта»
    public bool includeChildren = true;

    Renderer[] rends;
    MaterialPropertyBlock mpb;    // <— ВОТ ЭТО ПОЛЕ!
    Coroutine glowCo;
    int lastTriggeredWave = -1;

    void OnEnable()
    {
        if (!_registry.Contains(this)) _registry.Add(this);
    }
    void OnDisable()
    {
        _registry.Remove(this);
        // на всякий случай погасим при выключении
        StopGlowNow();
    }

    void Awake()
    {
        // Собираем рендереры (SkinnedMeshRenderer тоже попадает)
        rends = includeChildren ? GetComponentsInChildren<Renderer>(true)
                                : new[] { GetComponent<Renderer>() };

        // ИНИЦИАЛИЗИРУЕМ MPB!
        mpb = new MaterialPropertyBlock();

        // Включим эмиссию на инстансах материалов, чтобы она работала в рантайме
        foreach (var r in rends)
        {
            if (!r) continue;
            foreach (var m in r.materials) // ИМЕННО materials (инстансы), а не sharedMaterials
                m.EnableKeyword("_EMISSION");
        }

        // По умолчанию выключено
        SetEmission(Color.black);
    }

    void Update()
    {
        if (_suppressed) return;

        var sonar = SonarController.Instance;
        if (sonar == null || sonar.paused) return;

        // защищаем от повторного срабатывания на ту же волну
        if (lastTriggeredWave == sonar.WaveIndex) return;

        // фронт рядом по XZ
        if (sonar.IsFrontNearHorizontal(transform.position, sonar.waveWidth * 0.5f + triggerTolerance))
        {
            lastTriggeredWave = sonar.WaveIndex;
            if (glowCo != null) StopCoroutine(glowCo);
            glowCo = StartCoroutine(GlowCo());
        }
    }

    IEnumerator GlowCo()
    {
        float t = 0f;

        // старт — сразу включаем максимально
        SetEmission(glowColor * glowIntensity);

        while (t < glowDuration && !_suppressed)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / glowDuration); // плавное затухание
            SetEmission(glowColor * (glowIntensity * Mathf.Max(0.0001f, k)));
            yield return null;
        }

        SetEmission(Color.black);
        glowCo = null;
    }

    /// <summary> Мгновенно погасить подсветку (вызывается при парри). </summary>
    public void StopGlowNow()
    {
        if (glowCo != null) { StopCoroutine(glowCo); glowCo = null; }
        SetEmission(Color.black);

        // Чтобы во время вспышки света не перетриггериться этой же волной
        var sonar = SonarController.Instance;
        if (sonar) lastTriggeredWave = sonar.WaveIndex;
    }

    /// <summary>
    /// Установка эмиссии через MaterialPropertyBlock + управление ключом _EMISSION.
    /// </summary>
    void SetEmission(Color emission)
    {
        foreach (var r in rends)
        {
            if (!r) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", emission);
            r.SetPropertyBlock(mpb);

            // Ключ включаем на ИНСТАНСАХ материалов (materials), иначе может не сработать
            foreach (var m in r.materials)
            {
                if (!m) continue;
                if (emission.maxColorComponent > 0f) m.EnableKeyword("_EMISSION");
                else m.DisableKeyword("_EMISSION");
            }
        }
    }
}
