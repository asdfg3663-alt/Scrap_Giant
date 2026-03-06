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

        return $"{baseName}_T{Mathf.Max(1, tier)}";
    }
}
