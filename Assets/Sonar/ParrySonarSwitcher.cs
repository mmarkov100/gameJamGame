using System.Collections;
using UnityEngine;

/// <summary>
/// Подключите этот скрипт на объект с PlayerParry (или рядом),
/// назначьте ссылки и поставьте обработчик OnParrySuccess в инспекторе.
/// </summary>
public class ParrySonarSwitcher : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerParry parry;
    public Light roomLight;       // любой реальный свет (например, Directional или большой Point/Spot)
    public float lightIntensity = 2.5f;

    [Header("Длительность света после парирования")]
    public float parryLightTime = 2.0f;

    float baseLightIntensity;

    void Awake()
    {
        if (!parry) parry = GetComponent<PlayerParry>();
        if (roomLight)
        {
            baseLightIntensity = roomLight.intensity;
            roomLight.intensity = 0f;
            roomLight.enabled = true; // вкл, но интенсивность 0
        }
    }

    // В инспекторе добавьте этот метод в PlayerParry.OnParrySuccess
    public void OnParrySuccessHandler()
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(BurstCo());
    }


    IEnumerator BurstCo()
    {
        var sonar = SonarController.Instance;
        sonar?.PauseForSeconds(parryLightTime);

        // Включаем свет
        if (roomLight)
            roomLight.intensity = lightIntensity;

        yield return new WaitForSeconds(parryLightTime);

        // Гасим свет
        if (roomLight)
            roomLight.intensity = baseLightIntensity;
    }
}
