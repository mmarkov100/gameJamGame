using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip(" омпонент со здоровьем игрока (любой класс, где есть currentHP и maxHP).")]
    public MonoBehaviour playerHealthComponent;
    [Tooltip("»конка/картинка HP.")]
    public Image hpIcon;
    [Tooltip("“екстовое поле дл€ числа HP.")]
    public TextMeshProUGUI hpText;

    [Header("Formatting")]
    public bool roundToInt = true;
    public bool showMax = false;      // 75/100 или просто 75
    public string numberPrefix = "";  // например, "HP "
    public string numberSuffix = "";  // например, ""

    // кеш рефлексии под любые имена полей/свойств
    System.Reflection.FieldInfo fCurrent, fMax;
    System.Reflection.PropertyInfo pCurrent, pMax;

    void Awake()
    {
        if (!hpText) Debug.LogWarning("PlayerHealthUI: hpText не назначен");
        if (!hpIcon) Debug.LogWarning("PlayerHealthUI: hpIcon не назначен");

        // попытаемс€ найти пол€/свойства по распространЄнным именам
        if (playerHealthComponent != null)
        {
            var t = playerHealthComponent.GetType();
            fCurrent = t.GetField("currentHP") ?? t.GetField("CurrentHP");
            fMax = t.GetField("maxHP") ?? t.GetField("MaxHP");
            pCurrent = t.GetProperty("currentHP") ?? t.GetProperty("CurrentHP");
            pMax = t.GetProperty("maxHP") ?? t.GetProperty("MaxHP");
        }
    }

    void Update()
    {
        if (!playerHealthComponent || !hpText) return;

        float cur = GetValue(playerHealthComponent, fCurrent, pCurrent);
        float max = Mathf.Max(1f, GetValue(playerHealthComponent, fMax, pMax)); // защита от 0

        string curStr = roundToInt ? Mathf.RoundToInt(cur).ToString() : cur.ToString("0.#");
        string maxStr = roundToInt ? Mathf.RoundToInt(max).ToString() : max.ToString("0.#");

        hpText.text = showMax ? $"{numberPrefix}{curStr}/{maxStr}{numberSuffix}"
                              : $"{numberPrefix}{curStr}{numberSuffix}";
    }

    float GetValue(object obj, System.Reflection.FieldInfo f, System.Reflection.PropertyInfo p)
    {
        if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
        if (p != null && p.PropertyType == typeof(float)) return (float)p.GetValue(obj, null);

        // ѕопробуем int (часто хранитс€ как int)
        if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(obj);
        if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(obj, null);

        return 0f;
    }
}
