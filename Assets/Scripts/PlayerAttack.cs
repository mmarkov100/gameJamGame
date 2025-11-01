using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerAttack : MonoBehaviour
{
    [Header("Атака")]
    public float cooldown = 1f;          // кд между атаками
    public int damage = 25;              // урон за удар
    public float range = 1.6f;           // длина удара
    public float radius = 0.5f;          // «толщина» удара
    public float hitHeight = 0.8f;       // высота центра удара от пола
    public LayerMask hittableMask = ~0;  // можно ограничить слоем Enemy, иначе ~0

    [Tooltip("Проверять тег Enemy у цели (рекомендую оставить включённым)")]
    public bool useEnemyTagCheck = true;

    float nextAttackTime;

    void Update()
    {
        bool firePressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (firePressed && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + cooldown;
            DoMeleeHit();
        }
    }

    void DoMeleeHit()
    {
        // капсула от A до B перед игроком
        Vector3 origin = transform.position + Vector3.up * hitHeight;
        Vector3 a = origin;
        Vector3 b = origin + transform.forward * range;

        // ищем все коллайдеры в капсуле
        Collider[] hits = Physics.OverlapCapsule(a, b, radius, hittableMask, QueryTriggerInteraction.Ignore);

        // не бить один и тот же объект дважды
        var seen = new HashSet<Transform>();

        foreach (var col in hits)
        {
            if (col == null) continue;

            var t = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            if (seen.Contains(t)) continue;
            seen.Add(t);

            if (useEnemyTagCheck && !t.CompareTag("Enemy")) continue;

            // пробуем нанести урон
            if (col.TryGetComponent<EnemyHealth>(out var enemy))
            {
                Vector3 hitPoint = col.ClosestPoint(origin + transform.forward * (range * 0.5f));
                enemy.TakeDamage(damage, hitPoint, -transform.forward);
            }
        }
    }

    // визуализация капсулы удара в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.red;

        Vector3 origin = transform.position + Vector3.up * hitHeight;
        Vector3 a = origin;
        Vector3 b = origin + transform.forward * range;

        // грубая визуализация: несколько сфер вдоль линии
        int steps = 6;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = Vector3.Lerp(a, b, t);
            Gizmos.DrawWireSphere(p, radius);
        }
    }
}
