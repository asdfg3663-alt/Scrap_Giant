using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class LocalizationFontManager
{
    static TMP_FontAsset uiFontAsset;
    static readonly List<TMP_FontAsset> fallbackAssets = new();
    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        uiFontAsset = null;
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
        return uiFontAsset != null ? uiFontAsset : TMP_Settings.defaultFontAsset;
    }

    public static void ApplyFont(TMP_Text text)
    {
        if (text == null)
            return;

        EnsureInitialized();
        TMP_FontAsset font = GetUiFontAsset();
        if (font != null)
        {
            text.font = font;
            text.havePropertiesChanged = true;
            text.SetAllDirty();
        }
    }

    public static void RefreshActiveTexts()
    {
        EnsureInitialized();

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            TMP_FontAsset currentFont = text.font;
            if (uiFontAsset != null)
                text.font = uiFontAsset;
            else if (currentFont == null && TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;

            if (text.font != null)
            {
                AttachFallbackChain(text.font);
                text.havePropertiesChanged = true;
                text.SetAllDirty();
            }
        }
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;

        uiFontAsset = CreateDynamicFontAsset(new[] { "Segoe UI", "Arial" }, "Localized_UI_Primary");
        TMP_FontAsset koreanFallback = CreateDynamicFontAsset(new[] { "Malgun Gothic" }, "Localized_Korean_Fallback");
        TMP_FontAsset japaneseFallback = CreateDynamicFontAsset(new[] { "Yu Gothic UI", "Yu Gothic", "MS Gothic" }, "Localized_Japanese_Fallback");

        AddFallback(koreanFallback);
        AddFallback(japaneseFallback);

        AttachFallbackChain(TMP_Settings.defaultFontAsset);

        if (uiFontAsset == null)
        {
            ApplyGlobalFallbacks();
            return;
        }

        AttachFallbackChain(uiFontAsset);
        ApplyGlobalFallbacks();
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
                    // Try the next style / family combination.
                }
            }
        }

        return null;
    }

    static void AddFallbackToSettings(TMP_FontAsset fallback)
    {
        if (fallback == null)
            return;

        if (TMP_Settings.fallbackFontAssets == null)
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();

        if (!TMP_Settings.fallbackFontAssets.Contains(fallback))
            TMP_Settings.fallbackFontAssets.Add(fallback);
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
        if (font == null)
            return;

        for (int i = 0; i < fallbackAssets.Count; i++)
        {
            TMP_FontAsset fallback = fallbackAssets[i];
            if (fallback == null || fallback == font)
                continue;

            AddFallbackToFont(font, fallback);
        }
    }

    static void ApplyGlobalFallbacks()
    {
        if (uiFontAsset != null)
            TMP_Settings.defaultFontAsset = uiFontAsset;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        AttachFallbackChain(defaultFont);

        AddFallbacksToTmpSettings();
    }

    static void AddFallbacksToTmpSettings()
    {
        if (fallbackAssets.Count == 0)
            return;

        for (int i = 0; i < fallbackAssets.Count; i++)
        {
            AddFallbackToSettings(fallbackAssets[i]);
        }
    }
}
