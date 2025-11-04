using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class Dialog : MonoBehaviour
{
    [Header("Text (assign ONE)")]
    public Text uiText;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Text tmpText;
#endif

    [Header("Typing Settings")]
    [TextArea] public string message;
    public float charsPerSecond = 40f;
    public bool startOnEnable = true;

    [Header("Delays")]
    public float startDelay = 1f;   // задержка перед началом печати (для Fade In)
    public float endDelay = 1f;     // задержка перед onAdvance (для Fade Out)

    [Header("Flow")]
    public UnityEvent onAdvance;

    enum Phase { Idle, Typing, Shown }
    Phase phase = Phase.Idle;

    string full;
    Coroutine routine;
    bool advancing;                  // идёт задержка перед onAdvance

    void OnEnable()
    {
        if (startOnEnable && !string.IsNullOrEmpty(message))
            StartTyping(message);
    }

    public void StartTyping(string text)
    {
        full = text ?? "";
        if (routine != null) StopCoroutine(routine);
        advancing = false;
        routine = StartCoroutine(TypeRoutine());
    }

    IEnumerator TypeRoutine()
    {
        phase = Phase.Typing;
        SetText("");

        // задержка перед началом печати
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float shown = 0f;
        int lastLen = 0;

        while (lastLen < full.Length)
        {
            // Нажатие во время печати -> мгновенно показать весь текст, НО НЕ переходить дальше
            if (AnyPressThisFrame())
            {
                SetText(full);
                phase = Phase.Shown;
                routine = null;
                yield break;
            }

            shown += Time.deltaTime * Mathf.Max(1f, charsPerSecond);
            int newLen = Mathf.Clamp(Mathf.FloorToInt(shown), 0, full.Length);
            if (newLen != lastLen)
            {
                lastLen = newLen;
                SetText(full.Substring(0, lastLen));
            }

            yield return null;
        }

        phase = Phase.Shown;
        routine = null;
    }

    void Update()
    {
        if (!AnyPressThisFrame()) return;

        switch (phase)
        {
            case Phase.Typing:
                // первое нажатие — досветить текст до конца
                SetText(full);
                phase = Phase.Shown;
                break;

            case Phase.Shown:
                // второе нажатие — ждём endDelay, потом onAdvance
                if (!advancing)
                    StartCoroutine(AdvanceAfterDelay());
                break;
        }
    }

    IEnumerator AdvanceAfterDelay()
    {
        advancing = true;
        if (endDelay > 0f)
            yield return new WaitForSeconds(endDelay);
        onAdvance?.Invoke();
        phase = Phase.Idle;
        advancing = false;
    }

    bool AnyPressThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
        if (Gamepad.current != null)
            foreach (var c in Gamepad.current.allControls)
                if (c is ButtonControl b && b.wasPressedThisFrame) return true;
        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }

    void SetText(string s)
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText != null) tmpText.text = s;
#endif
        if (uiText != null) uiText.text = s;
    }
}
