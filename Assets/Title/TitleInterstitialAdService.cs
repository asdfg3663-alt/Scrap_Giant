using System;

public static class TitleInterstitialAdService
{
    public static bool IsAvailable => false;

    public static void ShowIfAvailable(Action onComplete)
    {
        onComplete?.Invoke();
    }
}
