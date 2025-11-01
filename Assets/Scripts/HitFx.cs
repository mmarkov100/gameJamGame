using System.Collections;
using UnityEngine;

public class HitFx : MonoBehaviour
{
    static Material MakeMat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Surface", 1);   // transparent
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        return m;
    }

    // Кольцо на полу (индикатор атаки)
    public static void ShowRing(Vector3 pos, float radius, Color color, float duration = 0.25f, float yOffset = 0.4f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "FX_Ring";
        go.transform.position = new Vector3(pos.x, pos.y + yOffset, pos.z);
        go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.material = MakeMat(new Color(color.r, color.g, color.b, 0.35f));
        go.AddComponent<AutoFadeDestroy>().Begin(r.material, duration);
    }

    // Маленькая вспышка/искра в точке попадания
    public static void HitSpark(Vector3 pos, Color color, float size = 0.15f, float duration = 0.18f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = "FX_HitSpark";
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.material = MakeMat(new Color(color.r, color.g, color.b, 0.7f));
        go.AddComponent<AutoScaleFadeDestroy>().Begin(r.material, duration, size * 2.2f);
    }

    // Временная перекраска рендереров (мигание уроном)
    public static void FlashRenderers(GameObject root, Color flash, float time = 0.08f)
    {
        root.GetComponent<MonoBehaviour>()?.StartCoroutine(FlashCo(root, flash, time));
    }

    static IEnumerator FlashCo(GameObject root, Color flash, float t)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        var orig = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            var m = rends[i].material;
            orig[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", flash); else m.color = flash;
        }
        yield return new WaitForSeconds(t);
        for (int i = 0; i < rends.Length; i++)
        {
            var m = rends[i].material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", orig[i]); else m.color = orig[i];
        }
    }

    // ——— внутренние помощники ———
    class AutoFadeDestroy : MonoBehaviour
    {
        Material mat; float dur; float t;
        public void Begin(Material m, float d) { mat = m; dur = d; }
        void Update()
        {
            if (mat == null) { Destroy(gameObject); return; }
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / dur);
            Color c = mat.GetColor("_BaseColor"); c.a = 0.35f * k; mat.SetColor("_BaseColor", c);
            if (t >= dur) Destroy(gameObject);
        }
    }
    class AutoScaleFadeDestroy : MonoBehaviour
    {
        Material mat; float dur; float t; float maxScale;
        Vector3 startScale;
        public void Begin(Material m, float d, float targetScale)
        {
            mat = m; dur = d; maxScale = targetScale; startScale = transform.localScale;
        }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            transform.localScale = Vector3.Lerp(startScale, Vector3.one * maxScale, k);
            if (mat)
            {
                Color c = mat.GetColor("_BaseColor"); c.a = 0.7f * (1f - k); mat.SetColor("_BaseColor", c);
            }
            if (t >= dur) Destroy(gameObject);
        }
    }
}
