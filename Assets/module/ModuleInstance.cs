using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ModuleInstance : MonoBehaviour
{
    const string TierGlowObjectName = "TierGlow";
    const string TierAuraRootObjectName = "TierAura";
    const float TierAuraPixelRadius = 5f;
    const float HeatPenaltyThreshold = 0.8f;
    const float HeatCriticalThreshold = 0.95f;

    static readonly Vector2[] TierAuraDirections =
    {
        new Vector2(1f, 0f),
        new Vector2(-1f, 0f),
        new Vector2(0f, 1f),
        new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f),
        new Vector2(-0.7071f, 0.7071f),
        new Vector2(-0.7071f, -0.7071f)
    };

    public ModuleData data;

    [Header("Runtime (per-instance)")]
    public int hp;
    public int maxHp;
    public bool resetHpOnDataAssign = true;
    public float currentHeat;
    public float repairProgress;
    public float overheatDamageProgress;

    [Header("Upgrade")]
    [Min(0)] public int upgradeLevel = 0;

    [NonSerialized] readonly Dictionary<int, Color> baseRendererColors = new();
    [NonSerialized] SpriteRenderer[] tierRenderers = Array.Empty<SpriteRenderer>();
    [NonSerialized] SpriteRenderer primaryRenderer;
    [NonSerialized] SpriteRenderer tierGlowRenderer;
    [NonSerialized] Transform tierAuraRoot;

    public int CurrentTier => data != null ? Mathf.Max(1, data.tier + upgradeLevel) : Mathf.Max(1, upgradeLevel);
    public string DisplayName => GetLocalizedDisplayName();
    public float TierMultiplier => GetTierStatMultiplier(2f);

    void Awake()
    {
        SyncFromDataIfNeeded(forceReset: false);
        ApplyTierVisuals();
    }

    void OnValidate()
    {
        SyncFromDataIfNeeded(forceReset: false);
        ApplyTierVisuals();
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
        overheatDamageProgress = Mathf.Max(0f, overheatDamageProgress);
    }

    public void ApplyUpgrade(int amount = 1)
    {
        upgradeLevel = Mathf.Max(0, upgradeLevel + amount);
        SyncFromDataIfNeeded(forceReset: false);
        ApplyTierVisuals();
    }

    public void RefreshVisualState(bool forceReset = false)
    {
        SyncFromDataIfNeeded(forceReset);
        ApplyTierVisuals();
    }

    public int GetMaxHp()
    {
        if (data == null)
            return Mathf.Max(1, maxHp);

        return Mathf.Max(1, Mathf.RoundToInt(data.maxHP * GetTierStatMultiplier(data.hpPerTierMultiplier)));
    }

    public float GetPowerGenPerSec() { return data != null ? data.powerGenPerSec * GetTierStatMultiplier(data.powerGenPerTierMultiplier) : 0f; }
    public float GetEffectivePowerGenPerSec() { return GetPowerGenPerSec() * GetHeatEfficiencyMultiplierForPower(); }
    public float GetPowerUsePerSec() { return data != null ? data.powerUsePerSec * GetTierStatMultiplier(data.powerUsePerTierMultiplier) : 0f; }
    public float GetMaxEnergy() { return data != null ? data.maxEnergy * GetTierStatMultiplier(data.energyPerTierMultiplier) : 0f; }
    public float GetMaxFuel() { return data != null ? data.maxFuel * GetTierStatMultiplier(data.fuelPerTierMultiplier) : 0f; }
    public float GetFuelSynthesisPerSec() { return data != null ? data.fuelSynthesisPerSec * GetTierStatMultiplier(data.fuelSynthesisPerTierMultiplier) : 0f; }
    public float GetThrust() { return data != null ? data.thrust * GetTierStatMultiplier(data.thrustPerTierMultiplier) : 0f; }
    public float GetMass() { return data != null ? data.mass * GetTierStatMultiplier(data.massPerTierMultiplier) : 0f; }
    public float GetScoreValue()
    {
        if (data == null)
            return 0f;

        return GetMass() * Mathf.Max(0f, data.scoreMultiplier) * GetTierStatMultiplier(data.scorePerTierMultiplier);
    }
    public float GetWeaponDamage() { return data != null ? data.weaponDamage * GetTierStatMultiplier(data.weaponDamagePerTierMultiplier) : 0f; }
    public float GetEffectiveWeaponDamage() { return GetWeaponDamage() * GetHeatEfficiencyMultiplierForWeapon(); }
    public float GetWeaponFireRate() { return data != null ? data.weaponFireRate * GetTierStatMultiplier(data.weaponFireRatePerTierMultiplier) : 0f; }
    public float GetWeaponPowerPerShot() { return data != null ? data.weaponPowerPerShot * GetTierStatMultiplier(data.weaponPowerPerShotPerTierMultiplier) : 0f; }
    public float GetWeaponHeatPerShot() { return data != null ? data.weaponHeatPerShot * GetTierStatMultiplier(data.weaponHeatPerShotPerTierMultiplier) : 0f; }
    public float GetWeaponAmmoPerShot() { return data != null ? data.weaponAmmoPerShot * GetTierStatMultiplier(data.weaponAmmoPerShotPerTierMultiplier) : 0f; }
    public float GetMaxHeat() { return data != null ? data.maxHeat * GetTierStatMultiplier(data.maxHeatPerTierMultiplier) : 0f; }
    public float GetHeatDissipationPerSec() { return data != null ? data.heatDissipationPerSec * GetTierStatMultiplier(data.heatDissipationPerTierMultiplier) : 0f; }
    public float GetRepairPerSecond() { return data != null ? data.repairPerSecond * GetTierStatMultiplier(data.repairPerTierMultiplier) : 0f; }

    public float GetDps()
    {
        if (data == null) return 0f;

        float heatMultiplier = GetHeatEfficiencyMultiplierForWeapon();
        if (data.dps > 0f)
            return data.dps * GetTierStatMultiplier(data.dpsPerTierMultiplier) * heatMultiplier;

        return Mathf.Max(0f, GetWeaponDamage()) * Mathf.Max(0f, GetWeaponFireRate()) * heatMultiplier;
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
            return GetEffectivePowerGenPerSec() * 2f;

        return 0f;
    }

    public float GetTotalHeatGenerationPerSecondPotential()
    {
        return GetPassiveHeatPerSecond() + GetWeaponHeatPerSecondPotential();
    }

    public bool IsHeatSourceModule()
    {
        return GetTotalHeatGenerationPerSecondPotential() > 0.001f;
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

        return GetDiscreteHeatEfficiencyMultiplier();
    }

    float GetHeatEfficiencyMultiplierForPower()
    {
        if (data == null || data.type != ModuleType.Reactor || GetMaxHeat() <= 0f)
            return 1f;

        return GetDiscreteHeatEfficiencyMultiplier();
    }

    float GetDiscreteHeatEfficiencyMultiplier()
    {
        float heatRatio = GetHeatRatio();
        if (heatRatio >= HeatCriticalThreshold)
            return 0.25f;

        if (heatRatio >= HeatPenaltyThreshold)
            return 0.5f;

        return 1f;
    }

    string GetLocalizedDisplayName()
    {
        string fallback = FormatDisplayName(data != null ? data.displayName : "Module", CurrentTier);
        return LocalizationManager.GetModuleText(data != null ? data.localizationKey : string.Empty, fallback);
    }

    float GetTierStatMultiplier(float perTierMultiplier)
    {
        int tierSteps = Mathf.Max(0, upgradeLevel);
        if (tierSteps <= 0)
            return 1f;

        return Mathf.Pow(Mathf.Max(0.01f, perTierMultiplier), tierSteps);
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

    void ApplyTierVisuals()
    {
        CacheTierRenderers();

        if (tierRenderers.Length == 0)
            return;

        Color tierColor = ModuleTierVisualPalette.GetTierColor(CurrentTier);
        float tintStrength = ModuleTierVisualPalette.GetTintStrength(CurrentTier);

        for (int i = 0; i < tierRenderers.Length; i++)
        {
            SpriteRenderer renderer = tierRenderers[i];
            if (renderer == null)
                continue;

            Color baseColor = GetBaseRendererColor(renderer);
            if (CurrentTier <= 1)
            {
                renderer.color = new Color(1f, 1f, 1f, baseColor.a);
                continue;
            }

            Color tintedColor = Color.Lerp(baseColor, tierColor, tintStrength);
            tintedColor.a = baseColor.a;
            renderer.color = tintedColor;
        }

        UpdateTierGlow();
    }

    void CacheTierRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var eligibleRenderers = new List<SpriteRenderer>(renderers.Length);
        SpriteRenderer preferredPrimary = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (!IsTierTintTarget(renderer))
                continue;

            eligibleRenderers.Add(renderer);

            int rendererId = renderer.GetInstanceID();
            if (!baseRendererColors.ContainsKey(rendererId))
                baseRendererColors[rendererId] = renderer.color;

            if (preferredPrimary == null || IsBetterPrimaryRenderer(renderer, preferredPrimary))
                preferredPrimary = renderer;
        }

        tierRenderers = eligibleRenderers.ToArray();
        primaryRenderer = preferredPrimary;

        if (tierGlowRenderer != null && primaryRenderer == null)
            tierGlowRenderer.enabled = false;
    }

    bool IsTierTintTarget(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return false;

        if (renderer.name == "EnemyOutline" || renderer.name == "LaserBeam" || renderer.name == "ExhaustCore" || renderer.name == TierGlowObjectName || renderer.name == "AuraPart")
            return false;

        return true;
    }

    bool IsBetterPrimaryRenderer(SpriteRenderer candidate, SpriteRenderer current)
    {
        bool candidateIsRoot = candidate.transform == transform;
        bool currentIsRoot = current.transform == transform;

        if (candidateIsRoot != currentIsRoot)
            return candidateIsRoot;

        if (candidate.sortingOrder != current.sortingOrder)
            return candidate.sortingOrder > current.sortingOrder;

        return candidate.transform.GetSiblingIndex() < current.transform.GetSiblingIndex();
    }

    Color GetBaseRendererColor(SpriteRenderer renderer)
    {
        if (renderer == null)
            return Color.white;

        int rendererId = renderer.GetInstanceID();
        if (baseRendererColors.TryGetValue(rendererId, out Color color))
            return color;

        color = renderer.color;
        baseRendererColors[rendererId] = color;
        return color;
    }

    void UpdateTierGlow()
    {
        if (primaryRenderer == null)
            return;

        float glowAlpha = ModuleTierVisualPalette.GetGlowAlpha(CurrentTier);
        if (glowAlpha <= 0.001f)
        {
            if (tierGlowRenderer != null)
                tierGlowRenderer.enabled = false;

            SetTierAuraActive(false);

            return;
        }

        SpriteRenderer glowRenderer = EnsureTierGlowRenderer();
        if (glowRenderer == null)
            return;

        glowRenderer.enabled = true;
        glowRenderer.sprite = primaryRenderer.sprite;
        glowRenderer.drawMode = SpriteDrawMode.Simple;
        glowRenderer.sortingLayerID = primaryRenderer.sortingLayerID;
        glowRenderer.sortingOrder = primaryRenderer.sortingOrder - 1;
        glowRenderer.maskInteraction = primaryRenderer.maskInteraction;
        glowRenderer.flipX = primaryRenderer.flipX;
        glowRenderer.flipY = primaryRenderer.flipY;

        Color glowColor = ModuleTierVisualPalette.GetGlowColor(CurrentTier);
        glowColor.a = glowAlpha;
        glowRenderer.color = glowColor;

        Transform glowTransform = glowRenderer.transform;
        glowTransform.localPosition = Vector3.zero;
        glowTransform.localRotation = Quaternion.identity;
        glowTransform.localScale = Vector3.one * ModuleTierVisualPalette.GetGlowScale(CurrentTier);

        UpdateTierAura(glowColor, glowAlpha);
    }

    SpriteRenderer EnsureTierGlowRenderer()
    {
        if (tierGlowRenderer != null)
            return tierGlowRenderer;

        if (primaryRenderer == null)
            return null;

        Transform existing = primaryRenderer.transform.Find(TierGlowObjectName);
        if (existing != null)
            tierGlowRenderer = existing.GetComponent<SpriteRenderer>();

        if (tierGlowRenderer != null)
            return tierGlowRenderer;

        if (!Application.isPlaying)
            return null;

        var glowObject = new GameObject(TierGlowObjectName);
        glowObject.transform.SetParent(primaryRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;

        tierGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
        tierGlowRenderer.sprite = primaryRenderer.sprite;
        tierGlowRenderer.sharedMaterial = primaryRenderer.sharedMaterial;
        return tierGlowRenderer;
    }

    void UpdateTierAura(Color glowColor, float glowAlpha)
    {
        if (CurrentTier < 6 || primaryRenderer == null)
        {
            SetTierAuraActive(false);
            return;
        }

        Transform auraRoot = EnsureTierAuraRoot();
        if (auraRoot == null)
            return;

        SetTierAuraActive(true);

        float pixelsPerUnit = primaryRenderer.sprite != null && primaryRenderer.sprite.pixelsPerUnit > 0f
            ? primaryRenderer.sprite.pixelsPerUnit
            : 128f;
        float offset = TierAuraPixelRadius / pixelsPerUnit;

        for (int i = 0; i < auraRoot.childCount; i++)
        {
            Transform segment = auraRoot.GetChild(i);
            if (segment == null)
                continue;

            segment.localPosition = (Vector3)(TierAuraDirections[i] * offset);

            SpriteRenderer auraRenderer = segment.GetComponent<SpriteRenderer>();
            if (auraRenderer == null)
                continue;

            auraRenderer.enabled = true;
            auraRenderer.sprite = primaryRenderer.sprite;
            auraRenderer.drawMode = SpriteDrawMode.Simple;
            auraRenderer.sortingLayerID = primaryRenderer.sortingLayerID;
            auraRenderer.sortingOrder = primaryRenderer.sortingOrder - 2;
            auraRenderer.maskInteraction = primaryRenderer.maskInteraction;
            auraRenderer.flipX = primaryRenderer.flipX;
            auraRenderer.flipY = primaryRenderer.flipY;

            Color auraColor = glowColor;
            auraColor.a = glowAlpha * 0.55f;
            auraRenderer.color = auraColor;
        }
    }

    Transform EnsureTierAuraRoot()
    {
        if (tierAuraRoot != null)
            return tierAuraRoot;

        if (primaryRenderer == null)
            return null;

        Transform existing = primaryRenderer.transform.Find(TierAuraRootObjectName);
        if (existing != null)
            tierAuraRoot = existing;

        if (tierAuraRoot != null)
            return tierAuraRoot;

        if (!Application.isPlaying)
            return null;

        var rootObject = new GameObject(TierAuraRootObjectName);
        rootObject.transform.SetParent(primaryRenderer.transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        tierAuraRoot = rootObject.transform;

        for (int i = 0; i < TierAuraDirections.Length; i++)
        {
            var segmentObject = new GameObject("AuraPart");
            segmentObject.transform.SetParent(tierAuraRoot, false);
            segmentObject.transform.localPosition = Vector3.zero;
            segmentObject.transform.localRotation = Quaternion.identity;

            SpriteRenderer auraRenderer = segmentObject.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = primaryRenderer.sprite;
            auraRenderer.sharedMaterial = primaryRenderer.sharedMaterial;
            auraRenderer.enabled = false;
        }

        return tierAuraRoot;
    }

    void SetTierAuraActive(bool active)
    {
        if (tierAuraRoot == null)
            return;

        if (tierAuraRoot.gameObject.activeSelf != active)
            tierAuraRoot.gameObject.SetActive(active);
    }
}
