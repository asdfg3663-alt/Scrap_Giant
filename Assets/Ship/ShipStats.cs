using UnityEngine;
using System.Collections.Generic;

public enum AssemblyPriorityMode
{
    FuelFirst,
    RepairFirst,
    AmmoFirst
}

public class ShipStats : MonoBehaviour
{
    static readonly Color FuelColor = new Color(0.38f, 0.85f, 0.95f, 1f);
    static readonly Color AmmoColor = new Color(0.96f, 0.32f, 0.24f, 1f);
    const float BaseHeatDissipationPerSecond = 1f;
    const float PowerPlantCriticalHeatThreshold = 0.96f;
    const float PowerPlantOverheatDamagePerSecond = 0.1f;

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

    [Header("Assembly Automation")]
    public AssemblyPriorityMode assemblyPriorityMode = AssemblyPriorityMode.FuelFirst;
    public float ammoSynthesisPerSec = 8f;
    public float ammoPerScrap = 12f;
    public int baseAmmoReserveTarget = 120;
    public int ammoReservePerWeapon = 40;

    [Header("Combat (MVP)")]
    public float totalDps;
    public float weaponPowerPerSecPotential;
    public float weaponHeatPerSecPotential;
    public float weaponAmmoPerSecPotential;

    ModuleInstance[] modules;
    bool rebuildQueued;
    float synthesisScrapProgress;
    float ammoSynthesisScrapProgress;
    bool fuelSynthesisActive;
    bool hasInitializedFuelReserve;
    bool weaponBatteryLocked;
    CoreFallbackLaser coreFallbackLaser;
    readonly System.Collections.Generic.List<ModuleInstance> repairQueue = new();
    readonly Vector2Int[] connectionDirs =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    public AssemblyPriorityMode CurrentAssemblyPriorityMode => assemblyPriorityMode;

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
            EnsureFallbackLaser();
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

    public void SetAssemblyPriorityMode(AssemblyPriorityMode mode)
    {
        assemblyPriorityMode = mode;
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
            weaponHeatPerSecPotential += Mathf.Max(0f, m.GetWeaponHeatPerSecondPotential());
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
        ammoSynthesisScrapProgress = Mathf.Clamp(ammoSynthesisScrapProgress, 0f, 1f);

        if (fuelMax <= 0f)
            fuelSynthesisActive = false;

        PruneRepairQueue();

        if (isPlayerShip)
            EnsureFallbackLaser();

        RefreshCollisionHull();
    }

    void RefreshCollisionHull()
    {
        ShipCollisionHull2D hull = GetComponent<ShipCollisionHull2D>();
        if (hull == null)
            hull = gameObject.AddComponent<ShipCollisionHull2D>();

        hull.RebuildHull(modules);
    }

    public void RefreshHudNow()
    {
        RefreshHudFuel();
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

    public void HandleOwnedModuleDestroyed(ModuleInstance destroyedModule, Transform destroyedTransform, Vector2 hitPoint, Vector2 hitNormal)
    {
        if (!isPlayerShip || destroyedTransform == null)
            return;

        ModuleAttachment destroyedAttachment = destroyedTransform.GetComponent<ModuleAttachment>();
        if (destroyedAttachment != null)
        {
            destroyedAttachment.shipRoot = null;
            destroyedAttachment.gridPos = default;
            destroyedAttachment.rot90 = 0;
        }

        DisableModuleForRemoval(destroyedTransform);
        destroyedTransform.SetParent(null, true);
        AudioRuntime.PlayPlayerModuleBreak();

        if (destroyedModule != null && destroyedModule.data != null && destroyedModule.data.type != ModuleType.Core)
            DetachDisconnectedModules(destroyedModule, hitPoint, hitNormal);

        rebuildQueued = false;
        Rebuild();
        RefreshHudFuel();
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
        return GetEffectiveThrust(totalThrust);
    }

    public float GetEffectiveThrust(float requestedThrust)
    {
        float clampedRequestedThrust = Mathf.Clamp(requestedThrust, 0f, Mathf.Max(0f, totalThrust));
        if (clampedRequestedThrust <= 0f || totalThrust <= 0f)
            return 0f;

        if (fuelCurrent > 0.001f)
            return clampedRequestedThrust;

        float effectiveTotalThrust = Mathf.Max(minimumEmergencyThrust, totalThrust * emptyFuelThrustMultiplier);
        float thrustScale = effectiveTotalThrust / totalThrust;
        return clampedRequestedThrust * thrustScale;
    }

    public void ConsumeFuelForThrust(float activeThrust, float deltaTime)
    {
        if (deltaTime <= 0f || activeThrust <= 0f || fuelCurrent <= 0f)
            return;

        fuelCurrent -= activeThrust * Mathf.Max(0f, fuelConsumptionMultiplier) * deltaTime;
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

    public Vector2 GetNearestModulePoint(Vector2 fromPosition)
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        Vector2 bestPoint = transform.position;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            if (!module.gameObject.activeInHierarchy || module.hp <= 0)
                continue;

            Vector2 point = module.transform.position;
            float distanceSq = (point - fromPosition).sqrMagnitude;
            if (distanceSq >= bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            bestPoint = point;
        }

        return bestPoint;
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

    public bool HasOperationalWeaponModules()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            bool isWeapon =
                module.data.type == ModuleType.Weapon ||
                module.data.weaponType != WeaponType.None ||
                module.data.dps > 0f;
            if (!isWeapon)
                continue;

            if (!module.gameObject.activeInHierarchy || module.hp <= 0)
                continue;

            return true;
        }

        return false;
    }

