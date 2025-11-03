using UnityEngine;

/// <summary>
/// Держит ГГ всегда видимым с помощью аддитивного Unlit-материала.
/// Никакого влияния на окружающий свет.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PlayerAlwaysGlow : MonoBehaviour
{
    public Color glowColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 6f)] public float glowIntensity = 1.2f;
    public bool includeChildren = true;

    Material glowMat;
    Renderer[] rends;
    int intensityID;

    void Awake()
    {
        rends = includeChildren ? GetComponentsInChildren<Renderer>(true)
                                : new[] { GetComponent<Renderer>() };

        glowMat = new Material(Shader.Find("Sonar/UnlitGlow"));
        glowMat.SetColor("_GlowColor", glowColor);
        glowMat.SetFloat("_GlowIntensity", glowIntensity);

        intensityID = Shader.PropertyToID("_GlowIntensity");

        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = glowMat;
            r.sharedMaterials = mats;
        }
    }

    void Update()
    {
        // можете анимировать яркость от стамины/хп и т.п.
        glowMat.SetFloat(intensityID, glowIntensity);
    }
}
