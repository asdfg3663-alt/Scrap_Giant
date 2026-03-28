using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MobileFireButtonControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Image targetImage;
    public Color normalColor = new Color(0.78f, 0.18f, 0.18f, 0.72f);
    public Color pressedColor = new Color(1f, 0.2f, 0.2f, 1f);

    bool isPressed;

    void Awake()
    {
        ApplyVisual(normalColor);
    }

    void OnDisable()
    {
        isPressed = false;
        MobileShipInput.SetFireHeld(false);
        ApplyVisual(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        MobileShipInput.SetFireHeld(true);
        ApplyVisual(pressedColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPressed)
            Release();
    }

    void Release()
    {
        isPressed = false;
        MobileShipInput.SetFireHeld(false);
        ApplyVisual(normalColor);
    }

    void ApplyVisual(Color color)
    {
        if (targetImage != null)
            targetImage.color = color;
    }
}
