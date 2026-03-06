using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHudRuntime : MonoBehaviour
{
    const string BestScorePrefsKey = "ScrapGiant.HUD.BestMassScore";
    const float ExpandedInventoryWidth = 280f;
    const float CollapsedInventoryWidth = 48f;

    static PlayerHudRuntime instance;
    static Sprite solidSprite;

    [Serializable]
    public class ResourceEntry
    {
        public string id;
        public string label;
        public int amount;
        public Color color;

        public ResourceEntry(string id, string label, int amount, Color color)
        {
            this.id = id;
            this.label = label;
            this.amount = amount;
            this.color = color;
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

    GameObject inventoryBody;
    GameObject inventoryEmptyState;

    TMP_Text bestValueText;
    TMP_Text currentScoreText;
    TMP_Text assemblyStatusText;
    TMP_Text inventoryCountText;
    TMP_Text inventoryButtonText;

    float bestScore;
    int lastCurrentScore = int.MinValue;
    int lastBestScore = int.MinValue;
    bool inventoryExpanded = true;
    bool hudBuilt;
    bool resourceUiDirty;
    bool ammoUiDirty;
    bool inventoryUiDirty;

    string assemblyLabel = "Idle";
    bool assemblyActive;

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
        RefreshScore(force: true);
    }

    public int GetResourceAmount(string id)
    {
        var entry = FindResource(resources, id);
        return entry != null ? entry.amount : 0;
    }

    public bool HasResource(string id, int amount)
    {
        return GetResourceAmount(id) >= Mathf.Max(0, amount);
    }

    public bool TryConsumeResource(string id, int amount)
    {
        amount = Mathf.Max(0, amount);
        if (amount == 0) return true;

        var entry = FindResource(resources, id);
        if (entry == null || entry.amount < amount)
            return false;

        entry.amount -= amount;
        resourceUiDirty = true;
        return true;
    }

    public void SetResource(string id, string label, int amount, Color color)
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
        }

        resourceUiDirty = true;
    }

    public void AddResource(string id, string label, int amount, Color color)
    {
        var entry = FindOrCreate(resources, id, label, color);
        entry.amount += amount;
        resourceUiDirty = true;
    }

    public void SetAmmo(string id, string label, int amount, Color color)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var entry = FindResource(ammoEntries, id);
        if (entry == null)
            ammoEntries.Add(new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? "Ammo" : label, amount, color));
        else
        {
            entry.label = string.IsNullOrWhiteSpace(label) ? entry.label : label;
            entry.amount = amount;
            entry.color = color;
        }

        ammoUiDirty = true;
    }

    public void SetAssemblyState(bool active, string label)
    {
        assemblyActive = active;
        assemblyLabel = string.IsNullOrWhiteSpace(label) ? "Idle" : label;

        if (assemblyStatusText != null)
            assemblyStatusText.text = assemblyActive ? $"Assembling: {assemblyLabel}" : $"Assembly: {assemblyLabel}";
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
        if (resources.Count == 0)
            resources.Add(new ResourceEntry("scrap", "Scrap", 240, new Color(0.97f, 0.63f, 0.22f, 1f)));

        if (ammoEntries.Count == 0)
            ammoEntries.Add(new ResourceEntry("ammo", "Ammo", 120, new Color(0.96f, 0.32f, 0.24f, 1f)));

        assemblyActive = false;
        assemblyLabel = "Idle";

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

        hudBuilt = true;
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
        SetAnchored(assemblyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(320f, 0f), new Vector2(260f, 82f));
        CreateBackPlate(assemblyPanel, new Color(0.05f, 0.1f, 0.12f, 0.9f));
        BuildAssemblyPanel(assemblyPanel);
    }

    void BuildResourcePanel(RectTransform panel)
    {
        CreateLabel(panel, "STATUS", 15f, new Color(0.68f, 0.84f, 0.86f, 1f), TextAlignmentOptions.TopLeft, new Vector2(18f, -10f), new Vector2(0f, 1f), FontStyles.Bold);

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
        CreateLabel(panel, "BEST", 15f, new Color(0.94f, 0.97f, 0.84f, 1f), TextAlignmentOptions.Top, new Vector2(0f, -8f), new Vector2(0.5f, 1f), FontStyles.Bold);
        bestValueText = CreateLabel(panel, "0", 34f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, -2f), new Vector2(0.5f, 0.5f), FontStyles.Bold);
        currentScoreText = CreateLabel(panel, "Current 0", 13f, new Color(0.76f, 0.85f, 0.89f, 1f), TextAlignmentOptions.Bottom, new Vector2(0f, 10f), new Vector2(0.5f, 0f), FontStyles.Normal);
    }

    void BuildAssemblyPanel(RectTransform panel)
    {
        var iconRoot = CreateRect("AssemblyIcon", panel);
        SetAnchored(iconRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
        CreateImage(iconRoot, new Color(0.34f, 0.88f, 0.67f, 1f));

        var inner = CreateRect("AssemblyIconInner", iconRoot);
        Stretch(inner, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), Vector2.zero, Vector2.zero);
        CreateImage(inner, new Color(0.07f, 0.18f, 0.14f, 1f));

        CreateLabel(panel, "ASSEMBLY", 15f, new Color(0.72f, 0.94f, 0.88f, 1f), TextAlignmentOptions.TopLeft, new Vector2(56f, -12f), new Vector2(0f, 1f), FontStyles.Bold);
        assemblyStatusText = CreateLabel(panel, "Assembly: Idle", 15f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(56f, 8f), new Vector2(0f, 0.5f), FontStyles.Normal);
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

        CreateLabel(inventoryPanel, "INVENTORY", 18f, new Color(0.79f, 0.89f, 0.93f, 1f), TextAlignmentOptions.TopLeft, new Vector2(18f, -14f), new Vector2(0f, 1f), FontStyles.Bold);
        inventoryCountText = CreateLabel(inventoryPanel, "0 parts", 13f, new Color(0.58f, 0.76f, 0.82f, 1f), TextAlignmentOptions.TopRight, new Vector2(-18f, -18f), new Vector2(1f, 1f), FontStyles.Normal);

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
        var emptyText = CreateLabel(inventoryEmptyState.transform, "Stored modules will appear here.", 15f, new Color(0.56f, 0.68f, 0.74f, 1f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(0.5f, 0.5f), FontStyles.Italic);
        Stretch(emptyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        ApplyInventoryState();
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
        SetAssemblyState(assemblyActive, assemblyLabel);
        RefreshResourceRows();
        RefreshAmmoRows();
        RefreshInventoryRows();
    }

    void RefreshScore(bool force)
    {
        int currentScore = trackedShip ? Mathf.RoundToInt(Mathf.Max(0f, trackedShip.totalMass)) : 0;
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
        if (currentScoreText != null) currentScoreText.text = $"Current {currentScore}";
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
        if (inventoryCountText != null) inventoryCountText.text = $"{inventoryEntries.Count} parts";

        inventoryUiDirty = false;
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
            pool[i].value.text = entry.amount.ToString();
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
        label.rectTransform.sizeDelta = new Vector2(70f, 20f);

        var value = CreateInlineLabel(row, "Value", 15f, Color.white, FontStyles.Bold);
        value.rectTransform.sizeDelta = new Vector2(56f, 20f);

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

        entry = new ResourceEntry(id, string.IsNullOrWhiteSpace(label) ? id : label, 0, color);
        source.Add(entry);
        return entry;
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
