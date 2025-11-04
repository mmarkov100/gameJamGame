using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerAttack : MonoBehaviour
{
    [Header("јтака")]
    public Animator anim;
    public float cooldown = 0.7f;
    public int damage = 1;
    public float range = 1.6f;
    public float radius = 0.1f;
    public float hitHeight = 0.8f;
    public LayerMask hittableMask = ~0;
    public bool useEnemyTagCheck = true;

    [Header("јудио Ч атака игрока")]
    public AudioSource sfxSource;
    public AudioClip attackStartClip;
    [Range(0f, 1f)] public float attackVolume = 0.9f;
    [Range(0f, 0.3f)] public float attackPitchJitter = 0.05f;

    void PlayAttackStartSfx()
    {
        if (!attackStartClip) return;

        if (sfxSource)
        {
            float basePitch = sfxSource.pitch;

            if (attackPitchJitter > 0f)
                sfxSource.pitch = Mathf.Clamp(basePitch + Random.Range(-attackPitchJitter, attackPitchJitter), 0.5f, 2f);

            sfxSource.PlayOneShot(attackStartClip, attackVolume);

            if (attackPitchJitter > 0f)
                sfxSource.pitch = basePitch;
        }
        else
        {
            AudioSource.PlayClipAtPoint(attackStartClip, transform.position, attackVolume);
        }
    }



    [Range(10f, 180f)]
    public float coneAngleDeg = 80f;

    float nextAttackTime;
    PlayerParry parry;

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        parry = GetComponent<PlayerParry>();
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool firePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        bool firePressed = Input.GetMouseButtonDown(0);
#endif
        if (!firePressed) return;

        //  ƒ
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + cooldown;

        if (parry != null && parry.InRiposteWindow)
            anim.SetTrigger("Riposte");
        else
            PlayAttackStartSfx();
            anim.SetTrigger("Attack");
    }
    public void MeleeHit() => DoMeleeHit();

    void DoMeleeHit()
    {
        Vector3 origin = transform.position + Vector3.up * hitHeight;
        Vector3 a = origin;
        Vector3 b = origin + transform.forward * range;

        // FX
        Vector3 ringPos = transform.position + Vector3.up * 0.01f + transform.forward * (range * 0.7f);
        HitFx.ShowRing(ringPos, Mathf.Max(radius * 1.2f, 0.25f), new Color(0.2f, 0.8f, 1f), 0.18f);

        Collider[] hits = Physics.OverlapCapsule(a, b, radius, hittableMask, QueryTriggerInteraction.Ignore);
        float cosThreshold = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);

        EnemyHealth bestEnemy = null;
        float bestSqrDist = float.PositiveInfinity;
        Vector3 bestHitPoint = Vector3.zero;

        foreach (var col in hits)
        {
            if (!col) continue;

            Transform t = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            if (useEnemyTagCheck && !t.CompareTag("Enemy")) continue;

            Vector3 probe = origin + transform.forward * (range * 0.5f);
            Vector3 candidatePoint = col.ClosestPoint(probe);

            Vector3 fromPlayer = candidatePoint - new Vector3(transform.position.x, candidatePoint.y, transform.position.z);
            fromPlayer.y = 0f;
            float sqrDist = fromPlayer.sqrMagnitude;
            if (sqrDist < 0.0001f) continue;

            Vector3 dir = fromPlayer.normalized;
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < cosThreshold) continue;

            if (sqrDist < bestSqrDist && col.TryGetComponent<EnemyHealth>(out var enemy))
            {
                bestEnemy = enemy;
                bestSqrDist = sqrDist;
                bestHitPoint = candidatePoint;
            }
        }

        if (bestEnemy != null)
        {
            HitFx.HitSpark(bestHitPoint, Color.yellow);
            bestEnemy.TakeDamage(damage, bestHitPoint, -transform.forward);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Vector3 p = transform.position + Vector3.up * hitHeight;
        Vector3 left = Quaternion.Euler(0, -coneAngleDeg * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, coneAngleDeg * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(p, p + left * range);
        Gizmos.DrawLine(p, p + right * range);
    }
}
