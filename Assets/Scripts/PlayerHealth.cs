using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("’ѕ игрока")]
    public int maxHP = 3;
    int hp;

    [Header("Ќеу€звимость после удара")]
    public float invulDuration = 0.5f; // длительность неу€звимости (i-frames)
    float invulUntilTime = -1f;

    void Awake() => hp = maxHP;

    public void TakeDamage(int amount)
    {
        // игрок мЄртв Ч уже пофиг
        if (hp <= 0) return;

        // если неу€звим Ч игнорируем урон
        if (Time.time < invulUntilTime)
            return;

        // начинаем i-frames
        invulUntilTime = Time.time + invulDuration;

        hp -= amount;

        if (hp <= 0)
        {
            if (TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            gameObject.SetActive(false);
            FindObjectOfType<ReloadSceneOnDeath>().OnPlayerDeath();
        }
    }
}
