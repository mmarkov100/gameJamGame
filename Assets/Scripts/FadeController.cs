using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class FadeController : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup fade;      // CanvasGroup на чёрной плашке
    public AudioSource logoSound; // Источник звука

    [Header("Timings")]
    public float soundDelay = 1f;   // Звук через 1 сек после старта сцены
    public float fadeInTime = 1f;   // Время исчезания чёрного (1 -> 0)
    public float holdTime = 1.5f;   // Пауза между ин/аут
    public float fadeOutTime = 1f;  // Время появления чёрного (0 -> 1)

    void Start()
    {
        // Защита от двойного звука
        if (logoSound)
        {
            logoSound.playOnAwake = false;
            logoSound.loop = false;
        }

        StartCoroutine(Run());
        StartCoroutine(PlayLogoSoundAfterDelay()); // строго через 1 сек после старта сцены
    }

    IEnumerator Run()
    {
        if (fade) fade.alpha = 1f;

        // FadeIn: чёрный 1 -> 0
        yield return Fade(1f, 0f, fadeInTime);

        // Пауза перед FadeOut
        yield return new WaitForSeconds(holdTime);

        // FadeOut: 0 -> 1
        yield return Fade(0f, 1f, fadeOutTime);

        // Переход на следующую сцену по индексу в Build Profiles
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.Log("Последняя сцена в билде — переход не выполнен.");
    }

    IEnumerator PlayLogoSoundAfterDelay()
    {
        if (!logoSound) yield break;
        yield return new WaitForSeconds(soundDelay);
        if (!logoSound.isPlaying) logoSound.Play();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        if (!fade || duration <= 0f) { if (fade) fade.alpha = to; yield break; }

        float t = 0f;
        fade.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            fade.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fade.alpha = to;
    }
}
