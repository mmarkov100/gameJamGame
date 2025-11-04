//using System.Collections;
//using UnityEngine;
//using UnityEngine.AI;
//using UnityEngine.Events;

//[RequireComponent(typeof(NavMeshAgent))]
//[RequireComponent(typeof(AudioSource))]
//public class FinalBoss : MonoBehaviour
//{
//    public enum Phase { Survive, Breakable }

//    [Header("Refs")]
//    public Transform player;
//    public NavMeshAgent agent;
//    public Animator animator;
//    public AudioSource audioSource;

//    [Header("Phase / Timer")]
//    public Phase phase = Phase.Survive;
//    public float surviveSeconds = 30f;
//    public UnityEvent onPhase2;
//    public UnityEvent onWin;
//    public UnityEvent onLose;

//    [Header("Combat (портировано из EnemyAI)")]
//    public float attackRange = 2.5f;        // радиус ближнего удара
//    public float attackCooldown = 3f;       // перезарядка между атаками
//    public float attackCastTime = 0.5f;     // «замах» до окна урона
//    public int damage = 1;                  // сколько урона босс наносит
//    [Range(0f, 1f)] public float frontDot = 0.3f; // допуск по фронту (dot > frontDot)
//    public float damageWindow = 0.2f;       // длительность окна урона

//    [Header("Hitbox")]
//    public BossHitbox hitbox;          // назначь объект с BossHitbox
//    public float hitboxForward = 1.2f; // насколько вперёд от босса ставить хитбокс
//    public float hitboxYOffset = 0.9f; // высота (если нужно)

//    [Header("Chase")]
//    public float targetChaseRange = 20f;

//    [Header("Parry / Phase")]
//    public int damageOnFailedParry = 1;
//    public float stunOnFailedParry = 0.5f;
//    public bool invincibleInPhase1 = true;

//    [Header("Animation")]
//    public float reTargetDelayAfterCombo = 0.4f;
//    public bool useAnimationEvents = true;
//    public float comboTimeoutSeconds = 3.0f;

//    [Header("Audio")]
//    public AudioClip attackSfx_Phase1;
//    public AudioClip attackSfx_Phase2;
//    public AudioClip hitFxSfx;
//    public AudioClip parryFxSfx;

//    [Header("FX")]
//    public Transform fxSpawn;
//    public GameObject hitFxPrefab;
//    public GameObject parryReflectFxPrefab;
//    public float fxAutoDestroy = 2f;

//    [Header("Events")]
//    public UnityEvent onBossHit;
//    public UnityEvent onBossParried;

//    [Header("Debug Attack")]
//    public bool debugAttackLogs = true;

//    // ===== Runtime =====
//    bool _alive = true;
//    bool _inCombo = false;
//    bool _facingLocked = false;
//    Vector3 _lockedForward;
//    float _nextAttackTime;
//    Coroutine _comboFailsafe;

//    // Окно урона (как в EnemyAI)
//    bool damageWindowOpen;
//    float damageWindowUntil;

//    void Reset()
//    {
//        agent = GetComponent<NavMeshAgent>();
//        audioSource = GetComponent<AudioSource>();
//    }

//    void Awake()
//    {
//        if (!agent) agent = GetComponent<NavMeshAgent>();
//        if (!audioSource) audioSource = GetComponent<AudioSource>();
//        if (!player)
//        {
//            var p = GameObject.FindGameObjectWithTag("Player");
//            if (p) player = p.transform;
//        }
//        agent.updateRotation = true;
//        EnsureAgentTuning();

//        if (hitbox)
//        {
//            hitbox.owner = this;
//            if (!hitbox.ownerRoot) hitbox.ownerRoot = transform;
//        }
//    }

//    void OnEnable()
//    {
//        StartCoroutine(PhaseTimer());
//    }


//    void LogA(string msg)
//    {
//        if (debugAttackLogs) Debug.Log($"[BOSS] {msg}", this);
//    }

//    bool IsInAttackRange()
//    {
//        if (!player) return false;

//        // Горизонтальная дистанция
//        Vector3 a = transform.position; a.y = 0;
//        Vector3 b = player.position; b.y = 0;
//        float dist = Vector3.Distance(a, b);

//        // Агент уже почти остановился у цели?
//        bool closeByDistance = dist <= Mathf.Max(0.3f, attackRange * 0.95f);
//        bool agentReady = !agent.pathPending && agent.remainingDistance <= Mathf.Max(0.3f, attackRange * 0.95f);

//        return closeByDistance || agentReady;
//    }

