using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
using UnityEngine.Rendering.Universal; // дл€ Light2D
#endif

public class ParrySonarSwitcher : MonoBehaviour
{
    [Header("ѕодписка на событие парировани€")]
    public PlayerParry parry; // если не задано Ч найдЄм у родител€

    [Header("Ёффекты, которые просто включаютс€ на врем€")]
    public Behaviour[] enableOnParry;
    [Tooltip("ќбща€ длительность вспышки (дл€ всех источников и Behaviour)")]
    public float lightPulseDuration = 0.4f;

    [Header("ѕлавность (обща€)")]
    [Range(0f, 1f)] public float fadeFraction = 0.0f; // 0 Ч мгновенно; 1 Ч ап/даун занимают всю длительность

    [System.Serializable]
    public class LightEntry3D
    {
        public Light light;
        [Tooltip("÷елева€ €ркость во врем€ вспышки")]
        public float targetIntensity = 3f;
        [Tooltip("≈сли свет выключен Ч временно включить на вспышку")]
        public bool enableIfDisabled = true;
    }

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
    [System.Serializable]
    public class LightEntry2D
    {
        public Light2D light2D;
        public float targetIntensity = 1.5f;
        public bool enableIfDisabled = true;
    }
#endif

    [Header("3D источники света (индивидуальна€ €ркость)")]
    public List<LightEntry3D> lights3D = new List<LightEntry3D>();

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
    [Header("URP 2D источники света (индивидуальна€ €ркость)")]
    public List<LightEntry2D> lights2D = new List<LightEntry2D>();
#endif

    [Header("ƒополнительно: пульс сонара при парри")]
    public SonarController sonarController;
    public bool spawnExtraSonarPulse = true;
    public float pulseWidth = 1.2f;
    public float pulseSpeed = 42f;
    public float pulseMaxRadius = 28f;
    public Color pulseColor = new Color(1f, 0.5f, 0.2f, 1f);
    public float pulseIntensity = 1.8f;
    public float pulseTail = 2f;

    // ЧЧЧ исходные значени€, чтобы корректно вернуть состо€ние ЧЧЧ
    readonly Dictionary<Light, float> _orig3DIntensity = new();
    readonly Dictionary<Light, bool> _orig3DEnabled = new();

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
    readonly Dictionary<Light2D, float> _orig2DIntensity = new();
    readonly Dictionary<Light2D, bool>  _orig2DEnabled   = new();
#endif

    Coroutine _running;

    void Awake()
    {
        if (parry == null) parry = GetComponentInParent<PlayerParry>();
    }

    void OnEnable()
    {
        if (parry != null) parry.OnParrySuccess.AddListener(HandleParry);
        SetBehaviours(false);
    }

    void OnDisable()
    {
        if (parry != null) parry.OnParrySuccess.RemoveListener(HandleParry);
        RestoreAllLightsNow();
        SetBehaviours(false);
    }

    // публичный Ч можно повесить пр€мо в UnityEvent
    public void HandleParry()
    {
        if (sonarController != null)
            sonarController.SuppressForSeconds(lightPulseDuration); // или onTime, что у теб€ общее
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(DoPulse());

        if (spawnExtraSonarPulse && sonarController != null)
        {
            sonarController.EmitParryPulse(
                pulseWidth, pulseSpeed, pulseMaxRadius,
                pulseColor, pulseIntensity, pulseTail
            );
        }
    }

