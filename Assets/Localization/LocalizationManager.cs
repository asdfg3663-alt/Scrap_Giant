using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    English,
    Korean,
    Japanese,
    Spanish,
    Russian,
    SimplifiedChinese
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
    public string zhHans;
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
            GameLanguage.Russian,
            GameLanguage.SimplifiedChinese
        };
    }

    public static string GetLanguageLabel(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Korean => Get("language.korean", "Korean"),
            GameLanguage.Japanese => Get("language.japanese", "Japanese"),
            GameLanguage.Spanish => Get("language.spanish", "Spanish"),
            GameLanguage.Russian => Get("language.russian", "Russian"),
            GameLanguage.SimplifiedChinese => Get("language.simplified_chinese", "Simplified Chinese"),
            _ => Get("language.english", "English")
        };
    }

    public static string GetNativeLanguageLabel(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Korean => "\uD55C\uAD6D\uC5B4",
            GameLanguage.Japanese => "\u65E5\u672C\u8A9E",
            GameLanguage.Spanish => "Espa\u00F1ol",
            GameLanguage.Russian => "\u0420\u0443\u0441\u0441\u043A\u0438\u0439",
            GameLanguage.SimplifiedChinese => "\u7B80\u4F53\u4E2D\u6587",
            _ => "English"
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

        string systemLanguageName = Application.systemLanguage.ToString();
        if (string.Equals(systemLanguageName, "ChineseSimplified", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(systemLanguageName, "ChineseTraditional", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(systemLanguageName, "Chinese", StringComparison.OrdinalIgnoreCase))
        {
            return GameLanguage.SimplifiedChinese;
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
            GameLanguage.SimplifiedChinese => entry.zhHans,
            _ => entry.en
        };
    }
}
