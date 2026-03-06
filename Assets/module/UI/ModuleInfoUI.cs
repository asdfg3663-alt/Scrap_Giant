using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
    TMP_Text upgradeActionLine;
    Button upgradeButton;

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

        SafeDisableTemplate(nameTemplate);
        SafeDisableTemplate(lineTemplate);

        Hide();
    }

    void LateUpdate()
    {
        var module = ModuleSelection.Selected;
        if (module == null || module.data == null)
        {
            Hide();
            return;
        }

        Show();
        BuildLines(module);

        if (autoStack != null)
            autoStack.Rebuild();

        FollowSelected(module);
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

        AddLine($"HP: {module.hp} / {module.maxHp}");

        if (data.powerGenPerSec > 0f || data.powerUsePerSec > 0f)
            AddLine($"Power: +{data.powerGenPerSec:0.##}/s  -{data.powerUsePerSec:0.##}/s");

        var ship = module.GetComponentInParent<ShipStats>();
        if (ship != null && ship.energyMax > 0f)
            AddLine($"Battery: {ship.energyCurrent:0.##} / {ship.energyMax:0.##}");

        if (data.mass > 0f) AddLine($"Mass: {data.mass:0.##}");
        if (data.thrust > 0f) AddLine($"Thrust: {data.thrust:0.##}");
        if (data.maxEnergy > 0f) AddLine($"Energy Cap: {data.maxEnergy:0.##}");
        if (module.CurrentTier > 0) AddLine($"Tier: T{module.CurrentTier}");

        bool hasWeapon =
            data.weaponType != WeaponType.None ||
            data.weaponDamage > 0f ||
            data.weaponFireRate > 0f ||
            data.dps > 0f ||
            data.weaponPowerPerShot > 0f ||
            data.weaponHeatPerShot > 0f ||
            data.weaponAmmoPerShot > 0f;

        if (hasWeapon)
        {
            float dps = data.dps > 0f ? data.dps : data.weaponDamage * data.weaponFireRate;
            if (dps > 0f) AddLine($"DPS: {dps:0.##} ({data.weaponType})");
            if (data.weaponDamage > 0f) AddLine($"Damage: {data.weaponDamage:0.##}");
            if (data.weaponFireRate > 0f) AddLine($"FireRate: {data.weaponFireRate:0.##}/s");
            if (data.weaponPowerPerShot > 0f) AddLine($"Power/Shot: {data.weaponPowerPerShot:0.##}");
            if (data.weaponHeatPerShot > 0f) AddLine($"Heat/Shot: {data.weaponHeatPerShot:0.##}");
            if (data.weaponAmmoPerShot > 0f) AddLine($"Ammo/Shot: {data.weaponAmmoPerShot:0.##}");
            if (data.powerUsePerSec > 0f) AddLine($"Power Use(Firing): {data.powerUsePerSec:0.##}/s");
        }

        EndLines();
        UpdateUpgradeAction(module);
    }

    void UpdateUpgradeAction(ModuleInstance module)
    {
        EnsureUpgradeActionLine();
        upgradeActionLine.transform.SetAsLastSibling();

        var upgradeSystem = ModuleUpgradeSystem.Instance;
        var info = upgradeSystem.GetUpgradeInfo(module);

        if (upgradeSystem.IsUpgrading(module))
        {
            float progress = info.progress01;
            int fill = Mathf.Clamp(Mathf.RoundToInt(progress * 10f), 0, 10);
            string bar = new string('#', fill) + new string('-', 10 - fill);

            upgradeActionLine.text = $"Upgrading [{bar}] {progress * 100f:0}%";
            upgradeActionLine.color = upgradeProgressColor;
            upgradeActionLine.raycastTarget = false;
            upgradeButton.interactable = false;
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        if (upgradeSystem.CanStartUpgrade(module, out string reason))
        {
            upgradeActionLine.text = $"Upgrade -> T{info.targetTier}  ({info.scrapCost} Scrap / {info.duration:0.#}s)";
            upgradeActionLine.color = upgradeReadyColor;
            upgradeActionLine.raycastTarget = true;
            upgradeButton.interactable = true;
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        upgradeActionLine.text = $"Upgrade Locked - {reason}";
        upgradeActionLine.color = upgradeLockedColor;
        upgradeActionLine.raycastTarget = false;
        upgradeButton.interactable = false;
        upgradeActionLine.gameObject.SetActive(true);
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
    }

    void EnsureUpgradeActionLine()
    {
        if (upgradeActionLine != null || lineTemplate == null) return;

        Transform parent = lineTemplate.transform.parent != null ? lineTemplate.transform.parent : transform;
        var go = Instantiate(lineTemplate.gameObject, parent);
        go.name = "UpgradeActionLine";
        go.SetActive(true);

        upgradeActionLine = go.GetComponent<TMP_Text>();
        upgradeActionLine.fontStyle = FontStyles.Bold;
        upgradeActionLine.text = "";
        upgradeActionLine.color = upgradeReadyColor;
        upgradeActionLine.raycastTarget = true;

        upgradeButton = go.GetComponent<Button>();
        if (upgradeButton == null) upgradeButton = go.AddComponent<Button>();
        upgradeButton.transition = Selectable.Transition.None;
        upgradeButton.targetGraphic = upgradeActionLine;
        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(OnUpgradeClicked);
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

    void FollowSelected(ModuleInstance module)
    {
        if (!cam) cam = Camera.main;

        Vector3 screenPos = cam.WorldToScreenPoint(module.transform.position);
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
        if (upgradeActionLine != null) upgradeActionLine.gameObject.SetActive(false);

        useCount = 0;
        EndLines();
    }

    void SafeDisableTemplate(TMP_Text template)
    {
        if (template == null) return;
        template.text = "";
        template.raycastTarget = false;
        if (template.gameObject.activeSelf)
            template.gameObject.SetActive(false);
    }
}
