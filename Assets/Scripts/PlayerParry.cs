using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.Events;


public class PlayerParry : MonoBehaviour
{
    [Header("Парирование")]
    public float parryWindow = 0.5f;     // окно для отражения
    public float parryRadius = 1.3f;     // радиус вокруг игрока
    public float stunDuration = 3f;      // длительность стана врага
    public float autoFaceTurnSpeed = 9999f;
    public float parryCooldown = 2f;     // КД отражения в секундах
    public float postImpactGrace = 0.06f; // 60 мс «позднего» парри

    float lastParryPressTime = -999f;
    public Animator anim;
    public UnityEvent OnParrySuccess;

    float parryReadyTime = 0f;           // когда способность снова доступна

    void Awake() { anim = GetComponent<Animator>(); }
    void Update()
    {
        bool parryPressed =
#if ENABLE_INPUT_SYSTEM
            Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        Input.GetMouseButtonDown(1);
#endif

        if (parryPressed && Time.time >= parryReadyTime)
        {
            lastParryPressTime = Time.time;              // запоминаем момент нажатия
            parryReadyTime = Time.time + parryCooldown;

            if (anim) anim.SetTrigger("Deflect");
        }
    }

    public bool TryParry(Transform enemy, out float stun)
    {
        stun = 0f;

        // нажатие должно быть не раньше, чем за parryWindow до удара,
        // и не позже, чем postImpactGrace после удара
        float dt = Time.time - lastParryPressTime;
        bool inTime = (dt >= 0f && dt <= parryWindow) || (dt < 0f && -dt <= postImpactGrace);
        if (!inTime) return false;

        // дистанция
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = enemy.position; b.y = 0;
        if (Vector3.Distance(a, b) > parryRadius) return false;

        // авто-фейс
        Vector3 dir = (b - a);
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, autoFaceTurnSpeed);
        }

        stun = stunDuration;

        OnParrySuccess?.Invoke();
        return true;
    }
}
