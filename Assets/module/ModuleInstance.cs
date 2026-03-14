using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ModuleInstance : MonoBehaviour
{
    public ModuleData data;

    [Header("Runtime (per-instance)")]
    public int hp;
    public int maxHp;
    public bool resetHpOnDataAssign = true;
    public float currentHeat;
    public float repairProgress;

    [Header("Upgrade")]
    [Min(0)] public int upgradeLevel = 0;

    public int CurrentTier => data != null ? Mathf.Max(1, data.tier + upgradeLevel) : Mathf.Max(1, upgradeLevel);
    public string DisplayName => GetLocalizedDisplayName();
    public float TierMultiplier => Mathf.Pow(2f, Mathf.Max(0, upgradeLevel));

    void Awake()
    {
        SyncFromDataIfNeeded(forceReset: false);
    }

    void OnValidate()
    {
        SyncFromDataIfNeeded(forceReset: false);
    }

    public void SyncFromDataIfNeeded(bool forceReset)
    {
        if (data == null) return;

        int previousMax = maxHp;
        int newMax = GetMaxHp();
        bool maxChanged = maxHp != newMax;

        maxHp = newMax;

        if (forceReset || (resetHpOnDataAssign && (hp <= 0 || maxChanged)))
            hp = maxHp;
        else if (maxChanged && previousMax > 0)
            hp += maxHp - previousMax;

        hp = Mathf.Clamp(hp, 0, maxHp);
        currentHeat = Mathf.Clamp(currentHeat, 0f, GetMaxHeat());
        repairProgress = Mathf.Max(0f, repairProgress);
    }

    public void ApplyUpgrade(int amount = 1)
    {
        upgradeLevel = Mathf.Max(0, upgradeLevel + amount);
        SyncFromDataIfNeeded(forceReset: false);
    }

    public int GetMaxHp()
    {
        if (data == null)
            return Mathf.Max(1, maxHp);

        int bonusPerTier = data.type == ModuleType.FuelTank ? 10 : 5;
        return Mathf.Max(1, data.maxHP + Mathf.Max(0, upgradeLevel) * bonusPerTier);
    }

    public float GetPowerGenPerSec() { return data != null ? data.powerGenPerSec * TierMultiplier : 0f; }
    public float GetEffectivePowerGenPerSec() { return GetPowerGenPerSec() * GetHeatEfficiencyMultiplierForPower(); }
    public float GetPowerUsePerSec() { return data != null ? data.powerUsePerSec * TierMultiplier : 0f; }
    public float GetMaxEnergy() { return data != null ? data.maxEnergy * TierMultiplier : 0f; }
    public float GetMaxFuel() { return data != null ? data.maxFuel * TierMultiplier : 0f; }
    public float GetFuelSynthesisPerSec() { return data != null ? data.fuelSynthesisPerSec * TierMultiplier : 0f; }
    public float GetThrust() { return data != null ? data.thrust * TierMultiplier : 0f; }
    public float GetMass() { return data != null ? data.mass * TierMultiplier : 0f; }
    public float GetScoreValue() { return data != null ? GetMass() * Mathf.Max(0f, data.scoreMultiplier) : 0f; }
    public float GetWeaponDamage() { return data != null ? data.weaponDamage * TierMultiplier : 0f; }
    public float GetEffectiveWeaponDamage() { return GetWeaponDamage() * GetHeatEfficiencyMultiplierForWeapon(); }
    public float GetWeaponFireRate() { return data != null ? data.weaponFireRate * TierMultiplier : 0f; }
    public float GetWeaponPowerPerShot() { return data != null ? data.weaponPowerPerShot * TierMultiplier : 0f; }
    public float GetWeaponHeatPerShot() { return data != null ? data.weaponHeatPerShot * TierMultiplier : 0f; }
    public float GetWeaponAmmoPerShot() { return data != null ? data.weaponAmmoPerShot * TierMultiplier : 0f; }
    public float GetMaxHeat() { return data != null ? data.maxHeat * TierMultiplier : 0f; }
    public float GetHeatDissipationPerSec() { return data != null ? data.heatDissipationPerSec * TierMultiplier : 0f; }
    public float GetRepairPerSecond() { return data != null ? data.repairPerSecond * TierMultiplier : 0f; }

    public float GetDps()
    {
        if (data == null) return 0f;
        if (data.dps > 0f) return data.dps * TierMultiplier;
        return Mathf.Max(0f, GetWeaponDamage()) * Mathf.Max(0f, GetWeaponFireRate());
    }

    public float GetWeaponHeatPerSecondPotential()
    {
        float heatPerShot = GetWeaponHeatPerShot();
        float fireRate = GetWeaponFireRate();
        if (heatPerShot > 0f && fireRate > 0f)
            return heatPerShot * fireRate;

        if (data != null && data.weaponType == WeaponType.Laser)
            return Mathf.Max(0f, GetDps());

        return 0f;
    }

    public float GetPassiveHeatPerSecond()
    {
        if (data == null)
            return 0f;

        if (data.type == ModuleType.Reactor && GetPowerGenPerSec() > 0f)
            return GetPowerGenPerSec() * 2f;

        return 0f;
    }

    public float GetTotalHeatGenerationPerSecondPotential()
    {
        return GetPassiveHeatPerSecond() + GetWeaponHeatPerSecondPotential();
    }

    public float GetHeatRatio()
    {
        float maxHeat = GetMaxHeat();
        if (maxHeat <= 0.001f)
            return 0f;

        return Mathf.Clamp01(currentHeat / maxHeat);
    }

    public void AddHeat(float amount)
    {
        if (amount <= 0f)
            return;

        currentHeat = Mathf.Clamp(currentHeat + amount, 0f, GetMaxHeat());
    }

    public void CoolHeat(float amount)
    {
        if (amount <= 0f)
            return;

        currentHeat = Mathf.Max(0f, currentHeat - amount);
    }

    float GetHeatEfficiencyMultiplierForWeapon()
    {
        if (data == null || data.weaponType != WeaponType.Laser || GetMaxHeat() <= 0f)
            return 1f;

        return Mathf.Lerp(1f, 0.5f, GetHeatRatio());
    }

    float GetHeatEfficiencyMultiplierForPower()
    {
        if (data == null || data.type != ModuleType.Reactor || GetMaxHeat() <= 0f)
            return 1f;

        return Mathf.Lerp(1f, 0.5f, GetHeatRatio());
    }

    string GetLocalizedDisplayName()
    {
        string fallback = FormatDisplayName(data != null ? data.displayName : "Module", CurrentTier);
        return LocalizationManager.GetModuleText(data != null ? data.localizationKey : string.Empty, fallback);
    }

    public static string FormatDisplayName(string rawName, int tier)
    {
        string baseName = string.IsNullOrWhiteSpace(rawName) ? "Module" : rawName.Trim();

        int underscoreTier = baseName.LastIndexOf("_T", StringComparison.OrdinalIgnoreCase);
        int parsedTier;
        if (underscoreTier >= 0 && int.TryParse(baseName.Substring(underscoreTier + 2), out parsedTier))
            baseName = baseName.Substring(0, underscoreTier);
        else
        {
            int spaceTier = baseName.LastIndexOf(" T", StringComparison.OrdinalIgnoreCase);
            if (spaceTier >= 0 && int.TryParse(baseName.Substring(spaceTier + 2), out parsedTier))
                baseName = baseName.Substring(0, spaceTier);
        }

        return baseName;
    }
}
