using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ModuleInfoUI : MonoBehaviour
{
    [Header("Templates (TMP)")]
    public TMP_Text nameTemplate;
    public TMP_Text lineTemplate;

    [Header("Auto Layout")]
    public AutoStackTMP autoStack;

    [Header("Follow")]
    public Vector2 screenOffset = new Vector2(0f, 40f);
    public bool clampToScreen = true;

    [Header("Upgrade Colors")]
    public Color upgradeReadyColor = new Color(0.9f, 0.95f, 0.55f, 1f);
    public Color upgradeLockedColor = new Color(0.62f, 0.66f, 0.72f, 1f);
    public Color upgradeProgressColor = new Color(0.47f, 0.95f, 0.78f, 1f);

    RectTransform rt;
    CanvasGroup cg;
    Camera cam;

    TMP_Text nameLine;
    TMP_Text upgradeHintLine;
    TMP_Text upgradeActionLine;
    Button upgradeButton;
    EventTrigger upgradeTrigger;
    RectTransform upgradeDockRoot;
    string upgradeHoverMessage;
    bool isUpgradeHovered;

    readonly List<TMP_Text> pool = new();
    int useCount;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cam = Camera.main;

        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        if (autoStack == null) autoStack = GetComponentInChildren<AutoStackTMP>(true);

        ResolveTemplates();
        ApplyLocalizedFont(nameTemplate);
        ApplyLocalizedFont(lineTemplate);

        SafeDisableTemplate(nameTemplate);
        SafeDisableTemplate(lineTemplate);

        Hide();
    }

    void LateUpdate()
    {
        var module = ModuleSelection.Selected;
        var scrap = ModuleSelection.SelectedScrap;
        bool hasModuleSelection = module != null && module.data != null;
        bool hasScrapSelection = scrap != null;

        if (!hasModuleSelection && !hasScrapSelection)
        {
            Hide();
            return;
        }

        Show();
        if (hasModuleSelection)
            BuildLines(module);
        else
            BuildScrapLines(scrap);

        if (autoStack != null)
            autoStack.Rebuild();

        if (hasModuleSelection)
            LayoutUpgradeControls();

        FollowSelected(hasModuleSelection ? module.transform : scrap.transform);
    }

    void ResolveTemplates()
    {
        if (nameTemplate != null && lineTemplate != null)
            return;

        var tmps = GetComponentsInChildren<TMP_Text>(true);
        if (tmps == null || tmps.Length == 0)
            return;

        TMP_Text largest = null;
        TMP_Text smallest = null;
        foreach (var t in tmps)
        {
            if (t == null) continue;
            if (largest == null || t.fontSize > largest.fontSize) largest = t;
            if (smallest == null || t.fontSize < smallest.fontSize) smallest = t;
        }

        if (nameTemplate == null) nameTemplate = largest;
        if (lineTemplate == null) lineTemplate = smallest != null ? smallest : largest;
    }

    void BuildLines(ModuleInstance module)
    {
        var data = module.data;

        EnsureNameLine();
        nameLine.text = module.DisplayName;
        nameLine.transform.SetAsFirstSibling();
        nameLine.gameObject.SetActive(true);

        BeginLines();

        if (module.CurrentTier > 0)
            AddLine(LocalizationManager.Format("info.tier", "Tier: {0}", module.CurrentTier));
        AddLine(LocalizationManager.Format("info.hp", "HP: {0} / {1}", module.hp, module.GetMaxHp()));

        float powerGen = module.GetPowerGenPerSec();
        float powerUse = module.GetPowerUsePerSec();
        if (powerGen > 0f || powerUse > 0f)
            AddLine(LocalizationManager.Format("info.power", "Power: +{0}/s  -{1}/s", powerGen.ToString("0.##"), powerUse.ToString("0.##")));

        var ship = module.GetComponentInParent<ShipStats>();
        if (ship != null && ship.energyMax > 0f)
            AddLine(LocalizationManager.Format("info.battery", "Battery: {0} / {1}", ship.energyCurrent.ToString("0.##"), ship.energyMax.ToString("0.##")));

        float mass = module.GetMass();
        float thrust = module.GetThrust();
        float energyCap = module.GetMaxEnergy();
        float fuelCap = module.GetMaxFuel();
        float fuelSynth = module.GetFuelSynthesisPerSec();

        if (mass > 0f) AddLine(LocalizationManager.Format("info.mass", "Mass: {0}", mass.ToString("0.##")));
        if (thrust > 0f) AddLine(LocalizationManager.Format("info.thrust", "Thrust: {0}", thrust.ToString("0.##")));
        if (energyCap > 0f) AddLine(LocalizationManager.Format("info.energy_cap", "Energy Cap: {0}", energyCap.ToString("0.##")));
        if (fuelCap > 0f) AddLine(LocalizationManager.Format("info.fuel_cap", "Fuel Cap: {0}", fuelCap.ToString("0.##")));
        if (fuelSynth > 0f) AddLine(LocalizationManager.Format("info.fuel_synth", "Fuel Synth: {0}/s", fuelSynth.ToString("0.##")));

        bool hasWeapon =
            data.weaponType != WeaponType.None ||
            module.GetWeaponDamage() > 0f ||
            module.GetWeaponFireRate() > 0f ||
            data.dps > 0f ||
            module.GetWeaponPowerPerShot() > 0f ||
            module.GetWeaponHeatPerShot() > 0f ||
            module.GetWeaponAmmoPerShot() > 0f;

        if (hasWeapon)
        {
            float dps = module.GetDps();
            if (dps > 0f) AddLine(LocalizationManager.Format("info.dps", "DPS: {0}", dps.ToString("0.##")));
        }

        EndLines();
        UpdateUpgradeAction(module);
    }

    void BuildScrapLines(FloatingScrap scrap)
    {
        EnsureNameLine();
        nameLine.text = scrap.DisplayName;
        nameLine.transform.SetAsFirstSibling();
        nameLine.gameObject.SetActive(true);

        BeginLines();
        AddLine(LocalizationManager.Format("info.hp", "HP: {0} / {1}", scrap.CurrentHP, scrap.MaxHP));
        AddLine(LocalizationManager.Format("info.mass", "Mass: {0}", scrap.Mass.ToString("0.##")));
        EndLines();

        ForceHideUpgradeHint();
        if (upgradeActionLine != null)
            upgradeActionLine.gameObject.SetActive(false);
    }

    void UpdateUpgradeAction(ModuleInstance module)
    {
        EnsureUpgradeActionLine();
        if (upgradeActionLine == null)
            return;

        var upgradeSystem = ModuleUpgradeSystem.Instance;
        var info = upgradeSystem.GetUpgradeInfo(module);

        if (upgradeSystem.IsUpgrading(module))
        {
            float progress = info.progress01;

            upgradeActionLine.text = $"<mark=#214B3D padding=\"22,22,7,7\">{LocalizationManager.Get("action.upgrading", "UPGRADING")}</mark>";
            upgradeActionLine.color = upgradeProgressColor;
            upgradeActionLine.raycastTarget = true;
            upgradeButton.interactable = true;
            upgradeHoverMessage = LocalizationManager.Format("hint.upgrade_progress", "{0} {1:0}%", module.DisplayName, progress * 100f);
            RefreshUpgradeHintVisibility();
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        if (upgradeSystem.CanStartUpgrade(module, out string reason))
        {
            upgradeActionLine.text = $"<mark=#5A6218 padding=\"22,22,7,7\">{LocalizationManager.Get("action.upgrade", "UPGRADE")}</mark>";
            upgradeActionLine.color = upgradeReadyColor;
            upgradeActionLine.raycastTarget = true;
            upgradeButton.interactable = true;
            upgradeHoverMessage = LocalizationManager.Format("hint.upgrade_cost", "{0} Scrap / {1:0.#}s", info.scrapCost, info.duration);
            RefreshUpgradeHintVisibility();
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        upgradeHoverMessage = reason;
        ForceHideUpgradeHint();
        upgradeActionLine.gameObject.SetActive(false);
    }

    void EnsureNameLine()
    {
        if (nameLine != null || nameTemplate == null) return;

        Transform parent = nameTemplate.transform.parent != null ? nameTemplate.transform.parent : transform;
        var go = Instantiate(nameTemplate.gameObject, parent);
        go.name = "NameLine";
        go.SetActive(true);

        nameLine = go.GetComponent<TMP_Text>();
        nameLine.text = "";
        nameLine.raycastTarget = false;
        ApplyLocalizedFont(nameLine);
    }

    void EnsureUpgradeActionLine()
    {
        if (upgradeActionLine != null || lineTemplate == null) return;

        RectTransform dock = EnsureUpgradeDockRoot();
        if (dock == null)
            return;

        EnsureUpgradeHintLine(dock);

        var go = Instantiate(lineTemplate.gameObject, dock);
        go.name = "UpgradeActionLine";
        go.SetActive(true);

        upgradeActionLine = go.GetComponent<TMP_Text>();
        upgradeActionLine.fontStyle = FontStyles.Bold;
        upgradeActionLine.alignment = TextAlignmentOptions.Center;
        upgradeActionLine.textWrappingMode = TextWrappingModes.NoWrap;
        upgradeActionLine.text = "";
        upgradeActionLine.color = upgradeReadyColor;
        upgradeActionLine.raycastTarget = true;
        upgradeActionLine.rectTransform.sizeDelta = new Vector2(220f, 30f);
        upgradeActionLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ConfigureDockedElement(upgradeActionLine.rectTransform, new Vector2(0f, 0f));
        ApplyLocalizedFont(upgradeActionLine);

        upgradeButton = go.GetComponent<Button>();
        if (upgradeButton == null) upgradeButton = go.AddComponent<Button>();
        upgradeButton.transition = Selectable.Transition.None;
        upgradeButton.targetGraphic = upgradeActionLine;
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(OnUpgradeClicked);

        upgradeTrigger = go.GetComponent<EventTrigger>();
        if (upgradeTrigger == null) upgradeTrigger = go.AddComponent<EventTrigger>();
        upgradeTrigger.triggers = new List<EventTrigger.Entry>();
        AddTrigger(EventTriggerType.PointerEnter, _ => ShowUpgradeHint());
        AddTrigger(EventTriggerType.PointerExit, _ => HideUpgradeHint());
    }

    void EnsureUpgradeHintLine(Transform parent)
    {
        if (upgradeHintLine != null || lineTemplate == null) return;

        var go = Instantiate(lineTemplate.gameObject, parent);
        go.name = "UpgradeHintLine";
        go.SetActive(false);

        upgradeHintLine = go.GetComponent<TMP_Text>();
        upgradeHintLine.fontSize = Mathf.Max(12f, lineTemplate.fontSize - 1f);
        upgradeHintLine.color = new Color(0.82f, 0.88f, 0.92f, 1f);
        upgradeHintLine.textWrappingMode = TextWrappingModes.NoWrap;
        upgradeHintLine.overflowMode = TextOverflowModes.Ellipsis;
        upgradeHintLine.rectTransform.sizeDelta = new Vector2(320f, 24f);
        upgradeHintLine.raycastTarget = false;
        upgradeHintLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ConfigureDockedElement(upgradeHintLine.rectTransform, new Vector2(0f, 36f));
        ApplyLocalizedFont(upgradeHintLine);
    }

    void BeginLines()
    {
        useCount = 0;
    }

    void AddLine(string text)
    {
        if (lineTemplate == null) return;

        var line = GetLineFromPool();
        line.text = text;
        line.color = lineTemplate.color;
        line.gameObject.SetActive(true);
    }

    TMP_Text GetLineFromPool()
    {
        if (useCount < pool.Count)
            return pool[useCount++];

        Transform parent = lineTemplate.transform.parent != null ? lineTemplate.transform.parent : transform;
        var go = Instantiate(lineTemplate.gameObject, parent);
        go.name = "Line";
        go.SetActive(true);

        var line = go.GetComponent<TMP_Text>();
        line.text = "";
        line.raycastTarget = false;
        ApplyLocalizedFont(line);
        pool.Add(line);
        useCount++;
        return line;
    }

    void EndLines()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            bool shouldShow = i < useCount;
            if (pool[i] != null)
                pool[i].gameObject.SetActive(shouldShow);
        }
    }

    void OnUpgradeClicked()
    {
        var module = ModuleSelection.Selected;
        if (module == null) return;

        ModuleUpgradeSystem.Instance.StartUpgrade(module);
    }

    void LayoutUpgradeControls()
    {
        if (upgradeDockRoot == null)
            return;

        ConfigureDockedElement(upgradeDockRoot, new Vector2(0f, 52f));

        if (upgradeActionLine != null)
            ConfigureDockedElement(upgradeActionLine.rectTransform, new Vector2(0f, 0f));

        if (upgradeHintLine != null)
            ConfigureDockedElement(upgradeHintLine.rectTransform, new Vector2(0f, 36f));
    }

    void ShowUpgradeHint()
    {
        isUpgradeHovered = true;
        if (upgradeHintLine == null || string.IsNullOrWhiteSpace(upgradeHoverMessage))
            return;

        upgradeHintLine.text = upgradeHoverMessage;
        upgradeHintLine.gameObject.SetActive(true);
        upgradeHintLine.transform.SetAsLastSibling();
        upgradeActionLine.transform.SetAsLastSibling();
    }

    void HideUpgradeHint()
    {
        isUpgradeHovered = false;
        if (upgradeHintLine != null)
            upgradeHintLine.gameObject.SetActive(false);
    }

    void RefreshUpgradeHintVisibility()
    {
        if (isUpgradeHovered)
            ShowUpgradeHint();
        else if (upgradeHintLine != null)
            upgradeHintLine.gameObject.SetActive(false);
    }

    void ForceHideUpgradeHint()
    {
        isUpgradeHovered = false;
        if (upgradeHintLine != null)
            upgradeHintLine.gameObject.SetActive(false);
    }

    void AddTrigger(EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        upgradeTrigger.triggers.Add(entry);
    }

    void FollowSelected(Transform target)
    {
        if (!cam) cam = Camera.main;
        if (target == null)
            return;

        Vector3 screenPos = cam.WorldToScreenPoint(target.position);
        Vector2 pos = (Vector2)screenPos + screenOffset;

        if (clampToScreen)
        {
            Vector2 size = rt.sizeDelta;
            float pad = 10f;
            pos.x = Mathf.Clamp(pos.x, pad + size.x * 0.5f, Screen.width - pad - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, pad + size.y * 0.5f, Screen.height - pad - size.y * 0.5f);
        }

        rt.position = pos;
    }

    void Show()
    {
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    void Hide()
    {
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (nameLine != null) nameLine.gameObject.SetActive(false);
        ForceHideUpgradeHint();
        if (upgradeActionLine != null) upgradeActionLine.gameObject.SetActive(false);

        useCount = 0;
        EndLines();
    }

    RectTransform EnsureUpgradeDockRoot()
    {
        if (upgradeDockRoot != null)
            return upgradeDockRoot;

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return null;

        var go = new GameObject("UpgradeDockRoot", typeof(RectTransform));
        upgradeDockRoot = go.GetComponent<RectTransform>();
        upgradeDockRoot.SetParent(rootCanvas.transform, false);
        ConfigureDockedElement(upgradeDockRoot, new Vector2(0f, 52f));
        upgradeDockRoot.sizeDelta = new Vector2(360f, 70f);
        upgradeDockRoot.SetAsLastSibling();
        return upgradeDockRoot;
    }

    void ConfigureDockedElement(RectTransform target, Vector2 anchoredPosition)
    {
        if (target == null)
            return;

        target.anchorMin = new Vector2(0.5f, 0f);
        target.anchorMax = new Vector2(0.5f, 0f);
        target.pivot = new Vector2(0.5f, 0f);
        target.anchoredPosition = anchoredPosition;
        target.localScale = Vector3.one;
    }

    void SafeDisableTemplate(TMP_Text template)
    {
        if (template == null) return;
        template.text = "";
        template.raycastTarget = false;
        if (template.gameObject.activeSelf)
            template.gameObject.SetActive(false);
    }

    void ApplyLocalizedFont(TMP_Text text)
    {
        LocalizationFontManager.ApplyFont(text);
    }
}
