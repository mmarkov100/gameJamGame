using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("ѕрефабы")]
    public GameObject enemyPrefab;             // префаб "Enemy" (с EnemyAI + NavMeshAgent)

    [Header("јрена (XZ) Ч две противоположные угловые точки")]
    public Vector2 arenaMin = new Vector2(-21f, -16f); // (xMin, zMin)
    public Vector2 arenaMax = new Vector2(21f, 16f); // (xMax, zMax)

    [Header("ѕараметры респавна")]
    public int desiredOnArena = 10;            // целевое число врагов на арене
    public float checkInterval = 5f;           // раз в N секунд провер€ть и доспавнивать
    public int totalRespawnLimit = 20;         // общий лимит респавнов (штук)
    public float outsideOffset = 1.5f;         // насколько наружу от кра€ арены спавним
    public float sideEdgePadding = 1.0f;       // отступ от углов по стороне
    public float perSpawnMinSpacing = 1.0f;    // мин. интервал между точками по стороне
    public float pointJitter = 0.5f;           // случайный джиттер вдоль стороны

    [Header("Ќавмеш")]
    public bool useNavMeshSample = true;       // пытатьс€ прилипнуть к навмешу
    public float navMeshSampleMaxDistance = 2.0f; // радиус поиска точки навмеша
    public int navMeshAgentTypeId = 0;         // тип агента (0 Ч Humanoid по умолчанию)

    [Header("¬ысота спавна")]
    public float raycastStartY = 50f;          // высота, с которой пускаем луч вниз, чтобы найти пол
    public float raycastDownDistance = 100f;   // длина луча вниз

    [Header("ќтладка")]
    public bool drawDebug = true;

    enum Side { North, East, South, West }
    Side nextSide = Side.North;

    float nextCheckTime;
    int totalRespawned;

    void OnEnable()
    {
        nextCheckTime = Time.time + checkInterval;
    }

    void Update()
    {
        if (enemyPrefab == null) return;
        if (totalRespawned >= totalRespawnLimit) return;

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        int aliveInside = CountEnemiesInsideArena();
        int deficit = Mathf.Clamp(desiredOnArena - aliveInside, 0, desiredOnArena);

        if (deficit <= 0) return;

        // не превышаем лимит общего респавна
        deficit = Mathf.Min(deficit, totalRespawnLimit - totalRespawned);
        if (deficit <= 0) return;

        // спавним "пакет" с текущей стороны
        SpawnBatchOnSide(nextSide, deficit);

        // следующа€ сторона по кругу
        nextSide = (Side)(((int)nextSide + 1) % 4);
    }

    // ---------- ѕодсчЄт врагов внутри арены ----------
    int CountEnemiesInsideArena()
    {
        // Ћибо через CombatDirector (если хочешь):
        // var enemies = FindObjectsOfType<EnemyAI>(); // достаточно дл€ дес€тков
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        int count = 0;
        float xmin = Mathf.Min(arenaMin.x, arenaMax.x);
        float xmax = Mathf.Max(arenaMin.x, arenaMax.x);
        float zmin = Mathf.Min(arenaMin.y, arenaMax.y);
        float zmax = Mathf.Max(arenaMin.y, arenaMax.y);

        foreach (var e in enemies)
        {
            if (!e.isActiveAndEnabled) continue;
            Vector3 p = e.transform.position;
            if (p.x >= xmin && p.x <= xmax && p.z >= zmin && p.z <= zmax)
                count++;
        }
        return count;
    }

    // ---------- —павн "пакета" на стороне ----------
    void SpawnBatchOnSide(Side side, int amount)
    {
        // вычисл€ем параметры стороны (начало/конец отрезка), и смещение наружу
        GetSideSegment(side, out Vector3 a, out Vector3 b, out Vector3 outward);

        // длина рабочей части сегмента с учЄтом отступов
        Vector3 dir = (b - a);
        float fullLen = dir.magnitude;
        if (fullLen < 0.01f) return;

        Vector3 dirNorm = dir / fullLen;

        float usableLen = Mathf.Max(0f, fullLen - sideEdgePadding * 2f);
        if (usableLen <= 0.1f) return;

        Vector3 segStart = a + dirNorm * sideEdgePadding;

        // равномерна€ раскладка + джиттер + минимальный интервал
        List<Vector3> points = new List<Vector3>(amount);
        if (amount == 1)
        {
            float t = 0.5f + Random.Range(-0.15f, 0.15f);
            t = Mathf.Clamp01(t);
            points.Add(segStart + dirNorm * (usableLen * t));
        }
        else
        {
            float step = usableLen / amount;
            for (int i = 0; i < amount; i++)
            {
                float baseDist = step * (i + 0.5f); // центр €чейки
                float jitter = Random.Range(-pointJitter, pointJitter);
                float pos = Mathf.Clamp(baseDist + jitter, 0f, usableLen);

                // обеспечим минимальный интервал
                if (i > 0 && pos < (points.Count * step + perSpawnMinSpacing))
                    pos = points.Count * step + perSpawnMinSpacing;
                pos = Mathf.Min(pos, usableLen);

                points.Add(segStart + dirNorm * pos);
            }
        }

        // спавним в каждой точке (снаружи от арены на offset)
        foreach (var pOnEdge in points)
        {
            Vector3 spawnPos = pOnEdge + outward * outsideOffset;

            // приземл€ем на пол (луч вниз)
            spawnPos = SnapToGround(spawnPos, out bool hitGround);

            // опционально Ч подправл€ем по навмешу
            if (useNavMeshSample)
                spawnPos = SnapToNavMesh(spawnPos);

            // финальный инстанс
            SpawnEnemy(spawnPos);
            totalRespawned++;
            if (totalRespawned >= totalRespawnLimit) break;
        }
    }

    // ---------- √еометри€ стороны ----------
    void GetSideSegment(Side side, out Vector3 a, out Vector3 b, out Vector3 outward)
    {
        float xmin = Mathf.Min(arenaMin.x, arenaMax.x);
        float xmax = Mathf.Max(arenaMin.x, arenaMax.x);
        float zmin = Mathf.Min(arenaMin.y, arenaMax.y);
        float zmax = Mathf.Max(arenaMin.y, arenaMax.y);

        switch (side)
        {
            case Side.North: // верхн€€ сторона по Z
                a = new Vector3(xmin, 0, zmax);
                b = new Vector3(xmax, 0, zmax);
                outward = Vector3.forward; // наружу Ч +Z
                break;

            case Side.East: // права€ сторона по X
                a = new Vector3(xmax, 0, zmin);
                b = new Vector3(xmax, 0, zmax);
                outward = Vector3.right; // наружу Ч +X
                break;

            case Side.South: // нижн€€ сторона по Z
                a = new Vector3(xmax, 0, zmin);
                b = new Vector3(xmin, 0, zmin);
                outward = Vector3.back; // наружу Ч -Z
                break;

            default: // West Ч лева€ сторона по X
                a = new Vector3(xmin, 0, zmax);
                b = new Vector3(xmin, 0, zmin);
                outward = Vector3.left; // наружу Ч -X
                break;
        }
    }

    // ---------- ѕрив€зка к земле ----------
    Vector3 SnapToGround(Vector3 pos, out bool hit)
    {
        Ray ray = new Ray(new Vector3(pos.x, raycastStartY, pos.z), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit rh, raycastDownDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            hit = true;
            return rh.point;
        }
        hit = false;
        return pos;
    }

    // ---------- ѕрив€зка к навмешу ----------
    Vector3 SnapToNavMesh(Vector3 pos)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(pos, out hit, navMeshSampleMaxDistance, NavMesh.AllAreas))
            return hit.position;
        return pos;
    }

    // ---------- »нстанс ----------
    void SpawnEnemy(Vector3 worldPos)
    {
        var go = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
        // при желании: повернуть лицом к центру арены
        Vector3 center = new Vector3((arenaMin.x + arenaMax.x) * 0.5f, worldPos.y, (arenaMin.y + arenaMax.y) * 0.5f);
        go.transform.rotation = Quaternion.LookRotation((center - worldPos).normalized, Vector3.up);
    }

    // ---------- √измо ----------
    void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;

        float xmin = Mathf.Min(arenaMin.x, arenaMax.x);
        float xmax = Mathf.Max(arenaMin.x, arenaMax.x);
        float zmin = Mathf.Min(arenaMin.y, arenaMax.y);
        float zmax = Mathf.Max(arenaMin.y, arenaMax.y);

        // пр€моугольник арены
        Gizmos.color = Color.green;
        Vector3 p1 = new Vector3(xmin, 0, zmin);
        Vector3 p2 = new Vector3(xmin, 0, zmax);
        Vector3 p3 = new Vector3(xmax, 0, zmax);
        Vector3 p4 = new Vector3(xmax, 0, zmin);
        Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p4); Gizmos.DrawLine(p4, p1);

        // линии Ђвнешнегої офсета
        Gizmos.color = new Color(1, 0.6f, 0, 0.8f);
        Gizmos.DrawLine(p2 + Vector3.forward * outsideOffset, p3 + Vector3.forward * outsideOffset); // North
        Gizmos.DrawLine(p3 + Vector3.right * outsideOffset, p4 + Vector3.right * outsideOffset); // East
        Gizmos.DrawLine(p4 + Vector3.back * outsideOffset, p1 + Vector3.back * outsideOffset); // South
        Gizmos.DrawLine(p1 + Vector3.left * outsideOffset, p2 + Vector3.left * outsideOffset); // West
    }
}
