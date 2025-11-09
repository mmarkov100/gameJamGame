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

    // === FLASH (вместо ParrySonarSwitcher) ===
    [System.Serializable]
    public struct LightEntry
    {
        public Light light;          // источник света
        public float flashIntensity; // интенсивность на время вспышки
    }

    [Header("Parry-Kill Flash")]
    [Tooltip("Света, которые должны вспыхнуть ТОЛЬКО когда босс умер от парирования после 30 сек.")]
    public LightEntry[] flashLights;
    [Tooltip("Длительность вспышки света при парри-килле.")]
    public float parryKillLightTime = 2.0f;

    [Tooltip("Опционально: подсветка игрока, которую надо подавить на время вспышки.")]
    public PlayerAlwaysGlow playerGlow;

    // базовые значения света
    float[] baseIntensities;
    bool[] baseEnabled;
    bool flashInitialized;
    bool parryKillFlashPlayed; // защита от повторного триггера

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

        InitFlashLights();
        if (!playerGlow) playerGlow = FindObjectOfType<PlayerAlwaysGlow>();
    }

    void InitFlashLights()
    {
        if (flashLights == null) flashLights = new LightEntry[0];
        baseIntensities = new float[flashLights.Length];
        baseEnabled = new bool[flashLights.Length];

        for (int i = 0; i < flashLights.Length; i++)
        {
            var L = flashLights[i].light;
            if (!L) continue;

            baseIntensities[i] = L.intensity;
            baseEnabled[i] = L.enabled;

            // подготовим: включим, но сделаем тёмным
            L.enabled = true;
            L.intensity = 0f;
        }
        flashInitialized = true;
    }

    void OnEnable()
    {
        spawnTime = Time.time;
        parryKillFlashPlayed = false;
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
            PlayRandom(sfxParryPunish);
            DamagePlayerDirect();
        }
        else
        {
            PlayRandom(sfxParryKill);

            // ⚡ сразу оборвать атаку и переключить аниматор на смерть
            InterruptAttackForDeath();

            if (health && !health.IsDead)
            {
                health.KillImmediate();          // вызовет OnDeath(), но мы уже перевели аниматор в смерть
                if (!parryKillFlashPlayed)
                {
                    parryKillFlashPlayed = true;
                    StartCoroutine(ParryKillFlashCo());
                }
            }
        }
    }


    public bool IsCurrentlyInvulnerable()
    {
        return Time.time - spawnTime < invulnerableSeconds;
    }

    // ==== АНИМАЦИОННЫЕ ИВЕНТЫ ====

    public void AttackStartEvent() { /* звук замаха уже ставим в EnemyMeleeHit если мимо */ }

    private void InterruptAttackForDeath()
    {
        // останавливаем преследование и атаку
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        AttackEndCallback = null;

        isAttacking = false;
        isInCooldown = false;

        if (agent)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (animator)
        {
            // сброс триггеров атаки и состояния комбо
            animator.ResetTrigger(triggerAttack);
            animator.SetBool(paramIsAttacking, false);

            // моментально уйти в смерть (см. настройки контроля ниже)
            animator.ResetTrigger(triggerDie);
            animator.SetTrigger(triggerDie);
        }
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
                if (parry.TryParry(transform, out float _))
                {
                    // ✅ Успешное отражение (далее ветвление внутри OnParryAttemptSuccessful)
                    OnParryAttemptSuccessful();
                    return;
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

    // ==== FLASH LOGIC ====
    private IEnumerator ParryKillFlashCo()
    {
        if (!flashInitialized) InitFlashLights();

        var sonar = SonarController.Instance;

        // 1) Пауза сонара и подсветок
        if (sonar) sonar.paused = true;
        if (playerGlow) playerGlow.SetSuppressed(true);
        EnemySonarResponder.SetSuppressed(true);

        // Дадим кадр на снятие материалов/эмиссии
        yield return null;

        // 2) Включаем каждый свет своей интенсивностью
        for (int i = 0; i < flashLights.Length; i++)
        {
            var entry = flashLights[i];
            var L = entry.light;
            if (!L) continue;

            L.enabled = true;
            L.intensity = entry.flashIntensity;
        }

        yield return new WaitForSeconds(parryKillLightTime);

        // 3) Откат света к исходным значениям
        for (int i = 0; i < flashLights.Length; i++)
        {
            var L = flashLights[i].light;
            if (!L) continue;

            L.intensity = baseIntensities[i];
            L.enabled = baseEnabled[i];
        }

        // 4) Возврат эффектов
        EnemySonarResponder.SetSuppressed(false);
        if (playerGlow) playerGlow.SetSuppressed(false);
        if (sonar) sonar.paused = false;
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
