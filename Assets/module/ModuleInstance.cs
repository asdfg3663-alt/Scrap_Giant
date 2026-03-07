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

    [Header("Upgrade")]
    [Min(0)] public int upgradeLevel = 0;

    public int CurrentTier => data != null ? Mathf.Max(1, data.tier + upgradeLevel) : Mathf.Max(1, upgradeLevel);
    public string DisplayName => FormatDisplayName(data != null ? data.displayName : "Module", CurrentTier);
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

        int newMax = Mathf.Max(1, data.maxHP);
        bool maxChanged = maxHp != newMax;

        maxHp = newMax;

        if (forceReset || (resetHpOnDataAssign && (hp <= 0 || maxChanged)))
            hp = maxHp;

        hp = Mathf.Clamp(hp, 0, maxHp);
    }

    public void ApplyUpgrade(int amount = 1)
    {
        upgradeLevel = Mathf.Max(0, upgradeLevel + amount);
        SyncFromDataIfNeeded(forceReset: false);
    }

    public int GetMaxHp()
    {
        return data != null ? Mathf.Max(1, data.maxHP) : Mathf.Max(1, maxHp);
    }

    public float GetPowerGenPerSec() { return data != null ? data.powerGenPerSec * TierMultiplier : 0f; }
    public float GetPowerUsePerSec() { return data != null ? data.powerUsePerSec * TierMultiplier : 0f; }
    public float GetMaxEnergy() { return data != null ? data.maxEnergy * TierMultiplier : 0f; }
    public float GetThrust() { return data != null ? data.thrust * TierMultiplier : 0f; }
    public float GetMass() { return data != null ? data.mass * TierMultiplier : 0f; }
    public float GetWeaponDamage() { return data != null ? data.weaponDamage * TierMultiplier : 0f; }
    public float GetWeaponFireRate() { return data != null ? data.weaponFireRate * TierMultiplier : 0f; }
    public float GetWeaponPowerPerShot() { return data != null ? data.weaponPowerPerShot * TierMultiplier : 0f; }
    public float GetWeaponHeatPerShot() { return data != null ? data.weaponHeatPerShot * TierMultiplier : 0f; }
    public float GetWeaponAmmoPerShot() { return data != null ? data.weaponAmmoPerShot * TierMultiplier : 0f; }

    public float GetDps()
    {
        if (data == null) return 0f;
        if (data.dps > 0f) return data.dps * TierMultiplier;
        return Mathf.Max(0f, GetWeaponDamage()) * Mathf.Max(0f, GetWeaponFireRate());
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
