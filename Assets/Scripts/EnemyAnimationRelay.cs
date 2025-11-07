using UnityEngine;

public class EnemyAnimationRelay : MonoBehaviour
{
    private EnemyAI ai;

    void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        if (ai == null)
            Debug.LogWarning("[EnemyAnimationRelay] Не найден EnemyAI у родителя.");
    }

    public void EnemyMeleeHit()
    {
        if (ai != null) ai.EnemyMeleeHit();
        else Debug.LogWarning("[EnemyAnimationRelay] EnemyAI отсутствует — вызвать EnemyMeleeHit некуда.");
    }
}
