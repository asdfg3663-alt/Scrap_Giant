using UnityEngine;

[DisallowMultipleComponent]
public class ModuleUpgradeSystem : MonoBehaviour
{
    public struct UpgradeInfo
    {
        public ModuleInstance module;
        public string moduleName;
        public int currentTier;
        public int targetTier;
        public int scrapCost;
        public float duration;
        public float progress01;
    }

    sealed class ActiveUpgrade
    {
        public ModuleInstance module;
        public ShipStats ship;
        public int scrapCost;
        public float duration;
        public float elapsed;
    }

    static ModuleUpgradeSystem instance;

    ActiveUpgrade activeUpgrade;
    AudioSource audioSource;
    AudioClip completionClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    public static ModuleUpgradeSystem Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("ModuleUpgradeSystem");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ModuleUpgradeSystem>();
            }

            return instance;
        }
    }

    public bool IsBusy => activeUpgrade != null;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        completionClip = CreateCompletionClip();
    }

    void Update()
    {
        if (activeUpgrade == null)
        {
            UpdateHudState();
            return;
        }

        if (activeUpgrade.module == null || activeUpgrade.ship == null)
        {
            ClearUpgrade();
            return;
        }

        activeUpgrade.elapsed += Time.deltaTime;
        UpdateHudState();

        if (activeUpgrade.elapsed >= activeUpgrade.duration)
            CompleteUpgrade();
    }

    public UpgradeInfo GetUpgradeInfo(ModuleInstance module)
    {
        UpgradeInfo info = new UpgradeInfo
        {
            module = module,
            moduleName = module != null ? module.DisplayName : "Module",
            currentTier = module != null ? module.CurrentTier : 1,
            targetTier = module != null ? module.CurrentTier + 1 : 2,
            scrapCost = CalculateScrapCost(module),
            duration = CalculateDuration(module),
            progress01 = module != null && IsUpgrading(module)
                ? Mathf.Clamp01(activeUpgrade.elapsed / Mathf.Max(0.01f, activeUpgrade.duration))
                : 0f
        };

        return info;
    }

    public bool IsUpgrading(ModuleInstance module)
    {
        return module != null && activeUpgrade != null && activeUpgrade.module == module;
    }

    public bool CanStartUpgrade(ModuleInstance module, out string reason)
    {
        reason = string.Empty;

        if (module == null || module.data == null)
        {
            reason = LocalizationManager.Get("upgrade.reason.no_selection", "No module selected");
            return false;
        }

        var ship = module.GetComponentInParent<ShipStats>();
        if (ship == null || !ship.isPlayerShip)
        {
            reason = LocalizationManager.Get("upgrade.reason.player_only", "Player ship only");
            return false;
        }

        if (activeUpgrade != null)
        {
            reason = activeUpgrade.module == module
                ? LocalizationManager.Get("upgrade.reason.already_upgrading", "Already upgrading")
                : LocalizationManager.Get("upgrade.reason.another_upgrading", "Another module is upgrading");
            return false;
        }

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
        {
            reason = LocalizationManager.Get("upgrade.reason.hud_unavailable", "HUD unavailable");
            return false;
        }

        int cost = CalculateScrapCost(module);
        if (!hud.HasResource("scrap", cost))
        {
            reason = LocalizationManager.Format("upgrade.reason.need_scrap", "Need {0} Scrap", cost);
            return false;
        }

        return true;
    }

    public bool StartUpgrade(ModuleInstance module)
    {
        string _;
        if (!CanStartUpgrade(module, out _))
            return false;

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return false;

        int cost = CalculateScrapCost(module);
        if (!hud.TryConsumeResource("scrap", cost))
            return false;

        activeUpgrade = new ActiveUpgrade
        {
            module = module,
            ship = module.GetComponentInParent<ShipStats>(),
            scrapCost = cost,
            duration = CalculateDuration(module),
            elapsed = 0f
        };

        UpdateHudState();
        return true;
    }

    int CalculateScrapCost(ModuleInstance module)
    {
        if (module == null || module.data == null) return 0;

        float baseCost = Mathf.Max(0f, module.data.mass * Mathf.Max(0f, module.data.upgradeScrapCostMassMultiplier));
        float tierScale = Mathf.Pow(
            Mathf.Max(0.01f, module.data.upgradeScrapCostPerTierMultiplier),
            Mathf.Max(0, module.upgradeLevel));
        return Mathf.Max(1, Mathf.CeilToInt(baseCost * tierScale));
    }

    float CalculateDuration(ModuleInstance module)
    {
        if (module == null || module.data == null) return 0f;

        float duration = Mathf.Max(0.1f, module.GetMass() * 3f);
        return duration / GetCoreUpgradeSpeedMultiplier(module);
    }

    void CompleteUpgrade()
    {
        if (activeUpgrade == null || activeUpgrade.module == null)
        {
            ClearUpgrade();
            return;
        }

        activeUpgrade.module.ApplyUpgrade(1);

        if (activeUpgrade.ship != null)
            activeUpgrade.ship.ScheduleRebuild();

        if (completionClip != null)
            audioSource.PlayOneShot(completionClip);

        ClearUpgrade();
    }

    void ClearUpgrade()
    {
        activeUpgrade = null;
        UpdateHudState();
    }

    void UpdateHudState()
    {
        var hud = PlayerHudRuntime.Instance;
        if (hud == null) return;

        if (activeUpgrade == null || activeUpgrade.module == null)
        {
            var ship = hud.TrackedShip;
            string primary = ship != null
                ? ship.GetFuelAssemblyPrimaryText()
                : LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready");
            string secondary = ship != null ? ship.GetFuelAssemblySecondaryText() : string.Empty;
            hud.SetAssemblyState(false, primary, secondary, null);
            return;
        }

        float progress01 = Mathf.Clamp01(activeUpgrade.elapsed / Mathf.Max(0.01f, activeUpgrade.duration));
        hud.SetAssemblyState(
            true,
            activeUpgrade.module.DisplayName,
            LocalizationManager.Format("assembly.complete_percent", "{0:0}% complete", progress01 * 100f),
            GetModuleSprite(activeUpgrade.module));
    }

    Sprite GetModuleSprite(ModuleInstance module)
    {
        if (module == null) return null;

        var renderer = module.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    float GetCoreUpgradeSpeedMultiplier(ModuleInstance module)
    {
        if (module == null)
            return 1f;

        ShipStats ship = module.GetComponentInParent<ShipStats>();
        if (ship == null)
            return 1f;

        ModuleInstance[] installedModules = ship.GetComponentsInChildren<ModuleInstance>(true);
        int highestCoreTier = 1;
        for (int i = 0; i < installedModules.Length; i++)
        {
            ModuleInstance installedModule = installedModules[i];
            if (installedModule == null || installedModule.data == null || installedModule.data.type != ModuleType.Core)
                continue;

            highestCoreTier = Mathf.Max(highestCoreTier, installedModule.CurrentTier);
        }

        return Mathf.Pow(2f, Mathf.Max(0, highestCoreTier - 1));
    }

    AudioClip CreateCompletionClip()
    {
        const int sampleRate = 44100;
        const float durationSeconds = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(1f - (t / durationSeconds));
            float freq =
                t < 0.14f ? 660f :
                t < 0.28f ? 880f :
                1100f;

            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.18f;
        }

        var clip = AudioClip.Create("ModuleUpgradeComplete", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
