using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    English,
    Korean,
    Japanese,
    Spanish,
    Russian
}

[Serializable]
public sealed class LocalizationEntryData
{
    public string key;
    public string en;
    public string ko;
    public string ja;
    public string es;
    public string ru;
}

[Serializable]
public sealed class LocalizationTableData
{
    public LocalizationEntryData[] entries;
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class LocalizationManager : MonoBehaviour
{
    const string LanguagePrefsKey = "ScrapGiant.Localization.Language";
    const string ResourcePath = "Localization/LocalizationDatabase";

    static LocalizationManager instance;

    readonly Dictionary<string, LocalizationEntryData> entries = new(StringComparer.OrdinalIgnoreCase);

    GameLanguage currentLanguage;
    bool initialized;

    public static event Action<GameLanguage> LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameLanguage CurrentLanguage => EnsureInstance().currentLanguage;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeIfNeeded();
    }

    public static LocalizationManager EnsureInstance()
    {
        if (instance == null)
            instance = FindFirstObjectByType<LocalizationManager>();

        if (instance == null)
        {
            var go = new GameObject("LocalizationManager");
            instance = go.AddComponent<LocalizationManager>();
        }

        instance.InitializeIfNeeded();
        return instance;
    }

    public static IReadOnlyList<GameLanguage> GetSupportedLanguages()
    {
        return new[]
        {
            GameLanguage.English,
            GameLanguage.Korean,
            GameLanguage.Japanese,
            GameLanguage.Spanish,
            GameLanguage.Russian
        };
    }

    public static string GetLanguageLabel(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Korean => Get("language.korean", "한국어"),
            GameLanguage.Japanese => Get("language.japanese", "日本語"),
            GameLanguage.Spanish => Get("language.spanish", "Español"),
            GameLanguage.Russian => Get("language.russian", "Русский"),
            _ => Get("language.english", "English")
        };
    }

    public static bool HasSavedLanguagePreference()
    {
        return PlayerPrefs.HasKey(LanguagePrefsKey);
    }

    public static void SetLanguage(GameLanguage language, bool savePreference = true)
    {
        var mgr = EnsureInstance();
        if (mgr.currentLanguage == language && mgr.initialized)
            return;

        mgr.currentLanguage = language;

        if (savePreference)
        {
            PlayerPrefs.SetString(LanguagePrefsKey, language.ToString());
            PlayerPrefs.Save();
        }

        LanguageChanged?.Invoke(language);
    }

    public static string Get(string key, string fallback = null)
    {
        return EnsureInstance().GetInternal(key, fallback);
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        string format = Get(key, fallback);
        return args == null || args.Length == 0 ? format : string.Format(format, args);
    }

    public static string GetModuleText(string localizationKey, string fallbackName)
    {
        string fallback = ModuleInstance.FormatDisplayName(fallbackName, 1);
        if (string.IsNullOrWhiteSpace(localizationKey))
            return fallback;

        return Get(localizationKey, fallback);
    }

    void InitializeIfNeeded()
    {
        if (initialized)
            return;

        LoadDatabase();
        currentLanguage = LoadSavedOrSystemLanguage();
        initialized = true;
    }

    void LoadDatabase()
    {
        entries.Clear();

        TextAsset textAsset = Resources.Load<TextAsset>(ResourcePath);
        if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            return;

        LocalizationTableData table = JsonUtility.FromJson<LocalizationTableData>(textAsset.text);
        if (table?.entries == null)
            return;

        for (int i = 0; i < table.entries.Length; i++)
        {
            LocalizationEntryData entry = table.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;

            entries[entry.key] = entry;
        }
    }

    GameLanguage LoadSavedOrSystemLanguage()
    {
        if (PlayerPrefs.HasKey(LanguagePrefsKey))
        {
            string raw = PlayerPrefs.GetString(LanguagePrefsKey, GameLanguage.English.ToString());
            if (Enum.TryParse(raw, true, out GameLanguage saved))
                return saved;
        }

        return Application.systemLanguage switch
        {
            SystemLanguage.Korean => GameLanguage.Korean,
            SystemLanguage.Japanese => GameLanguage.Japanese,
            SystemLanguage.Spanish => GameLanguage.Spanish,
            SystemLanguage.Russian => GameLanguage.Russian,
            _ => GameLanguage.English
        };
    }

    string GetInternal(string key, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(key) && entries.TryGetValue(key, out LocalizationEntryData entry))
        {
            string localized = GetTextForLanguage(entry, currentLanguage);
            if (!string.IsNullOrWhiteSpace(localized))
                return localized;

            if (!string.IsNullOrWhiteSpace(entry.en))
                return entry.en;
        }

        return string.IsNullOrWhiteSpace(fallback) ? key : fallback;
    }

    static string GetTextForLanguage(LocalizationEntryData entry, GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Korean => entry.ko,
            GameLanguage.Japanese => entry.ja,
            GameLanguage.Spanish => entry.es,
            GameLanguage.Russian => entry.ru,
            _ => entry.en
        };
    }
}
