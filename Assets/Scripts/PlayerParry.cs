using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerParry : MonoBehaviour
{
    [Header("Парирование")]
    public float parryWindow = 0.5f;     // окно для отражения
    public float parryRadius = 1.3f;     // радиус вокруг игрока
    public float stunDuration = 3f;      // длительность стана врага
    public float autoFaceTurnSpeed = 9999f;

    [Header("Кулдаун")]
    public float parryCooldown = 2f;     // КД отражения в секундах

    [Header("Riposte")]
    public float riposteWindow = 0.7f; // время на контратаку после успешного парри
    float riposteUntil;
    public bool InRiposteWindow => Time.time < riposteUntil;
    public Animator anim;

    float parryActiveUntil = -1f;
    float parryReadyTime = 0f;           // когда способность снова доступна

    void Awake() { if (!anim) anim = GetComponent<Animator>(); }
    void Update()
    {
        bool parryPressed =
            Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        // проверяем, не на КД ли способность
        if (parryPressed && Time.time >= parryReadyTime)
        {
            parryActiveUntil = Time.time + parryWindow;
            parryReadyTime = Time.time + parryCooldown; // запускаем КД

            // FX окно парирования
            HitFx.ShowRing(transform.position, parryRadius, new Color(0.2f, 1f, 1f), parryWindow);
            anim.SetTrigger("Deflect"); // телеграф анимации щита/стойки
        }
    }

    public bool TryParry(Transform enemy, out float stun)
    {
        stun = 0f;

        // окно парирования закрыто?
        if (Time.time > parryActiveUntil)
            return false;

        // проверяем дистанцию
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = enemy.position; b.y = 0;
        float dist = Vector3.Distance(a, b);
        if (dist > parryRadius) return false;

        // авто-поворот к врагу
        Vector3 dir = (b - a);
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, autoFaceTurnSpeed);
        }

        // FX успешного парирования
        HitFx.HitSpark(transform.position + Vector3.up * 0.8f, new Color(0.2f, 1f, 1f), 0.3f, 0.25f);

        stun = stunDuration;
        parryActiveUntil = -1f; // закрываем окно

        riposteUntil = Time.time + riposteWindow;

        return true;
    }
}
