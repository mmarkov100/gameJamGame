using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHP = 3;
    int hp;

    void Awake() => hp = maxHP;

    public void TakeDamage(int amount)
    {
        if (hp <= 0) return;
        hp -= amount;

        // визуализация
        HitFx.FlashRenderers(gameObject, new Color(1f, 0.2f, 0.2f), 0.06f);

        if (hp <= 0)
        {
            // смерть игрока: просто скрываем его
            if (TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            gameObject.SetActive(false);
        }
    }
}