    public bool HasCriticalOverheatModules(float threshold = 0.95f)
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        float clampedThreshold = Mathf.Clamp01(threshold);
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            if (module.GetMaxHeat() <= 0f)
                continue;

            if (module.GetHeatRatio() >= clampedThreshold)
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

    public int GetDesiredAmmoReserve()
    {
        return Mathf.Max(0, baseAmmoReserveTarget + GetOperationalWeaponCount() * ammoReservePerWeapon);
    }

    public string GetFuelAssemblyPrimaryText()
    {
        var hud = PlayerHudRuntime.Instance;
        ModuleInstance repairTarget = GetNextRepairTarget();
        bool fuelWanted = WantsFuelSynthesis(hud);
        bool ammoWanted = WantsAmmoProduction(hud);

        switch (assemblyPriorityMode)
        {
            case AssemblyPriorityMode.RepairFirst:
                if (repairTarget != null)
                {
                    if (totalRepairPerSecond > 0f)
                        return LocalizationManager.Format("assembly.repairing", "Repairing {0}", repairTarget.DisplayName);

                    return LocalizationManager.Format("assembly.repair_queued", "Repair queued: {0}", repairTarget.DisplayName);
                }

                if (fuelWanted)
                    return GetFuelStatusPrimaryText(hud);

                if (ammoWanted)
                    return GetAmmoStatusPrimaryText(hud);
                break;

            case AssemblyPriorityMode.AmmoFirst:
                if (ammoWanted)
                    return GetAmmoStatusPrimaryText(hud);

                if (fuelWanted)
                    return GetFuelStatusPrimaryText(hud);

                if (repairTarget != null)
                {
                    if (totalRepairPerSecond > 0f)
                        return LocalizationManager.Format("assembly.repairing", "Repairing {0}", repairTarget.DisplayName);

                    return LocalizationManager.Format("assembly.repair_queued", "Repair queued: {0}", repairTarget.DisplayName);
                }
                break;

            default:
                if (fuelWanted)
                    return GetFuelStatusPrimaryText(hud);

                if (repairTarget != null)
                {
                    if (totalRepairPerSecond > 0f)
                        return LocalizationManager.Format("assembly.repairing", "Repairing {0}", repairTarget.DisplayName);

                    return LocalizationManager.Format("assembly.repair_queued", "Repair queued: {0}", repairTarget.DisplayName);
                }

                if (ammoWanted)
                    return GetAmmoStatusPrimaryText(hud);
                break;
        }

        if (!HasFuelSystem())
            return LocalizationManager.Get("assembly.install_fuel_tank", "Install a fuel tank");

        if (repairTarget != null && totalRepairPerSecond <= 0f)
            return LocalizationManager.Get("assembly.install_repair_module", "Install repair module");

        if (ammoWanted)
            return LocalizationManager.Get("assembly.ammo_ready", "Ammo reserve ready");

        return LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready");
    }

