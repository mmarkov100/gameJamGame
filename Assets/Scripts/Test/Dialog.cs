using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Source text")]
    [TextArea(3, 20)]
    public string sourceText;

    [Tooltip("Если не пусто — используется напрямую, sourceText игнорируется.")]
    public List<string> pages = new List<string>();

    [Header("Typing")]
    public float charsPerSecond = 40f;
    public int charactersPerPage = 160;
    public GameObject continueHint;
    public bool startOnEnable = true;

    [Tooltip("Задержка перед началом печати страницы")]
    public float startDelay = 0f;

    [Tooltip("Задержка после страницы (оставь 0 для моментального перехода)")]
    public float endDelay = 0f;

    enum Phase { Idle, Typing, Shown }
    Phase phase = Phase.Idle;

    int currentPage = -1;
    string fullPage = "";
    Coroutine typingRoutine;

    void OnEnable()
    {
        if (startOnEnable)
            StartCoroutine(BeginWhenActive());
    }

    IEnumerator BeginWhenActive()
    {
        yield return new WaitUntil(() => isActiveAndEnabled);
        Begin();
    }

    public void Begin()
    {
        if (continueHint) continueHint.SetActive(false);

        pages = PreparePages();
        currentPage = -1;
        phase = Phase.Idle;
        ShowNextPage();
    }

    List<string> PreparePages()
    {
        if (pages != null && pages.Count > 0)
            return new List<string>(pages);

        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceText))
            return result;

        // [PAGE] tags
        var manual = sourceText.Split(new string[] { "[PAGE]" }, System.StringSplitOptions.None);
        if (manual.Length > 1)
        {
            foreach (var m in manual)
            {
                var t = m.Trim();
                if (!string.IsNullOrEmpty(t)) result.Add(t);
            }
            return result;
        }

        // Auto wrap
        var words = sourceText.Split(' ');
        var buffer = new System.Text.StringBuilder();
        foreach (var w in words)
        {
            int sep = buffer.Length == 0 ? 0 : 1;
            if (buffer.Length + sep + w.Length > Mathf.Max(1, charactersPerPage))
            {
                result.Add(buffer.ToString());
                buffer.Length = 0;
                buffer.Append(w);
            }
            else
            {
                if (buffer.Length > 0) buffer.Append(' ');
                buffer.Append(w);
            }
        }
        if (buffer.Length > 0) result.Add(buffer.ToString());

        return result;
    }

    void ShowNextPage()
    {
        currentPage++;

        // End of story — tell scene to fade out
        if (currentPage >= pages.Count)
        {
            FindObjectOfType<LoreSceneController>()?.RequestAdvance();
            return;
        }

        fullPage = pages[currentPage];
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypePage());
    }

    IEnumerator TypePage()
    {
        phase = Phase.Typing;
        SetText("");
        if (continueHint) continueHint.SetActive(false);

        if (startDelay > 0f)
            yield return new WaitForSecondsRealtime(startDelay);

        if (charsPerSecond <= 0f)
        {
            SetText(fullPage);
            FinishTyping();
            yield break;
        }

        float perChar = 1f / charsPerSecond;
        float t = 0f;
        int shown = 0;

        while (shown < fullPage.Length)
        {
            // fast skip
            if (AnyPressThisFrame())
            {
                SetText(fullPage);
                FinishTyping();
                yield break;
            }

            t += Time.unscaledDeltaTime;
            while (t >= perChar && shown < fullPage.Length)
            {
                t -= perChar;
                shown++;
                SetText(fullPage.Substring(0, shown));
            }

            yield return null;
        }

        FinishTyping();
    }

    void FinishTyping()
    {
        typingRoutine = null;
        phase = Phase.Shown;
        if (continueHint) continueHint.SetActive(true);
    }

    void Update()
    {
        if (!AnyPressThisFrame()) return;

        switch (phase)
        {
            case Phase.Typing:
                SetText(fullPage);
                FinishTyping();
                break;

            case Phase.Shown:
                if (continueHint) continueHint.SetActive(false);

                if (endDelay <= 0f)
                {
                    phase = Phase.Idle;
                    ShowNextPage(); // instant switch ⚡
                }
                else
                {
                    StartCoroutine(NextDelayed());
                }
                break;
        }
    }

    IEnumerator NextDelayed()
    {
        yield return new WaitForSecondsRealtime(endDelay);
        phase = Phase.Idle;
        ShowNextPage();
    }

    void SetText(string s)
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText) tmpText.text = s;
#endif
        if (uiText) uiText.text = s;
    }

    bool AnyPressThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;

        if (Gamepad.current != null)
            foreach (var c in Gamepad.current.allControls)
                if (c is ButtonControl b && b.wasPressedThisFrame) return true;

        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0;
#endif
    }
}
