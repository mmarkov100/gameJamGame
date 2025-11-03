using System.Collections;
using UnityEngine;

/// <summary>
/// Вешается на врага. Когда фронт волны проходит по врагу,
/// включает синюю подсветку на glowDuration секунд (аддитивно, без освещения мира).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnemySonarResponder : MonoBehaviour
{
    [Header("Подсветка")]
    public Color glowColor = new Color(0.4f, 0.8f, 1f, 1f);
    public float glowIntensity = 1.6f;     // множитель яркости эмиссии
    public float glowDuration = 1.0f;      // сколько горит после касания волной
    public float triggerTolerance = 0.9f;  // насколько «толстым» считаем фронт для события

    [Header("Что подсвечивать")]
    public bool includeChildren = true;

    Material glowMat;
    Renderer[] rends;
    int lastTriggeredWave = -1;
    int emissionID;

    void Awake()
    {
        rends = includeChildren ? GetComponentsInChildren<Renderer>(true)
                                : new[] { GetComponent<Renderer>() };

        // Материал однотонной эмиссии (Unlit), не влияет на освещение
        glowMat = new Material(Shader.Find("Sonar/UnlitGlow"));
        glowMat.SetColor("_GlowColor", glowColor);
        glowMat.SetFloat("_GlowIntensity", glowIntensity);

        emissionID = Shader.PropertyToID("_GlowIntensity");
    }

    void Update()
    {
        var sonar = SonarController.Instance;
        if (sonar == null || sonar.paused) return;

        // Уже срабатывали на текущую волну?
        if (lastTriggeredWave == sonar.WaveIndex) return;

        // Волна рядом с этим объектом?
        if (sonar.IsFrontNearHorizontal(transform.position, sonar.waveWidth * 0.5f + triggerTolerance))
        {
            lastTriggeredWave = sonar.WaveIndex;
            StartCoroutine(GlowCo());
        }
    }

    IEnumerator GlowCo()
    {
        // навешиваем мат как второй (аддитивный) на все рендереры
        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = glowMat;
            r.sharedMaterials = mats;
        }

        float t = 0f;
        while (t < glowDuration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / glowDuration); // плавное затухание
            glowMat.SetFloat(emissionID, glowIntensity * Mathf.Max(0.0001f, k));
            yield return null;
        }

        // снимаем мат
        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            int idx = System.Array.FindIndex(mats, m => m == glowMat);
            if (idx >= 0)
            {
                var list = new System.Collections.Generic.List<Material>(mats);
                list.RemoveAt(idx);
                r.sharedMaterials = list.ToArray();
            }
        }
    }
}
