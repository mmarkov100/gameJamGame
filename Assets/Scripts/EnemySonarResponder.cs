using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySonarResponder : MonoBehaviour
{
    [Header("Какие рендереры подсвечивать")]
    public bool includeChildren = true;
    public List<Renderer> overrideRenderers = new();

    [Header("Overlay (Sonar/UnlitGlow)")]
    public int overlayMaterialIndex = 1;
    public bool fallbackToIndex0IfMissing = true;   // ? добавлено
    public Color glowColor = new(0.4f, 0.8f, 1f, 1f);
    public float glowIntensity = 2.5f;
    public float glowDuration = 1.0f;

    [Header("Имена свойств")]
    public string glowColorProp = "_GlowColor";
    public string glowIntensityProp = "_GlowIntensity";

    readonly List<Renderer> _renderers = new();
    readonly Dictionary<Renderer, MaterialPropertyBlock> _mpb = new();

    Coroutine _fadeCo;
    int _lastWaveIdProcessed = -1;
    bool _suppressed = false;

    void OnEnable()
    {
        CollectRenderers();
        EnsureBlocks();
        SetOverlayIntensity(0f);

        SonarController.OnWaveFrontAdvanced += OnWaveFront;
        SonarController.OnSonarSuppressedChanged += OnSuppressedChanged;
    }

    void OnDisable()
    {
        SonarController.OnWaveFrontAdvanced -= OnWaveFront;
        SonarController.OnSonarSuppressedChanged -= OnSuppressedChanged;
        SetOverlayIntensity(0f);
    }

    void OnSuppressedChanged(bool v)
    {
        _suppressed = v;
        if (v)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            SetOverlayIntensity(0f);
        }
    }

    void CollectRenderers()
    {
        _renderers.Clear();

        if (overrideRenderers != null && overrideRenderers.Count > 0)
        {
            foreach (var r in overrideRenderers) if (r) _renderers.Add(r);
            return;
        }

        if (includeChildren) GetComponentsInChildren(true, _renderers);
        else { var r = GetComponent<Renderer>(); if (r) _renderers.Add(r); }
    }

    void EnsureBlocks()
    {
        _mpb.Clear();
        foreach (var r in _renderers)
        {
            if (!r) continue;

            // защита по индексу
            int idx = overlayMaterialIndex;
            var mats = r.sharedMaterials;              // ? без инстанцирования в редакторе
            if (idx < 0 || idx >= mats.Length)
            {
                if (fallbackToIndex0IfMissing && mats.Length > 0) idx = 0;
                else continue; // пропустить рендерер вообще
            }

            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b, idx);
            _mpb[r] = b;
        }
    }

    void OnWaveFront(SonarController.WaveSample s)
    {
        if (_suppressed) return;
        if (s.waveId == _lastWaveIdProcessed) return;

        // найдём любой валидный рендерер как репрезентативную точку
        Renderer refR = null;
        for (int i = 0; i < _renderers.Count; i++)
            if (_renderers[i]) { refR = _renderers[i]; break; }
        if (!refR) return;

        float dist = Vector3.Distance(refR.bounds.center, s.origin);
        float half = s.width * 0.5f;
        float minR = Mathf.Min(s.radiusPrev, s.radiusNow) - half;
        float maxR = Mathf.Max(s.radiusPrev, s.radiusNow) + half;

        if (dist >= minR && dist <= maxR)
        {
            _lastWaveIdProcessed = s.waveId;
            TriggerGlowOnce();
        }
    }

    void TriggerGlowOnce()
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        SetOverlayIntensity(glowIntensity);
        _fadeCo = StartCoroutine(FadeOutAfter(glowDuration));
    }

    IEnumerator FadeOutAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetOverlayIntensity(0f);
        _fadeCo = null;
    }

    void SetOverlayIntensity(float intensity)
    {
        foreach (var r in _renderers)
        {
            if (!r) continue;

            int idx = overlayMaterialIndex;
            var mats = r.sharedMaterials;      // ? не инстанцирует материалы
            if (idx < 0 || idx >= mats.Length)
            {
                if (fallbackToIndex0IfMissing && mats.Length > 0) idx = 0;
                else continue;
            }

            if (!_mpb.TryGetValue(r, out var b) || b == null) b = new MaterialPropertyBlock();

            r.GetPropertyBlock(b, idx);
            b.SetColor(glowColorProp, glowColor);
            b.SetFloat(glowIntensityProp, Mathf.Max(0f, intensity));
            r.SetPropertyBlock(b, idx);

            _mpb[r] = b;
        }
    }
}