    public string GetFuelAssemblySecondaryText()
    {
        var hud = PlayerHudRuntime.Instance;
        ModuleInstance repairTarget = GetNextRepairTarget();
        bool fuelWanted = WantsFuelSynthesis(hud);
        bool ammoWanted = WantsAmmoProduction(hud);

        switch (assemblyPriorityMode)
        {
            case AssemblyPriorityMode.RepairFirst:
                if (repairTarget != null)
                {
                    if (totalRepairPerSecond <= 0f)
                        return LocalizationManager.Get("assembly.install_repair_module", "Install repair module");

                    if (hud == null || !hud.HasResource("scrap", repairScrapCostPerHp))
                        return LocalizationManager.Get("assembly.need_scrap", "Need Scrap");

                    float repairHp = Mathf.Clamp(repairTarget.hp + repairTarget.repairProgress, 0f, repairTarget.maxHp);
                    return LocalizationManager.Format("info.hp", "HP: {0} / {1}", repairHp.ToString("0.#"), repairTarget.maxHp);
                }

                if (fuelWanted)
                    return GetFuelStatusSecondaryText(hud);

                if (ammoWanted)
                    return GetAmmoStatusSecondaryText(hud);
                break;

            case AssemblyPriorityMode.AmmoFirst:
                if (ammoWanted)
                    return GetAmmoStatusSecondaryText(hud);

                if (fuelWanted)
                    return GetFuelStatusSecondaryText(hud);

                if (repairTarget != null)
                {
                    if (totalRepairPerSecond <= 0f)
                        return LocalizationManager.Get("assembly.install_repair_module", "Install repair module");

                    if (hud == null || !hud.HasResource("scrap", repairScrapCostPerHp))
                        return LocalizationManager.Get("assembly.need_scrap", "Need Scrap");

                    float repairHp = Mathf.Clamp(repairTarget.hp + repairTarget.repairProgress, 0f, repairTarget.maxHp);
                    return LocalizationManager.Format("info.hp", "HP: {0} / {1}", repairHp.ToString("0.#"), repairTarget.maxHp);
                }
                break;

            default:
                if (fuelWanted)
                    return GetFuelStatusSecondaryText(hud);

                if (repairTarget != null)
                {
                    if (totalRepairPerSecond <= 0f)
                        return LocalizationManager.Get("assembly.install_repair_module", "Install repair module");

                    if (hud == null || !hud.HasResource("scrap", repairScrapCostPerHp))
                        return LocalizationManager.Get("assembly.need_scrap", "Need Scrap");

                    float repairHp = Mathf.Clamp(repairTarget.hp + repairTarget.repairProgress, 0f, repairTarget.maxHp);
                    return LocalizationManager.Format("info.hp", "HP: {0} / {1}", repairHp.ToString("0.#"), repairTarget.maxHp);
                }

                if (ammoWanted)
                    return GetAmmoStatusSecondaryText(hud);
                break;
        }

        return string.Empty;
    }

    bool TryUpdateFuelSynthesis(float deltaTime)
    {
        if (!isPlayerShip || deltaTime <= 0f || fuelMax <= 0f || fuelSynthesisPerSec <= 0f)
            return false;

        if (!fuelSynthesisActive && fuelCurrent < fuelMax * lowFuelSynthesisThreshold)
            fuelSynthesisActive = true;

        if (!fuelSynthesisActive)
            return false;

        if (fuelCurrent >= fuelMax)
        {
            fuelSynthesisActive = false;
            synthesisScrapProgress = 0f;
            return false;
        }

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return false;

        float missingFuel = fuelMax - fuelCurrent;
        if (missingFuel <= 0f)
        {
            synthesisScrapProgress = 0f;
            return false;
        }

        int availableScrap = hud.GetResourceAmount("scrap");
        if (availableScrap <= 0)
            return false;

        float fuelPerScrap = WorldSpawnDirector.GetFuelPerScrap();
        synthesisScrapProgress = Mathf.Min(
            synthesisScrapProgress + (fuelSynthesisPerSec * deltaTime) / fuelPerScrap,
            availableScrap);

        int scrapToSpend = Mathf.Min(availableScrap, Mathf.FloorToInt(synthesisScrapProgress));
        if (scrapToSpend <= 0)
            return false;

        if (!hud.TryConsumeResource("scrap", scrapToSpend))
            return false;

        float fuelToAdd = Mathf.Min(missingFuel, scrapToSpend * fuelPerScrap);
        fuelCurrent = Mathf.Clamp(fuelCurrent + fuelToAdd, 0f, fuelMax);
        synthesisScrapProgress = Mathf.Max(0f, synthesisScrapProgress - scrapToSpend);
        return fuelToAdd > 0f;
    }

