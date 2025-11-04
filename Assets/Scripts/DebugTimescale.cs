using UnityEngine;

public class DebugTimescale : MonoBehaviour
{
    public float slow = 0.2f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Time.timeScale = slow;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            Debug.Log("Slow-mo ON");
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            Debug.Log("Slow-mo OFF");
        }
    }
}
