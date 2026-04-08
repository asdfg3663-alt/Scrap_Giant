using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class LocalizationFontManager
{
    static TMP_FontAsset latinFontAsset;
    static TMP_FontAsset koreanFontAsset;
    static TMP_FontAsset japaneseFontAsset;
    static TMP_FontAsset cyrillicFontAsset;
    static TMP_FontAsset simplifiedChineseFontAsset;
    static TMP_FontAsset resourceFallbackFontAsset;
    static readonly List<TMP_FontAsset> fallbackAssets = new();
    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        latinFontAsset = null;
        koreanFontAsset = null;
        japaneseFontAsset = null;
        cyrillicFontAsset = null;
        simplifiedChineseFontAsset = null;
        resourceFallbackFontAsset = null;
        fallbackAssets.Clear();
        initialized = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyToExistingTextOnLoad()
    {
        RefreshActiveTexts();
    }

    public static TMP_FontAsset GetUiFontAsset()
    {
        EnsureInitialized();

        TMP_FontAsset localized = GetFontAssetForLanguage(LocalizationManager.CurrentLanguage);
        if (localized != null)
            return localized;

        if (resourceFallbackFontAsset != null)
            return resourceFallbackFontAsset;

        return TMP_Settings.defaultFontAsset;
    }

    public static TMP_FontAsset GetFontAssetForLanguage(GameLanguage language)
    {
        EnsureInitialized();

        TMP_FontAsset localized = GetPrimaryFontForLanguage(language);
        if (IsFontAssetUsable(localized))
            return localized;

        return IsFontAssetUsable(resourceFallbackFontAsset) ? resourceFallbackFontAsset : TMP_Settings.defaultFontAsset;
    }

    public static void ApplyFont(TMP_Text text)
    {
        if (text == null)
            return;

        EnsureInitialized();
        ApplyResolvedFont(text, GetUiFontAsset());
    }

    public static void ApplyFont(TMP_Text text, GameLanguage language)
    {
        if (text == null)
            return;

        EnsureInitialized();
        ApplyResolvedFont(text, GetFontAssetForLanguage(language));
    }

    public static void RefreshActiveTexts()
    {
        EnsureInitialized();

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        TMP_FontAsset activeFont = GetUiFontAsset();

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            TMP_FontAsset font = activeFont != null ? activeFont : text.font;
            if (!IsFontAssetUsable(font))
                font = TMP_Settings.defaultFontAsset;

            if (font == null)
                continue;

            ApplyResolvedFont(text, font);
        }
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;

        resourceFallbackFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        latinFontAsset = CreateDynamicFontAsset(GetPrimaryUiFontNames(), "Localized_UI_Primary");
        koreanFontAsset = CreateDynamicFontAsset(GetKoreanFontNames(), "Localized_Korean_Primary");
        japaneseFontAsset = CreateDynamicFontAsset(GetJapaneseFontNames(), "Localized_Japanese_Primary");
        cyrillicFontAsset = CreateDynamicFontAsset(GetCyrillicFontNames(), "Localized_Cyrillic_Primary");
        simplifiedChineseFontAsset = CreateDynamicFontAsset(GetSimplifiedChineseFontNames(), "Localized_Chinese_Primary");

        AddFallback(resourceFallbackFontAsset);
        AddFallback(koreanFontAsset);
        AddFallback(japaneseFontAsset);
        AddFallback(cyrillicFontAsset);
        AddFallback(simplifiedChineseFontAsset);

        AttachFallbackChain(resourceFallbackFontAsset);
        AttachFallbackChain(latinFontAsset);
        AttachFallbackChain(koreanFontAsset);
        AttachFallbackChain(japaneseFontAsset);
        AttachFallbackChain(cyrillicFontAsset);
        AttachFallbackChain(simplifiedChineseFontAsset);

        CleanupTmpSettingsReferences();
    }

    static TMP_FontAsset GetPrimaryFontForLanguage(GameLanguage language)
    {
        return language switch
        {
            GameLanguage.Korean => koreanFontAsset ?? latinFontAsset ?? resourceFallbackFontAsset,
            GameLanguage.Japanese => japaneseFontAsset ?? latinFontAsset ?? resourceFallbackFontAsset,
            GameLanguage.Russian => cyrillicFontAsset ?? latinFontAsset ?? resourceFallbackFontAsset,
            GameLanguage.SimplifiedChinese => simplifiedChineseFontAsset ?? latinFontAsset ?? resourceFallbackFontAsset,
            _ => latinFontAsset ?? resourceFallbackFontAsset
        };
    }

    static TMP_FontAsset CreateDynamicFontAsset(string[] fontNames, string assetName)
    {
        if (fontNames == null || fontNames.Length == 0)
            return null;

        TMP_FontAsset asset = TryCreateOsFontAsset(fontNames);
        if (asset == null)
        {
            Font sourceFont = Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (sourceFont != null)
                asset = TMP_FontAsset.CreateFontAsset(sourceFont);
        }

        if (asset == null)
            return null;

        asset.name = assetName;
        asset.hideFlags = HideFlags.HideAndDontSave;
        PreserveFontSubAssets(asset);
        return asset;
    }

    static void AddFallback(TMP_FontAsset fallback)
    {
        if (fallback == null)
            return;

        if (!fallbackAssets.Contains(fallback))
            fallbackAssets.Add(fallback);
    }

    static TMP_FontAsset TryCreateOsFontAsset(string[] fontNames)
    {
        string[] styleNames = { "Regular", "Normal", "Book", "Medium" };
        for (int i = 0; i < fontNames.Length; i++)
        {
            string familyName = fontNames[i];
            if (string.IsNullOrWhiteSpace(familyName))
                continue;

            for (int s = 0; s < styleNames.Length; s++)
            {
                try
                {
                    TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(familyName, styleNames[s], 90);
                    if (asset != null)
                        return asset;
                }
                catch
                {
                }
            }
        }

        return null;
    }

    static void AddFallbackToFont(TMP_FontAsset primary, TMP_FontAsset fallback)
    {
        if (primary == null || fallback == null)
            return;

        if (primary.fallbackFontAssetTable == null)
            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();

        if (!primary.fallbackFontAssetTable.Contains(fallback))
            primary.fallbackFontAssetTable.Add(fallback);
    }

    static void AttachFallbackChain(TMP_FontAsset font)
    {
        if (!IsFontAssetUsable(font))
            return;

        for (int i = 0; i < fallbackAssets.Count; i++)
        {
            TMP_FontAsset fallback = fallbackAssets[i];
            if (!IsFontAssetUsable(fallback) || fallback == font)
                continue;

            AddFallbackToFont(font, fallback);
        }
    }

    static void ApplyResolvedFont(TMP_Text text, TMP_FontAsset font)
    {
        if (text == null)
            return;

        if (!IsFontAssetUsable(font))
            font = IsFontAssetUsable(resourceFallbackFontAsset) ? resourceFallbackFontAsset : TMP_Settings.defaultFontAsset;

        if (!IsFontAssetUsable(font))
            return;

        AttachFallbackChain(font);
        text.font = font;
        if (font.material != null)
            text.fontSharedMaterial = font.material;
        text.havePropertiesChanged = true;
        text.SetAllDirty();
    }

    static void PreserveFontSubAssets(TMP_FontAsset font)
    {
        if (font == null)
            return;

        try
        {
            if (font.material != null)
                font.material.hideFlags = HideFlags.HideAndDontSave;

            Texture[] atlasTextures = font.atlasTextures;
            if (atlasTextures == null)
                return;

            for (int i = 0; i < atlasTextures.Length; i++)
            {
                if (atlasTextures[i] != null)
                    atlasTextures[i].hideFlags = HideFlags.HideAndDontSave;
            }
        }
        catch (MissingReferenceException)
        {
        }
    }

    static bool IsFontAssetUsable(TMP_FontAsset font)
    {
        if (font == null)
            return false;

        try
        {
            _ = font.material;
            _ = font.atlasTextures;
            return true;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
    }

    static void CleanupTmpSettingsReferences()
    {
        if (TMP_Settings.instance == null)
            return;

        if (!IsFontAssetUsable(TMP_Settings.defaultFontAsset))
        {
            TMP_FontAsset safeDefault = resourceFallbackFontAsset ?? latinFontAsset;
            if (IsFontAssetUsable(safeDefault))
                TMP_Settings.defaultFontAsset = safeDefault;
        }

        if (TMP_Settings.fallbackFontAssets == null)
            return;

        for (int i = TMP_Settings.fallbackFontAssets.Count - 1; i >= 0; i--)
        {
            if (!IsFontAssetUsable(TMP_Settings.fallbackFontAssets[i]))
                TMP_Settings.fallbackFontAssets.RemoveAt(i);
        }
    }

    static string[] GetPrimaryUiFontNames()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new[] { "sans-serif", "sans-serif-medium", "Roboto", "Droid Sans", "Noto Sans" };
#elif UNITY_IOS && !UNITY_EDITOR
        return new[] { "Arial Unicode MS", "Helvetica Neue", "Helvetica" };
#else
        return new[] { "Segoe UI", "Arial", "Liberation Sans" };
#endif
    }

    static string[] GetKoreanFontNames()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new[] { "Noto Sans CJK KR", "Noto Sans KR", "SamsungOneKorean", "sans-serif" };