    bool TryProduceAmmo(float deltaTime)
    {
        if (!isPlayerShip || deltaTime <= 0f || ammoPerScrap <= 0f || ammoSynthesisPerSec <= 0f)
            return false;

        var hud = PlayerHudRuntime.Instance;
        if (!WantsAmmoProduction(hud) || hud == null)
            return false;

        int currentAmmo = hud.GetAmmoAmount("ammo");
        int ammoTarget = GetDesiredAmmoReserve();
        int missingAmmo = Mathf.Max(0, ammoTarget - currentAmmo);
        if (missingAmmo <= 0)
        {
            ammoSynthesisScrapProgress = 0f;
            return false;
        }

        int availableScrap = hud.GetResourceAmount("scrap");
        if (availableScrap <= 0)
            return false;

        ammoSynthesisScrapProgress = Mathf.Min(
            ammoSynthesisScrapProgress + (ammoSynthesisPerSec * deltaTime) / ammoPerScrap,
            availableScrap);

        int maxScrapNeeded = Mathf.Max(1, Mathf.CeilToInt(missingAmmo / ammoPerScrap));
        int scrapToSpend = Mathf.Min(availableScrap, Mathf.FloorToInt(ammoSynthesisScrapProgress), maxScrapNeeded);
        if (scrapToSpend <= 0)
            return false;

        if (!hud.TryConsumeResource("scrap", scrapToSpend))
            return false;

        int ammoToAdd = Mathf.Min(missingAmmo, Mathf.RoundToInt(scrapToSpend * ammoPerScrap));
        if (ammoToAdd <= 0)
            return false;

        hud.SetAmmo(
            "ammo",
            LocalizationManager.Get("resource.ammo", "Ammo"),
            currentAmmo + ammoToAdd,
            AmmoColor);
        ammoSynthesisScrapProgress = Mathf.Max(0f, ammoSynthesisScrapProgress - scrapToSpend);
        return true;
    }

    bool WantsFuelSynthesis(PlayerHudRuntime hud)
    {
        if (!HasFuelSystem() || fuelSynthesisPerSec <= 0f || fuelCurrent >= fuelMax)
            return false;

        if (!fuelSynthesisActive && fuelCurrent < fuelMax * lowFuelSynthesisThreshold)
            fuelSynthesisActive = true;

        if (!fuelSynthesisActive)
            return false;

        return true;
    }

    bool WantsAmmoProduction(PlayerHudRuntime hud)
    {
        if (hud == null || ammoPerScrap <= 0f || ammoSynthesisPerSec <= 0f)
            return false;

        return hud.GetAmmoAmount("ammo") < GetDesiredAmmoReserve();
    }

    string GetFuelStatusSecondaryText(PlayerHudRuntime hud)
    {
        float fuelPerScrap = WorldSpawnDirector.GetFuelPerScrap();
        if (hud == null || !hud.HasResource("scrap", 1f))
            return LocalizationManager.Format("assembly.scrap_to_fuel", "1 Scrap -> {0} Fuel", fuelPerScrap.ToString("0.#"));

        return LocalizationManager.Format("assembly.fuel_rate", "{0} fuel/sec", fuelSynthesisPerSec.ToString("0.#"));
    }

    string GetFuelStatusPrimaryText(PlayerHudRuntime hud)
    {
        if (hud == null || !hud.HasResource("scrap", 1f))
            return LocalizationManager.Get("assembly.low_fuel_need_scrap", "Low fuel: need Scrap");

        return LocalizationManager.Get("assembly.fuel_active", "Fuel synthesis active");
    }

