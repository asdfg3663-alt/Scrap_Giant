using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MobileTouchpadControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform thumb;
    public float maxInputRadius = 72f;
    public float thumbTravelRadius = 52f;
    [Range(0f, 1f)] public float deadZone = 0.12f;

    RectTransform rectTransform;
    int activePointerId = int.MinValue;

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

        Vector2 normalized = maxInputRadius > 0.001f
            ? Vector2.ClampMagnitude(centeredPoint / maxInputRadius, 1f)
            : Vector2.zero;

        if (normalized.magnitude < deadZone)
            normalized = Vector2.zero;

        MobileShipInput.SetMoveVector(normalized);

        if (thumb != null)
            thumb.anchoredPosition = normalized * thumbTravelRadius;
    }

    void ResetInput()
    {
        activePointerId = int.MinValue;
        MobileShipInput.SetMoveVector(Vector2.zero);

        if (thumb != null)
            thumb.anchoredPosition = Vector2.zero;
    }
}
