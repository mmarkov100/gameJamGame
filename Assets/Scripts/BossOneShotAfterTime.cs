using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

public interface IHurtbox { void OnHit(int damage); }

[DisallowMultipleComponent]
public class BossOneShotAfterTime : MonoBehaviour, IHurtbox
{
    [Header("Фаза")]
    public float surviveSeconds = 30f;     // до этого времени босс бессмертен
    public bool startOnEnable = true;
    public UnityEvent onPhase2;            // визуал/звук “теперь убиваемый”

    [Header("Phase End Sound")]
    public AudioClip phaseEndBeep;   // звук конца таймера
    [Range(0f, 1.5f)] public float phaseEndBeepVolume = 1f;


    [Header("Смерть")]
    public UnityEvent onDeath;             // катсцена/дроп/выключение музыки
    public string dieTrigger = "Die";      // имя триггера в аниматоре (если есть)
    public float disableAfterDeath = 3f;   // через сколько отключить объект

    [Header("Refs (опционально)")]
    public EnemyAI enemyAI;                // если пусто — найдётся
    public Animator animator;              // если пусто — найдётся
    public NavMeshAgent agent;             // если пусто — найдётся
    public AudioSource audioSource;        // если пусто — найдётся
    public AudioClip phase2Sfx;            // звук перехода в фазу 2 (по желанию)
    public GameObject phase2Fx;            // VFX подсказка (по желанию)

    bool killable;     // после 30с = true
    bool dead;

    void Reset()
    {
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (!enemyAI) enemyAI = GetComponent<EnemyAI>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        if (startOnEnable) StartCoroutine(PhaseTimer());
    }

    IEnumerator PhaseTimer()
    {
        killable = false;
        yield return new WaitForSeconds(surviveSeconds);

        // Переход в фазу 2: теперь можно убить с 1 удара
        killable = true;
        onPhase2?.Invoke();
        if (phase2Sfx && audioSource) audioSource.PlayOneShot(phase2Sfx);
        if (phase2Fx) Instantiate(phase2Fx, transform.position + Vector3.up * 1.2f, Quaternion.identity);
        if (phaseEndBeep && audioSource)
        {
            audioSource.PlayOneShot(phaseEndBeep, phaseEndBeepVolume);
        }
    }

    // ===== Приём урона (любой вход) =====
    public void OnHit(int damage) { TryKill(damage); }
    public void TakeDamage(int amount) { TryKill(amount); }
    public void ApplyDamage(int amount) { TryKill(amount); }
    public void Damage(int amount) { TryKill(amount); }  // на всякий случай

    void TryKill(int amount)
    {
        if (dead) return;

        // До 30с — игнорим любой урон
        if (!killable) return;

        // После 30с — ваншот, независимо от урона
        Die();
    }

    void Die()
    {
        if (dead) return;
        dead = true;

        // Останавливаем ИИ/агента
        if (agent) agent.isStopped = true;

        // Вызов штатной смерти врага (если была логика в EnemyAI)
        if (enemyAI) enemyAI.OnDeath();

        // Анимация смерти (если есть триггер)
        if (animator && !string.IsNullOrEmpty(dieTrigger))
            animator.SetTrigger(dieTrigger);

        onDeath?.Invoke();

        // Отключим коллайдеры-хитбоксы, чтобы не ловить урон после смерти
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

        if (disableAfterDeath > 0f) Destroy(gameObject, disableAfterDeath);
        else enabled = false;
    }
}
