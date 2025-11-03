using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Waiting, Staging, Approaching, Attacking, Stunned, Dead }

    [Header("Основное")]
    public string playerTag = "Player";
    public float sightRadius = 20f;
    public float faceTurnSpeed = 720f;

    [Header("Атака")]
    public float attackRange = 1.2f;
    public float attackCooldown = 3f;
    public float attackCastTime = 0.5f;
    public int damage = 1;
    [Range(0f, 1f)] public float frontDot = 0.3f;

    [Header("Стайджинг (массовка)")]
    public float ringRadius = 5.0f;
    public float ringRadiusJitter = 0.6f;
    public float idleShuffleRadius = 0.5f;
    public float idleShuffleInterval = 2.2f;
    public float reengageDelay = 1.2f;

    [Header("Интеграция")]
    public bool autoRequestEngage = true;
    public Vector2 requestEngageEvery = new Vector2(1.0f, 2.0f);

    // --- внутреннее ---
    public Transform Player => player;
    NavMeshAgent agent;
    Transform player;
    State state = State.Waiting;

    float nextAttackTime;
    bool isCasting;
    bool isStunned;
    float stunUntil;

    Vector3 stagingSpot;
    float nextIdleShuffleTime;
    float nextRequestTime;
    bool registered;

    // --- АНИМАЦИЯ ---
    [Header("Анимация")]
    public Animator anim;               // назначь в инспекторе или найдётся сам
    public float moveBlendDamp = 0.1f;  // сглаживание параметра Move

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!anim) anim = GetComponentInChildren<Animator>();
        if (anim) anim.applyRootMotion = false; // двигаем NavMeshAgent-ом
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj) player = playerObj.transform;

        if (EnemyManagerInstanceExists())
        {
            CombatDirector.Instance.Register(this);
            registered = true;
        }

        SetState(State.Waiting);
        ScheduleNextRequest();
    }

    void OnDisable()
    {
        if (registered && EnemyManagerInstanceExists())
        {
            CombatDirector.Instance.Unregister(this);
            registered = false;
        }
    }

    void Update()
    {
        if (state == State.Dead || player == null) return;

        // обновляем параметр Move для Locomotion
        UpdateMoveParam();

        if (state == State.Stunned)
        {
            agent.isStopped = true;
            if (Time.time >= stunUntil) SetState(State.Waiting);
            return;
        }

        float distToPlayer = HorzDistance(transform.position, player.position);

        if (distToPlayer > sightRadius && state != State.Approaching && state != State.Attacking)
        {
            agent.isStopped = true;
            Face(player.position);
            return;
        }

        switch (state)
        {
            case State.Waiting: HandleWaiting(); break;
            case State.Staging: HandleStaging(); break;
            case State.Approaching: HandleApproaching(); break;
            case State.Attacking: Face(player.position); break; // сам каст в корутине
        }
    }

    // ---- движение → параметр Move ----
    void UpdateMoveParam()
    {
        if (!anim) return;

        // скорость агента вдоль своего forward (для -1..+1)
        Vector3 vel = agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        float speed = vel.magnitude;

        // знак по направлению (вперёд/назад)
        float signed = 0f;
        if (speed > 0.001f)
        {
            Vector3 dir = vel.normalized;
            signed = Vector3.Dot(transform.forward, new Vector3(dir.x, 0f, dir.z));
            signed *= Mathf.Clamp01(speed / Mathf.Max(0.01f, agent.speed)); // нормализация
        }

        // при остановке гасим к нулю
        float target = agent.isStopped ? 0f : signed;
        anim.SetFloat("Move", target, moveBlendDamp, Time.deltaTime);
    }

    // ====== Состояния (как у тебя) ======
    void HandleWaiting() { /* без изменений, как в твоём коде */ EnsureStagingSpot(); MoveTowards(stagingSpot, 0.2f); IdleShuffle(); Face(player.position); if (Time.time >= nextRequestTime) { ScheduleNextRequest(); if (EnemyManagerInstanceExists()) { if (CombatDirector.Instance.CanEngage(this)) { CombatDirector.Instance.Engage(this); EnterCombat(); } } else if (autoRequestEngage) { EnterCombat(); } } }
    void HandleStaging() { EnsureStagingSpot(); MoveTowards(stagingSpot, 0.2f); IdleShuffle(); Face(player.position); if (EnemyManagerInstanceExists()) { if (CombatDirector.Instance.CanEngage(this)) { CombatDirector.Instance.Engage(this); SetState(State.Approaching); } } else { SetState(State.Approaching); } }
    void HandleApproaching()
    {
        if (isCasting) return;
        Vector3 approachPoint = player.position;
        MoveTowards(approachPoint, Mathf.Max(0.1f, attackRange - 0.1f));
        Face(player.position);

        float dist = HorzDistance(transform.position, player.position);
        if (dist <= attackRange && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(AttackCastRoutine());
        }
    }

    public bool CanRequestEngage() { return (state == State.Waiting || state == State.Staging) && !isStunned; }
    public void EnterCombat() { SetState(State.Approaching); }
    public void LeaveCombatToStaging() 
    { 
        SetState(State.Staging); 
        nextAttackTime = Time.time + reengageDelay; if (EnemyManagerInstanceExists()) CombatDirector.Instance.Disengage(this); 
    }

    public void ApplyStun(float seconds)
    {
        isStunned = true;
        stunUntil = Time.time + seconds;
        isCasting = false;
        agent.isStopped = true;
        HitFx.ShowRing(transform.position, 0.8f, new Color(0.2f, 1f, 1f), 0.3f);
        HitFx.FlashRenderers(gameObject, new Color(0.6f, 0.9f, 1f), 0.12f);
        SetState(State.Stunned);
    }

    // ====== Каст и удар ======
    IEnumerator AttackCastRoutine()
    {
        isCasting = true;
        SetState(State.Attacking);
        agent.isStopped = true;

        HitFx.ShowRing(transform.position, 0.9f, Color.red, attackCastTime);
        if (anim) anim.SetTrigger("Attack");

        // ждём подготовку удара (анимация уже идёт)
        yield return new WaitForSeconds(attackCastTime);

        // ждём подготовку
        float endTime = Time.time + attackCastTime;
        while (Time.time < endTime)
        {
            Face(player ? player.position : transform.position + transform.forward);
            yield return null;
        }

        // сам урон пойдёт из события EnemyMeleeHit()
        // после атаки — назад на периметр
        isCasting = false;
        LeaveCombatToStaging();
    }

    // вызывется Animation Event из клипа "Attack"
    public void EnemyMeleeHit()
    {
        if (!player) return;

        // парирование прямо перед уроном
        if (player.TryGetComponent<PlayerParry>(out var parry))
        {
            if (parry.TryParry(transform, out float stun))
            {
                ApplyStun(stun);
                return; // отражено — урона нет
            }
        }

        float dist = HorzDistance(transform.position, player.position);
        Vector3 dir = (player.position - transform.position); dir.y = 0; dir.Normalize();

        bool inRange = dist <= attackRange;
        bool inFront = Vector3.Dot(transform.forward, dir) > frontDot;

        if (inRange && inFront && player.TryGetComponent<PlayerHealth>(out var hp))
        {
            hp.TakeDamage(damage);
            HitFx.HitSpark(player.position + Vector3.up * 0.3f, Color.red, 0.25f, 0.25f);
        }
        else
        {
            Vector3 ringPos = transform.position + Vector3.up * 0.01f + transform.forward * (attackRange * 0.7f);
            HitFx.ShowRing(ringPos, 0.25f, new Color(0.2f, 0.8f, 1f), 0.18f);
        }
    }

    public void OnAttackAnimationEnd()
    {
        isCasting = false;
        LeaveCombatToStaging();
    }  

    // ====== Вспомогательное (как было) ======
    void SetState(State s) { if (state == s) return; state = s; switch (state) { case State.Waiting: EnsureStagingSpot(); agent.isStopped = false; break; case State.Staging: EnsureStagingSpot(); agent.isStopped = false; break; case State.Approaching: agent.isStopped = false; break; case State.Attacking: agent.isStopped = true; break; case State.Stunned: agent.isStopped = true; break; case State.Dead: agent.isStopped = true; break; } }
    void EnsureStagingSpot() { if (!player) return; int index = CombatDirector.Instance != null ? CombatDirector.Instance.GetIndex(this) : Random.Range(0, 360); float angle = ((float)index / Mathf.Max(1, CombatDirector.Instance.TotalEnemies())) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f); float radius = ringRadius + Random.Range(-ringRadiusJitter, ringRadiusJitter); Vector3 center = player.position; Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius; stagingSpot = center + offset; }
    void IdleShuffle() { if (Time.time < nextIdleShuffleTime) return; nextIdleShuffleTime = Time.time + idleShuffleInterval + Random.Range(-0.4f, 0.4f); Vector2 rnd = Random.insideUnitCircle * idleShuffleRadius; Vector3 target = stagingSpot + new Vector3(rnd.x, 0, rnd.y); agent.SetDestination(target); }
    void MoveTowards(Vector3 worldPos, float stopDistance) { agent.stoppingDistance = stopDistance; agent.SetDestination(worldPos); agent.isStopped = false; }
    void Face(Vector3 worldPos) { Vector3 dir = worldPos - transform.position; dir.y = 0; if (dir.sqrMagnitude < 0.0001f) return; Quaternion look = Quaternion.LookRotation(dir); transform.rotation = Quaternion.RotateTowards(transform.rotation, look, faceTurnSpeed * Time.deltaTime); }
    float HorzDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
    void ScheduleNextRequest() { nextRequestTime = Time.time + Random.Range(requestEngageEvery.x, requestEngageEvery.y); }
    bool EnemyManagerInstanceExists() { return CombatDirector.Instance != null; }
    public void OnDeath() { SetState(State.Dead); if (EnemyManagerInstanceExists()) CombatDirector.Instance.Unregister(this); agent.isStopped = true; enabled = false; }
    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, attackRange); if (player != null) { Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f); Gizmos.DrawWireSphere(player.position, ringRadius); } }
}
