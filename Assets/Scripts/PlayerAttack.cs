using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerAttack : MonoBehaviour
{
    [Header("Атака")]
    public float cooldown = 0.7f;      // кд между атаками
    public int damage = 1;             // урон за удар
    public float range = 1.6f;         // длина удара (капсула)
    public float radius = 0.1f;        // «толщина» удара (капсула)
    public float hitHeight = 0.8f;     // высота центра удара от пола
    public LayerMask hittableMask = ~0;

    [Tooltip("Проверять тег Enemy у цели")]
    public bool useEnemyTagCheck = true;

    [Header("Форма удара")]
    [Range(10f, 180f)]
    public float coneAngleDeg = 80f;   // ширина конуса в градусах

    float nextAttackTime;

    void Update()
    {
        bool firePressed =
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        if (firePressed && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + cooldown;
            DoMeleeHit();
        }
    }

    void DoMeleeHit()
    {
        // Геометрия удара
        Vector3 origin = transform.position + Vector3.up * hitHeight;
        Vector3 a = origin;
        Vector3 b = origin + transform.forward * range;

        // FX контура удара (по желанию можно выключить)
        Vector3 ringPos = transform.position + Vector3.up * 0.01f + transform.forward * (range * 0.7f);
        HitFx.ShowRing(ringPos, Mathf.Max(radius * 1.2f, 0.25f), new Color(0.2f, 0.8f, 1f), 0.18f);

        // Собираем кандидатов
        Collider[] hits = Physics.OverlapCapsule(a, b, radius, hittableMask, QueryTriggerInteraction.Ignore);

        // Порог косинуса для конуса (угол от forward)
        float cosThreshold = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);

        EnemyHealth bestEnemy = null;
        float bestSqrDist = float.PositiveInfinity;
        Vector3 bestHitPoint = Vector3.zero;

        foreach (var col in hits)
        {
            if (!col) continue;

            // Тег
            Transform t = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            if (useEnemyTagCheck && !t.CompareTag("Enemy")) continue;

            // Точка «центра попадания» для оценки направления
            Vector3 probe = origin + transform.forward * (range * 0.5f);
            Vector3 candidatePoint = col.ClosestPoint(probe);

            // Проекция на горизонт
            Vector3 fromPlayer = candidatePoint - (transform.position + Vector3.up * (candidatePoint.y - transform.position.y));
            fromPlayer.y = 0f;
            float sqrDist = fromPlayer.sqrMagnitude;
            if (sqrDist < 0.0001f) continue;

            // Проверка угла (впереди ли цель)
            Vector3 dir = fromPlayer.normalized;
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < cosThreshold) continue; // вне конуса

            // Выбираем ближайшего
            if (sqrDist < bestSqrDist)
            {
                if (col.TryGetComponent<EnemyHealth>(out var enemy))
                {
                    bestEnemy = enemy;
                    bestSqrDist = sqrDist;
                    bestHitPoint = candidatePoint;
                }
            }
        }

        // Наносим урон ТОЛЬКО лучшему кандидату
        if (bestEnemy != null)
        {
            HitFx.HitSpark(bestHitPoint, Color.yellow);
            bestEnemy.TakeDamage(damage, bestHitPoint, -transform.forward);
        }
    }

    // Гизмо для наглядности конуса (по желанию)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Vector3 p = transform.position + Vector3.up * hitHeight;
        float half = coneAngleDeg * 0.5f * Mathf.Deg2Rad;
        Vector3 left = Quaternion.Euler(0, -coneAngleDeg * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, coneAngleDeg * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(p, p + left * range);
        Gizmos.DrawLine(p, p + right * range);
        // дугу рисовать не обязательно
    }
}
