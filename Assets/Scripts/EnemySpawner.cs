using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Префаб")]
    public GameObject enemyPrefab;

    [Header("Зона спавна (две мировые точки)")]
    public Vector3 zoneA = new Vector3(-4, 0, 0);
    public Vector3 zoneB = new Vector3(4, 0, 0);

    [Header("Поддержание численности")]
    public int desiredOnScene = 10;
    public int totalRespawnLimit = 20;
    public float checkInterval = 5f;
    public bool spawnOnStart = true;

    [Header("Учёт врагов")]
    public string enemyTag = "Enemy";     // поставь этот тег на префаб

    [Header("Раскладка вдоль зоны")]
    public float sideEdgePadding = 0.5f;
    public float perSpawnMinSpacing = 1.0f;
    public float pointJitter = 0.3f;

    [Header("Привязка к поверхности/навмешу")]
    public bool snapToGround = true;
    public float raycastStartY = 50f;
    public float raycastDownDistance = 100f;
    public bool useNavMeshSample = true;
    public float navMeshSampleMaxDistance = 2f;

    [Header("Поворот при спавне")]
    public Transform faceTarget;      // необязательно
    public bool faceAlongSegment = false;

    [Header("Отладка")]
    public bool drawDebug = true;

    public int RemainingRespawns => Mathf.Max(0, totalRespawnLimit - _totalRespawned);

    float _nextCheckTime;
    int _totalRespawned;

    void OnEnable()
    {
        _totalRespawned = 0;
        _nextCheckTime = Time.time + checkInterval;

        if (spawnOnStart)
            SpawnImmediate(desiredOnScene); // сразу увидишь врагов
    }

    void Update()
    {
        if (!enemyPrefab) return;
        if (_totalRespawned >= totalRespawnLimit) return;

        if (Time.time < _nextCheckTime) return;
        _nextCheckTime = Time.time + checkInterval;

        int alive = CountAlive();
        int deficit = Mathf.Clamp(desiredOnScene - alive, 0, desiredOnScene);
        if (deficit <= 0) return;

        deficit = Mathf.Min(deficit, totalRespawnLimit - _totalRespawned);
        if (deficit <= 0) return;

        SpawnBatchOnLine(deficit);
    }

    public void SpawnImmediate(int amount)
    {
        if (!enemyPrefab || amount <= 0) return;
        amount = Mathf.Min(amount, totalRespawnLimit - _totalRespawned);
        if (amount <= 0) return;

        SpawnBatchOnLine(amount);
    }

    int CountAlive()
    {
        // Надёжный способ: по тегу (только активные в сцене)
        if (!string.IsNullOrEmpty(enemyTag))
            return GameObject.FindGameObjectsWithTag(enemyTag).Length;

        // запасной вариант
        return FindObjectsOfType<EnemyAI>().Length;
    }

    void SpawnBatchOnLine(int amount)
    {
        Vector3 a = zoneA; a.y = 0f;
        Vector3 b = zoneB; b.y = 0f;

        Vector3 dir = b - a;
        float fullLen = dir.magnitude;
        if (fullLen < 0.01f) return;

        // защитимся от слишком большого padding
        float maxPadding = Mathf.Max(0f, fullLen * 0.49f);
        float pad = Mathf.Clamp(sideEdgePadding, 0f, maxPadding);

        Vector3 dirNorm = dir / fullLen;
        float usableLen = Mathf.Max(0f, fullLen - pad * 2f);
        if (usableLen <= 0.01f) usableLen = 0.01f; // чтобы не отсекалось
        Vector3 segStart = a + dirNorm * pad;

        // равномерная раскладка
        List<Vector3> points = new(amount);
        if (amount == 1)
        {
            float t = Mathf.Clamp01(0.5f + Random.Range(-0.15f, 0.15f));
            points.Add(segStart + dirNorm * (usableLen * t));
        }
        else
        {
            float step = usableLen / amount;
            for (int i = 0; i < amount; i++)
            {
                float baseDist = step * (i + 0.5f);
                float jitter = Random.Range(-pointJitter, pointJitter);
                float pos = Mathf.Clamp(baseDist + jitter, 0f, usableLen);

                if (i > 0 && pos < (points.Count * step + perSpawnMinSpacing))
                    pos = points.Count * step + perSpawnMinSpacing;
                pos = Mathf.Min(pos, usableLen);

                points.Add(segStart + dirNorm * pos);
            }
        }

        foreach (var p in points)
        {
            Vector3 spawnPos = p;

            if (snapToGround)
                spawnPos = SnapToGround(spawnPos, out _);

            if (useNavMeshSample)
                spawnPos = SnapToNavMesh(spawnPos);

            SpawnEnemy(spawnPos);
            _totalRespawned++;
            if (_totalRespawned >= totalRespawnLimit) break;
        }
    }

    Vector3 SnapToGround(Vector3 pos, out bool hit)
    {
        Ray ray = new Ray(new Vector3(pos.x, raycastStartY, pos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit rh, raycastDownDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            hit = true;
            return rh.point;
        }
        hit = false; // пола без коллайдера? вернём, как есть
        return pos;
    }

    Vector3 SnapToNavMesh(Vector3 pos)
    {
        if (NavMesh.SamplePosition(pos, out var hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
            return hit.position;
        return pos;
    }

    void SpawnEnemy(Vector3 worldPos)
    {
        var go = Instantiate(enemyPrefab, worldPos, Quaternion.identity);

        if (faceTarget)
        {
            Vector3 to = faceTarget.position; to.y = worldPos.y;
            go.transform.rotation = Quaternion.LookRotation((to - worldPos).normalized, Vector3.up);
        }
        else if (faceAlongSegment)
        {
            Vector3 a = zoneA; a.y = worldPos.y;
            Vector3 b = zoneB; b.y = worldPos.y;
            Vector3 d = (b - a).normalized;
            if (d.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }

    [ContextMenu("Spawn Now")]
    void CtxSpawnNow() => SpawnImmediate(desiredOnScene);

    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;

        Vector3 a = zoneA; a.y = 0f;
        Vector3 b = zoneB; b.y = 0f;

        Gizmos.color = Color.green; Gizmos.DrawLine(a, b);

        Vector3 dir = (b - a);
        float len = dir.magnitude;
        if (len > 0.01f)
        {
            Vector3 n = dir / len;
            float pad = Mathf.Clamp(sideEdgePadding, 0f, len * 0.49f);
            Vector3 s = a + n * pad;
            Vector3 e = b - n * pad;
            Gizmos.color = new Color(0, 1, 1, 0.6f);
            Gizmos.DrawLine(s, e);
        }

        if (faceTarget)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(faceTarget.position.x, 0, faceTarget.position.z), 0.25f);
        }
    }
}
