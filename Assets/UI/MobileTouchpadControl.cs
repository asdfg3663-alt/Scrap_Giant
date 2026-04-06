using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MobileTouchpadControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform thumb;
    public float maxInputRadius = 72f;
    public float thumbTravelRadius = 52f;
    [Range(0f, 1f)] public float deadZone = 0.12f;
    public float logicalRotationDegrees;

    RectTransform rectTransform;
    int activePointerId = int.MinValue;
    Vector2 currentScreenNormalized;
    bool hasActiveInput;

    void Update()
    {
        if (!hasActiveInput)
            return;

        ApplyCurrentInput();
    }

    void Awake()
    {
        rectTransform = transform as RectTransform;
    }

    void OnDisable()
    {
        ResetInput();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue)
            return;

        activePointerId = eventData.pointerId;
        UpdateInput(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        UpdateInput(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId)
            return;

        ResetInput();
    }

    void UpdateInput(PointerEventData eventData)
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rectTransform == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Vector2 centerOffset = rectTransform.rect.center;
        Vector2 centeredPoint = localPoint - centerOffset;

        currentScreenNormalized = maxInputRadius > 0.001f
            ? Vector2.ClampMagnitude(centeredPoint / maxInputRadius, 1f)
            : Vector2.zero;
        hasActiveInput = true;

        ApplyCurrentInput();
    }

    void ApplyCurrentInput()
    {
        Vector2 logicalNormalized = Quaternion.Euler(0f, 0f, -logicalRotationDegrees) * currentScreenNormalized;
        logicalNormalized = Vector2.ClampMagnitude(logicalNormalized, 1f);

        if (logicalNormalized.magnitude < deadZone)
            logicalNormalized = Vector2.zero;

        MobileShipInput.SetMoveVector(logicalNormalized);

        if (thumb != null)
            thumb.anchoredPosition = currentScreenNormalized * thumbTravelRadius;
    }

    void ResetInput()
    {
        activePointerId = int.MinValue;
        currentScreenNormalized = Vector2.zero;
        hasActiveInput = false;
        MobileShipInput.SetMoveVector(Vector2.zero);

        if (thumb != null)
            thumb.anchoredPosition = Vector2.zero;
    }
}
