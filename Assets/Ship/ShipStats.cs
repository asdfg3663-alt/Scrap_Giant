using UnityEngine;

public class ShipStats : MonoBehaviour
{
    static readonly Color FuelColor = new Color(0.38f, 0.85f, 0.95f, 1f);

    [Header("Identity")]
    [Tooltip("Player-controlled ship if true.")]
    public bool isPlayerShip = false;

    [Tooltip("If true, Tag=Player also marks this as the player ship.")]
    public bool autoDetectPlayerByTag = true;

    public int maxHP;
    public int currentHP;

    [Header("Power")]
    public float powerGenPerSec;
    public float powerUsePerSec;
    public float netPowerPerSec;

    [Header("Battery")]
    public float energyMax;
    public float energyCurrent;

    [Header("Fuel")]
    public float fuelMax;
    public float fuelCurrent;
    public float fuelSynthesisPerSec;
    public float lowFuelSynthesisThreshold = 0.2f;
    public float emptyFuelThrustMultiplier = 0.1f;
    public float minimumEmergencyThrust = 1f;

    [Header("Movement")]
    public float totalThrust;
    public float totalMass;

    [Header("Combat (MVP)")]
    public float totalDps;
    public float weaponPowerPerSecPotential;
    public float weaponHeatPerSecPotential;
    public float weaponAmmoPerSecPotential;

    ModuleInstance[] modules;
    bool rebuildQueued;
    float synthesisScrapProgress;
    bool fuelSynthesisActive;

    void Awake()
    {
        if (autoDetectPlayerByTag && CompareTag("Player"))
            isPlayerShip = true;
    }

    void Start()
    {
        Rebuild();

        if (energyCurrent <= 0f)
            energyCurrent = energyMax;

        if (fuelCurrent <= 0f && fuelMax > 0f)
            fuelCurrent = fuelMax;

        if (isPlayerShip)
        {
            PlayerHudRuntime.EnsureForPlayer(this);
            WorldSpawnDirector.RegisterPlayer(this);
            RefreshHudFuel();
        }
    }

    void Update()
    {
        energyCurrent += netPowerPerSec * Time.deltaTime;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);

