using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public string playerTag = "Player";
    public float attackRange = 1.2f;           // на каком расстоянии бьём
    public float attackCooldown = 3f;          // кд между атаками
    public float attackCastTime = 0.5f;        // время подготовки удара
    public int damage = 1;                     // урон
    public float faceTurnSpeed = 720f;         // скорость поворота к цели (°/с)

    NavMeshAgent agent;
    Transform target;
    float nextAttackTime;
    bool isCasting;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj) target = playerObj.transform;
    }

    void Update()
    {
        if (!target || isCasting) return;

        // расстояние без учета высоты
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = target.position; b.y = 0;
        float dist = Vector3.Distance(a, b);

        if (dist <= attackRange)
        {
            agent.isStopped = true;
            FaceTarget();

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                StartCoroutine(AttackCastRoutine());
            }
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }
    }

    System.Collections.IEnumerator AttackCastRoutine()
    {
        isCasting = true;

        // показать круг предупреждения (каст)
        HitFx.ShowRing(transform.position, 0.9f, Color.red, attackCastTime);

        // стоим и готовим удар
        agent.isStopped = true;

        // ждём каст
        yield return new WaitForSeconds(attackCastTime);

        // проверяем, что цель ещё существует
        if (target != null)
        {
            // перед атакой снова лицом к игроку
            FaceTarget();

            // расстояние и направление сейчас
            Vector3 a = transform.position; a.y = 0;
            Vector3 b = target.position; b.y = 0;
            float dist = Vector3.Distance(a, b);

            // проверяем попадание: игрок в радиусе и в секторе перед врагом
            bool inRange = dist <= attackRange;

            Vector3 dir = (target.position - transform.position).normalized;
            dir.y = 0;
            float dot = Vector3.Dot(transform.forward, dir); // >0 значит впереди

            bool inFront = dot > 0.3f; // можно настроить угол атаки

            if (inRange && inFront)
            {
                if (target.TryGetComponent<PlayerHealth>(out var hp))
                    hp.TakeDamage(damage);

                // эффект удара (искры под игроком)
                HitFx.HitSpark(target.position + Vector3.up * 0.3f, Color.red, 0.25f, 0.25f);
            }
        }

        // завершаем каст
        isCasting = false;
        agent.isStopped = false;
    }

    void FaceTarget()
    {
        if (!target) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, faceTurnSpeed * Time.deltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, attackRange);
    }
}
