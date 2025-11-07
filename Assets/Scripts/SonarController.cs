using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public class SonarController : MonoBehaviour
{
    // === настроечные параметры (оставь как у тебя) ===
    public Transform waveOrigin;
    public Vector3 localOffset = new Vector3(0f, 0.05f, 0f);
    public float maxRadius = 25f;
    public float ringWidth = 0.7f;
    public float speed = 30f;
    public float period = 0.9f;
    [Range(1, 8)] public int maxWavesAlive = 4;

    public Color ringColor = new Color(0.4f, 0.8f, 1f, 1f);
    [Range(0f, 8f)] public float intensity = 1.2f;
    public float tailLength = 0f;       // ← для чистого кольца ставим 0
    [Range(0f, 1f)] public float worldDarkness = 0f; // ← 0, чтобы не заливать
    public bool paused = false;         // общая пауза

    // === новые события для «тишины парирования» ===
    public static event Action<bool> OnSonarSuppressedChanged; // true = тишина (нет подсветок)

    // ---- событие фронта (как раньше) ----
    public struct WaveSample
    {
        public int waveId;
        public Vector3 origin;
        public float radiusPrev;
        public float radiusNow;
        public float width;
    }
    public static event Action<WaveSample> OnWaveFrontAdvanced;

    class Wave { public int id; public Vector3 origin; public float radiusNow; }
    readonly List<Wave> _waves = new();
    float _nextEmitTime;
    static int _nextId;

    float _tempPulseSpeed = -1f, _tempPulseMax = -1f;

    // === внутренняя «тишина» (в отличие от paused в инспекторе) ===
    bool _suppressed = false;
    Coroutine _suppressCo;

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; PushShaderDefaults(); }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; OnWaveFrontAdvanced = null; PushShaderZero(); }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        _waves.Clear();
        _nextEmitTime = Time.time;
        PushShaderDefaults();
        SetSuppressed(false);
    }

    void Update()
    {
        if (waveOrigin == null) return;

        if (paused || _suppressed)
        {
            Shader.SetGlobalFloat("_SonarPaused", 1f);
            return; // ничего не двигаем и не эмитим
        }
        Shader.SetGlobalFloat("_SonarPaused", 0f);

        if (Time.time >= _nextEmitTime) { Emit(); _nextEmitTime = Time.time + period; }

        float dt = Time.deltaTime;
        float useSpeed = (_tempPulseSpeed > 0f ? _tempPulseSpeed : speed);
        float useMax = (_tempPulseMax > 0f ? _tempPulseMax : maxRadius);

        for (int i = _waves.Count - 1; i >= 0; i--)
        {
            var w = _waves[i];
            float prev = w.radiusNow;
            w.radiusNow += useSpeed * dt;

            // рассылка фронта — только если НЕ suppressed
            OnWaveFrontAdvanced?.Invoke(new WaveSample
            {
                waveId = w.id,
                origin = w.origin,
                radiusPrev = prev,
                radiusNow = w.radiusNow,
                width = ringWidth
            });

            if (w.radiusNow > useMax + ringWidth * 2f) _waves.RemoveAt(i);
        }

        if (_waves.Count > 0)
        {
            var w = _waves[^1];
            Shader.SetGlobalVector("_SonarOrigin", new Vector4(w.origin.x, w.origin.y, w.origin.z, 0f));
            Shader.SetGlobalFloat("_SonarRadius", w.radiusNow);
            Shader.SetGlobalFloat("_SonarWidth", Mathf.Max(0.001f, ringWidth));
            Shader.SetGlobalFloat("_SonarTail", Mathf.Max(0f, tailLength));
            Shader.SetGlobalVector("_SonarColor", (Vector4)ringColor);
            Shader.SetGlobalFloat("_SonarIntensity", intensity);
            Shader.SetGlobalFloat("_WorldDarkness", worldDarkness);
        }
        else
        {
            Shader.SetGlobalFloat("_SonarRadius", 0f);
        }

        _tempPulseSpeed = -1f; _tempPulseMax = -1f;
    }

    void Emit()
    {
        var w = new Wave { id = _nextId++, origin = waveOrigin.TransformPoint(localOffset), radiusNow = 0f };
        _waves.Add(w);
        if (_waves.Count > maxWavesAlive) _waves.RemoveAt(0);
    }

    public void EmitParryPulse(float width, float speed, float maxRadius, Color color, float inten, float tail)
    {
        if (waveOrigin == null) return;
        var w = new Wave { id = _nextId++, origin = waveOrigin.TransformPoint(localOffset), radiusNow = 0f };
        _waves.Add(w); if (_waves.Count > maxWavesAlive) _waves.RemoveAt(0);

        Shader.SetGlobalVector("_SonarColor", (Vector4)color);
        Shader.SetGlobalFloat("_SonarIntensity", inten);
        Shader.SetGlobalFloat("_SonarWidth", Mathf.Max(0.001f, width));
        Shader.SetGlobalFloat("_SonarTail", Mathf.Max(0f, tail));

        _tempPulseSpeed = speed; _tempPulseMax = maxRadius;
    }

    // === Пауза сонара на N секунд (для парирования) ===
    public void SuppressForSeconds(float seconds)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_suppressCo != null) StopCoroutine(_suppressCo);
        _suppressCo = StartCoroutine(SuppressCo(seconds));
    }
    System.Collections.IEnumerator SuppressCo(float t)
    {
        SetSuppressed(true);
        yield return new WaitForSeconds(t);
        SetSuppressed(false);
    }
    void SetSuppressed(bool v)
    {
        _suppressed = v;
        Shader.SetGlobalFloat("_SonarPaused", v ? 1f : 0f);
        OnSonarSuppressedChanged?.Invoke(v); // враги/игрок погаснут/включатся
    }

    // утилиты шейдера
    void PushShaderDefaults()
    {
        Shader.SetGlobalVector("_SonarOrigin", Vector4.zero);
        Shader.SetGlobalFloat("_SonarRadius", 0f);
        Shader.SetGlobalFloat("_SonarWidth", Mathf.Max(0.001f, ringWidth));
        Shader.SetGlobalFloat("_SonarTail", Mathf.Max(0f, tailLength));
        Shader.SetGlobalVector("_SonarColor", (Vector4)ringColor);
        Shader.SetGlobalFloat("_SonarIntensity", intensity);
        Shader.SetGlobalFloat("_WorldDarkness", worldDarkness);
        Shader.SetGlobalFloat("_SonarPaused", paused ? 1f : 0f);
    }
    void PushShaderZero()
    {
        Shader.SetGlobalFloat("_SonarPaused", 1f);
        Shader.SetGlobalFloat("_SonarRadius", 0f);
    }
}
