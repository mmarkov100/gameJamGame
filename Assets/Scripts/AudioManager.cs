using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string PrefKey = "MasterVolume01";
    [Range(0f, 1f)] public float defaultVolume = 0.8f;

    float current01;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        current01 = PlayerPrefs.GetFloat(PrefKey, defaultVolume);
        Apply(current01);
    }

    public float GetVolume01() => current01;

    public void SetVolume01(float v01, bool save = true)
    {
        current01 = Mathf.Clamp01(v01);
        Apply(current01);
        if (save) PlayerPrefs.SetFloat(PrefKey, current01);
    }

    void Apply(float v01)
    {
        // Глобальная громкость для всех AudioSource
        AudioListener.volume = v01;
    }
}