//    void EnsureAgentTuning()
//    {
//        // Агент не должен останавливаться раньше, чем надо ударить
//        float desiredStop = Mathf.Clamp(attackRange * 0.55f, 0.1f, attackRange - 0.05f);
//        if (!Mathf.Approximately(agent.stoppingDistance, desiredStop))
//            agent.stoppingDistance = desiredStop;

//        // Агент должен реально крутить трансформ сам, вне комбо
//        agent.updateRotation = true;
//    }


//    IEnumerator PhaseTimer()
//    {
//        phase = Phase.Survive;
//        yield return new WaitForSeconds(surviveSeconds);

//        phase = Phase.Breakable;
//        onPhase2?.Invoke();

//        if (attackSfx_Phase2)
//        {
//            audioSource.clip = attackSfx_Phase2;
//            audioSource.Play(); // сигнал «теперь парри работает»
//        }
//    }

//    void Update()
//    {
//        if (!_alive || !player) return;

//        // === ЛОКОМОУШН И ПРЕСЛЕДОВАНИЕ (всегда вне комбо) ===
//        if (!_inCombo)
//        {
//            float dist = Vector3.Distance(transform.position, player.position);

//            // двигаемся к цели, если она в зоне агро
//            if (dist <= targetChaseRange)
//            {
//                agent.isStopped = false;
//                agent.SetDestination(player.position);

//                // Параметры движения
//                Vector3 vel = agent.velocity.sqrMagnitude > 0.001f ? agent.velocity : agent.desiredVelocity;
//                float signedMove = 0f;
//                if (vel.sqrMagnitude > 0.0001f)
//                {
//                    float speed = vel.magnitude;
//                    signedMove = Vector3.Dot(transform.forward, new Vector3(vel.x, 0, vel.z).normalized) * (speed / Mathf.Max(0.01f, agent.speed));
//                    signedMove = Mathf.Clamp(signedMove, -1f, 1f);
//                }
//                if (animator)
//                {
//                    animator.SetFloat("Move", signedMove);
//                }

//                // === СТАРТ АТАКИ ===
//                bool cooldownReady = Time.time >= _nextAttackTime;
//                if (cooldownReady && IsInAttackRange())
//                {
//                    LogA("BeginCombo() — в радиусе атаки и кулдаун готов");
//                    BeginCombo(); // внутри запустится каст/окно урона
//                }
//            }
//            else
//            {
//                // вне агро-радиуса — стоим
//                agent.isStopped = true;
//                if (animator)
//                {
//                    animator.SetFloat("Move", 0f);
//                }
//            }
//        }

//        // === ПОВОРОТ ===
//        if (_facingLocked)
//        {
//            transform.forward = _lockedForward;
//        }
//        else
//        {
//            Vector3 dir = player.position - transform.position; dir.y = 0f;
//            if (dir.sqrMagnitude > 0.001f)
//            {
//                var targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
//                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
//            }
//        }
//    }


//    // =========================
//    // АТАКА: КАСТ → ОКНО УРОНА → ВЫХОД (порт из EnemyAI)
//    // =========================
//    public void BeginCombo()
//    {
//        if (_inCombo || !_alive) return;
//        if (Time.time < _nextAttackTime) return;

//        _inCombo = true;
//        agent.isStopped = true;
//        agent.ResetPath();

//        // фикс направления
//        _lockedForward = player ? (new Vector3(player.position.x - transform.position.x, 0, player.position.z - transform.position.z)).normalized : transform.forward;
//        if (_lockedForward.sqrMagnitude < 0.0001f) _lockedForward = transform.forward;
//        _facingLocked = true;

//        if (animator)
//        {
//            animator.SetBool("InCombo", true);
//            animator.ResetTrigger("Attack");
//            animator.SetTrigger("Attack");
//        }

//        if (_comboFailsafe != null) StopCoroutine(_comboFailsafe);
//        _comboFailsafe = StartCoroutine(ComboFailsafeTimer());

//        // запускаем «замах»
//        StartCoroutine(AttackCastRoutine());
//    }

//    IEnumerator AttackCastRoutine()
//    {
//        yield return new WaitForSeconds(attackCastTime);

//        // Перед открытием окна — позиционируем хитбокс
//        PositionHitbox();

//        // ОТКРЫВАЕМ ОКНО УРОНА (включаем реальный триггер)
//        if (hitbox) hitbox.OpenWindow(damageWindow);

//        // держим окно (если нет события конца клипа — закроется сам)
//        yield return new WaitForSeconds(damageWindow);

//        // На всякий случай — закрыть (если уже закрыт, просто игнор)
//        if (hitbox) hitbox.CloseWindow();

//        // завершаем атаку (или дождись события конца клипа)
//        EndCombo();
//    }

//    void PositionHitbox()
//    {
//        if (!hitbox) return;
//        Vector3 basePos = transform.position + Vector3.up * hitboxYOffset + transform.forward * hitboxForward;
//        hitbox.transform.position = basePos;
//        hitbox.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
//    }

