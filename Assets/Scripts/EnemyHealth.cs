using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHealth : MonoBehaviour
{
    public int maxHP = 50;
    public bool destroyOnDeath = true;

    int hp;
    Renderer[] rends;
    Color[] baseColors;
    float flashTimer;

    void Awake()
    {
        hp = maxHP;
        rends = GetComponentsInChildren<Renderer>();
        baseColors = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++)
            baseColors[i] = rends[i].material.color;
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hp <= 0) return;

        hp -= amount;

        if (hp <= 0)
        {
            Die();
        }
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                for (int i = 0; i < rends.Length; i++)
                    rends[i].material.color = baseColors[i];
            }
        }
    }

    void Die()
    {
        // уведомляем AI
        if (TryGetComponent<EnemyAI>(out var ai))
            ai.OnDeath();

        // удаляем объект или выключаем
        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
