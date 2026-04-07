using UnityEngine;

public static class RewardedQuitAdService
{
    const string SettingsResourcePath = "Monetization/TitleInterstitialAdSettings";
    const string AndroidTestRewardedId = "ca-app-pub-3940256099942544/5224354917";
    const string IosTestRewardedId = "ca-app-pub-3940256099942544/1712485313";

    static TitleInterstitialAdSettingsData settings;
    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeOnLoad()
    {
        EnsureInitialized();
    }

    public static void ShowSaveQuitAd(System.Action<bool> onComplete)
    {
        EnsureInitialized();

        if (!IsRewardedEnabled())
        {
#if UNITY_EDITOR
            onComplete?.Invoke(true);
#else
            onComplete?.Invoke(false);
#endif
            return;
        }

        if (!AdMobRewardedBridge.IsRewardedReady)
        {
            AdMobRewardedBridge.LoadRewarded(GetActiveRewardedAdUnitId());
            onComplete?.Invoke(false);
            return;
        }

        AdMobRewardedBridge.ShowRewarded(onComplete);
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        settings = LoadSettings();

        if (!IsAdsSupportedOnCurrentPlatform() || !IsRewardedEnabled())
            return;

        AdMobRewardedBridge.Initialize();
        AdMobRewardedBridge.LoadRewarded(GetActiveRewardedAdUnitId());
    }

    static TitleInterstitialAdSettingsData LoadSettings()
    {
        TextAsset settingsAsset = Resources.Load<TextAsset>(SettingsResourcePath);
        if (settingsAsset == null || string.IsNullOrWhiteSpace(settingsAsset.text))
            return new TitleInterstitialAdSettingsData();

        try
        {
            TitleInterstitialAdSettingsData loaded = JsonUtility.FromJson<TitleInterstitialAdSettingsData>(settingsAsset.text);
            return loaded ?? new TitleInterstitialAdSettingsData();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to parse rewarded AdMob settings JSON: {ex.Message}");
            return new TitleInterstitialAdSettingsData();
        }
    }

    static bool IsRewardedEnabled()
    {
        return settings != null && settings.enabled && settings.showOnRewardedSaveQuit;
    }

    static bool IsAdsSupportedOnCurrentPlatform()
    {
#if UNITY_EDITOR
        return settings != null && settings.showInEditor;
#elif UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return false;
#endif
    }

    static string GetActiveRewardedAdUnitId()
    {
#if UNITY_ANDROID
        if (settings != null && settings.useTestAdUnits)
            return AndroidTestRewardedId;

        return settings != null ? settings.androidRewardedAdUnitId : string.Empty;
#elif UNITY_IOS
        if (settings != null && settings.useTestAdUnits)
            return IosTestRewardedId;

        return settings != null ? settings.iosRewardedAdUnitId : string.Empty;
#else
        return string.Empty;
#endif
    }
}
