using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

#if TMP_PRESENT || UNITY_TEXTMESHPRO
using TMPro;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Dialog : MonoBehaviour
{
    [Header("Text (assign ONE)")]
    public Text uiText;
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Text tmpText;
#endif

    [Header("Source text")]
    [TextArea(3, 20)] public string sourceText;
    [Tooltip("Если не пусто — используется напрямую, sourceText игнорируется.")]
    public List<string> pages = new List<string>();

    [Header("Typing")]
    [Tooltip("Символов в секунду. 0 — показать страницу сразу.")]
    public float charsPerSecond = 40f;
    [Tooltip("При отсутствии [PAGE] автосплит по символам.")]
    public int charactersPerPage = 160;

    [Tooltip("Иконка «далее», показывается когда страница допечатана.")]
    public GameObject continueHint;

    [Tooltip("Автозапуск печати при включении объекта.")]
    public bool startOnEnable = true;

    [Tooltip("Задержка перед началом печати страницы, сек (realtime).")]
    public float startDelay = 0f;

    [Tooltip("Задержка после страницы, сек (оставь 0 для мгновенного переключения после пробела).")]
    public float endDelay = 0f;

    enum Phase { Idle, Typing, Shown, AwaitLastConfirm }
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

        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceText)) return result;

        // ручные разрывы
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

        // автосплит
        var words = sourceText.Split(' ');
        var buf = new System.Text.StringBuilder();
        int maxChars = Mathf.Max(1, charactersPerPage);

        foreach (var w in words)
        {
            int sep = buf.Length == 0 ? 0 : 1;
            if (buf.Length + sep + w.Length > maxChars)
            {
                result.Add(buf.ToString());
                buf.Length = 0;
                buf.Append(w);
            }
            else
            {
                if (buf.Length > 0) buf.Append(' ');
                buf.Append(w);
            }
        }
        if (buf.Length > 0) result.Add(buf.ToString());
        return result;
    }

    void ShowNextPage()
    {
        currentPage++;

        if (currentPage >= pages.Count)
        {
            // Все страницы показаны — ждём ПРОБЕЛ для перехода в следующую сцену
            phase = Phase.AwaitLastConfirm;
            if (continueHint) continueHint.SetActive(true);
            SetText(""); // можно оставить последнюю страницу, но чаще логичнее очистить
            return;
        }

        fullPage = pages[currentPage] ?? "";
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
            if (SpacePressedThisFrame())
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
        if (!SpacePressedThisFrame()) return;

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
                    ShowNextPage(); // мгновенно, но по ПРОБЕЛУ
                }
                else
                {
                    StartCoroutine(NextDelayed());
                }
                break;

            case Phase.AwaitLastConfirm:
                if (continueHint) continueHint.SetActive(false);
                StartCoroutine(AdvanceToNextScene());
                break;
        }
    }

    IEnumerator NextDelayed()
    {
        yield return new WaitForSecondsRealtime(endDelay);
        phase = Phase.Idle;
        ShowNextPage();
    }

    IEnumerator AdvanceToNextScene()
    {
        if (endDelay > 0f)
            yield return new WaitForSecondsRealtime(endDelay);

        FindObjectOfType<LoreSceneController>()?.RequestAdvance();
    }

    void SetText(string s)
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText) tmpText.text = s;
#endif
        if (uiText) uiText.text = s;
    }

    // --------- только ПРОБЕЛ ---------
    bool SpacePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }
}
