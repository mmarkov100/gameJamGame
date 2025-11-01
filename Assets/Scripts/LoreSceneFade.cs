using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class LoreSceneFade : MonoBehaviour
{
    public float fadeDuration = 0.8f;
    private CanvasGroup cg;
    private bool isTransition;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
    }

    void Start()
    {
        StartCoroutine(Fade(1f, 0f, fadeDuration, () => cg.blocksRaycasts = false));
    }

    void Update()
    {
        if (isTransition) return;

        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        // --- New Input System ---
        // Клавиатура
        if (!pressed && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) pressed = true;

        // Мышь
        if (!pressed && Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame)) pressed = true;

        // Геймпад (перебор всех ButtonControl)
        if (!pressed && Gamepad.current != null)
        {
            var gp = Gamepad.current;
            foreach (var c in gp.allControls)
            {
                if (c is ButtonControl b && b.wasPressedThisFrame) { pressed = true; break; }
            }
        }

        // Тач
        if (!pressed && Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame) pressed = true;
#else
        // --- Старый Input ---
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0) pressed = true;
#endif

        if (pressed) NextScene();
    }

    public void NextScene()
    {
        if (isTransition) return;
        isTransition = true;
        cg.blocksRaycasts = true;
        StartCoroutine(FadeOutAndLoad());
    }

    IEnumerator FadeOutAndLoad()
    {
        yield return Fade(0f, 1f, fadeDuration);

        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.Log("Последняя сцена — переход невозможен.");
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
}
