using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverRuntime : MonoBehaviour
{
    static GameOverRuntime instance;

    Canvas canvas;
    TMP_Text messageLabel;
    TMP_Text scoreLabel;
    bool isShowing;
    float finishAtUnscaledTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("GameOverRuntime");
        instance = go.AddComponent<GameOverRuntime>();
        DontDestroyOnLoad(go);
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
    }

    void Update()
    {
        if (!isShowing || Time.unscaledTime < finishAtUnscaledTime)
            return;

        isShowing = false;
        TitleInterstitialAdService.ShowIfAvailable(ReturnToTitle);
    }

    public static void TriggerPlayerGameOver()
    {
        if (instance == null)
            Bootstrap();

        instance.ShowGameOver();
    }

    void ShowGameOver()
    {
        if (isShowing)
            return;

        isShowing = true;
        GameRuntimeState.SetGameplayBlocked(true);
        Time.timeScale = 0f;

        EnsureUi();
        messageLabel.gameObject.SetActive(true);
        scoreLabel.gameObject.SetActive(true);
        messageLabel.text = LocalizationManager.Get("title.game_over", "Run Complete");
        scoreLabel.text = LocalizationManager.Format("title.game_over_score", "Best score {0}", PlayerHudRuntime.GetSessionBestScore());
        finishAtUnscaledTime = Time.unscaledTime + 3f;

        GameSaveSystem.SaveCurrentGame(force: true);
        GameSaveSystem.EndSession(deleteSave: true);
    }

    void ReturnToTitle()
    {
        if (messageLabel != null) messageLabel.gameObject.SetActive(false);
        if (scoreLabel != null) scoreLabel.gameObject.SetActive(false);
        Time.timeScale = 1f;
        GameRuntimeState.SetGameplayBlocked(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void EnsureUi()
    {
        if (canvas != null)
            return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = CreateRect("GameOverRoot", transform);
        Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.05f, 0.82f);

        messageLabel = CreateCenteredText(root, 44f, FontStyles.Bold, new Vector2(0f, 24f));
        scoreLabel = CreateCenteredText(root, 26f, FontStyles.Normal, new Vector2(0f, -32f));
    }

    TMP_Text CreateCenteredText(Transform parent, float size, FontStyles style, Vector2 anchoredPos)
    {
        RectTransform rect = CreateRect("Text", parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(1000f, 64f);

        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        LocalizationFontManager.ApplyFont(label);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = Color.white;
        return label;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
    }
}
