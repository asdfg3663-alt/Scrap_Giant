using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class ModuleSelection : MonoBehaviour
{
    public static ModuleInstance Selected { get; private set; }
    public static FloatingScrap SelectedScrap { get; private set; }

    void Update()
    {
        if (GameRuntimeState.GameplayBlocked)
        {
            Selected = null;
            SelectedScrap = null;
            return;
        }

        // 마우스 클릭 (New Input System)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectAtScreen(Mouse.current.position.ReadValue());
        }

        // 터치(모바일)도 같이 지원
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TrySelectAtScreen(Touchscreen.current.primaryTouch.position.ReadValue());
        }
    }

    void TrySelectAtScreen(Vector2 screenPos)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        var cam = Camera.main;
        if (!cam) return;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector2 p = new Vector2(world.x, world.y);

        var hit = Physics2D.Raycast(p, Vector2.zero);
        if (hit.collider != null)
        {
            var module = hit.collider.GetComponentInParent<ModuleInstance>();
            if (module != null)
            {
                Selected = module;
                SelectedScrap = null;
                return;
            }

            var scrap = hit.collider.GetComponentInParent<FloatingScrap>();
            if (scrap != null)
            {
                Selected = null;
                SelectedScrap = scrap;
                return;
            }
        }

        Selected = null;
        SelectedScrap = null;
    }
}
