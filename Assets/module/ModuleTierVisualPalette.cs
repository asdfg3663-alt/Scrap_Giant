using UnityEngine;

public static class ModuleTierVisualPalette
{
    static readonly float[] TierHueOffsets =
    {
        0f / 360f,
        45f / 360f,
        90f / 360f,
        135f / 360f,
        180f / 360f,
        215f / 360f,
        250f / 360f,
        285f / 360f,
        320f / 360f,
        350f / 360f
    };

    static readonly Color BaseTierColor = new Color32(0x70, 0xD8, 0xFF, 0xFF);

    public static Color GetTierColor(int tier)
    {
        int clampedTier = Mathf.Clamp(tier, 1, TierHueOffsets.Length);
        int index = clampedTier - 1;

        Color.RGBToHSV(BaseTierColor, out float baseHue, out float baseSaturation, out float baseValue);

        float hue = Mathf.Repeat(baseHue + TierHueOffsets[index], 1f);
        float saturationBoost = Mathf.Lerp(0.06f, 0.24f, Mathf.InverseLerp(1f, 10f, clampedTier));
        float valueBoost = Mathf.Lerp(0f, 0.1f, Mathf.InverseLerp(1f, 10f, clampedTier));

        float saturation = Mathf.Clamp01(baseSaturation + saturationBoost);
        float value = Mathf.Clamp01(baseValue + valueBoost);
        return Color.HSVToRGB(hue, saturation, value);
    }

    public static float GetTintStrength(int tier)
    {
        if (tier <= 1)
            return 0f;

        float t = Mathf.InverseLerp(1f, 10f, Mathf.Clamp(tier, 1, 10));
        return Mathf.Lerp(0.72f, 1f, t);
    }

    public static float GetGlowAlpha(int tier)
    {
        if (tier <= 5)
            return 0f;

        float t = Mathf.InverseLerp(6f, 10f, Mathf.Clamp(tier, 1, 10));
        return Mathf.Lerp(0.2f, 0.56f, t);
    }

    public static float GetGlowScale(int tier)
    {
        if (tier <= 5)
            return 1f;

        float t = Mathf.InverseLerp(6f, 10f, Mathf.Clamp(tier, 1, 10));
        return Mathf.Lerp(1.1f, 1.28f, t);
    }

    public static Color GetGlowColor(int tier)
    {
        Color tierColor = GetTierColor(tier);
        float whiteMix = Mathf.InverseLerp(6f, 10f, Mathf.Clamp(tier, 1, 10)) * 0.6f;
        return Color.Lerp(tierColor, Color.white, whiteMix);
    }
}