    string GetAmmoStatusSecondaryText(PlayerHudRuntime hud)
    {
        int targetAmmo = GetDesiredAmmoReserve();
        int currentAmmo = hud != null ? hud.GetAmmoAmount("ammo") : 0;
        if (hud == null || !hud.HasResource("scrap", 1f))
            return LocalizationManager.Format("assembly.scrap_to_ammo", "1 Scrap -> {0} Ammo", ammoPerScrap.ToString("0.#"));

        return LocalizationManager.Format("assembly.ammo_status", "Ammo {0} / {1}", currentAmmo, targetAmmo);
    }

    string GetAmmoStatusPrimaryText(PlayerHudRuntime hud)
    {
        if (hud == null || !hud.HasResource("scrap", 1f))
            return LocalizationManager.Get("assembly.need_scrap", "Need Scrap");

        return LocalizationManager.Get("assembly.ammo_active", "Ammo production active");
    }

    int GetOperationalWeaponCount()
    {
        if (modules == null || modules.Length == 0)
            modules = GetComponentsInChildren<ModuleInstance>(true);

        int count = 0;
        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            bool isWeapon =
                module.data.type == ModuleType.Weapon ||
                module.data.weaponType != WeaponType.None ||
                module.data.dps > 0f;
            if (!isWeapon)
                continue;

            if (!module.gameObject.activeInHierarchy || module.hp <= 0)
                continue;

            count++;
        }

