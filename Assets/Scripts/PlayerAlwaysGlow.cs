using System.Collections.Generic; // NEW
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PlayerAlwaysGlow : MonoBehaviour
{
    public Color glowColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 6f)] public float glowIntensity = 1.2f;
    public bool includeChildren = true;

    Material glowMat;
    Renderer[] rends;
    int intensityID;
    bool attached = false; // NEW

    void Awake()
    {
        rends = includeChildren ? GetComponentsInChildren<Renderer>(true)
                                : new[] { GetComponent<Renderer>() };

        glowMat = new Material(Shader.Find("Sonar/UnlitGlow"));
        glowMat.SetColor("_GlowColor", glowColor);
        glowMat.SetFloat("_GlowIntensity", glowIntensity);

        intensityID = Shader.PropertyToID("_GlowIntensity");

        Attach(); // по умолчанию прикрепл€ем
    }

    void Update()
    {
        if (attached)
            glowMat.SetFloat(intensityID, glowIntensity);
    }

    // ЧЧЧ ”правление подавлением ЧЧЧ
    public void SetSuppressed(bool v) // NEW
    {
        if (v) Detach();
        else Attach();
    }

    void Attach() // NEW
    {
        if (attached) return;
        foreach (var r in rends)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            // не дублировать
            if (System.Array.Exists(mats, m => m == glowMat)) continue;
            System.Array.Resize(ref mats, mats.Length + 1);
            mats[mats.Length - 1] = glowMat;
            r.sharedMaterials = mats;
        }
        attached = true;
    }

    void Detach() // NEW
    {
        if (!attached) return;
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
        attached = false;
    }
}
