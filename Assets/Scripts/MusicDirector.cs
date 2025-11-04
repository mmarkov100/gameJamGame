using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

public class MusicDirector : MonoBehaviour
{
    public static MusicDirector Instance { get; private set; }

    [Header("Треки Wave 1..4")]
    public AudioClip wave1, wave2, wave3, wave4;

    AudioSource src;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        src = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene s, LoadSceneMode m) => AdoptForActiveScene();

    /// <summary>Выбрать/оставить корректный трек для активной сцены.</summary>
    public void AdoptForActiveScene()
    {
        var name = SceneManager.GetActiveScene().name; // "Wave 1" / "Lore 1" / ...

        if (TryParseIndex(name, out bool isWave, out int idx) && isWave)
        {
            var clip = GetClipForWave(idx);
            if (clip && src.clip != clip)
            {
                src.clip = clip;
                src.Play();
            }
        }
        // На Lore_N — ничего не меняем: музыка тянется с Wave_N.
    }

    AudioClip GetClipForWave(int i) => i switch
    {
        1 => wave1,
        2 => wave2,
        3 => wave3,
        4 => wave4,
        _ => null
    };

    /// <summary>
    /// Парсит имена "Wave 1", "Lore 1" (а также допускает "Wave_1"/"Lore_1").
    /// </summary>
    bool TryParseIndex(string sceneName, out bool isWave, out int index)
    {
        isWave = false; index = 0;
        if (string.IsNullOrEmpty(sceneName)) return false;

        // ^(Wave|Lore)\s*_?(\d+)$ — слово Wave/Lore, пробел или _, число
        var m = Regex.Match(sceneName, @"^(Wave|Lore)\s*_?(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success) return false;

        isWave = m.Groups[1].Value.ToLower() == "wave";
        return int.TryParse(m.Groups[2].Value, out index);
    }
}