    IEnumerator DoPulse()
    {
        float dur = Mathf.Max(0f, lightPulseDuration);
        float fade = Mathf.Clamp01(fadeFraction);
        float upTime = dur * fade * 0.5f;
        float downTime = dur * fade * 0.5f;
        float holdTime = dur - upTime - downTime;

        // запомнить исходные значени€ (один раз дл€ каждого источника)
        CacheOriginals();

        // включить Behaviour на общее врем€
        SetBehaviours(true);

        // включить выключенные источники (если разрешено)
        PrepareLightsEnable();

        // ап (все источники параллельно)
        if (upTime > 0f)
        {
            float t = 0f;
            while (t < upTime)
            {
                t += Time.deltaTime;
                float k = t / Mathf.Max(0.0001f, upTime);
                LerpAllToTarget(k);
                yield return null;
            }
        }
        else
        {
            SetAllToTarget();
        }

        // удержание
        if (holdTime > 0f)
            yield return new WaitForSeconds(holdTime);

        // даун
        if (downTime > 0f)
        {
            float t = 0f;
            while (t < downTime)
            {
                t += Time.deltaTime;
                float k = t / Mathf.Max(0.0001f, downTime);
                LerpAllToOriginal(k);
                yield return null;
            }
        }
        else
        {
            RestoreAllIntensities();
        }

        // вернуть enabled тем, кто был изначально выключен
        RestoreAllEnabledFlags();

        // выключить Behaviour
        SetBehaviours(false);

        _running = null;
    }

    // ЧЧЧ служебные методы ЧЧЧ

    void CacheOriginals()
    {
        foreach (var e in lights3D)
        {
            if (e.light == null) continue;
            if (!_orig3DIntensity.ContainsKey(e.light))
            {
                _orig3DIntensity[e.light] = e.light.intensity;
                _orig3DEnabled[e.light] = e.light.enabled;
            }
        }
#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var e in lights2D)
        {
            if (e.light2D == null) continue;
            if (!_orig2DIntensity.ContainsKey(e.light2D))
            {
                _orig2DIntensity[e.light2D] = e.light2D.intensity;
                _orig2DEnabled[e.light2D]   = e.light2D.enabled;
            }
        }
#endif
    }

    void PrepareLightsEnable()
    {
        foreach (var e in lights3D)
            if (e.light && e.enableIfDisabled && !_orig3DEnabled[e.light]) e.light.enabled = true;

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var e in lights2D)
            if (e.light2D && e.enableIfDisabled && !_orig2DEnabled[e.light2D]) e.light2D.enabled = true;
#endif
    }

    void LerpAllToTarget(float k)
    {
        foreach (var e in lights3D)
            if (e.light) e.light.intensity = Mathf.Lerp(_orig3DIntensity[e.light], e.targetIntensity, k);

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var e in lights2D)
            if (e.light2D) e.light2D.intensity = Mathf.Lerp(_orig2DIntensity[e.light2D], e.targetIntensity, k);
#endif
    }

    void SetAllToTarget()
    {
        foreach (var e in lights3D)
            if (e.light) e.light.intensity = e.targetIntensity;

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var e in lights2D)
            if (e.light2D) e.light2D.intensity = e.targetIntensity;
#endif
    }

    void LerpAllToOriginal(float k)
    {
        foreach (var e in lights3D)
            if (e.light) e.light.intensity = Mathf.Lerp(e.targetIntensity, _orig3DIntensity[e.light], k);

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var e in lights2D)
            if (e.light2D) e.light2D.intensity = Mathf.Lerp(e.targetIntensity, _orig2DIntensity[e.light2D], k);
#endif
    }

    void RestoreAllIntensities()
    {
        foreach (var kv in _orig3DIntensity)
            if (kv.Key) kv.Key.intensity = kv.Value;

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var kv in _orig2DIntensity)
            if (kv.Key) kv.Key.intensity = kv.Value;
#endif
    }

    void RestoreAllEnabledFlags()
    {
        foreach (var kv in _orig3DEnabled)
            if (kv.Key) kv.Key.enabled = kv.Value;

#if UNITY_RENDER_PIPELINE_URP || UNITY_URP
        foreach (var kv in _orig2DEnabled)
            if (kv.Key) kv.Key.enabled = kv.Value;
#endif
    }

    void SetBehaviours(bool state)
    {
        if (enableOnParry == null) return;
        foreach (var b in enableOnParry)
            if (b) b.enabled = state;
    }

    void RestoreAllLightsNow()
    {
        RestoreAllIntensities();
        RestoreAllEnabledFlags();
    }
}
