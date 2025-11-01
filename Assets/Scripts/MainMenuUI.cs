using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI")]
    public Button playButton;
    public Slider volumeSlider;

    [Header("Fade")]
    public CanvasGroup fadeGroup;     // CanvasGroup на чёрной плашке, растянутой на весь экран
    public float fadeDuration = 0.8f; // время фейда

    bool isTransition; // защита от двойных нажатий

    void Awake()
    {
        // стартуем с чёрного экрана
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;  // чтобы клики не проходили во время фейда
            fadeGroup.interactable = false;
        }
    }

    void Start()
    {
        // ---- громкость ----
        var mgr = AudioManager.Instance;
        if (mgr != null && volumeSlider != null)
            volumeSlider.value = mgr.GetVolume01();

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(v =>
            {
                AudioManager.Instance?.SetVolume01(v);
            });
        }

        // ---- кнопка Play ----
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        // ---- Fade In при входе ----
        if (fadeGroup != null)
            StartCoroutine(Fade(1f, 0f, fadeDuration, onDone: () =>
            {
                fadeGroup.blocksRaycasts = false;
                fadeGroup.interactable = false;
            }));
    }

    void OnPlayClicked()
    {
        if (isTransition) return;
        isTransition = true;

        // блокируем клики на время затемнения
        if (fadeGroup != null)
        {
            fadeGroup.blocksRaycasts = true;
            fadeGroup.interactable = true;
            StartCoroutine(FadeOutAndLoadNext());
        }
        else
        {
            // запасной вариант без фейда
            LoadNextScene();
        }
    }

    IEnumerator FadeOutAndLoadNext()
    {
        yield return Fade(0f, 1f, fadeDuration);
        LoadNextScene();
    }

    void LoadNextScene()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.LogWarning("Следующая сцена отсутствует в Build Settings.");
    }

    IEnumerator Fade(float from, float to, float duration, System.Action onDone = null)
    {
        if (fadeGroup == null || duration <= 0f)
        {
            if (fadeGroup != null) fadeGroup.alpha = to;
            onDone?.Invoke();
            yield break;
        }

        float t = 0f;
        fadeGroup.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeGroup.alpha = to;
        onDone?.Invoke();
    }
}
