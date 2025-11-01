using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Скорости")]
    public float moveSpeed = 5f;     // м/с вперёд/назад
    public float turnSpeed = 180f;   // °/с поворот влево/вправо
    public float acceleration = 30f; // разгон/торможение по продольной оси

    Rigidbody rb;

    // буфер ввода, читаем в Update, применяем в FixedUpdate
    float hInput, vInput;
    float currentForwardSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // В инспекторе также заморозьте вращение по X и Z, чтобы не заваливался.
    }

    void Update()
    {
        // --- Ввод ---
        var k = Keyboard.current;
        if (k != null)
        {
            hInput = (k.aKey.isPressed ? -1f : 0f) + (k.dKey.isPressed ? 1f : 0f);
            vInput = (k.sKey.isPressed ? -1f : 0f) + (k.wKey.isPressed ? 1f : 0f);
        }
    }

    void FixedUpdate()
    {
        // 1) Поворот вокруг себя
        float yawDelta = hInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawDelta, 0f));

        // 2) Продольная скорость (с плавным разгоном)
        float targetSpeed = vInput * moveSpeed;
        currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        // 3) Шаг вперёд/назад по направлению объекта
        Vector3 moveStep = transform.forward * currentForwardSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveStep);
    }
}
