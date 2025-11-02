using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class LoreSceneFade : MonoBehaviour
{
    [Header("Timings")]
    public float fadeDuration = 0.8f;   // длительность FadeIn/FadeOut
    public float beforeFadeOutDelay = 1f; // задержка перед FadeOut после нажати€

    CanvasGroup cg;
    bool isTransition;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        cg.alpha = 1f;                 // начинаем с чЄрного
        cg.blocksRaycasts = true;      // блокируем клики на старте
    }

    void Start()
    {
        // Fade In
        StartCoroutine(Fade(1f, 0f, fadeDuration, () =>
        {
            cg.blocksRaycasts = false; // разблокируем UI после по€влени€
        }));
    }

    void Update()
    {
        if (isTransition) return;
        if (AnyPressThisFrame()) NextScene(); // реакци€ на любую кнопку/клик/тач/геймпад
    }

    public void NextScene()
    {
        if (isTransition) return;
        isTransition = true;
        StartCoroutine(GoNextRoutine());
    }

    IEnumerator GoNextRoutine()
    {
        // 1) подождать секунду (настраиваетс€ через beforeFadeOutDelay)
        if (beforeFadeOutDelay > 0f)
            yield return new WaitForSeconds(beforeFadeOutDelay);

        // 2) FadeOut: 0 -> 1
        cg.blocksRaycasts = true; // блокируем ввод на врем€ затемнени€
        yield return Fade(0f, 1f, fadeDuration);

        // 3) загрузка следующей сцены по индексу
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.Log("ѕоследн€€ сцена Ч нет следующей.");
    }

    IEnumerator Fade(float from, float to, float duration, System.Action onDone = null)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
        onDone?.Invoke();
    }

    bool AnyPressThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

        if (Mouse.current != null &&
           (Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame ||
            Mouse.current.middleButton.wasPressedThisFrame)) return true;

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;

        if (Gamepad.current != null)
        {
            foreach (var c in Gamepad.current.allControls)
                if (c is ButtonControl b && b.wasPressedThisFrame) return true;
        }
        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }
}
