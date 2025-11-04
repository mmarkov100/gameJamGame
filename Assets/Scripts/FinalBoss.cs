using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class FinalBoss : MonoBehaviour
{
    public enum Phase { Survive, Breakable }

    [Header("Refs")]
    public Transform player;
    public NavMeshAgent agent;
    public Animator animator;
    public AudioSource audioSource;

    [Header("Phase / Timer")]
    public Phase phase = Phase.Survive;
    public float surviveSeconds = 30f;
    public UnityEvent onPhase2;
    public UnityEvent onWin;
    public UnityEvent onLose;

    [Header("Combat")]
    public int damageOnFailedParry = 1;
    public float stunOnFailedParry = 0.5f;
    public float attackRange = 2.5f;
    public float targetChaseRange = 20f;
    public LayerMask playerMask;

    [Header("Combo")]
    public int hitsPerCombo = 3;
    public float timeBetweenHits = 0.35f;      // не используетс€, если событи€ анимаций подключены
    public float reTargetDelayAfterCombo = 0.4f;

    [Header("Audio")]
    public AudioClip attackSfx_Phase1;
    public AudioClip attackSfx_Phase2;

    [Header("Damage Gate")]
    public bool invincibleInPhase1 = true;

    // ====== Runtime ======
    int _comboHitIndex = 0;
    bool _inCombo = false;
    bool _facingLocked = false;
    Vector3 _lockedForward;
    bool _alive = true;

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        // јгент крутит трансформ Ч нам норм, но во врем€ комбо мы его переопределим вручную
        agent.updateRotation = true;
    }

    void OnEnable()
    {
        StartCoroutine(PhaseTimer());
    }

    IEnumerator PhaseTimer()
    {
        phase = Phase.Survive;
        yield return new WaitForSeconds(surviveSeconds);

        phase = Phase.Breakable;
        onPhase2?.Invoke();

        if (attackSfx_Phase2)
        {
            audioSource.clip = attackSfx_Phase2;
            audioSource.Play();
        }
    }

    void Update()
    {
        if (!_alive || !player) return;

        // === ƒ¬»∆≈Ќ»≈ / ѕќ√ќЌя, если не в комбо ===
        if (!_inCombo)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= targetChaseRange)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);

                // Signed Move: проекци€ желаемой скорости агента на local forward босса
                Vector3 worldVel = agent.desiredVelocity; // куда агент хочет идти
                float forwardSpeed = 0f;
                if (worldVel.sqrMagnitude > 0.0001f)
                {
                    // ѕроецируем на forward, получаем знак (вперЄд/назад)
                    forwardSpeed = Vector3.Dot(transform.forward, worldVel.normalized) * worldVel.magnitude;
                }

                // Ќормализуем к -1..1 (или оставь как есть)
                float signedMove = Mathf.Clamp(forwardSpeed / (agent.speed > 0f ? agent.speed : 1f), -1f, 1f);

                if (animator)
                {
                    animator.SetFloat("Move", signedMove);           // >0 вперЄд, <0 назад, ~0 idle
                    animator.SetBool("IsMoving", Mathf.Abs(signedMove) > 0.05f);
                }

                // «апуск атаки при подходе
                if (dist <= attackRange * 0.9f)
                {
                    BeginCombo();
                }
            }
            else
            {
                agent.isStopped = true;
                if (animator)
                {
                    animator.SetFloat("Move", 0f);
                    animator.SetBool("IsMoving", false);
                }
            }
        }
        else
        {
            // ¬ комбо Ч скорость в аниматор = 0, чтобы не триггерить ходьбу
            if (animator)
            {
                animator.SetFloat("Move", 0f);
                animator.SetBool("IsMoving", false);
            }
        }

        // === ¬«√Ћяƒ ===
        if (_facingLocked)
        {
            transform.forward = _lockedForward;
        }
        else
        {
            // ¬не комбо Ч плавный разворот к игроку
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }
        }
    }

    // =========================
    // COMBO FLOW
    // =========================
    public void BeginCombo()
    {
        if (_inCombo || !_alive) return;

        agent.isStopped = true;
        _inCombo = true;
        _comboHitIndex = 0;

        // ‘иксируем направление в момент старта
        _lockedForward = (player ? (new Vector3(player.position.x - transform.position.x, 0, player.position.z - transform.position.z)).normalized : transform.forward);
        if (_lockedForward.sqrMagnitude < 0.0001f) _lockedForward = transform.forward;
        _facingLocked = true;

        if (animator)
        {
            animator.SetBool("InCombo", true);
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");      // перейти в стейт AttackCombo
        }

        // —обыти€ анимаций включены Ч таймерное автокомбо не нужно
        if (!HasAnimationEvents())
            StartCoroutine(AutoComboRhythm());
    }

    bool HasAnimationEvents() => true; // мы используем событи€ анимации

    IEnumerator AutoComboRhythm()
    {
        for (int i = 0; i < hitsPerCombo; i++)
        {
            yield return new WaitForSeconds(timeBetweenHits);
            OnAttackHit();
        }
        OnComboAnimationEnd();
    }

    // === —обытие анимации: момент удара ===
    public void OnAttackHit()
    {
        if (!_inCombo || !_alive) return;

        if (phase == Phase.Survive && attackSfx_Phase1)
            audioSource.PlayOneShot(attackSfx_Phase1);
        else if (phase == Phase.Breakable && attackSfx_Phase2)
            audioSource.PlayOneShot(attackSfx_Phase2);

        _comboHitIndex++;

        if (player)
        {
            Vector3 hitCenter = transform.position + transform.forward * (attackRange * 0.6f);
            float hitRadius = Mathf.Clamp(attackRange * 0.6f, 0.5f, 3f);
            var hits = Physics.OverlapSphere(hitCenter, hitRadius, playerMask, QueryTriggerInteraction.Collide);

            if (hits != null && hits.Length > 0)
            {
                ApplyDamageToPlayer(damage: 1, stun: 0f);
            }
        }
    }

    // === —обытие анимации: конец серии ===
    public void OnComboAnimationEnd()
    {
        EndCombo();
    }

    public void EndCombo()
    {
        if (!_inCombo) return;
        _inCombo = false;
        _facingLocked = false;

        if (animator)
        {
            animator.SetBool("InCombo", false);
        }

        StartCoroutine(RetargetAfterDelay());
    }

    IEnumerator RetargetAfterDelay()
    {
        yield return new WaitForSeconds(reTargetDelayAfterCombo);
        if (!_alive) yield break;
        agent.isStopped = false;
    }

    // =========================
    // PARRY API
    // =========================
    public void OnPlayerParry()
    {
        if (!_alive) return;

        if (phase == Phase.Survive)
        {
            ApplyDamageToPlayer(damageOnFailedParry, stunOnFailedParry);
            if (!_inCombo) BeginCombo();
        }
        else
        {
            Victory();
        }
    }

    void ApplyDamageToPlayer(int damage, float stun)
    {
        if (!player) return;

        var hp = player.GetComponent<IPlayerHealth>();
        var st = player.GetComponent<IStunnable>();
        var dmg = player.GetComponent<IHurtbox>();

        if (hp != null) hp.TakeDamage(damage);
        if (st != null && stun > 0f) st.Stun(stun);
        if (dmg != null) dmg.OnHit(damage);
    }

    public void TakeDamage(int amount)
    {
        if (!_alive) return;
        if (phase == Phase.Survive && invincibleInPhase1) return;
        // HP по желанию
    }

    void Victory()
    {
        if (!_alive) return;
        _alive = false;

        agent.isStopped = true;
        if (animator) animator.SetTrigger("Defeated");

        onWin?.Invoke();
        this.enabled = false;
    }

    public void ForceLose()
    {
        onLose?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetChaseRange);

        Gizmos.color = Color.red;
        Vector3 hitCenter = transform.position + transform.forward * (attackRange * 0.6f);
        float hitRadius = Mathf.Clamp(attackRange * 0.6f, 0.5f, 3f);
        Gizmos.DrawWireSphere(hitCenter, hitRadius);
    }
}

public interface IPlayerHealth { void TakeDamage(int amount); }
public interface IStunnable { void Stun(float seconds); }
public interface IHurtbox { void OnHit(int damage); }
