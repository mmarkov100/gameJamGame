using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public class LoreSceneController : MonoBehaviour
{
    [Header("Fade")]
    public CanvasGroup fade;          // CanvasGroup на чёрной плашке (FadePanel)
    public float fadeIn = 0.8f;       // секунды: от 1 -> 0
    public float fadeOut = 0.8f;      // секунды: от 0 -> 1
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Dialog hookup")]
    [Tooltip("Корневой объект фоновой плашки диалога (DialogBackground). Будет включён после fade-in.")]
    public GameObject dialogBackground;
    [Tooltip("Компонент Dialog, у которого вызывается Begin() и который вызовет RequestAdvance().")]
    public Dialog dialog;
    [Tooltip("Если ссылки не заданы — попытаемся найти автоматически на сцене.")]
    public bool autoFindDialog = true;

    bool requested;   // ставится в true из Dialog через RequestAdvance()

    void Reset()
    {
        fade = GetComponent<CanvasGroup>();
        if (fade)
        {
            fade.alpha = 1f;
            fade.blocksRaycasts = true;
            fade.interactable = true;
        }
    }

    void Awake()
    {
        if (!fade) fade = GetComponent<CanvasGroup>();

        // На старте — чёрный экран и блокировка кликов
        if (fade)
        {
            fade.alpha = 1f;
            fade.blocksRaycasts = true;
            fade.interactable = true;
        }

        // Если кто-то отключает DialogBackground в начале — нам это не мешает:
        // мы сами включим его после fade-in.
    }

    IEnumerator Start()
    {
        // Музыку оставляем ту же, что пришла с предыдущей сцены (если есть MusicDirector)
        // (Если MusicDirector отсутствует, вызов просто ничего не сделает.)
        try { MusicDirector.Instance?.AdoptForActiveScene(); } catch { }

        // Мягкое появление
        yield return Fade(1f, 0f, fadeIn);

        if (fade)
        {
            fade.blocksRaycasts = false;
            fade.interactable = false;
        }

        // ——— Подготовка и запуск диалога ———
        if (autoFindDialog)
        {
            if (dialog == null) dialog = FindObjectOfType<Dialog>(includeInactive: true);
            if (dialogBackground == null && dialog != null) dialogBackground = dialog.gameObject;
        }

        if (dialogBackground) dialogBackground.SetActive(true);   // гарантируем, что визуальная часть включена
        if (dialog != null) dialog.Begin();
        else Debug.LogWarning("[LoreSceneController] Dialog не найден. Сцена останется на месте до вызова RequestAdvance().");

        // ——— Ждём сигнал от Dialog ———
        yield return new WaitUntil(() => requested);

        // Фейд-аут и переход на следующую сцену
        if (fade)
        {
            fade.blocksRaycasts = true;
            fade.interactable = true;
        }

        yield return Fade(0f, 1f, fadeOut);
        LoadNextSceneByBuildIndex();
    }

    /// <summary>
    /// Вызывается Dialog'ом после последней страницы.
    /// </summary>
    public void RequestAdvance()
    {
        requested = true;
    }

    // ============ helpers ============

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!fade)
            yield break;

        duration = Mathf.Max(0f, duration);
        if (duration == 0f)
        {
            fade.alpha = to;
            yield break;
        }

        float t = 0f;
        fade.alpha = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float k = fadeCurve != null ? fadeCurve.Evaluate(p) : p;
            fade.alpha = Mathf.LerpUnclamped(from, to, k);
            yield return null;
        }

        fade.alpha = to;
    }

    void LoadNextSceneByBuildIndex()
    {
        int cur = SceneManager.GetActiveScene().buildIndex;
        int next = cur + 1;

        if (next < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
        else
        {
            Debug.LogWarning("[LoreSceneController] Следующей сцены в Build Settings нет. Остаёмся на текущей.");
        }
    }
}
