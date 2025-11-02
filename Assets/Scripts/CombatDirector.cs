using System.Collections.Generic;
using UnityEngine;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance;

    public int maxActiveEnemies = 10;
    public int maxEngagedEnemies = 2;
    public float selectInterval = 3f;

    List<EnemyAI> allEnemies = new();
    List<EnemyAI> engagedEnemies = new();

    float nextSelectTime;

    void Awake() => Instance = this;

    public void Register(EnemyAI enemy)
    {
        allEnemies.Add(enemy);
    }

    public void Unregister(EnemyAI enemy)
    {
        allEnemies.Remove(enemy);
        engagedEnemies.Remove(enemy);
    }

    public bool CanEngage(EnemyAI enemy)
    {
        return engagedEnemies.Count < maxEngagedEnemies && !engagedEnemies.Contains(enemy);
    }

    public void Engage(EnemyAI enemy)
    {
        if (!engagedEnemies.Contains(enemy))
            engagedEnemies.Add(enemy);
    }

    public void Disengage(EnemyAI enemy)
    {
        engagedEnemies.Remove(enemy);
    }

    public int TotalEnemies() => allEnemies.Count;

    public int GetIndex(EnemyAI e) => allEnemies.IndexOf(e);

    void Update()
    {
        if (Time.time < nextSelectTime) return;
        nextSelectTime = Time.time + selectInterval;

        // очищаем engaged, чтобы перераспределить
        foreach (var e in engagedEnemies)
            e.LeaveCombatToStaging();
        engagedEnemies.Clear();

        if (allEnemies.Count == 0) return;

        // сортируем по расстоянию к игроку
        allEnemies.Sort((a, b) =>
        {
            float da = Vector3.SqrMagnitude(a.transform.position - a.Player.position);
            float db = Vector3.SqrMagnitude(b.transform.position - b.Player.position);
            return da.CompareTo(db);
        });

        int count = Mathf.Min(maxEngagedEnemies, allEnemies.Count);

        for (int i = 0; i < count; i++)
        {
            var e = allEnemies[i];
            if (!e.CanRequestEngage()) continue;

            Engage(e);
            e.EnterCombat();
        }
    }
}