//    // ==== ВЫЗЫВАЕТСЯ ИЗ BossHitbox ПРИ УСПЕШНОМ ПОПАДАНИИ ====
//    public void OnHitConfirmedFromHitbox(object hpInterfaceOrNull)
//    {
//        // нанести урон
//        ApplyDamageToPlayer(damage, 0f);

//        // FX
//        Vector3 fxPos = fxSpawn ? fxSpawn.position : (player ? player.position + Vector3.up * 0.3f : transform.position + transform.forward * hitboxForward);
//        Quaternion fxRot = Quaternion.LookRotation(transform.forward, Vector3.up);
//        SpawnFx(hitFxPrefab, fxPos, fxRot, hitFxSfx);
//        onBossHit?.Invoke();
//    }

//    // ==== ВЫЗЫВАЕТСЯ ИЗ BossHitbox ПРИ ПАРРИ ====
//    public void OnParriedFromHitbox(float parryStunSeconds)
//    {
//        Vector3 reflectPos = fxSpawn ? fxSpawn.position : (transform.position + transform.forward * hitboxForward);
//        Quaternion reflectRot = Quaternion.LookRotation(-transform.forward, Vector3.up);
//        SpawnFx(parryReflectFxPrefab, reflectPos, reflectRot, parryFxSfx);
//        onBossParried?.Invoke();

//        if (phase == Phase.Survive)
//        {
//            ApplyDamageToPlayer(damageOnFailedParry, stunOnFailedParry);
//            // остаёмся в бою; можно перезапустить атаку после кулдауна
//        }
//        else
//        {
//            Victory();
//        }
//    }

//    IEnumerator ComboFailsafeTimer()
//    {
//        yield return new WaitForSeconds(comboTimeoutSeconds);
//        if (_inCombo)
//        {
//            LogA("Failsafe: EndCombo() по таймеру");
//            EndCombo();
//        }
//    }

//    // === Событие анимации: момент удара (ИМЯ как у Enemy) ===
//    public void EnemyMeleeHit()
//    {
//        if (!_inCombo || !_alive || !player) return;

//        // событие действует только в окне урона
//        if (!damageWindowOpen) return;

//        // одно попадание — закрываем окно
//        damageWindowOpen = false;

//        // Парри: используем существующую систему игрока
//        if (player.TryGetComponent<PlayerParry>(out var parry))
//        {
//            if (parry.TryParry(transform, out float stunSeconds))
//            {
//                // ЛОГИКА БОССА: Фаза 1 — наказать игрока; Фаза 2 — победа
//                Vector3 reflectPos = fxSpawn ? fxSpawn.position : transform.position + transform.forward * (attackRange * 0.6f);
//                Quaternion reflectRot = Quaternion.LookRotation(-transform.forward, Vector3.up);
//                SpawnFx(parryReflectFxPrefab, reflectPos, reflectRot, parryFxSfx);
//                onBossParried?.Invoke();

//                if (phase == Phase.Survive)
//                {
//                    // наказание за «ранний парри»
//                    ApplyDamageToPlayer(damageOnFailedParry, stunOnFailedParry);
//                    // можно продолжить давление: оставим комбо активным, либо перезапустим позже
//                }
//                else
//                {
//                    Victory();
//                }
//                return; // при парри обычный урон не наносим
//            }
//        }

//        // Проверки попадания как в EnemyAI: дистанция + фронт
//        float dist = Horz(transform.position, player.position);
//        Vector3 dir = (player.position - transform.position); dir.y = 0; dir.Normalize();
//        bool inRange = dist <= attackRange;
//        bool inFront = Vector3.Dot(transform.forward, dir) > frontDot;

//        if (inRange && inFront)
//        {
//            if (player.TryGetComponent<IPlayerHealth>(out var hp1)) hp1.TakeDamage(damage);
//            if (player.TryGetComponent<PlayerHealth>(out var hp2)) hp2.TakeDamage(damage); // на случай другой реализации

//            Vector3 fxPos = fxSpawn ? fxSpawn.position : (player.position + Vector3.up * 0.3f);
//            Quaternion fxRot = Quaternion.LookRotation(transform.forward, Vector3.up);
//            SpawnFx(hitFxPrefab, fxPos, fxRot, hitFxSfx);
//            onBossHit?.Invoke();
//        }
//        else
//        {
//            // Мимо — покажем ринг перед боссом (если используешь свой HitFx — вызови его здесь)
//            // HitFx.ShowRing(transform.position + transform.forward * (attackRange * 0.7f), 0.25f, new Color(0.2f,0.8f,1f), 0.18f);
//        }
//    }