#elif UNITY_IOS && !UNITY_EDITOR
        return new[] { "Apple SD Gothic Neo", "Arial Unicode MS" };
#else
        return new[] { "Malgun Gothic", "Arial Unicode MS", "Segoe UI Symbol" };
#endif
    }

    static string[] GetJapaneseFontNames()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new[] { "Noto Sans CJK JP", "Noto Sans JP", "Droid Sans Japanese", "sans-serif" };
#elif UNITY_IOS && !UNITY_EDITOR
        return new[] { "Hiragino Sans", "Arial Unicode MS" };
#else
        return new[] { "Yu Gothic UI", "Yu Gothic", "MS Gothic", "Arial Unicode MS" };
#endif
    }

    static string[] GetCyrillicFontNames()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new[] { "sans-serif", "Roboto", "Droid Sans" };
#elif UNITY_IOS && !UNITY_EDITOR
        return new[] { "Arial Unicode MS", "Helvetica Neue" };
#else
        return new[] { "Segoe UI", "Arial", "Tahoma" };
#endif
    }

    static string[] GetSimplifiedChineseFontNames()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return new[] { "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "MiSans", "sans-serif" };
#elif UNITY_IOS && !UNITY_EDITOR
        return new[] { "PingFang SC", "Heiti SC", "Arial Unicode MS" };
#else
        return new[] { "Microsoft YaHei UI", "Microsoft YaHei", "DengXian", "SimHei", "Arial Unicode MS" };
#endif
    }
}
