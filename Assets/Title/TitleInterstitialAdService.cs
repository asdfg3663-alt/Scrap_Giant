using System;
using UnityEngine;

public enum TitleInterstitialPlacement
{
    PlayStart,
    GameOver
}

[Serializable]
public sealed class TitleInterstitialAdSettingsData
{
    public bool enabled = true;
    public bool preloadOnStartup = true;
    public bool showOnPlayStart = true;
    public bool showOnGameOver = true;
    public bool showInEditor = false;
    public bool useTestAdUnits = true;
    public string androidAppId = "ca-app-pub-3940256099942544~3347511713";
    public string iosAppId = "ca-app-pub-3940256099942544~1458002511";
    public string androidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    public string iosInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
}

public static class TitleInterstitialAdService
{
    const string SettingsResourcePath = "Monetization/TitleInterstitialAdSettings";
    const string AndroidTestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
    const string IosTestInterstitialId = "ca-app-pub-3940256099942544/4411468910";

    static TitleInterstitialAdSettingsData settings;
    static bool initialized;

    public static bool IsAvailable => IsPlacementEnabled(TitleInterstitialPlacement.GameOver) && AdMobInterstitialBridge.IsInterstitialReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeOnLoad()
    {
        EnsureInitialized();
    }

    public static void ShowIfAvailable(Action onComplete)
    {
        ShowIfAvailable(TitleInterstitialPlacement.GameOver, onComplete);
    }

    public static void ShowIfAvailable(TitleInterstitialPlacement placement, Action onComplete)
    {
        EnsureInitialized();

        if (!IsPlacementEnabled(placement))
        {
            onComplete?.Invoke();
            return;
        }

        if (!AdMobInterstitialBridge.IsInterstitialReady)
        {
            AdMobInterstitialBridge.LoadInterstitial(GetActiveInterstitialAdUnitId());
            onComplete?.Invoke();
            return;
        }

        AdMobInterstitialBridge.ShowInterstitial(() =>
        {
            AudioListener.pause = false;
            onComplete?.Invoke();
        });
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        settings = LoadSettings();

        if (!IsAdsSupportedOnCurrentPlatform())
            return;

        AdMobInterstitialBridge.Initialize();

        if (settings.preloadOnStartup)
            AdMobInterstitialBridge.LoadInterstitial(GetActiveInterstitialAdUnitId());
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
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to parse AdMob settings JSON: {ex.Message}");
            return new TitleInterstitialAdSettingsData();
        }
    }

    static bool IsPlacementEnabled(TitleInterstitialPlacement placement)
    {
        if (!IsAdsSupportedOnCurrentPlatform())
            return false;

        if (!settings.enabled)
            return false;

        if (placement == TitleInterstitialPlacement.PlayStart)
            return settings.showOnPlayStart;

        if (placement == TitleInterstitialPlacement.GameOver)
            return settings.showOnGameOver;

        return false;
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

    static string GetActiveInterstitialAdUnitId()
    {
#if UNITY_ANDROID
        if (settings != null && settings.useTestAdUnits)
            return AndroidTestInterstitialId;

        return settings != null ? settings.androidInterstitialAdUnitId : string.Empty;
#elif UNITY_IOS
        if (settings != null && settings.useTestAdUnits)
            return IosTestInterstitialId;

        return settings != null ? settings.iosInterstitialAdUnitId : string.Empty;
#else
        return string.Empty;
#endif
    }
}
