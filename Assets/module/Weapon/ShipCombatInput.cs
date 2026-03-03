using UnityEngine;

public class ShipCombatInput : MonoBehaviour
{
    public static bool FireHeld { get; private set; }

    void Update()
    {
        FireHeld = Input.GetKey(KeyCode.Space);
    }

    // 모바일 UI 버튼에서 호출용 (PointerDown/Up에 연결)
    public void SetFireHeld(bool held) => FireHeld = held;
}