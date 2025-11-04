using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

public class ReloadSceneOnDeath : MonoBehaviour
{
    [Tooltip("Задержка перед перезагрузкой (реальное время, не зависит от timeScale)")]
    public float delay = 0.5f;

    [Tooltip("Вызывается перед перезагрузкой (для SFX/VFX/экрана смерти)")]
    public UnityEvent onBeforeReload;

    // Повесь этот метод в UnityEvent смерти игрока (или вызови из своего PlayerHealth)
    public void OnPlayerDeath()
    {
        StartCoroutine(ReloadRoutine());
    }

    IEnumerator ReloadRoutine()
    {
        onBeforeReload?.Invoke();
        // возвращаем нормальную скорость, чтобы задержка отработала предсказуемо
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(delay);

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
