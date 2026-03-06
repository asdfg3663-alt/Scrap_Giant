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
            reason = "No module selected";
            return false;
        }

        var ship = module.GetComponentInParent<ShipStats>();
        if (ship == null || !ship.isPlayerShip)
        {
            reason = "PlayerShip only";
            return false;
        }

        if (activeUpgrade != null)
        {
            reason = activeUpgrade.module == module ? "Already upgrading" : "Another module is upgrading";
            return false;
        }

        var hud = PlayerHudRuntime.Instance;
        if (hud == null)
        {
            reason = "HUD unavailable";
            return false;
        }

        int cost = CalculateScrapCost(module);
        if (!hud.HasResource("scrap", cost))
        {
            reason = $"Need {cost} Scrap";
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
        return Mathf.CeilToInt(Mathf.Max(0f, module.data.mass * 10f));
    }

    float CalculateDuration(ModuleInstance module)
    {
        if (module == null || module.data == null) return 0f;
        return Mathf.Max(0.1f, module.data.mass * 3f);
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
            hud.SetAssemblyState(false, "Idle");
            return;
        }

        float progress01 = Mathf.Clamp01(activeUpgrade.elapsed / Mathf.Max(0.01f, activeUpgrade.duration));
        hud.SetAssemblyState(true, $"Upgrade {activeUpgrade.module.DisplayName} {progress01 * 100f:0}%");
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
