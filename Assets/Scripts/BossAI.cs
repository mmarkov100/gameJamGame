using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

public class BossAI : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Animator animator;
    public NavMeshAgent agent;
    public BossHealth health;

    [Header("Movement")]
    public float stopDistance = 2.5f;
    public float attackStartDistance = 2.2f;

    [Header("Attack")]
    public int comboHits = 3;
    public float comboGap = 0.25f;
    public float attackCooldown = 2.0f;
    public float meleeDamage = 10f;
    public float meleeRange = 2.3f;
    public float meleeWidth = 1.6f;
    public LayerMask playerMask;

    [Header("Invulnerability Phase")]
    public float invulnerableSeconds = 30f;
    private float spawnTime;

    [Header("Animation Parameter Names")]
    public string paramSpeed = "Speed";
    public string triggerAttack = "Attack";
    public string paramIsAttacking = "IsAttacking";
    public string triggerDie = "Die";

    // === AUDIO ===
    [Header("Audio")]
    [Tooltip("Источник, через который будут играть все SFX босса. Если не задан — создастся автоматически.")]
    public AudioSource sfxSource;
    public AudioMixerGroup outputMixerGroup;

    [Tooltip("Звук в начале серии ударов (комбо старт).")]
    public AudioClip[] sfxComboStart;
    [Tooltip("Звук замаха/попытки удара (на старте атаки).")]
    public AudioClip[] sfxSwing;
    [Tooltip("Звук попадания по игроку.")]
    public AudioClip[] sfxHit;
    [Tooltip("Звук при попытке парирования ДО 30 сек (наказание игрока).")]
    public AudioClip[] sfxParryPunish;
    [Tooltip("Звук при парировании ПОСЛЕ 30 сек (смерть босса).")]
    public AudioClip[] sfxParryKill;

    [Range(0.8f, 1.2f)] public float randomPitchMin = 0.96f;
    [Range(0.8f, 1.2f)] public float randomPitchMax = 1.04f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    // runtime
    private bool isAttacking;
    private bool isInCooldown;
    private Coroutine attackRoutine;
    private System.Action AttackEndCallback;

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!health) health = GetComponent<BossHealth>();

        // Автосоздание источника звука при необходимости
        if (!sfxSource)
        {
            var child = new GameObject("Boss_SFX_AudioSource");
            child.transform.SetParent(transform, false);
            sfxSource = child.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 1f; // 3D
            sfxSource.rolloffMode = AudioRolloffMode.Linear;
            sfxSource.minDistance = 2f;
            sfxSource.maxDistance = 25f;
        }
        if (outputMixerGroup) sfxSource.outputAudioMixerGroup = outputMixerGroup;
    }

    void OnEnable()
    {
        spawnTime = Time.time;
        if (health) health.onDeath += OnDeath;
    }

    void OnDisable()
    {
        if (health) health.onDeath -= OnDeath;
    }

    void Update()
    {
        if (health && health.IsDead) return;
        if (!player) return;

        var flatDir = player.position - transform.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.001f)
        {
            var look = Quaternion.LookRotation(flatDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 10f);
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (isAttacking)
        {
            agent.isStopped = true;
            animator.SetFloat(paramSpeed, 0f);
            return;
        }

        if (dist > stopDistance)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            animator.SetFloat(paramSpeed, agent.velocity.magnitude);
        }
        else
        {
            agent.isStopped = true;
            animator.SetFloat(paramSpeed, 0f);
        }

        if (!isInCooldown && !isAttacking && dist <= attackStartDistance)
        {
            attackRoutine = StartCoroutine(ComboRoutine());
        }
    }

    private IEnumerator ComboRoutine()
    {
        isAttacking = true;
        animator.SetBool(paramIsAttacking, true);
        agent.isStopped = true;

        // 🔊 звук начала серии ударов
        PlayRandom(sfxComboStart);

        for (int i = 0; i < comboHits; i++)
        {
            animator.ResetTrigger(triggerAttack);
            animator.SetTrigger(triggerAttack);

            float safety = 0f;
            bool ended = false;
            System.Action endCb = () => ended = true;
            AttackEndCallback = endCb;

            while (!ended && safety < 3.0f)
            {
                safety += Time.deltaTime;
                yield return null;
            }
            AttackEndCallback = null;

            if (i < comboHits - 1 && comboGap > 0f)
                yield return new WaitForSeconds(comboGap);
        }

        animator.SetBool(paramIsAttacking, false);
        isAttacking = false;
        isInCooldown = true;
        yield return new WaitForSeconds(attackCooldown);
        isInCooldown = false;
    }

    // ==== ПАРРИ ====
    public void OnParryAttemptSuccessful()
    {
        if (Time.time - spawnTime < invulnerableSeconds)
        {
            // 🔊 парри в ранней фазе — наказываем игрока
            PlayRandom(sfxParryPunish);
            DamagePlayerDirect();
        }
        else
        {
            // 🔊 успешное парри после 30 сек — смерть босса
            PlayRandom(sfxParryKill);
            if (health && !health.IsDead) health.KillImmediate();
        }
    }

    public bool IsCurrentlyInvulnerable()
    {
        return Time.time - spawnTime < invulnerableSeconds;
    }

    // ==== АНИМАЦИОННЫЕ ИВЕНТЫ ====
    public void AttackStartEvent()
    {

    }

    public void EnemyMeleeHit()
    {
        Vector3 origin = transform.position + transform.forward * (meleeRange * 0.5f);
        float radius = meleeWidth * 0.5f;

        Collider[] hits = Physics.OverlapCapsule(
            transform.position + Vector3.up * 0.9f,
            origin + Vector3.up * 0.9f,
            radius,
            playerMask
        );

        bool hitSomething = false;

        foreach (var h in hits)
        {
            if (!h) continue;
            hitSomething = true;

            // Пытаемся получить PlayerParry у цели
            var parry = h.GetComponentInParent<PlayerParry>();
            if (parry != null)
            {
                if (parry.TryParry(transform, out float stun))
                {
                    // ✅ Успешное отражение
                    OnParryAttemptSuccessful();  // применяет логику бессмертия / смерти
                    return; // прекращаем атаку
                }
            }

            // ❌ Если парирования не было — обычный урон
            ApplyDamageToPlayer(new Collider[] { h });
            PlayRandom(sfxHit);
            return;
        }

        // Если вообще никого не задели — играем свист замаха
        if (!hitSomething)
            PlayRandom(sfxSwing);
    }


    public void AttackEndEvent()
    {
        AttackEndCallback?.Invoke();
    }

    // ==== ДАМАГ ИГРОКУ ====
    private void ApplyDamageToPlayer(Collider[] hits)
    {
        foreach (var h in hits)
        {
            if (!h) continue;
            var dmg = h.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.ApplyDamage(meleeDamage);
            else
            {
                h.SendMessageUpwards("ApplyDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
                h.SendMessageUpwards("TakeDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void DamagePlayerDirect()
    {
        if (!player) return;
        var dmg = player.GetComponentInParent<IDamageable>();
        if (dmg != null) dmg.ApplyDamage(meleeDamage);
        else
        {
            player.SendMessageUpwards("ApplyDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
            player.SendMessageUpwards("TakeDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnDeath()
    {
        if (agent) agent.isStopped = true;
        isAttacking = false;
        if (animator) animator.SetTrigger(triggerDie);
        // опционально: sfxSource.Stop();
    }

    // ==== AUDIO HELPERS ====
    private void PlayRandom(AudioClip[] bank)
    {
        if (bank == null || bank.Length == 0 || !sfxSource) return;
        var clip = bank[Random.Range(0, bank.Length)];
        if (!clip) return;

        float oldPitch = sfxSource.pitch;
        sfxSource.pitch = Random.Range(randomPitchMin, randomPitchMax);
        sfxSource.PlayOneShot(clip, sfxVolume);
        sfxSource.pitch = oldPitch;
    }
}

// как и раньше:
public interface IDamageable
{
    void ApplyDamage(float amount);
}
