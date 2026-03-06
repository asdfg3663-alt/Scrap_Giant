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

        if (module.CurrentTier > 0) AddLine($"Tier: {module.CurrentTier}");
        AddLine($"HP: {module.hp} / {module.GetMaxHp()}");

        float powerGen = module.GetPowerGenPerSec();
        float powerUse = module.GetPowerUsePerSec();
        if (powerGen > 0f || powerUse > 0f)
            AddLine($"Power: +{powerGen:0.##}/s  -{powerUse:0.##}/s");

        var ship = module.GetComponentInParent<ShipStats>();
        if (ship != null && ship.energyMax > 0f)
            AddLine($"Battery: {ship.energyCurrent:0.##} / {ship.energyMax:0.##}");

        float mass = module.GetMass();
        float thrust = module.GetThrust();
        float energyCap = module.GetMaxEnergy();

        if (mass > 0f) AddLine($"Mass: {mass:0.##}");
        if (thrust > 0f) AddLine($"Thrust: {thrust:0.##}");
        if (energyCap > 0f) AddLine($"Energy Cap: {energyCap:0.##}");

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
            if (dps > 0f) AddLine($"DPS: {dps:0.##}");
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

            upgradeActionLine.text = $"<mark=#214B3D padding=\"14,14,6,6\">UPGRADING [{bar}] {progress * 100f:0}%</mark>";
            upgradeActionLine.color = upgradeProgressColor;
            upgradeActionLine.raycastTarget = false;
            upgradeButton.interactable = false;
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        if (upgradeSystem.CanStartUpgrade(module, out string reason))
        {
            upgradeActionLine.text = $"<mark=#5A6218 padding=\"14,14,6,6\">UPGRADE TO {info.targetTier}  {info.scrapCost} SCRAP  {info.duration:0.#}s</mark>";
            upgradeActionLine.color = upgradeReadyColor;
            upgradeActionLine.raycastTarget = true;
            upgradeButton.interactable = true;
            upgradeActionLine.gameObject.SetActive(true);
            return;
        }

        upgradeActionLine.text = $"<mark=#444A54 padding=\"14,14,6,6\">UPGRADE LOCKED  {reason.ToUpperInvariant()}</mark>";
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
        upgradeActionLine.alignment = TextAlignmentOptions.Center;
        upgradeActionLine.text = "";
        upgradeActionLine.color = upgradeReadyColor;
        upgradeActionLine.raycastTarget = true;
        upgradeActionLine.rectTransform.sizeDelta = new Vector2(320f, 28f);

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