        UpdateFuelSynthesis(Time.deltaTime);
        RefreshHudFuel();
    }

    void LateUpdate()
    {
        if (!rebuildQueued)
            return;

        rebuildQueued = false;
        Rebuild();
    }

    void OnTransformChildrenChanged()
    {
        ScheduleRebuild();
    }

    public void ScheduleRebuild()
    {
        rebuildQueued = true;
    }

    public void Rebuild()
    {
        modules = GetComponentsInChildren<ModuleInstance>(true);

        float previousFuelMax = fuelMax;
        float previousFuelCurrent = fuelCurrent;

        maxHP = 0;
        powerGenPerSec = 0f;
        powerUsePerSec = 0f;
        netPowerPerSec = 0f;

        totalThrust = 0f;
        totalMass = 0f;
        energyMax = 0f;
        fuelMax = 0f;
        fuelSynthesisPerSec = 0f;

        totalDps = 0f;
        weaponPowerPerSecPotential = 0f;
        weaponHeatPerSecPotential = 0f;
        weaponAmmoPerSecPotential = 0f;

        foreach (var m in modules)
        {
            if (m == null || m.data == null)
                continue;

            var data = m.data;

            maxHP += m.GetMaxHp();
            powerGenPerSec += m.GetPowerGenPerSec();
            totalThrust += m.GetThrust();
            totalMass += m.GetMass();
            energyMax += m.GetMaxEnergy();
            fuelMax += m.GetMaxFuel();
            fuelSynthesisPerSec += m.GetFuelSynthesisPerSec();

            bool isWeapon = data.type == ModuleType.Weapon || data.weaponType != WeaponType.None || data.dps > 0f;
            if (!isWeapon)
                powerUsePerSec += m.GetPowerUsePerSec();

            if (!isWeapon)
                continue;

            float dps = m.GetDps();
            totalDps += dps;
            weaponPowerPerSecPotential += Mathf.Max(0f, m.GetWeaponPowerPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
            weaponHeatPerSecPotential += Mathf.Max(0f, m.GetWeaponHeatPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
            weaponAmmoPerSecPotential += Mathf.Max(0f, m.GetWeaponAmmoPerShot()) * Mathf.Max(0f, m.GetWeaponFireRate());
        }

        netPowerPerSec = powerGenPerSec - powerUsePerSec;
        currentHP = maxHP;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);

        float addedFuelCapacity = Mathf.Max(0f, fuelMax - previousFuelMax);
        if (addedFuelCapacity > 0f)
            previousFuelCurrent += addedFuelCapacity;

        if (previousFuelMax <= 0f && fuelMax > 0f && previousFuelCurrent <= 0f)
            previousFuelCurrent = fuelMax;

        fuelCurrent = Mathf.Clamp(previousFuelCurrent, 0f, fuelMax);
        synthesisScrapProgress = Mathf.Clamp(synthesisScrapProgress, 0f, 1f);

        if (fuelMax <= 0f)
            fuelSynthesisActive = false;
    }

    public bool TryConsumeBattery(float amount)
    {
        if (amount <= 0f)
            return true;

        if (energyCurrent < amount)
            return false;

        energyCurrent -= amount;
        if (energyCurrent < 0f)
            energyCurrent = 0f;

        return true;
    }

    public bool HasFuelSystem()
    {
        return fuelMax > 0.01f;
    }

    public float FuelRatio()
    {
        if (fuelMax <= 0.01f)
            return 0f;

        return Mathf.Clamp01(fuelCurrent / fuelMax);
    }

    public float GetEffectiveTotalThrust()
    {
        if (totalThrust <= 0f)
            return 0f;

        if (fuelCurrent > 0.001f)
            return totalThrust;

        return Mathf.Max(minimumEmergencyThrust, totalThrust * emptyFuelThrustMultiplier);
    }

    public void ConsumeFuelForThrust(float deltaTime)
    {
        if (deltaTime <= 0f || totalThrust <= 0f || fuelCurrent <= 0f)
            return;

        fuelCurrent -= totalThrust * deltaTime;
        if (fuelCurrent < 0f)
            fuelCurrent = 0f;
    }

    public Transform GetCoreTransform()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            if (module.data.type == ModuleType.Core)
                return module.transform;
        }

        return transform;
    }

    public int GetInstalledModuleCount()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        int count = 0;
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null && modules[i].data != null)
                count++;
        }

        return count;
    }

    public string GetFuelAssemblyPrimaryText()
    {
        if (!HasFuelSystem())
            return "Install a fuel tank";

        if (fuelSynthesisActive && fuelCurrent < fuelMax)
        {
            var hud = PlayerHudRuntime.Instance;
            if (hud == null || !hud.HasResource("scrap", 1f))
                return "Low fuel: need Scrap";

            return "Fuel synthesis active";
        }

        return "Fuel synthesis ready";
    }

    public string GetFuelAssemblySecondaryText()
    {
        if (!HasFuelSystem())
            return string.Empty;

        if (fuelSynthesisActive && fuelCurrent < fuelMax)
        {
            var hud = PlayerHudRuntime.Instance;
            if (hud == null || !hud.HasResource("scrap", 1f))
                return "1 Scrap -> 10 Fuel";

            return $"{fuelSynthesisPerSec:0.#} fuel/sec";
        }

        return string.Empty;
    }

    void UpdateFuelSynthesis(float deltaTime)
    {
        if (!isPlayerShip || deltaTime <= 0f || fuelMax <= 0f || fuelSynthesisPerSec <= 0f)
            return;

        if (!fuelSynthesisActive && fuelCurrent < fuelMax * lowFuelSynthesisThreshold)
            fuelSynthesisActive = true;

        if (!fuelSynthesisActive)
            return;

        if (fuelCurrent >= fuelMax)
        {
            fuelSynthesisActive = false;
            synthesisScrapProgress = 0f;
            return;
        }

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return;

        synthesisScrapProgress += (fuelSynthesisPerSec * deltaTime) / 10f;

        float missingFuel = fuelMax - fuelCurrent;
        if (missingFuel <= 0f)
        {
            synthesisScrapProgress = 0f;
            return;
        }

        int scrapToSpend = Mathf.Min(hud.GetResourceAmount("scrap"), Mathf.FloorToInt(synthesisScrapProgress));
        if (scrapToSpend <= 0)
            return;

        if (!hud.TryConsumeResource("scrap", scrapToSpend))
            return;

        float fuelToAdd = Mathf.Min(missingFuel, scrapToSpend * 10f);
        fuelCurrent = Mathf.Clamp(fuelCurrent + fuelToAdd, 0f, fuelMax);
        synthesisScrapProgress = Mathf.Max(0f, synthesisScrapProgress - scrapToSpend);
    }

    void RefreshHudFuel()
    {
        if (!isPlayerShip)
            return;

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return;

        hud.SetResourceDisplay(
            "fuel",
            "Fuel",
            fuelCurrent,
            FuelColor,
            $"{Mathf.CeilToInt(fuelCurrent)} / {Mathf.CeilToInt(fuelMax)}");
    }
}