//    // === Событие анимации: конец клипа атаки (как в Enemy) ===
//    public void OnAttackAnimationEnd()
//    {
//        EndCombo();
//    }

//    public void EndCombo()
//    {
//        if (!_inCombo) return;

//        LogA("EndCombo");

//        _inCombo = false;
//        _facingLocked = false;
//        damageWindowOpen = false;

//        // сброс анимации
//        if (animator)
//        {
//            animator.SetBool("InCombo", false);
//            animator.ResetTrigger("Attack");
//            animator.SetFloat("Move", 0f);
//            animator.SetBool("IsMoving", false);
//        }

//        // кулдаун атаки
//        _nextAttackTime = Time.time + attackCooldown;

//        // ВАЖНО: вернуть движение всегда
//        agent.isStopped = false;
//        agent.ResetPath();
//        if (player)
//            agent.SetDestination(player.position);

//        // маленькая задержка перед следующей атакой
//        StartCoroutine(RetargetAfterDelay());
//    }



//    IEnumerator RetargetAfterDelay()
//    {
//        yield return new WaitForSeconds(reTargetDelayAfterCombo);
//        if (!_alive) yield break;

//        agent.isStopped = false;
//        agent.ResetPath();
//        if (player) agent.SetDestination(player.position);
//    }

//    // =========================
//    // Парри извне (если твоя система вызывает у босса напрямую)
//    // =========================
//    public void OnPlayerParry()
//    {
//        if (!_alive) return;

//        Vector3 reflectPos = fxSpawn ? fxSpawn.position : transform.position + transform.forward * (attackRange * 0.6f);
//        Quaternion reflectRot = Quaternion.LookRotation(-transform.forward, Vector3.up);
//        SpawnFx(parryReflectFxPrefab, reflectPos, reflectRot, parryFxSfx);
//        onBossParried?.Invoke();

//        if (phase == Phase.Survive)
//        {
//            ApplyDamageToPlayer(damageOnFailedParry, stunOnFailedParry);
//            if (!_inCombo && Time.time >= _nextAttackTime) BeginCombo();
//        }
//        else
//        {
//            Victory();
//        }
//    }

//    // =========================
//    // Урон игроку / победа
//    // =========================
//    void ApplyDamageToPlayer(int dmg, float stun)
//    {
//        if (!player) return;
//        if (player.TryGetComponent<IPlayerHealth>(out var hp)) hp.TakeDamage(dmg);
//        if (stun > 0f && player.TryGetComponent<IStunnable>(out var st)) st.Stun(stun);
//        if (player.TryGetComponent<IHurtbox>(out var hb)) hb.OnHit(dmg);
//    }

//    public void TakeDamage(int amount)
//    {
//        if (!_alive) return;
//        if (phase == Phase.Survive && invincibleInPhase1) return;
//        // Добавь HP босса по желанию
//    }

//    void Victory()
//    {
//        if (!_alive) return;
//        _alive = false;

//        agent.isStopped = true;
//        if (animator) animator.SetTrigger("Defeated");

//        onWin?.Invoke();
//        enabled = false;
//    }

//    public void ForceLose() => onLose?.Invoke();

//    // =========================
//    // FX helper
//    // =========================
//    void SpawnFx(GameObject prefab, Vector3 pos, Quaternion rot, AudioClip sfx)
//    {
//        if (prefab)
//        {
//            var fx = Instantiate(prefab, pos, rot);
//            if (fxAutoDestroy > 0f) Destroy(fx, fxAutoDestroy);
//        }
//        if (sfx) audioSource.PlayOneShot(sfx);
//    }

//    // =========================
//    // Utils / Gizmos
//    // =========================
//    float Horz(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.yellow;
//        Gizmos.DrawWireSphere(transform.position, targetChaseRange);

//        Gizmos.color = Color.red;
//        Vector3 front = transform.position + transform.forward * attackRange;
//        Gizmos.DrawWireSphere(front, 0.2f);

//        // сектор фронта
//        Vector3 left = Quaternion.AngleAxis(Mathf.Acos(frontDot) * Mathf.Rad2Deg, Vector3.up) * transform.forward;
//        Vector3 right = Quaternion.AngleAxis(-Mathf.Acos(frontDot) * Mathf.Rad2Deg, Vector3.up) * transform.forward;
//        Gizmos.DrawLine(transform.position, transform.position + left * attackRange);
//        Gizmos.DrawLine(transform.position, transform.position + right * attackRange);
//    }
//}

//// адаптеры
//public interface IPlayerHealth { void TakeDamage(int amount); }
//public interface IStunnable { void Stun(float seconds); }
//public interface IHurtbox { void OnHit(int damage); }
