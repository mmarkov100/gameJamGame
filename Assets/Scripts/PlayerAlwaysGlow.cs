using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ѕосто€нное свечение игрока без вли€ни€ на окружение.
/// –ежимы:
///  - Emission: задаЄт _EmissionColor через MaterialPropertyBlock (нужен ключ _EMISSION).
///  - OverlayUnlitGlow: управл€ет материалом-оверлеем Sonar/UnlitGlow по индексу.
/// ¬о врем€ подавлени€ сонара (парирование) подсветка гаснет.
/// </summary>
public class PlayerAlwaysGlow : MonoBehaviour
{
    public enum GlowMode { Emission, OverlayUnlitGlow }

    [Header("–ежим")]
    public GlowMode mode = GlowMode.Emission;

    [Header("–ендереры")]
    [Tooltip("≈сли true Ч соберЄм все Renderer в дочерних объектах; иначе только на этом объекте.")]
    public bool includeChildren = true;

    [Tooltip("≈сли указаны Ч используем только их вместо автосбора.")]
    public List<Renderer> overrideRenderers = new List<Renderer>();

    [Header("÷вет/€ркость")]
    public Color glowColor = new(1f, 0.1f, 0.1f, 1f); // красный
    [Tooltip("ƒл€ Emission Ч множитель к цвету; дл€ Overlay Ч значение _GlowIntensity.")]
    public float glowIntensity = 1.5f;

    [Header("Emission")]
    [Tooltip("»м€ свойства эмиссии в шейдере.")]
    public string emissionColorProperty = "_EmissionColor";
    [Tooltip("¬ключать ключ _EMISSION (только во врем€ Play, чтобы не плодить материалы в редакторе).")]
    public bool enableEmissionKeywordAtRuntime = true;

    [Header("Overlay (Sonar/UnlitGlow)")]
    [Tooltip("»ндекс материала, где лежит Sonar/UnlitGlow (обычно 1).")]
    public int overlayMaterialIndex = 1;
    public string overlayColorProp = "_GlowColor";
    public string overlayIntensityProp = "_GlowIntensity";
    [Tooltip("≈сли у какого-то рендера нет материала с таким индексом Ч писать в 0.")]
    public bool overlayFallbackToIndex0 = true;

    // кеши
    readonly List<Renderer> _renderers = new();
    readonly Dictionary<Renderer, MaterialPropertyBlock> _mpb = new();

    bool _active = true; // светить/не светить (гасим при парировании)

    void OnEnable()
    {
        CollectRenderers();
        PrepareEmissionKeywordIfNeeded();   // включает _EMISSION только в Play
        EnsureBlocks();
        ApplyNow();

        SonarController.OnSonarSuppressedChanged += OnSuppressedChanged;
    }

    void OnDisable()
    {
        SonarController.OnSonarSuppressedChanged -= OnSuppressedChanged;
        _active = false;
        ApplyNow();
    }

    // не трогаем материалы в редакторе Ч иначе будут ворнинги
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        CollectRenderers();
        EnsureBlocks();
        ApplyNow();
    }

    void OnSuppressedChanged(bool suppressed)
    {
        _active = !suppressed;
        ApplyNow();
    }

    // ---------- сбор рендереров ----------
    void CollectRenderers()
    {
        _renderers.Clear();

        if (overrideRenderers != null && overrideRenderers.Count > 0)
        {
            foreach (var r in overrideRenderers)
                if (r) _renderers.Add(r);
            return;
        }

        if (includeChildren)
            GetComponentsInChildren(true, _renderers);
        else
        {
            var r = GetComponent<Renderer>();
            if (r) _renderers.Add(r);
        }
    }

    // ---------- включаем _EMISSION только в Play ----------
    void PrepareEmissionKeywordIfNeeded()
    {
        if (mode != GlowMode.Emission) return;
        if (!enableEmissionKeywordAtRuntime) return;
        if (!Application.isPlaying) return;

        // ¬ рантайме можно смело дернуть r.materials Ч Unity создаст инстансы без ворнингов.
        foreach (var r in _renderers)
        {
            if (!r) continue;
            var mats = r.materials; // OK: только в Play
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && !mats[i].IsKeywordEnabled("_EMISSION"))
                    mats[i].EnableKeyword("_EMISSION");
        }
    }

    // ---------- MPB дл€ каждого рендера ----------
    void EnsureBlocks()
    {
        _mpb.Clear();
        foreach (var r in _renderers)
        {
            if (!r) continue;
            _mpb[r] = new MaterialPropertyBlock();
        }
    }

    // ---------- примен€ем свечение ----------
    void ApplyNow()
    {
        foreach (var r in _renderers)
        {
            if (!r) continue;

            // получить/создать блок
            if (!_mpb.TryGetValue(r, out var b) || b == null)
            {
                b = new MaterialPropertyBlock();
                _mpb[r] = b;
            }

            if (mode == GlowMode.Emission)
            {
                r.GetPropertyBlock(b); // без индекса Ч на весь рендер
                var col = _active ? (glowColor * glowIntensity) : Color.black;
                if (!string.IsNullOrEmpty(emissionColorProperty))
                    b.SetColor(emissionColorProperty, col);
                r.SetPropertyBlock(b);
            }
            else // OverlayUnlitGlow
            {
                int idx = overlayMaterialIndex;
                var shared = r.sharedMaterials;               // безопасно в редакторе
                if (idx < 0 || idx >= shared.Length)
                {
                    if (overlayFallbackToIndex0 && shared.Length > 0) idx = 0;
                    else continue; // пропускаем рендерер без подход€щего индекса
                }

                r.GetPropertyBlock(b, idx);
                if (!string.IsNullOrEmpty(overlayColorProp))
                    b.SetColor(overlayColorProp, glowColor);
                if (!string.IsNullOrEmpty(overlayIntensityProp))
                    b.SetFloat(overlayIntensityProp, _active ? Mathf.Max(0f, glowIntensity) : 0f);
                r.SetPropertyBlock(b, idx);
            }
        }
    }
}
