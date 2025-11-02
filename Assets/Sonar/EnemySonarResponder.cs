using System.Collections;
using UnityEngine;

/// <summary>
/// –еагирует на прохождение любой активной волны: зажигает врага синим на 1 секунду.
/// –аботает на уровне материала Ч не освещает окружение.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class EnemySonarResponder : MonoBehaviour
{
    [Header("ѕодсветка")]
    public Color glowColor = new Color(0.4f, 0.8f, 1f, 1f);
    public float glowIntensity = 1.4f;   // насколько €рче базового
    public float glowDuration = 1.0f;    // как долго горит после попадани€ волны
    public string shaderName = "Custom/SonarUnlit";

    Renderer[] rends;
    MaterialPropertyBlock mpb;

    bool isGlowing;
    float lastTriggerTime = -999f;

    // защита от многократных срабатываний в пределах ширины кольца
    float rearmDelay = 0.25f;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        var sc = SonarController.Instance;
        if (sc == null) return;

        // ѕровер€ем попадание любой волны по горизонту
        Vector3 p = transform.position;

        for (int i = 0; i < sc.ActiveWaveCount; i++)
        {
            Vector3 o = sc.GetOrigin(i);
            float t0 = sc.GetStartTime(i);

            float age = Time.time - t0;
            if (age <= 0) continue;

            float r = sc.waveSpeed * age;
            float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(o.x, 0, o.z));

            if (Mathf.Abs(d - r) <= sc.waveWidth * 0.5f)
            {
                if (Time.time - lastTriggerTime > rearmDelay)
                {
                    lastTriggerTime = Time.time;
                    if (!isGlowing) StartCoroutine(GlowCo());
                }
            }
        }
    }

    IEnumerator GlowCo()
    {
        isGlowing = true;

        // ”станавливаем свойства шейдера на врем€ свечени€
        foreach (var r in rends)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_SonarColor", glowColor);
            mpb.SetFloat("_MaxReveal", glowIntensity);
            r.SetPropertyBlock(mpb);
        }

        yield return new WaitForSeconds(glowDuration);

        // ¬озврат к обычным значени€м
        foreach (var r in rends)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_MaxReveal", 0f); // только от волн (а волна уже ушла)
            r.SetPropertyBlock(mpb);
        }

        isGlowing = false;
    }
}
