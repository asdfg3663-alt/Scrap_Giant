using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldFeedbackRuntime : MonoBehaviour
{
    sealed class FloatingLabel
    {
        public RectTransform rect;
        public TMP_Text text;
        public Vector3 worldPosition;
        public float life;
        public float duration;
        public float riseDistance;
        public Color baseColor;
    }

    static WorldFeedbackRuntime instance;
    static readonly List<FloatingLabel> activeLabels = new();

    Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("WorldFeedbackRuntime");
        instance = go.AddComponent<WorldFeedbackRuntime>();
    }

    public static void ShowScrapGain(Vector3 worldPosition, int amount)
    {
        if (amount <= 0)
            return;

        EnsureInstance();
        if (instance == null)
            return;

        instance.SpawnLabel(
            worldPosition,
            $"+{amount} {LocalizationManager.Get("resource.scrap", "Scrap")}",
            new Color(1f, 0.82f, 0.34f, 1f),
            1.05f,
            54f);
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
        EnsureCanvas();
    }

    void LateUpdate()
    {
        if (activeLabels.Count == 0)
            return;

        Camera cam = Camera.main;
        for (int i = activeLabels.Count - 1; i >= 0; i--)
        {
            FloatingLabel label = activeLabels[i];
            if (label == null || label.rect == null || label.text == null)
            {
                activeLabels.RemoveAt(i);
                continue;
            }

            label.life += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(label.life / Mathf.Max(0.01f, label.duration));
            if (t >= 1f)
            {
                Destroy(label.rect.gameObject);
                activeLabels.RemoveAt(i);
                continue;
            }

            Vector3 screen = cam != null ? cam.WorldToScreenPoint(label.worldPosition) : Vector3.zero;
            if (screen.z < 0f)
                screen *= -1f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screen,
                null,
                out Vector2 anchored);

            anchored.y += Mathf.Lerp(0f, label.riseDistance, t);
            label.rect.anchoredPosition = anchored;

            Color color = label.baseColor;
            color.a = Mathf.Lerp(1f, 0f, t);
            label.text.color = color;
        }
    }

    void EnsureCanvas()
    {
        if (canvas != null)
            return;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>().enabled = false;
    }

    void SpawnLabel(Vector3 worldPosition, string textValue, Color color, float duration, float riseDistance)
    {
        EnsureCanvas();

        GameObject go = new GameObject("FloatingWorldLabel", typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.sizeDelta = new Vector2(360f, 40f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        LocalizationFontManager.ApplyFont(text);
        text.text = textValue;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;

        activeLabels.Add(new FloatingLabel
        {
            rect = rect,
            text = text,
            worldPosition = worldPosition,
            life = 0f,
            duration = duration,
            riseDistance = riseDistance,
            baseColor = color
        });
    }
}