        return count;
    }

    void ProcessAssemblyQueue(float deltaTime)
    {
        switch (assemblyPriorityMode)
        {
            case AssemblyPriorityMode.RepairFirst:
                if (TryProcessRepairQueue(deltaTime))
                    return;
                if (TryUpdateFuelSynthesis(deltaTime))
                    return;
                TryProduceAmmo(deltaTime);
                return;

            case AssemblyPriorityMode.AmmoFirst:
                if (TryProduceAmmo(deltaTime))
                    return;
                if (TryUpdateFuelSynthesis(deltaTime))
                    return;
                TryProcessRepairQueue(deltaTime);
                return;

            default:
                if (TryUpdateFuelSynthesis(deltaTime))
                    return;
                if (TryProcessRepairQueue(deltaTime))
                    return;
                TryProduceAmmo(deltaTime);
                return;
        }
    }

    void EnsureFallbackLaser()
    {
        if (!isPlayerShip)
            return;

        if (coreFallbackLaser == null)
            coreFallbackLaser = GetComponent<CoreFallbackLaser>();

        if (coreFallbackLaser == null)
            coreFallbackLaser = gameObject.AddComponent<CoreFallbackLaser>();
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
            return false;

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
            return false;

        if (!hud.TryConsumeResource("scrap", repairAmount * repairScrapCostPerHp))
            return false;

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

        int heatSourceCount = CountHeatSourceModules();
        float radiatorCoolingPerHeatSource = heatSourceCount > 0
            ? Mathf.Max(0f, totalHeatDissipationPerSec) / heatSourceCount
            : Mathf.Max(0f, totalHeatDissipationPerSec);

        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null || module.data == null)
                continue;

            float passiveHeat = module.GetPassiveHeatPerSecond();
            if (passiveHeat > 0f)
                module.AddHeat(passiveHeat * deltaTime);

            float coolingPerSecond = BaseHeatDissipationPerSecond;
            if (module.IsHeatSourceModule())
                coolingPerSecond += radiatorCoolingPerHeatSource;

            module.CoolHeat(coolingPerSecond * deltaTime);
            TickPlayerPowerPlantOverheatDamage(module, deltaTime);
        }
    }

    int CountHeatSourceModules()
    {
        if (modules == null || modules.Length == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null || module.hp <= 0 || !module.gameObject.activeInHierarchy)
                continue;

            if (module.IsHeatSourceModule())
                count++;
        }

        return count;
    }

    void TickPlayerPowerPlantOverheatDamage(ModuleInstance module, float deltaTime)
    {
        if (!isPlayerShip || module == null || module.data == null || module.data.type != ModuleType.Reactor)
            return;

        if (module.hp <= 0)
            return;

        if (module.GetHeatRatio() < PowerPlantCriticalHeatThreshold)
        {
            module.overheatDamageProgress = 0f;
            return;
        }

        module.overheatDamageProgress += PowerPlantOverheatDamagePerSecond * deltaTime;
        int damage = Mathf.FloorToInt(module.overheatDamageProgress);
        if (damage <= 0)
            return;

        module.overheatDamageProgress -= damage;
        module.hp = Mathf.Max(0, module.hp - damage);
        if (module.hp > 0)
            return;

        ModuleHP moduleHp = module.GetComponent<ModuleHP>();
        if (moduleHp != null)
        {
            Vector2 hitNormal = Random.insideUnitCircle;
            if (hitNormal.sqrMagnitude < 0.0001f)
                hitNormal = Vector2.up;

            moduleHp.ForceDestroy(module.transform.position, hitNormal.normalized);
            return;
        }

        HandleOwnedModuleDestroyed(module, module.transform, module.transform.position, Vector2.up);
        Destroy(module.gameObject);
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
            LocalizationManager.Get("resource.fuel", "Fuel"),
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

    void DisableModuleForRemoval(Transform moduleTransform)
    {
        var colliders = moduleTransform.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
                colliders[i].enabled = false;

        var renderers = moduleTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = false;
    }

    void DetachDisconnectedModules(ModuleInstance destroyedModule, Vector2 hitPoint, Vector2 hitNormal)
    {
        ModuleInstance[] currentModules = GetComponentsInChildren<ModuleInstance>(true);
        Dictionary<Vector2Int, ModuleInstance> byGrid = new();
        ModuleInstance coreModule = null;

        for (int i = 0; i < currentModules.Length; i++)
        {
            ModuleInstance module = currentModules[i];
            if (module == null || module == destroyedModule || module.data == null)
                continue;

            ModuleAttachment attachment = module.GetComponent<ModuleAttachment>();
            if (attachment == null || attachment.shipRoot != transform)
                continue;

            if (!byGrid.ContainsKey(attachment.gridPos))
                byGrid.Add(attachment.gridPos, module);

            if (module.data.type == ModuleType.Core && coreModule == null)
                coreModule = module;
        }

        if (coreModule == null)
            return;

        ModuleAttachment coreAttachment = coreModule.GetComponent<ModuleAttachment>();
        if (coreAttachment == null)
            return;

        HashSet<Vector2Int> connected = new();
        Queue<Vector2Int> frontier = new();
        frontier.Enqueue(coreAttachment.gridPos);
        connected.Add(coreAttachment.gridPos);

        while (frontier.Count > 0)
        {
            Vector2Int grid = frontier.Dequeue();
            for (int i = 0; i < connectionDirs.Length; i++)
            {
                Vector2Int next = grid + connectionDirs[i];
                if (connected.Contains(next) || !byGrid.ContainsKey(next))
                    continue;

                connected.Add(next);
                frontier.Enqueue(next);
            }
        }

        for (int i = 0; i < currentModules.Length; i++)
        {
            ModuleInstance module = currentModules[i];
            if (module == null || module == destroyedModule || module == coreModule || module.data == null)
                continue;

            ModuleAttachment attachment = module.GetComponent<ModuleAttachment>();
            if (attachment == null || attachment.shipRoot != transform)
                continue;

            if (connected.Contains(attachment.gridPos))
                continue;

            ScatterDetachedModule(module.transform, hitPoint, hitNormal);
        }
    }

    void ScatterDetachedModule(Transform moduleTransform, Vector2 hitPoint, Vector2 hitNormal)
    {
        moduleTransform.SetParent(null, true);

        ModuleAttachment attachment = moduleTransform.GetComponent<ModuleAttachment>();
        if (attachment != null)
        {
            attachment.shipRoot = null;
            attachment.gridPos = default;
            attachment.rot90 = 0;
        }

        WorldSpawnDirector.NeutralizeDetachedModule(moduleTransform);

        Rigidbody2D rb = moduleTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.None;

            Vector2 dir = ((Vector2)moduleTransform.position - hitPoint).normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Random.insideUnitCircle.normalized;

            rb.linearVelocity *= 0.2f;
            rb.angularVelocity *= 0.2f;
            rb.AddForce(dir * 0.9f, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-0.35f, 0.35f), ForceMode2D.Impulse);
        }

        WorldDistanceDespawn despawn = moduleTransform.GetComponent<WorldDistanceDespawn>();
        if (despawn == null)
            despawn = moduleTransform.gameObject.AddComponent<WorldDistanceDespawn>();

        despawn.axisLimit = WorldSpawnDirector.CurrentDespawnAxisLimit;
    }
}
