using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public string playerTag = "Player";
    public float attackRange = 1.2f;   // на каком расстоянии бьём
    public float attackCooldown = 3f;  // кд удара
    public int damage = 1;             // урон за удар
    public float faceTurnSpeed = 720f; // скорость поворота к цели (°/с)

    NavMeshAgent agent;
    Transform target;
    float nextAttackTime;

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
        if (!target) return;

        // идём к игроку
        agent.SetDestination(target.position);

        // горизонтальная дистанция
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

                if (target.TryGetComponent<PlayerHealth>(out var hp))
                    hp.TakeDamage(damage);
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    void FaceTarget()
    {
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
