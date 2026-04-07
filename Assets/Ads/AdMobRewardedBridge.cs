using System;
using UnityEngine;

#if SCRAP_GIANT_ADMOB
using GoogleMobileAds.Api;
#endif

public static class AdMobRewardedBridge
{
#if SCRAP_GIANT_ADMOB
    static bool initialized;
    static bool isLoading;
    static bool isShowing;
    static bool rewardEarned;
    static string lastAdUnitId;
    static Action<bool> pendingShowComplete;
    static RewardedAd currentRewarded;
#endif

    public static bool IsRewardedReady
    {
        get
        {
#if SCRAP_GIANT_ADMOB
            return currentRewarded != null && !isShowing;
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
        MobileAds.Initialize(_ => Debug.Log("AdMob rewarded initialization complete."));
#endif
    }

    public static void LoadRewarded(string adUnitId)
    {
#if SCRAP_GIANT_ADMOB
        Initialize();

        if (isLoading || string.IsNullOrWhiteSpace(adUnitId))
            return;

        if (currentRewarded != null && adUnitId == lastAdUnitId)
            return;

        lastAdUnitId = adUnitId;
        isLoading = true;
        DestroyCurrentRewarded();

        AdRequest request = new AdRequest();
        RewardedAd.Load(adUnitId, request, HandleRewardedLoaded);
#endif
    }

    public static void ShowRewarded(Action<bool> onComplete)
    {
#if SCRAP_GIANT_ADMOB
        if (!IsRewardedReady)
        {
            onComplete?.Invoke(false);
            return;
        }

        pendingShowComplete = onComplete;
        isShowing = true;
        rewardEarned = false;
        AudioListener.pause = true;
        currentRewarded.Show(reward =>
        {
            rewardEarned = reward != null && reward.Amount > 0d;
        });
#else
        onComplete?.Invoke(false);
#endif
    }

#if SCRAP_GIANT_ADMOB
    static void HandleRewardedLoaded(RewardedAd ad, LoadAdError error)
    {
        isLoading = false;

        if (error != null || ad == null)
        {
            Debug.LogWarning($"AdMob rewarded failed to load: {error}");
            return;
        }

        currentRewarded = ad;
        RegisterCallbacks(ad);
        Debug.Log("AdMob rewarded loaded.");
    }

    static void RegisterCallbacks(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            CompleteShowAndPreload(rewardEarned);
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"AdMob rewarded failed to present: {error}");
            CompleteShowAndPreload(false);
        };
    }

    static void CompleteShowAndPreload(bool earnedReward)
    {
        isShowing = false;
        AudioListener.pause = false;
        DestroyCurrentRewarded();

        Action<bool> callback = pendingShowComplete;
        pendingShowComplete = null;
        callback?.Invoke(earnedReward);

        if (!string.IsNullOrWhiteSpace(lastAdUnitId))
            LoadRewarded(lastAdUnitId);
    }

    static void DestroyCurrentRewarded()
    {
        if (currentRewarded == null)
            return;

        currentRewarded.Destroy();
        currentRewarded = null;
    }
#endif
}
