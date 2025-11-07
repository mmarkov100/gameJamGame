using System.Collections.Generic;
using UnityEngine;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance;

    public int maxActiveEnemies = 10;
    public int maxEngagedEnemies = 2;
    public float selectInterval = 3f;

    readonly List<EnemyAI> _all = new();
    readonly HashSet<EnemyAI> _engaged = new();

    // отложенные изменения (могут приходить из EnemyAI в любой момент)
    readonly List<EnemyAI> _pendingAdd = new();
    readonly List<EnemyAI> _pendingRemove = new();

    float _nextSelectTime;

    void Awake() => Instance = this;

    public void Register(EnemyAI enemy)
    {
        if (enemy == null) return;
        if (_all.Contains(enemy) || _pendingAdd.Contains(enemy)) return;
        _pendingAdd.Add(enemy);
    }

    public void Unregister(EnemyAI enemy)
    {
        if (enemy == null) return;
        _pendingRemove.Add(enemy); // снимем и из engaged тоже
    }

    public bool CanEngage(EnemyAI enemy) => !_engaged.Contains(enemy) && _engaged.Count < maxEngagedEnemies;

    public void Engage(EnemyAI enemy)
    {
        if (enemy != null) _engaged.Add(enemy);
    }

    public void Disengage(EnemyAI enemy)
    {
        if (enemy != null) _engaged.Remove(enemy);
    }

    public int TotalEnemies() => _all.Count;
    public int GetIndex(EnemyAI e) => _all.IndexOf(e);

    void Update()
    {
        // применяем отложенные изменения в начале кадра
        if (_pendingRemove.Count > 0)
        {
            foreach (var e in _pendingRemove)
            {
                _all.Remove(e);
                _engaged.Remove(e);
            }
            _pendingRemove.Clear();
        }
        if (_pendingAdd.Count > 0)
        {
            foreach (var e in _pendingAdd)
                if (e && !_all.Contains(e)) _all.Add(e);
            _pendingAdd.Clear();
        }

        // зачистка null/неактивных
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            var e = _all[i];
            if (e == null || !e.gameObject.activeInHierarchy)
            {
                _engaged.Remove(e);
                _all.RemoveAt(i);
            }
        }

        if (Time.time < _nextSelectTime) return;
        _nextSelectTime = Time.time + selectInterval;

        // просим текущих engaged уйти в «ожидание» — по снапшоту
        var engagedSnapshot = new EnemyAI[_engaged.Count];
        _engaged.CopyTo(engagedSnapshot);
        foreach (var e in engagedSnapshot)
            e?.LeaveCombatToStaging();
        _engaged.Clear();

        if (_all.Count == 0) return;

        // создаём отсортированный снимок по расстоянию
        var pool = _all.ToArray();
        System.Array.Sort(pool, (a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;
            float da = Vector3.SqrMagnitude(a.transform.position - a.Player.position);
            float db = Vector3.SqrMagnitude(b.transform.position - b.Player.position);
            return da.CompareTo(db);
        });

        int count = Mathf.Min(maxEngagedEnemies, pool.Length);
        int engaged = 0;
        for (int i = 0; i < pool.Length && engaged < count; i++)
        {
            var e = pool[i];
            if (e == null) continue;
            if (!e.CanRequestEngage()) continue;

            _engaged.Add(e);
            e.EnterCombat();
            engaged++;
        }
    }
}
