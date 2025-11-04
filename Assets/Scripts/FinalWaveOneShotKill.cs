using System;
using System.Reflection;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FinalWaveOneShotKill : MonoBehaviour
{
    [Header("Время до уязвимости")]
    public float surviveSeconds = 30f;

    [Header("Звук при включении уязвимости")]
    public AudioSource sfx;           // можно не указывать — создастся временный
    public AudioClip vulnerableSfx;   // поставь сюда клип

    [Header("Логи в консоль")]
    public bool verboseLogs = true;

    // --- авто-детект здоровья ---
    Component _healthComp;
    FieldInfo _currentHpField, _hpField, _healthField, _maxHpField, _maxHealthField;
    PropertyInfo _currentHpProp, _hpProp, _healthProp, _maxHpProp, _maxHealthProp;
    MethodInfo _setHealthMethod; // SetHealth(int) если вдруг есть

    void Awake()
    {
        // попробуем найти любой "health-like" компонент на объекте или выше
        _healthComp = GetHealthComponentOn(gameObject) ?? GetHealthComponentOn(transform.root.gameObject);
        if (verboseLogs)
        {
            Debug.Log(_healthComp
                ? $"[OneShotKill] Найдён компонент здоровья: {_healthComp.GetType().Name}"
                : "[OneShotKill] Компонент здоровья не найден. Попробуем выставлять через популярные названия полей, если появится позже.", this);
        }

        if (_healthComp)
            CacheMembers(_healthComp);
    }

    void OnEnable()
    {
        StartCoroutine(TimerRoutine());
    }

    IEnumerator TimerRoutine()
    {
        yield return new WaitForSeconds(surviveSeconds);
        MakeOneShotKill();
        PlaySfx();
    }

    void PlaySfx()
    {
        if (!vulnerableSfx) return;

        if (!sfx)
        {
            sfx = gameObject.GetComponent<AudioSource>();
            if (!sfx) sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 1f;
        }

        sfx.PlayOneShot(vulnerableSfx);
    }

    void MakeOneShotKill()
    {
        // если вдруг к этому моменту здоровье добавили/заменили — попытка найти ещё раз
        if (!_healthComp)
        {
            _healthComp = GetHealthComponentOn(gameObject) ?? GetHealthComponentOn(transform.root.gameObject);
            if (_healthComp) CacheMembers(_healthComp);
        }

        if (_healthComp == null)
        {
            Debug.LogWarning("[OneShotKill] Не нашёл компонент здоровья. " +
                             "Если у врага есть свой Health/EnemyHealth/HP — повесь этот скрипт на тот же объект или укажи ссылку вручную.", this);
            return;
        }

        try
        {
            // 1) явный SetHealth(int)
            if (_setHealthMethod != null)
            {
                _setHealthMethod.Invoke(_healthComp, new object[] { 1 });
                if (verboseLogs) Debug.Log("[OneShotKill] Установлено через SetHealth(1).", this);
                AlignMaxToOne();
                return;
            }

            // 2) property: CurrentHP / HP / Health
            if (TrySetProp(_currentHpProp, 1) || TrySetProp(_hpProp, 1) || TrySetProp(_healthProp, 1))
            {
                if (verboseLogs) Debug.Log("[OneShotKill] Установлено через property (HP/Health=1).", this);
                AlignMaxToOne();
                return;
            }

            // 3) field: currentHealth / health / hp
            if (TrySetField(_currentHpField, 1) || TrySetField(_healthField, 1) || TrySetField(_hpField, 1))
            {
                if (verboseLogs) Debug.Log("[OneShotKill] Установлено через field (hp/health=1).", this);
                AlignMaxToOne();
                return;
            }

            Debug.LogWarning("[OneShotKill] Не удалось выставить HP=1 — не нашёл подходящих полей/свойств.", this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OneShotKill] Ошибка при выставлении HP=1: {e.Message}", this);
        }
    }

    void AlignMaxToOne()
    {
        // если есть maxHealth/maxHp — тоже поставим 1 (не обязательно)
        bool set = false;
        set |= TrySetProp(_maxHpProp, 1);
        set |= TrySetProp(_maxHealthProp, 1);
        set |= TrySetField(_maxHpField, 1);
        set |= TrySetField(_maxHealthField, 1);
        if (verboseLogs && set) Debug.Log("[OneShotKill] Максимальное здоровье тоже поставлено = 1.", this);
    }

    // ---------- helpers ----------
    Component GetHealthComponentOn(GameObject go)
    {
        // самые частые названия компонентов
        var names = new[] { "EnemyHealth", "Health", "HP", "CharacterHealth", "Damageable", "Life", "BossHealth" };
        foreach (var n in names)
        {
            var c = go.GetComponent(n);
            if (c) return c;
        }
        // если у тебя интерфейс/базовый класс — можно добавить здесь поиск по типу
        return null;
    }

    void CacheMembers(Component c)
    {
        var t = c.GetType();
        BindingFlags B = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        _setHealthMethod = t.GetMethod("SetHealth", B, null, new[] { typeof(int) }, null);

        _currentHpProp = t.GetProperty("CurrentHP", B) ?? t.GetProperty("currentHP", B);
        _hpProp = t.GetProperty("HP", B);
        _healthProp = t.GetProperty("Health", B) ?? t.GetProperty("health", B);

        _currentHpField = t.GetField("currentHP", B) ?? t.GetField("currentHealth", B);
        _hpField = t.GetField("hp", B);
        _healthField = t.GetField("health", B);

        _maxHpProp = t.GetProperty("MaxHP", B) ?? t.GetProperty("maxHP", B);
        _maxHealthProp = t.GetProperty("MaxHealth", B) ?? t.GetProperty("maxHealth", B);
        _maxHpField = t.GetField("maxHP", B);
        _maxHealthField = t.GetField("maxHealth", B);
    }

    bool TrySetProp(PropertyInfo p, int value)
    {
        if (p == null || !p.CanWrite) return false;
        var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (type == typeof(int)) { p.SetValue(_healthComp, value); return true; }
        if (type == typeof(float)) { p.SetValue(_healthComp, (float)value); return true; }
        return false;
    }

    bool TrySetField(FieldInfo f, int value)
    {
        if (f == null) return false;
        var type = Nullable.GetUnderlyingType(f.FieldType) ?? f.FieldType;
        if (type == typeof(int)) { f.SetValue(_healthComp, value); return true; }
        if (type == typeof(float)) { f.SetValue(_healthComp, (float)value); return true; }
        return false;
    }
}
