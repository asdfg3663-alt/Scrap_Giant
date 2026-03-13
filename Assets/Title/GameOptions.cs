using UnityEngine;

public static class GameOptions
{
    const string MasterVolumeKey = "ScrapGiant.Options.MasterVolume";
    const string BgmVolumeKey = "ScrapGiant.Options.BgmVolume";
    const string SfxVolumeKey = "ScrapGiant.Options.SfxVolume";
    const string FullscreenKey = "ScrapGiant.Options.Fullscreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        ApplyAll();
    }

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            ApplyAll();
        }
    }

    public static float BgmVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 0.85f));
        set
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 0.9f));
        set
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyAll();
        }
    }

    public static void ApplyAll()
    {
        AudioListener.volume = MasterVolume;
        Screen.fullScreen = Fullscreen;
    }
}
