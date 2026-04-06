using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PlayerHudRuntime : MonoBehaviour
{
    const string BestScorePrefsKey = "ScrapGiant.HUD.BestScore";
    const float ExpandedInventoryWidth = 280f;
    const float CollapsedInventoryWidth = 48f;
    static readonly Color NavigatorScrapColor = new Color(0.7f, 0.74f, 0.78f, 1f);
    static readonly Color NavigatorEnemyColor = new Color(0.96f, 0.26f, 0.22f, 1f);
    static readonly Color NavigatorNeutralModuleColor = new Color(0.34f, 0.65f, 0.98f, 1f);

    static PlayerHudRuntime instance;
    static Sprite solidSprite;
    static Sprite circleSprite;
    static float sessionBestScore;

    [Serializable]
    public class ResourceEntry
    {
        public string id;
        public string label;
        public float amount;
        public Color color;
        public string displayText;

        public ResourceEntry(string id, string label, float amount, Color color, string displayText = null)
        {
            this.id = id;
            this.label = label;
            this.amount = amount;
            this.color = color;
            this.displayText = displayText;
        }
    }

    [Serializable]
    public class InventoryEntry
    {
        public string id;
        public string label;
        public int amount;
        public Color color;
        public GameObject prefab;
        public Sprite iconSprite;
        public int upgradeLevel;
        public ModuleType moduleType;

        public InventoryEntry(string id, string label, int amount, Color color, GameObject prefab = null, Sprite iconSprite = null, int upgradeLevel = 0, ModuleType moduleType = ModuleType.Scrap)
        {
            this.id = id;
            this.label = label;
            this.amount = amount;
            this.color = color;
            this.prefab = prefab;
            this.iconSprite = iconSprite;
            this.upgradeLevel = upgradeLevel;
            this.moduleType = moduleType;
        }
    }

    struct ValueRow
    {
        public GameObject root;
        public Image icon;
        public TMP_Text label;
        public TMP_Text value;
    }

    struct InventoryRow
    {
        public GameObject root;
        public Image background;
        public Image icon;
        public TMP_Text label;
        public TMP_Text value;
        public InventoryEntryDragHandle dragHandle;
    }

    ShipStats trackedShip;
    TMP_FontAsset fontAsset;

    RectTransform hudRoot;
    RectTransform resourceListRoot;
    RectTransform ammoListRoot;
    RectTransform inventoryListRoot;
    RectTransform inventoryPanel;
    RectTransform navigatorRoot;
    RectTransform mobileControlsRoot;
    RectTransform productionModePanel;

    GameObject inventoryBody;
    GameObject inventoryEmptyState;
    GameObject productionModeOptionsRoot;

    Image assemblyPreviewImage;
    TMP_Text statusTitleText;
    TMP_Text bestTitleText;
    TMP_Text bestValueText;
    TMP_Text currentScoreText;
    TMP_Text overheatWarningText;
    TMP_Text balanceWarningText;
    TMP_Text assemblyTitleText;
    TMP_Text assemblyPrimaryText;
    TMP_Text assemblySecondaryText;
    TMP_Text inventoryTitleText;
    TMP_Text inventoryCountText;
    TMP_Text inventoryButtonText;
    TMP_Text inventoryEmptyText;
    TMP_Text inventoryDetailText;
    TMP_Text scrapNavigatorArrow;
    TMP_Text enemyNavigatorArrow;
    TMP_Text neutralModuleNavigatorArrow;
    TMP_Text productionModeTitleText;
    TMP_Text productionModeButtonText;
    TMP_Text quitButtonLabel;
    readonly List<TMP_Text> productionModeOptionLabels = new();

    [SerializeField] int inventoryCapacity = 2;
    int nextInventoryEntrySerial = 1;
    string selectedInventoryEntryId;

    float bestScore;
    int lastCurrentScore = int.MinValue;
    int lastBestScore = int.MinValue;
    bool inventoryExpanded = true;
    bool hudBuilt;
    bool resourceUiDirty;
    bool ammoUiDirty;
    bool inventoryUiDirty;
    bool productionModeOptionsVisible;
    float navigatorRadius = 72f;

    string assemblyPrimaryLabel = "Fuel synthesis ready";
    string assemblySecondaryLabel = string.Empty;
    bool assemblyActive;
    Sprite assemblySprite;

    readonly List<ResourceEntry> resources = new();
    readonly List<ResourceEntry> ammoEntries = new();
    readonly List<InventoryEntry> inventoryEntries = new();
    readonly List<ValueRow> resourceRows = new();
    readonly List<ValueRow> ammoRows = new();
    readonly List<InventoryRow> inventoryRows = new();
    readonly HashSet<int> storingModuleInstanceIds = new();

    [Header("Mobile Controls")]
    [SerializeField] bool showMobileControlsInEditor = true;
    [SerializeField] bool showMobileControlsOnDesktop = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        solidSprite = null;
        sessionBestScore = 0f;
    }

    public static PlayerHudRuntime Instance => instance;
    public ShipStats TrackedShip => trackedShip;
    public static int GetRecordedBestScore() => Mathf.RoundToInt(PlayerPrefs.GetFloat(BestScorePrefsKey, 0f));
    public static int GetSessionBestScore() => Mathf.RoundToInt(sessionBestScore);
    public static void ResetSessionBestScore() => sessionBestScore = 0f;
    public static void SetSessionBestScore(float value) => sessionBestScore = Mathf.Max(0f, value);
    public int InventoryCapacity => Mathf.Max(0, inventoryCapacity);

    public static void EnsureForPlayer(ShipStats ship)
    {
        if (ship == null) return;

        if (instance == null)
        {
            var go = new GameObject("PlayerHudRuntime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PlayerHudRuntime>();
        }

        instance.BindShip(ship);
    }

    public void BindShip(ShipStats ship)
    {
        if (ship == null) return;

        trackedShip = ship;
        RefreshLocalizedText();
        RefreshScore(force: true);
    }

    public int GetResourceAmount(string id)
    {
        var entry = FindResource(resources, id);
        return entry != null ? Mathf.FloorToInt(entry.amount) : 0;
    }

    public int GetAmmoAmount(string id)
    {
        var entry = FindResource(ammoEntries, id);
        return entry != null ? Mathf.FloorToInt(entry.amount) : 0;
    }

    public float GetResourceValue(string id)
    {
        var entry = FindResource(resources, id);
        return entry != null ? Mathf.Max(0f, entry.amount) : 0f;
    }

    public bool HasResource(string id, int amount)
    {
        return HasResource(id, (float)amount);
    }

    public bool HasResource(string id, float amount)
    {
        var entry = FindResource(resources, id);
        return entry != null && entry.amount + 0.0001f >= Mathf.Max(0f, amount);
    }

    public bool TryConsumeResource(string id, float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f) return true;

        var entry = FindResource(resources, id);
        if (entry == null || entry.amount + 0.0001f < amount)
            return false;

        entry.amount -= amount;
        if (entry.amount < 0f)
            entry.amount = 0f;

        entry.displayText = null;
        resourceUiDirty = true;
        return true;
    }

    public void SetResource(string id, string label, float amount, Color color)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var entry = FindResource(resources, id);
        if (entry == null)
            resources.Add(new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? id.ToUpperInvariant() : label, amount, color));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount = amount;
            entry.color = color;
            entry.displayText = null;
        }

        resourceUiDirty = true;
    }

    public void SetResourceDisplay(string id, string label, float amount, Color color, string displayText)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var entry = FindResource(resources, id);
        if (entry == null)
            resources.Add(new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? id.ToUpperInvariant() : label, amount, color, displayText));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount = amount;
            entry.color = color;
            entry.displayText = displayText;
        }

        resourceUiDirty = true;
    }

    public void AddResource(string id, string label, float amount, Color color)
    {
        var entry = FindOrCreate(resources, id, label, color);
        entry.amount += amount;
        entry.displayText = null;
        resourceUiDirty = true;
    }

    public void SetAmmo(string id, string label, int amount, Color color)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var entry = FindResource(ammoEntries, id);
        if (entry == null)
            ammoEntries.Add(new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? LocalizationManager.Get("resource.ammo", "Ammo") : label, amount, color));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount = amount;
            entry.color = color;
        }

        ammoUiDirty = true;
    }

    public void AddAmmo(string id, string label, int amount, Color color)
    {
        if (string.IsNullOrWhiteSpace(id) || amount <= 0)
            return;

        var entry = FindResource(ammoEntries, id);
        if (entry == null)
            ammoEntries.Add(new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? LocalizationManager.Get("resource.ammo", "Ammo") : label, amount, color));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount += amount;
            entry.color = color;
        }

        ammoUiDirty = true;
    }

    public void SetAssemblyState(bool active, string primary, string secondary, Sprite sprite = null)
    {
        assemblyActive = active;
        assemblyPrimaryLabel = string.IsNullOrWhiteSpace(primary)
            ? LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready")
            : primary;
        assemblySecondaryLabel = string.IsNullOrWhiteSpace(secondary) ? string.Empty : secondary;
        assemblySprite = sprite;

        RefreshAssemblyPanel();
    }

    public void SetInventoryItem(string id, string label, int amount, Color color)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var entry = FindInventory(id);
        if (entry == null)
            inventoryEntries.Add(new InventoryEntry(id, string.IsNullOrWhiteSpace(label) ? id : label, amount, color));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount = amount;
            entry.color = color;
        }

        inventoryUiDirty = true;
    }

    public void AddInventoryItem(string id, string label, int amount, Color color)
    {
        var entry = FindOrCreateInventory(id, label, color);
        entry.amount += amount;
        inventoryUiDirty = true;
    }

    public void RemoveInventoryItem(string id, int amount)
    {
        var entry = FindInventory(id);
        if (entry == null) return;

        entry.amount = Mathf.Max(0, entry.amount - amount);
        inventoryEntries.RemoveAll(x => x.amount <= 0);
        inventoryUiDirty = true;
    }

    public bool CanAcceptInventoryModules(int amount = 1)
    {
        return GetStoredInventoryModuleCount() + Mathf.Max(0, amount) <= InventoryCapacity;
    }

    public bool IsScreenPointOverInventory(Vector2 screenPoint)
    {
        if (!inventoryExpanded || inventoryBody == null || !inventoryBody.activeInHierarchy)
            return false;

        return inventoryPanel != null && RectTransformUtility.RectangleContainsScreenPoint(inventoryPanel, screenPoint, null);
    }

    public bool AddStoredModulePrefab(GameObject prefab, int amount = 1, int upgradeLevel = 0)
    {
        if (prefab == null || amount <= 0 || !CanAcceptInventoryModules(amount))
            return false;

        ModuleInstance module = prefab.GetComponent<ModuleInstance>();
        ModuleData data = module != null ? module.data : null;
        string label = GetInventoryLabel(prefab, data);
        Color color = GetInventoryColor(data);
        Sprite icon = ResolveModuleSprite(prefab.transform);
        ModuleType moduleType = data != null ? data.type : ModuleType.Scrap;

        for (int i = 0; i < amount; i++)
        {
            string id = BuildInventoryId(prefab, data, upgradeLevel);
            InventoryEntry entry = new InventoryEntry(id, label, 1, color, prefab, icon, upgradeLevel, moduleType);
            inventoryEntries.Add(entry);
            selectedInventoryEntryId = entry.id;
        }

        inventoryUiDirty = true;
        return true;
    }

    public bool TryStoreDetachedModule(Transform moduleTransform, Vector2 screenPoint)
    {
        if (moduleTransform == null || !IsScreenPointOverInventory(screenPoint) || !CanAcceptInventoryModules())
            return false;

        int moduleInstanceId = moduleTransform.gameObject.GetInstanceID();
        if (storingModuleInstanceIds.Contains(moduleInstanceId))
            return true;

        ModuleInstance module = moduleTransform.GetComponent<ModuleInstance>();
        if (module == null || module.data == null)
            return false;

        GameObject prefab = ResolveInventoryPrefab(module);
        if (prefab == null)
            return false;

        storingModuleInstanceIds.Add(moduleInstanceId);
        if (!AddStoredModulePrefab(prefab, 1, module.upgradeLevel))
        {
            storingModuleInstanceIds.Remove(moduleInstanceId);
            return false;
        }

        NeutralModuleSpawnDirector.Unregister(module);
        Destroy(moduleTransform.gameObject);
        inventoryUiDirty = true;
        return true;
    }

    public bool TryBeginStoredModuleDrag(string entryId, Vector2 screenPoint, int pointerId = int.MinValue)
    {
        InventoryEntry entry = FindInventory(entryId);
        if (entry == null || entry.prefab == null)
            return false;

        ShipBuilder builder = trackedShip != null
            ? trackedShip.GetComponent<ShipBuilder>()
            : FindFirstObjectByType<ShipBuilder>();
        if (builder == null)
            return false;

        if (!builder.BeginInventoryDrag(entry.prefab, entry.upgradeLevel, screenPoint, pointerId))
            return false;

        inventoryEntries.Remove(entry);
        inventoryUiDirty = true;
        return true;
    }

    public void ClearStoredModules()
    {
        inventoryEntries.Clear();
        nextInventoryEntrySerial = 1;
        selectedInventoryEntryId = string.Empty;
        storingModuleInstanceIds.Clear();
        inventoryUiDirty = true;
    }

    public void SelectInventoryEntry(string entryId)
    {
        selectedInventoryEntryId = entryId ?? string.Empty;
        RefreshInventoryDetail();
        inventoryUiDirty = true;
    }

    public void ToggleInventory()
    {
        inventoryExpanded = !inventoryExpanded;
        ApplyInventoryState();
    }

    public void SetShipBalanceWarning(bool visible, string message)
    {
        if (balanceWarningText == null)
            return;

        balanceWarningText.text = visible
            ? (string.IsNullOrWhiteSpace(message) ? LocalizationManager.Get("warning.ship_unbalanced", "Ship is imbalanced") : message)
            : string.Empty;
        balanceWarningText.gameObject.SetActive(visible);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        bestScore = PlayerPrefs.GetFloat(BestScorePrefsKey, 0f);

        InitializeDefaults();
        EnsureCanvas();
        BuildHud();
        RefreshAll(force: true);
    }

    void OnEnable()
    {
        LocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    void OnDisable()
    {
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();

        if (!hudBuilt)
        {
            EnsureCanvas();
            BuildHud();
            RefreshAll(force: true);
        }

        RefreshScore(force: false);
        RefreshOverheatWarning();
        RefreshNavigator();

        if (resourceUiDirty) RefreshResourceRows();
        if (ammoUiDirty) RefreshAmmoRows();
        if (inventoryUiDirty) RefreshInventoryRows();
    }

    void EnsureCanvas()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 250;

        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        fontAsset = ResolveFontAsset();
    }

    void InitializeDefaults()
    {
        float startingScrap = WorldSpawnDirector.GetStartingScrap();
        int startingAmmo = WorldSpawnDirector.GetStartingAmmo();

        if (resources.Count == 0)
            resources.Add(new ResourceEntry("scrap", LocalizationManager.Get("resource.scrap", "Scrap"), startingScrap, new Color(0.97f, 0.63f, 0.22f, 1f)));

        if (FindResource(resources, "fuel") == null)
            resources.Add(new ResourceEntry("fuel", LocalizationManager.Get("resource.fuel", "Fuel"), 0f, new Color(0.38f, 0.85f, 0.95f, 1f), "0 / 0"));

        if (ammoEntries.Count == 0)
            ammoEntries.Add(new ResourceEntry("ammo", LocalizationManager.Get("resource.ammo", "Ammo"), startingAmmo, new Color(0.96f, 0.32f, 0.24f, 1f)));

        assemblyActive = false;
        assemblyPrimaryLabel = LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready");
        assemblySecondaryLabel = string.Empty;
        assemblySprite = null;

        resourceUiDirty = true;
        ammoUiDirty = true;
        inventoryUiDirty = true;
    }

    void BuildHud()
    {
        if (hudBuilt) return;

        hudRoot = CreateRect("HUDRoot", transform);
        Stretch(hudRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        CreateTopHud();
        CreateInventoryPanel();
        CreateProductionModePanel();
        CreateNavigatorOverlay();
        CreateMobileControls();
        CreateQuitButton();

        hudBuilt = true;
        LocalizationFontManager.RefreshActiveTexts();
    }

    void CreateTopHud()
    {
        var topRoot = CreateRect("TopHud", hudRoot);
        SetAnchored(topRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1180f, 110f));

        CreateBackPlate(topRoot, new Color(0.04f, 0.07f, 0.1f, 0.74f));

        var resourcePanel = CreateRect("ResourcesPanel", topRoot);
        SetAnchored(resourcePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-340f, 0f), new Vector2(360f, 82f));
        CreateBackPlate(resourcePanel, new Color(0.05f, 0.1f, 0.12f, 0.9f));
        BuildResourcePanel(resourcePanel);

        var scorePanel = CreateRect("ScorePanel", topRoot);
        SetAnchored(scorePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(220f, 82f));
        CreateBackPlate(scorePanel, new Color(0.07f, 0.14f, 0.16f, 0.92f));
        BuildScorePanel(scorePanel);

        var assemblyPanel = CreateRect("AssemblyPanel", topRoot);
        SetAnchored(assemblyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 0f), new Vector2(340f, 82f));
        CreateBackPlate(assemblyPanel, new Color(0.05f, 0.1f, 0.12f, 0.9f));
        BuildAssemblyPanel(assemblyPanel);

        balanceWarningText = CreateLabel(
            topRoot,
            string.Empty,
            14f,
            new Color(1f, 0.74f, 0.24f, 1f),
            TextAlignmentOptions.Center,
            new Vector2(0f, -104f),
            new Vector2(0.5f, 1f),
            FontStyles.Bold);
        balanceWarningText.textWrappingMode = TextWrappingModes.NoWrap;
        balanceWarningText.overflowMode = TextOverflowModes.Ellipsis;
        balanceWarningText.rectTransform.sizeDelta = new Vector2(560f, 24f);
        balanceWarningText.gameObject.SetActive(false);
    }

    void BuildResourcePanel(RectTransform panel)
    {
        statusTitleText = CreateLabel(panel, LocalizationManager.Get("ui.status", "STATUS"), 15f, new Color(0.68f, 0.84f, 0.86f, 1f), TextAlignmentOptions.TopLeft, new Vector2(18f, -10f), new Vector2(0f, 1f), FontStyles.Bold);

        resourceListRoot = CreateRect("Resources", panel);
        SetAnchored(resourceListRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -6f), new Vector2(170f, 52f));
        var resourceLayout = resourceListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        resourceLayout.spacing = 4f;
        resourceLayout.childAlignment = TextAnchor.UpperLeft;
        resourceLayout.childControlWidth = true;
        resourceLayout.childControlHeight = false;
        resourceLayout.childForceExpandHeight = false;
        resourceLayout.childForceExpandWidth = false;

        ammoListRoot = CreateRect("Ammo", panel);
        SetAnchored(ammoListRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, -6f), new Vector2(150f, 52f));
        var ammoLayout = ammoListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        ammoLayout.spacing = 4f;
        ammoLayout.childAlignment = TextAnchor.UpperLeft;
        ammoLayout.childControlWidth = true;
        ammoLayout.childControlHeight = false;
        ammoLayout.childForceExpandHeight = false;
        ammoLayout.childForceExpandWidth = false;
    }

    void BuildScorePanel(RectTransform panel)
    {
        bestTitleText = CreateLabel(panel, LocalizationManager.Get("ui.best", "BEST"), 15f, new Color(0.94f, 0.97f, 0.84f, 1f), TextAlignmentOptions.Top, new Vector2(0f, -8f), new Vector2(0.5f, 1f), FontStyles.Bold);
        bestValueText = CreateLabel(panel, "0", 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, -2f), new Vector2(0.5f, 0.5f), FontStyles.Bold);
        currentScoreText = CreateLabel(panel, LocalizationManager.Format("ui.current_score", "Current {0}", 0), 13f, new Color(0.76f, 0.85f, 0.89f, 1f), TextAlignmentOptions.Bottom, new Vector2(0f, 10f), new Vector2(0.5f, 0f), FontStyles.Normal);
        overheatWarningText = CreateLabel(
            panel,
            string.Empty,
            13f,
            new Color(1f, 0.32f, 0.32f, 1f),
            TextAlignmentOptions.Top,
            new Vector2(0f, -10f),
            new Vector2(0.5f, 0f),
            FontStyles.Bold);
        overheatWarningText.textWrappingMode = TextWrappingModes.NoWrap;
        overheatWarningText.overflowMode = TextOverflowModes.Ellipsis;
        overheatWarningText.rectTransform.sizeDelta = new Vector2(380f, 22f);
        overheatWarningText.gameObject.SetActive(false);
    }

    void BuildAssemblyPanel(RectTransform panel)
    {
        var previewFrame = CreateRect("AssemblyPreviewFrame", panel);
        SetAnchored(previewFrame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(60f, 60f));
        CreateBackPlate(previewFrame, new Color(0.08f, 0.18f, 0.14f, 0.95f));

        var previewImageRect = CreateRect("AssemblyPreview", previewFrame);
        Stretch(previewImageRect, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        assemblyPreviewImage = CreateImage(previewImageRect, Color.white);
        assemblyPreviewImage.type = Image.Type.Simple;
        assemblyPreviewImage.preserveAspect = true;

        assemblyTitleText = CreateLabel(panel, LocalizationManager.Get("ui.assembly", "ASSEMBLY"), 15f, new Color(0.72f, 0.94f, 0.88f, 1f), TextAlignmentOptions.TopLeft, new Vector2(86f, -10f), new Vector2(0f, 1f), FontStyles.Bold);
        assemblyPrimaryText = CreateLabel(panel, LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready"), 16f, Color.white, TextAlignmentOptions.TopLeft, new Vector2(86f, -30f), new Vector2(0f, 1f), FontStyles.Bold);
        assemblySecondaryText = CreateLabel(panel, string.Empty, 13f, new Color(0.74f, 0.86f, 0.9f, 1f), TextAlignmentOptions.TopLeft, new Vector2(86f, -52f), new Vector2(0f, 1f), FontStyles.Normal);

        assemblyPrimaryText.textWrappingMode = TextWrappingModes.NoWrap;
        assemblyPrimaryText.overflowMode = TextOverflowModes.Ellipsis;
        assemblyPrimaryText.rectTransform.sizeDelta = new Vector2(230f, 22f);

        assemblySecondaryText.textWrappingMode = TextWrappingModes.NoWrap;
        assemblySecondaryText.overflowMode = TextOverflowModes.Ellipsis;
        assemblySecondaryText.rectTransform.sizeDelta = new Vector2(230f, 20f);

        RefreshAssemblyPanel();
    }

    void CreateInventoryPanel()
    {
        inventoryPanel = CreateRect("InventoryPanel", hudRoot);
        SetAnchored(inventoryPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -130f), new Vector2(ExpandedInventoryWidth, 420f));
        CreateBackPlate(inventoryPanel, new Color(0.03f, 0.06f, 0.09f, 0.86f));

        var buttonRoot = CreateRect("FoldButton", inventoryPanel);
        SetAnchored(buttonRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-42f, 0f), new Vector2(42f, 108f));
        CreateBackPlate(buttonRoot, new Color(0.07f, 0.14f, 0.17f, 0.96f));
        var button = buttonRoot.gameObject.AddComponent<Button>();
        var buttonImage = buttonRoot.GetComponent<Image>();
        buttonImage.raycastTarget = true;
        var colors = button.colors;
        colors.normalColor = buttonImage.color;
        colors.highlightedColor = new Color(0.12f, 0.22f, 0.26f, 1f);
        colors.pressedColor = new Color(0.2f, 0.35f, 0.4f, 1f);
        button.colors = colors;
        button.onClick.AddListener(ToggleInventory);
        inventoryButtonText = CreateLabel(buttonRoot, "<", 24f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.5f, 0.5f), FontStyles.Bold);

        inventoryTitleText = CreateLabel(inventoryPanel, LocalizationManager.Get("ui.inventory", "INVENTORY"), 18f, new Color(0.79f, 0.89f, 0.93f, 1f), TextAlignmentOptions.TopLeft, new Vector2(18f, -14f), new Vector2(0f, 1f), FontStyles.Bold);
        inventoryCountText = CreateLabel(inventoryPanel, LocalizationManager.Format("ui.inventory_parts", "{0} parts", 0), 13f, new Color(0.58f, 0.76f, 0.82f, 1f), TextAlignmentOptions.TopRight, new Vector2(-18f, -18f), new Vector2(1f, 1f), FontStyles.Normal);

        inventoryBody = CreateRect("Body", inventoryPanel).gameObject;
        var bodyRect = inventoryBody.GetComponent<RectTransform>();
        Stretch(bodyRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(18f, 18f), new Vector2(-18f, -46f));

        inventoryListRoot = CreateRect("Items", bodyRect);
        Stretch(inventoryListRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 108f), new Vector2(-12f, -62f));
        var layout = inventoryListRoot.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(104f, 104f);
        layout.spacing = new Vector2(12f, 12f);
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;

        var detailRoot = CreateRect("InventoryDetail", bodyRect);
        SetAnchored(detailRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(232f, 86f));
        CreateBackPlate(detailRoot, new Color(0.05f, 0.09f, 0.12f, 0.92f));
        inventoryDetailText = CreateLabel(detailRoot, LocalizationManager.Get("ui.inventory_empty", "Drag modules here."), 13f, new Color(0.76f, 0.84f, 0.9f, 1f), TextAlignmentOptions.TopLeft, new Vector2(12f, -10f), new Vector2(0f, 1f), FontStyles.Normal);
        inventoryDetailText.textWrappingMode = TextWrappingModes.Normal;
        inventoryDetailText.overflowMode = TextOverflowModes.Ellipsis;
        inventoryDetailText.rectTransform.sizeDelta = new Vector2(208f, 64f);

        inventoryEmptyState = CreateRect("EmptyState", bodyRect).gameObject;
        inventoryEmptyText = CreateLabel(inventoryEmptyState.transform, LocalizationManager.Get("ui.inventory_empty", "Drag modules here."), 15f, new Color(0.56f, 0.68f, 0.74f, 1f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.5f, 0.5f), FontStyles.Italic);
        Stretch(inventoryEmptyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        ApplyInventoryState();
    }

    void CreateProductionModePanel()
    {
        productionModePanel = CreateRect("ProductionModePanel", hudRoot);
        SetAnchored(productionModePanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -562f), new Vector2(ExpandedInventoryWidth, 116f));
        CreateBackPlate(productionModePanel, new Color(0.03f, 0.06f, 0.09f, 0.9f));

        productionModeTitleText = CreateLabel(
            productionModePanel,
            LocalizationManager.Get("ui.production_mode", "PRODUCTION PRIORITY"),
            15f,
            new Color(0.78f, 0.88f, 0.93f, 1f),
            TextAlignmentOptions.TopLeft,
            new Vector2(18f, -12f),
            new Vector2(0f, 1f),
            FontStyles.Bold);

        Button selectorButton = CreateHudButton(
            productionModePanel,
            "ModeSelector",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 14f),
            new Vector2(232f, 44f),
            new Color(0.08f, 0.15f, 0.19f, 0.98f),
            new Color(0.13f, 0.23f, 0.28f, 1f),
            new Color(0.18f, 0.31f, 0.37f, 1f),
            out productionModeButtonText);
        selectorButton.onClick.AddListener(ToggleProductionModeOptions);

        RectTransform optionsRect = CreateRect("ModeOptions", productionModePanel);
        SetAnchored(optionsRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(232f, 150f));
        CreateBackPlate(optionsRect, new Color(0.05f, 0.09f, 0.12f, 0.98f));
        productionModeOptionsRoot = optionsRect.gameObject;
        productionModeOptionsRoot.SetActive(false);

        productionModeOptionLabels.Clear();
        CreateProductionModeOption(optionsRect, new Vector2(0f, 104f), AssemblyPriorityMode.FuelFirst);
        CreateProductionModeOption(optionsRect, new Vector2(0f, 56f), AssemblyPriorityMode.RepairFirst);
        CreateProductionModeOption(optionsRect, new Vector2(0f, 8f), AssemblyPriorityMode.AmmoFirst);

        RefreshProductionModeUi();
        ApplyInventoryState();
    }

    void CreateProductionModeOption(RectTransform parent, Vector2 anchoredPosition, AssemblyPriorityMode mode)
    {
        Button optionButton = CreateHudButton(
            parent,
            mode.ToString(),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            anchoredPosition,
            new Vector2(208f, 40f),
            new Color(0.07f, 0.13f, 0.17f, 0.98f),
            new Color(0.12f, 0.22f, 0.27f, 1f),
            new Color(0.18f, 0.3f, 0.36f, 1f),
            out TMP_Text optionLabel);
        optionButton.onClick.AddListener(() => SetProductionMode(mode));
        productionModeOptionLabels.Add(optionLabel);
    }

    void ToggleProductionModeOptions()
    {
        productionModeOptionsVisible = !productionModeOptionsVisible;
        if (productionModeOptionsRoot != null)
            productionModeOptionsRoot.SetActive(productionModeOptionsVisible && inventoryExpanded);
    }

    void SetProductionMode(AssemblyPriorityMode mode)
    {
        if (trackedShip != null)
            trackedShip.SetAssemblyPriorityMode(mode);

        productionModeOptionsVisible = false;
        if (productionModeOptionsRoot != null)
            productionModeOptionsRoot.SetActive(false);

        RefreshProductionModeUi();
        RefreshAssemblyPanel();
    }

    void RefreshProductionModeUi()
    {
        if (productionModeButtonText != null)
            productionModeButtonText.text = GetProductionModeLabel();

        if (productionModeTitleText != null)
            productionModeTitleText.text = LocalizationManager.Get("ui.production_mode", "PRODUCTION PRIORITY");

        for (int i = 0; i < productionModeOptionLabels.Count; i++)
        {
            AssemblyPriorityMode mode = (AssemblyPriorityMode)i;
            productionModeOptionLabels[i].text = GetProductionModeLabel(mode);
        }
    }

    string GetProductionModeLabel()
    {
        AssemblyPriorityMode mode = trackedShip != null
            ? trackedShip.CurrentAssemblyPriorityMode
            : AssemblyPriorityMode.FuelFirst;
        return GetProductionModeLabel(mode);
    }

    string GetProductionModeLabel(AssemblyPriorityMode mode)
    {
        return mode switch
        {
            AssemblyPriorityMode.RepairFirst => LocalizationManager.Get("assembly.mode.repair", "Repair First"),
            AssemblyPriorityMode.AmmoFirst => LocalizationManager.Get("assembly.mode.ammo", "Ammo First"),
            _ => LocalizationManager.Get("assembly.mode.fuel", "Fuel First")
        };
    }

    void CreateQuitButton()
    {
        Button quitButton = CreateHudButton(
            hudRoot,
            "QuitButton",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-16f, -16f),
            new Vector2(120f, 40f),
            new Color(0.42f, 0.08f, 0.08f, 0.96f),
            new Color(0.58f, 0.12f, 0.12f, 1f),
            new Color(0.72f, 0.18f, 0.18f, 1f),
            out quitButtonLabel);
        quitButtonLabel.text = LocalizationManager.Get("ui.quit", "Quit");
        quitButton.onClick.AddListener(QuitGame);
    }

    void QuitGame()
    {
        GameSaveSystem.SaveCurrentGame(force: true);
        Application.Quit();
    }

    void CreateNavigatorOverlay()
    {
        navigatorRoot = CreateRect("NavigatorOverlay", hudRoot);
        Stretch(navigatorRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        scrapNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "ScrapArrow", NavigatorScrapColor);
        enemyNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "EnemyArrow", NavigatorEnemyColor);
        neutralModuleNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "NeutralModuleArrow", NavigatorNeutralModuleColor);
    }

    void CreateMobileControls()
    {
        if (!ShouldShowMobileControls())
            return;

        mobileControlsRoot = CreateRect("MobileControls", hudRoot);
        Stretch(mobileControlsRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform touchpadRoot = CreateRect("Touchpad", mobileControlsRoot);
        SetAnchored(touchpadRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 36f), new Vector2(330f, 330f));
        Image touchpadBg = touchpadRoot.gameObject.AddComponent<Image>();
        touchpadBg.sprite = GetCircleSprite();
        touchpadBg.type = Image.Type.Simple;
        touchpadBg.color = new Color(0.05f, 0.09f, 0.13f, 0.52f);
        touchpadBg.raycastTarget = true;

        CreateTouchpadZone(touchpadRoot, "ForwardZone", 0f, 50f, new Color(0.22f, 0.9f, 0.62f, 0.34f));
        CreateTouchpadZone(touchpadRoot, "RightTurnZone", 90f, 130f, new Color(0.38f, 0.72f, 1f, 0.22f));
        CreateTouchpadZone(touchpadRoot, "ReverseZone", 180f, 50f, new Color(1f, 0.34f, 0.34f, 0.32f));
        CreateTouchpadZone(touchpadRoot, "LeftTurnZone", 270f, 130f, new Color(0.38f, 0.72f, 1f, 0.22f));

        RectTransform touchpadRing = CreateRect("Ring", touchpadRoot);
        Stretch(touchpadRing, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
        Image ringImage = touchpadRing.gameObject.AddComponent<Image>();
        ringImage.sprite = GetCircleSprite();
        ringImage.type = Image.Type.Simple;
        ringImage.color = new Color(0.5f, 0.76f, 0.92f, 0.16f);
        ringImage.raycastTarget = false;

        RectTransform touchpadCore = CreateRect("Core", touchpadRoot);
        Stretch(touchpadCore, new Vector2(0.34f, 0.34f), new Vector2(0.66f, 0.66f), Vector2.zero, Vector2.zero);
        Image coreImage = touchpadCore.gameObject.AddComponent<Image>();
        coreImage.sprite = GetCircleSprite();
        coreImage.type = Image.Type.Simple;
        coreImage.color = new Color(0.1f, 0.16f, 0.2f, 0.86f);
        coreImage.raycastTarget = false;

        RectTransform thumb = CreateRect("Thumb", touchpadRoot);
        SetAnchored(thumb, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(123f, 123f));
        Image thumbImage = thumb.gameObject.AddComponent<Image>();
        thumbImage.sprite = GetCircleSprite();
        thumbImage.type = Image.Type.Simple;
        thumbImage.color = new Color(0.58f, 0.86f, 1f, 0.8f);
        thumbImage.raycastTarget = false;

        MobileTouchpadControl touchpadControl = touchpadRoot.gameObject.AddComponent<MobileTouchpadControl>();
        touchpadControl.thumb = thumb;
        touchpadControl.maxInputRadius = 123f;
        touchpadControl.thumbTravelRadius = 87f;
        touchpadControl.deadZone = 0.12f;

        RectTransform fireButtonRoot = CreateRect("FireButton", mobileControlsRoot);
        SetAnchored(fireButtonRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-42f, 42f), new Vector2(160f, 160f));
        Image fireButtonImage = fireButtonRoot.gameObject.AddComponent<Image>();
        fireButtonImage.sprite = GetCircleSprite();
        fireButtonImage.type = Image.Type.Simple;
        fireButtonImage.color = new Color(0.78f, 0.18f, 0.18f, 0.72f);
        fireButtonImage.raycastTarget = true;

        RectTransform fireInner = CreateRect("Inner", fireButtonRoot);
        Stretch(fireInner, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero);
        Image fireInnerImage = fireInner.gameObject.AddComponent<Image>();
        fireInnerImage.sprite = GetCircleSprite();
        fireInnerImage.type = Image.Type.Simple;
        fireInnerImage.color = new Color(1f, 0.72f, 0.72f, 0.22f);
        fireInnerImage.raycastTarget = false;

        MobileFireButtonControl fireButtonControl = fireButtonRoot.gameObject.AddComponent<MobileFireButtonControl>();
        fireButtonControl.targetImage = fireButtonImage;
        fireButtonControl.normalColor = new Color(0.78f, 0.18f, 0.18f, 0.72f);
        fireButtonControl.pressedColor = new Color(1f, 0.16f, 0.16f, 1f);
    }

    void CreateTouchpadZone(RectTransform parent, string name, float centerAngle, float arcDegrees, Color color)
    {
        RectTransform zone = CreateRect(name, parent);
        Stretch(zone, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

        Image zoneImage = zone.gameObject.AddComponent<Image>();
        zoneImage.sprite = GetCircleSprite();
        zoneImage.type = Image.Type.Filled;
        zoneImage.fillMethod = Image.FillMethod.Radial360;
        zoneImage.fillOrigin = (int)Image.Origin360.Top;
        zoneImage.fillAmount = Mathf.Clamp01(arcDegrees / 360f);
        zoneImage.fillClockwise = true;
        zoneImage.color = color;
        zoneImage.raycastTarget = false;

        float startAngle = centerAngle - arcDegrees * 0.5f;
        zone.localRotation = Quaternion.Euler(0f, 0f, -startAngle);
    }

    bool ShouldShowMobileControls()
    {
        if (Application.isMobilePlatform)
            return true;

#if UNITY_EDITOR
        return showMobileControlsInEditor;
#else
        return showMobileControlsOnDesktop;
#endif
    }

    void RefreshAll(bool force)
    {
        if (force)
        {
            resourceUiDirty = true;
            ammoUiDirty = true;
            inventoryUiDirty = true;
        }

        RefreshScore(force);
        RefreshAssemblyPanel();
        RefreshResourceRows();
        RefreshAmmoRows();
        RefreshInventoryRows();
        RefreshProductionModeUi();
        RefreshNavigator();
    }

    void RefreshScore(bool force)
    {
        int currentScore = trackedShip ? Mathf.RoundToInt(Mathf.Max(0f, trackedShip.totalScore)) : 0;
        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            PlayerPrefs.SetFloat(BestScorePrefsKey, bestScore);
            PlayerPrefs.Save();
        }

        if (currentScore > sessionBestScore)
            sessionBestScore = currentScore;

        int bestRounded = Mathf.RoundToInt(bestScore);
        if (!force && currentScore == lastCurrentScore && bestRounded == lastBestScore) return;

        lastCurrentScore = currentScore;
        lastBestScore = bestRounded;

        if (bestValueText != null) bestValueText.text = bestRounded.ToString();
        if (currentScoreText != null) currentScoreText.text = LocalizationManager.Format("ui.current_score", "Current {0}", currentScore);
    }

    void RefreshResourceRows()
    {
        if (resourceListRoot == null) return;

        SyncValueRows(resources, resourceListRoot, resourceRows);
        resourceUiDirty = false;
    }

    void RefreshAmmoRows()
    {
        if (ammoListRoot == null) return;

        SyncValueRows(ammoEntries, ammoListRoot, ammoRows);
        ammoUiDirty = false;
    }

    void RefreshInventoryRows()
    {
        if (inventoryListRoot == null) return;

        if (!string.IsNullOrEmpty(selectedInventoryEntryId) && FindInventory(selectedInventoryEntryId) == null)
            selectedInventoryEntryId = string.Empty;

        while (inventoryRows.Count < InventoryCapacity)
            inventoryRows.Add(CreateInventoryRow(inventoryListRoot));

        for (int i = 0; i < inventoryRows.Count; i++)
        {
            inventoryRows[i].root.SetActive(true);

            bool hasEntry = i < inventoryEntries.Count;
            InventoryEntry entry = hasEntry ? inventoryEntries[i] : null;
            bool isSelected = hasEntry && string.Equals(entry.id, selectedInventoryEntryId, StringComparison.OrdinalIgnoreCase);

            inventoryRows[i].background.color = hasEntry
                ? (isSelected ? new Color(0.16f, 0.26f, 0.34f, 1f) : new Color(0.1f, 0.16f, 0.2f, 0.96f))
                : new Color(0.05f, 0.08f, 0.11f, 0.8f);
            inventoryRows[i].icon.sprite = hasEntry && entry.iconSprite != null ? entry.iconSprite : GetSolidSprite();
            inventoryRows[i].icon.type = hasEntry && entry.iconSprite != null ? Image.Type.Simple : Image.Type.Sliced;
            inventoryRows[i].icon.preserveAspect = hasEntry && entry.iconSprite != null;
            inventoryRows[i].icon.color = hasEntry
                ? (entry.iconSprite != null ? Color.white : entry.color)
                : new Color(0.22f, 0.3f, 0.35f, 0.55f);
            inventoryRows[i].label.text = hasEntry ? entry.label : string.Empty;
            inventoryRows[i].label.gameObject.SetActive(false);
            inventoryRows[i].value.text = hasEntry && entry.amount > 1 ? $"x{entry.amount}" : string.Empty;
            inventoryRows[i].dragHandle.Configure(this, hasEntry ? entry.id : string.Empty);
        }

        bool hasItems = inventoryEntries.Count > 0;
        if (inventoryEmptyState != null) inventoryEmptyState.SetActive(!hasItems);
        if (inventoryCountText != null) inventoryCountText.text = $"{GetStoredInventoryModuleCount()} / {InventoryCapacity}";
        RefreshInventoryDetail();

        inventoryUiDirty = false;
    }

    void RefreshAssemblyPanel()
    {
        if (assemblyTitleText == null || assemblyPrimaryText == null || assemblySecondaryText == null || assemblyPreviewImage == null)
            return;

        assemblyTitleText.text = assemblyActive
            ? LocalizationManager.Get("ui.upgrading", "UPGRADING")
            : LocalizationManager.Get("ui.assembly", "ASSEMBLY");
        assemblyPrimaryText.text = assemblyPrimaryLabel;
        assemblySecondaryText.text = assemblySecondaryLabel;
        assemblySecondaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(assemblySecondaryLabel));

        if (assemblySprite != null)
        {
            assemblyPreviewImage.sprite = assemblySprite;
            assemblyPreviewImage.color = Color.white;
        }
        else
        {
            assemblyPreviewImage.sprite = GetSolidSprite();
            assemblyPreviewImage.color = assemblyActive
                ? new Color(0.34f, 0.88f, 0.67f, 1f)
                : new Color(0.2f, 0.34f, 0.29f, 1f);
        }
    }

    void RefreshInventoryDetail()
    {
        if (inventoryDetailText == null)
            return;

        InventoryEntry entry = FindInventory(selectedInventoryEntryId);
        if (entry == null || entry.prefab == null)
        {
            inventoryDetailText.text = LocalizationManager.Get("ui.inventory_empty", "Drag modules here.");
            return;
        }

        ModuleInstance module = entry.prefab.GetComponent<ModuleInstance>();
        ModuleData data = module != null ? module.data : null;
        if (module == null || data == null)
        {
            inventoryDetailText.text = entry.label;
            return;
        }

        int tier = Mathf.Max(1, data.tier + Mathf.Max(0, entry.upgradeLevel));
        List<string> lines = new List<string>(5)
        {
            LocalizationManager.Format("info.tier", "Tier: {0}", tier),
            entry.label
        };

        if (module.GetMass() > 0f)
            lines.Add(LocalizationManager.Format("info.mass", "Mass: {0}", module.GetMass().ToString("0.##")));

        if (module.GetThrust() > 0f)
            lines.Add(LocalizationManager.Format("info.thrust", "Thrust: {0}", module.GetThrust().ToString("0.##")));

        if (module.GetMaxFuel() > 0f)
            lines.Add(LocalizationManager.Format("info.fuel_cap", "Fuel Cap: {0}", module.GetMaxFuel().ToString("0.##")));

        if (module.GetDps() > 0f)
            lines.Add(LocalizationManager.Format("info.dps", "DPS: {0}", module.GetDps().ToString("0.##")));

        inventoryDetailText.text = string.Join("\n", lines);
    }

    void RefreshOverheatWarning()
    {
        if (overheatWarningText == null)
            return;

        bool showWarning = trackedShip != null && trackedShip.HasCriticalOverheatModules();
        if (!showWarning)
        {
            overheatWarningText.text = string.Empty;
            overheatWarningText.gameObject.SetActive(false);
            return;
        }

        overheatWarningText.text = LocalizationManager.Get(
            "warning.overheat_modules",
            "[Modules are overheating and losing efficiency. (Radiator needed)]");
        if (!overheatWarningText.gameObject.activeSelf)
            overheatWarningText.gameObject.SetActive(true);
    }

    void RefreshNavigator()
    {
        if (navigatorRoot == null || trackedShip == null)
        {
            SetNavigatorVisible(scrapNavigatorArrow, false);
            SetNavigatorVisible(enemyNavigatorArrow, false);
            SetNavigatorVisible(neutralModuleNavigatorArrow, false);
            return;
        }

        var coreTransform = trackedShip.GetCoreTransform();
        if (coreTransform == null)
        {
            SetNavigatorVisible(scrapNavigatorArrow, false);
            SetNavigatorVisible(enemyNavigatorArrow, false);
            SetNavigatorVisible(neutralModuleNavigatorArrow, false);
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            SetNavigatorVisible(scrapNavigatorArrow, false);
            SetNavigatorVisible(enemyNavigatorArrow, false);
            SetNavigatorVisible(neutralModuleNavigatorArrow, false);
            return;
        }

        Vector3 playerScreenPos = cam.WorldToScreenPoint(coreTransform.position);
        if (playerScreenPos.z <= 0f)
        {
            SetNavigatorVisible(scrapNavigatorArrow, false);
            SetNavigatorVisible(enemyNavigatorArrow, false);
            SetNavigatorVisible(neutralModuleNavigatorArrow, false);
            return;
        }

        UpdateNavigatorArrow(scrapNavigatorArrow, coreTransform.position, playerScreenPos, WorldSpawnDirector.GetNearestFloatingScrap(coreTransform.position));
        UpdateNavigatorArrow(enemyNavigatorArrow, coreTransform.position, playerScreenPos, WorldSpawnDirector.GetNearestEnemyShip(coreTransform.position));
        UpdateNavigatorArrow(neutralModuleNavigatorArrow, coreTransform.position, playerScreenPos, NeutralModuleSpawnDirector.GetNearestNeutralModule(coreTransform.position));
    }

    void HandleLanguageChanged(GameLanguage _)
    {
        LocalizationFontManager.RefreshActiveTexts();
        RefreshLocalizedText();
        RefreshAll(force: true);
    }

    void RefreshLocalizedText()
    {
        if (statusTitleText != null)
            statusTitleText.text = LocalizationManager.Get("ui.status", "STATUS");

        if (bestTitleText != null)
            bestTitleText.text = LocalizationManager.Get("ui.best", "BEST");

        if (inventoryTitleText != null)
            inventoryTitleText.text = LocalizationManager.Get("ui.inventory", "INVENTORY");

        if (inventoryEmptyText != null)
            inventoryEmptyText.text = LocalizationManager.Get("ui.inventory_empty", "Drag modules here.");

        if (inventoryDetailText != null && string.IsNullOrWhiteSpace(selectedInventoryEntryId))
            inventoryDetailText.text = LocalizationManager.Get("ui.inventory_empty", "Drag modules here.");

        if (quitButtonLabel != null)
            quitButtonLabel.text = LocalizationManager.Get("ui.quit", "Quit");

        RefreshProductionModeUi();

        RefreshOverheatWarning();

        var scrapEntry = FindResource(resources, "scrap");
        if (scrapEntry != null)
            scrapEntry.label = LocalizationManager.Get("resource.scrap", "Scrap");

        var fuelEntry = FindResource(resources, "fuel");
        if (fuelEntry != null)
            fuelEntry.label = LocalizationManager.Get("resource.fuel", "Fuel");

        var ammoEntry = FindResource(ammoEntries, "ammo");
        if (ammoEntry != null)
            ammoEntry.label = LocalizationManager.Get("resource.ammo", "Ammo");

        if (!assemblyActive)
        {
            if (trackedShip != null)
            {
                assemblyPrimaryLabel = trackedShip.GetFuelAssemblyPrimaryText();
                assemblySecondaryLabel = trackedShip.GetFuelAssemblySecondaryText();
            }
            else
            {
                assemblyPrimaryLabel = LocalizationManager.Get("assembly.fuel_ready", "Fuel synthesis ready");
                assemblySecondaryLabel = string.Empty;
            }
        }

        resourceUiDirty = true;
        ammoUiDirty = true;
        inventoryUiDirty = true;
        RefreshScore(force: true);
        RefreshAssemblyPanel();
    }

    void UpdateNavigatorArrow(TMP_Text arrow, Vector3 originWorld, Vector3 originScreen, Component target)
    {
        if (arrow == null || target == null)
        {
            SetNavigatorVisible(arrow, false);
            return;
        }

        Vector2 worldDelta = target.transform.position - originWorld;
        if (worldDelta.sqrMagnitude <= 0.0001f)
        {
            SetNavigatorVisible(arrow, false);
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, originScreen, null, out Vector2 localPoint))
        {
            SetNavigatorVisible(arrow, false);
            return;
        }

        Vector2 direction = worldDelta.normalized;
        arrow.rectTransform.localPosition = localPoint + direction * navigatorRadius;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        SetNavigatorVisible(arrow, true);
    }

    void SetNavigatorVisible(TMP_Text arrow, bool visible)
    {
        if (arrow != null && arrow.gameObject.activeSelf != visible)
            arrow.gameObject.SetActive(visible);
    }

    void SyncValueRows(List<ResourceEntry> source, RectTransform parent, List<ValueRow> pool)
    {
        while (pool.Count < source.Count)
            pool.Add(CreateValueRow(parent));

        for (int i = 0; i < pool.Count; i++)
        {
            bool active = i < source.Count;
            pool[i].root.SetActive(active);
            if (!active) continue;

            var entry = source[i];
            pool[i].icon.color = entry.color;
            pool[i].label.text = entry.label;
            pool[i].value.text = FormatResourceValue(entry);
        }
    }

    ValueRow CreateValueRow(RectTransform parent)
    {
        var row = CreateRect("Row", parent);
        row.sizeDelta = new Vector2(0f, 24f);

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconRect = CreateRect("Icon", row);
        iconRect.sizeDelta = new Vector2(16f, 16f);
        var icon = CreateImage(iconRect, Color.white);

        var label = CreateInlineLabel(row, "Label", 14f, new Color(0.77f, 0.87f, 0.9f, 1f), FontStyles.Normal);
        label.rectTransform.sizeDelta = new Vector2(56f, 20f);

        var value = CreateInlineLabel(row, "Value", 15f, Color.white, FontStyles.Bold);
        value.rectTransform.sizeDelta = new Vector2(96f, 20f);

        return new ValueRow
        {
            root = row.gameObject,
            icon = icon,
            label = label,
            value = value
        };
    }

    InventoryRow CreateInventoryRow(RectTransform parent)
    {
        var row = CreateRect("InventoryRow", parent);
        row.sizeDelta = new Vector2(104f, 104f);
        CreateBackPlate(row, new Color(0.05f, 0.08f, 0.11f, 0.8f));
        var background = row.GetComponent<Image>();
        if (background != null)
            background.raycastTarget = true;

        var iconRect = CreateRect("Icon", row);
        Stretch(iconRect, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
        var icon = CreateImage(iconRect, new Color(0.22f, 0.3f, 0.35f, 0.55f));
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;

        var label = CreateInlineLabel(row, "Name", 15f, Color.white, FontStyles.Normal);
        label.rectTransform.anchorMin = new Vector2(0f, 0f);
        label.rectTransform.anchorMax = new Vector2(1f, 0f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        label.rectTransform.sizeDelta = new Vector2(-12f, 18f);
        label.alignment = TextAlignmentOptions.Center;
        label.gameObject.SetActive(false);

        var value = CreateInlineLabel(row, "Count", 14f, new Color(0.78f, 0.88f, 0.92f, 1f), FontStyles.Bold);
        value.rectTransform.anchorMin = new Vector2(1f, 0f);
        value.rectTransform.anchorMax = new Vector2(1f, 0f);
        value.rectTransform.pivot = new Vector2(1f, 0f);
        value.rectTransform.anchoredPosition = new Vector2(-8f, 8f);
        value.rectTransform.sizeDelta = new Vector2(40f, 18f);
        value.alignment = TextAlignmentOptions.BottomRight;

        var dragHandle = row.gameObject.AddComponent<InventoryEntryDragHandle>();

        return new InventoryRow
        {
            root = row.gameObject,
            background = background,
            icon = icon,
            label = label,
            value = value,
            dragHandle = dragHandle
        };
    }

    void ApplyInventoryState()
    {
        if (inventoryPanel == null) return;

        inventoryPanel.sizeDelta = new Vector2(inventoryExpanded ? ExpandedInventoryWidth : CollapsedInventoryWidth, inventoryPanel.sizeDelta.y);
        if (inventoryBody != null) inventoryBody.SetActive(inventoryExpanded);
        if (inventoryCountText != null) inventoryCountText.gameObject.SetActive(inventoryExpanded);
        if (inventoryButtonText != null) inventoryButtonText.text = inventoryExpanded ? ">" : "<";
        if (productionModePanel != null) productionModePanel.gameObject.SetActive(inventoryExpanded);
        if (!inventoryExpanded)
            productionModeOptionsVisible = false;
        if (productionModeOptionsRoot != null)
            productionModeOptionsRoot.SetActive(inventoryExpanded && productionModeOptionsVisible);
    }

    ResourceEntry FindResource(List<ResourceEntry> source, string id)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (string.Equals(source[i].id, id, StringComparison.OrdinalIgnoreCase))
                return source[i];
        }

        return null;
    }

    ResourceEntry FindOrCreate(List<ResourceEntry> source, string id, string label, Color color)
    {
        var entry = FindResource(source, id);
        if (entry != null) return entry;

        entry = new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? id : label, 0f, color);
        source.Add(entry);
        return entry;
    }

    string FormatResourceValue(ResourceEntry entry)
    {
        if (entry == null)
            return "0";

        if (!string.IsNullOrWhiteSpace(entry.displayText))
            return entry.displayText;

        if (Mathf.Abs(entry.amount - Mathf.Round(entry.amount)) <= 0.001f)
            return Mathf.RoundToInt(entry.amount).ToString();

        return entry.amount.ToString("0.#");
    }

    InventoryEntry FindInventory(string id)
    {
        for (int i = 0; i < inventoryEntries.Count; i++)
        {
            if (string.Equals(inventoryEntries[i].id, id, StringComparison.OrdinalIgnoreCase))
                return inventoryEntries[i];
        }

        return null;
    }

    InventoryEntry FindOrCreateInventory(string id, string label, Color color)
    {
        var entry = FindInventory(id);
        if (entry != null) return entry;

        entry = new InventoryEntry(id, string.IsNullOrWhiteSpace(label) ? id : label, 0, color);
        inventoryEntries.Add(entry);
        return entry;
    }

    int GetStoredInventoryModuleCount()
    {
        int total = 0;
        for (int i = 0; i < inventoryEntries.Count; i++)
            total += Mathf.Max(0, inventoryEntries[i].amount);

        return total;
    }

    string BuildInventoryId(GameObject prefab, ModuleData data, int upgradeLevel)
    {
        string baseId = data != null && !string.IsNullOrWhiteSpace(data.name)
            ? data.name
            : (prefab != null ? prefab.name : "module");
        return $"{baseId}:{Mathf.Max(0, upgradeLevel)}:{nextInventoryEntrySerial++}";
    }

    string GetInventoryLabel(GameObject prefab, ModuleData data)
    {
        if (data != null)
        {
            string formatted = ModuleInstance.FormatDisplayName(data.displayName, data.tier);
            return LocalizationManager.GetModuleText(data.localizationKey, formatted);
        }

        return prefab != null ? prefab.name : "Module";
    }

    Color GetInventoryColor(ModuleData data)
    {
        if (data == null)
            return new Color(0.8f, 0.84f, 0.88f, 1f);

        return data.type switch
        {
            ModuleType.Engine => new Color(0.35f, 0.78f, 1f, 1f),
            ModuleType.Weapon => new Color(1f, 0.37f, 0.27f, 1f),
            ModuleType.FuelTank => new Color(0.34f, 0.9f, 0.6f, 1f),
            ModuleType.Reactor => new Color(1f, 0.82f, 0.29f, 1f),
            ModuleType.Radiator => new Color(0.66f, 0.83f, 1f, 1f),
            ModuleType.Repair => new Color(0.94f, 0.53f, 0.89f, 1f),
            ModuleType.Structure => new Color(0.72f, 0.76f, 0.82f, 1f),
            ModuleType.SolarPanel => new Color(0.56f, 0.95f, 0.48f, 1f),
            _ => new Color(0.8f, 0.84f, 0.88f, 1f)
        };
    }

    Sprite ResolveModuleSprite(Transform moduleTransform)
    {
        if (moduleTransform == null)
            return null;

        SpriteRenderer renderer = moduleTransform.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    GameObject ResolveInventoryPrefab(ModuleInstance module)
    {
        if (module == null || module.data == null)
            return null;

        if (module.data.type == ModuleType.Weapon)
            return WorldSpawnDirector.GetModulePrefabByType(ModuleType.Weapon);

        return WorldSpawnDirector.GetModulePrefabByType(module.data.type);
    }

    TMP_FontAsset ResolveFontAsset()
    {
        TMP_FontAsset localizedFont = LocalizationFontManager.GetUiFontAsset();
        if (localizedFont != null)
            return localizedFont;

        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    Image CreateImage(RectTransform parent, Color color)
    {
        var image = parent.gameObject.AddComponent<Image>();
        image.sprite = GetSolidSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    void CreateBackPlate(RectTransform parent, Color color)
    {
        CreateImage(parent, color);
    }

    Button CreateHudButton(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color normalColor,
        Color highlightedColor,
        Color pressedColor,
        out TMP_Text label)
    {
        RectTransform buttonRect = CreateRect(name, parent);
        SetAnchored(buttonRect, anchorMin, anchorMax, pivot, anchoredPosition, size);
        Image image = CreateImage(buttonRect, normalColor);
        image.raycastTarget = true;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = highlightedColor;
        colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, normalColor.a * 0.45f);
        button.colors = colors;

        label = CreateLabel(buttonRect, string.Empty, 16f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.5f, 0.5f), FontStyles.Bold);
        label.rectTransform.sizeDelta = size - new Vector2(16f, 8f);
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    TMP_Text CreateLabel(Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 pivot, FontStyles style)
    {
        var rect = CreateRect("Text", parent);
        SetAnchored(rect, pivot, pivot, pivot, anchoredPosition, new Vector2(200f, 28f));

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.text = text;
        label.alignment = alignment;
        label.fontStyle = style;
        label.raycastTarget = false;
        return label;
    }

    TMP_Text CreateInlineLabel(Transform parent, string name, float fontSize, Color color, FontStyles style)
    {
        var rect = CreateRect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        return label;
    }

    TMP_Text CreateNavigatorArrow(Transform parent, string name, Color color)
    {
        var rect = CreateRect(name, parent);
        rect.sizeDelta = new Vector2(30f, 30f);

        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.fontSize = 34f;
        label.color = color;
        label.text = "▲";
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.gameObject.SetActive(false);
        return label;
    }

    void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.anchoredPosition = Vector2.zero;
    }

    void SetAnchored(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    static Sprite GetSolidSprite()
    {
        if (solidSprite != null) return solidSprite;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        solidSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        solidSprite.name = "HUDSolidSprite";
        return solidSprite;
    }

    static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 2f;
        float feather = 2.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / feather);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        circleSprite.name = "HUDCircleSprite";
        return circleSprite;
    }
}

