using System;
using UnityEngine;

#if SCRAP_GIANT_ADMOB
using GoogleMobileAds.Api;
#endif

public static class AdMobInterstitialBridge
{
#if SCRAP_GIANT_ADMOB
    static bool initialized;
    static bool isLoading;
    static bool isShowing;
    static string lastAdUnitId;
    static Action pendingShowComplete;
    static InterstitialAd currentInterstitial;
#endif

    public static bool IsInterstitialReady
    {
        get
        {
#if SCRAP_GIANT_ADMOB
            return currentInterstitial != null && !isShowing;
#else
            return false;
#endif
        }
    }

    public static void Initialize()
    {
#if SCRAP_GIANT_ADMOB
        if (initialized)
            return;

        initialized = true;
        MobileAds.Initialize(_ => Debug.Log("AdMob initialization complete."));
#endif
    }

    public static void LoadInterstitial(string adUnitId)
    {
#if SCRAP_GIANT_ADMOB
        Initialize();

        if (isLoading || string.IsNullOrWhiteSpace(adUnitId))
            return;

        if (currentInterstitial != null && adUnitId == lastAdUnitId)
            return;

        lastAdUnitId = adUnitId;
        isLoading = true;
        DestroyCurrentInterstitial();

        AdRequest request = new AdRequest();
        InterstitialAd.Load(adUnitId, request, HandleInterstitialLoaded);
#endif
    }

    public static void ShowInterstitial(Action onComplete)
    {
#if SCRAP_GIANT_ADMOB
        if (!IsInterstitialReady)
        {
            onComplete?.Invoke();
            return;
        }

        pendingShowComplete = onComplete;
        isShowing = true;
        AudioListener.pause = true;
        currentInterstitial.Show();
#else
        onComplete?.Invoke();
#endif
    }

#if SCRAP_GIANT_ADMOB
    static void HandleInterstitialLoaded(InterstitialAd ad, LoadAdError error)
    {
        isLoading = false;

        if (error != null || ad == null)
        {
            Debug.LogWarning($"AdMob interstitial failed to load: {error}");
            return;
        }

        currentInterstitial = ad;
        RegisterCallbacks(ad);
        Debug.Log("AdMob interstitial loaded.");
    }

    static void RegisterCallbacks(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            CompleteShowAndPreload();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"AdMob interstitial failed to present: {error}");
            CompleteShowAndPreload();
        };
    }

    static void CompleteShowAndPreload()
    {
        isShowing = false;
        AudioListener.pause = false;
        DestroyCurrentInterstitial();

        Action callback = pendingShowComplete;
        pendingShowComplete = null;
        callback?.Invoke();

        if (!string.IsNullOrWhiteSpace(lastAdUnitId))
            LoadInterstitial(lastAdUnitId);
    }

    static void DestroyCurrentInterstitial()
    {
        if (currentInterstitial == null)
            return;

        currentInterstitial.Destroy();
        currentInterstitial = null;
    }
#endif
}
