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

/// <summary>
/// Покадровая печать страниц лора. По клику — следующая страница.
/// После последней — сигнал LoreSceneController.RequestAdvance().
/// </summary>
public class Dialog : MonoBehaviour
{
    [Header("Text (assign ONE)")]
    public Text uiText;                 // для UI.Text
#if TMP_PRESENT || UNITY_TEXTMESHPRO
    public TMP_Text tmpText;            // для TextMeshPro
#endif

    [Header("Source")]
    [TextArea(3, 20)]
    [Tooltip("Вставляйте [PAGE] для ручного разрыва страниц. Если пусто — используйте Pages.")]
    public string sourceText;

    [Tooltip("Если задать — эти строки станут страницами как есть (sourceText игнорируется).")]
    public List<string> pages = new List<string>();

    [Header("Typing")]
    [Tooltip("Символов в секунду. 0 — показать страницу сразу.")]
    public float charsPerSecond = 40f;

    [Tooltip("Автосплит, если страниц нет и в sourceText нет [PAGE].")]
    public int charactersPerPage = 160;

    [Tooltip("Иконка/стрелка «далее». Включается, когда страница дописана.")]
    public GameObject continueHint;

    [Tooltip("Автозапуск при включении объекта.")]
    public bool startOnEnable = true;

    [Tooltip("Задержка перед началом печати страницы (сек, realtime).")]
    public float startDelay = 0f;

    [Tooltip("Задержка перед переключением на следующую страницу/сцену (сек, realtime).")]
    public float endDelay = 0f;

    enum Phase { Idle, Typing, Shown }
    Phase phase = Phase.Idle;

    int currentPage = -1;
    string fullPage = "";
    Coroutine typing;

    void OnEnable()
    {
        if (startOnEnable)
            StartCoroutine(BeginWhenActive());
    }

    IEnumerator BeginWhenActive()
    {
        // защита от старта на неактивном объекте
        yield return new WaitUntil(() => isActiveAndEnabled);
        Begin();
    }

    /// <summary>Запустить/перезапустить диалог.</summary>
    public void Begin()
    {
        if (continueHint) continueHint.SetActive(false);

        // приготовить список страниц
        var prepared = PreparePages();
        pages = prepared;

        currentPage = -1;
        phase = Phase.Idle;
        ShowNextPage();
    }

    List<string> PreparePages()
    {
        // приоритет: явные pages из инспектора
        if (pages != null && pages.Count > 0)
            return new List<string>(pages);

        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceText))
            return result;

        // разрывы по [PAGE]
        var manual = sourceText.Split(new string[] { "[PAGE]" }, System.StringSplitOptions.None);
        if (manual.Length > 1)
        {
            foreach (var p in manual)
            {
                var t = p.Trim();
                if (!string.IsNullOrEmpty(t)) result.Add(t);
            }
            return result;
        }

        // авто-разбиение по словам до charactersPerPage
        var words = sourceText.Split(' ');
        var buf = new System.Text.StringBuilder();
        foreach (var w in words)
        {
            int sep = buf.Length == 0 ? 0 : 1;
            if (buf.Length + sep + w.Length > Mathf.Max(1, charactersPerPage))
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
            // всё — просим LoreSceneController сделать fade и переключить сцену
            var controller = FindObjectOfType<LoreSceneController>();
            if (controller != null)
                controller.RequestAdvance(); // дальше сценой управляет LoreSceneController
            else
                Debug.LogWarning("[Dialog] LoreSceneController не найден на сцене.");
            return;
        }

        fullPage = pages[currentPage] ?? "";
        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(TypePage());
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
            // клик во время печати — раскрыть сразу
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
        phase = Phase.Shown;
        typing = null;
        if (continueHint) continueHint.SetActive(true);
    }

    void Update()
    {
        if (!AnyPressThisFrame()) return;

        switch (phase)
        {
            case Phase.Typing:
                // завершить страницу мгновенно
                SetText(fullPage);
                FinishTyping();
                break;

            case Phase.Shown:
                // перейти к следующей
                StartCoroutine(AdvanceAfterDelay());
                break;
        }
    }

    IEnumerator AdvanceAfterDelay()
    {
        if (continueHint) continueHint.SetActive(false);
        if (endDelay > 0f)
            yield return new WaitForSecondsRealtime(endDelay);
        phase = Phase.Idle;
        ShowNextPage();
    }

    // ---------- helpers ----------

    void SetText(string s)
    {
#if TMP_PRESENT || UNITY_TEXTMESHPRO
        if (tmpText != null) tmpText.text = s;
#endif
        if (uiText != null) uiText.text = s;
    }

    bool AnyPressThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
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
