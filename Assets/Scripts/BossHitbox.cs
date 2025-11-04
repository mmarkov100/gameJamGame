//using UnityEngine;

//[RequireComponent(typeof(Collider))]
//public class BossHitbox : MonoBehaviour
//{
//    [Header("Ќастройки")]
//    public LayerMask targetMask;          // слой игрока
//    [Range(0f, 1f)] public float frontDot = 0.25f; // допуск по фронту (на вс€кий)

//    [Header("—в€зи")]
//    public FinalBoss owner;               // назначь в инспекторе
//    public Transform ownerRoot;           // трансформ босса (если null Ч возьмЄм owner.transform)

//    // ---- runtime ----
//    bool windowOpen;
//    bool consumed;
//    float windowUntil;

//    Collider col;

//    void Awake()
//    {
//        col = GetComponent<Collider>();
//        col.isTrigger = true;
//        if (!ownerRoot && owner) ownerRoot = owner.transform;
//        gameObject.SetActive(false); // по умолчанию выключен
//    }

//    void Update()
//    {
//        if (!windowOpen) return;

//        if (Time.time > windowUntil)
//        {
//            CloseWindow();
//        }
//    }

//    public void OpenWindow(float seconds)
//    {
//        consumed = false;
//        windowOpen = true;
//        windowUntil = Time.time + seconds;
//        gameObject.SetActive(true);
//    }

//    public void CloseWindow()
//    {
//        windowOpen = false;
//        gameObject.SetActive(false);
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        TryHit(other);
//    }

//    void OnTriggerStay(Collider other)
//    {
//        // на случай, если юнит вошЄл до открыти€ Ч поймаем кадром позже
//        TryHit(other);
//    }

//    void TryHit(Collider other)
//    {
//        if (!windowOpen || consumed || owner == null) return;

//        // маска
//        if (targetMask.value != 0 && (targetMask.value & (1 << other.gameObject.layer)) == 0) return;

//        // ищем корень игрока Ч сначала сам коллайдер, потом его родители
//        Transform t = other.transform;

//        // фронт (подстраховка Ч триггер и так перед боссом)
//        Vector3 to = (t.position - (ownerRoot ? ownerRoot.position : transform.position));
//        to.y = 0f;
//        if (to.sqrMagnitude < 0.0001f) return;
//        to.Normalize();
//        if (Vector3.Dot((ownerRoot ? ownerRoot.forward : transform.forward), to) < frontDot) return;

//        // ѕарри Ч как у Enemy: сначала шанс парировани€
//        if (t.TryGetComponent<PlayerParry>(out var parry) || t.GetComponentInParent<PlayerParry>(out parry))
//        {
//            if (parry.TryParry(owner.transform, out float stunSeconds))
//            {
//                consumed = true;
//                owner.OnParriedFromHitbox(stunSeconds);
//                CloseWindow();
//                return;
//            }
//        }

//        // ”рон игроку
//        if (t.TryGetComponent<IPlayerHealth>(out var hp) || t.GetComponentInParent<IPlayerHealth>(out hp))
//        {
//            consumed = true;
//            owner.OnHitConfirmedFromHitbox(hp);
//            CloseWindow();
//        }
//        else if (t.TryGetComponent<PlayerHealth>(out var hp2) || t.GetComponentInParent<PlayerHealth>(out hp2))
//        {
//            consumed = true;
//            owner.OnHitConfirmedFromHitbox(hp2);
//            CloseWindow();
//        }
//    }
//}
