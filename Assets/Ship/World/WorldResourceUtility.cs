using UnityEngine;

public static class WorldResourceUtility
{
    static readonly Color ScrapColor = new Color(0.97f, 0.63f, 0.22f, 1f);

    public static int RollScrapAmount(float sourceMass)
    {
        int maxAmount = Mathf.Max(1, Mathf.CeilToInt(sourceMass));
        return Random.Range(1, maxAmount + 1);
    }

    public static void AwardScrapFromMass(float sourceMass)
    {
        AwardScrap(RollScrapAmount(sourceMass));
    }

    public static void AwardScrapFromMass(float sourceMass, Vector3 worldPosition)
    {
        AwardScrap(RollScrapAmount(sourceMass), worldPosition);
    }

    public static void AwardScrap(int amount)
    {
        AwardScrap(amount, default, false);
    }

    public static void AwardScrap(int amount, Vector3 worldPosition)
    {
        AwardScrap(amount, worldPosition, true);
    }

    static void AwardScrap(int amount, Vector3 worldPosition, bool hasWorldPosition)
    {
        if (amount <= 0)
            return;

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return;

        hud.AddResource("scrap", LocalizationManager.Get("resource.scrap", "Scrap"), amount, ScrapColor);

        if (hasWorldPosition)
            WorldFeedbackRuntime.ShowScrapGain(worldPosition, amount);

        AudioRuntime.PlayScrapPickup();
    }
}
