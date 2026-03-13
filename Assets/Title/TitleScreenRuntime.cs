using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed partial class TitleScreenRuntime : MonoBehaviour
{
    sealed class HowToPage
    {
        public string tabKey;
        public string tabFallback;
        public string titleKey;
        public string titleFallback;
        public string bodyKey;
        public string bodyFallback;
        public Color previewColor;
    }

    static TitleScreenRuntime instance;
    static Sprite solidSprite;
    static Sprite roundedButtonSprite;

    readonly List<Button> languageButtons = new();
    readonly List<TMP_Text> languageButtonLabels = new();
    readonly List<Button> howToTabButtons = new();
    readonly List<TMP_Text> howToTabLabels = new();

    readonly HowToPage[] howToPages =
    {
        new HowToPage
        {
            tabKey = "title.howto.tab_resources",
            tabFallback = "Resources",
            titleKey = "title.howto.resources_title",
            titleFallback = "Scrap, Fuel, and Ammo",
            bodyKey = "title.howto.resources_body",
            bodyFallback = "Collect Scrap from floating wreckage and destroyed modules. When fuel drops low, your ship can synthesize Fuel from Scrap. Ammo supports laser sustain and future weapon types.",
            previewColor = new Color(0.95f, 0.63f, 0.26f, 1f)
        },
        new HowToPage
        {
            tabKey = "title.howto.tab_build",
            tabFallback = "Build",
            titleKey = "title.howto.build_title",
            titleFallback = "Build and Upgrade",
            bodyKey = "title.howto.build_body",
            bodyFallback = "Drag nearby neutral modules onto your ship, expand your frame from fuel tanks and cores, and spend Scrap to upgrade modules. Bigger ships raise your score and also enemy threat.",
            previewColor = new Color(0.37f, 0.84f, 0.95f, 1f)
        },
        new HowToPage
        {
            tabKey = "title.howto.tab_combat",
            tabFallback = "Combat",
            titleKey = "title.howto.combat_title",
            titleFallback = "Combat, Heat, and Repair",
            bodyKey = "title.howto.combat_body",
            bodyFallback = "Laser weapons and power plants build heat while running. Radiators cool the whole ship, and repair modules spend Scrap to restore damaged modules in the assembly queue.",
            previewColor = new Color(0.98f, 0.32f, 0.27f, 1f)
        }
    };

    Canvas canvas;
    RawImage videoImage;
    VideoPlayer videoPlayer;
    RenderTexture videoTexture;
    Texture2D fallbackTexture;
    RectTransform modalRoot;
    RectTransform previewPulse;

    TMP_Text highScoreLabel;
    TMP_Text playButtonLabel;
    TMP_Text playSaveLabel;
    TMP_Text optionsButtonLabel;
    TMP_Text howToButtonLabel;
    TMP_Text shopButtonLabel;
    TMP_Text creditsButtonLabel;

    TMP_Text optionsTitleLabel;
    TMP_Text languageSectionLabel;
    TMP_Text languageModeLabel;
    TMP_Text masterVolumeLabel;
    TMP_Text bgmVolumeLabel;
    TMP_Text sfxVolumeLabel;
    TMP_Text fullscreenLabel;
    TMP_Text fullscreenValueLabel;
    TMP_Text deleteSaveButtonLabel;

    TMP_Text howToTitleLabel;
    TMP_Text howToPreviewTitleLabel;
    TMP_Text howToPreviewBodyLabel;

    TMP_Text shopTitleLabel;
    TMP_Text shopBodyLabel;

    TMP_Text creditsTitleLabel;
    TMP_Text creditsBodyLabel;

    Slider masterSlider;
    Slider bgmSlider;
    Slider sfxSlider;
    Button fullscreenButton;
    Button deleteSaveButton;

    GameObject optionsPanel;
    GameObject howToPanel;
    GameObject shopPanel;
    GameObject creditsPanel;
    GameObject modalDimmer;

    int currentHowToPageIndex;
    float howToPreviewPhase;
    AudioSource audioSource;
    AudioClip buttonClickClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null || GameSaveSystem.HasStartedSession)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "SampleScene")
            return;

        GameObject go = new GameObject("TitleScreenRuntime");
        instance = go.AddComponent<TitleScreenRuntime>();
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
        GameRuntimeState.SetGameplayBlocked(true);
        Time.timeScale = 0f;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        buttonClickClip = CreateButtonClickClip();
        EnsureEventSystem();
        BuildUi();
        PrepareBackgroundVideo();
    }

    void OnEnable()
    {
        LocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    void OnDisable()
    {
        LocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.Stop();

        if (videoTexture != null)
            videoTexture.Release();

        Time.timeScale = 1f;
        GameRuntimeState.SetGameplayBlocked(false);
    }

    void Update()
    {
        UpdateHowToPreview();
    }

    void BuildUi()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = CreateRect("TitleRoot", transform);
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform backgroundRect = CreateRect("VideoBackground", root);
        Stretch(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        videoImage = backgroundRect.gameObject.AddComponent<RawImage>();
        videoImage.color = Color.white;

        RectTransform dimRect = CreateRect("BackgroundDim", root);
        Stretch(dimRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateImage(dimRect, new Color(0.02f, 0.04f, 0.07f, 0.38f));

        highScoreLabel = CreateText(root, string.Empty, 33f, new Color(0.94f, 0.22f, 0.2f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);
        SetAnchored(highScoreLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(900f, 54f));

        RectTransform buttonBar = CreateRect("ButtonBar", root);
        SetAnchored(buttonBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(980f, 112f));
        HorizontalLayoutGroup layout = buttonBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform playRoot = CreateRect("PlayButtonRoot", buttonBar);
        playRoot.sizeDelta = new Vector2(176f, 112f);
        VerticalLayoutGroup playLayout = playRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        playLayout.spacing = 8f;
        playLayout.childAlignment = TextAnchor.MiddleCenter;
        playLayout.childControlWidth = false;
        playLayout.childControlHeight = false;
        playLayout.childForceExpandWidth = false;
        playLayout.childForceExpandHeight = false;

        Button playButton = CreateMenuButton(playRoot, out playButtonLabel, OnPlayPressed);
        playButton.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 58f);
        playSaveLabel = CreateText(playRoot, string.Empty, 14f, new Color(0.88f, 0.92f, 0.97f, 0.95f), FontStyles.Normal, TextAlignmentOptions.Center);
        playSaveLabel.rectTransform.sizeDelta = new Vector2(200f, 34f);

        Button optionsButton = CreateMenuButton(buttonBar, out optionsButtonLabel, () => ShowPanel(optionsPanel));
        Button howToButton = CreateMenuButton(buttonBar, out howToButtonLabel, () => ShowPanel(howToPanel));
        Button shopButton = CreateMenuButton(buttonBar, out shopButtonLabel, ShowShop);
        Button creditsButton = CreateMenuButton(buttonBar, out creditsButtonLabel, () => ShowPanel(creditsPanel));

        optionsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 58f);
        howToButton.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 58f);
        shopButton.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 58f);
        creditsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(176f, 58f);

        modalDimmer = CreateRect("ModalDimmer", root).gameObject;
        Stretch(modalDimmer.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dimImage = CreateImage(modalDimmer.GetComponent<RectTransform>(), new Color(0.01f, 0.03f, 0.04f, 0.74f));
        dimImage.raycastTarget = true;
        Button dimButton = modalDimmer.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(HidePanels);
        modalDimmer.SetActive(false);

        modalRoot = CreateRect("ModalRoot", root);
        SetAnchored(modalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(1040f, 620f));

        optionsPanel = BuildModalPanel("OptionsPanel", out optionsTitleLabel);
        BuildOptionsPanel(GetPanelBody(optionsPanel.transform));

        howToPanel = BuildModalPanel("HowToPanel", out howToTitleLabel);
        BuildHowToPanel(GetPanelBody(howToPanel.transform));

        shopPanel = BuildModalPanel("ShopPanel", out shopTitleLabel);
        BuildShopPanel(GetPanelBody(shopPanel.transform));

        creditsPanel = BuildModalPanel("CreditsPanel", out creditsTitleLabel);
        BuildCreditsPanel(GetPanelBody(creditsPanel.transform));

        HidePanels();
        RefreshLocalizedText();
    }

    void PrepareBackgroundVideo()
    {
        videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        videoTexture.Create();
        videoImage.texture = videoTexture;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;

        string videoPath = Path.Combine(Application.dataPath, "Title", "Title.mp4");
        if (File.Exists(videoPath))
        {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = videoPath;
            videoPlayer.prepareCompleted += _ => videoPlayer.Play();
            videoPlayer.Prepare();
            return;
        }

        string fallbackPath = Path.Combine(Application.dataPath, "Title", "Title img.jpg");
        if (!File.Exists(fallbackPath))
            return;

        fallbackTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        fallbackTexture.LoadImage(File.ReadAllBytes(fallbackPath));
        videoImage.texture = fallbackTexture;
    }

    GameObject BuildModalPanel(string name, out TMP_Text titleText)
    {
        RectTransform panel = CreateRect(name, modalRoot);
        Stretch(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        CreateImage(panel, new Color(0.03f, 0.07f, 0.1f, 0.96f));

        RectTransform titleBar = CreateRect("TitleBar", panel);
        SetAnchored(titleBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 68f));
        CreateImage(titleBar, new Color(0.07f, 0.15f, 0.18f, 0.98f));

        titleText = CreateText(titleBar, "Title", 28f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        SetAnchored(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(520f, 40f));

        Button closeButton = CreateMenuButton(titleBar, out TMP_Text closeLabel, HidePanels);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-24f, 0f);
        closeRect.sizeDelta = new Vector2(72f, 42f);
        closeLabel.text = "X";

        RectTransform body = CreateRect("Body", panel);
        Stretch(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 28f), new Vector2(-28f, -90f));
        VerticalLayoutGroup bodyLayout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = 16f;
        bodyLayout.padding = new RectOffset(0, 0, 10, 0);
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = false;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        return panel.gameObject;
    }

    void BuildOptionsPanel(Transform parent)
    {
        RectTransform languageBlock = CreateBlock(parent, 164f);
        languageSectionLabel = CreateSectionLabel(languageBlock, "Language");
        languageModeLabel = CreateBodyText(languageBlock, string.Empty, 16f);
        languageModeLabel.enableWordWrapping = true;
        languageModeLabel.alignment = TextAlignmentOptions.Left;
        SetAnchored(languageModeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -48f), new Vector2(-36f, 24f));

        RectTransform langRow = CreateRect("LanguageRow", languageBlock);
        langRow.anchorMin = new Vector2(0f, 0f);
        langRow.anchorMax = new Vector2(1f, 0f);
        langRow.pivot = new Vector2(0.5f, 0f);
        langRow.anchoredPosition = new Vector2(0f, 20f);
        langRow.sizeDelta = new Vector2(-36f, 84f);
        GridLayoutGroup langLayout = langRow.gameObject.AddComponent<GridLayoutGroup>();
        langLayout.cellSize = new Vector2(170f, 38f);
        langLayout.spacing = new Vector2(10f, 8f);
        langLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        langLayout.constraintCount = 3;
        langLayout.childAlignment = TextAnchor.UpperLeft;

        IReadOnlyList<GameLanguage> languages = LocalizationManager.GetSupportedLanguages();
        for (int i = 0; i < languages.Count; i++)
        {
            GameLanguage lang = languages[i];
            Button button = CreateMenuButton(langRow, out TMP_Text buttonLabel, () => SetLanguage(lang));
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 38f);
            languageButtons.Add(button);
            languageButtonLabels.Add(buttonLabel);
        }

        RectTransform audioBlock = CreateBlock(parent, 196f);
        masterVolumeLabel = CreateBodyText(audioBlock, "Master Volume", 18f);
        RectTransform masterRow = CreateOptionRow(audioBlock, "MasterVolumeRow", 30f);
        masterVolumeLabel.rectTransform.SetParent(masterRow, false);
        Stretch(masterVolumeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(280f, 14f));
        masterVolumeLabel.alignment = TextAlignmentOptions.Left;
        masterSlider = CreateSlider(masterRow, new Vector2(210f, 0f), value => GameOptions.MasterVolume = value);

        bgmVolumeLabel = CreateBodyText(audioBlock, "BGM Volume", 16f);
        RectTransform bgmRow = CreateOptionRow(audioBlock, "BgmVolumeRow", 82f);
        bgmVolumeLabel.rectTransform.SetParent(bgmRow, false);
        Stretch(bgmVolumeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(170f, 14f));
        bgmVolumeLabel.alignment = TextAlignmentOptions.Left;
        bgmSlider = CreateSlider(bgmRow, new Vector2(210f, 0f), value => GameOptions.BgmVolume = value);

        sfxVolumeLabel = CreateBodyText(audioBlock, "SFX Volume", 16f);
        RectTransform sfxRow = CreateOptionRow(audioBlock, "SfxVolumeRow", 134f);
        sfxVolumeLabel.rectTransform.SetParent(sfxRow, false);
        Stretch(sfxVolumeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(170f, 14f));
        sfxVolumeLabel.alignment = TextAlignmentOptions.Left;
        sfxSlider = CreateSlider(sfxRow, new Vector2(210f, 0f), value => GameOptions.SfxVolume = value);

        RectTransform displayBlock = CreateBlock(parent, 100f);
        fullscreenLabel = CreateSectionLabel(displayBlock, "Display");
        fullscreenButton = CreateMenuButton(displayBlock, out fullscreenValueLabel, ToggleFullscreen);
        RectTransform fullscreenRect = fullscreenButton.GetComponent<RectTransform>();
        fullscreenRect.anchorMin = new Vector2(0f, 0f);
        fullscreenRect.anchorMax = new Vector2(0f, 0f);
        fullscreenRect.pivot = new Vector2(0f, 0f);
        fullscreenRect.anchoredPosition = new Vector2(18f, 18f);
        fullscreenRect.sizeDelta = new Vector2(310f, 48f);

        deleteSaveButton = CreateMenuButton(displayBlock, out deleteSaveButtonLabel, DeleteSaveFile);
        RectTransform deleteRect = deleteSaveButton.GetComponent<RectTransform>();
        deleteRect.anchorMin = new Vector2(1f, 0f);
        deleteRect.anchorMax = new Vector2(1f, 0f);
        deleteRect.pivot = new Vector2(1f, 0f);
        deleteRect.anchoredPosition = new Vector2(-18f, 18f);
        deleteRect.sizeDelta = new Vector2(240f, 48f);
        ApplyButtonPalette(deleteSaveButton, new Color(0.43f, 0.08f, 0.08f, 0.98f), new Color(0.58f, 0.12f, 0.12f, 1f), new Color(0.72f, 0.18f, 0.18f, 1f));
    }

    void BuildHowToPanel(Transform parent)
    {
        RectTransform previewBlock = CreateBlock(parent, 250f);

        RectTransform previewFrame = CreateRect("PreviewFrame", previewBlock);
        SetAnchored(previewFrame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(250f, 210f));
        CreateImage(previewFrame, new Color(0.07f, 0.13f, 0.16f, 0.92f));

        previewPulse = CreateRect("PreviewPulse", previewFrame);
        previewPulse.sizeDelta = new Vector2(72f, 72f);
        CreateImage(previewPulse, howToPages[0].previewColor);

        RectTransform textBlock = CreateRect("TextBlock", previewBlock);
        SetAnchored(textBlock, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(280f, -10f), new Vector2(-280f, -10f));

        howToPreviewTitleLabel = CreateText(textBlock, "How To Play", 26f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Stretch(howToPreviewTitleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -42f));

        howToPreviewBodyLabel = CreateText(textBlock, string.Empty, 18f, new Color(0.82f, 0.9f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        howToPreviewBodyLabel.enableWordWrapping = true;
        Stretch(howToPreviewBodyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -56f));

        RectTransform tabRow = CreateRect("HowToTabs", parent);
        tabRow.sizeDelta = new Vector2(0f, 58f);
        HorizontalLayoutGroup tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 12f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;

        for (int i = 0; i < howToPages.Length; i++)
        {
            int index = i;
            Button tabButton = CreateMenuButton(tabRow, out TMP_Text tabLabel, () => SetHowToPage(index));
            tabButton.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 44f);
            howToTabButtons.Add(tabButton);
            howToTabLabels.Add(tabLabel);
        }

        SetHowToPage(0);
    }

    void BuildShopPanel(Transform parent)
    {
        RectTransform block = CreateBlock(parent, 260f);
        shopBodyLabel = CreateText(block, string.Empty, 20f, new Color(0.86f, 0.92f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        shopBodyLabel.enableWordWrapping = true;
        Stretch(shopBodyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), Vector2.zero);
    }

    void BuildCreditsPanel(Transform parent)
    {
        RectTransform block = CreateBlock(parent, 300f);
        creditsBodyLabel = CreateText(block, string.Empty, 20f, new Color(0.86f, 0.92f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        creditsBodyLabel.enableWordWrapping = true;
        Stretch(creditsBodyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
    }

    RectTransform CreateBlock(Transform parent, float height)
    {
        RectTransform block = CreateRect("Block", parent);
        block.sizeDelta = new Vector2(0f, height);
        CreateImage(block, new Color(0.05f, 0.11f, 0.14f, 0.9f));
        return block;
    }

    Transform GetPanelBody(Transform panelRoot)
    {
        Transform body = panelRoot.Find("Body");
        return body != null ? body : panelRoot;
    }

    RectTransform CreateOptionRow(Transform parent, string name, float topOffset)
    {
        RectTransform row = CreateRect(name, parent);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -topOffset);
        row.sizeDelta = new Vector2(-36f, 48f);
        return row;
    }

    void ApplyButtonPalette(Button button, Color normal, Color highlighted, Color pressed)
    {
        Image image = button != null ? button.GetComponent<Image>() : null;
        if (image == null)
            return;

        image.color = normal;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        button.colors = colors;
    }

    TMP_Text CreateSectionLabel(Transform parent, string text)
    {
        TMP_Text label = CreateText(parent, text, 20f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -46f), new Vector2(-18f, -18f));
        return label;
    }

    TMP_Text CreateBodyText(Transform parent, string text, float size)
    {
        TMP_Text label = CreateText(parent, text, size, new Color(0.82f, 0.9f, 0.95f, 1f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Stretch(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -42f), new Vector2(-18f, -18f));
        return label;
    }

    Slider CreateSlider(Transform parent, Vector2 anchoredPosition, UnityEngine.Events.UnityAction<float> onChanged)
    {
        RectTransform root = CreateRect("Slider", parent);
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(1f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.anchoredPosition = anchoredPosition;
        root.sizeDelta = new Vector2(-308f, 24f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform background = CreateRect("Background", root);
        Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image backgroundImage = CreateImage(background, new Color(0.14f, 0.22f, 0.26f, 1f));
        backgroundImage.raycastTarget = false;

        RectTransform fillArea = CreateRect("FillArea", root);
        Stretch(fillArea, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 5f), new Vector2(-8f, -5f));
        RectTransform fill = CreateRect("Fill", fillArea);
        Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image fillImage = CreateImage(fill, new Color(0.34f, 0.83f, 0.94f, 1f));
        fillImage.raycastTarget = false;

        RectTransform handleArea = CreateRect("HandleArea", root);
        Stretch(handleArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(18f, 32f);
        Image handleImage = CreateImage(handle, Color.white);

        slider.targetGraphic = handleImage;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    Button CreateMenuButton(Transform parent, out TMP_Text label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform buttonRect = CreateRect("Button", parent);
        buttonRect.sizeDelta = new Vector2(176f, 58f);
        Image image = CreateRoundedButtonImage(buttonRect, new Color(0.08f, 0.16f, 0.2f, 0.94f));
        image.raycastTarget = true;

        Outline outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.92f, 0.98f, 0.26f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.16f, 0.29f, 0.34f, 1f);
        colors.pressedColor = new Color(0.28f, 0.44f, 0.49f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            PlayButtonClick();
            onClick?.Invoke();
        });

        label = CreateText(buttonRect, "Button", 20f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 6f), new Vector2(-8f, -6f));
        return button;
    }

    TMP_Text CreateText(Transform parent, string text, float fontSize, Color color, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect("Text", parent);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        LocalizationFontManager.ApplyFont(label);
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }

    void RefreshLocalizedText()
    {
        playButtonLabel.text = LocalizationManager.Get("title.play", "Play");
        optionsButtonLabel.text = LocalizationManager.Get("title.options", "Options");
        howToButtonLabel.text = LocalizationManager.Get("title.how_to_play", "How To Play");
        shopButtonLabel.text = LocalizationManager.Get("title.shop", "Shop");
        creditsButtonLabel.text = LocalizationManager.Get("title.credits", "Credits");

        optionsTitleLabel.text = LocalizationManager.Get("title.options", "Options");
        languageSectionLabel.text = LocalizationManager.Get("title.options.language", "Language");
        masterVolumeLabel.text = LocalizationManager.Get("title.options.master_volume", "Master Volume");
        bgmVolumeLabel.text = LocalizationManager.Get("title.options.bgm_volume", "BGM Volume");
        sfxVolumeLabel.text = LocalizationManager.Get("title.options.sfx_volume", "SFX Volume");
        fullscreenLabel.text = LocalizationManager.Get("title.options.display", "Display");
        deleteSaveButtonLabel.text = LocalizationManager.Get("title.options.delete_save", "Delete Save");

        howToTitleLabel.text = LocalizationManager.Get("title.how_to_play", "How To Play");
        shopTitleLabel.text = LocalizationManager.Get("title.shop", "Shop");
        creditsTitleLabel.text = LocalizationManager.Get("title.credits", "Credits");

        for (int i = 0; i < languageButtonLabels.Count; i++)
        {
            if (i < LocalizationManager.GetSupportedLanguages().Count)
                languageButtonLabels[i].text = LocalizationManager.GetLanguageLabel(LocalizationManager.GetSupportedLanguages()[i]);
        }

        for (int i = 0; i < howToTabLabels.Count; i++)
        {
            if (i < howToPages.Length)
                howToTabLabels[i].text = LocalizationManager.Get(howToPages[i].tabKey, howToPages[i].tabFallback);
        }

        bool hasSavedLanguage = LocalizationManager.HasSavedLanguagePreference();
        languageModeLabel.text = hasSavedLanguage
            ? LocalizationManager.Get("title.options.language_saved", "Saved language preference is active.")
            : LocalizationManager.Format("title.options.language_auto", "Language auto-detected: {0}", LocalizationManager.GetLanguageLabel(LocalizationManager.CurrentLanguage));

        fullscreenValueLabel.text = GameOptions.Fullscreen
            ? LocalizationManager.Get("title.options.fullscreen_on", "Fullscreen: ON")
            : LocalizationManager.Get("title.options.fullscreen_off", "Fullscreen: OFF");

        shopBodyLabel.text = LocalizationManager.Get("title.shop.body", "Shop is prepared as a future Android in-app purchase entry point. The live store flow is not connected yet.");
        creditsBodyLabel.text = LocalizationManager.Get("title.credits.body", "Scrap Giant Prototype\n\nDesign, code, and systems are currently integrated for rapid iteration.\n\nTitle, options, tutorials, and store hooks are now ready for expansion.");

        RefreshSaveInfo();
        RefreshOptionValues();
        SetHowToPage(currentHowToPageIndex);
        UpdateLanguageButtonStates();
    }

    void RefreshOptionValues()
    {
        masterSlider.SetValueWithoutNotify(GameOptions.MasterVolume);
        bgmSlider.SetValueWithoutNotify(GameOptions.BgmVolume);
        sfxSlider.SetValueWithoutNotify(GameOptions.SfxVolume);
    }

    void RefreshSaveInfo()
    {
        if (!GameSaveSystem.HasSaveFile())
        {
            playSaveLabel.gameObject.SetActive(false);
        }
        else
        {
            playSaveLabel.gameObject.SetActive(true);
            playSaveLabel.text = LocalizationManager.Format("title.save_score", "Saved score {0}", Mathf.RoundToInt(GameSaveSystem.GetSavedScore()));
        }

        if (highScoreLabel != null)
            highScoreLabel.text = LocalizationManager.Format("title.best_score", "Best score {0}", PlayerHudRuntime.GetRecordedBestScore());
    }

    void UpdateLanguageButtonStates()
    {
        IReadOnlyList<GameLanguage> languages = LocalizationManager.GetSupportedLanguages();
        for (int i = 0; i < languageButtons.Count; i++)
        {
            if (i >= languages.Count)
                continue;

            Color targetColor = languages[i] == LocalizationManager.CurrentLanguage
                ? new Color(0.23f, 0.48f, 0.58f, 1f)
                : new Color(0.08f, 0.16f, 0.2f, 0.94f);

            Image image = languageButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = targetColor;
        }
    }

    void SetLanguage(GameLanguage language)
    {
        LocalizationManager.SetLanguage(language);
    }

    void SetHowToPage(int index)
    {
        currentHowToPageIndex = Mathf.Clamp(index, 0, howToPages.Length - 1);
        HowToPage page = howToPages[currentHowToPageIndex];

        howToPreviewTitleLabel.text = LocalizationManager.Get(page.titleKey, page.titleFallback);
        howToPreviewBodyLabel.text = LocalizationManager.Get(page.bodyKey, page.bodyFallback);

        Image pulseImage = previewPulse != null ? previewPulse.GetComponent<Image>() : null;
        if (pulseImage != null)
            pulseImage.color = page.previewColor;

        for (int i = 0; i < howToTabButtons.Count; i++)
        {
            Image image = howToTabButtons[i].GetComponent<Image>();
            if (image == null)
                continue;

            image.color = i == currentHowToPageIndex
                ? new Color(0.23f, 0.48f, 0.58f, 1f)
                : new Color(0.08f, 0.16f, 0.2f, 0.94f);
        }
    }

    void UpdateHowToPreview()
    {
        if (previewPulse == null || howToPanel == null || !howToPanel.activeSelf)
            return;

        howToPreviewPhase += Time.unscaledDeltaTime * 1.6f;
        float x = Mathf.Sin(howToPreviewPhase) * 68f;
        float y = Mathf.Cos(howToPreviewPhase * 1.7f) * 18f;
        float scale = 0.85f + Mathf.Sin(howToPreviewPhase * 2.3f) * 0.08f;
        previewPulse.anchoredPosition = new Vector2(125f + x, -108f + y);
        previewPulse.localScale = new Vector3(scale, scale, 1f);
    }

    void ToggleFullscreen()
    {
        GameOptions.Fullscreen = !GameOptions.Fullscreen;
        RefreshLocalizedText();
    }

    void DeleteSaveFile()
    {
        GameSaveSystem.DeleteSave();
        RefreshLocalizedText();
    }

    void ShowShop()
    {
        if (TitleShopService.OpenStore())
            return;

        ShowPanel(shopPanel);
    }

    void OnPlayPressed()
    {
        GameSaveSystem.MarkSessionStarted();

        if (GameSaveSystem.HasSaveFile())
            GameSaveSystem.LoadIntoCurrentScene();
        else
            GameSaveSystem.SaveCurrentGame(force: true);

        Time.timeScale = 1f;
        GameRuntimeState.SetGameplayBlocked(false);
        Destroy(gameObject);
    }

    void ShowPanel(GameObject panel)
    {
        optionsPanel.SetActive(panel == optionsPanel);
        howToPanel.SetActive(panel == howToPanel);
        shopPanel.SetActive(panel == shopPanel);
        creditsPanel.SetActive(panel == creditsPanel);
        modalDimmer.SetActive(panel != null);
    }

    void HidePanels()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (howToPanel != null) howToPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (modalDimmer != null) modalDimmer.SetActive(false);
    }

    void HandleLanguageChanged(GameLanguage _)
    {
        LocalizationFontManager.RefreshActiveTexts();
        RefreshLocalizedText();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    static Image CreateImage(RectTransform parent, Color color)
    {
        Image image = parent.gameObject.AddComponent<Image>();
        image.sprite = GetSolidSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Image CreateRoundedButtonImage(RectTransform parent, Color color)
    {
        Image image = parent.gameObject.AddComponent<Image>();
        image.sprite = GetRoundedButtonSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
    }

    static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    static Sprite GetSolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        solidSprite.name = "TitleSolidSprite";
        return solidSprite;
    }

    static Sprite GetRoundedButtonSprite()
    {
        if (roundedButtonSprite != null)
            return roundedButtonSprite;

        const int width = 64;
        const int height = 32;
        const int radius = 10;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int nearestX = Mathf.Clamp(x, radius, width - radius - 1);
                int nearestY = Mathf.Clamp(y, radius, height - radius - 1);
                float dx = x - nearestX;
                float dy = y - nearestY;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool inside = dist <= radius;
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        roundedButtonSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedButtonSprite.name = "TitleRoundedButtonSprite";
        return roundedButtonSprite;
    }

    void PlayButtonClick()
    {
        if (audioSource != null && buttonClickClip != null)
            audioSource.PlayOneShot(buttonClickClip, GameOptions.SfxVolume);
    }

    static AudioClip CreateButtonClickClip()
    {
        const int sampleRate = 22050;
        const float duration = 0.06f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (i / (float)sampleCount);
            samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 880f) * envelope * 0.12f;
        }

        AudioClip clip = AudioClip.Create("TitleButtonClick", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
