using System.Collections;
using System.Collections.Generic; // NEW
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class EnemySonarResponder : MonoBehaviour
{
    // === Глобальное подавление для всех врагов ===
    static readonly List<EnemySonarResponder> _registry = new List<EnemySonarResponder>(); // NEW
    static bool _suppressed = false; // NEW

    public static void SetSuppressed(bool v) // NEW
    {
        _suppressed = v;
        if (v)
        {
            // Сразу снять все текущие свечения
            foreach (var r in _registry)
                r.StopGlowNow();
        }
    }

    // ========== дальше как было ==========
    [Header("Подсветка")]
    public Color glowColor = new Color(0.4f, 0.8f, 1f, 1f);
    public float glowIntensity = 1.6f;
    public float glowDuration = 1.0f;
    public float triggerTolerance = 0.9f;

    [Header("Что подсвечивать")]
    public bool includeChildren = true;

    Material glowMat;
    Renderer[] rends;
    int lastTriggeredWave = -1;
    int emissionID;

    Coroutine glowCo; // NEW — чтобы можно было отменять

    void OnEnable() // NEW
    {
        if (!_registry.Contains(this)) _registry.Add(this);
    }

    void OnDisable() // NEW
    {
        _registry.Remove(this);
    }

    void Awake()
    {
        rends = includeChildren ? GetComponentsInChildren<Renderer>(true)
                                : new[] { GetComponent<Renderer>() };

        glowMat = new Material(Shader.Find("Sonar/UnlitGlow"));
        glowMat.SetColor("_GlowColor", glowColor);
        glowMat.SetFloat("_GlowIntensity", glowIntensity);

        emissionID = Shader.PropertyToID("_GlowIntensity");
    }

    void Update()
    {
        if (_suppressed) return; // NEW: при подавлении ничего не делаем

        var sonar = SonarController.Instance;
        if (sonar == null || sonar.paused) return;

        if (lastTriggeredWave == sonar.WaveIndex) return;

        if (sonar.IsFrontNearHorizontal(transform.position, sonar.waveWidth * 0.5f + triggerTolerance))
        {
            lastTriggeredWave = sonar.WaveIndex;
            if (glowCo != null) StopCoroutine(glowCo);
            glowCo = StartCoroutine(GlowCo());
        }
    }

    IEnumerator GlowCo()
    {
        // навешиваем мат как второй (аддитивный)
        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = glowMat;
            r.sharedMaterials = mats;
        }

        float t = 0f;
        while (t < glowDuration && !_suppressed) // NEW: прерывание, если подавлено
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / glowDuration);
            glowMat.SetFloat(emissionID, glowIntensity * Mathf.Max(0.0001f, k));
            yield return null;
        }

        // снимаем мат
        RemoveGlowMat();
        glowCo = null;
    }

    // Снять подсветку мгновенно (вызывается при парри)
    public void StopGlowNow() // NEW
    {
        if (glowCo != null) { StopCoroutine(glowCo); glowCo = null; }
        RemoveGlowMat();
    }

    void RemoveGlowMat()
    {
        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            int idx = System.Array.FindIndex(mats, m => m == glowMat);
            if (idx >= 0)
            {
                var list = new List<Material>(mats);
                list.RemoveAt(idx);
                r.sharedMaterials = list.ToArray();
            }
        }
    }
}
