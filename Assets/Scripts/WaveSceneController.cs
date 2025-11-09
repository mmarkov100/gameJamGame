using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class WaveSceneController : MonoBehaviour
{
    [Header("Fade")]
    public CanvasGroup fade;           // чёрная плашка поверх
    public float fadeIn = 0.8f;
    public float fadeOut = 0.8f;

    [Header("Старт боя")]
    public float startFreeze = 1.0f;   // игра «замирает» на N сек

    [Header("Проверка завершения волны (обычный режим)")]
    public float checkEvery = 0.5f;    // как часто проверять «врагов больше нет»

    [Header("Режим этой сцены")]
    [Tooltip("Если включено — вместо проверки волны ждём УСПЕШНОЕ ПАРРИ босса ПОСЛЕ 30 сек и уходим через 2 секунды.")]
    public bool advanceOnBossParryAfter30s = false;

    [Tooltip("Игрок с компонентом PlayerParry (для подписки на OnParrySuccess).")]
    public PlayerParry playerParry;

    [Tooltip("Босс этой сцены — нужен, чтобы проверить, что его фаза бессмертия уже прошла.")]
    public BossAI boss;

    [Tooltip("Задержка перед сменой сцены после корректного парри (сек).")]
    public float advanceDelayOnParry = 2.0f;

    // внутреннее состояние
    bool advanceTriggered;

    void Awake()
    {
        if (!fade) fade = GetComponent<CanvasGroup>();
        if (fade) { fade.alpha = 1f; fade.blocksRaycasts = true; }
    }

    void OnEnable()
    {
        // подписываемся только если включён специальный режим
        if (advanceOnBossParryAfter30s && playerParry != null)
        {
            playerParry.OnParrySuccess.AddListener(OnPlayerParrySuccess);
        }
    }

    void OnDisable()
    {
        if (advanceOnBossParryAfter30s && playerParry != null)
        {
            playerParry.OnParrySuccess.RemoveListener(OnPlayerParrySuccess);
        }
    }

    IEnumerator Start()
    {
        // Музыка: выбрать/оставить нужный трек для этой сцены
        MusicDirector.Instance?.AdoptForActiveScene();

        // Fade-in
        yield return Fade(1f, 0f, fadeIn);
        if (fade) fade.blocksRaycasts = false;

        // Глобальная пауза ИИ на старте
        EnemyAI.GlobalPaused = true;
        yield return new WaitForSeconds(startFreeze);
        EnemyAI.GlobalPaused = false;

        if (advanceOnBossParryAfter30s)
        {
            // В спец-режиме ждём триггера (OnPlayerParrySuccess проверит таймер босса)
            while (!advanceTriggered)
                yield return null;

            // ждём заданную задержку
            yield return new WaitForSeconds(advanceDelayOnParry);

            // уходим дальше с fade-out
            if (fade) fade.blocksRaycasts = true;
            yield return Fade(0f, 1f, fadeOut);

            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next < SceneManager.sceneCountInBuildSettings)
                SceneManager.LoadScene(next);

            yield break;
        }

        // Обычный режим для остальных сцен — ждём пока врагов не останется
        while (!WaveCleared())
            yield return new WaitForSeconds(checkEvery);

        // Переход дальше: Fade-out и следующая сцена
        if (fade) fade.blocksRaycasts = true;
        yield return Fade(0f, 1f, fadeOut);

        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next < SceneManager.sceneCountInBuildSettings)
                SceneManager.LoadScene(next);
        }
    }

    // Хэндлер клика парри у игрока — фильтруем только «после 30 секунд»
    void OnPlayerParrySuccess()
    {
        if (advanceTriggered) return;       // уже триггернули

        // Должен быть задан босс, и он больше НЕ в инвулнерабл-фазе
        if (boss == null) return;
        if (boss.IsCurrentlyInvulnerable()) return; // ещё не прошло 30 сек — игнорируем

        // В этот момент парри по удару босса прошло в окно: даём зелёный свет на переход
        advanceTriggered = true;
    }

    bool WaveCleared()
    {
        var enemies = FindObjectsOfType<EnemyAI>(true); // включая отключённых
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue; // объект скрыт -> не считаем
            if (!e.enabled) continue;                      // компонент отключён -> не считаем
            if (!e.IsDead) return false;                   // ещё жив
        }
        return true; // активных, не-мёртвых не осталось
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
