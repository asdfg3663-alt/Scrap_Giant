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
    [Range(0f, 1f)] public float weaponBatteryResumeThreshold = 0.5f;

    [Header("Fuel")]
    public float fuelMax;
    public float fuelCurrent;
    public float fuelSynthesisPerSec;
    public float fuelConsumptionMultiplier = 0.5f;
    public float lowFuelSynthesisThreshold = 0.2f;
    public float emptyFuelThrustMultiplier = 0.1f;
    public float minimumEmergencyThrust = 1f;

    [Header("Movement")]
    public float totalThrust;
    public float totalMass;
    public float totalScore;
    public float totalHeatDissipationPerSec;
    public float totalRepairPerSecond;
    public float repairScrapCostPerHp = 0.1f;

    [Header("Combat (MVP)")]
    public float totalDps;
    public float weaponPowerPerSecPotential;
    public float weaponHeatPerSecPotential;
    public float weaponAmmoPerSecPotential;

    ModuleInstance[] modules;
    bool rebuildQueued;
    float synthesisScrapProgress;
    bool fuelSynthesisActive;
    bool hasInitializedFuelReserve;
    bool weaponBatteryLocked;
    readonly System.Collections.Generic.List<ModuleInstance> repairQueue = new();

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

        if (isPlayerShip)
        {
            PlayerHudRuntime.EnsureForPlayer(this);
            WorldSpawnDirector.RegisterPlayer(this);
            RefreshHudFuel();
        }
    }

    void Update()
    {
        TickModuleHeat(Time.deltaTime);

        powerGenPerSec = ComputeEffectivePowerGeneration();
        netPowerPerSec = powerGenPerSec - powerUsePerSec;
        energyCurrent += netPowerPerSec * Time.deltaTime;
        energyCurrent = Mathf.Clamp(energyCurrent, 0f, energyMax);
        UpdateWeaponBatteryLock();

        ProcessAssemblyQueue(Time.deltaTime);
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
        totalScore = 0f;
        totalHeatDissipationPerSec = 0f;
        totalRepairPerSecond = 0f;
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
            totalScore += m.GetScoreValue();
            totalHeatDissipationPerSec += m.GetHeatDissipationPerSec();
            totalRepairPerSecond += m.GetRepairPerSecond();
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

        if (!hasInitializedFuelReserve && fuelMax > 0f)
        {
            fuelCurrent = fuelMax * WorldSpawnDirector.GetInitialFuelFillRatio();
            hasInitializedFuelReserve = true;
        }
        else
        {
            fuelCurrent = Mathf.Clamp(previousFuelCurrent, 0f, fuelMax);
        }
        synthesisScrapProgress = Mathf.Clamp(synthesisScrapProgress, 0f, 1f);

        if (fuelMax <= 0f)
            fuelSynthesisActive = false;

        PruneRepairQueue();
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

    public bool CanFireWeaponsFromBattery()
    {
        return !weaponBatteryLocked;
    }

    public bool TryConsumeWeaponBattery(float amount)
    {
        if (weaponBatteryLocked)
            return false;

        bool consumed = TryConsumeBattery(amount);
        UpdateWeaponBatteryLock();
        return consumed;
    }

    public void QueueRepair(ModuleInstance module)
    {
        if (!isPlayerShip || module == null || module.data == null)
            return;

        if (module.maxHp <= 0 || module.hp >= module.maxHp)
            return;

        if (repairQueue.Contains(module))
            return;

        repairQueue.Add(module);
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

        fuelCurrent -= totalThrust * Mathf.Max(0f, fuelConsumptionMultiplier) * deltaTime;
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

    public bool HasOperationalModuleType(ModuleType moduleType)
    {
        modules = GetComponentsInChildren<ModuleInstance>(true);
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            if (module.data.type != moduleType)
                continue;

            if (!module.gameObject.activeInHierarchy)
                continue;

            if (module.hp <= 0)
                continue;

            return true;
        }

        return false;
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
        ModuleInstance repairTarget = GetNextRepairTarget();
        if (repairTarget != null)
        {
            if (totalRepairPerSecond > 0f)
                return $"Repairing {repairTarget.DisplayName}";

            return $"Repair queued: {repairTarget.DisplayName}";
        }

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
        ModuleInstance repairTarget = GetNextRepairTarget();
        if (repairTarget != null)
        {
            var hud = PlayerHudRuntime.Instance;
            if (totalRepairPerSecond <= 0f)
                return "Install repair module";

            if (hud == null || !hud.HasResource("scrap", repairScrapCostPerHp))
                return "Need Scrap";

            float repairHp = Mathf.Clamp(repairTarget.hp + repairTarget.repairProgress, 0f, repairTarget.maxHp);
            return $"{repairHp:0.#} / {repairTarget.maxHp} HP";
        }

        if (!HasFuelSystem())
            return string.Empty;

        if (fuelSynthesisActive && fuelCurrent < fuelMax)
        {
            var hud = PlayerHudRuntime.Instance;
            float fuelPerScrap = WorldSpawnDirector.GetFuelPerScrap();
            if (hud == null || !hud.HasResource("scrap", 1f))
                return $"1 Scrap -> {fuelPerScrap:0.#} Fuel";

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

        float fuelPerScrap = WorldSpawnDirector.GetFuelPerScrap();
        synthesisScrapProgress += (fuelSynthesisPerSec * deltaTime) / fuelPerScrap;

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

        float fuelToAdd = Mathf.Min(missingFuel, scrapToSpend * fuelPerScrap);
        fuelCurrent = Mathf.Clamp(fuelCurrent + fuelToAdd, 0f, fuelMax);
        synthesisScrapProgress = Mathf.Max(0f, synthesisScrapProgress - scrapToSpend);
    }

    void ProcessAssemblyQueue(float deltaTime)
    {
        if (TryProcessRepairQueue(deltaTime))
            return;

        UpdateFuelSynthesis(deltaTime);
    }

    bool TryProcessRepairQueue(float deltaTime)
    {
        if (!isPlayerShip || deltaTime <= 0f)
            return false;

        ModuleInstance target = GetNextRepairTarget();
        if (target == null)
            return false;

        if (totalRepairPerSecond <= 0f)
            return false;

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return true;

        float missingHp = Mathf.Max(0f, target.maxHp - (target.hp + target.repairProgress));
        if (missingHp <= 0.001f)
        {
            repairQueue.Remove(target);
            return GetNextRepairTarget() != null;
        }

        float repairAmount = Mathf.Min(missingHp, totalRepairPerSecond * deltaTime);
        float availableScrap = hud.GetResourceValue("scrap");
        float affordableRepair = repairScrapCostPerHp > 0f
            ? availableScrap / repairScrapCostPerHp
            : repairAmount;
        repairAmount = Mathf.Min(repairAmount, affordableRepair);

        if (repairAmount <= 0f)
            return true;

        if (!hud.TryConsumeResource("scrap", repairAmount * repairScrapCostPerHp))
            return true;

        target.repairProgress += repairAmount;
        int wholeHp = Mathf.FloorToInt(target.repairProgress);
        if (wholeHp > 0)
        {
            target.hp = Mathf.Clamp(target.hp + wholeHp, 0, target.maxHp);
            target.repairProgress -= wholeHp;
        }

        if (target.hp >= target.maxHp)
        {
            target.hp = target.maxHp;
            target.repairProgress = 0f;
            repairQueue.Remove(target);
        }

        return true;
    }

    void TickModuleHeat(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        float coolingPerSecond = 1f + Mathf.Max(0f, totalHeatDissipationPerSec);
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            if (module.data.type == ModuleType.Reactor && module.GetPowerGenPerSec() > 0f)
                module.AddHeat(module.GetPowerGenPerSec() * 2f * deltaTime);

            module.CoolHeat(coolingPerSecond * deltaTime);
        }
    }

    float ComputeEffectivePowerGeneration()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        float total = 0f;
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            total += module.GetEffectivePowerGenPerSec();
        }

        return total;
    }

    ModuleInstance GetNextRepairTarget()
    {
        PruneRepairQueue();
        return repairQueue.Count > 0 ? repairQueue[0] : null;
    }

    void PruneRepairQueue()
    {
        repairQueue.RemoveAll(module =>
            module == null ||
            module.data == null ||
            !module.transform.IsChildOf(transform) ||
            module.hp >= module.maxHp);
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

    void UpdateWeaponBatteryLock()
    {
        if (energyMax <= 0f)
        {
            weaponBatteryLocked = false;
            return;
        }

        if (weaponBatteryLocked)
        {
            if (energyCurrent >= energyMax * Mathf.Clamp01(weaponBatteryResumeThreshold))
                weaponBatteryLocked = false;

            return;
        }

        if (energyCurrent <= 0.001f)
            weaponBatteryLocked = true;
    }
}
