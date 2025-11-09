using UnityEngine;

public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("HP")]
    public float maxHP = 99999f;  // "Очень много", пока идёт ранняя фаза
    public float currentHP;

    [Header("Hooks")]
    public BossAI bossAI;

    [HideInInspector] public bool IsDead;
    public System.Action onDeath;

    void Awake()
    {
        currentHP = maxHP;
        if (!bossAI) bossAI = GetComponent<BossAI>();
    }

    public void ApplyDamage(float amount)
    {
        if (IsDead) return;

        // В первые 30 сек — игнорируем входящий урон полностью
        if (bossAI && bossAI.IsCurrentlyInvulnerable())
            return;

        currentHP -= Mathf.Abs(amount);
        if (currentHP <= 0f)
        {
            KillImmediate();
        }
    }

    public void KillImmediate()
    {
        if (IsDead) return;
        IsDead = true;
        currentHP = 0f;
        onDeath?.Invoke();

        // Отключим коллайдеры/агент, если нужно
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent) agent.isStopped = true;

        // Можно добавить авто-удаление
        // Destroy(gameObject, 5f);
    }
}
