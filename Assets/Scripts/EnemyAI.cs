using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ИИ врага для арены: ожидание на периметре, вход в бой, атака с кастом,
/// поддержка парирования/стана, интеграция с EnemyManager (опционально).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State
    {
        Waiting,    // стоит в толпе, вне активного боя
        Staging,    // занимает позицию на окружности вокруг игрока
        Approaching,// вошёл в бой, подходит для удара
        Attacking,  // каст/удар
        Stunned,    // оглушён
        Dead
    }

    [Header("Основное")]
    public string playerTag = "Player";
    public float sightRadius = 20f;          // дистанция отслеживания игрока
    public float faceTurnSpeed = 720f;       // скорость разворота к игроку

    [Header("Атака")]
    public float attackRange = 1.2f;         // дистанция удара
    public float attackCooldown = 3f;        // КД между атаками
    public float attackCastTime = 0.5f;      // телеграф/подготовка удара
    public int damage = 1;                   // урон
    [Range(0f, 1f)] public float frontDot = 0.3f; // > 0.3 — цель «впереди»

    [Header("Стайджинг (массовка)")]
    public float ringRadius = 5.0f;          // радиус окружения игрока
    public float ringRadiusJitter = 0.6f;    // случайное отклонение радиуса
    public float idleShuffleRadius = 0.5f;   // микро-движения на «споте»
    public float idleShuffleInterval = 2.2f; // раз в N секунд чуть смещаться
    public float reengageDelay = 1.2f;       // отдых после атаки перед новым входом

    [Header("Интеграция с менеджером")]
    public bool autoRequestEngage = true;    // если нет EnemyManager — сам будет пытаться вступать
    public Vector2 requestEngageEvery = new Vector2(1.0f, 2.0f); // интервал запросов на вход

    // внутреннее
    public Transform Player => player;
    NavMeshAgent agent;
    Transform player;
    State state = State.Waiting;

    float nextAttackTime;
    bool isCasting;
    bool isStunned;
    float stunUntil;

    Vector3 stagingSpot;         // целевая точка «массовки» на окружности
    float nextIdleShuffleTime;   // для лёгких перемещений на споте
    float nextRequestTime;       // периодические попытки войти в бой
    bool registered;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj) player = playerObj.transform;

        // регистрация в менеджере (если есть)
        if (EnemyManagerInstanceExists())
        {
            CombatDirector.Instance.Register(this);
            registered = true;
        }

        // стартовое состояние
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

        // обработка стана
        if (state == State.Stunned)
        {
            agent.isStopped = true;
            if (Time.time >= stunUntil)
            {
                // из стана возвращаемся в ожидание/массовку
                SetState(State.Waiting);
            }
            return;
        }

        // дистанция до игрока по горизонту
        float distToPlayer = HorzDistance(transform.position, player.position);

        // далёк от игрока? стоим на месте
        if (distToPlayer > sightRadius && state != State.Approaching && state != State.Attacking)
        {
            agent.isStopped = true;
            Face(player.position);
            return;
        }

        switch (state)
        {
            case State.Waiting:
                // ждём слота на вход в бой
                HandleWaiting();
                break;

            case State.Staging:
                HandleStaging();
                break;

            case State.Approaching:
                HandleApproaching();
                break;

            case State.Attacking:
                // обработка в корутине AttackCastRoutine()
                Face(player.position);
                break;
        }
    }

    // ====== Состояния ======

    void HandleWaiting()
    {
        // держимся на периметре, слегка смотрим/двигаемся
        EnsureStagingSpot();
        MoveTowards(stagingSpot, stopDistance: 0.2f);
        IdleShuffle();
        Face(player.position);

        // пытаемся войти в бой — через менеджер или самостоятельно
        if (Time.time >= nextRequestTime)
        {
            ScheduleNextRequest();

            if (EnemyManagerInstanceExists())
            {
                if (CombatDirector.Instance.CanEngage(this))
                {
                    CombatDirector.Instance.Engage(this);
                    EnterCombat();
                }
            }
            else if (autoRequestEngage)
            {
                // простая вероятность входа — можно заменить на логику лимита
                EnterCombat();
            }
        }
    }

    void HandleStaging()
    {
        // занимаем «спот» на окружности вокруг игрока
        EnsureStagingSpot();
        MoveTowards(stagingSpot, stopDistance: 0.2f);
        IdleShuffle();
        Face(player.position);

        // если нам позволено — начинаем сближение для атаки
        if (EnemyManagerInstanceExists())
        {
            if (CombatDirector.Instance.CanEngage(this))
            {
                CombatDirector.Instance.Engage(this);
                SetState(State.Approaching);
            }
        }
        else
        {
            SetState(State.Approaching);
        }
    }

    void HandleApproaching()
    {
        if (isCasting) return;

        // подходим к дистанции удара
        Vector3 approachPoint = player.position - (player.forward * 0.0f); // можно смещать
        MoveTowards(approachPoint, stopDistance: Mathf.Max(0.1f, attackRange - 0.1f));
        Face(player.position);

        float dist = HorzDistance(transform.position, player.position);
        if (dist <= attackRange)
        {
            agent.isStopped = true;

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                StartCoroutine(AttackCastRoutine());
            }
        }
    }

    // ====== Переходы ======

    public bool CanRequestEngage()
    {
        // враг свободен и готов войти в бой
        return (state == State.Waiting || state == State.Staging) && !isStunned;
    }

    public void EnterCombat()
    {
        // при входе переводим во «вход в бой»
        SetState(State.Approaching);
    }

    public void LeaveCombatToStaging()
    {
        // после удара/ошибки — возвращаемся на периметр
        SetState(State.Staging);
        nextAttackTime = Time.time + reengageDelay;

        if (EnemyManagerInstanceExists())
            CombatDirector.Instance.Disengage(this);
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

    // ====== Атака с кастом и парированием ======

    IEnumerator AttackCastRoutine()
    {
        isCasting = true;
        SetState(State.Attacking);

        // телеграф удара
        HitFx.ShowRing(transform.position, 0.9f, Color.red, attackCastTime);
        agent.isStopped = true;

        // ждём каст
        float endTime = Time.time + attackCastTime;
        while (Time.time < endTime)
        {
            // во время каста стоим и смотрим на игрока
            Face(player != null ? player.position : transform.position + transform.forward);
            yield return null;
        }

        // перед ударом — проверка парирования
        if (player != null && player.TryGetComponent<PlayerParry>(out var parry))
        {
            if (parry.TryParry(transform, out float stunDuration))
            {
                ApplyStun(stunDuration);  // отражено
                isCasting = false;
                yield break;
            }
        }

        // обычный удар: проверяем дистанцию и «впереди ли игрок»
        if (player != null)
        {
            float dist = HorzDistance(transform.position, player.position);
            Vector3 dir = (player.position - transform.position);
            dir.y = 0;
            dir.Normalize();

            bool inRange = dist <= attackRange;
            bool inFront = Vector3.Dot(transform.forward, dir) > frontDot;

            if (inRange && inFront)
            {
                if (player.TryGetComponent<PlayerHealth>(out var hp))
                    hp.TakeDamage(damage);

                HitFx.HitSpark(player.position + Vector3.up * 0.3f, Color.red, 0.25f, 0.25f);
            }
            else
            {
                // промах
                Vector3 ringPos = transform.position + Vector3.up * 0.01f + transform.forward * (attackRange * 0.7f);
                HitFx.ShowRing(ringPos, 0.25f, new Color(0.2f, 0.8f, 1f), 0.18f);
            }
        }

        isCasting = false;

        // освободить слот и вернуться на периметр
        LeaveCombatToStaging();
    }

    // ====== Вспомогательное ======

    void SetState(State s)
    {
        if (state == s) return;

        state = s;

        switch (state)
        {
            case State.Waiting:
                EnsureStagingSpot();
                agent.isStopped = false;
                break;

            case State.Staging:
                EnsureStagingSpot();
                agent.isStopped = false;
                break;

            case State.Approaching:
                agent.isStopped = false;
                break;

            case State.Attacking:
                agent.isStopped = true;
                break;

            case State.Stunned:
                agent.isStopped = true;
                break;

            case State.Dead:
                agent.isStopped = true;
                break;
        }
    }

    void EnsureStagingSpot()
    {
        if (player == null) return;

        // индекс врага в массиве — его позиция на круге
        int index = CombatDirector.Instance != null ?
            CombatDirector.Instance.GetIndex(this) :
            Random.Range(0, 360);

        float angle = ((float)index / Mathf.Max(1, CombatDirector.Instance.TotalEnemies())) * Mathf.PI * 2f;

        // случайный offset чтобы не было идеальной сетки
        angle += Random.Range(-0.2f, 0.2f);

        float radius = ringRadius + Random.Range(-ringRadiusJitter, ringRadiusJitter);

        Vector3 center = player.position;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;

        stagingSpot = center + offset;
    }

    void IdleShuffle()
    {
        if (Time.time < nextIdleShuffleTime) return;
        nextIdleShuffleTime = Time.time + idleShuffleInterval + Random.Range(-0.4f, 0.4f);

        // немного сдвинемся вокруг stagingSpot
        Vector2 rnd = Random.insideUnitCircle * idleShuffleRadius;
        Vector3 target = stagingSpot + new Vector3(rnd.x, 0, rnd.y);
        agent.SetDestination(target);
    }

    void MoveTowards(Vector3 worldPos, float stopDistance)
    {
        agent.stoppingDistance = stopDistance;
        agent.SetDestination(worldPos);
        agent.isStopped = false;
    }

    void Face(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, faceTurnSpeed * Time.deltaTime);
    }

    float HorzDistance(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }

    void ScheduleNextRequest()
    {
        nextRequestTime = Time.time + Random.Range(requestEngageEvery.x, requestEngageEvery.y);
    }

    bool EnemyManagerInstanceExists()
    {
        // безопасная проверка на наличие синглтона
        return CombatDirector.Instance != null;
    }

    // Можно вызвать из EnemyHealth.Die()
    public void OnDeath()
    {
        SetState(State.Dead);
        if (EnemyManagerInstanceExists())
            CombatDirector.Instance.Unregister(this);
        agent.isStopped = true;
        enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, attackRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        if (player != null)
            Gizmos.DrawWireSphere(player.position, ringRadius);
    }
}
