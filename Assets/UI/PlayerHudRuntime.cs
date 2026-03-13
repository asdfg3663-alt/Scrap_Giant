using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        public InventoryEntry(string id, string label, int amount, Color color)
        {
            this.id = id;
            this.label = label;
            this.amount = amount;
            this.color = color;
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
        public Image icon;
        public TMP_Text label;
        public TMP_Text value;
    }

    ShipStats trackedShip;
    TMP_FontAsset fontAsset;

    RectTransform hudRoot;
    RectTransform resourceListRoot;
    RectTransform ammoListRoot;
    RectTransform inventoryListRoot;
    RectTransform inventoryPanel;
    RectTransform navigatorRoot;

    GameObject inventoryBody;
    GameObject inventoryEmptyState;

    Image assemblyPreviewImage;
    TMP_Text statusTitleText;
    TMP_Text bestTitleText;
    TMP_Text bestValueText;
    TMP_Text currentScoreText;
    TMP_Text assemblyTitleText;
    TMP_Text assemblyPrimaryText;
    TMP_Text assemblySecondaryText;
    TMP_Text inventoryTitleText;
    TMP_Text inventoryCountText;
    TMP_Text inventoryButtonText;
    TMP_Text inventoryEmptyText;
    TMP_Text scrapNavigatorArrow;
    TMP_Text enemyNavigatorArrow;
    TMP_Text neutralModuleNavigatorArrow;

    float bestScore;
    int lastCurrentScore = int.MinValue;
    int lastBestScore = int.MinValue;
    bool inventoryExpanded = true;
    bool hudBuilt;
    bool resourceUiDirty;
    bool ammoUiDirty;
    bool inventoryUiDirty;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        solidSprite = null;
    }

    public static PlayerHudRuntime Instance => instance;
    public ShipStats TrackedShip => trackedShip;

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

    public void ToggleInventory()
    {
        inventoryExpanded = !inventoryExpanded;
        ApplyInventoryState();
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
        CreateNavigatorOverlay();

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
        Stretch(inventoryListRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var layout = inventoryListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        inventoryEmptyState = CreateRect("EmptyState", bodyRect).gameObject;
        inventoryEmptyText = CreateLabel(inventoryEmptyState.transform, LocalizationManager.Get("ui.inventory_empty", "Stored modules will appear here."), 15f, new Color(0.56f, 0.68f, 0.74f, 1f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.5f, 0.5f), FontStyles.Italic);
        Stretch(inventoryEmptyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        ApplyInventoryState();
    }

    void CreateNavigatorOverlay()
    {
        navigatorRoot = CreateRect("NavigatorOverlay", hudRoot);
        Stretch(navigatorRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        scrapNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "ScrapArrow", NavigatorScrapColor);
        enemyNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "EnemyArrow", NavigatorEnemyColor);
        neutralModuleNavigatorArrow = CreateNavigatorArrow(navigatorRoot, "NeutralModuleArrow", NavigatorNeutralModuleColor);
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

        while (inventoryRows.Count < inventoryEntries.Count)
            inventoryRows.Add(CreateInventoryRow(inventoryListRoot));

        for (int i = 0; i < inventoryRows.Count; i++)
        {
            bool active = i < inventoryEntries.Count;
            inventoryRows[i].root.SetActive(active);
            if (!active) continue;

            var entry = inventoryEntries[i];
            inventoryRows[i].icon.color = entry.color;
            inventoryRows[i].label.text = entry.label;
            inventoryRows[i].value.text = $"x{entry.amount}";
        }

        bool hasItems = inventoryEntries.Count > 0;
        if (inventoryEmptyState != null) inventoryEmptyState.SetActive(!hasItems);
        if (inventoryCountText != null) inventoryCountText.text = LocalizationManager.Format("ui.inventory_parts", "{0} parts", inventoryEntries.Count);

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
            inventoryEmptyText.text = LocalizationManager.Get("ui.inventory_empty", "Stored modules will appear here.");

        var scrapEntry = FindResource(resources, "scrap");
        if (scrapEntry != null)
            scrapEntry.label = LocalizationManager.Get("resource.scrap", "Scrap");

        var fuelEntry = FindResource(resources, "fuel");
        if (fuelEntry != null)
            fuelEntry.label = LocalizationManager.Get("resource.fuel", "Fuel");

        var ammoEntry = FindResource(ammoEntries, "ammo");
        if (ammoEntry != null)
            ammoEntry.label = LocalizationManager.Get("resource.ammo", "Ammo");

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
        row.sizeDelta = new Vector2(0f, 34f);
        CreateBackPlate(row, new Color(0.07f, 0.11f, 0.14f, 0.88f));

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 7, 7);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var iconRect = CreateRect("Icon", row);
        iconRect.sizeDelta = new Vector2(20f, 20f);
        var icon = CreateImage(iconRect, new Color(0.8f, 0.84f, 0.88f, 1f));

        var label = CreateInlineLabel(row, "Name", 15f, Color.white, FontStyles.Normal);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.rectTransform.sizeDelta = new Vector2(150f, 20f);

        var spacer = CreateRect("Spacer", row);
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var value = CreateInlineLabel(row, "Count", 14f, new Color(0.78f, 0.88f, 0.92f, 1f), FontStyles.Bold);
        value.rectTransform.sizeDelta = new Vector2(44f, 20f);

        return new InventoryRow
        {
            root = row.gameObject,
            icon = icon,
            label = label,
            value = value
        };
    }

    void ApplyInventoryState()
    {
        if (inventoryPanel == null) return;

        inventoryPanel.sizeDelta = new Vector2(inventoryExpanded ? ExpandedInventoryWidth : CollapsedInventoryWidth, inventoryPanel.sizeDelta.y);
        if (inventoryBody != null) inventoryBody.SetActive(inventoryExpanded);
        if (inventoryCountText != null) inventoryCountText.gameObject.SetActive(inventoryExpanded);
        if (inventoryButtonText != null) inventoryButtonText.text = inventoryExpanded ? ">" : "<";
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
}

