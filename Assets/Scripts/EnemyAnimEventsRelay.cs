using UnityEngine;

public class EnemyAnimEventsRelay : MonoBehaviour
{
    public void EnemyMeleeHit()
    {
        var ai = GetComponentInParent<EnemyAI>();
        if (ai) ai.EnemyMeleeHit();
    }

    // по желанию: событие в конце клипа
    public void EnemyAttackEnd()
    {
        var ai = GetComponentInParent<EnemyAI>();
        if (ai) ai.OnAttackAnimationEnd();
    }
}
