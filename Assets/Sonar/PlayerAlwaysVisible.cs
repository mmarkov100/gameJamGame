using UnityEngine;

/// <summary>
/// Делает игрока всегда видимым в шейдере «SonarUnlit»
/// (не освещает окружение).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class PlayerAlwaysVisible : MonoBehaviour
{
    public Color playerColor = new Color(1f, 0.95f, 0.8f, 1f);
    public float emission = 1.2f;

    Renderer[] rends;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        foreach (var r in rends)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_AlwaysVisible", 1f);
            mpb.SetColor("_SonarColor", playerColor);
            mpb.SetFloat("_AlwaysEmit", emission);
            r.SetPropertyBlock(mpb);
        }
    }
}
