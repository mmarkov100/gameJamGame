using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Вспышка врага при прохождении волны сонара (через Emission).
/// - Работает с любыми материалами, у которых есть _EmissionColor (URP/Built-in).
/// - Не требует рендера на самом объекте: может висеть на корне, соберёт детей.
/// - Глобально подавляется на время парри (SetSuppressed).
/// - После перезагрузки сцены статика сбрасывается (фикс неработающих вспышек).
/// </summary>
[DisallowMultipleComponent]
public class EnemySonarResponder : MonoBehaviour
{
    // ===== Глобальное состояние (реестр + подавление) =====
    static readonly List<EnemySonarResponder> _registry = new();
    static bool _suppressed = false;

    /// <summary>Включить/выключить глобальное подавление свечения (напр., при парри).</summary>
    public static void SetSuppressed(bool value)
    {
        _suppressed = value;
        if (value)
        {
            // мгновенно погасить всех текущих
            for (int i = 0; i < _registry.Count; i++)
                if (_registry[i] != null) _registry[i].StopGlowNow();
        }
    }

    /// <summary>Сброс статики после загрузки любой сцены (важно при Reload).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ResetStaticsOnSceneLoad()
    {
        _suppressed = false;
        _registry.Clear();
    }

    // ===== Параметры подсветки =====
    [Header("Подсветка при касании волной")]
    public Color glowColor = new(0.40f, 0.80f, 1.00f, 1f);
    [Tooltip("Множитель яркости эмиссии")]
    public float glowIntensity = 1.6f;
    [Tooltip("Сколько секунд горит после касания")]
    public float glowDuration = 1.0f;
    [Tooltip("Добавочная толщина фронта волны (меньше пропусков)")]
    public float triggerTolerance = 0.9f;

    [Header("Охват рендереров")]
    [Tooltip("Собирать все Renderer в детях (рекомендуется для корневого объекта врага)")]
    public bool includeChildren = true;

    // ===== Внутреннее =====
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    Coroutine glowCo;
    int lastTriggeredWave = -1;

    void OnEnable()
    {
        if (!_registry.Contains(this)) _registry.Add(this);
    }

    void OnDisable()
    {
        _registry.Remove(this);
        StopGlowNow(); // на всякий случай погасим при выключении
    }

    void Awake()
    {
        rends = includeChildren
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        mpb = new MaterialPropertyBlock();

        // Гарантируем, что эмиссия может работать на инстансах материалов
        foreach (var r in rends)
        {
            if (!r) continue;
            foreach (var m in r.materials) // именно materials (инстансы), не sharedMaterials
            {
                if (!m) continue;
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", Color.black);
            }
        }

        // стартовое состояние — не светимся
        SetEmission(Color.black);
    }

    void Update()
    {
        if (_suppressed) return;

        var sonar = SonarController.Instance;
        if (sonar == null || sonar.paused) return;

        // защитимся от повторного срабатывания на ту же волну
        if (lastTriggeredWave == sonar.WaveIndex) return;

        // проверка близости фронта (по XZ)
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

        // старт — зажечь максимально
        SetEmission(glowColor * glowIntensity);

        while (t < glowDuration && !_suppressed)
        {
            t += Time.deltaTime;
            // плавное затухание
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / glowDuration);
            SetEmission(glowColor * (glowIntensity * Mathf.Max(0.0001f, k)));
            yield return null;
        }

        SetEmission(Color.black);
        glowCo = null;
    }

    /// <summary>Мгновенно погасить подсветку (используется при парри и выключении).</summary>
    public void StopGlowNow()
    {
        if (glowCo != null) { StopCoroutine(glowCo); glowCo = null; }
        SetEmission(Color.black);

        // чтобы в кадр перезагрузки/подавления не перетриггериться текущей волной
        var sonar = SonarController.Instance;
        if (sonar) lastTriggeredWave = sonar.WaveIndex;
    }

    // ===== Работа с эмиссией через MPB + ключ _EMISSION =====
    void SetEmission(Color emission)
    {
        foreach (var r in rends)
        {
            if (!r) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", emission);
            r.SetPropertyBlock(mpb);

            // Включаем/выключаем ключ на инстансах материалов,
            // чтобы даже не-HDRP/URP шейдеры корректно реагировали.
            bool on = emission.maxColorComponent > 0f;
            foreach (var m in r.materials)
            {
                if (!m) continue;
                if (on) m.EnableKeyword("_EMISSION");
                else m.DisableKeyword("_EMISSION");
            }
        }
    }
}
