using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class LoreSceneController : MonoBehaviour
{
    public CanvasGroup fade;
    public float fadeIn = 0.8f;
    public float fadeOut = 0.8f;

    bool requested;

    void Awake()
    {
        if (!fade) fade = GetComponent<CanvasGroup>();
        if (fade) { fade.alpha = 1f; fade.blocksRaycasts = true; }
    }

    IEnumerator Start()
    {
        // ћузыка: оставить ту же, что пришла с Wave_N
        MusicDirector.Instance?.AdoptForActiveScene();

        yield return Fade(1f, 0f, fadeIn);
        if (fade) fade.blocksRaycasts = false;

        // ждЄм, пока Dialog вызовет RequestAdvance()
        yield return new WaitUntil(() => requested);

        if (fade) fade.blocksRaycasts = true;
        yield return Fade(0f, 1f, fadeOut);

        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
    }

    public void RequestAdvance() => requested = true;

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!fade || duration <= 0f) { if (fade) fade.alpha = to; yield break; }
        float t = 0f; fade.alpha = from;
        while (t < duration) { t += Time.deltaTime; fade.alpha = Mathf.Lerp(from, to, t / duration); yield return null; }
        fade.alpha = to;
    }
}
